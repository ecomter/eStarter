using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace eStarter.Core.Kernel
{
    /// <summary>
    /// System API command categories.
    /// </summary>
    public enum ApiCategory : byte
    {
        System = 0,
        Process = 1,
        FileSystem = 2,
        UI = 3,
        Ipc = 4,
        Permission = 5,
        Registry = 6
    }

    /// <summary>
    /// System API commands. High byte = category, low byte = command.
    /// </summary>
    public enum ApiCommand : ushort
    {
        // System (0x00XX)
        Ping            = 0x0001,
        GetSystemInfo   = 0x0002,
        Shutdown        = 0x0003,
        GetTime         = 0x0004,

        // Process (0x01XX)
        Launch          = 0x0100,
        Terminate       = 0x0101,
        GetProcessList  = 0x0102,
        GetProcessInfo  = 0x0103,

        // FileSystem (0x02XX)
        ReadFile        = 0x0200,
        WriteFile       = 0x0201,
        DeleteFile      = 0x0202,
        ListDirectory   = 0x0203,
        CreateDirectory = 0x0204,
        FileExists      = 0x0205,
        GetFileInfo     = 0x0206,

        // UI (0x03XX)
        ShowNotification = 0x0300,
        ShowDialog       = 0x0301,
        ClipboardGet     = 0x0302,
        ClipboardSet     = 0x0303,
        ShowToast        = 0x0304,

        // IPC (0x04XX)
        SendMessage     = 0x0400,
        Broadcast       = 0x0401,
        Subscribe       = 0x0402,
        Unsubscribe     = 0x0403,

        // Permission (0x05XX)
        RequestPermission = 0x0500,
        CheckPermission   = 0x0501,
        GetPermissions    = 0x0502,

        // Registry (0x06XX)
        RegGet          = 0x0600,
        RegSet          = 0x0601,
        RegDelete       = 0x0602,
        RegList         = 0x0603
    }

    /// <summary>
    /// API response status codes.
    /// </summary>
    public enum ApiStatus : byte
    {
        Success = 0,
        Error = 1,
        PermissionDenied = 2,
        NotFound = 3,
        InvalidRequest = 4,
        Timeout = 5,
        Busy = 6,
        NotSupported = 7
    }

    /// <summary>
    /// Base API request. Minimal allocation, uses pooled JSON.
    /// </summary>
    public sealed class ApiRequest
    {
        [JsonPropertyName("cmd")]
        public ApiCommand Command { get; set; }

        [JsonPropertyName("rid")]
        public uint RequestId { get; set; }

        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }

        private static uint _nextId;
        public static uint NextId() => System.Threading.Interlocked.Increment(ref _nextId);
    }

    /// <summary>
    /// Base API response.
    /// </summary>
    public sealed class ApiResponse
    {
        [JsonPropertyName("rid")]
        public uint RequestId { get; set; }

        [JsonPropertyName("status")]
        public ApiStatus Status { get; set; }

        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        public static ApiResponse Success(uint requestId, JsonElement? data = null)
            => new() { RequestId = requestId, Status = ApiStatus.Success, Data = data };

        public static ApiResponse Fail(uint requestId, ApiStatus status, string error)
            => new() { RequestId = requestId, Status = status, Error = error };

        public static ApiResponse PermissionDenied(uint requestId, Permission missing)
            => new() { RequestId = requestId, Status = ApiStatus.PermissionDenied, Error = $"Missing: {missing}" };
    }

    /// <summary>
    /// Maps API commands to required permissions.
    /// </summary>
    public static class ApiPermissionMap
    {
        private static readonly Permission[] _map = new Permission[0x0700];

        static ApiPermissionMap()
        {
            // System
            Set(ApiCommand.Ping, Permission.None);
            Set(ApiCommand.GetSystemInfo, Permission.SystemInfo);
            Set(ApiCommand.Shutdown, Permission.Admin);
            Set(ApiCommand.GetTime, Permission.None);

            // Process
            Set(ApiCommand.Launch, Permission.ProcessLaunch);
            Set(ApiCommand.Terminate, Permission.ProcessKill);
            Set(ApiCommand.GetProcessList, Permission.SystemInfo);
            Set(ApiCommand.GetProcessInfo, Permission.SystemInfo);

            // FileSystem
            Set(ApiCommand.ReadFile, Permission.FileRead);
            Set(ApiCommand.WriteFile, Permission.FileWrite);
            Set(ApiCommand.DeleteFile, Permission.FileDelete);
            Set(ApiCommand.ListDirectory, Permission.FileRead);
            Set(ApiCommand.CreateDirectory, Permission.FileWrite);
            Set(ApiCommand.FileExists, Permission.FileRead);
            Set(ApiCommand.GetFileInfo, Permission.FileRead);

            // UI
            Set(ApiCommand.ShowNotification, Permission.Notification);
            Set(ApiCommand.ShowDialog, Permission.Dialog);
            Set(ApiCommand.ClipboardGet, Permission.Clipboard);
            Set(ApiCommand.ClipboardSet, Permission.Clipboard);
            Set(ApiCommand.ShowToast, Permission.Notification);

            // IPC
            Set(ApiCommand.SendMessage, Permission.IpcSend);
            Set(ApiCommand.Broadcast, Permission.IpcBroadcast);
            Set(ApiCommand.Subscribe, Permission.IpcReceive);
            Set(ApiCommand.Unsubscribe, Permission.IpcReceive);

            // Permission
            Set(ApiCommand.RequestPermission, Permission.None);
            Set(ApiCommand.CheckPermission, Permission.None);
            Set(ApiCommand.GetPermissions, Permission.None);

            // Registry
            Set(ApiCommand.RegGet, Permission.FileRead);
            Set(ApiCommand.RegSet, Permission.FileWrite);
            Set(ApiCommand.RegDelete, Permission.FileWrite);
            Set(ApiCommand.RegList, Permission.FileRead);
        }

        private static void Set(ApiCommand cmd, Permission perm) => _map[(int)cmd] = perm;

        public static Permission GetRequired(ApiCommand command)
        {
            var idx = (int)command;
            return idx < _map.Length ? _map[idx] : Permission.Admin;
        }
    }
}
