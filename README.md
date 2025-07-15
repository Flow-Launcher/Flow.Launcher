<p align="center">
        <a href="https://flowlauncher.com">
        	<img src="https://user-images.githubusercontent.com/6903107/207167068-2196d2a3-2caa-4856-958b-a780fbda95c6.gif" width="500">
        </a><br />
        <img src="https://user-images.githubusercontent.com/6903107/207168016-85d0dd16-1f3b-4d42-9d37-0e0d5a596ead.png" width="400">
</p>
<p align="center">
<img src="https://img.shields.io/maintenance/yes/3000">
<a href="https://crowdin.com/project/flow-launcher"><img src="https://badges.crowdin.net/flow-launcher/localized.svg"></a>
<a href="https://ci.appveyor.com/project/JohnTheGr8/flow-launcher/branch/dev"><img src="https://ci.appveyor.com/api/projects/status/32r7s2skrgm9ubva?svg=true&retina=true"></a>
<a href="https://github.com/Flow-Launcher/Flow.Launcher/releases"><img src="https://img.shields.io/github/downloads/Flow-Launcher/Flow.Launcher/total.svg"></a><br />
<img src="https://img.shields.io/github/release-date/Flow-Launcher/Flow.Launcher">
<a href="https://github.com/Flow-Launcher/Flow.Launcher/releases/latest"><img src="https://img.shields.io/github/v/release/Flow-Launcher/Flow.Launcher"></a>
<a href="https://flowlauncher.com/docs"><img src="https://img.shields.io/badge/Documentation-7389D8"></a>
<a href="https://discord.gg/AvgAQgh"><img src="https://img.shields.io/discord/727828229250875472?color=7389D8&labelColor=6A7EC2&label=Community&logo=discord&logoColor=white"></a>
</p>

<p align="center">
A quick file search and app launcher for Windows with community-made plugins.</p>

<p align="center">
Dedicated to making your work flow more seamless. Search everything from applications, files, bookmarks, YouTube, Twitter and more. Flow will continue to evolve, designed to be open and built with the community at heart.</p>

<p align="center"> <sub>Remember to star it, flow will love you more :)</sub></p>

<img src="https://user-images.githubusercontent.com/6903107/144858082-8b654daf-60fb-4ee6-89b2-6183b73510d1.png" width="100%">

<h4 align="center">
  <a href="#-getting-started">Getting Started</a> •
  <a href="#-features">Features</a> •
  <a href="#-plugins">Plugins</a> •
  <a href="#%EF%B8%8F-hotkeys">Hotkeys</a> •
  <a href="#sponsors">Sponsors</a> •
  <a href="#-questionssuggestions">Questions/Suggestions</a> •
  <a href="#development">Development</a> •
  <a href="https://flowlauncher.com/docs">Docs</a>
</h4>

<img src="https://user-images.githubusercontent.com/6903107/144858082-8b654daf-60fb-4ee6-89b2-6183b73510d1.png" width="100%">

## 🚗 Getting Started

### Installation

[Windows 7+ Installer](https://github.com/Flow-Launcher/Flow.Launcher/releases/latest/download/Flow-Launcher-Setup.exe) or [Portable Version](https://github.com/Flow-Launcher/Flow.Launcher/releases/latest/download/Flow-Launcher-Portable.zip)

#### Winget

```
winget install "Flow Launcher"
```

#### Scoop

```
scoop install Flow-Launcher
```

#### Chocolatey

```
choco install Flow-Launcher
```

> When installing for the first time Windows may raise an issue about security due to code not being signed, if you downloaded from this repo then you are good to continue the set up.

Or download the [early access version](https://github.com/Flow-Launcher/Prereleases/releases).

<img src="https://user-images.githubusercontent.com/6903107/144858082-8b654daf-60fb-4ee6-89b2-6183b73510d1.png" width="100%">

## 🎁 Features

### Applications & Files

<img src="https://user-images.githubusercontent.com/6903107/145332614-74909973-f6eb-47c2-8235-289931e30718.png" width="400">

- Search for apps, files or file contents.
- Supports Everything and Windows Index.

<img src="https://user-images.githubusercontent.com/6903107/145018796-658b7c24-a34f-46b6-98d4-cf4f636d8b60.png" width="400">

- Support search using environment variable paths.

### Web Searches & URLs

<img src="https://user-images.githubusercontent.com/6903107/144517502-5325de01-d0d9-4c2e-aafb-33c3f5d82f81.png" width="400">
<img src="https://user-images.githubusercontent.com/6903107/144831031-0e01e8ea-3247-4ba4-a7b4-48b0db620bc1.png" width="400">
<img src="https://user-images.githubusercontent.com/6903107/222829602-aabb1144-db5c-4250-b5ae-66f8342e4ae4.png" width="400">

### Browser Bookmarks

<img src="https://user-images.githubusercontent.com/6903107/207143428-e6406306-4f1e-4c24-917d-d2a333d5dc2b.png" width="400">

### System Commands

<img src="https://user-images.githubusercontent.com/6903107/144517557-9b5b82fc-6408-48a0-af59-69b385a0782e.png" width="400">

- Provides system related commands. shutdown, lock, settings, etc.
- [System command list](#system-command-list)

### Calculator

<img src="https://user-images.githubusercontent.com/6903107/207142449-7de0c30d-8d5b-4331-967e-f3e78c17ea93.png" width="400">

- Do mathematical calculations and copy the result to clipboard.

### Shell Command

<img src="https://user-images.githubusercontent.com/6903107/207142197-9e910147-96a9-466e-bbc4-b1163314ef59.png" width="400">

- Run batch and PowerShell commands as Administrator or a different user.
- <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>Enter</kbd> to Run as Administrator.

### Explorer

<img src="https://user-images.githubusercontent.com/6903107/207145376-fbb68ec2-93b9-4b0f-befe-0aeb792367a7.png" width="400">

- Save file or folder locations for quick access.

#### Drag & Drop

<img src="https://user-images.githubusercontent.com/6903107/207159486-1993510f-09f2-4e33-bba7-4ca59ca1bc5a.png" width="500">

- Drag a file/folder to File Explorer, or even Discord.
- Copy/move behavior can be change via <kbd>Ctrl</kbd> or <kbd>Shift</kbd>, and the operation is displayed on the mouse cursor.

### Windows & Control Panel Settings

<img src="https://user-images.githubusercontent.com/6903107/207140658-52c1bea6-5b14-4db8-ae35-acc65e6bda85.png" width="400">

- Search for Windows & Control Panel settings.

### Priority

<img src="https://user-images.githubusercontent.com/6903107/144517677-857a2b0a-4b94-4be0-bc89-f35723ecddf9.png" width="500">

- Prioritise the order of each plugin's results.

### Preview Panel

<img src="https://user-images.githubusercontent.com/6903107/207159213-662999d3-2c18-4256-b473-c417efca0069.png" width="400">

- Use <kbd>F1</kbd> to toggle the preview panel.
- Media files will be displayed as large images, otherwise a large icon and full path will be displayed.
- Turn on preview permanently via Settings (Always Preview).
- Use <kbd>Ctrl</kbd>+<kbd>+</kbd>/<kbd>-</kbd> and <kbd>Ctrl</kbd>+<kbd>[</kbd>/<kbd>]</kbd> to adjust search window width and height quickly if the preview area is too narrow.


### Customizations

<img src="https://user-images.githubusercontent.com/6903107/144693887-1b92ed16-dca1-4b7e-8644-5e9524cdfb31.gif" width="500">

- Window size adjustment, animation, and sound
- Color Scheme (aka Dark Mode)

<img src="https://user-images.githubusercontent.com/6903107/144527796-7c06ca31-d933-4f6b-9eb0-4fb06fa94384.png" width="500">

- There are various themes and you also can make your own.

#### Date & Time Display In Search Window

<img src="https://user-images.githubusercontent.com/6903107/207159348-8b0c7a2b-0836-4764-916b-e0236087f7f3.png" width="400">

- Display date and time in search window.

### 💬 Languages

- Supports languages from Chinese to Italian and more.
- Supports Pinyin (拼音) search.
- [Crowdin](https://crowdin.com/project/flow-launcher) support for language translations.

<details>
<summary>Supported languages</summary>
<ul>
  <li>English</li>
  <li>中文</li>
  <li>中文（繁体）</li>
  <li>Українська</li>
  <li>Русский</li>
  <li>Français</li>
  <li>日本語</li>
  <li>Dutch</li>
  <li>Polski</li>
  <li>Dansk</li>
  <li>de, Deutsch</li>
  <li>ko, 한국어</li>
  <li>Srpski</li>
  <li>Português</li>
  <li>Português (Brasil)</li>
  <li>Spanish</li>
  <li>es-419, Spanish (Latin America)</li>
  <li>Italiano</li>
  <li>Norsk Bokmål</li>
  <li>Slovenčina</li>
  <li>Türkçe</li>
  <li>čeština</li>
  <li>اللغة العربية</li>
  <li>Tiếng Việt</li>
</ul>
</details>

### Portable

- Fully portable.
- Type `flow user data` to open your saved user settings folder. They are located at:
  - If using roaming: `%APPDATA%\FlowLauncher`
  - If using portable, by default: `%localappdata%\FlowLauncher\app-<VersionOfYourFlowLauncher>\UserData`
  - Type `open log location` to open your logs folder, they are saved along with your user settings folder.

### 🎮 Game Mode

<img src="https://user-images.githubusercontent.com/6903107/207144711-0c5f8b2b-4b1b-44c8-b23e-c123f6b05146.png" width="200">

- Pause hotkey activation when you are playing games.
- When in search window use <kbd>Ctrl</kbd>+<kbd>F12</kbd> to toggle on/off.
- Type `Toggle Game Mode`

<img src="https://user-images.githubusercontent.com/6903107/144858082-8b654daf-60fb-4ee6-89b2-6183b73510d1.png" width="100%">

## 📦 Plugins

- Support wide range of plugins. Visit [here](https://www.flowlauncher.com/plugins/) for our plugin portfolio.
- Publish your own plugin to flow! Create plugins in:

<p align="center">
<a href="https://flowlauncher.com/docs/#/develop-dotnet-plugins"><img src="https://user-images.githubusercontent.com/6903107/147870065-4096f233-147c-434e-a3ac-69519582605f.png" width="64"></a>
<a href="https://github.com/Flow-Launcher/plugin-samples/tree/master/HelloWorldFSharp"><img src="https://user-images.githubusercontent.com/26427004/156536959-dfdc7be8-4b59-4587-9c6a-a297903e4ce1.png" width="64"></a>
<a href="https://www.flowlauncher.com/docs/#/py-develop-plugins"><img src="https://user-images.githubusercontent.com/6903107/147870066-7599eb15-0333-468e-82e8-4d432ceb5a45.png" width="64"></a>
<a href="https://flowlauncher.com/docs/#/nodejs-develop-plugins"><img src="https://user-images.githubusercontent.com/6903107/147870071-d67c736b-0748-428f-a283-14587696dfa3.png" width="64"></a>
<a href="https://flowlauncher.com/docs/#/nodejs-develop-plugins"><img src="https://user-images.githubusercontent.com/6903107/147870069-9bde6fe6-d50c-4d85-8fde-fe5ae921ab8c.png" width="64"></a>
</p>

### [SpotifyPremium](https://github.com/fow5040/Flow.Launcher.Plugin.SpotifyPremium)

<img src="https://user-images.githubusercontent.com/6903107/144533469-da920295-8c36-46e8-89eb-a9cdd94b74ef.png" width="400">

### [Steam Search](https://github.com/Garulf/Steam-Search)

<img src="https://user-images.githubusercontent.com/6903107/144533523-afd79dca-a444-40e5-b2d9-6d3fe3aaece1.png" width="400">

### [Clipboard History](https://github.com/liberize/Flow.Launcher.Plugin.ClipboardHistory)

<img src="https://user-images.githubusercontent.com/6903107/144533481-58e473fd-38d9-4604-861f-ad870770967d.png" width="400">

### [Home Assistant Commander](https://github.com/Garulf/HA-Commander)

<img src="https://user-images.githubusercontent.com/6903107/144533538-3caa2944-3037-4755-87b9-70fa918d2efa.png" width="400">

### [Colors](https://github.com/Flow-Launcher/Flow.Launcher.Plugin.Color)

<img src="https://user-images.githubusercontent.com/6903107/144533487-2caff162-a8f6-4577-af3f-d1b05d423ee4.png" width="400">

### [GitHub](https://github.com/JohnTheGr8/Flow.Plugin.Github)

<img src="https://user-images.githubusercontent.com/6903107/144533497-8677f800-95c5-4758-8ca3-c96333ee1943.png" width="400">

### [Window Walker](https://github.com/taooceros/Flow.Plugin.WindowWalker)

<img src="https://user-images.githubusercontent.com/6903107/144533517-07bf011f-726c-4221-8657-0e442eca8a82.png" width="400">

......and [more!](https://flowlauncher.com/docs/#/plugins)

<img src="https://user-images.githubusercontent.com/6903107/144858082-8b654daf-60fb-4ee6-89b2-6183b73510d1.png" width="100%">

### 🛒 Plugin Store

<img src="https://user-images.githubusercontent.com/6903107/207155616-d559f0d2-ee95-4072-a7bc-3ffcc2faec27.png" width="700">

- You can view the full plugin list or quickly install a plugin via the Plugin Store menu inside Settings

- or type `pm` `install`/`uninstall`/`update` + the plugin name in the search window,

<img src="https://user-images.githubusercontent.com/6903107/144858082-8b654daf-60fb-4ee6-89b2-6183b73510d1.png" width="100%">

## ⌨️ Hotkeys

| Hotkey                                                                    | Description                                     |
| ------------------------------------------------------------------------- | ----------------------------------------------- |
| <kbd>Alt</kbd>+<kbd>Space</kbd>                                           | Open search window (default and configurable)   |
| <kbd>Enter</kbd>                                                          | Execute                                         |
| <kbd>Ctrl</kbd>+<kbd>Enter</kbd>                                          | Open containing folder                          |
| <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>Enter</kbd>                         | Run as admin                                    |
| <kbd>↑</kbd>/<kbd>↓</kbd>, <kbd>Shift</kbd>+<kbd>Tab</kbd>/<kbd>Tab</kbd> | Previous / Next result                          |
| <kbd>←</kbd>/<kbd>→</kbd>                                                 | Back to result / Open Context Menu              |
| <kbd>Ctrl</kbd>+<kbd>O</kbd> , <kbd>Shift</kbd>+<kbd>Enter</kbd>          | Open Context Menu                               |
| <kbd>Ctrl</kbd>+<kbd>Tab</kbd>                                            | Autocomplete                                    |
| <kbd>F1</kbd>                                                             | Toggle Preview Panel (default and configurable) |
| <kbd>Esc</kbd>                                                            | Back to results / hide search window            |
| <kbd>Ctrl</kbd>+<kbd>C</kbd>                                              | Copy folder / file                              |
| <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>C</kbd>                             | Copy folder / file path                         |
| <kbd>Ctrl</kbd>+<kbd>I</kbd>                                              | Open Flow's settings                            |
| <kbd>Ctrl</kbd>+<kbd>R</kbd>                                              | Run the current query again (refresh results)   |
| <kbd>F5</kbd>                                                             | Reload all plugin data                          |
| <kbd>Ctrl</kbd>+<kbd>F12</kbd>                                            | Toggle Game Mode when in search window          |
| <kbd>Ctrl</kbd>+<kbd>+</kbd>,<kbd>-</kbd>                                 | Adjust maximum results shown                    |
| <kbd>Ctrl</kbd>+<kbd>[</kbd>,<kbd>]</kbd>                                 | Adjust search window width                      |
| <kbd>Ctrl</kbd>+<kbd>H</kbd>                                              | Open search history                             |
| <kbd>Ctrl</kbd>+<kbd>Backspace</kbd>                                      | Back to previous directory                      |
| <kbd>PageUp</kbd>/<kbd>PageDown</kbd>                                     | Previous / Next Page                            |

## System Command List

| Command                            | Description                                                                 |
| ---------------------------------- | --------------------------------------------------------------------------- |
| Shutdown                           | Shutdown computer                                                           |
| Restart                            | Restart computer                                                            |
| Restart With Advanced Boot Options | Restart the computer with Advanced Boot option for safe and debugging modes |
| Log Off/Sign Out                   | Log off                                                                     |
| Lock                               | Lock computer                                                               |
| Sleep                              | Put computer to sleep                                                       |
| Hibernate                          | Hibernate computer                                                          |
| Empty Recycle Bin                  | Empty recycle bin                                                           |
| Open Recycle Bin                   | Open recycle bin                                                            |
| Exit                               | Close Flow Launcher                                                         |
| Save Settings                      | Save all Flow Launcher settings                                             |
| Restart Flow Launcher              | Restart Flow Launcher                                                       |
| Settings                           | Tweak this app                                                              |
| Reload Plugin Data                 | Refreshes plugin data with new content                                      |
| Check For Update                   | Check for new Flow Launcher update                                          |
| Open Log Location                  | Open Flow Launcher's log location                                           |
| Index Option                       | Open Windows Search Index window                                            |
| Flow Launcher Tips                 | Visit Flow Launcher's documentation for more help and how to use tips       |
| Flow Launcher UserData Folder      | Open the location where Flow Launcher's settings are stored                 |
| Toggle Game Mode                   | Toggle Game Mode                                                            |

### 💁‍♂️ Tips

- [More tips](https://flowlauncher.com/docs/#/usage-tips)

<img src="https://user-images.githubusercontent.com/6903107/144858082-8b654daf-60fb-4ee6-89b2-6183b73510d1.png" width="100%">

## Sponsors
<p align="center">
  <a href="https://coderabbit.ai/">
    <img src="https://github.com/Flow-Launcher/Flow.Launcher/assets/6903107/7c996d74-0c69-4011-922f-a95ca7e874b0" width="30%" alt="Coderabbit Logo" />
  </a>
  <br />
  <br />
  <a href="https://github.com/TheBestPessimist">
    <img src='https://avatars.githubusercontent.com/u/4482210?v=4' width="10%"/>
  </a>
  <a href="https://github.com/AjmalParkar006">
    <img src='https://avatars.githubusercontent.com/u/76547256?v=4' width="10%"/>
  </a>
</p>
<p align="center">
  <a href="https://appwrite.io">
    <img src='https://appwrite.io/assets/logotype/white.svg' width="30%" alt="Appwrite Logo" />
  </a>
  <br />
</p>
<p align="center">
  <a href="https://github.com/itsonlyfrans"><img src="https://avatars.githubusercontent.com/u/46535667?v=4" width="10%" /></a>
  <a href="https://github.com/atilford"><img src="https://avatars.githubusercontent.com/u/13649625?v=4" width="10%" /></a>
  <a href="https://github.com/andreqramos"><img src="https://avatars.githubusercontent.com/u/49326063?v=4" width="10%" /></a>
  <a href="https://github.com/Yuba4"><img src="https://avatars.githubusercontent.com/u/46278200?v=4" width="10%" /></a>
  <a href="https://github.com/Mavrik327"><img src="https://avatars.githubusercontent.com/u/121626149?v=4" width="10%" /></a>
  <a href="https://github.com/tikkatek"><img src="https://avatars.githubusercontent.com/u/26571381?v=4" width="10%" /></a>
  <a href="https://github.com/patrickdobler"><img src="https://avatars.githubusercontent.com/u/16536946?v=4" width="10%" /></a>
  <a href="https://github.com/benflap"><img src="https://avatars.githubusercontent.com/u/62034481?v=4" width="10%" /></a>
</p>

### Mentions

- [Why I Chose to Support Flow-Launcher](https://dev.to/appwrite/appwrite-loves-open-source-why-i-chose-to-support-flow-launcher-54pj) - Appwrite
- [Softpedia Editor's Pick](https://www.softpedia.com/get/System/Launchers-Shutdown-Tools/Flow-Launcher.shtml)

<img src="https://user-images.githubusercontent.com/6903107/144858082-8b654daf-60fb-4ee6-89b2-6183b73510d1.png" width="100%">

## ❔ Questions/Suggestions

Yes please, let us know in the [Q&A](https://github.com/Flow-Launcher/Flow.Launcher/discussions/categories/q-a) section. **Join our community on [Discord](https://discord.gg/AvgAQgh)!**

<img src="https://user-images.githubusercontent.com/6903107/144858082-8b654daf-60fb-4ee6-89b2-6183b73510d1.png" width="100%">

## Development

### Localization

Our project localization is based on [Crowdin](https://crowdin.com). If you would like to change them, please go to https://crowdin.com/project/flow-launcher.

### New changes

All changes to flow are captured via pull requests. Some new changes will have been merged but still pending release, this means whilst a change may not exist in the current release, it may very well have been accepted and merged into the dev branch and available as a pre-release download. It is therefore a good idea that before you start to make changes, search through the open and closed pull requests to make sure the change you intend to make is not already done.

Each of the pull requests will be marked with a milestone indicating the planned release version for the change.  

### Contributing

Contributions are very welcome, in addition to the main project(C#) there are also [documentation](https://github.com/Flow-Launcher/docs)(md), [website](https://github.com/Flow-Launcher/flow-launcher.github.io)(html/css) and [others](https://github.com/Flow-Launcher) that can be contributed to. If you are unsure of a change you want to make, let us know in the [Discussions](https://github.com/Flow-Launcher/Flow.Launcher/discussions/categories/ideas), otherwise feel free to put in a pull request.

You will find the main goals of flow placed under the [Projects board](https://github.com/Flow-Launcher/Flow.Launcher/projects), so feel free to contribute on that. If you would like to make small incremental changes, feel free to do so as well.

Get in touch if you like to join the Flow-Launcher Team and help build this great tool.

### Developing/Debugging

- Flow Launcher's target framework is .Net 7

- Install Visual Studio 2022

- Install .Net 9 SDK
  - via Visual Studio installer
  - via winget `winget install Microsoft.DotNet.SDK.7`
  - Manually from [here](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
