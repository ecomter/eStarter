using System;
using System.Runtime.CompilerServices;

namespace eStarter.Core.Kernel
{
    /// <summary>
    /// System permissions using bit flags for O(1) permission checks.
    /// </summary>
    [Flags]
    public enum Permission : ulong
    {
        None = 0,

        // File System (bits 0-7)
        FileRead        = 1UL << 0,
        FileWrite       = 1UL << 1,
        FileDelete      = 1UL << 2,
        FileSystem      = FileRead | FileWrite | FileDelete,

        // Network (bits 8-15)
        NetworkAccess   = 1UL << 8,
        NetworkListen   = 1UL << 9,
        Network         = NetworkAccess | NetworkListen,

        // UI (bits 16-23)
        Notification    = 1UL << 16,
        Clipboard       = 1UL << 17,
        Dialog          = 1UL << 18,
        Overlay         = 1UL << 19,
        UI              = Notification | Clipboard | Dialog | Overlay,

        // System (bits 24-31)
        ProcessLaunch   = 1UL << 24,
        ProcessKill     = 1UL << 25,
        SystemSettings  = 1UL << 26,
        SystemInfo      = 1UL << 27,
        System          = ProcessLaunch | ProcessKill | SystemSettings | SystemInfo,

        // IPC (bits 32-39)
        IpcSend         = 1UL << 32,
        IpcReceive      = 1UL << 33,
        IpcBroadcast    = 1UL << 34,
        Ipc             = IpcSend | IpcReceive | IpcBroadcast,

        // Hardware (bits 40-47)
        Camera          = 1UL << 40,
        Microphone      = 1UL << 41,
        Location        = 1UL << 42,
        Hardware        = Camera | Microphone | Location,

        // Special (bits 56-63)
        Admin           = 1UL << 56,
        Kernel          = 1UL << 57,

        // Common bundles
        Basic           = FileRead | Notification | IpcSend | IpcReceive,
        Standard        = Basic | FileWrite | Clipboard | Dialog | ProcessLaunch,
        Full            = ulong.MaxValue & ~(Admin | Kernel)
    }

    /// <summary>
    /// Immutable permission set with fast bitwise operations.
    /// </summary>
    public readonly struct PermissionSet : IEquatable<PermissionSet>
    {
        public readonly Permission Granted;
        public readonly Permission Denied;

        public PermissionSet(Permission granted, Permission denied = Permission.None)
        {
            Granted = granted;
            Denied = denied;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(Permission permission)
            => (Granted & permission) == permission && (Denied & permission) == Permission.None;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAny(Permission permission)
            => (Granted & permission) != Permission.None && (Denied & permission) != permission;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PermissionSet Grant(Permission permission)
            => new(Granted | permission, Denied & ~permission);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PermissionSet Deny(Permission permission)
            => new(Granted & ~permission, Denied | permission);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PermissionSet Revoke(Permission permission)
            => new(Granted & ~permission, Denied & ~permission);

        public static PermissionSet Empty => new(Permission.None);
        public static PermissionSet Default => new(Permission.Basic);

        public bool Equals(PermissionSet other)
            => Granted == other.Granted && Denied == other.Denied;

        public override bool Equals(object? obj)
            => obj is PermissionSet other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(Granted, Denied);

        public static bool operator ==(PermissionSet left, PermissionSet right) => left.Equals(right);
        public static bool operator !=(PermissionSet left, PermissionSet right) => !left.Equals(right);
    }

    /// <summary>
    /// Permission check result for API responses.
    /// </summary>
    public readonly struct PermissionResult
    {
        public readonly bool Allowed;
        public readonly Permission Missing;

        public PermissionResult(bool allowed, Permission missing = Permission.None)
        {
            Allowed = allowed;
            Missing = missing;
        }

        public static PermissionResult Success => new(true);
        public static PermissionResult Fail(Permission missing) => new(false, missing);
    }
}
