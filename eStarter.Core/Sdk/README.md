# eStarter SDK 使用指南

## 快速开始

### 1. 创建 manifest.json

在您的应用程序目录中创建 `manifest.json`:

```json
{
  "id": "com.example.myapp",
  "name": "My App",
  "description": "A sample eStarter app",
  "publisher": "Your Name",
  "version": "1.0.0",
  "category": "Utilities",
  "permissions": [
    "FileRead",
    "FileWrite",
    "Notification"
  ],
  "minApiVersion": 1,
  "sandboxed": true
}
```

### 2. 连接到 eStarter

```csharp
using eStarter.Sdk;

// 方式1: 手动创建客户端
var client = new EStarterClient("com.example.myapp", "1.0.0");
if (await client.ConnectAsync())
{
    Console.WriteLine("Connected to eStarter!");
}

// 方式2: 从 manifest.json 自动创建
var client = await SdkHelpers.CreateFromManifestAsync();

// 方式3: 继承 EStarterApp 基类
public class MyApp : EStarterApp
{
    public MyApp() : base("com.example.myapp", "1.0.0") { }

    protected override async Task OnConnectedAsync()
    {
        // 连接成功后的初始化
    }
}
```

## API 使用示例

### 文件系统 API

```csharp
// 写入文件
var path = FileSystemApi.AppDataPath(client.AppId, "settings.json");
await client.FileSystem.WriteTextAsync(path, "{\"theme\": \"dark\"}");

// 读取文件
var result = await client.FileSystem.ReadTextAsync(path);
if (result.Success)
{
    Console.WriteLine(result.Data);
}

// 列出目录
var files = await client.FileSystem.ListDirectoryAsync(
    FileSystemApi.AppDataPath(client.AppId));
if (files.Success)
{
    foreach (var file in files.Data)
    {
        Console.WriteLine($"{file.Name} - {file.Size} bytes");
    }
}

// 检查文件是否存在
bool exists = await client.FileSystem.ExistsAsync(path);
```

### 权限 API

```csharp
// 检查权限
if (!await client.Permissions.HasAsync(PermissionApi.FileSystem.Write))
{
    // 请求权限
    bool granted = await client.Permissions.RequestAsync(PermissionApi.FileSystem.Write);
    if (!granted)
    {
        Console.WriteLine("Permission denied!");
        return;
    }
}

// 获取所有权限
var perms = await client.Permissions.GetAllAsync();
if (perms.Success)
{
    Console.WriteLine($"Granted: {perms.Data.Granted}");
}
```

### 系统 API

```csharp
// Ping 测试连接
bool connected = await client.System.PingAsync();

// 获取系统信息
var info = await client.System.GetInfoAsync();
if (info.Success)
{
    Console.WriteLine($"OS: {info.Data.Os} v{info.Data.Version}");
    Console.WriteLine($"Uptime: {info.Data.UptimeSeconds}s");
}

// 获取进程列表
var processes = await client.System.GetProcessListAsync();

// 启动另一个应用
await client.System.LaunchAppAsync("com.example.otherapp", "--arg1 value");
```

### IPC API

```csharp
// 发送消息给另一个应用
await client.Ipc.SendAsync("com.example.otherapp", "greeting", new { message = "Hello!" });

// 广播消息
await client.Ipc.BroadcastAsync("announcement", new { text = "System update!" });

// 订阅频道
await client.Ipc.SubscribeAsync("notifications");
```

### 事件订阅

```csharp
// 订阅系统事件
client.Events.Subscribe(SystemEvents.ThemeChanged, data =>
{
    Console.WriteLine("Theme changed!");
});

// 订阅带类型的事件
client.Events.Subscribe<NotificationData>("notification", notification =>
{
    Console.WriteLine($"Got notification: {notification.Title}");
});

// 等待特定事件
var eventData = await client.Events.WaitForAsync("app.ready", timeoutMs: 10000);
```

## 虚拟路径格式

| 区域 | 路径格式 | 描述 |
|------|----------|------|
| AppData | `/appdata/{appId}/...` | 应用私有数据（持久化） |
| Cache | `/cache/{appId}/...` | 缓存数据（可清除） |
| Temp | `/temp/{appId}/...` | 临时文件 |
| Shared | `/shared/{appId}/...` | 共享存储（需权限） |
| System | `/system/...` | 系统文件（只读） |

## 可用权限

| 类别 | 权限名 | 描述 |
|------|--------|------|
| 文件 | `FileRead` | 读取文件 |
| 文件 | `FileWrite` | 写入文件 |
| 文件 | `FileDelete` | 删除文件 |
| 网络 | `NetworkAccess` | 网络访问 |
| UI | `Notification` | 显示通知 |
| UI | `Clipboard` | 剪贴板访问 |
| UI | `Dialog` | 显示对话框 |
| 系统 | `ProcessLaunch` | 启动其他应用 |
| 系统 | `SystemInfo` | 获取系统信息 |
| IPC | `IpcSend` | 发送消息 |
| IPC | `IpcReceive` | 接收消息 |
| IPC | `IpcBroadcast` | 广播消息 |

## 最佳实践

1. **始终检查 API 结果**: 使用 `result.Success` 检查操作是否成功
2. **处理断开连接**: 订阅 `Disconnected` 事件
3. **请求必要权限**: 在 manifest.json 中声明所需权限
4. **使用沙盒模式**: 设置 `sandboxed: true` 以增强安全性
5. **正确释放资源**: 使用 `using` 或 `DisposeAsync()`
