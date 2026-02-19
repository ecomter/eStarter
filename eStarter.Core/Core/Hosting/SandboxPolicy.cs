using System;
using eStarter.Models;

namespace eStarter.Core.Hosting
{
    /// <summary>
    /// Immutable per-app resource-limit policy derived from <see cref="AppManifest"/>.
    /// All limit values use 0 to mean "unlimited".
    /// </summary>
    public sealed class SandboxPolicy
    {
        // ── Memory ────────────────────────────────────────────────────────

        /// <summary>Maximum working-set memory in bytes. 0 = unlimited.</summary>
        public long MemoryLimitBytes { get; }

        // ── Processes ─────────────────────────────────────────────────────

        /// <summary>Maximum number of active child processes. 0 = unlimited.</summary>
        public int MaxProcesses { get; }

        // ── CPU ───────────────────────────────────────────────────────────

        /// <summary>CPU quota as a percentage (1–100). 0 = unlimited.</summary>
        public int CpuQuotaPercent { get; }

        // ── Network ───────────────────────────────────────────────────────

        /// <summary>Whether outbound network access is permitted.</summary>
        public bool NetworkAllowed { get; }

        // ── Lifetime ──────────────────────────────────────────────────────

        /// <summary>Maximum wall-clock lifetime. <see cref="TimeSpan.Zero"/> = unlimited.</summary>
        public TimeSpan MaxRuntime { get; }

        // ── Runtime ───────────────────────────────────────────────────────

        /// <summary>Runtime type requested by the manifest.</summary>
        public AppRuntime Runtime { get; }

        // ── Construction ──────────────────────────────────────────────────

        private SandboxPolicy(
            long memoryLimitBytes,
            int maxProcesses,
            int cpuQuotaPercent,
            bool networkAllowed,
            TimeSpan maxRuntime,
            AppRuntime runtime)
        {
            MemoryLimitBytes = memoryLimitBytes;
            MaxProcesses = maxProcesses;
            CpuQuotaPercent = Math.Clamp(cpuQuotaPercent, 0, 100);
            NetworkAllowed = networkAllowed;
            MaxRuntime = maxRuntime;
            Runtime = runtime;
        }

        /// <summary>
        /// Build a <see cref="SandboxPolicy"/> from the values declared in <paramref name="manifest"/>.
        /// </summary>
        public static SandboxPolicy FromManifest(AppManifest manifest)
        {
            ArgumentNullException.ThrowIfNull(manifest);

            long memBytes = manifest.MemoryLimitMb > 0
                ? (long)manifest.MemoryLimitMb * 1024 * 1024
                : 0L;

            var maxRuntime = manifest.MaxRuntimeSeconds > 0
                ? TimeSpan.FromSeconds(manifest.MaxRuntimeSeconds)
                : TimeSpan.Zero;

            return new SandboxPolicy(
                memoryLimitBytes: memBytes,
                maxProcesses: manifest.MaxProcesses,
                cpuQuotaPercent: manifest.CpuQuota,
                networkAllowed: manifest.NetworkAllowed,
                maxRuntime: maxRuntime,
                runtime: manifest.Runtime);
        }

        /// <summary>Unrestricted policy used for trusted/system apps.</summary>
        public static SandboxPolicy Unrestricted { get; } = new SandboxPolicy(
            memoryLimitBytes: 0,
            maxProcesses: 0,
            cpuQuotaPercent: 0,
            networkAllowed: true,
            maxRuntime: TimeSpan.Zero,
            runtime: AppRuntime.Native);

        /// <inheritdoc />
        public override string ToString() =>
            $"[SandboxPolicy rt={Runtime} mem={MemoryLimitBytes / 1024 / 1024}MB " +
            $"procs={MaxProcesses} cpu={CpuQuotaPercent}% net={NetworkAllowed} " +
            $"maxT={MaxRuntime}]";
    }
}
