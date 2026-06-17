PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS tracks (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  path TEXT NOT NULL UNIQUE,
  file_name TEXT NOT NULL,
  title TEXT,
  artist TEXT,
  album TEXT,
  duration REAL DEFAULT 0,
  cover_art TEXT,
  size_bytes INTEGER NOT NULL DEFAULT 0,
  modified_at INTEGER NOT NULL DEFAULT 0,
  added_at INTEGER NOT NULL,
  last_played_at INTEGER
);

CREATE TABLE IF NOT EXISTS playlists (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL UNIQUE,
  created_at INTEGER NOT NULL,
  updated_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS playlist_tracks (
  playlist_id INTEGER NOT NULL,
  track_id INTEGER NOT NULL,
  position INTEGER NOT NULL,
  added_at INTEGER NOT NULL,
  PRIMARY KEY (playlist_id, track_id),
  FOREIGN KEY (playlist_id) REFERENCES playlists(id) ON DELETE CASCADE,
  FOREIGN KEY (track_id) REFERENCES tracks(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS playback_history (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  track_id INTEGER NOT NULL,
  played_at INTEGER NOT NULL,
  position REAL NOT NULL DEFAULT 0,
  FOREIGN KEY (track_id) REFERENCES tracks(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS settings (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL,
  updated_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS scanned_folders (
  path TEXT PRIMARY KEY,
  scanned_at INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_tracks_added_at ON tracks(added_at DESC);
CREATE INDEX IF NOT EXISTS idx_tracks_artist_album ON tracks(artist, album);
CREATE INDEX IF NOT EXISTS idx_playlist_tracks_order ON playlist_tracks(playlist_id, position);
