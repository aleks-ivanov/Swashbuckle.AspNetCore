﻿using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Swashbuckle.AspNetCore.SwaggerGen;

public static partial class XmlCommentsTextHelper
{
    public static string Humanize(string text)
    {
        return Humanize(text, null);
    }

    public static string Humanize(string text, string xmlCommentEndOfLine)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        // Call DecodeXml last to avoid entities like &lt; and &gt; breaking valid XML

        return text
            .NormalizeIndentation(xmlCommentEndOfLine)
            .HumanizeRefTags()
            .HumanizeHrefTags()
            .HumanizeCodeTags()
            .HumanizeMultilineCodeTags(xmlCommentEndOfLine)
            .HumanizeParaTags()
            .HumanizeBrTags(xmlCommentEndOfLine) // Must be called after HumanizeParaTags() so that it replaces any additional <br> tags
            .DecodeXml();
    }

    private static string NormalizeIndentation(this string text, string xmlCommentEndOfLine)
    {
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        string padding = GetCommonLeadingWhitespace(lines);

        int padLen = padding?.Length ?? 0;

        // Remove leading padding from each line
        for (int i = 0, l = lines.Length; i < l; ++i)
        {
            string line = lines[i].TrimEnd('\r'); // Remove trailing '\r'

#if NET
            if (padLen != 0 && line.Length >= padLen && line[..padLen] == padding)
            {
                line = line[padLen..];
            }
#else
            if (padLen != 0 && line.Length >= padLen && line.Substring(0, padLen) == padding)
            {
                line = line.Substring(padLen);
            }
#endif

            lines[i] = line;
        }

        // Remove leading empty lines, but not all leading padding
        // Remove all trailing whitespace, regardless
        return string.Join(EndOfLine(xmlCommentEndOfLine), lines.SkipWhile(string.IsNullOrWhiteSpace)).TrimEnd();
    }

    private static string GetCommonLeadingWhitespace(string[] lines)
    {
        if (null == lines)
        {
            throw new ArgumentException("lines");
        }

        if (lines.Length == 0)
        {
            return null;
        }

        string[] nonEmptyLines = [.. lines.Where(x => !string.IsNullOrWhiteSpace(x))];

        if (nonEmptyLines.Length < 1)
        {
            return null;
        }

        int padLength = 0;

        // Use the first line as a seed, and see what is shared over all nonEmptyLines
        string seed = nonEmptyLines[0];
        for (int i = 0, l = seed.Length; i < l; ++i)
        {
            if (!char.IsWhiteSpace(seed, i))
            {
                break;
            }

            if (nonEmptyLines.Any(line => line[i] != seed[i]))
            {
                break;
            }

            ++padLength;
        }

        if (padLength > 0)
        {
#if NET
            return seed[..padLength];
#else
            return seed.Substring(0, padLength);
#endif
        }

        return null;
    }

    private static string HumanizeRefTags(this string text)
    {
        return RefTag().Replace(text, (match) => match.Groups["display"].Value);
    }

    private static string HumanizeHrefTags(this string text)
    {
        return HrefTag().Replace(text, m => $"[{m.Groups[2].Value}]({m.Groups[1].Value})");
    }

    private static string HumanizeCodeTags(this string text)
    {
        return CodeTag().Replace(text, (match) => "`" + match.Groups["display"].Value + "`");
    }

    private static string HumanizeMultilineCodeTags(this string text, string xmlCommentEndOfLine)
    {
        return MultilineCodeTag().Replace(text, match =>
        {
            var codeText = match.Groups["display"].Value;

            if (LineBreaks().IsMatch(codeText))
            {
                var builder = new StringBuilder().Append("```");

#if NET
                if (!codeText.StartsWith('\r') && !codeText.StartsWith('\n'))
#else
                if (!codeText.StartsWith("\r") && !codeText.StartsWith("\n"))
#endif
                {
                    builder.Append(EndOfLine(xmlCommentEndOfLine));
                }

                builder.Append(RemoveCommonLeadingWhitespace(codeText, xmlCommentEndOfLine));

#if NET
                if (!codeText.EndsWith('\n'))
#else
                if (!codeText.EndsWith("\n"))
#endif
                {
                    builder.Append(EndOfLine(xmlCommentEndOfLine));
                }

                builder.Append("```");
                return DoubleUpLineBreaks().Replace(builder.ToString(), EndOfLine(xmlCommentEndOfLine));
            }

            return $"```{codeText}```";
        });
    }

    private static string HumanizeParaTags(this string text)
    {
        return ParaTag().Replace(text, match => "<br>" + match.Groups["display"].Value.Trim());
    }

    private static string HumanizeBrTags(this string text, string xmlCommentEndOfLine)
    {
        return BrTag().Replace(text, _ => EndOfLine(xmlCommentEndOfLine));
    }

    private static string DecodeXml(this string text)
    {
        return WebUtility.HtmlDecode(text);
    }

    private static string RemoveCommonLeadingWhitespace(string input, string xmlCommentEndOfLine)
    {
        var lines = input.Split(["\r\n", "\n"], StringSplitOptions.None);
        var padding = GetCommonLeadingWhitespace(lines);

        if (string.IsNullOrEmpty(padding))
        {
            return input;
        }

        var minLeadingSpaces = padding.Length;
        var builder = new StringBuilder();

        foreach (var line in lines)
        {
            builder.Append(string.IsNullOrWhiteSpace(line)
                ? line
#if NET
                : line[minLeadingSpaces..]);
#else
                : line.Substring(minLeadingSpaces));
#endif

            builder.Append(EndOfLine(xmlCommentEndOfLine));
        }

        return builder.ToString();
    }

    internal static string EndOfLine(string xmlCommentEndOfLine)
    {
        return xmlCommentEndOfLine ?? Environment.NewLine;
    }

    private const string RefTagPattern = @"<(see|paramref) (name|cref|langword)=""([TPF]{1}:)?(?<display>.+?)"" ?/>";
    private const string CodeTagPattern = @"<c>(?<display>.+?)</c>";
    private const string MultilineCodeTagPattern = @"<code>(?<display>.+?)</code>";
    private const string ParaTagPattern = @"<para>(?<display>.+?)</para>";
    private const string HrefPattern = @"<see\s+href=\""([^""]*)\"">\s*(.*?)\s*<\/see>";
    private const string BrPattern = @"(<br ?\/?>)"; // handles <br>, <br/>, <br />
    private const string LineBreaksPattern = @"\r?\n";
    private const string DoubleUpLineBreaksPattern = @"(\r?\n){2,}";

#if NET
    [GeneratedRegex(RefTagPattern)]
    private static partial Regex RefTag();

    [GeneratedRegex(CodeTagPattern)]
    private static partial Regex CodeTag();

    [GeneratedRegex(MultilineCodeTagPattern, RegexOptions.Singleline)]
    private static partial Regex MultilineCodeTag();

    [GeneratedRegex(ParaTagPattern, RegexOptions.Singleline)]
    private static partial Regex ParaTag();

    [GeneratedRegex(HrefPattern, RegexOptions.Singleline)]
    private static partial Regex HrefTag();

    [GeneratedRegex(BrPattern)]
    private static partial Regex BrTag();

    [GeneratedRegex(LineBreaksPattern)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(DoubleUpLineBreaksPattern)]
    private static partial Regex DoubleUpLineBreaks();
#else
    private static readonly Regex _refTag = new(RefTagPattern);
    private static readonly Regex _codeTag = new(CodeTagPattern);
    private static readonly Regex _multilineCodeTag = new(MultilineCodeTagPattern, RegexOptions.Singleline);
    private static readonly Regex _paraTag = new(ParaTagPattern, RegexOptions.Singleline);
    private static readonly Regex _hrefTag = new(HrefPattern);
    private static readonly Regex _brTag = new(BrPattern);
    private static readonly Regex _lineBreaks = new(LineBreaksPattern);
    private static readonly Regex _doubleUpLineBreaks = new(DoubleUpLineBreaksPattern);

    private static Regex RefTag() => _refTag;
    private static Regex CodeTag() => _codeTag;
    private static Regex MultilineCodeTag() => _multilineCodeTag;
    private static Regex ParaTag() => _paraTag;
    private static Regex HrefTag() => _hrefTag;
    private static Regex BrTag() => _brTag;
    private static Regex LineBreaks() => _lineBreaks;
    private static Regex DoubleUpLineBreaks() => _doubleUpLineBreaks;
#endif
}
