using Microsoft.AspNetCore.Mvc;

namespace Basic.Controllers;

[Route("files")]
public class FilesController : Controller
{
    [HttpPost("single")]
    public IActionResult PostFile(IFormFile file)
    {
        throw new NotImplementedException();
    }

    [HttpPost("multiple")]
    public IActionResult PostFiles(IFormFileCollection files)
    {
        throw new NotImplementedException();
    }

    [HttpPost("form-with-file")]
    public IActionResult PostFormWithFile([FromForm] FormWithFile formWithFile)
    {
        throw new NotImplementedException();
    }

    [HttpGet("{name}")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "text/plain", "application/zip")]
    public FileResult GetFile(string name)
    {
        var stream = new MemoryStream();

        var writer = new StreamWriter(stream);
        writer.WriteLine("Hello world!");
        writer.Flush();
        stream.Position = 0;

        var contentType = name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? "application/zip" : "text/plain";

        return File(stream, contentType, name);
    }
}

public class FormWithFile
{
    public string Name { get; set; }

    public IFormFile File { get; set; }
}
