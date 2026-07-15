using System;
using System.IO;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AmberolWpf.Services
{
    public class SmtcService
    {
        private SystemMediaTransportControls? _smtc;

        public event Action? PlayPauseRequested;
        public event Action? NextRequested;
        public event Action? PreviousRequested;

        public void Initialize(IntPtr hwnd)
        {
            try
            {
                // In CsWinRT (.NET 6+), use SystemMediaTransportControlsInterop helper class
                _smtc = SystemMediaTransportControlsInterop.GetForWindow(hwnd);
                
                _smtc.IsPlayEnabled = true;
                _smtc.IsPauseEnabled = true;
                _smtc.IsNextEnabled = true;
                _smtc.IsPreviousEnabled = true;

                _smtc.ButtonPressed += Smtc_ButtonPressed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize SMTC: {ex.Message}");
            }
        }

        private void Smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                case SystemMediaTransportControlsButton.Pause:
                    PlayPauseRequested?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    NextRequested?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    PreviousRequested?.Invoke();
                    break;
            }
        }

        public async void UpdateMetadata(string title, string artist, string album, string? coverArtPath)
        {
            if (_smtc == null) return;

            try
            {
                var updater = _smtc.DisplayUpdater;
                updater.Type = MediaPlaybackType.Music;
                updater.MusicProperties.Title = title ?? "Unknown Title";
                updater.MusicProperties.Artist = artist ?? "Unknown Artist";
                updater.MusicProperties.AlbumTitle = album ?? "Unknown Album";

                if (!string.IsNullOrEmpty(coverArtPath) && File.Exists(coverArtPath))
                {
                    try
                    {
                        var storageFile = await StorageFile.GetFileFromPathAsync(coverArtPath);
                        updater.Thumbnail = RandomAccessStreamReference.CreateFromFile(storageFile);
                    }
                    catch
                    {
                        updater.Thumbnail = null;
                    }
                }
                else
                {
                    updater.Thumbnail = null;
                }

                updater.Update();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update SMTC metadata: {ex.Message}");
            }
        }

        public void SetPlaybackStatus(bool isPlaying)
        {
            if (_smtc == null) return;

            try
            {
                _smtc.PlaybackStatus = isPlaying ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set SMTC playback status: {ex.Message}");
            }
        }
    }
}
