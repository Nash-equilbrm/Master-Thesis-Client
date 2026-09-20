using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using UnityEngine;

namespace Thesis.Dibr
{
    // Reads OpenDIBR's live frame export (Item 3 of
    // Master-Thesis-Reports/handoff_spec_Sep_20th_opendibr_live_control_and_export.md)
    // — named shared memory "OpenDIBR_FrameExport" + named event
    // "OpenDIBR_FrameReady" — and uploads each frame into a Texture2D.
    //
    // Uses .NET's MemoryMappedFile/EventWaitHandle directly (no native plugin
    // needed — these Win32-backed APIs are available from managed C# in
    // Unity, since they're both created as *named* kernel objects on the
    // OpenDIBR side).
    public sealed class OpenDibrFrameReceiver : MonoBehaviour
    {
        private const string MapName = "OpenDIBR_FrameExport";
        private const string EventName = "OpenDIBR_FrameReady";
        private const int HeaderBytes = 4 + 4 + 4 + 8; // width, height, format(uint32 each), frameCounter(uint64)
        private const int TornReadRetries = 3;

        public Texture2D Texture { get; private set; }
        public bool IsRunning { get; private set; }
        public event Action OnFrameUpdated;

        private MemoryMappedFile _mmf;
        private EventWaitHandle _frameReadyEvent;
        private Thread _thread;
        private volatile bool _running;

        private readonly object _lock = new();
        private byte[] _pendingPixels;
        private int _pendingWidth, _pendingHeight;
        private bool _hasPending;

        // Returns true once connected to both kernel objects. Returns false
        // (harmlessly, not an exception) if OpenDIBR hasn't created them yet
        // — callers should retry with backoff, this is the expected state
        // before/while OpenDIBR is still starting up.
        public bool TryStart()
        {
            if (_running) return true;

            try
            {
                _mmf = MemoryMappedFile.OpenExisting(MapName);
                _frameReadyEvent = EventWaitHandle.OpenExisting(EventName);
            }
            catch (Exception)
            {
                _mmf?.Dispose();
                _mmf = null;
                return false;
            }

            _running = true;
            IsRunning = true;
            _thread = new Thread(ReadLoop) { IsBackground = true, Name = "OpenDibrFrameReceiver" };
            _thread.Start();
            return true;
        }

        public void Stop()
        {
            _running = false;
            IsRunning = false;
            _frameReadyEvent?.Set(); // wake the blocked WaitOne so the thread can exit
            _thread?.Join(500);
            _thread = null;
            _frameReadyEvent?.Dispose();
            _frameReadyEvent = null;
            _mmf?.Dispose();
            _mmf = null;
        }

        private void ReadLoop()
        {
            while (_running)
            {
                bool signaled;
                try { signaled = _frameReadyEvent.WaitOne(500); }
                catch (ObjectDisposedException) { return; }
                if (!signaled || !_running) continue;

                try
                {
                    using var accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

                    for (int attempt = 0; attempt < TornReadRetries; attempt++)
                    {
                        ulong counterBefore = accessor.ReadUInt64(12);
                        uint width = accessor.ReadUInt32(0);
                        uint height = accessor.ReadUInt32(4);
                        // format at offset 8 — only RGBA8 (0) is defined by the spec today.

                        long pixelBytes = (long)width * height * 4;
                        if (pixelBytes <= 0) break;

                        var buffer = new byte[pixelBytes];
                        accessor.ReadArray(HeaderBytes, buffer, 0, (int)pixelBytes);
                        ulong counterAfter = accessor.ReadUInt64(12);

                        if (counterBefore != counterAfter) continue; // torn — retry

                        lock (_lock)
                        {
                            _pendingPixels = buffer;
                            _pendingWidth = (int)width;
                            _pendingHeight = (int)height;
                            _hasPending = true;
                        }
                        break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[OpenDibrFrameReceiver] Read failed: {e.Message}");
                }
            }
        }

        private void Update()
        {
            byte[] pixels;
            int w, h;
            lock (_lock)
            {
                if (!_hasPending) return;
                pixels = _pendingPixels;
                w = _pendingWidth;
                h = _pendingHeight;
                _hasPending = false;
            }

            if (Texture == null || Texture.width != w || Texture.height != h)
            {
                if (Texture != null) Destroy(Texture);
                Texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            }

            Texture.LoadRawTextureData(pixels);
            Texture.Apply(false);
            OnFrameUpdated?.Invoke();
        }

        private void OnDestroy() => Stop();
    }
}
