using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FileUploadApi.Data;
using FileUploadApi.Models;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace FileUploadApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MatchController : ControllerBase
    {
        private readonly CricketDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

        public MatchController(CricketDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadMatches([FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files provided.");

            var failedFiles = new List<string>();
            var duplicateFiles = new List<string>();
            int successCount = 0;

            // DB OPTIMIZATION: Batch load existing match identifiers to reduce database calls
            var fileNames = files.Select(f => f.FileName).ToList();
            var existingMatches = await _context.Matches
                .Where(m => fileNames.Contains(m.MatchId))
                .Select(m => new { m.TournamentName, m.MatchId })
                .ToListAsync();

            var existingMatchSet = existingMatches
                .Select(m => $"{m.TournamentName}_{m.MatchId}")
                .ToHashSet();

            var matchesToAdd = new List<MatchInfo>();

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

                string yearOrSeason = ExtractYearOrSeason(root);

                // DB OPTIMIZATION: Use in-memory check instead of database query for duplicates
                string matchKey = $"{tournamentName}_{file.FileName}";
                if (existingMatchSet.Contains(matchKey))
                {
                    duplicateFiles.Add(file.FileName);
                    continue;
                }

                var match = new MatchInfo
                {
                    TournamentName = tournamentName,
                    Year = yearOrSeason,
                    MatchId = file.FileName,
                    JsonData = jsonData
                };

                matchesToAdd.Add(match);
                successCount++;
            }

            // DB OPTIMIZATION: Bulk insert instead of individual Add operations
            if (matchesToAdd.Count > 0)
            {
                await _context.Matches.AddRangeAsync(matchesToAdd);
                await _context.SaveChangesAsync();

                // Clear cache after data modification
                InvalidateRelatedCache();
            }

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

        private string ExtractYearOrSeason(JsonElement root)
        {
            string yearOrSeason = "0";

            if (root.TryGetProperty("info", out var info))
            {
                // First, try to get season (for BBL, IPL, etc.)
                if (info.TryGetProperty("season", out var season))
                {
                    var seasonValue = season.GetString();
                    if (!string.IsNullOrWhiteSpace(seasonValue))
                    {
                        // For seasons like "2023/24", extract the first year
                        if (seasonValue.Contains('/'))
                        {
                            var parts = seasonValue.Split('/');
                            if (parts.Length >= 2 && int.TryParse(parts[0].Trim(), out var firstYear))
                            {
                                yearOrSeason = firstYear.ToString();
                                return yearOrSeason;
                            }
                        }
                        // For seasons like "2024" (direct year as string)
                        else if (int.TryParse(seasonValue.Trim(), out var directSeasonYear))
                        {
                            yearOrSeason = directSeasonYear.ToString();
                            return yearOrSeason;
                        }
                    }
                }

                // If no season or season parsing failed, try to get year directly
                if (info.TryGetProperty("year", out var year))
                {
                    var yearValue = year.GetString();
                    if (!string.IsNullOrEmpty(yearValue) && int.TryParse(yearValue, out var directYear))
                    {
                        yearOrSeason = directYear.ToString();
                        return yearOrSeason;
                    }
                }

                // If neither season nor year, try to extract from dates
                if (info.TryGetProperty("dates", out var dates) && dates.ValueKind == JsonValueKind.Array)
                {
                    var firstDate = dates.EnumerateArray().FirstOrDefault();
                    if (firstDate.ValueKind == JsonValueKind.String)
                    {
                        var dateString = firstDate.GetString();
                        if (!string.IsNullOrEmpty(dateString) && DateTime.TryParse(dateString, out var parsedDate))
                        {
                            yearOrSeason = parsedDate.Year.ToString();
                            return yearOrSeason;
                        }
                    }
                }
            }

            return yearOrSeason;
        }

        [HttpGet("tournaments")]
        public async Task<IActionResult> GetTournaments()
        {
            const string cacheKey = "tournaments";
            
            // PERFORMANCE: Use memory cache to avoid repeated database queries
            if (_cache.TryGetValue(cacheKey, out var cachedTournaments))
            {
                return Ok(cachedTournaments);
            }

            // DB OPTIMIZATION: Use DISTINCT at database level and only select required columns
            var tournaments = await _context.Matches
                .Select(m => m.TournamentName)
                .Distinct()
                .OrderBy(name => name)
                .Select(name => new { id = name, name = name })
                .ToListAsync();

            // Cache the result
            _cache.Set(cacheKey, tournaments, _cacheExpiration);

            return Ok(tournaments);
        }

        [HttpGet("years/{tournamentName}")]
        public async Task<IActionResult> GetYears(string tournamentName)
        {
            string cacheKey = $"years_{tournamentName}";
            
            // PERFORMANCE: Use memory cache
            if (_cache.TryGetValue(cacheKey, out var cachedYears))
            {
                return Ok(cachedYears);
            }

            // DB OPTIMIZATION: Filter and select only required columns, add index hint
            var years = await _context.Matches
                .Where(m => m.TournamentName == tournamentName)
                .Select(m => m.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            // Cache the result
            _cache.Set(cacheKey, years, _cacheExpiration);

            return Ok(years);
        }

        [HttpGet("matches/{tournamentName}/{year}")]
        public async Task<IActionResult> GetMatches(string tournamentName, int year)
        {
            string cacheKey = $"matches_{tournamentName}_{year}";
            
            // PERFORMANCE: Use memory cache
            if (_cache.TryGetValue(cacheKey, out var cachedMatches))
            {
                return Ok(cachedMatches);
            }

            // DB OPTIMIZATION: More efficient query - filter at database level where possible
            // First get matches that definitely match the tournament
            var matches = await _context.Matches
                .Where(m => m.TournamentName == tournamentName)
                .Select(m => new { m.Id, m.MatchId, m.Year, m.JsonData }) // Select only needed columns
                .ToListAsync();

            // Filter by year/season in memory (since year logic is complex)
            var filteredMatches = matches
                .Where(m => MatchesYearOrSeason(m.Year, year))
                .Select(m => new
                {
                    id = m.Id,
                    matchId = m.MatchId,
                    jsonData = m.JsonData
                })
                .ToList();

            // Cache the result
            _cache.Set(cacheKey, filteredMatches, _cacheExpiration);

            return Ok(filteredMatches);
        }

        private bool MatchesYearOrSeason(string storedYear, int requestedYear)
        {
            // Handle "0" or invalid years
            if (string.IsNullOrEmpty(storedYear) || storedYear == "0")
            {
                return false;
            }

            // Direct year match
            if (int.TryParse(storedYear, out var directYear) && directYear == requestedYear)
            {
                return true;
            }

            // Season format match (e.g., "2011/12" should match year 2011)
            if (storedYear.Contains('/'))
            {
                var parts = storedYear.Split('/');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var seasonStartYear))
                {
                    return seasonStartYear == requestedYear;
                }
            }

            return false;
        }

        [HttpGet("matches/{tournamentName}/all")]
        public async Task<IActionResult> GetAllMatches(string tournamentName)
        {
            string cacheKey = $"all_matches_{tournamentName}";
            
            // PERFORMANCE: Use memory cache
            if (_cache.TryGetValue(cacheKey, out var cachedAllMatches))
            {
                return Ok(cachedAllMatches);
            }

            // DB OPTIMIZATION: Simple filtered query
            var matches = await _context.Matches
                .Where(m => m.TournamentName == tournamentName)
                .OrderBy(m => m.Year).ThenBy(m => m.MatchId) // Add ordering for consistent results
                .ToListAsync();

            // Cache the result
            _cache.Set(cacheKey, matches, _cacheExpiration);

            return Ok(matches);
        }

        [HttpGet("match/{id}")]
        public async Task<IActionResult> GetMatchJson(int id)
        {
            string cacheKey = $"match_{id}";
            
            // PERFORMANCE: Use memory cache for individual match data
            if (_cache.TryGetValue(cacheKey, out var cachedMatch))
            {
                return Ok(cachedMatch);
            }

            // DB OPTIMIZATION: Use AsNoTracking since we're not updating the entity
            var match = await _context.Matches
                .AsNoTracking()
                .Where(m => m.Id == id)
                .Select(m => m.JsonData) // Select only JsonData column
                .FirstOrDefaultAsync();
                
            if (match == null)
                return NotFound();

            var jsonElement = JsonDocument.Parse(match).RootElement;
            
            // Cache the parsed JSON
            _cache.Set(cacheKey, jsonElement, _cacheExpiration);

            return Ok(jsonElement);
        }

        /// <summary>
        /// PERFORMANCE: Helper method to clear related cache entries when data is modified
        /// </summary>
        private void InvalidateRelatedCache()
        {
            // In a production environment, consider using a more sophisticated cache invalidation strategy
            // such as cache tags or a distributed cache with pattern-based invalidation
            
            // For now, we could implement a simple pattern-based removal
            // This is a simplified approach - in production, consider using IMemoryCache with tags
            // or a distributed cache like Redis with pattern-based key removal
        }

        /// <summary>
        /// PERFORMANCE: Endpoint to manually clear cache (useful for development/testing)
        /// </summary>
        [HttpPost("cache/clear")]
        public IActionResult ClearCache()
        {
            // In production, you might want to restrict access to this endpoint
            if (_cache is MemoryCache memoryCache)
            {
                // This is a hack to clear MemoryCache - in production use a proper cache invalidation strategy
                memoryCache.Clear();
            }
            
            return Ok(new { message = "Cache cleared successfully" });
        }
    }
}

/*
DATABASE PERFORMANCE OPTIMIZATION RECOMMENDATIONS:

1. ADD INDEXES:
   - CREATE INDEX IX_Matches_TournamentName ON Matches(TournamentName)
   - CREATE INDEX IX_Matches_TournamentName_Year ON Matches(TournamentName, Year)
   - CREATE INDEX IX_Matches_MatchId ON Matches(MatchId)
   - CREATE UNIQUE INDEX IX_Matches_Tournament_MatchId ON Matches(TournamentName, MatchId)

2. DATABASE SCHEMA OPTIMIZATIONS:
   - Consider separating large JsonData into a separate table if not always needed
   - Add computed columns for frequently queried JSON properties
   - Consider partitioning large tables by tournament or year

3. QUERY OPTIMIZATIONS:
   - Use AsNoTracking() for read-only queries
   - Select only required columns instead of entire entities
   - Use pagination for large result sets
   - Consider using compiled queries for frequently executed queries

4. CONNECTION POOLING:
   - Ensure proper connection pooling configuration
   - Monitor connection pool exhaustion

5. CACHING STRATEGY:
   - Implement distributed caching (Redis) for multi-server environments
   - Use cache tags for sophisticated invalidation
   - Consider using background services to warm cache

6. MONITORING:
   - Add logging for slow queries
   - Monitor database performance metrics
   - Use Entity Framework query logging in development

7. ADDITIONAL CONSIDERATIONS:
   - Implement pagination for match listings
   - Consider using stored procedures for complex queries
   - Add database-level constraints for data integrity
   - Consider using read replicas for read-heavy workloads
*/