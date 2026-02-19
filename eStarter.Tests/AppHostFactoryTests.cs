using eStarter.Core.Hosting;
using eStarter.Models;

namespace eStarter.Tests;

public class AppHostFactoryTests
{
    private static eStarter.Core.Kernel.Kernel Kernel => eStarter.Core.Kernel.Kernel.Instance;

    [Fact]
    public void Create_NativeRuntime_ReturnsProcessHost()
    {
        var manifest = new AppManifest
        {
            Id = "test.native",
            Runtime = AppRuntime.Native,
            Entry = "app.exe"
        };

        var host = AppHostFactory.Create(manifest, @"C:\fake\dir", Kernel);

        Assert.IsType<ProcessHost>(host);
        Assert.Equal("test.native", host.AppId);
        Assert.Equal(AppHostState.Created, host.State);
    }

    [Fact]
    public void Create_WasmRuntime_ReturnsWasmAppHost()
    {
        var manifest = new AppManifest
        {
            Id = "test.wasm",
            Runtime = AppRuntime.Wasm,
            Entry = "app.wasm"
        };

        var host = AppHostFactory.Create(manifest, @"C:\fake\dir", Kernel);

        Assert.IsType<WasmAppHost>(host);
        Assert.Equal("test.wasm", host.AppId);
        Assert.Equal(AppHostState.Created, host.State);
    }

    [Fact]
    public void Create_DotnetRuntime_ThrowsNotSupported()
    {
        var manifest = new AppManifest
        {
            Id = "test.dotnet",
            Runtime = AppRuntime.Dotnet,
            Entry = "app.dll"
        };

        Assert.Throws<NotSupportedException>(() =>
            AppHostFactory.Create(manifest, @"C:\fake\dir", Kernel));
    }

    [Fact]
    public void Create_NativeNoEntry_ThrowsInvalidOperation()
    {
        var manifest = new AppManifest
        {
            Id = "test.noentry",
            Runtime = AppRuntime.Native
            // No Entry, no ExePath
        };

        Assert.Throws<InvalidOperationException>(() =>
            AppHostFactory.Create(manifest, @"C:\fake\dir", Kernel));
    }

    [Fact]
    public void Create_WasmNoEntry_ThrowsInvalidOperation()
    {
        var manifest = new AppManifest
        {
            Id = "test.wasmnoentry",
            Runtime = AppRuntime.Wasm
            // No Entry
        };

        Assert.Throws<InvalidOperationException>(() =>
            AppHostFactory.Create(manifest, @"C:\fake\dir", Kernel));
    }

    [Fact]
    public void Create_NullManifest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AppHostFactory.Create(null!, @"C:\fake\dir", Kernel));
    }

    [Fact]
    public void Create_NullDirectory_ThrowsArgumentNull()
    {
        var manifest = new AppManifest { Id = "test", Entry = "a.exe" };

        Assert.Throws<ArgumentNullException>(() =>
            AppHostFactory.Create(manifest, null!, Kernel));
    }

    [Fact]
    public void Create_NullKernel_ThrowsArgumentNull()
    {
        var manifest = new AppManifest { Id = "test", Entry = "a.exe" };

        Assert.Throws<ArgumentNullException>(() =>
            AppHostFactory.Create(manifest, @"C:\fake", null!));
    }

    [Fact]
    public void Create_FallsBackToExePath_WhenEntryIsNull()
    {
        var manifest = new AppManifest
        {
            Id = "test.fallback",
            Runtime = AppRuntime.Native,
            ExePath = "legacy.exe"
            // Entry is null → should use ExePath
        };

        var host = AppHostFactory.Create(manifest, @"C:\fake\dir", Kernel);

        Assert.IsType<ProcessHost>(host);
    }

    [Fact]
    public void Create_UsesExplicitPolicy_WhenProvided()
    {
        var manifest = new AppManifest
        {
            Id = "test.policy",
            Runtime = AppRuntime.Native,
            Entry = "app.exe",
            MemoryLimitMb = 512
        };

        var customPolicy = SandboxPolicy.Unrestricted;
        var host = AppHostFactory.Create(manifest, @"C:\fake", Kernel, customPolicy);

        Assert.IsType<ProcessHost>(host);
    }
}
