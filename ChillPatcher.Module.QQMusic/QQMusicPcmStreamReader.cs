using System;
using System.Runtime.InteropServices;
using System.Threading;
using ChillPatcher.SDK.Interfaces;
using ChillPatcher.SDK.Models;

namespace ChillPatcher.Module.QQMusic
{
    /// <summary>
    /// PCM stream reader for QQ Music that implements the SDK's IPcmStreamReader interface
    /// </summary>
    public class QQMusicPcmStreamReader : IPcmStreamReader
    {
        private readonly QQMusicBridge _bridge;
        private readonly long _streamId;
        private PcmStreamInfo _info;
        private bool _disposed;
        private bool _endOfStream;
        private IntPtr _nativeBuffer;
        private const int NATIVE_BUFFER_FRAMES = 4096;

        public QQMusicPcmStreamReader(QQMusicBridge bridge, long streamId, int sampleRate, int channels, float duration)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _streamId = streamId;

            // Initialize with provided values, will be updated when stream is ready
            _info = new PcmStreamInfo
            {
                SampleRate = sampleRate,
                Channels = channels,
                TotalFrames = (ulong)(duration * sampleRate),
                Format = "unknown",
                CanSeek = false
            };

            // Allocate native buffer for reading
            _nativeBuffer = Marshal.AllocHGlobal(NATIVE_BUFFER_FRAMES * channels * sizeof(float));
        }

        public PcmStreamInfo Info
        {
            get
            {
                // Try to get updated info from the stream
                if (IsReady)
                {
                    var nativeInfo = _bridge.GetPcmStreamInfo(_streamId);
                    if (nativeInfo != null)
                    {
                        _info = new PcmStreamInfo
                        {
                            SampleRate = nativeInfo.SampleRate,
                            Channels = nativeInfo.Channels,
                            TotalFrames = nativeInfo.TotalFrames,
                            Format = nativeInfo.Format,
                            CanSeek = nativeInfo.CanSeek
                        };
                    }
                }
                return _info;
            }
        }

        public ulong CurrentFrame => _bridge.GetPcmStreamCurrentFrame(_streamId);

        public bool IsEndOfStream => _endOfStream;

        public bool IsReady => _bridge.IsPcmStreamReady(_streamId);

        public bool CanSeek => Info.CanSeek || _bridge.IsCacheComplete(_streamId);

        public bool HasPendingSeek => _bridge.HasPendingSeek(_streamId);

        public long PendingSeekFrame => _bridge.GetPendingSeekFrame(_streamId);

        public double CacheProgress => _bridge.GetCacheProgress(_streamId);

        public bool IsCacheComplete => _bridge.IsCacheComplete(_streamId);

        public long ReadFrames(float[] buffer, int framesToRead)
        {
            if (_disposed || buffer == null || framesToRead <= 0)
                return 0;

            if (!IsReady)
                return 0;

            int totalFramesRead = 0;
            int channels = Info.Channels;
            int offset = 0;

            while (totalFramesRead < framesToRead)
            {
                int framesToReadThisBatch = Math.Min(framesToRead - totalFramesRead, NATIVE_BUFFER_FRAMES);
                int framesRead = _bridge.ReadPcmFrames(_streamId, _nativeBuffer, framesToReadThisBatch);

                if (framesRead <= 0)
                {
                    if (framesRead == 0 || framesRead == -1)
                    {
                        _endOfStream = true;
                    }
                    break;
                }

                // Copy from native buffer to managed array
                int samplesToRead = framesRead * channels;
                Marshal.Copy(_nativeBuffer, buffer, offset, samplesToRead);
                offset += samplesToRead;
                totalFramesRead += framesRead;
            }

            return totalFramesRead;
        }

        public bool Seek(ulong frameIndex)
        {
            if (_disposed)
                return false;

            _endOfStream = false;
            return _bridge.SeekPcmStream(_streamId, (long)frameIndex);
        }

        public void CancelPendingSeek()
        {
            if (!_disposed)
            {
                _bridge.CancelPendingSeek(_streamId);
            }
        }

        public bool WaitForReady(int timeoutMs = 20000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                if (IsReady)
                    return true;

                Thread.Sleep(50);
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_nativeBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_nativeBuffer);
                _nativeBuffer = IntPtr.Zero;
            }

            _bridge.ClosePcmStream(_streamId);
        }
    }
}
