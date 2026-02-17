using System;
using System.Runtime.CompilerServices;

namespace eStarter.Core.Kernel.FileSystem
{
    /// <summary>
    /// Virtual path zones in the file system.
    /// </summary>
    public enum PathZone : byte
    {
        /// <summary>App's private sandbox storage.</summary>
        AppData = 0,
        /// <summary>App's cache (can be cleared).</summary>
        Cache = 1,
        /// <summary>App's temporary files.</summary>
        Temp = 2,
        /// <summary>Shared storage accessible by all apps with permission.</summary>
        Shared = 3,
        /// <summary>System files (read-only for apps).</summary>
        System = 4,
        /// <summary>Invalid or unknown zone.</summary>
        Invalid = 255
    }

    /// <summary>
    /// Represents a normalized virtual path with O(1) zone detection.
    /// Format: /{zone}/{appId}/{relative/path}
    /// Example: /appdata/com.example.app/settings.json
    /// </summary>
    public readonly struct VirtualPath : IEquatable<VirtualPath>
    {
        public readonly string Raw;
        public readonly PathZone Zone;
        public readonly string AppId;
        public readonly string RelativePath;
        public readonly bool IsValid;

        private VirtualPath(string raw, PathZone zone, string appId, string relativePath, bool isValid)
        {
            Raw = raw;
            Zone = zone;
            AppId = appId;
            RelativePath = relativePath;
            IsValid = isValid;
        }

        /// <summary>
        /// Parse and normalize a virtual path.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VirtualPath Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Invalid(path);

            // Normalize separators
            var normalized = path.Replace('\\', '/').TrimStart('/');
            var parts = normalized.Split('/', 3, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return Invalid(path);

            var zone = ParseZone(parts[0]);
            if (zone == PathZone.Invalid)
                return Invalid(path);

            var appId = parts[1];
            var relative = parts.Length > 2 ? parts[2] : string.Empty;

            // Security: prevent directory traversal
            if (ContainsTraversal(relative))
                return Invalid(path);

            return new VirtualPath($"/{parts[0]}/{appId}/{relative}", zone, appId, relative, true);
        }

        /// <summary>
        /// Create a path for an app's zone.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VirtualPath Create(PathZone zone, string appId, string relativePath = "")
        {
            if (string.IsNullOrEmpty(appId) || zone == PathZone.Invalid)
                return Invalid(string.Empty);

            // Security: prevent directory traversal
            if (ContainsTraversal(relativePath))
                return Invalid(relativePath);

            var zoneName = GetZoneName(zone);
            var normalized = relativePath.Replace('\\', '/').TrimStart('/');
            return new VirtualPath($"/{zoneName}/{appId}/{normalized}", zone, appId, normalized, true);
        }

        /// <summary>
        /// Get child path by appending a segment.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VirtualPath Combine(string segment)
        {
            if (!IsValid || string.IsNullOrEmpty(segment))
                return this;

            if (ContainsTraversal(segment))
                return Invalid(segment);

            var newRelative = string.IsNullOrEmpty(RelativePath)
                ? segment
                : $"{RelativePath}/{segment}";

            return new VirtualPath($"/{GetZoneName(Zone)}/{AppId}/{newRelative}", Zone, AppId, newRelative, true);
        }

        /// <summary>
        /// Check if this path is within another app's sandbox.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool BelongsTo(string appId)
            => IsValid && (Zone == PathZone.Shared || string.Equals(AppId, appId, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Check if path requires specific permission.
        /// </summary>
        public Permission RequiredPermission(bool write)
        {
            return Zone switch
            {
                PathZone.System => write ? Permission.Admin : Permission.FileRead,
                PathZone.Shared => write ? Permission.FileWrite : Permission.FileRead,
                _ => write ? Permission.FileWrite : Permission.FileRead
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PathZone ParseZone(string zone)
        {
            return zone.ToLowerInvariant() switch
            {
                "appdata" => PathZone.AppData,
                "cache" => PathZone.Cache,
                "temp" => PathZone.Temp,
                "shared" => PathZone.Shared,
                "system" => PathZone.System,
                _ => PathZone.Invalid
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetZoneName(PathZone zone)
        {
            return zone switch
            {
                PathZone.AppData => "appdata",
                PathZone.Cache => "cache",
                PathZone.Temp => "temp",
                PathZone.Shared => "shared",
                PathZone.System => "system",
                _ => "invalid"
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsTraversal(string path)
        {
            // Fast check for directory traversal attempts
            return path.Contains("..") || 
                   path.Contains("./") || 
                   path.StartsWith('.') ||
                   path.Contains("//");
        }

        private static VirtualPath Invalid(string raw)
            => new(raw, PathZone.Invalid, string.Empty, string.Empty, false);

        public bool Equals(VirtualPath other)
            => string.Equals(Raw, other.Raw, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj)
            => obj is VirtualPath other && Equals(other);

        public override int GetHashCode()
            => StringComparer.OrdinalIgnoreCase.GetHashCode(Raw);

        public override string ToString() => Raw;

        public static bool operator ==(VirtualPath left, VirtualPath right) => left.Equals(right);
        public static bool operator !=(VirtualPath left, VirtualPath right) => !left.Equals(right);

        public static implicit operator string(VirtualPath path) => path.Raw;
    }
}
