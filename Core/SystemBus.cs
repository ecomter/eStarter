using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using eStarter.Sdk.Ipc;

namespace eStarter.Core
{
    public class SystemBus : IDisposable
    {
        private bool _isRunning;
        private const string PipeName = "eStarterBus";
        private CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<string, PipeStream> _connectedApps = new ConcurrentDictionary<string, PipeStream>();

        // Event to notify UI or System Services about new messages
        public event EventHandler<IpcMessage> MessageReceived;
        public event EventHandler<string> AppConnected;
        public event EventHandler<string> AppDisconnected;

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            
            // Start the listening loop
            Task.Run(ListenLoop, _cts.Token);
            Debug.WriteLine($"[SystemBus] Started listening on pipe: {PipeName}");
        }

        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            foreach (var stream in _connectedApps.Values)
            {
                try { stream.Dispose(); } catch { }
            }
            _connectedApps.Clear();
        }

        private async Task ListenLoop()
        {
            while (_isRunning && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Create a new pipe instance for the next client
                    var server = new NamedPipeServerStream(
                        PipeName, 
                        PipeDirection.InOut, 
                        NamedPipeServerStream.MaxAllowedServerInstances, 
                        PipeTransmissionMode.Message, 
                        PipeOptions.Asynchronous);

                    // Wait for connection
                    await server.WaitForConnectionAsync(_cts.Token);

                    // Handle this connection in a separate task
                    _ = HandleClientAsync(server);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SystemBus] Error in listener: {ex.Message}");
                    await Task.Delay(1000); // Backoff on error
                }
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream stream)
        {
            string appId = string.Empty;
            try
            {
                // Read handshake or first message
                // For simplicity, we just enter the read loop. 
                // The first message SHOULD be a Handshake according to protocol, 
                // but we handle generic messages here.

                while (stream.IsConnected && _isRunning)
                {
                    var message = await PipeStreamHelper.ReadMessageAsync(stream);
                    if (message == null) break; // Connection closed

                    // Handle Handshake specifically to register the connection
                    if (message.Type == IpcMessageType.Handshake)
                    {
                        appId = message.SourceAppId;
                        _connectedApps.TryAdd(appId, stream);
                        AppConnected?.Invoke(this, appId);
                        Debug.WriteLine($"[SystemBus] App connected: {appId}");
                    }

                    // Propagate message
                    MessageReceived?.Invoke(this, message);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemBus] Client error ({appId}): {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(appId))
                {
                    _connectedApps.TryRemove(appId, out _);
                    AppDisconnected?.Invoke(this, appId);
                }
                stream.Dispose();
            }
        }

        public async Task SendToAppAsync(string targetAppId, IpcMessage message)
        {
            if (_connectedApps.TryGetValue(targetAppId, out var stream))
            {
                if (stream.IsConnected)
                {
                    await PipeStreamHelper.WriteMessageAsync(stream, message);
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
