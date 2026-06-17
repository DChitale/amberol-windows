use chrono::Utc;
use rusqlite::{params, Connection, OptionalExtension};
use serde::{Deserialize, Serialize};
use std::{
    fs,
    path::{Path, PathBuf},
    sync::Mutex,
};
use tauri::{AppHandle, Manager, State};
use walkdir::WalkDir;

const AUDIO_EXTENSIONS: &[&str] = &["mp3", "flac", "wav", "ogg"];

struct AppDb {
    conn: Mutex<Connection>,
}

#[derive(Debug, Serialize)]
struct Track {
    id: i64,
    path: String,
    file_name: String,
    title: Option<String>,
    artist: Option<String>,
    album: Option<String>,
    duration: f64,
    cover_art: Option<String>,
    size_bytes: i64,
    modified_at: i64,
    added_at: i64,
    last_played_at: Option<i64>,
}

#[derive(Debug, Serialize)]
struct Playlist {
    id: i64,
    name: String,
    track_count: i64,
    duration: f64,
    created_at: i64,
    updated_at: i64,
}

#[derive(Debug, Deserialize)]
struct MetadataInput {
    id: i64,
    title: Option<String>,
    artist: Option<String>,
    album: Option<String>,
    duration: Option<f64>,
    cover_art: Option<String>,
}

#[derive(Debug, Deserialize)]
struct HistoryInput {
    track_id: i64,
    position: f64,
}

#[derive(Debug, Deserialize)]
struct SettingInput {
    key: String,
    value: String,
}

#[derive(Debug, Serialize)]
struct ScanResult {
    discovered: usize,
    inserted_or_updated: usize,
    tracks: Vec<Track>,
}

fn now() -> i64 {
    Utc::now().timestamp()
}

fn app_db_path(app: &AppHandle) -> Result<PathBuf, String> {
    let dir = app
        .path()
        .app_data_dir()
        .map_err(|err| format!("Unable to resolve app data directory: {err}"))?;
    fs::create_dir_all(&dir)
        .map_err(|err| format!("Unable to create app data directory: {err}"))?;
    Ok(dir.join("amberol.sqlite3"))
}

fn init_db(app: &AppHandle) -> Result<AppDb, String> {
    let conn = Connection::open(app_db_path(app)?).map_err(|err| err.to_string())?;
    conn.execute_batch(include_str!("schema.sql"))
        .map_err(|err| err.to_string())?;
    seed_library_playlist(&conn)?;
    Ok(AppDb {
        conn: Mutex::new(conn),
    })
}

fn seed_library_playlist(conn: &Connection) -> Result<(), String> {
    let current = now();
    conn.execute(
        "INSERT OR IGNORE INTO playlists (name, created_at, updated_at) VALUES ('Library', ?1, ?1)",
        params![current],
    )
    .map_err(|err| err.to_string())?;
    Ok(())
}

fn is_audio_file(path: &Path) -> bool {
    path.extension()
        .and_then(|ext| ext.to_str())
        .map(|ext| {
            AUDIO_EXTENSIONS
                .iter()
                .any(|allowed| ext.eq_ignore_ascii_case(allowed))
        })
        .unwrap_or(false)
}

fn track_from_row(row: &rusqlite::Row<'_>) -> rusqlite::Result<Track> {
    Ok(Track {
        id: row.get(0)?,
        path: row.get(1)?,
        file_name: row.get(2)?,
        title: row.get(3)?,
        artist: row.get(4)?,
        album: row.get(5)?,
        duration: row.get(6)?,
        cover_art: row.get(7)?,
        size_bytes: row.get(8)?,
        modified_at: row.get(9)?,
        added_at: row.get(10)?,
        last_played_at: row.get(11)?,
    })
}

fn query_tracks(
    conn: &Connection,
    sql: &str,
    params: &[&dyn rusqlite::ToSql],
) -> Result<Vec<Track>, String> {
    let mut stmt = conn.prepare(sql).map_err(|err| err.to_string())?;
    let rows = stmt
        .query_map(params, track_from_row)
        .map_err(|err| err.to_string())?;

    rows.collect::<Result<Vec<_>, _>>()
        .map_err(|err| err.to_string())
}

#[tauri::command]
fn scan_folder(folder: String, db: State<'_, AppDb>) -> Result<ScanResult, String> {
    let root = PathBuf::from(&folder);
    if !root.exists() || !root.is_dir() {
        return Err("Selected path is not a folder".to_string());
    }

    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    let current = now();
    let mut discovered = 0usize;
    let mut inserted_or_updated = 0usize;

    for entry in WalkDir::new(&root)
        .follow_links(false)
        .into_iter()
        .filter_map(Result::ok)
    {
        let path = entry.path();
        if !path.is_file() || !is_audio_file(path) {
            continue;
        }

        discovered += 1;
        let metadata = fs::metadata(path).map_err(|err| err.to_string())?;
        let modified_at = metadata
            .modified()
            .ok()
            .and_then(|time| time.duration_since(std::time::UNIX_EPOCH).ok())
            .map(|duration| duration.as_secs() as i64)
            .unwrap_or(0);
        let file_name = path
            .file_stem()
            .and_then(|name| name.to_str())
            .unwrap_or("Unknown Track")
            .to_string();
        let path_string = path.to_string_lossy().to_string();

        let changed = conn
            .execute(
                "INSERT INTO tracks (path, file_name, title, size_bytes, modified_at, added_at)
                 VALUES (?1, ?2, ?2, ?3, ?4, ?5)
                 ON CONFLICT(path) DO UPDATE SET
                   file_name = excluded.file_name,
                   size_bytes = excluded.size_bytes,
                   modified_at = excluded.modified_at",
                params![
                    path_string,
                    file_name,
                    metadata.len() as i64,
                    modified_at,
                    current
                ],
            )
            .map_err(|err| err.to_string())?;
        inserted_or_updated += changed;
    }

    conn.execute(
        "INSERT INTO scanned_folders (path, scanned_at) VALUES (?1, ?2)
         ON CONFLICT(path) DO UPDATE SET scanned_at = excluded.scanned_at",
        params![folder, current],
    )
    .map_err(|err| err.to_string())?;

    let tracks = query_tracks(
        &conn,
        "SELECT id, path, file_name, title, artist, album, duration, cover_art, size_bytes,
                modified_at, added_at, last_played_at
         FROM tracks ORDER BY added_at DESC, id DESC",
        &[],
    )?;

    Ok(ScanResult {
        discovered,
        inserted_or_updated,
        tracks,
    })
}

#[tauri::command]
fn get_tracks(db: State<'_, AppDb>) -> Result<Vec<Track>, String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    query_tracks(
        &conn,
        "SELECT id, path, file_name, title, artist, album, duration, cover_art, size_bytes,
                modified_at, added_at, last_played_at
         FROM tracks ORDER BY artist IS NULL, artist, album, title, file_name",
        &[],
    )
}

#[tauri::command]
fn get_recent_tracks(limit: i64, db: State<'_, AppDb>) -> Result<Vec<Track>, String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    query_tracks(
        &conn,
        "SELECT id, path, file_name, title, artist, album, duration, cover_art, size_bytes,
                modified_at, added_at, last_played_at
         FROM tracks ORDER BY added_at DESC, id DESC LIMIT ?1",
        &[&limit],
    )
}

#[tauri::command]
fn read_file_bytes(path: String) -> Result<Vec<u8>, String> {
    fs::read(path).map_err(|err| err.to_string())
}

#[tauri::command]
fn update_track_metadata(input: MetadataInput, db: State<'_, AppDb>) -> Result<(), String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    conn.execute(
        "UPDATE tracks
         SET title = COALESCE(?2, title),
             artist = COALESCE(?3, artist),
             album = COALESCE(?4, album),
             duration = COALESCE(?5, duration),
             cover_art = COALESCE(?6, cover_art)
         WHERE id = ?1",
        params![
            input.id,
            input.title,
            input.artist,
            input.album,
            input.duration,
            input.cover_art
        ],
    )
    .map_err(|err| err.to_string())?;
    Ok(())
}

#[tauri::command]
fn get_playlists(db: State<'_, AppDb>) -> Result<Vec<Playlist>, String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    let mut stmt = conn
        .prepare(
            "SELECT p.id, p.name, COUNT(pt.track_id) AS track_count,
                    COALESCE(SUM(t.duration), 0) AS duration, p.created_at, p.updated_at
             FROM playlists p
             LEFT JOIN playlist_tracks pt ON p.id = pt.playlist_id
             LEFT JOIN tracks t ON t.id = pt.track_id
             GROUP BY p.id
             ORDER BY p.name = 'Library' DESC, p.updated_at DESC",
        )
        .map_err(|err| err.to_string())?;

    let rows = stmt
        .query_map([], |row| {
            Ok(Playlist {
                id: row.get(0)?,
                name: row.get(1)?,
                track_count: row.get(2)?,
                duration: row.get(3)?,
                created_at: row.get(4)?,
                updated_at: row.get(5)?,
            })
        })
        .map_err(|err| err.to_string())?;

    rows.collect::<Result<Vec<_>, _>>()
        .map_err(|err| err.to_string())
}

#[tauri::command]
fn create_playlist(name: String, db: State<'_, AppDb>) -> Result<i64, String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    let current = now();
    conn.execute(
        "INSERT INTO playlists (name, created_at, updated_at) VALUES (?1, ?2, ?2)",
        params![name, current],
    )
    .map_err(|err| err.to_string())?;
    Ok(conn.last_insert_rowid())
}

#[tauri::command]
fn update_playlist(id: i64, name: String, db: State<'_, AppDb>) -> Result<(), String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    conn.execute(
        "UPDATE playlists SET name = ?2, updated_at = ?3 WHERE id = ?1 AND name != 'Library'",
        params![id, name, now()],
    )
    .map_err(|err| err.to_string())?;
    Ok(())
}

#[tauri::command]
fn delete_playlist(id: i64, db: State<'_, AppDb>) -> Result<(), String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    conn.execute(
        "DELETE FROM playlists WHERE id = ?1 AND name != 'Library'",
        params![id],
    )
    .map_err(|err| err.to_string())?;
    Ok(())
}

#[tauri::command]
fn get_playlist_tracks(playlist_id: i64, db: State<'_, AppDb>) -> Result<Vec<Track>, String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    query_tracks(
        &conn,
        "SELECT t.id, t.path, t.file_name, t.title, t.artist, t.album, t.duration, t.cover_art,
                t.size_bytes, t.modified_at, t.added_at, t.last_played_at
         FROM tracks t
         JOIN playlist_tracks pt ON pt.track_id = t.id
         WHERE pt.playlist_id = ?1
         ORDER BY pt.position ASC",
        &[&playlist_id],
    )
}

#[tauri::command]
fn add_to_playlist(playlist_id: i64, track_id: i64, db: State<'_, AppDb>) -> Result<(), String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    let position: i64 = conn
        .query_row(
            "SELECT COALESCE(MAX(position) + 1, 0) FROM playlist_tracks WHERE playlist_id = ?1",
            params![playlist_id],
            |row| row.get(0),
        )
        .map_err(|err| err.to_string())?;
    conn.execute(
        "INSERT OR IGNORE INTO playlist_tracks (playlist_id, track_id, position, added_at)
         VALUES (?1, ?2, ?3, ?4)",
        params![playlist_id, track_id, position, now()],
    )
    .map_err(|err| err.to_string())?;
    conn.execute(
        "UPDATE playlists SET updated_at = ?2 WHERE id = ?1",
        params![playlist_id, now()],
    )
    .map_err(|err| err.to_string())?;
    Ok(())
}

#[tauri::command]
fn set_playlist_tracks(
    playlist_id: i64,
    track_ids: Vec<i64>,
    db: State<'_, AppDb>,
) -> Result<(), String> {
    let mut conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    let tx = conn.transaction().map_err(|err| err.to_string())?;
    tx.execute(
        "DELETE FROM playlist_tracks WHERE playlist_id = ?1",
        params![playlist_id],
    )
    .map_err(|err| err.to_string())?;

    for (position, track_id) in track_ids.iter().enumerate() {
        tx.execute(
            "INSERT INTO playlist_tracks (playlist_id, track_id, position, added_at)
             VALUES (?1, ?2, ?3, ?4)",
            params![playlist_id, track_id, position as i64, now()],
        )
        .map_err(|err| err.to_string())?;
    }

    tx.execute(
        "UPDATE playlists SET updated_at = ?2 WHERE id = ?1",
        params![playlist_id, now()],
    )
    .map_err(|err| err.to_string())?;
    tx.commit().map_err(|err| err.to_string())?;
    Ok(())
}

#[tauri::command]
fn record_playback(input: HistoryInput, db: State<'_, AppDb>) -> Result<(), String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    let current = now();
    conn.execute(
        "INSERT INTO playback_history (track_id, played_at, position) VALUES (?1, ?2, ?3)",
        params![input.track_id, current, input.position],
    )
    .map_err(|err| err.to_string())?;
    conn.execute(
        "UPDATE tracks SET last_played_at = ?2 WHERE id = ?1",
        params![input.track_id, current],
    )
    .map_err(|err| err.to_string())?;
    Ok(())
}

#[tauri::command]
fn get_settings(db: State<'_, AppDb>) -> Result<Vec<(String, String)>, String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    let mut stmt = conn
        .prepare("SELECT key, value FROM settings ORDER BY key")
        .map_err(|err| err.to_string())?;
    let rows = stmt
        .query_map([], |row| Ok((row.get(0)?, row.get(1)?)))
        .map_err(|err| err.to_string())?;
    rows.collect::<Result<Vec<_>, _>>()
        .map_err(|err| err.to_string())
}

#[tauri::command]
fn set_setting(input: SettingInput, db: State<'_, AppDb>) -> Result<(), String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    conn.execute(
        "INSERT INTO settings (key, value, updated_at) VALUES (?1, ?2, ?3)
         ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at",
        params![input.key, input.value, now()],
    )
    .map_err(|err| err.to_string())?;
    Ok(())
}

#[tauri::command]
fn get_setting(key: String, db: State<'_, AppDb>) -> Result<Option<String>, String> {
    let conn = db
        .conn
        .lock()
        .map_err(|_| "Database lock failed".to_string())?;
    conn.query_row(
        "SELECT value FROM settings WHERE key = ?1",
        params![key],
        |row| row.get(0),
    )
    .optional()
    .map_err(|err| err.to_string())
}

pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .setup(|app| {
            let db = init_db(app.handle())?;
            app.manage(db);
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            scan_folder,
            get_tracks,
            get_recent_tracks,
            read_file_bytes,
            update_track_metadata,
            get_playlists,
            create_playlist,
            update_playlist,
            delete_playlist,
            get_playlist_tracks,
            add_to_playlist,
            set_playlist_tracks,
            record_playback,
            get_settings,
            set_setting,
            get_setting
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
