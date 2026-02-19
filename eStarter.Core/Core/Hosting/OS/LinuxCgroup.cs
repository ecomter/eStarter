using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace eStarter.Core.Hosting
{
    /// <summary>
    /// Uses cgroups v2 to enforce memory, PID, and CPU limits for a child process on Linux.
    /// Creates a per-app cgroup under <c>/sys/fs/cgroup/estarter/{appId}</c>.
    /// </summary>
    [SupportedOSPlatform("linux")]
    internal sealed class LinuxCgroup : IDisposable
    {
        private const string CgroupBase = "/sys/fs/cgroup/estarter";
        private readonly string _cgroupPath;
        private bool _disposed;

        private LinuxCgroup(string cgroupPath) => _cgroupPath = cgroupPath;

        /// <summary>
        /// Create a cgroup, write limits, and assign <paramref name="pid"/> to it.
        /// </summary>
        public static LinuxCgroup Apply(SandboxPolicy policy, int pid)
        {
            var appSafe = SanitiseName(policy.Runtime.ToString() + "_" + pid);
            var path = Path.Combine(CgroupBase, appSafe);

            Directory.CreateDirectory(path);

            var cg = new LinuxCgroup(path);
            try
            {
                cg.WriteLimits(policy);
                cg.AddProcess(pid);
                return cg;
            }
            catch
            {
                cg.Dispose();
                throw;
            }
        }

        // ── Configuration ─────────────────────────────────────────────────

        private void WriteLimits(SandboxPolicy policy)
        {
            if (policy.MemoryLimitBytes > 0)
                WriteControlFile("memory.max", policy.MemoryLimitBytes.ToString());

            if (policy.MaxProcesses > 0)
                WriteControlFile("pids.max", policy.MaxProcesses.ToString());

            if (policy.CpuQuotaPercent > 0)
            {
                // cpu.max format: "$QUOTA $PERIOD"
                // e.g. 50% of one core @ 100ms period = "50000 100000"
                const int periodUs = 100_000;
                int quotaUs = policy.CpuQuotaPercent * periodUs / 100;
                WriteControlFile("cpu.max", $"{quotaUs} {periodUs}");
            }
        }

        private void AddProcess(int pid)
        {
            WriteControlFile("cgroup.procs", pid.ToString());
        }

        // ── IDisposable ───────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (Directory.Exists(_cgroupPath))
                    Directory.Delete(_cgroupPath, recursive: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LinuxCgroup] Failed to remove cgroup '{_cgroupPath}': {ex.Message}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void WriteControlFile(string name, string value)
        {
            var file = Path.Combine(_cgroupPath, name);
            File.WriteAllText(file, value);
        }

        private static string SanitiseName(string raw)
        {
            // Keep alphanumeric, dash, underscore only.
            var buf = new char[raw.Length];
            int len = 0;
            foreach (var c in raw)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    buf[len++] = c;
            }
            return len > 0 ? new string(buf, 0, len) : "unknown";
        }
    }
}
