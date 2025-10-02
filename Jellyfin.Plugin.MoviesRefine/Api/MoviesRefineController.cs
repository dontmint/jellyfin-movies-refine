using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;

namespace Jellyfin.Plugin.MoviesRefine.Api
{
    /// <summary>
    /// API controller for Movies Refine plugin
    /// </summary>
    [ApiController]
    [Authorize(Policy = "RequiresElevation")]
    [Route("MoviesRefine")]
    public class MoviesRefineController : ControllerBase
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<MoviesRefineController> _logger;

        public MoviesRefineController(ILibraryManager libraryManager, ILogger<MoviesRefineController> logger)
        {
            _libraryManager = libraryManager;
            _logger = logger;
        }

        /// <summary>
        /// Manually trigger movie name cleanup
        /// </summary>
        /// <returns>Cleanup results</returns>
        [HttpPost("ManualCleanup")]
        public async Task<ActionResult<ManualCleanupResult>> ManualCleanup()
        {
            try
            {
                _logger.LogInformation("Manual cleanup triggered via API");
                
                var config = Plugin.Instance.Configuration;
                var movies = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie },
                    Recursive = true
                }).OfType<Movie>().ToList();

                int totalMovies = movies.Count;
                int processed = 0;
                int cleaned = 0;

                foreach (var movie in movies)
                {
                    var originalName = movie.Name;
                    var cleanedName = CleanMovieName(originalName, config);

                    if (originalName != cleanedName)
                    {
                        _logger.LogInformation($"Manual cleanup: '{originalName}' -> '{cleanedName}'");
                        
                        movie.Name = cleanedName;
                        await _libraryManager.UpdateItemAsync(movie, movie.GetParent(), 
                            ItemUpdateType.MetadataEdit, CancellationToken.None);
                        
                        cleaned++;
                    }

                    processed++;
                }

                var result = new ManualCleanupResult
                {
                    Processed = processed,
                    Cleaned = cleaned,
                    Success = true,
                    Message = $"Manual cleanup completed. Processed: {processed}, Cleaned: {cleaned}"
                };

                _logger.LogInformation(result.Message);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual cleanup");
                return StatusCode(500, new ManualCleanupResult
                {
                    Processed = 0,
                    Cleaned = 0,
                    Success = false,
                    Message = $"Error during cleanup: {ex.Message}"
                });
            }
        }

        private bool HasMessyPatterns(string name)
        {
            var messyPatterns = new[]
            {
                @"\bwww\.[^\s]+\b",                         // Website tags (e.g., www.something.com)
                @"\b\d{3,4}p\b",                            // Quality markers (e.g., 1080p, 720p)
                @"\[.*?\]",                                 // Brackets
                @"\b(BluRay|WEB-DL|WEB|BRRip|HDRip|BDRip|DVDRip|x264|x265|h264|h265|HEVC|YIFY|YTS|RARBG)\b" // Common release tags with boundaries
            };

            foreach (var pattern in messyPatterns)
            {
                if (Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string CleanMovieName(string name, PluginConfiguration config)
        {
            // Detection-first: if no messy patterns detected, return the original name unchanged
            if (!HasMessyPatterns(name))
            {
                return name;
            }

            string cleaned = name;

            // Extract year if we want to preserve it
            var yearMatch = Regex.Match(name, @"\b(19\d{2}|20\d{2})\b");
            string year = yearMatch.Success ? yearMatch.Value : "";

            // Apply each removal pattern (word-boundary safe, non-greedy)
            foreach (var pattern in config.RemovalPatterns)
            {
                try
                {
                    cleaned = Regex.Replace(cleaned, pattern, "", RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error applying pattern: {pattern}");
                }
            }

            // Remove trailing scene/release group suffix like '-SWTYBLZ'
            cleaned = Regex.Replace(cleaned, @"-\s*[A-Za-z0-9]+$", "", RegexOptions.IgnoreCase);

            // Remove year if configured
            if (config.RemoveYear && !string.IsNullOrEmpty(year))
            {
                cleaned = cleaned.Replace(year, "");
            }

            // Clean up extra spaces, dashes, and dots
            cleaned = Regex.Replace(cleaned, @"[\.\-_]+", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            cleaned = cleaned.Trim();

            // Re-add year at the end if we preserved it
            if (!config.RemoveYear && !string.IsNullOrEmpty(year) && !cleaned.Contains(year))
            {
                cleaned = $"{cleaned} ({year})".Trim();
            }

            // Final safeguard: never return empty, fall back to original name
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                cleaned = name.Trim();
            }

            return cleaned;
        }

        /// <summary>
        /// Serve the configuration page HTML
        /// </summary>
        /// <returns>Configuration page HTML</returns>
        [HttpGet("Configuration")]
        [AllowAnonymous]
        public ActionResult GetConfigurationPage()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Jellyfin.Plugin.MoviesRefine.Configuration.configPage.html";
                
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        _logger.LogError("Configuration page resource not found: {ResourceName}", resourceName);
                        return NotFound("Configuration page not found");
                    }
                    
                    using (var reader = new StreamReader(stream))
                    {
                        var html = reader.ReadToEnd();
                        return Content(html, "text/html");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving configuration page");
                return StatusCode(500, "Error loading configuration page");
            }
        }
    }

    /// <summary>
    /// Result of manual cleanup operation
    /// </summary>
    public class ManualCleanupResult
    {
        public int Processed { get; set; }
        public int Cleaned { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}