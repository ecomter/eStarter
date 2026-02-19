using eStarter.Core.Hosting;
using eStarter.Core.Kernel;

namespace eStarter.Tests;

public class ProcessHostTests
{
    private static Kernel Kernel => Kernel.Instance;

    [Fact]
    public void Constructor_SetsInitialState()
    {
        var host = new ProcessHost(
            "test.app", @"C:\fake\app.exe", @"C:\fake", Kernel,
            SandboxPolicy.Unrestricted);

        Assert.Equal("test.app", host.AppId);
        Assert.Equal(AppHostState.Created, host.State);
        Assert.Equal(-1, host.ProcessId);
    }

    [Fact]
    public void Constructor_NullAppId_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProcessHost(null!, @"C:\f.exe", @"C:\", Kernel, SandboxPolicy.Unrestricted));
    }

    [Fact]
    public void Constructor_EmptyAppId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ProcessHost("", @"C:\f.exe", @"C:\", Kernel, SandboxPolicy.Unrestricted));
    }

    [Fact]
    public void Constructor_NullExePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProcessHost("app", null!, @"C:\", Kernel, SandboxPolicy.Unrestricted));
    }

    [Fact]
    public async Task StartAsync_InvalidExe_TransitionsToFaulted()
    {
        var host = new ProcessHost(
            "test.bad", @"C:\nonexistent\app.exe", @"C:\nonexistent",
            Kernel, SandboxPolicy.Unrestricted);

        AppHostExitedEventArgs? exitArgs = null;
        host.Exited += (_, e) => exitArgs = e;

        await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());

        Assert.Equal(AppHostState.Faulted, host.State);
        Assert.NotNull(exitArgs);
        Assert.Equal("test.bad", exitArgs!.AppId);
        Assert.NotNull(exitArgs.Exception);
    }

    [Fact]
    public async Task StopAsync_WhenStopped_IsNoOp()
    {
        var host = new ProcessHost(
            "test.noop", @"C:\fake.exe", @"C:\", Kernel, SandboxPolicy.Unrestricted);

        // Force into Faulted to skip real start
        try { await host.StartAsync(); } catch { }

        // Second stop should not throw
        await host.StopAsync();
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_Safe()
    {
        var host = new ProcessHost(
            "test.dispose", @"C:\fake.exe", @"C:\", Kernel, SandboxPolicy.Unrestricted);

        await host.DisposeAsync();
        await host.DisposeAsync(); // second call is no-op
    }

    [Fact]
    public async Task StartAsync_AfterDispose_Throws()
    {
        var host = new ProcessHost(
            "test.afterdispose", @"C:\fake.exe", @"C:\", Kernel, SandboxPolicy.Unrestricted);

        await host.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.StartAsync());
    }

    [Fact]
    public async Task StartAsync_WithRealProcess_RunsAndExits()
    {
        // Use dotnet --version as a quick real process that exits immediately.
        var dotnetPath = "dotnet";

        var host = new ProcessHost(
            "test.real", dotnetPath, Directory.GetCurrentDirectory(),
            Kernel, SandboxPolicy.Unrestricted, arguments: "--version");

        var exited = new TaskCompletionSource<AppHostExitedEventArgs>();
        host.Exited += (_, e) => exited.TrySetResult(e);

        await host.StartAsync();

        // Wait for the process to exit (dotnet --version is fast).
        var args = await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("test.real", args.AppId);
        Assert.Equal(0, args.ExitCode);
        Assert.Null(args.Exception);
    }
}
