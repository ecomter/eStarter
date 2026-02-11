using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace eStarter.Sdk
{
    /// <summary>
    /// Event subscription manager for the SDK client.
    /// </summary>
    public sealed class EventManager
    {
        private readonly EStarterClient _client;
        private readonly ConcurrentDictionary<string, ConcurrentBag<Action<JsonElement?>>> _handlers = new();
        private readonly ConcurrentDictionary<string, ConcurrentBag<Func<JsonElement?, Task>>> _asyncHandlers = new();

        internal EventManager(EStarterClient client)
        {
            _client = client;
            _client.EventReceived += OnEventReceived;
        }

        /// <summary>
        /// Subscribe to an event with a synchronous handler.
        /// </summary>
        public IDisposable Subscribe(string eventName, Action<JsonElement?> handler)
        {
            var handlers = _handlers.GetOrAdd(eventName, _ => []);
            handlers.Add(handler);
            return new Subscription(() => TryRemove(handlers, handler));
        }

        /// <summary>
        /// Subscribe to an event with an async handler.
        /// </summary>
        public IDisposable Subscribe(string eventName, Func<JsonElement?, Task> handler)
        {
            var handlers = _asyncHandlers.GetOrAdd(eventName, _ => []);
            handlers.Add(handler);
            return new Subscription(() => TryRemove(handlers, handler));
        }

        /// <summary>
        /// Subscribe to a typed event.
        /// </summary>
        public IDisposable Subscribe<T>(string eventName, Action<T?> handler)
        {
            return Subscribe(eventName, data =>
            {
                if (data.HasValue)
                {
                    var typed = JsonSerializer.Deserialize<T>(data.Value.GetRawText());
                    handler(typed);
                }
                else
                {
                    handler(default);
                }
            });
        }

        /// <summary>
        /// Wait for a specific event once.
        /// </summary>
        public async Task<JsonElement?> WaitForAsync(string eventName, int timeoutMs = 30000, CancellationToken ct = default)
        {
            var tcs = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(JsonElement? data)
            {
                tcs.TrySetResult(data);
            }

            using var subscription = Subscribe(eventName, Handler);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            cts.Token.Register(() => tcs.TrySetCanceled());

            return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Wait for a typed event once.
        /// </summary>
        public async Task<T?> WaitForAsync<T>(string eventName, int timeoutMs = 30000, CancellationToken ct = default)
        {
            var data = await WaitForAsync(eventName, timeoutMs, ct).ConfigureAwait(false);
            if (data.HasValue)
            {
                return JsonSerializer.Deserialize<T>(data.Value.GetRawText());
            }
            return default;
        }

        /// <summary>
        /// Clear all handlers for an event.
        /// </summary>
        public void Clear(string eventName)
        {
            _handlers.TryRemove(eventName, out _);
            _asyncHandlers.TryRemove(eventName, out _);
        }

        /// <summary>
        /// Clear all handlers.
        /// </summary>
        public void ClearAll()
        {
            _handlers.Clear();
            _asyncHandlers.Clear();
        }

        private void OnEventReceived(string eventName, JsonElement? data)
        {
            // Invoke sync handlers
            if (_handlers.TryGetValue(eventName, out var syncHandlers))
            {
                foreach (var handler in syncHandlers)
                {
                    try { handler(data); } catch { }
                }
            }

            // Invoke async handlers
            if (_asyncHandlers.TryGetValue(eventName, out var asyncHandlers))
            {
                foreach (var handler in asyncHandlers)
                {
                    _ = Task.Run(async () =>
                    {
                        try { await handler(data).ConfigureAwait(false); } catch { }
                    });
                }
            }
        }

        private static void TryRemove<T>(ConcurrentBag<T> bag, T item)
        {
            // ConcurrentBag doesn't support removal, so we rebuild
            var items = bag.ToArray();
            while (bag.TryTake(out _)) { }
            foreach (var i in items)
            {
                if (!ReferenceEquals(i, item))
                {
                    bag.Add(i);
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action? _onDispose;

            public Subscription(Action onDispose) => _onDispose = onDispose;

            public void Dispose()
            {
                Interlocked.Exchange(ref _onDispose, null)?.Invoke();
            }
        }
    }

    /// <summary>
    /// Common system event names.
    /// </summary>
    public static class SystemEvents
    {
        public const string AppLaunched = "app.launched";
        public const string AppTerminated = "app.terminated";
        public const string PermissionGranted = "permission.granted";
        public const string PermissionDenied = "permission.denied";
        public const string ClipboardChanged = "clipboard.changed";
        public const string SettingsChanged = "settings.changed";
        public const string ThemeChanged = "theme.changed";
        public const string Shutdown = "system.shutdown";
    }
}
