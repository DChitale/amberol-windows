# Amberol Windows
<div align="center">
<img width="100" height="100" alt="Amberol logo emblem" src="https://raw.githubusercontent.com/DChitale/amberol-windows/v2/icon.png" />
</div>
<div align="center">
  <p>A lightweight, minimalist music player for Windows. Replicating the aesthetic of GNOME's Amberol, built natively on WPF, C#, and NAudio.</p>
  
  [![Version](https://img.shields.io/github/v/release/DChitale/amberol-windows?include_prereleases&style=for-the-badge&color=2563eb&label=version)](https://github.com/DChitale/amberol-windows/releases/latest)
  [![Platform](https://img.shields.io/badge/platform-Windows-0078d4?style=for-the-badge)](https://github.com/DChitale/amberol-windows/releases/latest)
  [![C#](https://img.shields.io/badge/C%23-Backend-178600?style=for-the-badge&logo=c-sharp&logoColor=white)](https://dotnet.microsoft.com/)
  [![License](https://img.shields.io/badge/license-MIT-3da639?style=for-the-badge)](LICENSE)
</div>

## Features
 
- **Minimalist & Adaptive UI:** Clean flat design with solid charcoal backdrops, smooth slide-out panel animations, and dynamic album-art colors.
- **Word-by-Word Synced Lyrics:** Full parsing of standard and enhanced `.lrc` files with fluid, real-time karaoke-style word highlighting at 60 FPS.
- **Advanced Audio Engine:** Powered by NAudio with a 10-band Equalizer, gapless playback, repeat/shuffle logic, and custom equalizer presets.
- **Opus & Ogg Playback:** Native stream decoding for Opus files (.opus, .ogg) using the Concentus library.
- **Playlist Management:** Complete support for creating custom playlists, organizing tracks, deleting playlists, and a context menu for adding songs.
- **Optimized Memory Footprint:** Built natively with WPF to keep resource utilization down.
- **Built-in Cache Utility:** Instantly clear cached tracks, scanned folders, and image thumbnail cache with one click.
---
 
## Supported Formats
 
- MP3 (`.mp3`)
- WAV (`.wav`)
- FLAC (`.flac`)
- Ogg Vorbis (`.ogg`)
- Opus (`.opus`)
---
 
## Getting Started
 
### Prerequisites
 
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (with .NET desktop development workload)
 
### Build Instructions
 
```powershell
# Clone and navigate to repository
git clone https://github.com/DChitale/amberol-windows
cd amberol-windows
 
# Restore packages and build
dotnet build -c Release
```
 
### Running the Application
 
```powershell
bin\Release\net8.0-windows10.0.19041.0\amberol-win.exe
```
 
---
 
## Architecture
 
### Frontend Layer
- **Window Shell:** MainWindow.xaml - Minimalist control grid and sliding panels.
- **Code-Behind:** MainWindow.xaml.cs - Window interactions, rendering ticks, and UI animations.
- **Converters & Helpers:** Base64 to image mapping, adaptive color extractor.
 
### Service Layer
- **Audio Engine:** AudioEngine.cs - NAudio output device wrapper, equalizer pipeline, and waveform rendering.
- **Local Database:** Database.cs - JSON-backed storage for tracks, settings, and playlists.
- **Custom Decoders:** OpusWaveStream.cs - Decodes Concentus packet streams to PCM WaveFormat for NAudio.
 
---
 
## Configuration
 
User settings, local playlist definitions, and scanned tracks are stored in local JSON databases located at:
```
%LocalAppData%\AmberolNet\
  ├── tracks.json
  ├── playlists.json
  ├── settings.json
  └── covers\ (cached album cover art images)
```
 
---
 

## License
 
MIT License - See LICENSE file for details.
