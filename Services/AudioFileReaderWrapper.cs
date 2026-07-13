using System;
using System.IO;
using NAudio.Wave;
using AmberolWpf.Streams;

namespace AmberolWpf.Services
{
    public class AudioFileReaderWrapper : WaveStream, ISampleProvider
    {
        private readonly WaveStream _readerStream;
        private readonly ISampleProvider _sampleProvider;
        private float _volume = 1.0f;

        public AudioFileReaderWrapper(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            if (ext == ".opus")
            {
                var opusStream = new OpusWaveStream(filePath);
                _readerStream = opusStream;
                _sampleProvider = opusStream;
            }
            else
            {
                var fileReader = new AudioFileReader(filePath);
                _readerStream = fileReader;
                _sampleProvider = fileReader;
                _volume = fileReader.Volume;
            }
        }

        public override WaveFormat WaveFormat => _readerStream.WaveFormat;

        public override long Length => _readerStream.Length;

        public override long Position
        {
            get => _readerStream.Position;
            set => _readerStream.Position = value;
        }

        public float Volume
        {
            get
            {
                if (_readerStream is AudioFileReader fileReader)
                    return fileReader.Volume;
                return _volume;
            }
            set
            {
                _volume = value;
                if (_readerStream is AudioFileReader fileReader)
                    fileReader.Volume = _volume;
            }
        }

        public override TimeSpan CurrentTime
        {
            get => _readerStream.CurrentTime;
            set => _readerStream.CurrentTime = value;
        }

        public override TimeSpan TotalTime => _readerStream.TotalTime;

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _sampleProvider.Read(buffer, offset, count);
            if (_readerStream is not AudioFileReader && _volume != 1.0f)
            {
                for (int i = 0; i < read; i++)
                {
                    buffer[offset + i] *= _volume;
                }
            }
            return read;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _readerStream.Read(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _readerStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
