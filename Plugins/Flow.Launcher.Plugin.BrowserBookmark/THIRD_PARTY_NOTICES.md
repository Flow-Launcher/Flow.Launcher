# Third-party notices

This project uses third-party NuGet packages.  

| Reference                  | Version | License Type | License                               |
|---------------------------------------------------------------------------------------------|
| BrowserTabs                | 0.2.0   | Apache-2.0   | https://licenses.nuget.org/Apache-2.0 |
| CommunityToolkit.Mvvm      | 8.4.0   | MIT          | https://licenses.nuget.org/MIT        |
| Flow.Launcher.Localization | 0.0.6   | MIT          | https://licenses.nuget.org/MIT        |
| Microsoft.Data.Sqlite      | 10.0.0  | MIT          | https://licenses.nuget.org/MIT        |
| SkiaSharp                  | 3.119.1 | MIT          | https://licenses.nuget.org/MIT        |
| Svg.Skia                   | 3.2.1   | MIT          | https://licenses.nuget.org/MIT        |

Detailed information (package id, version, license, repository URL) is available in [THIRD_PARTY_NOTICES.json](THIRD_PARTY_NOTICES.json).  

# How to generate the list

1. Install `dotnet-project-licenses`
1. Use the tool as below
1. Copy markdown above
1. Rename `licenses.json` to `THIRD_PARTY_NOTICES.json` and format the json

```
dotnet tool install --global dotnet-project-licenses
dotnet-project-licenses --input Flow.Launcher.Plugin.BrowserBookmark.csproj --json
```
