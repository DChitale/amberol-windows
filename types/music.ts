export type RepeatMode = "off" | "all" | "one";

export interface Track {
  id: number;
  path: string;
  file_name: string;
  title: string | null;
  artist: string | null;
  album: string | null;
  duration: number;
  cover_art: string | null;
  size_bytes: number;
  modified_at: number;
  added_at: number;
  last_played_at: number | null;
}

export interface Playlist {
  id: number;
  name: string;
  track_count: number;
  duration: number;
  created_at: number;
  updated_at: number;
}

export interface ScanResult {
  discovered: number;
  inserted_or_updated: number;
  tracks: Track[];
}

export interface TrackMetadataInput {
  id: number;
  title?: string | null;
  artist?: string | null;
  album?: string | null;
  duration?: number | null;
  cover_art?: string | null;
}

export interface MetadataOutput {
  title: string | null;
  artist: string | null;
  album: string | null;
  duration: number | null;
  cover_art: string | null;
}
