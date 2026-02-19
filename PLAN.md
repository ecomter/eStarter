# eStarter — Multi-Runtime Hosting Implementation Plan

> Based on `ROADMAP.md`. Each phase is self-contained and builds on the previous one.

---

## Current State

| Component | Status |
|---|---|
| `Kernel` + `PermissionManager` + `VirtualFileSystem` | ✅ Done |
| Named-pipe IPC (`PipeStreamHelper`, `IpcMessage`) | ✅ Done |
| `AppManifest` (permissions, sandboxed, runtime, entry, limits) | ✅ Done |
| `ManagedProcess` (wraps `System.Diagnostics.Process`) | ✅ Done |
| `IAppHost` / `SandboxPolicy` / `AppHostFactory` | ✅ Done (Phase 1) |
| `ProcessHost` (StreamJsonRpc bridge) | ✅ Done (Phase 2) |
| `MainViewModel.LaunchApp` integration | ✅ Done (Phase 2.3) |
| OS limits (JobObject / cgroups) | ✅ Done (Phase 3) |
| `WasmAppHost` (Wasmtime) | ✅ Done (Phase 4) |
| SDK dual transport (pipe + stdio) | ✅ Done (Phase 5) |
| Duplicate-file dedup (`.csproj` cleanup) | ✅ Done |
| Tests + sample apps | ✅ Done (Phase 6) — 31 tests, 1 sample app |

---

## Phase 1 — Foundation types  *(1–2 days)*

**Goal:** Define the contracts that all later phases implement against.

### 1.1 Extend `AppManifest`

Add backwards-compatible fields:

```json
{
  "runtime": "native",   // "native" | "wasm" | "dotnet"
  "entry": "app.exe",    // path relative to app directory
  "memoryLimitMb": 256,  // optional, 0 = unlimited
  "maxProcesses": 4,
  "cpuQuota": 50,        // percentage 0-100, 0 = unlimited
  "networkAllowed": false,
  "maxRuntimeSeconds": 0
}
```

File: `eStarter.Core/Models/AppManifest.cs`

### 1.2 `SandboxPolicy`

Immutable value-type that captures per-app resource limits, created from `AppManifest`.

File: `eStarter.Core/Core/Hosting/SandboxPolicy.cs`

### 1.3 `IAppHost` interface

```csharp
public interface IAppHost : IAsyncDisposable
{
    string AppId { get; }
    AppHostState State { get; }
    event EventHandler<AppHostExitedEventArgs>? Exited;
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
```

File: `eStarter.Core/Core/Hosting/IAppHost.cs`

### 1.4 `AppHostFactory` (stub)

Reads `manifest.runtime` and returns the right host. Initially only `ProcessHost` is wired; WASM falls back gracefully.

File: `eStarter.Core/Core/Hosting/AppHostFactory.cs`

---

## Phase 2 — `ProcessHost` with StreamJsonRpc  *(3–5 days)*

**Goal:** Replace raw `Process.Start` in `MainViewModel.LaunchApp` with a hosted, JSON-RPC-bridged process.

### 2.1 Add NuGet packages

```xml
<PackageReference Include="StreamJsonRpc" Version="2.*" />
```

### 2.2 `ProcessHost` responsibilities

- Clear environment variables (keep only `PATH` + `ESTARTER_MODE=hosted`).
- Set working directory to `%LocalAppData%\eStarter\apps\{appId}`.
- Redirect `stdin`/`stdout` for JSON-RPC; `stderr` → debug log.
- Create a `JsonRpc` server on `stdin`/`stdout`.
- For every incoming JSON-RPC call, build an `ApiRequest` and forward to `Kernel.HandleApiAsync(appId, request)`.
- Raise `IAppHost.Exited` when the process exits.

File: `eStarter.Core/Core/Hosting/ProcessHost.cs`

### 2.3 Wire into `MainViewModel.LaunchApp`

Replace direct `Process.Start` calls with `AppHostFactory.CreateHost(manifest, policy).StartAsync()`.
Keep the running host in a `_activeHosts` dictionary keyed by `appId`.

---

## Phase 3 — OS resource limits  *(2–4 days)*

**Goal:** Enforce `SandboxPolicy` at the OS level.

### 3.1 Windows — Job Objects

```csharp
// eStarter.Core/Core/Hosting/OS/WindowsJobObject.cs
internal sealed class WindowsJobObject : IDisposable { ... }
```

Use `CreateJobObject` + `SetInformationJobObject` P/Invoke (or NuGet `JobObjectWrapper`) to:
- Set `JOBOBJECT_EXTENDED_LIMIT_INFORMATION.ProcessMemoryLimit`
- Set `JOBOBJECT_BASIC_LIMIT_INFORMATION.ActiveProcessLimit`
- Set `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`

Assign the child process to the Job Object immediately after `Process.Start`.

### 3.2 Linux — cgroups v2

```csharp
// eStarter.Core/Core/Hosting/OS/LinuxCgroup.cs
internal sealed class LinuxCgroup : IDisposable { ... }
```

Writes to `/sys/fs/cgroup/estarter/{appId}/`:
- `memory.max` ← `SandboxPolicy.MemoryLimitBytes`
- `pids.max` ← `SandboxPolicy.MaxProcesses`
- `cpu.max` ← derived from `SandboxPolicy.CpuQuotaPercent`
- `cgroup.procs` ← child PID after start

### 3.3 Abstraction

```csharp
// eStarter.Core/Core/Hosting/OS/IOsResourceLimit.cs
internal interface IOsResourceLimit : IDisposable
{
    void Apply(int pid);
}
```

`ProcessHost` calls `OsResourceLimitFactory.Create(policy)` which returns the right implementation.

---

## Phase 4 — `WasmAppHost`  *(3–5 days)*

**Goal:** Run `app.wasm` modules with full memory isolation via Wasmtime.

### 4.1 Add NuGet packages

```xml
<PackageReference Include="Wasmtime" Version="*" />
```

### 4.2 `WasmAppHost` responsibilities

- Load `app.wasm` from the app's directory.
- Create a Wasmtime `Engine` + `Store` per app (strong isolation).
- Export a set of host functions (`estarter_api_call`, `estarter_log`) that:
  - Deserialise the guest's payload
  - Call `Kernel.HandleApiAsync(appId, request)`
  - Write the response back into WASM memory
- Enforce `SandboxPolicy.MemoryLimitBytes` via `Store.SetLimits`.
- Raise `IAppHost.Exited` when the WASM `_start` export returns.

File: `eStarter.Core/Core/Hosting/WasmAppHost.cs`

---

## Phase 5 — SDK stdio transport  *(1–2 days)*

**Goal:** The client SDK auto-detects whether it is running under `ProcessHost` and switches from named pipes to the stdio JSON-RPC channel.

### 5.1 Detection

`EStarterClient` checks `Environment.GetEnvironmentVariable("ESTARTER_MODE")`:
- `"hosted"` → use `StdioTransport` (reads/writes JSON-RPC on stdin/stdout via `StreamJsonRpc` proxy)
- anything else → keep existing named-pipe transport

### 5.2 `StdioTransport`

```csharp
// eStarter.Core/Sdk/Transport/StdioTransport.cs
```

Wraps `Console.OpenStandardInput()` / `Console.OpenStandardOutput()` with `StreamJsonRpc.JsonRpc`.

---

## Phase 6 — Tests + sample apps  *(1–2 days)*

### 6.1 Unit tests

- `SandboxPolicy.FromManifest` parses correctly
- `AppHostFactory` selects correct host type
- `ProcessHost` forwards API calls to `Kernel` (use a mock kernel)

### 6.2 Sample native app

`samples/hello-native/`:
- A minimal .NET 8 console app that uses `EStarterClient` (stdio mode).
- Packaged as `hello-native.app.zip` with `manifest.json` (`runtime: "native"`).

### 6.3 Sample WASM app

`samples/hello-wasm/`:
- A minimal Rust (or C) app compiled to WASM.
- Calls `estarter_api_call` for a `ShowNotification`.

---

## New files summary

```
eStarter.Core/
└─ Core/
   └─ Hosting/
      ├─ IAppHost.cs           ← Phase 1
      ├─ SandboxPolicy.cs      ← Phase 1
      ├─ AppHostFactory.cs     ← Phase 1 (stub) / Phase 2 (wired)
      ├─ ProcessHost.cs        ← Phase 2
      ├─ WasmAppHost.cs        ← Phase 4
      └─ OS/
         ├─ IOsResourceLimit.cs    ← Phase 3
         ├─ OsResourceLimitFactory.cs ← Phase 3
         ├─ WindowsJobObject.cs    ← Phase 3 (Windows)
         └─ LinuxCgroup.cs        ← Phase 3 (Linux)
eStarter.Core/
└─ Sdk/
   └─ Transport/
      └─ StdioTransport.cs     ← Phase 5
eStarter.Core/
└─ Models/
   └─ AppManifest.cs           ← Phase 1 (extend)
```

---

## NuGet packages to add

| Package | Phase | Purpose |
|---|---|---|
| `StreamJsonRpc` | 2 | JSON-RPC over stdin/stdout |
| `Wasmtime` | 4 | WASM runtime |
| `JobObjectWrapper` (optional) | 3 | Windows Job Objects (or use P/Invoke helper) |

---

## Implementation order (recommended)

```
Phase 1  →  Phase 2  →  Phase 3 (Windows)  →  Phase 3 (Linux)  →  Phase 4  →  Phase 5  →  Phase 6
```

Phase 3 (Linux) can be done in parallel with Phase 4 if resources allow.

---

_Plan generated from `ROADMAP.md`. Phases 1–2 are highest priority for unblocking real app launching._
