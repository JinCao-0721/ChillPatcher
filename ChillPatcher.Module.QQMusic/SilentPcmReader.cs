using System;
using ChillPatcher.SDK.Interfaces;
using ChillPatcher.SDK.Models;

namespace ChillPatcher.Module.QQMusic
{
    /// <summary>
    /// A silent PCM reader used for login song placeholder
    /// </summary>
    public class SilentPcmReader : IPcmStreamReader
    {
        private readonly PcmStreamInfo _info;
        private ulong _currentFrame;
        private bool _disposed;

        public SilentPcmReader(float durationSeconds, int sampleRate = 44100, int channels = 2)
        {
            _info = new PcmStreamInfo
            {
                SampleRate = sampleRate,
                Channels = channels,
                TotalFrames = (ulong)(durationSeconds * sampleRate),
                Format = "silent",
                CanSeek = true
            };
        }

        public PcmStreamInfo Info => _info;

        public ulong CurrentFrame => _currentFrame;

        public bool IsEndOfStream => _currentFrame >= _info.TotalFrames;

        public bool IsReady => true;

        public bool CanSeek => true;

        public bool HasPendingSeek => false;

        public long PendingSeekFrame => -1;

        public double CacheProgress => 1.0;

        public bool IsCacheComplete => true;

        public long ReadFrames(float[] buffer, int framesToRead)
        {
            if (_disposed || IsEndOfStream)
                return 0;

            var framesRemaining = (long)(_info.TotalFrames - _currentFrame);
            var framesToActuallyRead = Math.Min(framesToRead, (int)framesRemaining);
            var samplesToWrite = framesToActuallyRead * _info.Channels;

            // Fill with silence (zeros)
            for (int i = 0; i < samplesToWrite; i++)
            {
                buffer[i] = 0f;
            }

            _currentFrame += (ulong)framesToActuallyRead;
            return framesToActuallyRead;
        }

        public bool Seek(ulong frameIndex)
        {
            if (_disposed)
                return false;

            if (frameIndex > _info.TotalFrames)
                frameIndex = _info.TotalFrames;

            _currentFrame = frameIndex;
            return true;
        }

        public void CancelPendingSeek()
        {
            // No pending seeks for silent reader
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
