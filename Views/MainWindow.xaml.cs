using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AmberolWpf.Models;
using AmberolWpf.Services;
using AmberolWpf.Helpers;
using Track = AmberolWpf.Models.Track;

namespace AmberolWpf.Views
{
    public partial class MainWindow : Window
    {
        private List<Track> _queue = new List<Track>();
        private List<Track> _browsedQueue = new List<Track>();
        private int _currentTrackIndex = -1;

        // Procedures for waveform heights caching
        private List<double> _currentWaveformHeights = new List<double>();

        // Lyrics tracking
        private List<LrcLine> _lyricsLines = new List<LrcLine>();
        private int _lastActiveLyricIndex = -1;
        private List<TextBlock> _lyricTextBlocks = new List<TextBlock>();

        // UI state
        private bool _isLyricsPaneOpen = false;
        private bool _isQueuePaneOpen = false;
        private bool _isShuffle = false;
        private bool _isRepeat = false;

        // Equalizer gain values (10 bands)
        private Slider[] _eqSliders = new Slider[10];
        private bool _updatingSlidersFromPreset = false;

        // Cached brushes to avoid GC allocations in composition loop
        private Brush _activeBrush;
        private Brush _inactiveBrush;

        private SmtcService? _smtcService;

        public MainWindow()
        {
            InitializeComponent();
            
            // Set Window Icon
            try
            {
                this.Icon = new BitmapImage(new Uri("pack://application:,,,/icon.png"));
            }
            catch { }
            
            // Build Equalizer Sliders dynamically
            BuildEqualizerSliders();

            // Setup audio engine event handlers
            AudioEngine.Instance.PositionChanged += AudioEngine_PositionChanged;
            AudioEngine.Instance.PlaybackFinished += AudioEngine_PlaybackFinished;

            // Load saved settings & library session
            RestoreSession();
            InitializePlaylists();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // Center the player column on the screen
            try
            {
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                
                this.Left = (screenWidth - 380) / 2 - 380;
                this.Top = (screenHeight - 520) / 2;
            }
            catch { }

            // Setup CompositionTarget.Rendering for smooth 60fps waveform render updates
            CompositionTarget.Rendering += OnCompositionTargetRendering;

            // Initialize Windows SMTC service
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                _smtcService = new SmtcService();
                _smtcService.Initialize(hwnd);
                _smtcService.PlayPauseRequested += () => Dispatcher.Invoke(() => PlayPauseButton_Click(null, null));
                _smtcService.NextRequested += () => Dispatcher.Invoke(() => NextButton_Click(null, null));
                _smtcService.PreviousRequested += () => Dispatcher.Invoke(() => PreviousButton_Click(null, null));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize SMTC service: {ex.Message}");
            }
        }

        #region Session Restoration & Initialization

        private void RestoreSession()
        {
            var db = Database.Instance;

            // Restore volume
            if (double.TryParse(db.GetSetting("volume", "1.0"), out double vol))
            {
                AudioEngine.Instance.Volume = (float)vol;
            }

            // Restore speed
            if (double.TryParse(db.GetSetting("speed", "1.00"), out double speed))
            {
                AudioEngine.Instance.Speed = speed;
                SpeedText.Text = $"Speed {speed:F2}x";
            }

            // Restore Equalizer settings
            bool eqEnabled = db.GetSetting("eq_enabled", "false") == "true";
            EqEnableToggle.IsChecked = eqEnabled;
            EqEnableToggle.Content = eqEnabled ? "On" : "Off";
            AudioEngine.Instance.EqEnabled = eqEnabled;

            string presetName = db.GetSetting("eq_preset", "Flat");
            string gainsStr = db.GetSetting("eq_gains", "0,0,0,0,0,0,0,0,0,0");
            float[] gains = gainsStr.Split(',').Select(s => float.TryParse(s, out float g) ? g : 0f).ToArray();
            
            if (gains.Length < 10)
            {
                Array.Resize(ref gains, 10);
            }
            AudioEngine.Instance.EqGains = gains;

            // Set preset selection in combo
            foreach (ComboBoxItem item in EqPresetComboBox.Items)
            {
                if (item.Content.ToString() == presetName)
                {
                    EqPresetComboBox.SelectedItem = item;
                    break;
                }
            }

            // Set Equalizer sliders
            _updatingSlidersFromPreset = true;
            for (int i = 0; i < 10; i++)
            {
                _eqSliders[i].Value = gains[i];
            }
            _updatingSlidersFromPreset = false;

            // Restore last scanned directory
            string lastScannedFolder = db.GetSetting("last_scanned_folder", "");
            if (!string.IsNullOrEmpty(lastScannedFolder) && Directory.Exists(lastScannedFolder))
            {
                LoadTracksFromCache();
                
                // Restore last played track
                if (int.TryParse(db.GetSetting("last_track_id", "0"), out int lastTrackId) && lastTrackId > 0)
                {
                    int index = _queue.FindIndex(t => t.Id == lastTrackId);
                    if (index >= 0)
                    {
                        SelectTrack(index, playImmediately: false);
                    }
                }
            }
            else
            {
                ShowWelcomeScreen();
            }
        }

        private void LoadTracksFromCache()
        {
            var tracks = Database.Instance.GetTracks();
            if (tracks.Count > 0)
            {
                bool dirty = false;
                foreach (var track in tracks)
                {
                    if (!string.IsNullOrEmpty(track.CoverArt) && track.CoverArt.StartsWith("data:image"))
                    {
                        try
                        {
                            string base64 = track.CoverArt.Substring(track.CoverArt.IndexOf(",") + 1);
                            byte[] bytes = Convert.FromBase64String(base64);
                            string path = SaveThumbnail(bytes, track.Path);
                            track.CoverArt = path;
                            dirty = true;
                        }
                        catch { }
                    }
                }
                if (dirty)
                {
                    Database.Instance.SaveTracks(tracks);
                }

                _queue = tracks;
                _currentTrackIndex = 0;

                WelcomePanel.Visibility = Visibility.Collapsed;
                AlbumArtBorder.Visibility = Visibility.Visible;
                TrackInfoPanel.Visibility = Visibility.Visible;
                WaveformCanvas.Visibility = Visibility.Visible;
                TimeLabelsPanel.Visibility = Visibility.Visible;
                PlaybackControlsPanel.Visibility = Visibility.Visible;
                BottomToolbarPanel.Visibility = Visibility.Visible;

                // Force full garbage collection after database restoration/migration
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                SelectTrack(0, playImmediately: false);
            }
        }

        #endregion

        #region Title Bar & Window Management

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            AudioEngine.Instance.Stop();
            Application.Current.Shutdown();
        }

        #endregion

        #region Equalizer UI Builders

        private void BuildEqualizerSliders()
        {
            string[] bands = { "62", "110", "250", "370", "650", "1.2k", "2.1k", "4.5k", "6.8k", "16k" };
            SlidersGrid.Children.Clear();

            for (int i = 0; i < 10; i++)
            {
                var colDef = SlidersGrid.ColumnDefinitions[i];
                
                var stack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Vertical slider
                var slider = new Slider
                {
                    Orientation = Orientation.Vertical,
                    Height = 120, // More compact
                    Minimum = -15,
                    Maximum = 15,
                    Value = 0,
                    TickFrequency = 1,
                    IsSnapToTickEnabled = false,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Tag = i // Store band index
                };
                if (TryFindResource("EqSliderStyle") is Style style)
                {
                    slider.Style = style;
                }
                
                slider.ValueChanged += Slider_ValueChanged;
                _eqSliders[i] = slider;

                // Frequency label
                var label = new TextBlock
                {
                    Text = bands[i],
                    Foreground = new SolidColorBrush(Color.FromArgb(178, 255, 255, 255)),
                    FontSize = 9.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0),
                    FontWeight = FontWeights.SemiBold
                };

                stack.Children.Add(slider);
                stack.Children.Add(label);

                Grid.SetColumn(stack, i);
                SlidersGrid.Children.Add(stack);
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingSlidersFromPreset) return;

            var slider = (Slider)sender;
            int bandIdx = (int)slider.Tag;
            float val = (float)slider.Value;

            // Apply value to audio engine by cloning the array
            float[] gains = AudioEngine.Instance.EqGains.ToArray();
            gains[bandIdx] = val;
            AudioEngine.Instance.EqGains = gains;

            // Update database settings
            Database.Instance.SetSetting("eq_gains", string.Join(",", gains.Select(g => g.ToString("F1"))));

            // Switch preset selection to "Custom"
            _updatingSlidersFromPreset = true;
            EqPresetComboBox.SelectedIndex = 6; // Custom is the last item (Index 6)
            Database.Instance.SetSetting("eq_preset", "Custom");
            _updatingSlidersFromPreset = false;
        }

        private void EqEnableToggle_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = EqEnableToggle.IsChecked == true;
            AudioEngine.Instance.EqEnabled = isChecked;
            Database.Instance.SetSetting("eq_enabled", isChecked ? "true" : "false");
            EqEnableToggle.Content = isChecked ? "On" : "Off";
        }

        private void EqPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSlidersFromPreset) return;

            var combo = (ComboBox)sender;
            var selectedItem = (ComboBoxItem)combo.SelectedItem;
            if (selectedItem == null) return;

            string preset = selectedItem.Content.ToString();
            if (preset == "Custom") return;

            float[] gains = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            switch (preset)
            {
                case "Flat":
                    gains = new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                    break;
                case "Bass":
                    gains = new float[] { 6, 5, 4, 2, 0, 0, 0, 0, 0, 0 };
                    break;
                case "Vocal":
                    gains = new float[] { -2, -1, 1, 3, 4, 4, 3, 1, -1, -2 };
                    break;
                case "Pop":
                    gains = new float[] { -1, 2, 5, 6, 4, -1, -2, -2, -1, -1 };
                    break;
                case "Classical":
                    gains = new float[] { 5, 3, 2, 2, 0, 0, 0, 2, 4, 5 };
                    break;
                case "Rock":
                    gains = new float[] { 4, 3, -1, -2, -1, 2, 4, 5, 5, 5 };
                    break;
            }

            AudioEngine.Instance.EqGains = gains;
            Database.Instance.SetSetting("eq_preset", preset);
            Database.Instance.SetSetting("eq_gains", string.Join(",", gains.Select(g => g.ToString("F1"))));

            // Update UI sliders
            _updatingSlidersFromPreset = true;
            for (int i = 0; i < 10; i++)
            {
                _eqSliders[i].Value = gains[i];
            }
            _updatingSlidersFromPreset = false;
        }

        #endregion

        #region Folder Scanning (TagLibSharp Integration)

        private void ScanFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Music Directory"
            };

            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FolderName;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    ScanAndCacheFolder(path);
                }
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] filesAndFolders = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (filesAndFolders != null && filesAndFolders.Length > 0)
                {
                    ProcessDroppedItems(filesAndFolders);
                }
            }
        }

        private void ProcessDroppedItems(string[] paths)
        {
            var newTracks = new List<Track>();
            var extensions = new[] { ".mp3", ".wav", ".flac", ".ogg", ".opus", ".m4a", ".aac", ".wma" };
            var filesToProcess = new List<string>();

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        var dirFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                                                .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                                                .ToList();
                        filesToProcess.AddRange(dirFiles);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error scanning folder {path}: {ex.Message}");
                    }
                }
                else if (File.Exists(path))
                {
                    if (extensions.Contains(Path.GetExtension(path).ToLower()))
                    {
                        filesToProcess.Add(path);
                    }
                }
            }

            if (filesToProcess.Count == 0)
            {
                ShowCustomMessageBox("No supported audio files found.", "No Media Found", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int nextId = (_queue.Count > 0) ? (_queue.Max(t => t.Id) + 1) : 1;
            bool wasQueueEmpty = _queue.Count == 0;

            foreach (var file in filesToProcess)
            {
                try
                {
                    var tagFile = TagLib.File.Create(file);
                    
                    string title = tagFile.Tag.Title;
                    if (string.IsNullOrEmpty(title))
                    {
                        title = Path.GetFileNameWithoutExtension(file);
                    }

                    string artist = tagFile.Tag.FirstPerformer;
                    if (string.IsNullOrEmpty(artist)) artist = "Unknown Artist";

                    string album = tagFile.Tag.Album;
                    if (string.IsNullOrEmpty(album)) album = "Unknown Album";

                    double duration = tagFile.Properties.Duration.TotalSeconds;

                    // Cache cover art if exists
                    string cachedCoverPath = null;
                    if (tagFile.Tag.Pictures != null && tagFile.Tag.Pictures.Length > 0)
                    {
                        var picture = tagFile.Tag.Pictures[0];
                        byte[] imgData = picture.Data.Data;
                        cachedCoverPath = SaveThumbnail(imgData, file);
                    }

                    var fileInfo = new FileInfo(file);
                    long size = fileInfo.Length;
                    long modified = new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds();
                    long added = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    newTracks.Add(new Track
                    {
                        Id = nextId++,
                        Path = file,
                        FileName = Path.GetFileName(file),
                        Title = title,
                        Artist = artist,
                        Album = album,
                        Duration = duration,
                        CoverArt = cachedCoverPath,
                        SizeBytes = size,
                        ModifiedAt = modified,
                        AddedAt = added
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error scanning file {file}: {ex.Message}");
                }
            }

            if (newTracks.Count > 0)
            {
                // Add to current queue
                _queue.AddRange(newTracks);
                
                // Cache tracks
                Database.Instance.SaveTracks(_queue);

                InitializePlaylists();

                // Clean up massive allocation garbage
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Switch panels if it was empty
                if (wasQueueEmpty)
                {
                    WelcomePanel.Visibility = Visibility.Collapsed;
                    AlbumArtBorder.Visibility = Visibility.Visible;
                    TrackInfoPanel.Visibility = Visibility.Visible;
                    WaveformCanvas.Visibility = Visibility.Visible;
                    TimeLabelsPanel.Visibility = Visibility.Visible;
                    PlaybackControlsPanel.Visibility = Visibility.Visible;
                    BottomToolbarPanel.Visibility = Visibility.Visible;

                    // Play first song
                    SelectTrack(0, playImmediately: true);
                }
                else
                {
                    var selected = PlaylistsListBox.SelectedItem as Playlist;
                    if (selected != null && selected.Id == 9999) // All Songs
                    {
                        var allTracks = Database.Instance.GetTracks();
                        _browsedQueue = allTracks;
                        QueueListBox.ItemsSource = null;
                        QueueListBox.ItemsSource = _browsedQueue;
                        PlayQueueHeaderTitle.Text = $"Play Queue ({_browsedQueue.Count})";
                    }
                }
            }
        }

        private string SaveThumbnail(byte[] imgData, string trackPath)
        {
            try
            {
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(trackPath))).Substring(0, 16);
                string targetPath = Path.Combine(Database.Instance.CoversDir, $"{hash}.jpg");

                if (File.Exists(targetPath))
                {
                    return targetPath;
                }

                using (var ms = new MemoryStream(imgData))
                {
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count == 0) return null;
                    var frame = decoder.Frames[0];

                    double scaleX = 48.0 / frame.PixelWidth;
                    double scaleY = 48.0 / frame.PixelHeight;
                    var scale = new ScaleTransform(scaleX, scaleY);
                    var resized = new TransformedBitmap(frame, scale);

                    var encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(resized));

                    using (var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        encoder.Save(fs);
                    }
                }

                return targetPath;
            }
            catch
            {
                return null;
            }
        }

        private void ScanAndCacheFolder(string folderPath)
        {
            var tracks = new List<Track>();
            var extensions = new[] { ".mp3", ".wav", ".flac", ".ogg", ".opus", ".m4a", ".aac", ".wma" };
            
            try
            {
                var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                     .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                                     .ToList();

                int id = 1;
                foreach (string file in files)
                {
                    try
                    {
                        var tagFile = TagLib.File.Create(file);
                        
                        string title = tagFile.Tag.Title;
                        if (string.IsNullOrEmpty(title))
                        {
                            title = Path.GetFileNameWithoutExtension(file);
                        }

                        string artist = tagFile.Tag.FirstPerformer;
                        if (string.IsNullOrEmpty(artist)) artist = "Unknown Artist";

                        string album = tagFile.Tag.Album;
                        if (string.IsNullOrEmpty(album)) album = "Unknown Album";

                        double duration = tagFile.Properties.Duration.TotalSeconds;

                        // Cache cover art if exists
                        string cachedCoverPath = null;
                        if (tagFile.Tag.Pictures != null && tagFile.Tag.Pictures.Length > 0)
                        {
                            var picture = tagFile.Tag.Pictures[0];
                            byte[] imgData = picture.Data.Data;
                            cachedCoverPath = SaveThumbnail(imgData, file);
                        }

                        var fileInfo = new FileInfo(file);
                        long size = fileInfo.Length;
                        long modified = new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds();
                        long added = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                        tracks.Add(new Track
                        {
                            Id = id++,
                            Path = file,
                            FileName = Path.GetFileName(file),
                            Title = title,
                            Artist = artist,
                            Album = album,
                            Duration = duration,
                            CoverArt = cachedCoverPath,
                            SizeBytes = size,
                            ModifiedAt = modified,
                            AddedAt = added
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error scanning file {file}: {ex.Message}");
                    }
                }

                if (tracks.Count > 0)
                {
                    // Cache tracks
                    Database.Instance.SaveTracks(tracks);
                    Database.Instance.SetSetting("last_scanned_folder", folderPath);

                    _queue = tracks;
                    _currentTrackIndex = 0;

                    InitializePlaylists();

                    // Clean up massive allocation garbage from scanning files
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    // Switch panels
                    WelcomePanel.Visibility = Visibility.Collapsed;
                    AlbumArtBorder.Visibility = Visibility.Visible;
                    TrackInfoPanel.Visibility = Visibility.Visible;
                    WaveformCanvas.Visibility = Visibility.Visible;
                    TimeLabelsPanel.Visibility = Visibility.Visible;
                    PlaybackControlsPanel.Visibility = Visibility.Visible;
                    BottomToolbarPanel.Visibility = Visibility.Visible;

                    // Play first song
                    SelectTrack(0, playImmediately: true);
                }
                else
                {
                    ShowCustomMessageBox("No audio files (.mp3, .wav, .flac, .ogg, .opus, .m4a, .aac, .wma) found in this folder.", "No Media Found", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox("Failed to scan directory: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Media Player Operations

        private void SelectTrack(int index, bool playImmediately)
        {
            if (_queue == null || _queue.Count == 0 || index < 0 || index >= _queue.Count) return;

            // Aggressively collect memory from the previous track
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _currentTrackIndex = index;
            var track = _queue[_currentTrackIndex];

            // Update details
            TrackTitleText.Text = track.Title;
            TrackArtistText.Text = track.Artist;
            RemainingTimeText.Text = FormatTime(track.Duration);
            CurrentTimeText.Text = "0:00";

            // Update Quality badge
            UpdateQualityBadge(track);

            // Save last played session
            Database.Instance.SetSetting("last_track_id", track.Id.ToString());

            // Ensure player controls are visible and welcome screen is collapsed
            WelcomePanel.Visibility = Visibility.Collapsed;
            AlbumArtBorder.Visibility = Visibility.Visible;
            TrackInfoPanel.Visibility = Visibility.Visible;
            WaveformCanvas.Visibility = Visibility.Visible;
            TimeLabelsPanel.Visibility = Visibility.Visible;
            PlaybackControlsPanel.Visibility = Visibility.Visible;

            // Update Favorite star visual state
            bool isFav = Database.Instance.IsTrackInPlaylist("Favorites", track.Id);
            FavoriteStarPath.Fill = isFav ? Brushes.White : Brushes.Transparent;

            // Build procedural waveform heights (matching 76 bars)
            GenerateProceduralWaveform(track.Id, track.Duration);

            // Update Backdrop colors
            UpdateBackdrop(track);

            // Load Lyrics
            LoadLyrics(track);

            // Update Windows SMTC metadata and status
            if (_smtcService != null)
            {
                _smtcService.UpdateMetadata(track.Title, track.Artist, track.Album, track.CoverArt);
                _smtcService.SetPlaybackStatus(playImmediately);
            }

            if (playImmediately)
            {
                try
                {
                    AudioEngine.Instance.PlayTrack(track.Path);
                    PlayPauseIcon.Data = (Geometry)Application.Current.Resources["IconPause"];
                    PlayPauseIcon.Margin = new Thickness(0, 0, 0, 0); // Reset offset margin
                }
                catch (Exception ex)
                {
                    ShowCustomMessageBox(ex.Message, "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                AudioEngine.Instance.Stop();
                PlayPauseIcon.Data = (Geometry)Application.Current.Resources["IconPlay"];
                PlayPauseIcon.Margin = new Thickness(4, 0, 0, 0); // Play button visual offset margin
            }
        }

        private void UpdateQualityBadge(Track track)
        {
            string ext = Path.GetExtension(track.Path).ToUpper().Replace(".", "");
            if (track.SizeBytes > 0 && track.Duration > 0)
            {
                int kbps = (int)Math.Round((track.SizeBytes * 8) / (track.Duration * 1000));
                QualityBadgeText.Text = $"{ext} • {kbps} kbps";
            }
            else
            {
                QualityBadgeText.Text = ext;
            }
        }

        private BitmapImage LoadFullCoverArt(string trackPath)
        {
            try
            {
                var tagFile = TagLib.File.Create(trackPath);
                if (tagFile.Tag.Pictures != null && tagFile.Tag.Pictures.Length > 0)
                {
                    var picture = tagFile.Tag.Pictures[0];
                    byte[] imgData = picture.Data.Data;
                    using (var ms = new MemoryStream(imgData))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad; // Load immediately so the stream can be disposed
                        bitmap.DecodePixelWidth = 200; // Limit image decode size! Huge RAM saver!
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze(); // Freeze to allow thread-safe usage and free stream resources!
                        return bitmap;
                    }
                }
            }
            catch
            {
                // Fallback
            }
            return null;
        }

        private void UpdateBackdrop(Track track)
        {
            BitmapImage bitmap = LoadFullCoverArt(track.Path);

            if (bitmap != null)
            {
                AlbumArtImage.Source = bitmap;
                BlurredBgImage.Source = bitmap;

                // Extract colors
                var colors = ColorExtractor.ExtractColors(bitmap);
                if (colors.Count >= 4)
                {
                    LinearGradientRect.Fill = new LinearGradientBrush(colors[0], colors[2], 45.0);
                    RadialGradientRect1.Fill = new RadialGradientBrush(colors[1], Color.FromArgb(0, 0, 0, 0))
                    {
                        Center = new Point(0.8, 0.1),
                        GradientOrigin = new Point(0.8, 0.1),
                        RadiusX = 0.6,
                        RadiusY = 0.6
                    };
                    RadialGradientRect2.Fill = new RadialGradientBrush(colors[3], Color.FromArgb(0, 0, 0, 0))
                    {
                        Center = new Point(0.1, 0.9),
                        GradientOrigin = new Point(0.1, 0.9),
                        RadiusX = 0.6,
                        RadiusY = 0.6
                    };
                }
            }
            else
            {
                AlbumArtImage.Source = null;
                BlurredBgImage.Source = null;

                // Fallback dark gradient
                LinearGradientRect.Fill = new LinearGradientBrush(Color.FromRgb(12, 18, 30), Color.FromRgb(24, 38, 56), 45.0);
                RadialGradientRect1.Fill = new RadialGradientBrush(Color.FromRgb(30, 60, 45), Color.FromArgb(0, 0, 0, 0))
                {
                    Center = new Point(0.8, 0.1),
                    RadiusX = 0.6,
                    RadiusY = 0.6
                };
                RadialGradientRect2.Fill = new RadialGradientBrush(Color.FromRgb(40, 25, 55), Color.FromArgb(0, 0, 0, 0))
                {
                    Center = new Point(0.1, 0.9),
                    RadiusX = 0.6,
                    RadiusY = 0.6
                };
            }
        }

        #endregion

        #region Synced Waveform Rendering (60fps composition loop)

        private void GenerateProceduralWaveform(int trackId, double duration)
        {
            int count = 76;
            int seed = (trackId * 1000 + (int)Math.Floor(duration)) != 0 ? (trackId * 1000 + (int)Math.Floor(duration)) : 42;
            var rand = new Random(seed);

            _currentWaveformHeights.Clear();

            // Create smooth harmonics
            double h1Freq = 0.05 + rand.NextDouble() * 0.05;
            double h2Freq = 0.1 + rand.NextDouble() * 0.15;
            double h3Freq = 0.2 + rand.NextDouble() * 0.3;

            double h1Amp = 0.3 + rand.NextDouble() * 0.2;
            double h2Amp = 0.15 + rand.NextDouble() * 0.15;
            double h3Amp = 0.05 + rand.NextDouble() * 0.1;

            double totalAmp = h1Amp + h2Amp + h3Amp;

            for (int i = 0; i < count; i++)
            {
                double baseVal = Math.Sin(i * h1Freq) * h1Amp +
                                 Math.Sin(i * h2Freq) * h2Amp +
                                 Math.Cos(i * h3Freq) * h3Amp;

                double normalized = (baseVal + totalAmp) / (totalAmp * 2);
                double noise = rand.NextDouble();

                double heightVal = 0.2 + normalized * 0.55 + noise * 0.2;
                heightVal = Math.Clamp(heightVal, 0.15, 0.95);
                _currentWaveformHeights.Add(heightVal);
            }

            // Pre-create the border elements on the canvas to avoid per-frame allocations
            WaveformCanvas.Children.Clear();
            for (int i = 0; i < count; i++)
            {
                var border = new Border();
                WaveformCanvas.Children.Add(border);
            }
        }

        private void OnCompositionTargetRendering(object sender, EventArgs e)
        {
            if (AudioEngine.Instance.IsPlaying)
            {
                SyncLyricsProgress(AudioEngine.Instance.Position);
            }

            if (WaveformCanvas.Visibility != Visibility.Visible || _currentWaveformHeights.Count == 0) return;
            if (WaveformCanvas.Children.Count < _currentWaveformHeights.Count) return;

            // Calculate progress ratio
            double curSec = AudioEngine.Instance.Position.TotalSeconds;
            double totSec = AudioEngine.Instance.Duration.TotalSeconds;
            if (totSec <= 0) totSec = 1;

            double progressRatio = Math.Clamp(curSec / totSec, 0.0, 1.0);

            // Layout on canvas
            double canvasWidth = WaveformCanvas.ActualWidth;
            double canvasHeight = WaveformCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            double progressX = progressRatio * canvasWidth;
            int count = _currentWaveformHeights.Count;
            double slotWidth = canvasWidth / count;
            double barWidth = Math.Max(1.5, slotWidth * 0.7);
            double gap = slotWidth - barWidth;

            // Reuse brushes to avoid allocation
            if (_activeBrush == null) _activeBrush = new SolidColorBrush(Color.FromArgb(242, 255, 255, 255)); // 95% opacity
            if (_inactiveBrush == null) _inactiveBrush = new SolidColorBrush(Color.FromArgb(61, 255, 255, 255)); // 24% opacity

            for (int i = 0; i < count; i++)
            {
                var border = (Border)WaveformCanvas.Children[i];
                double val = _currentWaveformHeights[i];
                double barHeight = Math.Max(4, val * canvasHeight * 0.72);
                double x = i * slotWidth + gap / 2;
                double y = (canvasHeight - barHeight) / 2;

                border.Width = barWidth;
                border.Height = barHeight;
                border.CornerRadius = new CornerRadius(barWidth / 2);
                
                // Color active or inactive based on progress
                bool isActive = (x + barWidth / 2) <= progressX;
                border.Background = isActive ? _activeBrush : _inactiveBrush;

                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
            }
        }

        private void WaveformCanvas_PointerPressed(object sender, MouseButtonEventArgs e)
        {
            if (_queue == null || _queue.Count == 0) return;
            
            double clickX = e.GetPosition(WaveformCanvas).X;
            double width = WaveformCanvas.ActualWidth;
            if (width <= 0) return;

            double ratio = Math.Clamp(clickX / width, 0.0, 1.0);
            double targetSeconds = ratio * AudioEngine.Instance.Duration.TotalSeconds;
            
            AudioEngine.Instance.Position = TimeSpan.FromSeconds(targetSeconds);
            CurrentTimeText.Text = FormatTime(targetSeconds);
        }

        #endregion

        #region Synced Lyrics Integration

        private void LoadLyrics(Track track)
        {
            _lyricsLines.Clear();
            LyricsStackPanel.Children.Clear();
            _lyricTextBlocks.Clear();
            _lastActiveLyricIndex = -1;

            string lrcText = null;

            // 1. Try sidecar .lrc file
            string dir = Path.GetDirectoryName(track.Path);
            string name = Path.GetFileNameWithoutExtension(track.Path);
            string sidecarLrc = Path.Combine(dir, name + ".lrc");

            if (File.Exists(sidecarLrc))
            {
                try
                {
                    lrcText = File.ReadAllText(sidecarLrc, Encoding.UTF8);
                }
                catch { }
            }

            // 2. Try embedded tag lyrics
            if (string.IsNullOrEmpty(lrcText))
            {
                try
                {
                    var tagFile = TagLib.File.Create(track.Path);
                    lrcText = tagFile.Tag.Lyrics;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(lrcText))
            {
                _lyricsLines = LrcParser.Parse(lrcText);
            }

            if (_lyricsLines.Count > 0)
            {
                NoLyricsPlaceholder.Visibility = Visibility.Collapsed;

                // Create UI TextBlocks for each lyric line
                for (int i = 0; i < _lyricsLines.Count; i++)
                {
                    var line = _lyricsLines[i];
                    var textBlock = new TextBlock
                    {
                        Foreground = Brushes.White,
                        Opacity = 0.40,
                        FontSize = 18, // Uniform font size for identical wrapping across states
                        FontWeight = FontWeights.Normal,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 280, 
                        Margin = new Thickness(0, 10, 0, 10),
                        Cursor = Cursors.Hand,
                        Tag = i
                    };

                    if (line.HasWordSync)
                    {
                        foreach (var word in line.Words)
                        {
                            var run = new System.Windows.Documents.Run
                            {
                                Text = word.Text + " "
                            };
                            
                            var brush = new LinearGradientBrush
                            {
                                StartPoint = new Point(0, 0),
                                EndPoint = new Point(1, 0)
                            };
                            brush.GradientStops.Add(new GradientStop(Colors.White, 0.0));
                            brush.GradientStops.Add(new GradientStop(Color.FromArgb(102, 255, 255, 255), 0.0));
                            
                            run.Foreground = brush;
                            run.Tag = word;
                            textBlock.Inlines.Add(run);
                        }
                    }
                    else
                    {
                        textBlock.Text = line.Text;
                    }

                    // Clicking a line seeks player to that time
                    textBlock.MouseLeftButtonDown += LyricLine_MouseLeftButtonDown;
                    
                    LyricsStackPanel.Children.Add(textBlock);
                    _lyricTextBlocks.Add(textBlock);
                }
            }
            else
            {
                NoLyricsPlaceholder.Visibility = Visibility.Visible;
                NoLyricsPlaceholder.Text = "No lyrics found.\nAdd a .lrc file in the same directory.";
                LyricsStackPanel.Children.Add(NoLyricsPlaceholder);
            }
        }

        private void LyricLine_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBlock = (TextBlock)sender;
            int index = (int)textBlock.Tag;
            if (index >= 0 && index < _lyricsLines.Count)
            {
                var line = _lyricsLines[index];
                AudioEngine.Instance.Position = line.Time;
                CurrentTimeText.Text = FormatTime(line.Time.TotalSeconds);
            }
        }

        private void SyncLyricsProgress(TimeSpan currentPosition)
        {
            if (_lyricsLines.Count == 0 || _lyricTextBlocks.Count == 0) return;

            int activeIndex = LrcParser.GetActiveLineIndex(_lyricsLines, currentPosition);
            if (activeIndex != _lastActiveLyricIndex)
            {
                // Reset old active line
                if (_lastActiveLyricIndex >= 0 && _lastActiveLyricIndex < _lyricTextBlocks.Count)
                {
                    var oldBlock = _lyricTextBlocks[_lastActiveLyricIndex];
                    AnimateLyricLine(oldBlock, active: false);
                    oldBlock.Effect = null;
                }

                // Highlight new active line
                if (activeIndex >= 0 && activeIndex < _lyricTextBlocks.Count)
                {
                    var newBlock = _lyricTextBlocks[activeIndex];
                    AnimateLyricLine(newBlock, active: true);

                    var line = _lyricsLines[activeIndex];
                    if (line.HasWordSync)
                    {
                        newBlock.Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = Colors.White,
                            BlurRadius = 15,
                            ShadowDepth = 0,
                            Opacity = 0.7
                        };
                    }

                    // Smoothly scroll active line to center of scroll view
                    ScrollLyricToCenter(newBlock);
                }

                // Reset all word-sync lines' gradients to their correct state based on whether they are before or after the active line
                for (int i = 0; i < _lyricTextBlocks.Count; i++)
                {
                    var block = _lyricTextBlocks[i];
                    var lLine = _lyricsLines[i];
                    if (lLine.HasWordSync)
                    {
                        double targetOffset = (i < activeIndex) ? 1.0 : 0.0;
                        foreach (var inline in block.Inlines)
                        {
                            if (inline is System.Windows.Documents.Run run && run.Foreground is LinearGradientBrush brush && brush.GradientStops.Count >= 2)
                            {
                                brush.GradientStops[0].Offset = targetOffset;
                                brush.GradientStops[1].Offset = targetOffset;
                            }
                        }
                    }
                }

                _lastActiveLyricIndex = activeIndex;
            }

            // Real-time progressive word-by-word highlighting within the active line
            if (activeIndex >= 0 && activeIndex < _lyricTextBlocks.Count)
            {
                var activeBlock = _lyricTextBlocks[activeIndex];
                var line = _lyricsLines[activeIndex];
                if (line.HasWordSync)
                {
                    foreach (var inline in activeBlock.Inlines)
                    {
                        if (inline is System.Windows.Documents.Run run && run.Tag is LrcWord word)
                        {
                            double progress = 0.0;
                            if (currentPosition >= word.Time)
                            {
                                TimeSpan elapsed = currentPosition - word.Time;
                                if (word.Duration.TotalMilliseconds > 0)
                                {
                                    progress = Math.Clamp(elapsed.TotalMilliseconds / word.Duration.TotalMilliseconds, 0.0, 1.0);
                                }
                                else
                                {
                                    progress = 1.0;
                                }
                            }
                            
                            if (run.Foreground is LinearGradientBrush brush && brush.GradientStops.Count >= 2)
                            {
                                brush.GradientStops[0].Offset = progress;
                                brush.GradientStops[1].Offset = progress;
                            }
                        }
                    }
                }
            }
        }

        private void AnimateLyricLine(TextBlock textBlock, bool active)
        {
            var opacityAnim = new DoubleAnimation
            {
                To = active ? 1.0 : 0.40,
                Duration = TimeSpan.FromMilliseconds(200)
            };

            textBlock.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            textBlock.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
        }

        private void ScrollLyricToCenter(TextBlock activeBlock)
        {
            try
            {
                // Calculate position relative to the StackPanel container
                var relativePoint = activeBlock.TransformToAncestor(LyricsStackPanel).Transform(new Point(0, 0));
                double blockY = relativePoint.Y;

                double viewportHeight = LyricsScrollViewer.ActualHeight;
                if (viewportHeight <= 0) viewportHeight = 350; // Fallback estimate

                // Center in ScrollViewer
                double scrollOffset = blockY + LyricsStackPanel.Margin.Top - (viewportHeight / 2) + (activeBlock.ActualHeight / 2);
                scrollOffset = Math.Max(0, scrollOffset);

                // Smooth scroll animation
                var storyboard = new Storyboard();
                var animation = new DoubleAnimation
                {
                    To = scrollOffset,
                    Duration = TimeSpan.FromMilliseconds(400),
                    DecelerationRatio = 0.5
                };
                Storyboard.SetTarget(animation, LyricsScrollViewer);
                Storyboard.SetTargetProperty(animation, new PropertyPath(ScrollViewerBehavior.VerticalOffsetProperty));
                storyboard.Children.Add(animation);
                storyboard.Begin();
            }
            catch { }
        }

        private void LoadLrcManually_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTrackIndex < 0 || _currentTrackIndex >= _queue.Count) return;
            var track = _queue[_currentTrackIndex];

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "LRC Lyrics Files (*.lrc)|*.lrc|Text Files (*.txt)|*.txt",
                Title = "Select Lyrics File"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                    
                    // Copy selected LRC file to the music sidecar destination
                    string musicDir = Path.GetDirectoryName(track.Path);
                    string musicName = Path.GetFileNameWithoutExtension(track.Path);
                    string targetLrc = Path.Combine(musicDir, musicName + ".lrc");

                    if (dialog.FileName != targetLrc)
                    {
                        File.Copy(dialog.FileName, targetLrc, overwrite: true);
                    }

                    LoadLyrics(track);
                }
                catch (Exception ex)
                {
                    ShowCustomMessageBox("Failed to load lyrics: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region User Event Handlers

        private void AudioEngine_PositionChanged(object sender, TimeSpan currentPosition)
        {
            Dispatcher.Invoke(() =>
            {
                double seconds = currentPosition.TotalSeconds;
                CurrentTimeText.Text = FormatTime(seconds);

                double tot = AudioEngine.Instance.Duration.TotalSeconds;
                if (tot > 0)
                {
                    double remaining = Math.Max(0, tot - seconds);
                    RemainingTimeText.Text = "-" + FormatTime(remaining);
                }
            });
        }

        private void AudioEngine_PlaybackFinished(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_isRepeat)
                {
                    SelectTrack(_currentTrackIndex, playImmediately: true);
                }
                else if (_isShuffle && _queue.Count > 1)
                {
                    var rand = new Random();
                    int nextIdx = rand.Next(_queue.Count);
                    if (nextIdx == _currentTrackIndex)
                    {
                        nextIdx = (nextIdx + 1) % _queue.Count;
                    }
                    SelectTrack(nextIdx, playImmediately: true);
                }
                else
                {
                    int nextIdx = (_currentTrackIndex + 1) % _queue.Count;
                    SelectTrack(nextIdx, playImmediately: true);
                }
            });
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_queue.Count == 0) return;

            if (AudioEngine.Instance.IsPlaying)
            {
                AudioEngine.Instance.Pause();
                PlayPauseIcon.Data = (Geometry)Application.Current.Resources["IconPlay"];
                PlayPauseIcon.Margin = new Thickness(4, 0, 0, 0); // Visual offset center
                _smtcService?.SetPlaybackStatus(false);
            }
            else
            {
                if (_currentTrackIndex < 0)
                {
                    SelectTrack(0, playImmediately: true);
                }
                else if (AudioEngine.Instance.Duration == TimeSpan.Zero)
                {
                    SelectTrack(_currentTrackIndex, playImmediately: true);
                }
                else
                {
                    AudioEngine.Instance.Play();
                    PlayPauseIcon.Data = (Geometry)Application.Current.Resources["IconPause"];
                    PlayPauseIcon.Margin = new Thickness(0, 0, 0, 0); // No offset when paused
                    _smtcService?.SetPlaybackStatus(true);
                }
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_queue.Count == 0) return;
            int prevIdx = _currentTrackIndex - 1;
            if (prevIdx < 0) prevIdx = _queue.Count - 1;
            SelectTrack(prevIdx, playImmediately: true);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_queue.Count == 0) return;
            int nextIdx = (_currentTrackIndex + 1) % _queue.Count;
            SelectTrack(nextIdx, playImmediately: true);
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTrackIndex >= 0 && _currentTrackIndex < _queue.Count)
            {
                var track = _queue[_currentTrackIndex];
                Database.Instance.ToggleTrackInPlaylist("Favorites", track.Id);
                
                // Update visual state
                bool isFav = Database.Instance.IsTrackInPlaylist("Favorites", track.Id);
                FavoriteStarPath.Fill = isFav ? Brushes.White : Brushes.Transparent;

                // If we are currently viewing the "Favorites" playlist, refresh the queue
                if (PlaylistsListBox.SelectedItem is Playlist selected && selected.Name == "Favorites")
                {
                    var allTracks = Database.Instance.GetTracks();
                    var playlists = Database.Instance.GetPlaylists();
                    var updatedFav = playlists.Find(p => p.Name == "Favorites") ?? selected;
                    var trackIds = updatedFav.TrackIds ?? new List<int>();
                    _queue = allTracks.Where(t => trackIds.Contains(t.Id)).ToList();
                    QueueListBox.ItemsSource = null;
                    QueueListBox.ItemsSource = _queue;
                    PlayQueueHeaderTitle.Text = $"Play Queue ({_queue.Count})";
                }
            }
        }

        private void InitializePlaylists()
        {
            var playlists = Database.Instance.GetPlaylists();
            var items = new List<Playlist>();

            // Virtual playlist for All Songs
            items.Add(new Playlist
            {
                Id = 9999,
                Name = "All Songs"
            });

            // Make sure Favorites is initialized in DB
            var favPlaylist = playlists.Find(p => p.Name == "Favorites");
            if (favPlaylist == null)
            {
                favPlaylist = new Playlist
                {
                    Id = 2,
                    Name = "Favorites",
                    TrackIds = new List<int>()
                };
                playlists.Add(favPlaylist);
                Database.Instance.SavePlaylists(playlists);
            }
            items.Add(favPlaylist);

            // Add any other user playlists
            foreach (var p in playlists)
            {
                if (p.Name != "Favorites" && p.Name != "Library")
                {
                    items.Add(p);
                }
            }

            PlaylistsListBox.ItemsSource = items;
            PlaylistsListBox.SelectedIndex = 0;
        }

        private void PlaylistsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                var allTracks = Database.Instance.GetTracks();
                
                if (selectedPlaylist.Id == 9999) // All Songs
                {
                    _browsedQueue = allTracks;
                }
                else
                {
                    // Refresh the playlist reference from database to get latest track IDs
                    var playlists = Database.Instance.GetPlaylists();
                    var currentPlaylist = playlists.Find(p => p.Id == selectedPlaylist.Id) ?? selectedPlaylist;
                    var trackIds = currentPlaylist.TrackIds ?? new List<int>();
                    _browsedQueue = allTracks.Where(t => trackIds.Contains(t.Id)).ToList();
                }

                QueueListBox.ItemsSource = null;
                QueueListBox.ItemsSource = _browsedQueue;

                PlayQueueHeaderTitle.Text = $"Play Queue ({_browsedQueue.Count})";
            }
        }

        private void PlayPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var playlist = (Playlist)button.Tag;
            if (playlist == null) return;

            var allTracks = Database.Instance.GetTracks();
            List<Track> playlistTracks;

            if (playlist.Id == 9999) // All Songs
            {
                playlistTracks = allTracks;
            }
            else
            {
                var playlists = Database.Instance.GetPlaylists();
                var currentPlaylist = playlists.Find(p => p.Id == playlist.Id) ?? playlist;
                var trackIds = currentPlaylist.TrackIds ?? new List<int>();
                playlistTracks = allTracks.Where(t => trackIds.Contains(t.Id)).ToList();
            }

            if (playlistTracks.Count > 0)
            {
                // Load into active queue and browsed queue
                _queue = playlistTracks;
                _browsedQueue = playlistTracks;
                QueueListBox.ItemsSource = null;
                QueueListBox.ItemsSource = _browsedQueue;
                PlayQueueHeaderTitle.Text = $"Play Queue ({_browsedQueue.Count})";

                // Play first song
                SelectTrack(0, playImmediately: true);
            }
            else
            {
                ShowCustomMessageBox("This playlist has no songs.", "Empty Playlist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTrackIndex < 0 || _currentTrackIndex >= _queue.Count) return;
            var track = _queue[_currentTrackIndex];

            var contextMenu = new ContextMenu();

            // Menu Item 1: Details
            var itemDetails = new MenuItem { Header = "Song Details" };
            itemDetails.Click += (s, ev) =>
            {
                ShowCustomMessageBox($"File: {track.FileName}\nPath: {track.Path}\nSize: {track.SizeBytes / (1024f * 1024f):F2} MB", "Song Details", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            contextMenu.Items.Add(itemDetails);

         //   contextMenu.Items.Add(new Separator());

            // Menu Item 2: Add to Playlist
            var itemAddToPlaylist = new MenuItem { Header = "Add to Playlist" };

            // Option to Create a Playlist right from the context menu
            var itemCreatePlaylist = new MenuItem { Header = "Create Playlist" };
            itemCreatePlaylist.Click += (s, ev) =>
            {
                string name = PromptForPlaylistName();
                if (string.IsNullOrEmpty(name)) return;

                var freshPlaylists = Database.Instance.GetPlaylists();
                if (freshPlaylists.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    ShowCustomMessageBox($"A playlist named \"{name}\" already exists.", "Duplicate Playlist", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newPlaylist = new Playlist
                {
                    Id = freshPlaylists.Count > 0 ? freshPlaylists.Max(p => p.Id) + 1 : 1,
                    Name = name,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    TrackIds = new List<int> { track.Id } // Add the song immediately
                };
                freshPlaylists.Add(newPlaylist);
                Database.Instance.SavePlaylists(freshPlaylists);

                InitializePlaylists();

                // Select the newly created playlist in the UI
                for (int i = 0; i < PlaylistsListBox.Items.Count; i++)
                {
                    if (PlaylistsListBox.Items[i] is Playlist pl && pl.Id == newPlaylist.Id)
                    {
                        PlaylistsListBox.SelectedIndex = i;
                        break;
                    }
                }
            };
            itemAddToPlaylist.Items.Add(itemCreatePlaylist);
            
            var playlists = Database.Instance.GetPlaylists();
            foreach (var p in playlists)
            {
                // Exclude "Library" and "Favorites" from addition list (Favorites has a dedicated star button)
                if (p.Name == "Library" || p.Name == "Favorites") continue;

                var playlistItem = new MenuItem { Header = p.Name, Tag = p };
                playlistItem.Click += (s, ev) =>
                {
                    var clickedMenu = (MenuItem)s;
                    var targetPlaylist = (Playlist)clickedMenu.Tag;
                    if (targetPlaylist != null)
                    {
                        // Reload playlists from database to avoid stale references
                        var freshPlaylists = Database.Instance.GetPlaylists();
                        var dbPlaylist = freshPlaylists.Find(x => x.Id == targetPlaylist.Id);
                        if (dbPlaylist != null)
                        {
                            if (!dbPlaylist.TrackIds.Contains(track.Id))
                            {
                                dbPlaylist.TrackIds.Add(track.Id);
                                dbPlaylist.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                                Database.Instance.SavePlaylists(freshPlaylists);
                                
                                // Refresh browsed list if we are currently looking at this playlist
                                if (PlaylistsListBox.SelectedItem is Playlist selected && selected.Id == dbPlaylist.Id)
                                {
                                    var allTracks = Database.Instance.GetTracks();
                                    _browsedQueue = allTracks.Where(t => dbPlaylist.TrackIds.Contains(t.Id)).ToList();
                                    QueueListBox.ItemsSource = null;
                                    QueueListBox.ItemsSource = _browsedQueue;
                                    PlayQueueHeaderTitle.Text = $"Play Queue ({_browsedQueue.Count})";
                                }
                            }
                        }
                    }
                };
                itemAddToPlaylist.Items.Add(playlistItem);
            }

            contextMenu.Items.Add(itemAddToPlaylist);

            // Open context menu near the button
            var button = (Button)sender;
            contextMenu.PlacementTarget = button;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            contextMenu.IsOpen = true;
        }

        private void CreatePlaylist_Click(object sender, RoutedEventArgs e)
        {
            string name = PromptForPlaylistName();
            if (string.IsNullOrEmpty(name)) return;

            var playlists = Database.Instance.GetPlaylists();
            if (playlists.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                ShowCustomMessageBox($"A playlist named \"{name}\" already exists.", "Duplicate Playlist", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newPlaylist = new Playlist
            {
                Id = playlists.Count > 0 ? playlists.Max(p => p.Id) + 1 : 1,
                Name = name,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                TrackIds = new List<int>()
            };
            playlists.Add(newPlaylist);
            Database.Instance.SavePlaylists(playlists);

            InitializePlaylists();
            
            // Select the newly created playlist
            for (int i = 0; i < PlaylistsListBox.Items.Count; i++)
            {
                if (PlaylistsListBox.Items[i] is Playlist p && p.Id == newPlaylist.Id)
                {
                    PlaylistsListBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void DeletePlaylist_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var playlist = (Playlist)button.Tag;
            if (playlist == null || !playlist.CanDelete) return;

            var result = ShowCustomMessageBox($"Are you sure you want to delete the playlist \"{playlist.Name}\"?", "Delete Playlist", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                var playlists = Database.Instance.GetPlaylists();
                var p = playlists.Find(x => x.Id == playlist.Id);
                if (p != null)
                {
                    playlists.Remove(p);
                    Database.Instance.SavePlaylists(playlists);
                    InitializePlaylists();
                }
            }
        }

        private string PromptForPlaylistName()
        {
            var dialog = new Window
            {
                Title = "Create Playlist",
                Width = 320,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(242, 18, 19, 26)), // #F212131A
                BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)), // #26FFFFFF
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(0)
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleTxt = new TextBlock
            {
                Text = "New Playlist Name",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(titleTxt, 0);
            grid.Children.Add(titleTxt);

            var textBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 16)
            };
            textBox.Loaded += (s, ev) => textBox.Focus();
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonGrid = new Grid();
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnCancel = new Button
            {
                Content = "Cancel",
                Style = (Style)FindResource("TextButtonStyle"),
                Height = 32
            };
            btnCancel.Click += (s, ev) => { dialog.DialogResult = false; dialog.Close(); };
            Grid.SetColumn(btnCancel, 0);
            buttonGrid.Children.Add(btnCancel);

            var btnCreate = new Button
            {
                Content = "Create",
                Style = (Style)FindResource("TextButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Height = 32,
                IsDefault = true
            };
            btnCreate.Click += (s, ev) => { dialog.DialogResult = true; dialog.Close(); };
            Grid.SetColumn(btnCreate, 2);
            buttonGrid.Children.Add(btnCreate);

            Grid.SetRow(buttonGrid, 2);
            grid.Children.Add(buttonGrid);

            border.Child = grid;
            dialog.Content = border;

            if (dialog.ShowDialog() == true)
            {
                return textBox.Text.Trim();
            }
            return null;
        }

        private MessageBoxResult ShowCustomMessageBox(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 360,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(242, 18, 19, 26)), // #F212131A
                BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)), // #26FFFFFF
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(0)
            };

            var grid = new Grid { Margin = new Thickness(24) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Message
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Title
            var titleTxt = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(titleTxt, 0);
            grid.Children.Add(titleTxt);

            // Message
            var msgTxt = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 200)),
                FontSize = 13,
                LineHeight = 18,
                Margin = new Thickness(0, 0, 0, 20),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(msgTxt, 1);
            grid.Children.Add(msgTxt);

            // Buttons grid
            var buttonGrid = new Grid();
            var result = MessageBoxResult.None;

            if (buttons == MessageBoxButton.YesNo)
            {
                buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var btnNo = new Button
                {
                    Content = "No",
                    Style = (Style)FindResource("TextButtonStyle"),
                    Height = 32
                };
                btnNo.Click += (s, ev) => { result = MessageBoxResult.No; dialog.DialogResult = false; dialog.Close(); };
                Grid.SetColumn(btnNo, 0);
                buttonGrid.Children.Add(btnNo);

                var btnYes = new Button
                {
                    Content = "Yes",
                    Style = (Style)FindResource("TextButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                    Height = 32,
                    IsDefault = true
                };
                btnYes.Click += (s, ev) => { result = MessageBoxResult.Yes; dialog.DialogResult = true; dialog.Close(); };
                Grid.SetColumn(btnYes, 2);
                buttonGrid.Children.Add(btnYes);
            }
            else // Default OK button
            {
                buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var btnOk = new Button
                {
                    Content = "OK",
                    Style = (Style)FindResource("TextButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                    Height = 32,
                    IsDefault = true
                };
                btnOk.Click += (s, ev) => { result = MessageBoxResult.OK; dialog.DialogResult = true; dialog.Close(); };
                Grid.SetColumn(btnOk, 0);
                buttonGrid.Children.Add(btnOk);
            }

            Grid.SetRow(buttonGrid, 2);
            grid.Children.Add(buttonGrid);

            border.Child = grid;
            dialog.Content = border;

            dialog.ShowDialog();
            return result;
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var result = ShowCustomMessageBox("Are you sure you want to clear the track cache and local cover thumbnails? Your playlists will be preserved.", "Clear Cache", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                // Stop playback
                AudioEngine.Instance.Stop();
                _currentTrackIndex = -1;
                
                // Clear active queues
                _queue = new List<Track>();
                _browsedQueue = new List<Track>();
                QueueListBox.ItemsSource = null;
                PlayQueueHeaderTitle.Text = "Play Queue (0)";

                // Clear tracks in database
                Database.Instance.SaveTracks(new List<Track>());
                
                // Clear settings
                Database.Instance.SetSetting("last_scanned_folder", "");
                Database.Instance.SetSetting("last_track_id", "");

                // Delete local thumbnail files
                try
                {
                    string coverCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AmberolNet", "covers");
                    if (Directory.Exists(coverCacheDir))
                    {
                        var files = Directory.GetFiles(coverCacheDir);
                        foreach (var file in files)
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                }
                catch { }

                // Refresh playlists to sync UI state
                InitializePlaylists();

                // Show welcome screen
                ShowWelcomeScreen();
                ShowCustomMessageBox("Cache cleared successfully.", "Clear Cache", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowWelcomeScreen()
        {
            WelcomePanel.Visibility = Visibility.Visible;
            AlbumArtBorder.Visibility = Visibility.Collapsed;
            TrackInfoPanel.Visibility = Visibility.Collapsed;
            WaveformCanvas.Visibility = Visibility.Collapsed;
            TimeLabelsPanel.Visibility = Visibility.Collapsed;
            PlaybackControlsPanel.Visibility = Visibility.Collapsed;

            // Hide bottom toolbar panels (Equalizer and Lyrics icons)
            BottomToolbarPanel.Visibility = Visibility.Collapsed;

            // Clear background and album art images
            BlurredBgImage.Source = null;
            AlbumArtImage.Source = null;

            // Collapse sidebars
            _isQueuePaneOpen = false;
            _isLyricsPaneOpen = false;
            UpdateWindowLayout();
        }

        #endregion

        #region Sliding Panes & Sizing Animations

        private void LyricsToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isLyricsPaneOpen = !_isLyricsPaneOpen;
            UpdateWindowLayout();
        }

        private void CloseLyrics_Click(object sender, RoutedEventArgs e)
        {
            _isLyricsPaneOpen = false;
            UpdateWindowLayout();
        }

        private void QueueToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isQueuePaneOpen = !_isQueuePaneOpen;
            
            if (_isQueuePaneOpen)
            {
                QueueListBox.ItemsSource = null;
                QueueListBox.ItemsSource = _browsedQueue;
                if (_queue == _browsedQueue && _currentTrackIndex >= 0 && _currentTrackIndex < _queue.Count)
                {
                    QueueListBox.SelectedIndex = _currentTrackIndex;
                    QueueListBox.ScrollIntoView(_queue[_currentTrackIndex]);
                }
            }
            
            UpdateWindowLayout();
        }

        private void UpdateWindowLayout()
        {
            // 1. Calculate Target Backdrop Width and Margin Left
            double targetBackdropWidth = 380;
            Thickness targetBackdropMargin;

            if (_isQueuePaneOpen && _isLyricsPaneOpen)
            {
                targetBackdropWidth = 1140;
                targetBackdropMargin = new Thickness(0, 0, 0, 0);
            }
            else if (_isQueuePaneOpen)
            {
                targetBackdropWidth = 760;
                targetBackdropMargin = new Thickness(0, 0, 380, 0);
            }
            else if (_isLyricsPaneOpen)
            {
                targetBackdropWidth = 760;
                targetBackdropMargin = new Thickness(380, 0, 0, 0);
            }
            else
            {
                targetBackdropWidth = 380;
                targetBackdropMargin = new Thickness(380, 0, 380, 0);
            }

            // 2. Animate Backdrop Width and Margin
            var widthAnim = new DoubleAnimation(BackdropBorder.ActualWidth, targetBackdropWidth, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            var marginAnim = new ThicknessAnimation(BackdropBorder.Margin, targetBackdropMargin, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            BackdropBorder.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            BackdropBorder.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);

            // 3. Animate Sliding Transforms (Queue right-to-left, Lyrics left-to-right)
            var queueTransform = (TranslateTransform)QueuePane.RenderTransform;
            var queueAnim = new DoubleAnimation(queueTransform.X, _isQueuePaneOpen ? 0 : 380, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            queueTransform.BeginAnimation(TranslateTransform.XProperty, queueAnim);

            var lyricsTransform = (TranslateTransform)LyricsPane.RenderTransform;
            var lyricsAnim = new DoubleAnimation(lyricsTransform.X, _isLyricsPaneOpen ? 0 : -380, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            lyricsTransform.BeginAnimation(TranslateTransform.XProperty, lyricsAnim);
        }

        private void SpeedDecrease_Click(object sender, RoutedEventArgs e)
        {
            double newSpeed = AudioEngine.Instance.Speed - 0.10;
            UpdateSpeed(newSpeed);
        }

        private void SpeedIncrease_Click(object sender, RoutedEventArgs e)
        {
            double newSpeed = AudioEngine.Instance.Speed + 0.10;
            UpdateSpeed(newSpeed);
        }

        private void UpdateSpeed(double speedVal)
        {
            speedVal = Math.Round(speedVal, 1);
            speedVal = Math.Clamp(speedVal, 0.5, 2.0);

            AudioEngine.Instance.Speed = speedVal;
            SpeedText.Text = $"Speed {speedVal:F2}x";

            Database.Instance.SetSetting("speed", speedVal.ToString("F2"));
        }

        #endregion

        #region Equalizer Dialog Toggle

        private void EqualizerButton_Click(object sender, RoutedEventArgs e)
        {
            EqDialogOverlay.Visibility = Visibility.Visible;
            
            // Fade-in overlay
            var fadeAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
            EqDialogOverlay.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        private void CloseEq_Click(object sender, RoutedEventArgs e)
        {
            var fadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150));
            fadeAnim.Completed += (s, ev) =>
            {
                EqDialogOverlay.Visibility = Visibility.Collapsed;
            };
            EqDialogOverlay.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        private void EqOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CloseEq_Click(null, null);
        }

        private void EqBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Stop event bubbling
        }

        #endregion

        #region Play Queue Event Handlers & List Updates

        private void QueueListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QueueListBox.SelectedIndex >= 0 && QueueListBox.SelectedIndex < _browsedQueue.Count)
            {
                int selectedIdx = QueueListBox.SelectedIndex;

                // Sync active playing queue with the browsed queue
                if (_queue != _browsedQueue)
                {
                    _queue = _browsedQueue;
                }

                if (selectedIdx != _currentTrackIndex)
                {
                    SelectTrack(selectedIdx, playImmediately: true);
                }
            }
        }

        private void RemoveFromQueue_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var track = (Track)button.Tag;
            if (track == null) return;

            int idx = _browsedQueue.IndexOf(track);
            if (idx >= 0)
            {
                _browsedQueue.RemoveAt(idx);
                
                // Persist removal to the database if viewing a specific playlist
                if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist && selectedPlaylist.Id != 9999)
                {
                    var playlists = Database.Instance.GetPlaylists();
                    var dbPlaylist = playlists.Find(p => p.Id == selectedPlaylist.Id);
                    if (dbPlaylist != null)
                    {
                        dbPlaylist.TrackIds.Remove(track.Id);
                        dbPlaylist.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        Database.Instance.SavePlaylists(playlists);
                    }
                }

                // If active playing queue is the same, also update it
                if (_queue == _browsedQueue)
                {
                    // Adjust current track index if necessary
                    if (idx < _currentTrackIndex)
                    {
                        _currentTrackIndex--;
                    }
                    else if (idx == _currentTrackIndex)
                    {
                        // Playing track was removed, play the next one if possible
                        if (_queue.Count > 0)
                        {
                            _currentTrackIndex = _currentTrackIndex % _queue.Count;
                            SelectTrack(_currentTrackIndex, playImmediately: AudioEngine.Instance.IsPlaying);
                        }
                        else
                        {
                            AudioEngine.Instance.Stop();
                            _currentTrackIndex = -1;
                            
                            // Show welcome screen
                            WelcomePanel.Visibility = Visibility.Visible;
                            AlbumArtBorder.Visibility = Visibility.Collapsed;
                            TrackInfoPanel.Visibility = Visibility.Collapsed;
                            WaveformCanvas.Visibility = Visibility.Collapsed;
                            TimeLabelsPanel.Visibility = Visibility.Collapsed;
                            PlaybackControlsPanel.Visibility = Visibility.Collapsed;
                        }
                    }
                }

                // Refresh queue items source
                QueueListBox.ItemsSource = null;
                QueueListBox.ItemsSource = _browsedQueue;
                PlayQueueHeaderTitle.Text = $"Play Queue ({_browsedQueue.Count})";
            }
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            _isShuffle = !_isShuffle;
            
            // Update button visual state
            if (_isShuffle)
            {
                ShuffleButton.ToolTip = "Shuffle On";
                var path = (System.Windows.Shapes.Path)ShuffleButton.Content;
                path.Fill = new SolidColorBrush(Color.FromArgb(242, 255, 255, 255)); // Bright white
            }
            else
            {
                ShuffleButton.ToolTip = "Shuffle Off";
                var path = (System.Windows.Shapes.Path)ShuffleButton.Content;
                path.Fill = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255)); // Translucent white (40%)
            }
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            _isRepeat = !_isRepeat;
            
            // Update button visual state
            if (_isRepeat)
            {
                RepeatButton.ToolTip = "Repeat On";
                var path = (System.Windows.Shapes.Path)RepeatButton.Content;
                path.Fill = new SolidColorBrush(Color.FromArgb(242, 255, 255, 255)); // Bright white
            }
            else
            {
                RepeatButton.ToolTip = "Repeat Off";
                var path = (System.Windows.Shapes.Path)RepeatButton.Content;
                path.Fill = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255)); // Translucent white (40%)
            }
        }

        #endregion

        #region Utilities

        private string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                return "0:00";

            int min = (int)(seconds / 60);
            int sec = (int)(seconds % 60);
            return $"{min}:{sec:D2}";
        }

        #endregion
    }
}
