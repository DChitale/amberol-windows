using System;
using System.IO;
using System.Collections.Generic;
using NAudio.Wave;
using Concentus;
using Concentus.Structs;
using Concentus.Oggfile;

namespace AmberolWpf.Streams
{
    public class OpusWaveStream : WaveStream, ISampleProvider
    {
        private readonly WaveFormat _waveFormat;
        private readonly List<float> _decodedSamples = new List<float>();
        private long _positionInSamples = 0;

        public OpusWaveStream(string filePath)
        {
            // Set standard format (Opus is always 48000 Hz, stereo)
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var decoder = OpusCodecFactory.CreateDecoder(48000, 2);
                var oggStream = new OpusOggReadStream(decoder, fileStream);

                while (oggStream.HasNextPacket)
                {
                    short[] packet = oggStream.DecodeNextPacket();
                    if (packet != null)
                    {
                        foreach (short sample in packet)
                        {
                            _decodedSamples.Add(sample / 32768f);
                        }
                    }
                }
            }
        }

        public override WaveFormat WaveFormat => _waveFormat;

        public override long Length => _decodedSamples.Count * 4; // float is 4 bytes

        public override long Position
        {
            get => _positionInSamples * 4;
            set
            {
                long samplePos = value / 4;
                _positionInSamples = Math.Clamp(samplePos, 0, _decodedSamples.Count);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int floatCount = count / 4;
            int floatsRead = ReadFloat(buffer, offset, floatCount);
            return floatsRead * 4;
        }

        private int ReadFloat(byte[] byteBuffer, int byteOffset, int floatCount)
        {
            int availableFloats = (int)(_decodedSamples.Count - _positionInSamples);
            int floatsToRead = Math.Min(floatCount, availableFloats);

            if (floatsToRead <= 0) return 0;

            for (int i = 0; i < floatsToRead; i++)
            {
                float val = _decodedSamples[(int)(_positionInSamples + i)];
                byte[] bytes = BitConverter.GetBytes(val);
                Buffer.BlockCopy(bytes, 0, byteBuffer, byteOffset + i * 4, 4);
            }

            _positionInSamples += floatsToRead;
            return floatsToRead;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int availableFloats = (int)(_decodedSamples.Count - _positionInSamples);
            int floatsToRead = Math.Min(count, availableFloats);

            if (floatsToRead <= 0) return 0;

            for (int i = 0; i < floatsToRead; i++)
            {
                buffer[offset + i] = _decodedSamples[(int)(_positionInSamples + i)];
            }
            _positionInSamples += floatsToRead;
            return floatsToRead;
        }
    }
}
