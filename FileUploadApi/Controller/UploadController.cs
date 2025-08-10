using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FileUploadApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public UploadController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost("folder")]
    public async Task<IActionResult> UploadFiles([FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest("No files provided.");

        var tempPath = Path.Combine(_env.ContentRootPath, "UploadedFiles", "Temp");

        if (!Directory.Exists(tempPath))
            Directory.CreateDirectory(tempPath);

        var summary = new Dictionary<string, int>(); // year -> count

        // Save files temporarily
        foreach (var file in files)
        {
            var tempFilePath = Path.Combine(tempPath, file.FileName);
            using var stream = new FileStream(tempFilePath, FileMode.Create);
            await file.CopyToAsync(stream);
        }

        // Process each file and move to year folder
        foreach (var file in files)
        {
            var tempFilePath = Path.Combine(tempPath, file.FileName);
            try
            {
                string jsonContent = await System.IO.File.ReadAllTextAsync(tempFilePath);

                using var doc = JsonDocument.Parse(jsonContent);

                int year = ExtractYearFromJson(doc);

                string yearFolderName = year > 0 ? year.ToString() : "UnknownYear";

                var yearFolderPath = Path.Combine(_env.ContentRootPath, "UploadedFiles", yearFolderName);
                if (!Directory.Exists(yearFolderPath))
                    Directory.CreateDirectory(yearFolderPath);

                var destFilePath = Path.Combine(yearFolderPath, file.FileName);

                System.IO.File.Move(tempFilePath, destFilePath, overwrite: true);

                if (!summary.ContainsKey(yearFolderName))
                    summary[yearFolderName] = 0;
                summary[yearFolderName]++;
            }
            catch
            {
                var unknownPath = Path.Combine(_env.ContentRootPath, "UploadedFiles", "UnknownYear");
                if (!Directory.Exists(unknownPath))
                    Directory.CreateDirectory(unknownPath);

                var unknownDestPath = Path.Combine(unknownPath, file.FileName);
                System.IO.File.Move(tempFilePath, unknownDestPath, overwrite: true);

                if (!summary.ContainsKey("UnknownYear"))
                    summary["UnknownYear"] = 0;
                summary["UnknownYear"]++;
            }
        }

        return Ok(new { Message = "Files uploaded and sorted by year.", Summary = summary });
    }

    private int ExtractYearFromJson(JsonDocument doc)
{
    if (doc.RootElement.TryGetProperty("info", out JsonElement infoElement))
    {
        if (infoElement.TryGetProperty("dates", out JsonElement datesElement) &&
            datesElement.ValueKind == JsonValueKind.Array &&
            datesElement.GetArrayLength() > 0)
        {
            var dateStr = datesElement[0].GetString();
            if (DateTime.TryParse(dateStr, out DateTime date))
            {
                return date.Year;
            }
        }
    }

    // fallback to older logic if needed
    if (doc.RootElement.TryGetProperty("match_date", out JsonElement dateElement))
    {
        var dateStr = dateElement.GetString();
        if (DateTime.TryParse(dateStr, out DateTime date))
        {
            return date.Year;
        }
    }

    if (doc.RootElement.TryGetProperty("year", out JsonElement yearElement))
    {
        if (yearElement.TryGetInt32(out int year))
            return year;
    }

    return 0;
}

}
