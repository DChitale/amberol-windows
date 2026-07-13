using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AmberolWpf.Models;

namespace AmberolWpf.Services
{
    public class Database
    {
        private readonly string _appDir;
        private readonly string _tracksPath;
        private readonly string _playlistsPath;
        private readonly string _settingsPath;
        private readonly string _historyPath;
        private readonly object _lock = new object();

        private static Database _instance;
        public static Database Instance => _instance ??= new Database();

        public string CoversDir { get; }

        private Database()
        {
            _appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AmberolNet");
            if (!Directory.Exists(_appDir))
            {
                Directory.CreateDirectory(_appDir);
            }

            CoversDir = Path.Combine(_appDir, "covers");
            if (!Directory.Exists(CoversDir))
            {
                Directory.CreateDirectory(CoversDir);
            }

            _tracksPath = Path.Combine(_appDir, "tracks.json");
            _playlistsPath = Path.Combine(_appDir, "playlists.json");
            _settingsPath = Path.Combine(_appDir, "settings.json");
            _historyPath = Path.Combine(_appDir, "history.json");

            InitializeDefaultPlaylists();
        }

        private void InitializeDefaultPlaylists()
        {
            lock (_lock)
            {
                if (!File.Exists(_playlistsPath))
                {
                    var defaultPlaylists = new List<Playlist>
                    {
                        new Playlist
                        {
                            Id = 1,
                            Name = "Library",
                            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            TrackIds = new List<int>()
                        }
                    };
                    SavePlaylists(defaultPlaylists);
                }
            }
        }

        public List<Track> GetTracks()
        {
            lock (_lock)
            {
                if (!File.Exists(_tracksPath)) return new List<Track>();
                try
                {
                    string json = File.ReadAllText(_tracksPath);
                    return JsonSerializer.Deserialize<List<Track>>(json) ?? new List<Track>();
                }
                catch
                {
                    return new List<Track>();
                }
            }
        }

        public void SaveTracks(List<Track> tracks)
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(tracks, options);
                    File.WriteAllText(_tracksPath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error saving tracks: " + ex.Message);
                }
            }
        }

        public List<Playlist> GetPlaylists()
        {
            lock (_lock)
            {
                if (!File.Exists(_playlistsPath)) return new List<Playlist>();
                try
                {
                    string json = File.ReadAllText(_playlistsPath);
                    return JsonSerializer.Deserialize<List<Playlist>>(json) ?? new List<Playlist>();
                }
                catch
                {
                    return new List<Playlist>();
                }
            }
        }

        public void SavePlaylists(List<Playlist> playlists)
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(playlists, options);
                    File.WriteAllText(_playlistsPath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error saving playlists: " + ex.Message);
                }
            }
        }

        public void ToggleTrackInPlaylist(string playlistName, int trackId)
        {
            lock (_lock)
            {
                var playlists = GetPlaylists();
                var p = playlists.Find(x => x.Name == playlistName);
                if (p == null)
                {
                    p = new Playlist
                    {
                        Id = playlistName == "Favorites" ? 2 : playlists.Count + 1,
                        Name = playlistName,
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        TrackIds = new List<int>()
                    };
                    playlists.Add(p);
                }

                if (p.TrackIds.Contains(trackId))
                {
                    p.TrackIds.Remove(trackId);
                }
                else
                {
                    p.TrackIds.Add(trackId);
                }
                p.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                SavePlaylists(playlists);
            }
        }

        public bool IsTrackInPlaylist(string playlistName, int trackId)
        {
            lock (_lock)
            {
                var playlists = GetPlaylists();
                var p = playlists.Find(x => x.Name == playlistName);
                if (p == null) return false;
                return p.TrackIds.Contains(trackId);
            }
        }

        public List<Setting> GetSettings()
        {
            lock (_lock)
            {
                if (!File.Exists(_settingsPath)) return new List<Setting>();
                try
                {
                    string json = File.ReadAllText(_settingsPath);
                    return JsonSerializer.Deserialize<List<Setting>>(json) ?? new List<Setting>();
                }
                catch
                {
                    return new List<Setting>();
                }
            }
        }

        public string GetSetting(string key, string defaultValue = "")
        {
            lock (_lock)
            {
                var settings = GetSettings();
                var match = settings.FirstOrDefault(s => s.Key == key);
                return match != null ? match.Value : defaultValue;
            }
        }

        public void SetSetting(string key, string value)
        {
            lock (_lock)
            {
                var settings = GetSettings();
                var match = settings.FirstOrDefault(s => s.Key == key);
                long current = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (match != null)
                {
                    match.Value = value;
                    match.UpdatedAt = current;
                }
                else
                {
                    settings.Add(new Setting
                    {
                        Key = key,
                        Value = value,
                        UpdatedAt = current
                    });
                }

                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(settings, options);
                    File.WriteAllText(_settingsPath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error saving settings: " + ex.Message);
                }
            }
        }

        public List<HistoryItem> GetHistory()
        {
            lock (_lock)
            {
                if (!File.Exists(_historyPath)) return new List<HistoryItem>();
                try
                {
                    string json = File.ReadAllText(_historyPath);
                    return JsonSerializer.Deserialize<List<HistoryItem>>(json) ?? new List<HistoryItem>();
                }
                catch
                {
                    return new List<HistoryItem>();
                }
            }
        }

        public void AddHistoryItem(int trackId, double position)
        {
            lock (_lock)
            {
                var history = GetHistory();
                history.Add(new HistoryItem
                {
                    TrackId = trackId,
                    PlayedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Position = position
                });

                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(history, options);
                    File.WriteAllText(_historyPath, json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error saving history: " + ex.Message);
                }
            }
        }
    }
}
