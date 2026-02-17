using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace eStarter.Core.Kernel
{
    /// <summary>
    /// Represents a running process in the eStarter OS.
    /// </summary>
    public sealed class ManagedProcess
    {
        private readonly Process? _nativeProcess;

        public required string AppId { get; init; }
        public required int ProcessId { get; init; }
        public required string Version { get; init; }
        public required string ExePath { get; init; }
        public PermissionSet Permissions { get; set; }
        public DateTime StartTime { get; init; } = DateTime.UtcNow;
        public ProcessState State { get; set; } = ProcessState.Running;

        /// <summary>
        /// Memory usage in bytes. Returns 0 if unavailable.
        /// </summary>
        public long MemoryUsage
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                try
                {
                    return _nativeProcess?.WorkingSet64 ?? GetProcessMemory();
                }
                catch { return 0; }
            }
        }

        /// <summary>
        /// CPU time used by the process.
        /// </summary>
        public TimeSpan CpuTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                try
                {
                    return _nativeProcess?.TotalProcessorTime ?? TimeSpan.Zero;
                }
                catch { return TimeSpan.Zero; }
            }
        }

        /// <summary>
        /// Whether the native process is still running.
        /// </summary>
        public bool IsRunning
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                try
                {
                    if (_nativeProcess != null)
                        return !_nativeProcess.HasExited;
                    
                    var proc = Process.GetProcessById(ProcessId);
                    return proc != null && !proc.HasExited;
                }
                catch { return false; }
            }
        }

        public ManagedProcess() { }

        public ManagedProcess(Process nativeProcess)
        {
            _nativeProcess = nativeProcess;
        }

        /// <summary>
        /// Attempt to terminate the process.
        /// </summary>
        public bool Terminate(bool force = false)
        {
            try
            {
                State = ProcessState.Terminating;

                if (_nativeProcess != null)
                {
                    if (force)
                        _nativeProcess.Kill(true);
                    else
                        _nativeProcess.CloseMainWindow();
                    
                    State = ProcessState.Terminated;
                    return true;
                }

                var proc = Process.GetProcessById(ProcessId);
                if (force)
                    proc.Kill(true);
                else
                    proc.CloseMainWindow();

                State = ProcessState.Terminated;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private long GetProcessMemory()
        {
            try
            {
                return Process.GetProcessById(ProcessId).WorkingSet64;
            }
            catch { return 0; }
        }
    }

    /// <summary>
    /// Lightweight snapshot of process info for API responses.
    /// </summary>
    public readonly struct ProcessSnapshot
    {
        public readonly string AppId;
        public readonly int ProcessId;
        public readonly string Version;
        public readonly ProcessState State;
        public readonly long MemoryBytes;
        public readonly double UptimeSeconds;

        public ProcessSnapshot(ManagedProcess process)
        {
            AppId = process.AppId;
            ProcessId = process.ProcessId;
            Version = process.Version;
            State = process.State;
            MemoryBytes = process.MemoryUsage;
            UptimeSeconds = (DateTime.UtcNow - process.StartTime).TotalSeconds;
        }
    }
}
