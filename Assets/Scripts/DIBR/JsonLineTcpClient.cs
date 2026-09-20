using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Thesis.Dibr
{
    // Shared transport for the newline-delimited-JSON TCP control channels
    // used by both OpenDIBR (Item 4 of
    // Master-Thesis-Reports/handoff_spec_Sep_20th_opendibr_live_control_and_export.md,
    // port 40124) and the DIBR bridge process (Phase A of the plan, port
    // 40125, same request/reply shape). One command in flight at a time per
    // instance; reconnects lazily on first use or after a failure.
    internal sealed class JsonLineTcpClient : IDisposable
    {
        private readonly IPEndPoint _endpoint;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private TcpClient _tcp;
        private System.IO.StreamWriter _writer;
        private System.IO.StreamReader _reader;

        public JsonLineTcpClient(int port) => _endpoint = new IPEndPoint(IPAddress.Loopback, port);

        // Sends one JSON line, waits for one JSON line back. Throws on
        // connect failure, timeout, or a closed connection — callers treat
        // any exception as "the process isn't ready yet" (drives the
        // fallback-first UX), not a fatal error.
        public async Task<string> SendAsync(string jsonLine, int timeoutMs = 3000)
        {
            await _lock.WaitAsync();
            try
            {
                await EnsureConnectedAsync(timeoutMs);
                await _writer.WriteLineAsync(jsonLine);

                var readTask = _reader.ReadLineAsync();
                var winner = await Task.WhenAny(readTask, Task.Delay(timeoutMs));
                if (winner != readTask)
                {
                    Disconnect();
                    throw new TimeoutException($"No reply within {timeoutMs}ms.");
                }

                string line = await readTask;
                if (line == null)
                {
                    Disconnect();
                    throw new System.IO.IOException("Connection closed by remote.");
                }
                return line;
            }
            catch
            {
                Disconnect();
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task EnsureConnectedAsync(int timeoutMs)
        {
            if (_tcp is { Connected: true }) return;

            Disconnect();
            _tcp = new TcpClient();
            var connectTask = _tcp.ConnectAsync(_endpoint.Address, _endpoint.Port);
            var winner = await Task.WhenAny(connectTask, Task.Delay(timeoutMs));
            if (winner != connectTask)
                throw new TimeoutException($"Connect timed out after {timeoutMs}ms.");
            await connectTask; // propagate connection exceptions, if any

            var stream = _tcp.GetStream();
            _writer = new System.IO.StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            _reader = new System.IO.StreamReader(stream, Encoding.UTF8);
        }

        private void Disconnect()
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _tcp?.Dispose();
            _writer = null;
            _reader = null;
            _tcp = null;
        }

        public void Dispose()
        {
            Disconnect();
            _lock.Dispose();
        }
    }
}
