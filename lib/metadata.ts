import jsmediatags from "jsmediatags";
import { audioSrc, readFileBytes, updateTrackMetadata } from "@/lib/tauri-api";
import type { Track, TrackMetadataInput } from "@/types/music";

function pictureToDataUrl(picture?: { format: string; data: number[] }) {
  if (!picture?.data?.length) {
    return null;
  }

  let binary = "";
  for (const byte of picture.data) {
    binary += String.fromCharCode(byte);
  }

  return `data:${picture.format};base64,${btoa(binary)}`;
}

function readTagsFromBlob(blob: Blob): Promise<Partial<TrackMetadataInput>> {
  return new Promise((resolve) => {
    jsmediatags.read(blob, {
      onSuccess: ({ tags }) => {
        resolve({
          title: tags.title || null,
          artist: tags.artist || null,
          album: tags.album || null,
          cover_art: pictureToDataUrl(tags.picture)
        });
      },
      onError: () => resolve({})
    });
  });
}

function readDuration(path: string): Promise<number | null> {
  return new Promise((resolve) => {
    const audio = new Audio(audioSrc(path));
    audio.preload = "metadata";
    audio.onloadedmetadata = () => resolve(Number.isFinite(audio.duration) ? audio.duration : null);
    audio.onerror = () => resolve(null);
  });
}

export async function enrichTrackMetadata(track: Track) {
  if (track.artist && track.album && track.cover_art && track.duration > 0) {
    return null;
  }

  const bytes = await readFileBytes(track.path);
  const blob = new Blob([new Uint8Array(bytes)]);
  const [tags, duration] = await Promise.all([readTagsFromBlob(blob), readDuration(track.path)]);
  const input: TrackMetadataInput = {
    id: track.id,
    title: tags.title ?? null,
    artist: tags.artist ?? null,
    album: tags.album ?? null,
    cover_art: tags.cover_art ?? null,
    duration
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
