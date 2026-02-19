using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace eStarter.Core.Hosting
{
    /// <summary>
    /// Wraps a Windows Job Object to enforce memory, process-count, and
    /// kill-on-close limits on a child process tree.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class WindowsJobObject : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;

        private WindowsJobObject(IntPtr handle) => _handle = handle;

        /// <summary>
        /// Create a Job Object, configure limits from <paramref name="policy"/>,
        /// and assign <paramref name="pid"/> into it.
        /// </summary>
        public static WindowsJobObject Apply(SandboxPolicy policy, int pid)
        {
            var handle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (handle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var job = new WindowsJobObject(handle);
            try
            {
                job.SetLimits(policy);
                job.AssignProcess(pid);
                return job;
            }
            catch
            {
                job.Dispose();
                throw;
            }
        }

        // ── Configuration ─────────────────────────────────────────────────

        private void SetLimits(SandboxPolicy policy)
        {
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            var basic = new JOBOBJECT_BASIC_LIMIT_INFORMATION();

            uint flags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            if (policy.MemoryLimitBytes > 0)
            {
                flags |= JOB_OBJECT_LIMIT_PROCESS_MEMORY;
                info.ProcessMemoryLimit = new UIntPtr((ulong)policy.MemoryLimitBytes);
            }

            if (policy.MaxProcesses > 0)
            {
                flags |= JOB_OBJECT_LIMIT_ACTIVE_PROCESS;
                basic.ActiveProcessLimit = (uint)policy.MaxProcesses;
            }

            basic.LimitFlags = flags;
            info.BasicLimitInformation = basic;

            int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            IntPtr ptr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, ptr, false);
                if (!NativeMethods.SetInformationJobObject(
                    _handle,
                    JobObjectInfoType.ExtendedLimitInformation,
                    ptr,
                    (uint)length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private void AssignProcess(int pid)
        {
            using var proc = Process.GetProcessById(pid);
            if (!NativeMethods.AssignProcessToJobObject(_handle, proc.Handle))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        // ── IDisposable ───────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_handle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }

        // ── P/Invoke ──────────────────────────────────────────────────────

        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY     = 0x00000100;
        private const uint JOB_OBJECT_LIMIT_ACTIVE_PROCESS     = 0x00000008;

        private enum JobObjectInfoType
        {
            BasicLimitInformation = 2,
            ExtendedLimitInformation = 9
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetInformationJobObject(
                IntPtr hJob,
                JobObjectInfoType infoType,
                IntPtr lpJobObjectInfo,
                uint cbJobObjectInfoLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr hObject);
        }
    }
}
