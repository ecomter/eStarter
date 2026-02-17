using System;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;

namespace eStarter.Sdk.Ipc
{
    public static class PipeStreamHelper
    {
        public static async Task WriteMessageAsync(PipeStream stream, IpcMessage message)
        {
            var json = JsonSerializer.Serialize(message);
            var buffer = System.Text.Encoding.UTF8.GetBytes(json);
            var lengthBuffer = BitConverter.GetBytes(buffer.Length);

            await stream.WriteAsync(lengthBuffer, 0, 4);
            await stream.WriteAsync(buffer, 0, buffer.Length);
            await stream.FlushAsync();
        }

        public static async Task<IpcMessage?> ReadMessageAsync(PipeStream stream)
        {
            var lengthBuffer = new byte[4];
            int bytesRead = await ReadFullAsync(stream, lengthBuffer, 4);
            if (bytesRead == 0) return null; // End of stream

            int length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > 10 * 1024 * 1024) return null; // Safety check: 10MB limit

            var buffer = new byte[length];
            await ReadFullAsync(stream, buffer, length);

            var json = System.Text.Encoding.UTF8.GetString(buffer);
            return JsonSerializer.Deserialize<IpcMessage>(json);
        }

        private static async Task<int> ReadFullAsync(PipeStream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
                if (read == 0) return totalRead; // Unexpected end of stream or valid end
                totalRead += read;
            }
            return totalRead;
        }
    }
}
