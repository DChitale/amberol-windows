import { readMetadata, updateTrackMetadata } from "@/lib/tauri-api";
import type { Track, TrackMetadataInput } from "@/types/music";

export async function enrichTrackMetadata(track: Track) {
  if (track.artist && track.album && track.cover_art && track.duration > 0) {
    return null;
  }

  const meta = await readMetadata(track.path);
  const input: TrackMetadataInput = {
    id: track.id,
    title: meta.title ?? null,
    artist: meta.artist ?? null,
    album: meta.album ?? null,
    cover_art: meta.cover_art ?? null,
    duration: meta.duration ?? null
  };

  await updateTrackMetadata(input);
  return input;
}

export async function enrichMissingMetadata(tracks: Track[], limit = 25) {
  const candidates = tracks
    .filter((track) => !track.artist || !track.album || !track.cover_art || track.duration <= 0)
    .slice(0, limit);

  for (const track of candidates) {
    await enrichTrackMetadata(track);
  }
}
