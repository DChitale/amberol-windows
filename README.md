# Amberol Windows

Offline desktop music player built with Tauri 2, Next.js App Router, TypeScript, Tailwind CSS, shadcn/ui-style primitives, Zustand, SQLite, Web Audio API, and jsmediatags.

## Features

- Recursive folder scanning for `mp3`, `flac`, `wav`, and `ogg`
- SQLite library cache for tracks, playlists, history, settings, and scanned folders
- ID3 metadata and cover extraction with `jsmediatags`
- Play, pause, seek, next, previous, volume, speed, shuffle, and repeat controls
- Keyboard shortcuts: Space, ArrowLeft, ArrowRight, ArrowUp, ArrowDown
- Web Audio API waveform and frequency bars synced to playback
- Playlist creation, persistence, drag-drop ordering, search, and large-list virtualization
- Lazy album art rendering and cached metadata to avoid repeated file reads
- Amberol-inspired dark translucent desktop layout

## Project Structure

```text
app/
  globals.css              Tailwind theme and Amberol-style window shell
  layout.tsx               Next.js root layout
  page.tsx                 App entry page
components/
  music-player.tsx         Main desktop player orchestration
  now-playing.tsx          Album art, transport, sliders, speed, waveform
  playlist-sidebar.tsx     Playlists, scan action, search, recent tracks
  queue-panel.tsx          Virtualized current queue with drag reordering
  track-row.tsx            Reusable track list row
  ui/                      Local shadcn/ui-style primitives
hooks/
  use-keyboard-shortcuts.ts
lib/
  metadata.ts              jsmediatags and audio duration enrichment
  tauri-api.ts             Tauri command wrappers
  utils.ts                 Formatting and class helpers
store/
  player-store.ts          Zustand playback and library state
src-tauri/
  src/lib.rs               Rust commands and SQLite access
  src/schema.sql           SQLite schema
  tauri.conf.json          Tauri 2 configuration
```

## Tauri Commands

- `scan_folder(folder)` recursively discovers audio files and upserts them into SQLite.
- `get_tracks()` and `get_recent_tracks(limit)` load cached library rows.
- `read_file_bytes(path)` streams local bytes to the frontend for `jsmediatags`.
- `update_track_metadata(input)` persists ID3 and duration cache fields.
- `get_playlists()`, `create_playlist(name)`, `update_playlist(id, name)`, and `delete_playlist(id)` manage playlists.
- `get_playlist_tracks(playlist_id)`, `add_to_playlist(playlist_id, track_id)`, and `set_playlist_tracks(playlist_id, track_ids)` persist playlist order.
- `record_playback(input)` stores listening history and updates `last_played_at`.
- `get_settings()`, `get_setting(key)`, and `set_setting(input)` persist app settings.

## Build Instructions

Install prerequisites:

- Node.js 20+
- Rust stable
- Microsoft Visual Studio Build Tools with the Desktop C++ workload
- WebView2 Runtime

Install dependencies:

```powershell
npm install
```

Run in development:

```powershell
npm run tauri:dev
```

Generate Windows installers:

```powershell
npm run tauri:build
```

The `.exe` and installer artifacts are emitted under `src-tauri/target/release/bundle/`.

## SQLite Schema

The schema is stored in `src-tauri/src/schema.sql` and is applied automatically on startup. The database file is created in the platform app data directory as `amberol.sqlite3`.
