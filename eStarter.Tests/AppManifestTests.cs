using System.Text.Json;
using eStarter.Core.Hosting;
using eStarter.Models;

namespace eStarter.Tests;

public class AppManifestTests
{
    [Fact]
    public void Deserialize_FullManifest_AllFieldsPopulated()
    {
        var json = """
        {
            "id": "com.example.test",
            "name": "Test App",
            "description": "A test app",
            "publisher": "Test Publisher",
            "version": "2.0.0",
            "category": "Tools",
            "exePath": "test.exe",
            "entry": "main.exe",
            "runtime": "Wasm",
            "sandboxed": true,
            "memoryLimitMb": 128,
            "maxProcesses": 2,
            "cpuQuota": 75,
            "networkAllowed": true,
            "maxRuntimeSeconds": 60,
            "permissions": ["FileRead", "Notification"]
        }
        """;

        var manifest = JsonSerializer.Deserialize<AppManifest>(json)!;

        Assert.Equal("com.example.test", manifest.Id);
        Assert.Equal("Test App", manifest.Name);
        Assert.Equal("A test app", manifest.Description);
        Assert.Equal("Test Publisher", manifest.Publisher);
        Assert.Equal("2.0.0", manifest.Version);
        Assert.Equal("Tools", manifest.Category);
        Assert.Equal("test.exe", manifest.ExePath);
        Assert.Equal("main.exe", manifest.Entry);
        Assert.Equal(AppRuntime.Wasm, manifest.Runtime);
        Assert.True(manifest.Sandboxed);
        Assert.Equal(128, manifest.MemoryLimitMb);
        Assert.Equal(2, manifest.MaxProcesses);
        Assert.Equal(75, manifest.CpuQuota);
        Assert.True(manifest.NetworkAllowed);
        Assert.Equal(60, manifest.MaxRuntimeSeconds);
        Assert.Equal(2, manifest.Permissions!.Length);
    }

    [Fact]
    public void Deserialize_MinimalManifest_DefaultsApplied()
    {
        var json = """{ "id": "minimal" }""";

        var manifest = JsonSerializer.Deserialize<AppManifest>(json)!;

        Assert.Equal("minimal", manifest.Id);
        Assert.Equal(AppRuntime.Native, manifest.Runtime);
        Assert.Null(manifest.Entry);
        Assert.Equal(0, manifest.MemoryLimitMb);
        Assert.Equal(0, manifest.MaxProcesses);
        Assert.Equal(0, manifest.CpuQuota);
        Assert.False(manifest.NetworkAllowed);
        Assert.Equal(0, manifest.MaxRuntimeSeconds);
        Assert.True(manifest.Sandboxed);
    }

    [Fact]
    public void Roundtrip_SerializeDeserialize_Preserves()
    {
        var original = new AppManifest
        {
            Id = "roundtrip",
            Runtime = AppRuntime.Wasm,
            Entry = "app.wasm",
            MemoryLimitMb = 64,
            CpuQuota = 30
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppManifest>(json)!;

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Runtime, restored.Runtime);
        Assert.Equal(original.Entry, restored.Entry);
        Assert.Equal(original.MemoryLimitMb, restored.MemoryLimitMb);
        Assert.Equal(original.CpuQuota, restored.CpuQuota);
    }

    [Fact]
    public void BackwardsCompatibility_NoRuntimeField_DefaultsToNative()
    {
        var json = """
        {
            "id": "legacy.app",
            "exePath": "old.exe"
        }
        """;

        var manifest = JsonSerializer.Deserialize<AppManifest>(json)!;

        Assert.Equal(AppRuntime.Native, manifest.Runtime);
        Assert.Null(manifest.Entry);
        Assert.Equal("old.exe", manifest.ExePath);
    }
}
