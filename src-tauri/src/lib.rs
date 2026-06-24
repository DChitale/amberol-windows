use base64::Engine;
use chrono::Utc;
use lofty::prelude::*;
use serde::{Deserialize, Serialize};
use std::{
    fs,
    path::{Path, PathBuf},
    sync::Mutex,
};
use tauri::{AppHandle, Manager, State};
use walkdir::WalkDir;

const AUDIO_EXTENSIONS: &[&str] = &["mp3", "flac", "wav", "ogg"];

pub struct AppStore {
    lock: Mutex<()>,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
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

#[derive(Debug, Serialize, Clone)]
struct Playlist {
    id: i64,
    name: String,
    track_count: i64,
    duration: f64,
    created_at: i64,
    updated_at: i64,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct PlaylistStore {
    id: i64,
    name: String,
    created_at: i64,
    updated_at: i64,
    track_ids: Vec<i64>,
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

#[derive(Debug, Serialize, Deserialize, Clone)]
struct HistoryStore {
    track_id: i64,
    played_at: i64,
    position: f64,
}

#[derive(Debug, Deserialize)]
struct SettingInput {
    key: String,
    value: String,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct SettingStore {
    key: String,
    value: String,
    updated_at: i64,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
struct MetadataOutput {
    title: Option<String>,
    artist: Option<String>,
    album: Option<String>,
    duration: Option<f64>,
    cover_art: Option<String>,
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

fn extract_metadata(path: &Path) -> MetadataOutput {
    let mut result = MetadataOutput {
        title: None,
        artist: None,
        album: None,
        duration: None,
        cover_art: None,
    };

    let tagged_file = match lofty::read_from_path(path) {
        Ok(f) => f,
        Err(_) => return result,
    };

    // Extract duration from file properties
    let props = tagged_file.properties();
    let dur = props.duration();
    let seconds = dur.as_secs_f64();
    if seconds > 0.0 {
        result.duration = Some(seconds);
    }

    // Try primary tag first, fall back to any tag
    let tag = tagged_file.primary_tag().or_else(|| tagged_file.first_tag());
    if let Some(tag) = tag {
        result.title = tag.title().map(|s| s.to_string());
        result.artist = tag.artist().map(|s| s.to_string());
        result.album = tag.album().map(|s| s.to_string());

        // Extract cover art and encode as base64 data URL
        if let Some(picture) = tag.pictures().first() {
            let mime = picture.mime_type();
            let mime_str = match mime {
                Some(m) => m.as_str().to_string(),
                None => "image/jpeg".to_string(),
            };
            let b64 = base64::engine::general_purpose::STANDARD.encode(picture.data());
            result.cover_art = Some(format!("data:{};base64,{}", mime_str, b64));
        }
    }

    result
}

fn get_app_dir(app: &AppHandle) -> Result<PathBuf, String> {
    let dir = app
        .path()
        .app_data_dir()
        .map_err(|err| format!("Unable to resolve app data directory: {err}"))?;
    fs::create_dir_all(&dir)
        .map_err(|err| format!("Unable to create app data directory: {err}"))?;
    Ok(dir)
}

fn read_tracks_file(app: &AppHandle) -> Result<Vec<Track>, String> {
    let path = get_app_dir(app)?.join("tracks.json");
    if !path.exists() {
        return Ok(Vec::new());
    }
    let data = fs::read_to_string(&path).map_err(|err| err.to_string())?;
    let tracks: Vec<Track> = serde_json::from_str(&data).unwrap_or_default();
    Ok(tracks)
}

fn write_tracks_file(app: &AppHandle, tracks: &[Track]) -> Result<(), String> {
    let path = get_app_dir(app)?.join("tracks.json");
    let data = serde_json::to_string_pretty(tracks).map_err(|err| err.to_string())?;
    fs::write(&path, data).map_err(|err| err.to_string())
}

fn read_playlists_file(app: &AppHandle) -> Result<Vec<PlaylistStore>, String> {
    let path = get_app_dir(app)?.join("playlists.json");
    if !path.exists() {
        let current = now();
        let library = PlaylistStore {
            id: 1,
            name: "Library".to_string(),
            created_at: current,
            updated_at: current,
            track_ids: Vec::new(),
        };
        let list = vec![library];
        write_playlists_file(app, &list)?;
        return Ok(list);
    }
    let data = fs::read_to_string(&path).map_err(|err| err.to_string())?;
    let playlists: Vec<PlaylistStore> = serde_json::from_str(&data).unwrap_or_default();
    Ok(playlists)
}

fn write_playlists_file(app: &AppHandle, playlists: &[PlaylistStore]) -> Result<(), String> {
    let path = get_app_dir(app)?.join("playlists.json");
    let data = serde_json::to_string_pretty(playlists).map_err(|err| err.to_string())?;
    fs::write(&path, data).map_err(|err| err.to_string())
}

fn read_settings_file(app: &AppHandle) -> Result<Vec<SettingStore>, String> {
    let path = get_app_dir(app)?.join("settings.json");
    if !path.exists() {
        return Ok(Vec::new());
    }
    let data = fs::read_to_string(&path).map_err(|err| err.to_string())?;
    let settings: Vec<SettingStore> = serde_json::from_str(&data).unwrap_or_default();
    Ok(settings)
}

fn write_settings_file(app: &AppHandle, settings: &[SettingStore]) -> Result<(), String> {
    let path = get_app_dir(app)?.join("settings.json");
    let data = serde_json::to_string_pretty(settings).map_err(|err| err.to_string())?;
    fs::write(&path, data).map_err(|err| err.to_string())
}

#[tauri::command]
fn scan_folder(folder: String, app: AppHandle, state: State<'_, AppStore>) -> Result<ScanResult, String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let root = PathBuf::from(&folder);
    if !root.exists() || !root.is_dir() {
        return Err("Selected path is not a folder".to_string());
    }

    let mut tracks = read_tracks_file(&app)?;
    let current = now();
    let mut discovered = 0usize;
    let mut inserted_or_updated = 0usize;

    let mut next_id = tracks.iter().map(|t| t.id).max().unwrap_or(0) + 1;

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

        let existing_index = tracks.iter().position(|t| t.path == path_string);
        if let Some(idx) = existing_index {
            let track = &mut tracks[idx];
            track.file_name = file_name;
            track.size_bytes = metadata.len() as i64;
            track.modified_at = modified_at;
            inserted_or_updated += 1;
        } else {
            let meta = extract_metadata(path);
            let track = Track {
                id: next_id,
                path: path_string,
                file_name: file_name.clone(),
                title: meta.title.or(Some(file_name)),
                artist: meta.artist,
                album: meta.album,
                duration: meta.duration.unwrap_or(0.0),
                cover_art: meta.cover_art,
                size_bytes: metadata.len() as i64,
                modified_at,
                added_at: current,
                last_played_at: None,
            };
            tracks.push(track);
            next_id += 1;
            inserted_or_updated += 1;
        }
    }

    // Save scanned folder to settings
    let mut settings = read_settings_file(&app)?;
    let settings_current = settings.iter().position(|s| s.key == "last_scanned_folder");
    if let Some(idx) = settings_current {
        settings[idx].value = folder.clone();
        settings[idx].updated_at = current;
    } else {
        settings.push(SettingStore {
            key: "last_scanned_folder".to_string(),
            value: folder,
            updated_at: current,
        });
    }
    write_settings_file(&app, &settings)?;

    write_tracks_file(&app, &tracks)?;

    let mut result_tracks = tracks.clone();
    result_tracks.sort_by(|a, b| {
        b.added_at.cmp(&a.added_at).then_with(|| b.id.cmp(&a.id))
    });

    Ok(ScanResult {
        discovered,
        inserted_or_updated,
        tracks: result_tracks,
    })
}

#[tauri::command]
fn get_tracks(app: AppHandle, state: State<'_, AppStore>) -> Result<Vec<Track>, String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let mut tracks = read_tracks_file(&app)?;
    
    tracks.sort_by(|a, b| {
        let artist_order = match (&a.artist, &b.artist) {
            (Some(x), Some(y)) => x.cmp(y),
            (Some(_), None) => std::cmp::Ordering::Less,
            (None, Some(_)) => std::cmp::Ordering::Greater,
            (None, None) => std::cmp::Ordering::Equal,
        };
        artist_order
            .then_with(|| a.album.cmp(&b.album))
            .then_with(|| a.title.cmp(&b.title))
            .then_with(|| a.file_name.cmp(&b.file_name))
    });

    Ok(tracks)
}

#[tauri::command]
fn get_recent_tracks(limit: i64, app: AppHandle, state: State<'_, AppStore>) -> Result<Vec<Track>, String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let mut tracks = read_tracks_file(&app)?;
    tracks.sort_by(|a, b| {
        b.added_at.cmp(&a.added_at).then_with(|| b.id.cmp(&a.id))
    });
    let result = tracks.into_iter().take(limit as usize).collect();
    Ok(result)
}

#[tauri::command]
fn read_file_bytes(path: String) -> Result<Vec<u8>, String> {
    fs::read(path).map_err(|err| err.to_string())
}

#[tauri::command]
fn read_metadata(path: String) -> Result<MetadataOutput, String> {
    let p = Path::new(&path);
    if !p.exists() || !p.is_file() {
        return Err(format!("File not found: {}", path));
    }
    Ok(extract_metadata(p))
}

#[tauri::command]
fn update_track_metadata(input: MetadataInput, app: AppHandle, state: State<'_, AppStore>) -> Result<(), String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let mut tracks = read_tracks_file(&app)?;
    
    let position = tracks.iter().position(|t| t.id == input.id);
    if let Some(idx) = position {
        let track = &mut tracks[idx];
        if let Some(title) = input.title { track.title = Some(title); }
        if let Some(artist) = input.artist { track.artist = Some(artist); }
        if let Some(album) = input.album { track.album = Some(album); }
        if let Some(duration) = input.duration { track.duration = duration; }
        if let Some(cover_art) = input.cover_art { track.cover_art = Some(cover_art); }
        write_tracks_file(&app, &tracks)?;
        Ok(())
    } else {
        Err("Track not found".to_string())
    }
}

#[tauri::command]
fn get_playlists(app: AppHandle, state: State<'_, AppStore>) -> Result<Vec<Playlist>, String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let playlists = read_playlists_file(&app)?;
    let tracks = read_tracks_file(&app)?;

    let mut result = Vec::new();
    for p in playlists {
        let (track_count, duration) = if p.name == "Library" {
            let count = tracks.len() as i64;
            let dur = tracks.iter().map(|t| t.duration).sum();
            (count, dur)
        } else {
            let count = p.track_ids.len() as i64;
            let mut dur = 0.0;
            for tid in &p.track_ids {
                if let Some(track) = tracks.iter().find(|t| t.id == *tid) {
                    dur += track.duration;
                }
            }
            (count, dur)
        };
        
        result.push(Playlist {
            id: p.id,
            name: p.name,
            track_count,
            duration,
            created_at: p.created_at,
            updated_at: p.updated_at,
        });
    }

    result.sort_by(|a, b| {
        let a_is_lib = a.name == "Library";
        let b_is_lib = b.name == "Library";
        match (a_is_lib, b_is_lib) {
            (true, false) => std::cmp::Ordering::Less,
            (false, true) => std::cmp::Ordering::Greater,
            _ => b.updated_at.cmp(&a.updated_at),
        }
    });

    Ok(result)
}

#[tauri::command]
fn create_playlist(name: String, app: AppHandle, state: State<'_, AppStore>) -> Result<i64, String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let mut playlists = read_playlists_file(&app)?;
    
    if playlists.iter().any(|p| p.name == name) {
        return Err("Playlist already exists".to_string());
    }

    let next_id = playlists.iter().map(|p| p.id).max().unwrap_or(0) + 1;
    let current = now();
    let playlist = PlaylistStore {
        id: next_id,
        name,
        created_at: current,
        updated_at: current,
        track_ids: Vec::new(),
    };
    playlists.push(playlist);
    write_playlists_file(&app, &playlists)?;
    Ok(next_id)
}

#[tauri::command]
fn update_playlist(id: i64, name: String, app: AppHandle, state: State<'_, AppStore>) -> Result<(), String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let mut playlists = read_playlists_file(&app)?;
    
    let position = playlists.iter().position(|p| p.id == id);
    if let Some(idx) = position {
        if playlists[idx].name == "Library" {
            return Err("Cannot rename default Library playlist".to_string());
        }
        playlists[idx].name = name;
        playlists[idx].updated_at = now();
        write_playlists_file(&app, &playlists)?;
        Ok(())
    } else {
        Err("Playlist not found".to_string())
    }
}

#[tauri::command]
fn delete_playlist(id: i64, app: AppHandle, state: State<'_, AppStore>) -> Result<(), String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let mut playlists = read_playlists_file(&app)?;
    
    let position = playlists.iter().position(|p| p.id == id);
    if let Some(idx) = position {
        if playlists[idx].name == "Library" {
            return Err("Cannot delete default Library playlist".to_string());
        }
        playlists.remove(idx);
        write_playlists_file(&app, &playlists)?;
        Ok(())
    } else {
        Err("Playlist not found".to_string())
    }
}

#[tauri::command]
fn get_playlist_tracks(playlist_id: i64, app: AppHandle, state: State<'_, AppStore>) -> Result<Vec<Track>, String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let playlists = read_playlists_file(&app)?;
    let mut tracks = read_tracks_file(&app)?;

    let playlist = playlists.iter().find(|p| p.id == playlist_id);
    if let Some(p) = playlist {
        if p.name == "Library" {
            tracks.sort_by(|a, b| {
                let artist_order = match (&a.artist, &b.artist) {
                    (Some(x), Some(y)) => x.cmp(y),
                    (Some(_), None) => std::cmp::Ordering::Less,
                    (None, Some(_)) => std::cmp::Ordering::Greater,
                    (None, None) => std::cmp::Ordering::Equal,
                };
                artist_order
                    .then_with(|| a.album.cmp(&b.album))
                    .then_with(|| a.title.cmp(&b.title))
                    .then_with(|| a.file_name.cmp(&b.file_name))
            });
            Ok(tracks)
        } else {
            let mut result = Vec::new();
            for tid in &p.track_ids {
                if let Some(track) = tracks.iter().find(|t| t.id == *tid) {
                    result.push(track.clone());
                }
            }
            Ok(result)
        }
    } else {
        Err("Playlist not found".to_string())
    }
}

#[tauri::command]
fn add_to_playlist(playlist_id: i64, track_id: i64, app: AppHandle, state: State<'_, AppStore>) -> Result<(), String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let mut playlists = read_playlists_file(&app)?;
    
    let position = playlists.iter().position(|p| p.id == playlist_id);
    if let Some(idx) = position {
        let playlist = &mut playlists[idx];
        if !playlist.track_ids.contains(&track_id) {
            playlist.track_ids.push(track_id);
            playlist.updated_at = now();
            write_playlists_file(&app, &playlists)?;
        }
        Ok(())
    } else {
        Err("Playlist not found".to_string())
    }
}

#[tauri::command]
fn set_playlist_tracks(playlist_id: i64, track_ids: Vec<i64>, app: AppHandle, state: State<'_, AppStore>) -> Result<(), String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let mut playlists = read_playlists_file(&app)?;
    
    let position = playlists.iter().position(|p| p.id == playlist_id);
    if let Some(idx) = position {
        playlists[idx].track_ids = track_ids;
        playlists[idx].updated_at = now();
        write_playlists_file(&app, &playlists)?;
        Ok(())
    } else {
        Err("Playlist not found".to_string())
    }
}

#[tauri::command]
fn record_playback(input: HistoryInput, app: AppHandle, state: State<'_, AppStore>) -> Result<(), String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let current = now();
    
    let path = get_app_dir(&app)?.join("history.json");
    let mut history: Vec<HistoryStore> = if path.exists() {
        let data = fs::read_to_string(&path).unwrap_or_default();
        serde_json::from_str(&data).unwrap_or_default()
    } else {
        Vec::new()
    };
    
    history.push(HistoryStore {
        track_id: input.track_id,
        played_at: current,
        position: input.position,
    });
    
    let data = serde_json::to_string_pretty(&history).map_err(|err| err.to_string())?;
    fs::write(&path, data).map_err(|err| err.to_string())?;

    let mut tracks = read_tracks_file(&app)?;
    if let Some(idx) = tracks.iter().position(|t| t.id == input.track_id) {
        tracks[idx].last_played_at = Some(current);
        write_tracks_file(&app, &tracks)?;
    }
    
    Ok(())
}

#[tauri::command]
fn get_settings(app: AppHandle, state: State<'_, AppStore>) -> Result<Vec<(String, String)>, String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let settings = read_settings_file(&app)?;
    let result = settings.into_iter().map(|s| (s.key, s.value)).collect();
    Ok(result)
}

#[tauri::command]
fn set_setting(input: SettingInput, app: AppHandle, state: State<'_, AppStore>) -> Result<(), String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let mut settings = read_settings_file(&app)?;
    
    let position = settings.iter().position(|s| s.key == input.key);
    let current = now();
    if let Some(idx) = position {
        settings[idx].value = input.value;
        settings[idx].updated_at = current;
    } else {
        settings.push(SettingStore {
            key: input.key,
            value: input.value,
            updated_at: current,
        });
    }
    write_settings_file(&app, &settings)?;
    Ok(())
}

#[tauri::command]
fn get_setting(key: String, app: AppHandle, state: State<'_, AppStore>) -> Result<Option<String>, String> {
    let _guard = state.lock.lock().map_err(|_| "Lock failed".to_string())?;
    let settings = read_settings_file(&app)?;
    let setting = settings.iter().find(|s| s.key == key).map(|s| s.value.clone());
    Ok(setting)
}

#[derive(Debug, Serialize, Deserialize)]
pub struct EqPreset {
    name: String,
    gains: Vec<f64>,
}

fn parse_fac_file(content: &str) -> Result<EqPreset, String> {
    let mut name = String::new();
    let mut gains = Vec::new();
    let lines: Vec<&str> = content.lines().collect();
    
    if lines.len() >= 3 {
        name = lines[2].trim().to_string();
    }
    
    let mut num_bands = 0;
    let mut eq_start_line = 0;
    for (i, line) in lines.iter().enumerate() {
        if line.contains("Number of EQ Bands") {
            if let Some(num_str) = line.split(':').next() {
                num_bands = num_str.trim().parse::<usize>().unwrap_or(0);
                eq_start_line = i;
                break;
            }
        }
    }
    
    if num_bands > 0 && eq_start_line > 0 {
        let mut line_idx = eq_start_line + 2; // Skip "Number of EQ Bands" and "On/Off Flag"
        for _ in 0..num_bands {
            if line_idx + 2 >= lines.len() {
                break;
            }
            let gain_line = lines[line_idx + 2];
            if let Some(gain_str) = gain_line.split(':').next() {
                let gain = gain_str.trim().parse::<f64>().unwrap_or(0.0);
                gains.push(gain);
            }
            line_idx += 3;
        }
    }
    
    Ok(EqPreset { name, gains })
}

#[tauri::command]
fn get_equalizer_presets(app: AppHandle) -> Result<Vec<EqPreset>, String> {
    let mut presets = Vec::new();
    
    // Check resource directory (production)
    let mut presets_path = None;
    if let Ok(resource_dir) = app.path().resource_dir() {
        let path = resource_dir.join("presets");
        if path.exists() && path.is_dir() {
            presets_path = Some(path);
        }
    }
    
    // Fallback to current directory (dev environment)
    if presets_path.is_none() {
        if let Ok(current_dir) = std::env::current_dir() {
            let path = current_dir.join("presets");
            if path.exists() && path.is_dir() {
                presets_path = Some(path);
            }
        }
    }
    
    let path = match presets_path {
        Some(p) => p,
        None => return Ok(presets),
    };
    
    if let Ok(entries) = fs::read_dir(path) {
        for entry in entries.filter_map(Result::ok) {
            let path = entry.path();
            if path.is_file() && path.extension().and_then(|ext| ext.to_str()) == Some("fac") {
                let name = path.file_stem().and_then(|s| s.to_str()).unwrap_or("Unknown").to_string();
                if let Ok(content) = fs::read_to_string(&path) {
                    if let Ok(mut preset) = parse_fac_file(&content) {
                        preset.name = name;
                        presets.push(preset);
                    }
                }
            }
        }
    }
    
    Ok(presets)
}

pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .plugin(tauri_plugin_process::init())
        .setup(|app| {
            app.manage(AppStore { lock: Mutex::new(()) });
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            scan_folder,
            get_tracks,
            get_recent_tracks,
            read_file_bytes,
            read_metadata,
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
            get_setting,
            get_equalizer_presets
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
