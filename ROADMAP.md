# eStarter 路线图（简明 + 详细）


## Part One : Runtime & Sandbox Architecture（已大体完成）
Goal: evolve eStarter from a WPF host + loose app launcher into a small, cross-platform app runtime with true per-app isolation while keeping compatibility with existing native apps. Deliver a multi-runtime host (WASM / native / .NET) and an OS-style Kernel that enforces permissions and VFS access.

### Features implemented so far
- Kernel 的 API 路径与 PermissionManager 基础已实现（可接收并处理 API 请求）。
- WASM 主机与示例（hello-wasm）已存在，能通过 Kernel 发起权限请求。
- WPF 前端、MainViewModel、KernelService、AppInstaller 等基础功能已就绪。

### details

Summary of chosen approach

- Provide three runtimes supported by a unified `IAppHost` interface:
  - `wasm` (recommended default): run apps as WebAssembly modules under Wasmtime (`wasmtime-dotnet`) — strongest isolation (memory + capability sandbox) and cross-platform.
  - `native`: run existing `.exe` / native binaries under a `ProcessHost` with stdio JSON-RPC bridge (use `StreamJsonRpc`) and OS resource limits (Windows Job Objects, Linux cgroups). Keeps compatibility.
  - `dotnet`: optional future host using `AssemblyLoadContext` for managed apps.

- All app capability requests MUST go through the Kernel's `HandleApiAsync` (existing code). Host implementations map transport calls into `ApiRequest` objects and call `Kernel.HandleApiAsync(appId, request)`.

- Add a `SandboxPolicy` per app (driven by `manifest.json`) that controls memory, process limits, CPU quota, networking, runtime type, and max runtime.

Open-source libraries to use (high feasibility)

- `StreamJsonRpc` (Microsoft) — stdin/stdout JSON-RPC transport for `native` ProcessHost.
- `wasmtime-dotnet` — production-quality WASM runtime for `wasm` apps.
- `JobObjectWrapper` or similar NuGet — Windows Job Object wrapper to avoid low-level P/Invoke.
- For Linux, use cgroups v2 by interacting with `/sys/fs/cgroup` (small helper) or a lightweight library if preferred (no single canonical NuGet). `Tmds.LinuxAsync` can help for async Linux-specific operations.

High-level architecture

```
Kernel (PermissionManager, VFS, API handlers)
└─ AppHostFactory
   ├─ WasmAppHost (wasmtime)  <-- manifest.runtime == "wasm"
   │   └─ exports -> Kernel.HandleApiAsync
   ├─ ProcessHost (native)    <-- manifest.runtime == "native"
   │   └─ StreamJsonRpc over stdin/stdout -> Kernel.HandleApiAsync
   └─ DotnetAppHost (optional)
```

Manifest additions (backwards compatible)

- `runtime` : `"wasm" | "native" | "dotnet"` (default `native`)
- `entry` : path to `app.wasm` or `exe` or `dll`
- existing `permissions` remain authoritative

Example manifest snippet:

```json
{
  "id": "com.example.myapp",
  "runtime": "wasm",
  "entry": "app.wasm",
  "permissions": ["FileRead","Notification"],
  "sandboxed": true
}
```

Core new types (proposed)

- `SandboxPolicy` — memory/cpu/process/network config; created from manifest.
- `IAppHost` interface with `StartAsync()`, `StopAsync()`, `Dispose()` and events.
- `AppHostFactory` -> chooses and configures host implementation.
- `ProcessHost` (native) — minimal environment, clears env, sets working dir to app sandbox, applies OS limits, creates `StreamJsonRpc` on stdio, translates JSON-RPC->ApiRequest->Kernel.
- `WasmAppHost` (wasmtime) — instantiate WASM, export minimal host calls that call Kernel APIs; enforce memory/table limits via Wasmtime.

OS-level sandboxing plan

- Windows
  - Use `JobObject` wrapper (NuGet `JobObjectWrapper` or write minimal P/Invoke helper) to enforce memory limit, active process limit, kill-on-close.
  - Optionally create restricted token for lower privileges (requires elevation to create tokens in some scenarios).
- Linux
  - Use cgroups v2: create a per-app cgroup, write `memory.max`, `pids.max`, `cpu.max`, then add process PID to `cgroup.procs`.
  - If systemd or user namespaces are available, prefer user cgroups for unprivileged operation.

Implementation milestones (suggested)

1. (1–2 days) Create `ROADMAP.md` (this file), `SandboxPolicy` type, `IAppHost` interface, and `AppHostFactory` (stubs).
2. (3–5 days) Implement `ProcessHost`:
   - Add NuGet `StreamJsonRpc`
   - Build stdio JSON-RPC bridge, clear env, lock working directory
   - Basic Kernel API forwarding
3. (2–4 days) Add OS limits for `ProcessHost`:
   - Add `JobObjectWrapper` integration on Windows
   - Add cgroup v2 helper on Linux
4. (3–5 days) Implement `WasmAppHost` with `wasmtime-dotnet`:
   - Export host functions mapping to Kernel API commands
   - Enforce `SandboxPolicy` resource limits inside Wasmtime
5. (1–2 days) SDK changes: add stdio transport for the client SDK (auto-detect `ESTARTER_MODE` env).
6. (1–2 days) Tests + examples: create a sample `native` zip and a sample `wasm` app (Rust/C#) and verify permission enforcement.



##  Part Two
Goal: for the first rollout prioritize Windows and guarantee that any process launched by eStarter (the top-level process) — and any children it spawns — remain controlled: every sensitive API call must be intercepted, authenticated, authorized via eStarter's PermissionManager, and audited.

- 在 `ProcessHost` 中以挂起方式创建子进程（CreateProcess + CREATE_SUSPENDED），注入受信任的 `estarter_shim.dll`，再恢复执行。
- `estarter_shim.dll` 在进程内 hook 敏感 API（文件/注册表/网络/进程创建），将请求发到本地 `ShimAgent` 进行授权，shim 也负责在创建子进程时传递注入与短期 token，保证链式覆盖。
- 使用 Windows Job Object 将进程树管理在一起，便于资源限制与统一回收。
### Requirements

#### 最小可交付里程碑
1) PoC（监控模式，3–5 天）
   - `ProcessHost`：实现挂起创建 + token 管理。
   - 简易 shim：拦截 `CreateProcess` 与少量文件 API，上报 `ShimAgent`（监控模式先只记录）。
2) 强制模式（7–12 天）
   - 完整 hook（文件/注册表/网络），`ShimAgent` 授权映射、缓存/TTL、JobObject 限制、失败策略与测试。

#### 核心组件（简述）
- `ProcessHost`：挂起创建、注入 shim、注册 token、JobObject 管理。
- `estarter_shim`：进程内 hook + 向 `ShimAgent` 发起授权请求并缓存决策。
- `ShimAgent`：验证 token、调用 `PermissionManager`、写结构化审计日志。
- `PermissionManager`：支持原生动作映射、短期缓存与审计。

#### 测试要点（必须覆盖）
- 由 eStarter 启动的进程执行文件读写能被 shim 拦截并上报。
- 被启动进程 spawn 的子进程同样被注入并受控。
- 注入失败时的默认行为（拒绝启动或进入监控）正确。

#### 注意事项
- token 建议用继承句柄或受限临时文件传递，避免环境变量泄露。
- 默认策略为注入失败即拒绝启动；生产前可先用监控模式收集策略数据。
- 本方案保障的是由 eStarter 启动的受控流程。

### Details

Design overview
- Force-inject a small trusted shim DLL into every process launched by eStarter. Injection happens before the process is allowed to run (CreateProcess with CREATE_SUSPENDED), then LoadLibrary into the suspended child, then ResumeThread. The shim hooks sensitive APIs and proxies requests to a local eStarter agent (ShimAgent) for authorization.
- The shim also hooks process-creation APIs (CreateProcess/ShellExecute/etc.) inside the target process and, when such APIs are called, ensures the child process is created suspended and receives the same shim + a short-lived signed token that proves the child was launched under eStarter's control. The shim therefore enforces chain-wide coverage.
- The ProcessHost performs binary hashing / signature checks and generates a per-launch token. The token is transferred securely (inherited handle, temporary protected file, or secure environment block) to the child and validated by the ShimAgent.
- Use Windows Job Objects to contain the whole process tree (resource limits, kill-on-close) and to make it easier to manage lifecycle and auditing.

Key components to implement (Windows-priority)
- Modify: `eStarter.Core\Core\Hosting\ProcessHost.cs` — add suspended create + injection + job object registration + token generation.
- Add: `HostShim/windows/estarter_shim.c` (or C++) — minimal hooking PoC using MinHook; build instructions in `HostShim/windows/README.md`.
- Add: `Services\ShimAgent.cs` — named pipe server integrated with `PermissionManager` and audit logger.
- Modify: `eStarter.Core\Core\Kernel\PermissionManager.cs` — mapping native request descriptors into Permission checks and audit hooks.
- Add tests: integration tests that start a sample native app via eStarter which spawns children; assert ShimAgent sees calls and PermissionManager decisions are enforced.

Testing matrix (Windows focus)
- T1: Start sample exe via eStarter; sample does simple file read — Shim must intercept and agent must be called.
- T2: Sample exe spawns a child exe via CreateProcess — Shim must inject child and agent must be called for child's sensitive calls.
- T3: Injection failure scenario — ensure ProcessHost refuses to run or switches to monitored mode and logs appropriately.
- T4: Job object limits — create CPU/memory stress tests to verify Job Object enforcement.

Timeline (Windows-priority estimate)
- PoC (monitoring, LD/preload equivalent for Windows): 3–5 days
  - Minimal shim that only intercepts CreateProcess + a small file API and reports to ShimAgent.
  - ProcessHost suspended create + token plumbing.
- Harden & full enforcement: 7–12 days
  - Add registry and networking hooks, caching/TTL, JobObject integration, full PermissionManager mapping, and tests.
- Rollout steps
  1. Deploy PoC in monitor mode to collect policy data.
  2. Convert to enforced mode with short TTL caches and admin override.
  3. Iterate on rule exceptions and performance tuning.

Notes on cross-platform follow-up
- The Windows-first plan focuses on reliable pre-execution injection and JobObjects that are Windows strengths. The same chain-model will be applied to Linux later using `LD_PRELOAD`, inherited FD tokens, and cgroup control; Windows work will guide design and IPC protocol.


Appendix: security checklist for controlled launches
- Ensure ProcessHost signs and records the launch token before starting the process.
- Ensure shim validates token with ShimAgent before allowing any sensitive API forwarding.
- Ensure ShimAgent verifies token ownership and matches registered PID + JobObject.
- Record every authenticated decision to the audit log with sufficient context for post-mortem.


NuGet packages to add

- `StreamJsonRpc`
- `Wasmtime` (when implementing WASM host)
- `JobObjectWrapper` (Windows Job Object wrapper) or implement minimal P/Invoke helper
- Optionally `Tmds.LinuxAsync` for Linux async helpers

Security notes / pitfalls

- Do not rely solely on environment variable or working dir locking — treat `native` apps as partially trusted and prefer `wasm` for untrusted submissions.
- cgroups manipulation may require privileges on some systems; document runtime requirements.
- Job objects and restricted tokens are OS-specific and may require elevated permissions to configure certain limits.
- Always validate app manifest signatures (future work) before granting powerful permissions.
