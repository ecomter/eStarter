using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace eStarter.Core.Kernel.FileSystem
{
    /// <summary>
    /// Registers file system API handlers with the kernel.
    /// </summary>
    public sealed class FileSystemApiHandler
    {
        private readonly VirtualFileSystem _fs;
        private readonly Kernel _kernel;

        public FileSystemApiHandler(Kernel kernel, VirtualFileSystem fs)
        {
            _kernel = kernel;
            _fs = fs;
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            _kernel.RegisterHandler(ApiCommand.ReadFile, HandleReadFile);
            _kernel.RegisterHandler(ApiCommand.WriteFile, HandleWriteFile);
            _kernel.RegisterHandler(ApiCommand.DeleteFile, HandleDeleteFile);
            _kernel.RegisterHandler(ApiCommand.ListDirectory, HandleListDirectory);
            _kernel.RegisterHandler(ApiCommand.CreateDirectory, HandleCreateDirectory);
            _kernel.RegisterHandler(ApiCommand.FileExists, HandleFileExists);
            _kernel.RegisterHandler(ApiCommand.GetFileInfo, HandleGetFileInfo);
        }

        private async ValueTask<ApiResponse> HandleReadFile(ProcessInfo caller, ApiRequest request)
        {
            var path = GetPathParam(request);
            if (path == null)
                return ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Missing 'path' parameter");

            var vpath = VirtualPath.Parse(path);
            if (!vpath.IsValid)
                return ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Invalid path format");

            var (result, data) = await _fs.ReadFileAsync(vpath, caller.AppId).ConfigureAwait(false);
            
            if (!result.Success)
                return ApiResponse.Fail(request.RequestId, ApiStatus.Error, result.Error ?? "Read failed");

            // Return as base64 for binary safety
            var base64 = data != null ? Convert.ToBase64String(data) : string.Empty;
            var json = JsonSerializer.SerializeToElement(new
            {
                path = vpath.Raw,
                size = result.BytesAffected,
                data = base64
            });

            return ApiResponse.Success(request.RequestId, json);
        }

        private async ValueTask<ApiResponse> HandleWriteFile(ProcessInfo caller, ApiRequest request)
        {
            var path = GetPathParam(request);
            if (path == null)
                return ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Missing 'path' parameter");

            var vpath = VirtualPath.Parse(path);
            if (!vpath.IsValid)
                return ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Invalid path format");

            // Get data (base64 encoded)
            byte[] data;
            if (request.Data?.TryGetProperty("data", out var dataEl) == true)
            {
                var dataStr = dataEl.GetString();
                if (string.IsNullOrEmpty(dataStr))
                    data = [];
                else
                {
                    try
                    {
                        data = Convert.FromBase64String(dataStr);
                    }
                    catch
                    {
                        // Treat as UTF-8 text if not valid base64
                        data = Encoding.UTF8.GetBytes(dataStr);
                    }
                }
            }
            else if (request.Data?.TryGetProperty("text", out var textEl) == true)
            {
                data = Encoding.UTF8.GetBytes(textEl.GetString() ?? string.Empty);
            }
            else
            {
                return ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Missing 'data' or 'text' parameter");
            }

            var result = await _fs.WriteFileAsync(vpath, caller.AppId, data).ConfigureAwait(false);

            if (!result.Success)
                return ApiResponse.Fail(request.RequestId, ApiStatus.Error, result.Error ?? "Write failed");

            var json = JsonSerializer.SerializeToElement(new
            {
                path = vpath.Raw,
                written = result.BytesAffected
            });

            return ApiResponse.Success(request.RequestId, json);
        }

        private async ValueTask<ApiResponse> HandleDeleteFile(ProcessInfo caller, ApiRequest request)
        {
            var path = GetPathParam(request);
            if (path == null)
                return ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Missing 'path' parameter");

            var vpath = VirtualPath.Parse(path);
            if (!vpath.IsValid)
                return ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Invalid path format");

            var result = await _fs.DeleteFileAsync(vpath, caller.AppId).ConfigureAwait(false);

            if (!result.Success)
                return ApiResponse.Fail(request.RequestId, ApiStatus.NotFound, result.Error ?? "Delete failed");

            return ApiResponse.Success(request.RequestId, JsonSerializer.SerializeToElement(new { deleted = true }));
        }

        private ValueTask<ApiResponse> HandleListDirectory(ProcessInfo caller, ApiRequest request)
        {
            var path = GetPathParam(request);
            if (path == null)
                return new(ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Missing 'path' parameter"));

            var vpath = VirtualPath.Parse(path);
            if (!vpath.IsValid)
                return new(ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Invalid path format"));

            var entries = _fs.ListDirectory(vpath, caller.AppId);
            
            var items = new object[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                items[i] = new
                {
                    name = entries[i].Name,
                    path = entries[i].Path,
                    isDir = entries[i].IsDirectory,
                    size = entries[i].Size,
                    modified = entries[i].ModifiedTicks
                };
            }

            var json = JsonSerializer.SerializeToElement(new { path = vpath.Raw, items });
            return new(ApiResponse.Success(request.RequestId, json));
        }

        private ValueTask<ApiResponse> HandleCreateDirectory(ProcessInfo caller, ApiRequest request)
        {
            var path = GetPathParam(request);
            if (path == null)
                return new(ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Missing 'path' parameter"));

            var vpath = VirtualPath.Parse(path);
            if (!vpath.IsValid)
                return new(ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Invalid path format"));

            var result = _fs.CreateDirectory(vpath, caller.AppId);

            if (!result.Success)
                return new(ApiResponse.Fail(request.RequestId, ApiStatus.Error, result.Error ?? "Create failed"));

            return new(ApiResponse.Success(request.RequestId, JsonSerializer.SerializeToElement(new { created = true })));
        }

        private ValueTask<ApiResponse> HandleFileExists(ProcessInfo caller, ApiRequest request)
        {
            var path = GetPathParam(request);
            if (path == null)
                return new(ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Missing 'path' parameter"));

            var vpath = VirtualPath.Parse(path);
            if (!vpath.IsValid)
                return new(ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Invalid path format"));

            var exists = _fs.FileExists(vpath, caller.AppId);
            var json = JsonSerializer.SerializeToElement(new { path = vpath.Raw, exists });
            
            return new(ApiResponse.Success(request.RequestId, json));
        }

        private ValueTask<ApiResponse> HandleGetFileInfo(ProcessInfo caller, ApiRequest request)
        {
            var path = GetPathParam(request);
            if (path == null)
                return new(ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Missing 'path' parameter"));

            var vpath = VirtualPath.Parse(path);
            if (!vpath.IsValid)
                return new(ApiResponse.Fail(request.RequestId, ApiStatus.InvalidRequest, "Invalid path format"));

            var info = _fs.GetFileInfo(vpath, caller.AppId);
            if (info == null)
                return new(ApiResponse.Fail(request.RequestId, ApiStatus.NotFound, "File not found"));

            var fi = info.Value;
            var json = JsonSerializer.SerializeToElement(new
            {
                name = fi.Name,
                path = fi.Path,
                size = fi.Size,
                modified = fi.ModifiedTicks
            });

            return new(ApiResponse.Success(request.RequestId, json));
        }

        private static string? GetPathParam(ApiRequest request)
        {
            if (request.Data?.TryGetProperty("path", out var pathEl) == true)
                return pathEl.GetString();
            return null;
        }
    }
}
