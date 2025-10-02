using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

Console.WriteLine("Movie Name Cleaner - Test Suite\n");
Console.WriteLine("=" + new string('=', 60) + "\n");

var testCases = new List<string>
{
    "www.UIndex.org - Pawn Sacrifice 2015 1080p AMZN WEB-DL DDP5 1 H 264-GPRS",
    "The Matrix 1999 2160p BluRay x265 10bit HDR DTS-HD MA 5.1-SWTYBLZ",
    "[YTS.MX] Inception (2010) [1080p] [BluRay] [5.1] [YTS] [YIFY]",
    "www.Torrenting.com - Interstellar.2014.1080p.BluRay.x264.DTS-WiKi",
    "Fight Club (1999) BRRip 720p x264 AAC [Team Nanban]",
    "The.Shawshank.Redemption.1994.1080p.BluRay.x264.YIFY",
    // Clean names that should NOT be changed:
    "Pulp Fiction",
    "The Godfather",
    "Forrest Gump",
    "Schindler's List",
    "Avengers.Endgame.2019.1080p.WEB-DL.DD5.1.H264-FGT",
    "Parasite (2019) (1080p BluRay x265 HEVC 10bit AAC 5.1 Korean)",
};

// Test with default config (RemoveYear = true)
Console.WriteLine("TEST 1: With Year Removal (RemoveYear = true)");
Console.WriteLine(new string('-', 60) + "\n");

var configWithoutYear = new Config
{
    RemovalPatterns = new List<string>
    {
        @"\bwww\.[^\s]+\b\s*-\s*",                    // www.something.com -
        @"\b\d{4}\s*\d{3,4}p\b",                       // 2015 1080p or 20151080p
        @"\b\d{3,4}p\b",                                 // Standalone quality: 1080p, 720p, 2160p
        @"\b(AMZN|WEB-DL|WEB|BluRay|BRRip|HDRip|BDRip|DVDRip|x264|x265|h264|h265|HEVC|10bit|HDR|DTS(?:-HD)?|AAC|DD\d|YIFY|YTS|RARBG|GPRS|FGT)\b",  // Quality/codec info (bounded)
        @"\[.*?\]",                                        // Anything in brackets
        @"\(.*?\)",                                        // Anything in parentheses
    },
    RemoveYear = true
};

foreach (var testCase in testCases)
{
    var cleaned = CleanMovieName(testCase, configWithoutYear);
    Console.WriteLine($"Original:  {testCase}");
    Console.WriteLine($"Cleaned:   {cleaned}");
    Console.WriteLine();
}

// Test with year preservation
Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("TEST 2: With Year Preservation (RemoveYear = false)");
Console.WriteLine(new string('-', 60) + "\n");

var configWithYear = new Config
{
    RemovalPatterns = new List<string>
    {
        @"\bwww\.[^\s]+\b\s*-\s*",
        @"\b\d{4}\s*\d{3,4}p\b",
        @"\b\d{3,4}p\b",
        @"\b(AMZN|WEB-DL|WEB|BluRay|BRRip|HDRip|BDRip|DVDRip|x264|x265|h264|h265|HEVC|10bit|HDR|DTS(?:-HD)?|AAC|DD\d|YIFY|YTS|RARBG|GPRS|FGT)\b",
        @"\[.*?\]",
        @"\(.*?\)",
    },
    RemoveYear = false
};

foreach (var testCase in testCases.Take(3))
{
    var cleaned = CleanMovieName(testCase, configWithYear);
    Console.WriteLine($"Original:  {testCase}");
    Console.WriteLine($"Cleaned:   {cleaned}");
    Console.WriteLine();
}

// Add verification that clean names remain unchanged
Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("TEST 3: Verify clean names remain unchanged");
Console.WriteLine(new string('-', 60) + "\n");

var cleanNames = new List<string>
{
    "Pulp Fiction",
    "The Godfather",
    "Forrest Gump",
    "Schindler's List",
    "Interstellar",
    "No Country for Old Men",
    "Moonlight",
    "La La Land",
    "Spider-Man: Into the Spider-Verse"
};

foreach (var name in cleanNames)
{
    var cleanedRemoveYear = CleanMovieName(name, configWithoutYear);
    var cleanedKeepYear = CleanMovieName(name, configWithYear);

    Console.WriteLine($"Clean input: {name}");
    Console.WriteLine($"→ RemoveYear=true: {(cleanedRemoveYear == name ? "UNCHANGED (PASS)" : $"CHANGED to '{cleanedRemoveYear}' (FAIL)")}");
    Console.WriteLine($"→ RemoveYear=false: {(cleanedKeepYear == name ? "UNCHANGED (PASS)" : $"CHANGED to '{cleanedKeepYear}' (FAIL)")}");
    Console.WriteLine();
}

// Interactive mode
Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("Interactive Testing - Enter movie names to clean");
Console.WriteLine("Commands: 'year' to toggle year removal, 'exit' to quit");
Console.WriteLine(new string('=', 60) + "\n");

var currentConfig = configWithoutYear;
Console.WriteLine($"Current mode: RemoveYear = {currentConfig.RemoveYear}\n");

while (true)
{
    Console.Write("Enter movie name (or command): ");
    var input = Console.ReadLine();
    
    if (string.IsNullOrWhiteSpace(input))
        continue;
    
    if (input.ToLower() == "exit")
        break;
    
    if (input.ToLower() == "year")
    {
        currentConfig.RemoveYear = !currentConfig.RemoveYear;
        Console.WriteLine($"→ RemoveYear is now: {currentConfig.RemoveYear}\n");
        continue;
    }

    var result = CleanMovieName(input, currentConfig);
    Console.WriteLine($"→ {result}\n");
}

string CleanMovieName(string name, Config config)
{
    // Check if the name has patterns that need cleaning
    bool needsCleaning = false;
    
    var messyPatterns = new[]
    {
        @"www\.\w+\.\w+",           // Website tags
        @"\b\d{3,4}p\b",              // Quality markers (1080p, 720p, etc.)
        @"\[.*?\]",                     // Brackets
        @"\b(BluRay|WEB-DL|BRRip|HDRip|x264|x265|HEVC|YIFY|YTS|RARBG)\b", // Release formats (bounded)
    };
    
    foreach (var pattern in messyPatterns)
    {
        if (Regex.IsMatch(name, pattern, RegexOptions.IgnoreCase))
        {
            needsCleaning = true;
            break;
        }
    }
    
    // If no messy patterns detected, return original name
    if (!needsCleaning)
    {
        return name;
    }
    
    // Proceed with cleaning
    string cleaned = name;

    // Extract year FIRST before removing anything
    var yearMatch = Regex.Match(name, @"\b(19\d{2}|20\d{2})\b");
    string year = yearMatch.Success ? yearMatch.Value : "";

    // Apply each removal pattern
    foreach (var pattern in config.RemovalPatterns)
    {
        try
        {
            cleaned = Regex.Replace(cleaned, pattern, "", RegexOptions.IgnoreCase);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error with pattern '{pattern}': {ex.Message}");
        }
    }

    // Remove trailing scene/release group suffix like '-SWTYBLZ'
    cleaned = Regex.Replace(cleaned, @"-\s*[A-Za-z0-9]+$", "", RegexOptions.IgnoreCase);

    // Clean up extra spaces, dashes, dots, and leftover brackets/parentheses
    cleaned = Regex.Replace(cleaned, @"[\.\-_]+", " ");      // Replace separators with spaces
    cleaned = Regex.Replace(cleaned, @"[\[\]\(\)]+", "");    // Remove any leftover brackets/parentheses
    cleaned = Regex.Replace(cleaned, @"\s+", " ");           // Multiple spaces to single space
    cleaned = cleaned.Trim();

    // Re-add year at the end if we want to preserve it
    if (!config.RemoveYear && !string.IsNullOrEmpty(year))
    {
        cleaned = $"{cleaned} ({year})";
    }

    // Final safeguard: never return empty, fall back to original name
    if (string.IsNullOrWhiteSpace(cleaned))
    {
        cleaned = name.Trim();
    }

    return cleaned;
}

class Config
{
    public List<string> RemovalPatterns { get; set; }
    public bool RemoveYear { get; set; }
}