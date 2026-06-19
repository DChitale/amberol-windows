# Amberol Windows

[![Tauri Version](https://img.shields.io/badge/Tauri-v2-blue?logo=tauri)](https://tauri.app)
[![Next.js Version](https://img.shields.io/badge/Next.js-v14-black?logo=nextdotjs)](https://nextjs.org)
[![Rust Version](https://img.shields.io/badge/Rust-stable-orange?logo=rust)](https://www.rust-lang.org)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

A Windows desktop port of the GNOME [Amberol](https://gitlab.gnome.org/World/amberol) music player. Developed using Tauri v2, Next.js 14, and Rust, this project brings GNOME's minimalist, aesthetic music listening experience to Windows environments with native desktop integration.

![Preview](https://ik.imagekit.io/dchitale/Amberol/image.png)


## Features

- **Dynamic Backdrop Extraction:** Extracts ambient color profiles from album art to generate responsive gradients.
- **Precision Audio Waveform:** A canvas-based, 60fps waveform progress drawing utility synchronized directly with the hardware audio clock.
- **Equalizer Support:** Integrated equalizer supporting standard presets (Bass, Vocal, Pop, Classical, Rock, Flat) and custom gain adjustments.
- **Local Cache Storage:** A robust JSON-based file storage backend managed by Rust to cache scanned tracks, custom playlists, listening history, and configurations.
- **Recursive Audio Directory Scanning:** Fast recursive lookup and tag reading for lossless (`.flac`, `.wav`) and lossy (`.mp3`, `.ogg`) audio formats.
- **Playlist & Queue Management:** High-performance virtualized track queues supporting drag-and-drop reordering, filtering, and single-click removal.
- **Custom Window Chrome:** Clean, borderless window framing with native drag boundaries and custom window control buttons.
- **Keyboard Shortcuts & Transitions:** Integrated hardware shortcut keys (Space, Arrow keys) and smooth audio crossfades on track changes.
- **Automatic App Updates:** Built-in update notifications powered by Tauri's native updater plugin.

---

## Technology Stack

- **Frontend Framework:** Next.js
- **Styling:** Tailwind CSS, Radix UI Primitives, Lucide React
- **Application Engine:** Tauri v2
- **Backend Systems:** Rust

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for more information.
