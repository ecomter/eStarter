using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace eStarter.Sdk
{
    /// <summary>
    /// Inter-process communication API wrapper.
    /// </summary>
    public sealed class IpcApi
    {
        private readonly EStarterClient _client;

        // API command codes
        private const ushort CmdSendMessage = 0x0400;
        private const ushort CmdBroadcast = 0x0401;
        private const ushort CmdSubscribe = 0x0402;
        private const ushort CmdUnsubscribe = 0x0403;

        internal IpcApi(EStarterClient client) => _client = client;

        /// <summary>
        /// Send a message to another app.
        /// </summary>
        public Task<ApiResult> SendAsync(string targetAppId, string channel, object data)
            => _client.CallApiAsync(CmdSendMessage, new { target = targetAppId, channel, data });

        /// <summary>
        /// Broadcast a message to all apps.
        /// </summary>
        public Task<ApiResult> BroadcastAsync(string channel, object data)
            => _client.CallApiAsync(CmdBroadcast, new { channel, data });

        /// <summary>
        /// Subscribe to a channel.
        /// </summary>
        public Task<ApiResult> SubscribeAsync(string channel)
            => _client.CallApiAsync(CmdSubscribe, new { channel });

        /// <summary>
        /// Unsubscribe from a channel.
        /// </summary>
        public Task<ApiResult> UnsubscribeAsync(string channel)
            => _client.CallApiAsync(CmdUnsubscribe, new { channel });
    }
}
