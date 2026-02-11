using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace eStarter.Sdk.Ipc
{
    public enum IpcMessageType : byte
    {
        Handshake = 0,
        Command = 1,
        Event = 2,
        Response = 3,
        Error = 4,
        ApiRequest = 5,
        ApiResponse = 6,
        Ping = 7,
        Pong = 8
    }

    /// <summary>
    /// Optimized IPC message with minimal allocations.
    /// </summary>
    public sealed class IpcMessage
    {
        private static uint _idCounter;

        [JsonPropertyName("id")]
        public uint Id { get; set; }

        [JsonPropertyName("type")]
        public IpcMessageType Type { get; set; }

        [JsonPropertyName("src")]
        public string SourceAppId { get; set; } = string.Empty;

        [JsonPropertyName("dst")]
        public string TargetId { get; set; } = "system";

        [JsonPropertyName("cmd")]
        public ushort Command { get; set; }

        [JsonPropertyName("payload")]
        public string Payload { get; set; } = string.Empty;

        [JsonPropertyName("ts")]
        public long Timestamp { get; set; }

        [JsonIgnore]
        public DateTime Time => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).UtcDateTime;

        public IpcMessage()
        {
            Id = NextId();
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint NextId() => System.Threading.Interlocked.Increment(ref _idCounter);

        public static IpcMessage CreateApiRequest(string sourceAppId, ushort command, string payload = "")
            => new()
            {
                Type = IpcMessageType.ApiRequest,
                SourceAppId = sourceAppId,
                Command = command,
                Payload = payload
            };

        public static IpcMessage CreateApiResponse(uint requestId, string sourceAppId, string payload)
            => new()
            {
                Id = requestId,
                Type = IpcMessageType.ApiResponse,
                SourceAppId = sourceAppId,
                Payload = payload
            };

        public static IpcMessage CreateEvent(string sourceAppId, string eventName, string payload = "")
            => new()
            {
                Type = IpcMessageType.Event,
                SourceAppId = sourceAppId,
                Payload = JsonSerializer.Serialize(new { name = eventName, data = payload })
            };

        public static IpcMessage Ping(string sourceAppId)
            => new() { Type = IpcMessageType.Ping, SourceAppId = sourceAppId };

        public static IpcMessage Pong(uint requestId)
            => new() { Id = requestId, Type = IpcMessageType.Pong };
    }

    public sealed class HandshakePayload
    {
        [JsonPropertyName("appId")]
        public string AppId { get; set; } = string.Empty;

        [JsonPropertyName("ver")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("pid")]
        public int ProcessId { get; set; }

        [JsonPropertyName("perms")]
        public ulong RequestedPermissions { get; set; }
    }

    /// <summary>
    /// Shared JSON options for IPC serialization.
    /// </summary>
    public static class IpcSerializer
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? Deserialize<T>(ReadOnlySpan<byte> utf8Json) => JsonSerializer.Deserialize<T>(utf8Json, Options);
    }
}
