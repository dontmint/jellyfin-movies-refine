# Jellyfin Movie Name Cleaner Plugin

A smart Jellyfin plugin that intelligently cleans messy movie titles by removing quality markers, website tags, and release group information while preserving already clean names.

## Features

- **Intelligent Detection**: Only processes titles that contain detectable messy patterns
- **Preserves Clean Names**: Clean movie titles remain completely unchanged
- **Comprehensive Cleaning**: Removes quality markers (1080p, BluRay, x265), website tags, audio formats (DTS-HD MA), and release group suffixes
- **Safe Processing**: Never returns empty titles - falls back to original if cleaning fails
- **Configurable**: Optional year removal and customizable removal patterns

## Examples

| Original Title | Cleaned Title |
|----------------|---------------|
| `The Matrix 1999 2160p BluRay x265 10bit HDR DTS-HD MA 5.1-SWTYBLZ` | `The Matrix (1999)` |
| `Interstellar.2014.1080p.BluRay.x264-SPARKS` | `Interstellar (2014)` |
| `www.YTS.mx - Inception (2010) [1080p] [BluRay] [5.1] [YTS] [YIFY]` | `Inception (2010)` |
| `Pulp Fiction` | `Pulp Fiction` *(unchanged - already clean)* |
| `The Godfather` | `The Godfather` *(unchanged - already clean)* |

## Installation

### From Jellyfin Plugin Catalog (Recommended)
1. Open Jellyfin web interface
2. Go to **Dashboard** → **Plugins** → **Catalog**
3. Search for "Movie Name Cleaner"
4. Click **Install**
5. Restart Jellyfin

### Manual Installation
1. Download the latest `Jellyfin.Plugin.MovieNameCleaner.dll` from [Releases](https://github.com/YOUR_USERNAME/jellyfin-plugin-movienamecleaner/releases)
2. Create folder: `/path/to/jellyfin/plugins/MovieNameCleaner/`
3. Copy the DLL to this folder
4. Restart Jellyfin

## Usage

1. Go to **Dashboard** → **Plugins** → **Movie Name Cleaner**
2. Configure settings:
   - **Remove Year**: Toggle year extraction and re-addition
   - **Custom Patterns**: Add additional removal patterns if needed
3. Go to **Dashboard** → **Scheduled Tasks**
4. Find "Clean Movie Names" task and click **Run**

## Configuration

The plugin detects these messy patterns automatically:
- Quality markers: `1080p`, `2160p`, `BluRay`, `WEBRip`, `x264`, `x265`, `HEVC`
- Audio formats: `DTS-HD MA`, `TrueHD`, `Atmos`, `DD+`, `AC3`
- Website tags: `www.site.com`, `YTS`, `YIFY`, `RARBG`
- Release groups: `-SPARKS`, `-SWTYBLZ`, `-FGT` (trailing suffixes)
- Brackets and quality info: `[1080p]`, `(BluRay)`, `{x265}`

## Development

### Building
```bash
dotnet build Jellyfin.Plugin.MovieNameCleaner/Jellyfin.Plugin.MovieNameCleaner.csproj --configuration Release
```

### Testing
Run the included test suite:
```bash
dotnet run --project PluginTester.cs
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

If you encounter issues or have suggestions:
- Open an [issue](https://github.com/YOUR_USERNAME/jellyfin-plugin-movienamecleaner/issues)
- Check existing issues for solutions
- Provide example movie titles that aren't cleaning correctly