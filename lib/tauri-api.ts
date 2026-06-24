import { convertFileSrc, invoke } from "@tauri-apps/api/core";
import { open } from "@tauri-apps/plugin-dialog";
import type { MetadataOutput, Playlist, ScanResult, Track, TrackMetadataInput } from "@/types/music";

export async function pickMusicFolder() {
  const selected = await open({
    directory: true,
    multiple: false,
    title: "Select music folder"
  });

  return typeof selected === "string" ? selected : null;
}

export async function scanFolder(folder: string) {
  return invoke<ScanResult>("scan_folder", { folder });
}

export async function getTracks() {
  return invoke<Track[]>("get_tracks");
}

export async function getRecentTracks(limit = 50) {
  return invoke<Track[]>("get_recent_tracks", { limit });
}

export async function getPlaylists() {
  return invoke<Playlist[]>("get_playlists");
}

export async function createPlaylist(name: string) {
  return invoke<number>("create_playlist", { name });
}

export async function updatePlaylist(id: number, name: string) {
  return invoke<void>("update_playlist", { id, name });
}

export async function deletePlaylist(id: number) {
  return invoke<void>("delete_playlist", { id });
}

export async function getPlaylistTracks(playlistId: number) {
  return invoke<Track[]>("get_playlist_tracks", { playlistId });
}

export async function addToPlaylist(playlistId: number, trackId: number) {
  return invoke<void>("add_to_playlist", { playlistId, trackId });
}

export async function setPlaylistTracks(playlistId: number, trackIds: number[]) {
  return invoke<void>("set_playlist_tracks", { playlistId, trackIds });
}

export async function readFileBytes(path: string) {
  return invoke<number[]>("read_file_bytes", { path });
}

export async function readMetadata(path: string) {
  return invoke<MetadataOutput>("read_metadata", { path });
}

export async function updateTrackMetadata(input: TrackMetadataInput) {
  return invoke<void>("update_track_metadata", { input });
}

export async function recordPlayback(trackId: number, position: number) {
  return invoke<void>("record_playback", { input: { track_id: trackId, position } });
}

export async function getSettings() {
  return invoke<[string, string][]>("get_settings");
}

export async function setSetting(key: string, value: string) {
  return invoke<void>("set_setting", { input: { key, value } });
}

export function audioSrc(path: string) {
  return convertFileSrc(path);
}
