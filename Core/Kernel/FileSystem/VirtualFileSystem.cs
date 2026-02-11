using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace eStarter.Core.Kernel.FileSystem
{
    /// <summary>
    /// File operation result.
    /// </summary>
    public readonly struct FileResult
    {
        public readonly bool Success;
        public readonly string? Error;
        public readonly long BytesAffected;

        private FileResult(bool success, string? error, long bytes)
        {
            Success = success;
            Error = error;
            BytesAffected = bytes;
        }

        public static FileResult Ok(long bytes = 0) => new(true, null, bytes);
        public static FileResult Fail(string error) => new(false, error, 0);
    }

    /// <summary>
    /// File info for directory listings.
    /// </summary>
    public readonly struct VirtualFileInfo
    {
        public readonly string Name;
        public readonly string Path;
        public readonly bool IsDirectory;
        public readonly long Size;
        public readonly long ModifiedTicks;

        public VirtualFileInfo(string name, string path, bool isDir, long size, long modified)
        {
            Name = name;
            Path = path;
            IsDirectory = isDir;
            Size = size;
            ModifiedTicks = modified;
        }

        public DateTime ModifiedTime => new(ModifiedTicks, DateTimeKind.Utc);
    }

    /// <summary>
    /// Virtual File System with sandbox isolation per app.
    /// All paths are virtual and mapped to physical storage.
    /// </summary>
    public sealed class VirtualFileSystem : IDisposable
    {
        private readonly string _rootPath;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
        private readonly Kernel _kernel;
        private bool _disposed;

        // Buffer pool for efficient I/O
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;
        private const int DefaultBufferSize = 81920; // 80KB

        public VirtualFileSystem(Kernel kernel, string? rootPath = null)
        {
            _kernel = kernel;
            _rootPath = rootPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "eStarter", "vfs");

            EnsureDirectoryStructure();
        }

        #region Path Resolution

        /// <summary>
        /// Resolve virtual path to physical path with security validation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ResolvePath(VirtualPath vpath, string callerAppId)
        {
            if (!vpath.IsValid)
                return null;

            // Sandbox check: app can only access its own zone or shared
            if (!vpath.BelongsTo(callerAppId))
                return null;

            var zonePath = GetZonePath(vpath.Zone);
            var physicalPath = Path.Combine(zonePath, vpath.AppId, vpath.RelativePath);

            // Final security check: ensure resolved path is still under root
            var fullPath = Path.GetFullPath(physicalPath);
            if (!fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
                return null;

            return fullPath;
        }

        private string GetZonePath(PathZone zone)
        {
            return zone switch
            {
                PathZone.AppData => Path.Combine(_rootPath, "appdata"),
                PathZone.Cache => Path.Combine(_rootPath, "cache"),
                PathZone.Temp => Path.Combine(_rootPath, "temp"),
                PathZone.Shared => Path.Combine(_rootPath, "shared"),
                PathZone.System => Path.Combine(_rootPath, "system"),
                _ => _rootPath
            };
        }

        #endregion

        #region File Operations

        /// <summary>
        /// Read file contents.
        /// </summary>
        public async ValueTask<(FileResult Result, byte[]? Data)> ReadFileAsync(
            VirtualPath vpath, string callerAppId, CancellationToken ct = default)
        {
            var physicalPath = ResolvePath(vpath, callerAppId);
            if (physicalPath == null)
                return (FileResult.Fail("Access denied or invalid path"), null);

            if (!File.Exists(physicalPath))
                return (FileResult.Fail("File not found"), null);

            var lockObj = GetFileLock(physicalPath);
            await lockObj.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var data = await File.ReadAllBytesAsync(physicalPath, ct).ConfigureAwait(false);
                return (FileResult.Ok(data.Length), data);
            }
            catch (Exception ex)
            {
                return (FileResult.Fail(ex.Message), null);
            }
            finally
            {
                lockObj.Release();
            }
        }

        /// <summary>
        /// Read file as string (UTF-8).
        /// </summary>
        public async ValueTask<(FileResult Result, string? Content)> ReadTextAsync(
            VirtualPath vpath, string callerAppId, CancellationToken ct = default)
        {
            var (result, data) = await ReadFileAsync(vpath, callerAppId, ct).ConfigureAwait(false);
            if (!result.Success || data == null)
                return (result, null);

            return (result, Encoding.UTF8.GetString(data));
        }

        /// <summary>
        /// Write file contents.
        /// </summary>
        public async ValueTask<FileResult> WriteFileAsync(
            VirtualPath vpath, string callerAppId, ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            var physicalPath = ResolvePath(vpath, callerAppId);
            if (physicalPath == null)
                return FileResult.Fail("Access denied or invalid path");

            // System zone is read-only
            if (vpath.Zone == PathZone.System)
                return FileResult.Fail("System zone is read-only");

            var lockObj = GetFileLock(physicalPath);
            await lockObj.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(physicalPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllBytesAsync(physicalPath, data.ToArray(), ct).ConfigureAwait(false);
                return FileResult.Ok(data.Length);
            }
            catch (Exception ex)
            {
                return FileResult.Fail(ex.Message);
            }
            finally
            {
                lockObj.Release();
            }
        }

        /// <summary>
        /// Write text file (UTF-8).
        /// </summary>
        public ValueTask<FileResult> WriteTextAsync(
            VirtualPath vpath, string callerAppId, string content, CancellationToken ct = default)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            return WriteFileAsync(vpath, callerAppId, bytes, ct);
        }

        /// <summary>
        /// Delete file.
        /// </summary>
        public async ValueTask<FileResult> DeleteFileAsync(VirtualPath vpath, string callerAppId)
        {
            var physicalPath = ResolvePath(vpath, callerAppId);
            if (physicalPath == null)
                return FileResult.Fail("Access denied or invalid path");

            if (vpath.Zone == PathZone.System)
                return FileResult.Fail("System zone is read-only");

            var lockObj = GetFileLock(physicalPath);
            await lockObj.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(physicalPath))
                {
                    var size = new FileInfo(physicalPath).Length;
                    File.Delete(physicalPath);
                    return FileResult.Ok(size);
                }
                return FileResult.Fail("File not found");
            }
            catch (Exception ex)
            {
                return FileResult.Fail(ex.Message);
            }
            finally
            {
                lockObj.Release();
            }
        }

        /// <summary>
        /// Check if file exists.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool FileExists(VirtualPath vpath, string callerAppId)
        {
            var physicalPath = ResolvePath(vpath, callerAppId);
            return physicalPath != null && File.Exists(physicalPath);
        }

        /// <summary>
        /// Get file info.
        /// </summary>
        public VirtualFileInfo? GetFileInfo(VirtualPath vpath, string callerAppId)
        {
            var physicalPath = ResolvePath(vpath, callerAppId);
            if (physicalPath == null || !File.Exists(physicalPath))
                return null;

            var info = new FileInfo(physicalPath);
            return new VirtualFileInfo(
                info.Name,
                vpath.Raw,
                false,
                info.Length,
                info.LastWriteTimeUtc.Ticks);
        }

        #endregion

        #region Directory Operations

        /// <summary>
        /// Create directory.
        /// </summary>
        public FileResult CreateDirectory(VirtualPath vpath, string callerAppId)
        {
            var physicalPath = ResolvePath(vpath, callerAppId);
            if (physicalPath == null)
                return FileResult.Fail("Access denied or invalid path");

            if (vpath.Zone == PathZone.System)
                return FileResult.Fail("System zone is read-only");

            try
            {
                Directory.CreateDirectory(physicalPath);
                return FileResult.Ok();
            }
            catch (Exception ex)
            {
                return FileResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// List directory contents.
        /// </summary>
        public VirtualFileInfo[] ListDirectory(VirtualPath vpath, string callerAppId)
        {
            var physicalPath = ResolvePath(vpath, callerAppId);
            if (physicalPath == null || !Directory.Exists(physicalPath))
                return [];

            try
            {
                var entries = new DirectoryInfo(physicalPath).GetFileSystemInfos();
                var result = new VirtualFileInfo[entries.Length];

                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    var isDir = (entry.Attributes & FileAttributes.Directory) != 0;
                    var size = isDir ? 0 : ((FileInfo)entry).Length;
                    var childPath = vpath.Combine(entry.Name);

                    result[i] = new VirtualFileInfo(
                        entry.Name,
                        childPath.Raw,
                        isDir,
                        size,
                        entry.LastWriteTimeUtc.Ticks);
                }

                return result;
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Delete directory and all contents.
        /// </summary>
        public FileResult DeleteDirectory(VirtualPath vpath, string callerAppId, bool recursive = false)
        {
            var physicalPath = ResolvePath(vpath, callerAppId);
            if (physicalPath == null)
                return FileResult.Fail("Access denied or invalid path");

            if (vpath.Zone == PathZone.System)
                return FileResult.Fail("System zone is read-only");

            try
            {
                if (!Directory.Exists(physicalPath))
                    return FileResult.Fail("Directory not found");

                Directory.Delete(physicalPath, recursive);
                return FileResult.Ok();
            }
            catch (Exception ex)
            {
                return FileResult.Fail(ex.Message);
            }
        }

        #endregion

        #region App Sandbox Management

        /// <summary>
        /// Initialize sandbox for an app.
        /// </summary>
        public void InitializeAppSandbox(string appId)
        {
            var zones = new[] { PathZone.AppData, PathZone.Cache, PathZone.Temp };
            foreach (var zone in zones)
            {
                var path = Path.Combine(GetZonePath(zone), appId);
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Clear app's cache.
        /// </summary>
        public FileResult ClearAppCache(string appId)
        {
            var cachePath = Path.Combine(GetZonePath(PathZone.Cache), appId);
            return ClearDirectory(cachePath);
        }

        /// <summary>
        /// Clear app's temp files.
        /// </summary>
        public FileResult ClearAppTemp(string appId)
        {
            var tempPath = Path.Combine(GetZonePath(PathZone.Temp), appId);
            return ClearDirectory(tempPath);
        }

        /// <summary>
        /// Delete all app data (uninstall).
        /// </summary>
        public FileResult DeleteAppData(string appId)
        {
            long total = 0;
            var zones = new[] { PathZone.AppData, PathZone.Cache, PathZone.Temp };
            
            foreach (var zone in zones)
            {
                var path = Path.Combine(GetZonePath(zone), appId);
                if (Directory.Exists(path))
                {
                    try
                    {
                        var size = GetDirectorySize(path);
                        Directory.Delete(path, true);
                        total += size;
                    }
                    catch { }
                }
            }

            return FileResult.Ok(total);
        }

        /// <summary>
        /// Get app's storage usage.
        /// </summary>
        public (long AppData, long Cache, long Temp) GetAppStorageUsage(string appId)
        {
            return (
                GetDirectorySize(Path.Combine(GetZonePath(PathZone.AppData), appId)),
                GetDirectorySize(Path.Combine(GetZonePath(PathZone.Cache), appId)),
                GetDirectorySize(Path.Combine(GetZonePath(PathZone.Temp), appId))
            );
        }

        #endregion

        #region Helpers

        private SemaphoreSlim GetFileLock(string path)
        {
            return _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        }

        private static FileResult ClearDirectory(string path)
        {
            if (!Directory.Exists(path))
                return FileResult.Ok();

            try
            {
                long total = 0;
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    total += new FileInfo(file).Length;
                    File.Delete(file);
                }
                foreach (var dir in Directory.GetDirectories(path))
                {
                    Directory.Delete(dir, true);
                }
                return FileResult.Ok(total);
            }
            catch (Exception ex)
            {
                return FileResult.Fail(ex.Message);
            }
        }

        private static long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path))
                return 0;

            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    size += new FileInfo(file).Length;
                }
            }
            catch { }
            return size;
        }

        private void EnsureDirectoryStructure()
        {
            var zones = new[] { PathZone.AppData, PathZone.Cache, PathZone.Temp, PathZone.Shared, PathZone.System };
            foreach (var zone in zones)
            {
                Directory.CreateDirectory(GetZonePath(zone));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var lockObj in _fileLocks.Values)
            {
                lockObj.Dispose();
            }
            _fileLocks.Clear();
        }

        #endregion
    }
}
