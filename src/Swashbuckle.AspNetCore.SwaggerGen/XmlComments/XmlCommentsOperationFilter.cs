﻿using System.Reflection;
using System.Xml.XPath;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Swashbuckle.AspNetCore.SwaggerGen;

public class XmlCommentsOperationFilter : IOperationFilter
{
    private readonly IReadOnlyDictionary<string, XPathNavigator> _xmlDocMembers;
    private readonly SwaggerGeneratorOptions _options;

    public XmlCommentsOperationFilter(XPathDocument xmlDoc)
        : this(XmlCommentsDocumentHelper.CreateMemberDictionary(xmlDoc), null)
    {
    }

    [ActivatorUtilitiesConstructor]
    internal XmlCommentsOperationFilter(IReadOnlyDictionary<string, XPathNavigator> xmlDocMembers, SwaggerGeneratorOptions options)
    {
        _xmlDocMembers = xmlDocMembers;
        _options = options;
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo == null)
        {
            return;
        }

        // If method is from a constructed generic type, look for comments from the generic type method
        var targetMethod = context.MethodInfo.DeclaringType.IsConstructedGenericType
            ? context.MethodInfo.GetUnderlyingGenericTypeMethod()
            : context.MethodInfo;

        if (targetMethod != null)
        {
            ApplyControllerTags(operation, targetMethod.DeclaringType);
            ApplyMethodTags(operation, targetMethod);
        }
    }

    private void ApplyControllerTags(OpenApiOperation operation, Type controllerType)
    {
        var typeMemberName = XmlCommentsNodeNameHelper.GetMemberNameForType(controllerType);

        if (_xmlDocMembers.TryGetValue(typeMemberName, out var methodNode))
        {
            var responseNodes = methodNode.SelectChildren("response");
            ApplyResponseTags(operation, responseNodes);
        }
    }

    private void ApplyMethodTags(OpenApiOperation operation, MethodInfo methodInfo)
    {
        var methodMemberName = XmlCommentsNodeNameHelper.GetMemberNameForMethod(methodInfo);

        if (!_xmlDocMembers.TryGetValue(methodMemberName, out var methodNode))
        {
            return;
        }

        var summaryNode = methodNode.SelectFirstChild("summary");
        if (summaryNode != null)
        {
            operation.Summary = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml, _options?.XmlCommentEndOfLine);
        }

        var remarksNode = methodNode.SelectFirstChild("remarks");
        if (remarksNode != null)
        {
            operation.Description = XmlCommentsTextHelper.Humanize(remarksNode.InnerXml, _options?.XmlCommentEndOfLine);
        }

        var responseNodes = methodNode.SelectChildren("response");
        ApplyResponseTags(operation, responseNodes);
    }

    private void ApplyResponseTags(OpenApiOperation operation, XPathNodeIterator responseNodes)
    {
        while (responseNodes.MoveNext())
        {
            var code = responseNodes.Current.GetAttribute("code");
            if (!operation.Responses.TryGetValue(code, out var response))
            {
                response = new OpenApiResponse();
                operation.Responses[code] = response;
            }

            response.Description = XmlCommentsTextHelper.Humanize(responseNodes.Current.InnerXml, _options?.XmlCommentEndOfLine);
        }
    }
}
