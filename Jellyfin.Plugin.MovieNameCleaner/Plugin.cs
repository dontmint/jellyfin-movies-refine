using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;

namespace Jellyfin.Plugin.MovieNameCleaner
{
    /// <summary>
    /// Plugin configuration
    /// </summary>
    public class PluginConfiguration : MediaBrowser.Model.Plugins.BasePluginConfiguration
    {
        public int ScanIntervalHours { get; set; } = 1;
        
        public List<string> RemovalPatterns { get; set; } = new List<string>
        {
            @"\bwww\.[^\s]+\b\s*-\s*",  // Website tags like www.something.com -
            @"\b\d{3,4}p\b",                       // Quality info: 720p, 1080p, 2160p
            @"\b(AMZN|WEB-DL|WEB|BluRay|BRRip|HDRip|BDRip|DVDRip|x264|x265|h264|h265|HEVC|10bit|HDR|YIFY|YTS|RARBG|GPRS|FGT)\b", // Release/codec tags
            @"\bDTS(?:-HD)?\s+MA\b",                // DTS-HD MA phrase
            @"\bDTS(?:-HD)?\b",                     // DTS or DTS-HD
            @"\bAAC\b",                             // Audio codec
            @"\bDD\d\b",                           // Dolby Digital variants: DD5, DD7, etc.
            @"\b\d\.\d\b",                        // Audio channels like 5.1, 7.1
            @"\[.*?\]",                              // Anything in brackets
            @"\(.*?\)",                              // Anything in parentheses (except year handled separately)
        };
        
        public bool RemoveYear { get; set; } = true;
        public bool PreserveOriginalFiles { get; set; } = true;
    }

    /// <summary>
    /// Main plugin class
    /// </summary>
    public class Plugin : MediaBrowser.Common.Plugins.BasePlugin<PluginConfiguration>
    {
        public override string Name => "Movie Name Cleaner";
        public override Guid Id => Guid.Parse("a1b2c3d4-e5f6-4789-a012-3456789abcde");

        public Plugin(MediaBrowser.Common.Configuration.IApplicationPaths applicationPaths, 
                     MediaBrowser.Model.Serialization.IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin Instance { get; private set; }
    }

    /// <summary>
    /// Scheduled task to clean movie names
    /// </summary>
    public class MovieNameCleanerTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<MovieNameCleanerTask> _logger;

        public string Name => "Clean Movie Names";
        public string Key => "MovieNameCleaner";
        public string Description => "Removes unnecessary patterns from movie titles for better metadata matching";
        public string Category => "Library";
        
        // IConfigurableScheduledTask properties
        public bool IsHidden => false;
        public bool IsEnabled => true;
        public bool IsLogged => true;

        public MovieNameCleanerTask(ILibraryManager libraryManager, ILogger<MovieNameCleanerTask> logger)
        {
            _libraryManager = libraryManager;
            _logger = logger;
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Movie Name Cleaner task");
            
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
                if (cancellationToken.IsCancellationRequested)
                    break;

                var originalName = movie.Name;
                var cleanedName = CleanMovieName(originalName, config);

                if (originalName != cleanedName)
                {
                    _logger.LogInformation($"Cleaning: '{originalName}' -> '{cleanedName}'");
                    
                    movie.Name = cleanedName;
                    await _libraryManager.UpdateItemAsync(movie, movie.GetParent(), 
                        ItemUpdateType.MetadataEdit, cancellationToken);
                    
                    cleaned++;
                }

                processed++;
                progress?.Report((double)processed / totalMovies * 100);
            }

            _logger.LogInformation($"Movie Name Cleaner completed. Processed: {processed}, Cleaned: {cleaned}");
        }

        // Keep old Execute method for backwards compatibility
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            return ExecuteAsync(progress, cancellationToken);
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

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var config = Plugin.Instance?.Configuration;
            var intervalHours = config?.ScanIntervalHours ?? 1;
            
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(intervalHours).Ticks
                }
            };
        }
    }
}