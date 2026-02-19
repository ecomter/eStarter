using eStarter.Core.Hosting;
using eStarter.Models;

namespace eStarter.Tests;

public class SandboxPolicyTests
{
    [Fact]
    public void FromManifest_DefaultValues_AllUnlimited()
    {
        var manifest = new AppManifest { Id = "test.app" };

        var policy = SandboxPolicy.FromManifest(manifest);

        Assert.Equal(0L, policy.MemoryLimitBytes);
        Assert.Equal(0, policy.MaxProcesses);
        Assert.Equal(0, policy.CpuQuotaPercent);
        Assert.False(policy.NetworkAllowed);
        Assert.Equal(TimeSpan.Zero, policy.MaxRuntime);
        Assert.Equal(AppRuntime.Native, policy.Runtime);
    }

    [Fact]
    public void FromManifest_WithLimits_ConvertsCorrectly()
    {
        var manifest = new AppManifest
        {
            Id = "test.limited",
            MemoryLimitMb = 256,
            MaxProcesses = 4,
            CpuQuota = 50,
            NetworkAllowed = true,
            MaxRuntimeSeconds = 120,
            Runtime = AppRuntime.Wasm
        };

        var policy = SandboxPolicy.FromManifest(manifest);

        Assert.Equal(256L * 1024 * 1024, policy.MemoryLimitBytes);
        Assert.Equal(4, policy.MaxProcesses);
        Assert.Equal(50, policy.CpuQuotaPercent);
        Assert.True(policy.NetworkAllowed);
        Assert.Equal(TimeSpan.FromSeconds(120), policy.MaxRuntime);
        Assert.Equal(AppRuntime.Wasm, policy.Runtime);
    }

    [Fact]
    public void FromManifest_CpuQuotaClamped()
    {
        var manifest = new AppManifest
        {
            Id = "test.clamp",
            CpuQuota = 200
        };

        var policy = SandboxPolicy.FromManifest(manifest);

        Assert.Equal(100, policy.CpuQuotaPercent);
    }

    [Fact]
    public void FromManifest_NegativeCpuQuotaClamped()
    {
        var manifest = new AppManifest
        {
            Id = "test.neg",
            CpuQuota = -10
        };

        var policy = SandboxPolicy.FromManifest(manifest);

        Assert.Equal(0, policy.CpuQuotaPercent);
    }

    [Fact]
    public void FromManifest_NullManifest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SandboxPolicy.FromManifest(null!));
    }

    [Fact]
    public void Unrestricted_IsFullyOpen()
    {
        var u = SandboxPolicy.Unrestricted;

        Assert.Equal(0L, u.MemoryLimitBytes);
        Assert.Equal(0, u.MaxProcesses);
        Assert.Equal(0, u.CpuQuotaPercent);
        Assert.True(u.NetworkAllowed);
        Assert.Equal(TimeSpan.Zero, u.MaxRuntime);
        Assert.Equal(AppRuntime.Native, u.Runtime);
    }

    [Fact]
    public void ManifestSandboxPolicyProperty_ReturnsSameResult()
    {
        var manifest = new AppManifest
        {
            Id = "test.prop",
            MemoryLimitMb = 128,
            Runtime = AppRuntime.Wasm
        };

        var p1 = manifest.SandboxPolicy;
        var p2 = SandboxPolicy.FromManifest(manifest);

        Assert.Equal(p1.MemoryLimitBytes, p2.MemoryLimitBytes);
        Assert.Equal(p1.Runtime, p2.Runtime);
    }

    [Fact]
    public void ToString_ContainsKeyInfo()
    {
        var manifest = new AppManifest
        {
            Id = "test.str",
            MemoryLimitMb = 64,
            CpuQuota = 25,
            Runtime = AppRuntime.Native
        };

        var s = SandboxPolicy.FromManifest(manifest).ToString();

        Assert.Contains("Native", s);
        Assert.Contains("64MB", s);
        Assert.Contains("25%", s);
    }
}
