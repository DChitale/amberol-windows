"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { WindowControls } from "@/components/window-controls";
import { arrayMove } from "@dnd-kit/sortable";
import { NowPlaying } from "@/components/now-playing";
import { WelcomeScreen } from "@/components/welcome-screen";
import { PlaylistDialog } from "@/components/playlist-dialog";
import { PlaylistSidebar } from "@/components/playlist-sidebar";
import { UpdaterToast } from "@/components/updater-toast";
import { EqualizerDialog } from "@/components/equalizer-dialog";
import { useKeyboardShortcuts } from "@/hooks/use-keyboard-shortcuts";
import { enrichMissingMetadata } from "@/lib/metadata";
import {
  audioSrc,
  addToPlaylist,
  createPlaylist,
  deletePlaylist,
  getPlaylistTracks,
  getPlaylists,
  getRecentTracks,
  getSettings,
  getTracks,
  pickMusicFolder,
  recordPlayback,
  scanFolder,
  setPlaylistTracks,
  setSetting,
  updatePlaylist,
  readFileBytes
} from "@/lib/tauri-api";
import { usePlayerStore } from "@/store/player-store";
import type { Playlist, Track } from "@/types/music";

let globalAudioCtx: AudioContext | null = null;
let globalSourceNode: MediaElementAudioSourceNode | null = null;
let globalFilters: BiquadFilterNode[] = [];

export function MusicPlayer() {
  const audioRef = useRef<HTMLAudioElement>(null);
  const metadataPassRef = useRef(false);
  const isTransitioningRef = useRef(false);
  const [progress, setProgress] = useState(0);
  const [duration, setDuration] = useState(0);
  const [scanning, setScanning] = useState(false);
  const [playlistDialogOpen, setPlaylistDialogOpen] = useState(false);
  const [eqDialogOpen, setEqDialogOpen] = useState(false);
  const [eqEnabled, setEqEnabled] = useState(false);
  const [eqGains, setEqGains] = useState<number[]>(Array(10).fill(0));
  const [eqPreset, setEqPreset] = useState<string>("flat");
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [audioUrl, setAudioUrl] = useState("");
  const [lastScannedFolder, setLastScannedFolder] = useState<string>("");
  const [bgStyle, setBgStyle] = useState("");
  const [isMaximized, setIsMaximized] = useState(false);
  const isFadingRef = useRef(false);
  const tracks = usePlayerStore((state) => state.tracks);
  const queue = usePlayerStore((state) => state.queue);
  const playlists = usePlayerStore((state) => state.playlists);
  const activePlaylistId = usePlayerStore((state) => state.activePlaylistId);
  const currentTrack = usePlayerStore((state) => state.currentTrack);
  const isPlaying = usePlayerStore((state) => state.isPlaying);
  const volume = usePlayerStore((state) => state.volume);
  const speed = usePlayerStore((state) => state.speed);
  const shuffle = usePlayerStore((state) => state.shuffle);
  const repeat = usePlayerStore((state) => state.repeat);
  const search = usePlayerStore((state) => state.search);
  const setTracks = usePlayerStore((state) => state.setTracks);
  const setQueue = usePlayerStore((state) => state.setQueue);
  const setPlaylists = usePlayerStore((state) => state.setPlaylists);
  const setActivePlaylistId = usePlayerStore((state) => state.setActivePlaylistId);
  const setCurrentTrack = usePlayerStore((state) => state.setCurrentTrack);
  const setIsPlaying = usePlayerStore((state) => state.setIsPlaying);
  const setVolume = usePlayerStore((state) => state.setVolume);
  const setSpeed = usePlayerStore((state) => state.setSpeed);
  const setShuffle = usePlayerStore((state) => state.setShuffle);
  const toggleRepeat = usePlayerStore((state) => state.toggleRepeat);
  const setSearch = usePlayerStore((state) => state.setSearch);
  const nextTrack = usePlayerStore((state) => state.nextTrack);
  const previousTrack = usePlayerStore((state) => state.previousTrack);

  const visibleQueue = useMemo(() => (queue.length ? queue : tracks), [queue, tracks]);

  const refreshLibrary = useCallback(async () => {
    const [tracks, playlists, settings] = await Promise.all([
      getTracks(),
      getPlaylists(),
      getSettings()
    ]);

    const settingsMap = new Map(settings);
    const volume = Number(settingsMap.get("volume"));
    const speed = Number(settingsMap.get("speed"));
    const lastFolder = settingsMap.get("last_scanned_folder") || "";

    const eqEnabledSetting = settingsMap.get("eq_enabled");
    let eqPresetLoaded = settingsMap.get("eq_preset") || "Flat";
    const eqGainsSetting = settingsMap.get("eq_gains") || "0,0,0,0,0,0,0,0,0,0";

    // Normalize case for old settings
    if (eqPresetLoaded === "flat") eqPresetLoaded = "Flat";
    if (eqPresetLoaded === "bass") eqPresetLoaded = "Bass";
    if (eqPresetLoaded === "vocal") eqPresetLoaded = "Vocal";
    if (eqPresetLoaded === "pop") eqPresetLoaded = "Pop";
    if (eqPresetLoaded === "classical") eqPresetLoaded = "Classical";
    if (eqPresetLoaded === "rock") eqPresetLoaded = "Rock";

    const eqEnabledLoaded = eqEnabledSetting === "true";
    let eqGainsLoaded = eqGainsSetting.split(",").map(Number);
    if (eqGainsLoaded.length < 10) {
      eqGainsLoaded = [...eqGainsLoaded, ...Array(10 - eqGainsLoaded.length).fill(0)];
    }
    
    setTracks(tracks);
    setPlaylists(playlists);
    setLastScannedFolder(lastFolder);
    if (Number.isFinite(volume)) setVolume(volume);
    if (Number.isFinite(speed)) setSpeed(speed);

    setEqEnabled(eqEnabledLoaded);
    setEqPreset(eqPresetLoaded);
    if (eqGainsLoaded.length === 10 && eqGainsLoaded.every(Number.isFinite)) {
      setEqGains(eqGainsLoaded);
    }

    if (globalFilters.length === 10) {
      globalFilters.forEach((filter, idx) => {
        filter.gain.value = eqEnabledLoaded ? (eqGainsLoaded[idx] ?? 0) : 0;
      });
    }

    // Session restoration
    const lastPlaylistIdSetting = settingsMap.get("last_playlist_id");
    const lastTrackIdSetting = settingsMap.get("last_track_id");
    const lastPlaylistId = lastPlaylistIdSetting ? Number(lastPlaylistIdSetting) : null;
    const lastTrackId = lastTrackIdSetting ? Number(lastTrackIdSetting) : null;

    let targetPlaylistId = activePlaylistId;
    if (!targetPlaylistId) {
      targetPlaylistId = (lastPlaylistId && Number.isInteger(lastPlaylistId)) ? lastPlaylistId : (playlists[0]?.id ?? null);
    }

    if (targetPlaylistId) {
      setActivePlaylistId(targetPlaylistId);
      
      let playlistTracks: Track[] = [];
      const playlist = playlists.find((p) => p.id === targetPlaylistId);
      if (playlist) {
        if (playlist.name === "Library") {
          playlistTracks = tracks;
        } else {
          playlistTracks = await getPlaylistTracks(targetPlaylistId);
        }
      }
      setQueue(playlistTracks);

      if (lastTrackId && Number.isInteger(lastTrackId)) {
        const lastTrack = playlistTracks.find((t) => t.id === lastTrackId);
        if (lastTrack) {
          setCurrentTrack(lastTrack);
        } else if (playlistTracks.length > 0) {
          setCurrentTrack(playlistTracks[0]);
        }
      } else if (playlistTracks.length > 0) {
        setCurrentTrack(playlistTracks[0]);
      }
    }

    if (!metadataPassRef.current) {
      metadataPassRef.current = true;
      void enrichMissingMetadata(tracks).then(refreshLibrary).catch(() => undefined);
    }
  }, [
    activePlaylistId,
    setActivePlaylistId,
    setCurrentTrack,
    setPlaylists,
    setQueue,
    setSpeed,
    setTracks,
    setVolume
  ]);

  useEffect(() => {
    void refreshLibrary();
  }, [refreshLibrary]);

  const applyGainsToNodes = useCallback((gainsList: number[], isEnabled: boolean) => {
    if (globalFilters.length === 10) {
      globalFilters.forEach((filter, idx) => {
        filter.gain.value = isEnabled ? (gainsList[idx] ?? 0) : 0;
      });
    }
  }, []);

  useEffect(() => {
    applyGainsToNodes(eqGains, eqEnabled);
  }, [eqGains, eqEnabled, applyGainsToNodes]);

  const handleEqGainsChange = useCallback((nextGains: number[]) => {
    setEqGains(nextGains);
    void setSetting("eq_gains", nextGains.join(","));
  }, []);

  const handleEqEnabledChange = useCallback((nextEnabled: boolean) => {
    setEqEnabled(nextEnabled);
    void setSetting("eq_enabled", String(nextEnabled));
  }, []);

  const handleEqPresetChange = useCallback((nextPreset: string) => {
    setEqPreset(nextPreset);
    void setSetting("eq_preset", nextPreset);
  }, []);

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) {
      return;
    }

    if (!isFadingRef.current) {
      audio.volume = volume;
    }
    audio.playbackRate = speed;
  }, [speed, volume]);

  useEffect(() => {
    const audio = audioRef.current;
    const track = currentTrack;
    if (!audio) {
      return;
    }

    // Immediately halt playback of the previous track to prevent audio lag/bleeding
    isTransitioningRef.current = true;
    audio.pause();

    if (!track) {
      isTransitioningRef.current = false;
      return;
    }

    let active = true;

    async function loadAudio(el: HTMLAudioElement, t: Track) {
      try {
        // Reset volume to the current target in case a previous fade-out was active
        el.volume = volume;

        const bytes = await readFileBytes(t.path);
        if (!active) return;

        // Convert the bytes to a blob
        const blob = new Blob([new Uint8Array(bytes)], { type: "audio/mpeg" });
        const url = URL.createObjectURL(blob);

        if (!active) {
          URL.revokeObjectURL(url);
          return;
        }

        // Revoke old URL
        setAudioUrl((prev) => {
          if (prev) {
            try {
              URL.revokeObjectURL(prev);
            } catch (e) {}
          }
          return url;
        });

        el.src = url;
        el.load();
        
        // Read directly from Zustand state to avoid stale React closures while async loading
        if (usePlayerStore.getState().isPlaying) {
          void el.play()
            .then(() => {
              isTransitioningRef.current = false;
            })
            .catch(() => {
              isTransitioningRef.current = false;
              setIsPlaying(false);
            });
        } else {
          isTransitioningRef.current = false;
        }
      } catch (err) {
        console.error("Failed to load audio from database/file system bytes:", err);
        // Fallback to convertFileSrc
        if (active) {
          el.src = audioSrc(t.path);
          el.load();
          if (usePlayerStore.getState().isPlaying) {
            void el.play()
              .then(() => {
                isTransitioningRef.current = false;
              })
              .catch(() => {
                isTransitioningRef.current = false;
                setIsPlaying(false);
              });
          } else {
            isTransitioningRef.current = false;
          }
        }
      }
    }

    void loadAudio(audio, track);

    return () => {
      active = false;
    };
  }, [currentTrack]);

  // Clean up object URL on unmount
  useEffect(() => {
    return () => {
      if (audioUrl) {
        try {
          URL.revokeObjectURL(audioUrl);
        } catch (e) {}
      }
    };
  }, [audioUrl]);

  // Play/Pause effect
  useEffect(() => {
    const audio = audioRef.current;
    if (!audio || isTransitioningRef.current) {
      return;
    }

    if (isPlaying) {
      void audio.play().catch(() => setIsPlaying(false));
    } else {
      audio.pause();
    }
  }, [isPlaying, setIsPlaying]);

  useEffect(() => {
    void setSetting("volume", String(volume));
  }, [volume]);

  useEffect(() => {
    void setSetting("speed", String(speed));
  }, [speed]);

  useEffect(() => {
    if (activePlaylistId) {
      void setSetting("last_playlist_id", String(activePlaylistId));
    }
  }, [activePlaylistId]);

  useEffect(() => {
    if (currentTrack) {
      void setSetting("last_track_id", String(currentTrack.id));
    }
  }, [currentTrack]);

  useEffect(() => {
    const coverArt = currentTrack?.cover_art;
    if (!coverArt) {
      if (currentTrack) {
        // Generate a unique, beautiful ambient gradient based on track properties
        const seed = (currentTrack.id * 137 + Math.floor(currentTrack.duration)) || 42;
        const h1 = seed % 360;
        const h2 = (h1 + 120) % 360;
        const h3 = (h1 + 240) % 360;
        const h4 = (h1 + 60) % 360;
        
        const gradient = `
          radial-gradient(circle at 68% 20%, hsla(${h2}, 50%, 12%, 0.35), transparent 45%),
          linear-gradient(135deg, hsla(${h1}, 45%, 7%, 0.98), hsla(${h3}, 40%, 6%, 0.98) 56%, hsla(${h4}, 45%, 7%, 0.98))
        `;
        setBgStyle(gradient);
      } else {
        setBgStyle("");
      }
      return;
    }

    let active = true;
    extractPosterColors(coverArt).then((hslColors) => {
      if (!active) return;
      const [h1, h2, h3, h4] = hslColors;
      const gradient = `
        radial-gradient(ellipse at 80% 10%, hsla(${h2}, 65%, 18%, 0.45), transparent 50%),
        radial-gradient(ellipse at 10% 90%, hsla(${h4}, 60%, 12%, 0.35), transparent 50%),
        linear-gradient(135deg, hsla(${h1}, 55%, 6%, 0.99), hsla(${h3}, 50%, 8%, 0.99) 56%, hsla(${h2}, 52%, 6%, 0.99))
      `;
      setBgStyle(gradient);
    });

    return () => {
      active = false;
    };
  }, [currentTrack]);

  // Monitor window maximization status
  useEffect(() => {
    if (typeof window === "undefined") return;

    let unlisten: (() => void) | undefined;

    import("@tauri-apps/api/window").then(async (mod) => {
      const appWindow = mod.getCurrentWindow();
      
      const maximized = await appWindow.isMaximized();
      setIsMaximized(maximized);

      // Debounce window checks to handle OS maximize/restore transition settle times
      const unsub = await appWindow.onResized(async () => {
        setTimeout(async () => {
          const current = await appWindow.isMaximized();
          setIsMaximized(current);
        }, 150);
      });
      unlisten = unsub;
    }).catch((err) => console.error("Failed to initialize window listeners:", err));

    return () => {
      if (unlisten) {
        unlisten();
      }
    };
  }, []);

  // Synchronize sidebars state and set compact size on restore
  useEffect(() => {
    if (typeof window !== "undefined") {
      import("@tauri-apps/api/window").then(async (mod) => {
        const appWindow = mod.getCurrentWindow();
        if (isMaximized) {
          setSidebarOpen(true);
        } else {
          await appWindow.setSize(new mod.LogicalSize(350, 700));
          setSidebarOpen(false);
        }
      }).catch((err) => console.error("Failed to set window size:", err));
    }
  }, [isMaximized]);

  const playTrack = useCallback(
    (track: Track) => {
      setCurrentTrack(track);
      setIsPlaying(true);
      void setSetting("last_track_id", String(track.id));
    },
    [setCurrentTrack, setIsPlaying]
  );

  const togglePlayback = useCallback(() => {
    const isCurrentTrackInQueue = currentTrack && visibleQueue.some((t) => t.id === currentTrack.id);
    if ((!currentTrack || !isCurrentTrackInQueue) && visibleQueue[0]) {
      playTrack(visibleQueue[0]);
      return;
    }
    setIsPlaying(!isPlaying);
  }, [currentTrack, isPlaying, playTrack, setIsPlaying, visibleQueue]);

  const next = useCallback(() => {
    const track = nextTrack();
    if (track) {
      setIsPlaying(true);
    }
  }, [nextTrack, setIsPlaying]);

  const previous = useCallback(() => {
    const track = previousTrack();
    if (track) {
      setIsPlaying(true);
    }
  }, [previousTrack, setIsPlaying]);

  const seek = useCallback((seconds: number) => {
    const audio = audioRef.current;
    if (!audio) {
      return;
    }
    audio.currentTime = Math.min(Math.max(seconds, 0), audio.duration || 0);
    setProgress(audio.currentTime);
  }, []);

  useKeyboardShortcuts({
    togglePlayback,
    next,
    previous,
    seekBy: (seconds) => seek((audioRef.current?.currentTime ?? 0) + seconds)
  });

  async function scan() {
    const folder = await pickMusicFolder();
    if (!folder) {
      return;
    }

    setScanning(true);
    metadataPassRef.current = false;
    await scanFolder(folder);
    await setSetting("last_scanned_folder", folder);
    setLastScannedFolder(folder);
    await refreshLibrary();
    setScanning(false);
  }

  async function refreshFolder() {
    if (!lastScannedFolder) {
      return;
    }

    setScanning(true);
    metadataPassRef.current = false;
    await scanFolder(lastScannedFolder);
    await refreshLibrary();
    setScanning(false);
  }

  async function selectPlaylist(playlist: Playlist) {
    setActivePlaylistId(playlist.id);
    let playlistTracks: Track[] = [];
    if (playlist.name === "Library") {
      playlistTracks = tracks;
    } else {
      playlistTracks = await getPlaylistTracks(playlist.id);
    }

    setQueue(playlistTracks);

    if (playlistTracks.length > 0) {
      const isCurrentInPlaylist = currentTrack && playlistTracks.some((t) => t.id === currentTrack.id);
      if (!isCurrentInPlaylist) {
        setCurrentTrack(playlistTracks[0]);
      }
    } else {
      setCurrentTrack(null);
    }
  }

  async function playPlaylist(playlist: Playlist) {
    let playlistTracks: Track[] = [];
    if (playlist.name === "Library") {
      playlistTracks = tracks;
    } else {
      playlistTracks = await getPlaylistTracks(playlist.id);
    }

    setQueue(playlistTracks);
    setActivePlaylistId(playlist.id);

    if (playlistTracks.length > 0) {
      playTrack(playlistTracks[0]);
    }
  }

  async function createNewPlaylist(name: string) {
    await createPlaylist(name);
    setPlaylists(await getPlaylists());
  }

  async function renamePlaylist(playlist: Playlist, name: string) {
    await updatePlaylist(playlist.id, name);
    setPlaylists(await getPlaylists());
  }

  async function removePlaylist(playlist: Playlist) {
    await deletePlaylist(playlist.id);
    if (activePlaylistId === playlist.id) {
      setActivePlaylistId(playlists[0]?.id ?? null);
      setQueue(tracks);
    }
    setPlaylists(await getPlaylists());
  }

  async function addTrackToPlaylist(playlist: Playlist, track: Track) {
    await addToPlaylist(playlist.id, track.id);
    const updatedPlaylists = await getPlaylists();
    setPlaylists(updatedPlaylists);

    if (activePlaylistId === playlist.id) {
      const playlistTracks = await getPlaylistTracks(playlist.id);
      setQueue(playlistTracks);
    }
  }

  async function reorder(activeId: number, overId: number) {
    const oldIndex = queue.findIndex((track) => track.id === activeId);
    const newIndex = queue.findIndex((track) => track.id === overId);
    if (oldIndex < 0 || newIndex < 0) {
      return;
    }

    const reordered = arrayMove(queue, oldIndex, newIndex);
    setQueue(reordered);
    if (activePlaylistId) {
      await setPlaylistTracks(activePlaylistId, reordered.map((track) => track.id));
      setPlaylists(await getPlaylists());
    }
  }

  async function removeTrackFromQueue(track: Track) {
    const newQueue = queue.filter((t) => t.id !== track.id);
    setQueue(newQueue);

    if (activePlaylistId && playlists.find((p) => p.id === activePlaylistId)?.name !== "Library") {
      await setPlaylistTracks(activePlaylistId, newQueue.map((t) => t.id));
      setPlaylists(await getPlaylists());
    }

    if (currentTrack?.id === track.id) {
      if (newQueue.length > 0) {
        const oldIndex = queue.findIndex((t) => t.id === track.id);
        const nextTrack = newQueue[Math.min(oldIndex, newQueue.length - 1)];
        if (nextTrack) {
          playTrack(nextTrack);
        } else {
          setCurrentTrack(null);
          setIsPlaying(false);
        }
      } else {
        setCurrentTrack(null);
        setIsPlaying(false);
      }
    }
  }

  function onEnded() {
    if (currentTrack) {
      void recordPlayback(currentTrack.id, duration);
    }
    if (repeat === "one") {
      const audio = audioRef.current;
      if (audio) {
        audio.currentTime = 0;
        audio.volume = volume;
        void audio.play().catch(() => setIsPlaying(false));
        setProgress(0);
        return;
      }
    }
    next();
  }

  return (
    <div className="glass-window relative flex h-screen w-screen overflow-hidden rounded-xl border border-white/12 text-white">
      {/* Layer 1: Blurred Album Cover Art Image */}
      <div 
        className="absolute inset-0 -z-20 transition-all duration-1000 ease-in-out bg-cover bg-center scale-110 opacity-34 filter blur-[36px] saturate-[1.4] brightness-[0.35]"
        style={{
          backgroundImage: currentTrack?.cover_art ? `url("${currentTrack.cover_art}")` : "none",
          backgroundColor: "#0b0c10"
        }}
      />
      {/* Layer 2: Dynamic Ambient Gradient Overlay (adds rich color glow depth) */}
      <div 
        className="absolute inset-0 -z-10 transition-all duration-1000 ease-in-out opacity-45"
        style={{
          background: bgStyle || `radial-gradient(circle at 68% 20%, rgba(89, 160, 115, 0.2), transparent 45%)`,
        }}
      />
      {/* Layer 3: Frosted Glass overlay */}
      <div className="absolute inset-0 -z-10 bg-black/24 backdrop-blur-[24px]" />

        <audio
          ref={audioRef}
          onPlay={() => {
            isTransitioningRef.current = false;
            setIsPlaying(true);

            try {
              if (typeof window !== "undefined" && !globalAudioCtx && audioRef.current) {
                const AudioCtx = window.AudioContext || (window as any).webkitAudioContext;
                const ctx = new AudioCtx();
                globalAudioCtx = ctx;

                const source = ctx.createMediaElementSource(audioRef.current);
                globalSourceNode = source;

                const bands = [62.5, 110, 250, 370, 650, 1200, 2130, 4550, 6850, 16000];
                const types: BiquadFilterType[] = [
                  "lowshelf",
                  "peaking",
                  "peaking",
                  "peaking",
                  "peaking",
                  "peaking",
                  "peaking",
                  "peaking",
                  "peaking",
                  "highshelf"
                ];

                const filters = bands.map((freq, idx) => {
                  const filter = ctx.createBiquadFilter();
                  filter.type = types[idx] ?? "peaking";
                  filter.frequency.value = freq;
                  filter.Q.value = 1.0;
                  filter.gain.value = eqEnabled ? (eqGains[idx] ?? 0) : 0;
                  return filter;
                });

                globalFilters = filters;

                let lastNode: AudioNode = source;
                filters.forEach((filter) => {
                  lastNode.connect(filter);
                  lastNode = filter;
                });
                lastNode.connect(ctx.destination);
              }

              if (globalAudioCtx && globalAudioCtx.state === "suspended") {
                void globalAudioCtx.resume();
              }
            } catch (err) {
              console.error("Failed to initialize Web Audio API Equalizer graph:", err);
            }
          }}
          onPause={() => {
            if (isTransitioningRef.current) return;
            setIsPlaying(false);
          }}
          onTimeUpdate={(event) => {
            const el = event.currentTarget;
            setProgress(el.currentTime);
            if (isFadingRef.current) return;
            
            // Smooth fade out in the last 1.8 seconds of the song
            const fadeDuration = 1.8;
            const timeLeft = el.duration - el.currentTime;
            if (el.duration > 0 && timeLeft <= fadeDuration && timeLeft > 0 && !el.paused) {
              const ratio = Math.max(0, timeLeft / fadeDuration);
              el.volume = volume * ratio;
            } else if (el.volume !== volume) {
              el.volume = volume;
            }
          }}
          onLoadedMetadata={(event) => setDuration(event.currentTarget.duration || currentTrack?.duration || 0)}
          onEnded={onEnded}
        />
        {sidebarOpen && (
          <PlaylistSidebar
            playlists={playlists}
            activePlaylistId={activePlaylistId}
            lastScannedFolder={lastScannedFolder}
            scanning={scanning}
            onSelectPlaylist={selectPlaylist}
            onCreatePlaylist={() => setPlaylistDialogOpen(true)}
            onScanFolder={scan}
            onRefreshFolder={refreshFolder}
            onRenamePlaylist={renamePlaylist}
            onDeletePlaylist={removePlaylist}
            onPlayPlaylist={playPlaylist}
            onClose={() => setSidebarOpen(false)}
            showCloseButton={!isMaximized}
            queueTracks={visibleQueue}
            currentTrack={currentTrack}
            onPlayTrack={playTrack}
            onReorderQueue={reorder}
            onAddToPlaylist={addTrackToPlaylist}
            onRemoveFromQueue={removeTrackFromQueue}
            className={isMaximized 
              ? "border-r border-white/10 bg-black/14 w-[340px]" 
              : "absolute left-0 top-0 bottom-0 z-30 w-[340px] border-r border-white/10 bg-black/20 backdrop-blur-[20px] shadow-2xl transition-all duration-300"
            }
          />
        )}
        {tracks.length === 0 ? (
          <WelcomeScreen onScanFolder={scan} scanning={scanning} />
        ) : (
          <NowPlaying
            track={currentTrack}
            audioRef={audioRef}
            isPlaying={isPlaying}
            volume={volume}
            speed={speed}
            progress={progress}
            duration={duration || currentTrack?.duration || 0}
            shuffle={shuffle}
            repeat={repeat}
            onToggle={togglePlayback}
            onNext={next}
            onPrevious={previous}
            onSeek={seek}
            onVolume={setVolume}
            onSpeed={setSpeed}
            onShuffle={() => setShuffle(!shuffle)}
            onRepeat={toggleRepeat}
            sidebarOpen={sidebarOpen}
            onToggleSidebar={() => setSidebarOpen(!sidebarOpen)}
            eqEnabled={eqEnabled}
            onToggleEqualizer={() => setEqDialogOpen(!eqDialogOpen)}
          />
        )}
        <div className="no-drag absolute right-0 top-0 z-50">
          <WindowControls />
        </div>
        <PlaylistDialog open={playlistDialogOpen} onOpenChange={setPlaylistDialogOpen} onSubmit={createNewPlaylist} />
        <UpdaterToast />
        <EqualizerDialog 
          open={eqDialogOpen} 
          onOpenChange={setEqDialogOpen} 
          enabled={eqEnabled}
          onEnabledChange={handleEqEnabledChange}
          gains={eqGains}
          onGainsChange={handleEqGainsChange}
          activePreset={eqPreset}
          onPresetChange={handleEqPresetChange}
        />
      </div>
  );
}

/** Returns 4 distinct hue values (0-360) extracted from the album art. */
function extractPosterColors(src: string): Promise<number[]> {
  const fallback = [12, 160, 210, 280];
  return new Promise((resolve) => {
    const img = new Image();
    if (!src.startsWith("data:")) img.crossOrigin = "Anonymous";
    img.onload = () => {
      try {
        const SIZE = 16;
        const canvas = document.createElement("canvas");
        canvas.width = SIZE;
        canvas.height = SIZE;
        const ctx = canvas.getContext("2d");
        if (!ctx) { resolve(fallback); return; }
        ctx.drawImage(img, 0, 0, SIZE, SIZE);
        const data = ctx.getImageData(0, 0, SIZE, SIZE).data;

        // Collect all pixel hues weighted by saturation & brightness
        const hues: number[] = [];
        for (let i = 0; i < data.length; i += 4) {
          const r = data[i] / 255;
          const g = data[i + 1] / 255;
          const b = data[i + 2] / 255;
          const max = Math.max(r, g, b);
          const min = Math.min(r, g, b);
          const delta = max - min;
          if (delta < 0.15 || max < 0.1) continue; // skip grays and near-black
          const s = delta / max; // saturation (HSV)
          if (s < 0.3) continue; // skip desaturated
          let h = 0;
          if (max === r) h = ((g - b) / delta) % 6;
          else if (max === g) h = (b - r) / delta + 2;
          else h = (r - g) / delta + 4;
          h = Math.round(h * 60);
          if (h < 0) h += 360;
          // push hue multiple times proportional to saturation for weighting
          const weight = Math.round(s * max * 5);
          for (let w = 0; w < weight; w++) hues.push(h);
        }

        if (hues.length < 4) { resolve(fallback); return; }

        // Pick 4 hues spread across hue space
        const buckets = [0, 90, 180, 270]; // 4 quadrants
        const picked = buckets.map((start) => {
          const end = start + 90;
          const bucket = hues.filter((h) => h >= start && h < end);
          if (bucket.length === 0) {
            // use dominant if nothing in this quadrant
            return hues[Math.floor(hues.length / 2)];
          }
          // median hue in bucket
          bucket.sort((a, b) => a - b);
          return bucket[Math.floor(bucket.length / 2)];
        });

        resolve(picked);
      } catch {
        resolve(fallback);
      }
    };
    img.onerror = () => resolve(fallback);
    img.src = src;
  });
}
