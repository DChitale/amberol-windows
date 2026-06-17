# Amberol Windows

[![Tauri Version](https://img.shields.io/badge/Tauri-v2-blue?logo=tauri)](https://tauri.app)
[![Next.js Version](https://img.shields.io/badge/Next.js-v14-black?logo=nextdotjs)](https://nextjs.org)
[![Rust Version](https://img.shields.io/badge/Rust-stable-orange?logo=rust)](https://www.rust-lang.org)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

An elegant, minimalist offline desktop music player inspired by the GNOME Amberol player, built specifically for Windows. Leveraging the power of Tauri v2, Next.js, Rust, and SQLite, it offers a high-performance audio engine wrapped in a gorgeous frosted-glass, ambient aesthetic that dynamically adapts to your album cover art.

---

## 🎨 Design Philosophy
Amberol Windows centers around simplicity and visual harmony. The user interface adaptively extracts colors from the active track's cover art to construct a smooth gradient backdrop with a subtle, translucent frosted-glass overlay. The sidebars overlay fluidly on top of the player in compact viewports, maximizing horizontal space while keeping controls within reach.

---

## ✨ Features

* **Subtle Frosted-Glass Aesthetics:** Dynamic backdrop color extraction from album art producing responsive, vibrant ambient gradients.
* **Smooth Waveform Animation:** 60fps canvas-based sub-pixel progress drawing, synced directly to the hardware audio clock.
* **Lossless & Lossy Audio Support:** Fast recursive scanning for `.mp3`, `.flac`, `.wav`, and `.ogg` formats.
* **Persistent Library Cache:** Highly reliable local SQLite storage for tracks, custom playlists, listening history, and application settings.
* **Playlist & Queue Management:** Dynamic queue panel featuring virtualized lists for large collections, drag-and-drop song reordering, search filtering, and single-click removal.
* **Hardware Media Controls & Transitions:** Responsive keyboard shortcuts (Space, Arrows) combined with volume crossfades at track ends and switches.
* **Frameless Window Custom Controls:** Borderless window design with tailored window drag boundaries and integrated minimize, maximize, and exit controls.

---

## 🛠️ Getting Started

### Prerequisites
Ensure your system meets the requirements for building Tauri applications on Windows:
* **Node.js** (v20 or newer)
* **Rust compiler & Cargo** (stable channel)
* **Microsoft C++ Build Tools** (including the Desktop development with C++ workload)
* **WebView2 Runtime**

### Installation
1. Clone this repository:
   ```bash
   git clone https://github.com/DChitale/amberol-windows.git
   cd amberol-windows
   ```
2. Install the frontend and development dependencies:
   ```bash
   npm install
   ```

### Development
Launch the local Next.js development server and the Tauri WebView container:
```bash
npm run tauri:dev
```

### Production Build
Generate an optimized production binary and native installers (`.msi`, `.exe`):
```bash
npm run tauri:build
```
The compiled release executable and installer bundles will be generated under `src-tauri/target/release/bundle/`.

---

## 📄 License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
