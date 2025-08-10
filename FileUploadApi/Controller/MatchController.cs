using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FileUploadApi.Data;
using FileUploadApi.Models;
using System.Text.Json;

namespace FileUploadApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchController : ControllerBase
    {
        private readonly CricketDbContext _context;

        public MatchController(CricketDbContext context)
        {
            _context = context;
        }

        // [HttpPost("upload")]
        // public async Task<IActionResult> UploadMatches([FromForm] List<IFormFile> files)
        // {
        //     if (files == null || files.Count == 0)
        //         return BadRequest("No files provided.");

        //     foreach (var file in files)
        //     {
        //         using var ms = new MemoryStream();
        //         await file.CopyToAsync(ms);
        //         ms.Position = 0;

        //         string jsonData;
        //         using (var reader = new StreamReader(ms, leaveOpen: true))
        //         {
        //             jsonData = await reader.ReadToEndAsync();
        //         }

        //         using var doc = JsonDocument.Parse(jsonData);
        //         var root = doc.RootElement;

        //         string tournamentName = "UnknownTournament";
        //         if (root.TryGetProperty("info", out var info) &&
        //             info.TryGetProperty("event", out var ev) &&
        //             ev.TryGetProperty("name", out var evName))
        //         {
        //             tournamentName = evName.GetString() ?? "UnknownTournament";
        //         }

        //         int year = 0;
        //         if (root.TryGetProperty("info", out var infoYear) &&
        //             infoYear.TryGetProperty("season", out var season))
        //         {
        //             int.TryParse(season.GetString(), out year);
        //         }

        //         var match = new MatchInfo
        //         {
        //             TournamentName = tournamentName,
        //             Year = year.ToString(),
        //             MatchId = file.FileName, // or generate your own if needed
        //             JsonData = jsonData
        //         };

        //         _context.Matches.Add(match);
        //     }

        //     await _context.SaveChangesAsync();

        //     return Ok(new { Message = $"{files.Count} matches uploaded successfully." });
        // }


        [HttpPost("upload")]
public async Task<IActionResult> UploadMatches([FromForm] List<IFormFile> files)
{
    if (files == null || files.Count == 0)
        return BadRequest("No files provided.");

    var failedFiles = new List<string>();
    var duplicateFiles = new List<string>();
    int successCount = 0;

    foreach (var file in files)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        string jsonData;
        using (var reader = new StreamReader(ms, leaveOpen: true))
        {
            jsonData = await reader.ReadToEndAsync();
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonData);
        }
        catch (JsonException)
        {
            // JSON parsing failed - log filename and skip
            failedFiles.Add(file.FileName);
            continue;
        }

        var root = doc.RootElement;

        string tournamentName = "UnknownTournament";
        if (root.TryGetProperty("info", out var info) &&
            info.TryGetProperty("event", out var ev) &&
            ev.TryGetProperty("name", out var evName))
        {
            tournamentName = evName.GetString() ?? "UnknownTournament";
        }

        int year = 0;
        if (root.TryGetProperty("info", out var infoYear) &&
            infoYear.TryGetProperty("season", out var season))
        {
            int.TryParse(season.GetString(), out year);
        }

        // Check for duplicate by TournamentName + MatchId (filename)
        bool isDuplicate = await _context.Matches
            .AnyAsync(m => m.TournamentName == tournamentName && m.MatchId == file.FileName);

        if (isDuplicate)
        {
            duplicateFiles.Add(file.FileName);
            continue; // skip this duplicate file
        }

        var match = new MatchInfo
        {
            TournamentName = tournamentName,
            Year = year.ToString(),
            MatchId = file.FileName,
            JsonData = jsonData
        };

        _context.Matches.Add(match);
        successCount++;
    }

    await _context.SaveChangesAsync();

    // Build response message
    var response = new
    {
        Uploaded = successCount,
        Failed = failedFiles.Count,
        Duplicates = duplicateFiles.Count,
        FailedFiles = failedFiles,
        DuplicateFiles = duplicateFiles
    };

    return Ok(response);
}


        [HttpGet("tournaments")]
        public async Task<IActionResult> GetTournaments()
        {
            var tournaments = await _context.Matches
                .GroupBy(m => m.TournamentName)
                .Select(g => new
                {
                    id = g.Key,
                    name = g.Key
                })
                .ToListAsync();

            return Ok(tournaments);
        }

        [HttpGet("years/{tournamentName}")]
        public async Task<IActionResult> GetYears(string tournamentName)
        {
            var years = await _context.Matches
                .Where(m => m.TournamentName == tournamentName)
                .Select(m => m.Year)
                .Distinct()
                .OrderBy(y => y)
                .ToListAsync();

            return Ok(years);
        }

        [HttpGet("matches/{tournamentName}/{year}")]
        public async Task<IActionResult> GetMatches(string tournamentName, int year)
        {
            var matches = await _context.Matches
                .Where(m => m.TournamentName == tournamentName)
                .ToListAsync();

            var filteredMatches = matches
                .Where(m => int.TryParse(m.Year, out var y) && y == year)
                .Select(m => new
                {
                    id = m.Id,
                    matchId = m.MatchId,
                      jsonData = m.JsonData 
                    // Optionally parse date or opponent from JSONData here or return minimal
                })
                .ToList();

            return Ok(filteredMatches);
        }

[HttpGet("matches/{tournamentName}/all")]
public async Task<IActionResult> GetAllMatches(string tournamentName)
{
    var matches = await _context.Matches
        .Where(m => m.TournamentName == tournamentName)
        .ToListAsync();

    return Ok(matches);
}

        

        [HttpGet("match/{id}")]
        public async Task<IActionResult> GetMatchJson(int id)
        {
            var match = await _context.Matches.FindAsync(id);
            if (match == null)
                return NotFound();

            return Ok(JsonDocument.Parse(match.JsonData).RootElement);
        }
    }
}
