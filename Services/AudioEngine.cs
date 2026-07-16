using System;
using System.IO;
using System.Timers;
using NAudio.Wave;
using NAudio.Dsp;

namespace AmberolWpf.Services
{
    public class AudioEngine : IDisposable
    {
        private WaveOutEvent _waveOut;
        private AudioFileReaderWrapper _audioReader;
        private SpeedControlSampleProvider _speedProvider;
        private EqualizerSampleProvider _eqProvider;

        private float _volume = 1.0f;
        private double _speed = 1.0;
        private bool _eqEnabled = false;
        private float[] _eqGains = new float[10];

        private bool _isChangingTrack = false;
        private System.Timers.Timer _positionTimer;

        // Tracks exact playback position from hardware output bytes
        private TimeSpan _positionAtSeek = TimeSpan.Zero;
        private long _waveOutBytesAtSeek = 0;

        public event EventHandler PlaybackFinished;
        public event EventHandler<TimeSpan> PositionChanged;

        private static AudioEngine _instance;
        public static AudioEngine Instance => _instance ??= new AudioEngine();

        private AudioEngine()
        {
            _positionTimer = new System.Timers.Timer(100); // 10 ticks per second
            _positionTimer.Elapsed += OnPositionTimerElapsed;
        }

        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

        public TimeSpan Position
        {
            get
            {
                if (_audioReader == null) return TimeSpan.Zero;
                if (_waveOut == null) return _audioReader.CurrentTime;
                try
                {
                    // GetPosition() returns actual bytes rendered by hardware — no buffer lag.
                    long currentBytes = _waveOut.GetPosition();
                    long deltaBytes = Math.Max(0, currentBytes - _waveOutBytesAtSeek);
                    var fmt = _eqProvider?.WaveFormat ?? _audioReader.WaveFormat;
                    if (fmt.AverageBytesPerSecond <= 0) return _audioReader.CurrentTime;
                    var pos = _positionAtSeek + TimeSpan.FromSeconds(deltaBytes / (double)fmt.AverageBytesPerSecond);
                    var total = _audioReader.TotalTime;
                    return pos > total ? total : pos;
                }
                catch
                {
                    return _audioReader.CurrentTime;
                }
            }
            set
            {
                if (_audioReader != null)
                {
                    _audioReader.CurrentTime = value;
                    _speedProvider?.Reset();
                    // Record offset so GetPosition() delta is relative to the seek point
                    _positionAtSeek = value;
                    if (_waveOut != null)
                        _waveOutBytesAtSeek = _waveOut.GetPosition();
                }
            }
        }

        public TimeSpan Duration => _audioReader?.TotalTime ?? TimeSpan.Zero;

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 1f);
                if (_audioReader != null)
                {
                    _audioReader.Volume = _volume;
                }
            }
        }

        public double Speed
        {
            get => _speed;
            set
            {
                _speed = Math.Clamp(value, 0.5, 2.0);
                if (_speedProvider != null)
                {
                    _speedProvider.Speed = _speed;
                }
            }
        }

        public bool EqEnabled
        {
            get => _eqEnabled;
            set
            {
                _eqEnabled = value;
                if (_eqProvider != null)
                {
                    _eqProvider.Enabled = _eqEnabled;
                }
            }
        }

        public float[] EqGains
        {
            get => _eqGains;
            set
            {
                if (value != null && value.Length == 10)
                {
                    _eqGains = value;
                    if (_eqProvider != null)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            _eqProvider.SetGain(i, _eqGains[i]);
                        }
                    }
                }
            }
        }

        public void PlayTrack(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("Audio file not found", filePath);
            }

            _isChangingTrack = true;
            Stop();

            try
            {
                _audioReader = new AudioFileReaderWrapper(filePath)
                {
                    Volume = _volume
                };

                // Speed provider wraps AudioFileReader
                _speedProvider = new SpeedControlSampleProvider(_audioReader)
                {
                    Speed = _speed
                };

                // Equalizer provider wraps speed provider
                _eqProvider = new EqualizerSampleProvider(_speedProvider)
                {
                    Enabled = _eqEnabled
                };

                // Apply current gains to EQ provider
                for (int i = 0; i < 10; i++)
                {
                    _eqProvider.SetGain(i, _eqGains[i]);
                }

                _waveOut = new WaveOutEvent();
                _waveOut.Init(_eqProvider);
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Play();

                // Reset position tracking — GetPosition() starts at 0 on a new device
                _positionAtSeek = TimeSpan.Zero;
                _waveOutBytesAtSeek = 0;

                _isChangingTrack = false;
                _positionTimer.Start();
            }
            catch (Exception ex)
            {
                _isChangingTrack = false;
                Stop();
                throw new Exception("Failed to play track: " + ex.Message, ex);
            }
        }

        public void Play()
        {
            if (_waveOut != null && _waveOut.PlaybackState != PlaybackState.Playing)
            {
                _waveOut.Play();
                _positionTimer.Start();
            }
        }

        public void Pause()
        {
            if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
            {
                _waveOut.Pause();
                _positionTimer.Stop();
            }
        }

        public void Stop()
        {
            _positionTimer.Stop();

            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            if (_audioReader != null)
            {
                _audioReader.Dispose();
                _audioReader = null;
            }

            _speedProvider = null;
            _eqProvider = null;
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            _positionTimer.Stop();
            
            // If the track stopped naturally and was not stopped by user actions
            if (!_isChangingTrack)
            {
                // Double check if we reached the end of the file (within 0.5s)
                if (_audioReader != null && (_audioReader.TotalTime - _audioReader.CurrentTime).TotalSeconds < 0.5)
                {
                    PlaybackFinished?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void OnPositionTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (IsPlaying && _audioReader != null)
            {
                // Fire with the accurate hardware position, not the buffered reader position
                PositionChanged?.Invoke(this, Position);
            }
        }

        public void Dispose()
        {
            Stop();
            _positionTimer?.Dispose();
        }
    }

    public class SpeedControlSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private double _playbackSpeed = 1.0;
        private readonly float[] _sourceBuffer;
        private int _sourceBufferLength;
        private double _currentSourceIndex = 0.0;

        public SpeedControlSampleProvider(ISampleProvider source)
        {
            _source = source;
            // 2 seconds buffer based on sample rate and channels
            _sourceBuffer = new float[source.WaveFormat.SampleRate * source.WaveFormat.Channels * 2];
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public double Speed
        {
            get => _playbackSpeed;
            set
            {
                if (value >= 0.5 && value <= 2.0)
                {
                    _playbackSpeed = value;
                }
            }
        }

        public void Reset()
        {
            _sourceBufferLength = 0;
            _currentSourceIndex = 0.0;
        }

        public double BufferDelaySeconds
        {
            get
            {
                int channels = _source.WaveFormat.Channels;
                int sampleRate = _source.WaveFormat.SampleRate;
                if (sampleRate <= 0 || channels <= 0) return 0.0;
                double remainingSamples = _sourceBufferLength - (_currentSourceIndex * channels);
                return Math.Max(0.0, remainingSamples / (sampleRate * channels));
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int channels = _source.WaveFormat.Channels;
            int framesNeeded = count / channels;
            int framesWritten = 0;

            while (framesWritten < framesNeeded)
            {
                int frameIndexNeeded = (int)Math.Floor(_currentSourceIndex);
                int sampleIndexNeeded = (frameIndexNeeded + 1) * channels;

                if (sampleIndexNeeded >= _sourceBufferLength)
                {
                    int unreadStartSample = frameIndexNeeded * channels;
                    int remainingSamples = _sourceBufferLength - unreadStartSample;
                    if (remainingSamples > 0 && unreadStartSample > 0)
                    {
                        Array.Copy(_sourceBuffer, unreadStartSample, _sourceBuffer, 0, remainingSamples);
                        _sourceBufferLength = remainingSamples;
                    }
                    else if (unreadStartSample >= _sourceBufferLength)
                    {
                        _sourceBufferLength = 0;
                    }

                    _currentSourceIndex -= frameIndexNeeded;

                    int readSpace = _sourceBuffer.Length - _sourceBufferLength;
                    int read = _source.Read(_sourceBuffer, _sourceBufferLength, readSpace);
                    if (read == 0 && _sourceBufferLength == 0)
                    {
                        break; // EOF
                    }
                    _sourceBufferLength += read;

                    frameIndexNeeded = (int)Math.Floor(_currentSourceIndex);
                    sampleIndexNeeded = (frameIndexNeeded + 1) * channels;

                    if (sampleIndexNeeded >= _sourceBufferLength)
                    {
                        break; // EOF
                    }
                }

                int index = (int)Math.Floor(_currentSourceIndex);
                double fraction = _currentSourceIndex - index;

                for (int ch = 0; ch < channels; ch++)
                {
                    float s1 = _sourceBuffer[index * channels + ch];
                    float s2 = _sourceBuffer[(index + 1) * channels + ch];
                    buffer[offset + framesWritten * channels + ch] = (float)(s1 + (s2 - s1) * fraction);
                }

                framesWritten++;
                _currentSourceIndex += _playbackSpeed;
            }

            return framesWritten * channels;
        }
    }

    public class EqualizerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly BiQuadFilter[,] _filters; // [bandCount, channelCount]
        private readonly int _bands;
        private readonly int _channels;
        private readonly float[] _gains = new float[10];
        private bool _enabled;

        public EqualizerSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            _bands = 10;
            _filters = new BiQuadFilter[_bands, _channels];
            
            UpdateFilters();
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                UpdateFilters();
            }
        }

        public void SetGain(int bandIndex, float gainDb)
        {
            if (bandIndex >= 0 && bandIndex < 10)
            {
                _gains[bandIndex] = gainDb;
                UpdateFilters();
            }
        }

        private void UpdateFilters()
        {
            double[] frequencies = { 62.5, 110, 250, 370, 650, 1200, 2130, 4550, 6850, 16000 };
            double q = 1.0;

            for (int band = 0; band < _bands; band++)
            {
                float gain = _enabled ? _gains[band] : 0.0f;
                double freq = frequencies[band];

                for (int channel = 0; channel < _channels; channel++)
                {
                    if (band == 0)
                    {
                        _filters[band, channel] = BiQuadFilter.LowShelf((float)_source.WaveFormat.SampleRate, (float)freq, (float)q, gain);
                    }
                    else if (band == _bands - 1)
                    {
                        _filters[band, channel] = BiQuadFilter.HighShelf((float)_source.WaveFormat.SampleRate, (float)freq, (float)q, gain);
                    }
                    else
                    {
                        _filters[band, channel] = BiQuadFilter.PeakingEQ((float)_source.WaveFormat.SampleRate, (float)freq, (float)q, gain);
                    }
                }
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);
            if (!_enabled) return samplesRead;

            for (int i = 0; i < samplesRead; i++)
            {
                int channel = (offset + i) % _channels;
                float sample = buffer[offset + i];

                for (int band = 0; band < _bands; band++)
                {
                    var filter = _filters[band, channel];
                    if (filter != null)
                    {
                        sample = filter.Transform(sample);
                    }
                }

                buffer[offset + i] = sample;
            }

            return samplesRead;
        }
    }
}
