using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace eStarter.Core.Hosting
{
    /// <summary>
    /// Factory that creates the platform-appropriate OS resource limiter.
    /// Returns <c>null</c> when no limits are required or the platform is unsupported.
    /// </summary>
    internal static class OsResourceLimitFactory
    {
        /// <summary>
        /// Try to apply OS-level resource limits for <paramref name="pid"/>.
        /// Returns a disposable that releases the resources, or <c>null</c> on failure / no-op.
        /// </summary>
        public static IDisposable? TryCreate(SandboxPolicy policy, int pid)
        {
            if (policy.MemoryLimitBytes <= 0 && policy.MaxProcesses <= 0 && policy.CpuQuotaPercent <= 0)
                return null; // Nothing to enforce.

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return WindowsJobObject.Apply(policy, pid);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return LinuxCgroup.Apply(policy, pid);

                Debug.WriteLine("[OsResourceLimit] Platform not supported for OS-level limits.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OsResourceLimit] Failed to apply OS limits: {ex.Message}");
                return null;
            }
        }
    }
}
