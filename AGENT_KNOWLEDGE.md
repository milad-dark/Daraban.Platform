# Daraban.Agent — Phase 4 Knowledge Reference

> **Source:** `https://github.com/milad-dark/Daraban.Agent`
> **Local copy:** `src/Agent/` (55 files)
> **Purpose:** Server-side integration for Phase 4 tasks

---

## 1. Architecture Overview

```
Daraban.Agent.Cli          (Console app — System.CommandLine)
    └── Daraban.Agent.Core  (Library — tasks, collectors, transport)
Daraban.Agent.Service       (Windows Service / systemd — BackgroundService)
    └── Daraban.Agent.Core
```

- **AgentRunner** — Single scheduling logic shared by CLI and Service
- **IAgentTask** — Plugin interface: `Name` + `RunAsync(AgentOptions, CancellationToken)`
- **TaskRegistry** — Static list of 8 tasks (CLI and Service both use this)
- **AgentStatusTracker** — Thread-safe status backing `/status` HTTP endpoint

---

## 2. Agent → Server API Contract (DarabanClient)

### Authentication (transitional dual-auth)
1. **OAuth2 client_credentials** — `Authorization: Bearer <jwt>` (preferred)
2. **API Key** — `X-Api-Key: <key>` (transitional, to be removed)
3. **User-Agent** — Always `Daraban-Agent/1.0`
4. **Content-Encoding** — Optional `gzip` when `--gzip` is set

### All POST bodies use this envelope shape:
```json
{
  "deviceId": "string",
  "itemtype": "Computer" | "EsxHost" | null,
  "action": "inventory" | "discovery" | "netinventory" | "wakeonlan" | "esx" | "deployResult",
  "timestampUtc": "2026-08-27T12:00:00Z",
  "content": { /* task-specific payload */ }
}
```

### Routes the server MUST implement:

| Method | Route | Request Body | Response | Notes |
|--------|-------|-------------|----------|-------|
| `GET` | `/api/agent/prolog?deviceId={id}` | — | JSON config string | Handshake; agent sends this before every task run |
| `POST` | `/api/agent/inventory` | Envelope(DeviceInventory.Content) | 200 OK | Computer inventory |
| `POST` | `/api/agent/discovery` | Envelope(List\<DiscoveredHost\>) | 200 OK | Network discovery results |
| `POST` | `/api/agent/netinventory` | Envelope(List\<NetworkDeviceInventory\>) | 200 OK | SNMP network inventory |
| `POST` | `/api/agent/wakeonlan` | Envelope(List\<WakeOnLanResult\>) | 200 OK | WoL results |
| `POST` | `/api/agent/esx` | Envelope(EsxHostInfo) | 200 OK | ESXi host + VM inventory |
| `GET` | `/api/agent/deploy/jobs?deviceId={id}` | — | List\<DeployJob\> | Pending deploy jobs |
| `POST` | `/api/agent/deploy/result` | Envelope(DeployJobResult) | 200 OK | Deploy completion report |
| `GET` | `/api/agent/collect/jobs` | — | List\<CollectJob\> (204 = empty) | Ad-hoc collect jobs |
| `POST` | `/api/agent/collect/results` | CollectResultsPayload | 200 OK | Collect job results |

---

## 3. Key Models

### DeviceInventory (inventory submission)
```csharp
public class DeviceInventory {
    string DeviceId;       // machine hostname or agent ID
    string Action;         // "inventory"
    string Content;        // JSON-serialized DeviceContent
}
```

### DeviceContent (rich hardware/software data)
- **System:** ComputerName, OperatingSystem, OsArchitecture, Domain, LoggedOnUser
- **Hardware:** ComputerSystemInfo (Manufacturer, Model, SystemType), BiosInfo, Motherboard*
- **Components:** List\<CpuInfo\>, List\<MemoryInfo\>, List\<StorageInfo\>, List\<NetworkInterfaceInfo\>
- **Peripherals:** List\<MonitorInfo\>, List\<AudioDevice\>, List\<VideoControllerInfo\>
- **Security:** List\<UserAccountInfo\>, List\<GroupInfo\>, List\<ServiceInfo\>
- **Software:** List\<SoftwareInfo\>, List\<HotfixInfo\>, List\<ProcessInfo\>, List\<PrinterInfo\>
- **Power:** List\<BatteryInfo\>

### DeployJob (server → agent)
```csharp
public sealed record DeployJob {
    string JobId; string Name; List<DeployFile> Files;
    string InstallCommand; int TimeoutSeconds;  // default 600
}
public sealed record DeployFile {
    string Url; string FileName; string Sha256;
}
public sealed record DeployJobResult {
    string JobId; DeployStatus Status; string? Message;
    int? ExitCode; DateTime CompletedUtc;
}
```

### CollectJob (server → agent)
```csharp
public sealed class CollectJob {
    string JobId; CollectJobType Type;
    // RegistryKey: RegistryHive, RegistryPath, RegistryValue
    // WmiQuery: WmiNamespace, WmiQuery, WmiProperty
    // FileContent: FilePath, FileRegex
    // Command: Command, Arguments
}
public enum CollectJobType { RegistryKey=1, WmiQuery=2, FileContent=3, Command=4 }
// Custom JsonConverter handles: "1", 1, "RegistryKey" formats
```

### CollectResultsPayload (agent → server)
```csharp
internal sealed class CollectResultsPayload {
    string AgentId; DateTime Timestamp; IList<CollectResult> Results;
}
public sealed class CollectResult {
    string JobId; bool Success; string? Value; string? Error; DateTime CollectedAt;
}
```

### Network Models
```csharp
public sealed record DiscoveredHost {
    string IpAddress; string? MacAddress; string? Hostname;
    bool Responded; long RoundtripMs;
    bool SnmpReachable; string? SysDescr; string? SysObjectId; string? DeviceType;
}
public sealed record NetworkDeviceInventory {
    string IpAddress; string? Community; bool Reachable;
    DeviceInventory? Inventory; string? Error;
}
public sealed record EsxHostInfo {
    string Name; string? Vendor, Model, CpuModel, BiosVersion, Version;
    int CpuCores; long MemoryMb; List<EsxVmInfo> VirtualMachines;
}
public sealed record EsxVmInfo {
    string Name; string? Uuid, GuestOs, PowerState;
    int CpuCount; long MemoryMb;
    List<string> IpAddresses; List<long> DiskSizesMb;
}
public sealed record WakeOnLanResult {
    string MacAddress; bool Sent; string? Error;
}
```

---

## 4. OAuthTokenProvider (client_credentials flow)

- **Endpoint:** Configured via `AgentOptions.OAuthTokenEndpoint`
- **Grant:** `client_credentials` with `client_id`, `client_secret`, `scope`
- **Default scope:** `daraban.agent.inventory`
- **Caching:** Double-checked locking with `SemaphoreSlim`; refreshes 1 minute before expiry
- **Token response:** `{ "access_token": "...", "expires_in": 300 }`
- **If no endpoint configured:** Returns null (no auth header sent)

---

## 5. AgentOptions (configuration model)

Key properties for Phase 4 server-side:
- `Server` — Base URL of the Daraban.Platform Host API
- `Tag` / `AgentId` — Machine identity (defaults to hostname)
- `ApiKey` — Transitional API key header
- `OAuthTokenEndpoint` / `OAuthClientId` / `OAuthClientSecret` / `OAuthScope`
- `Tasks` / `NoTasks` — Which tasks to run
- `DelayTimeSeconds` — Scheduling interval (default 3600)
- `Lazy` — Random jitter to avoid fleet thundering herd
- `UseGzip` — Compress POST bodies
- `IpRange`, `SnmpCommunity`, `DiscoveryThreads` — Network task config
- `EsxHost`/`EsxUser`/`EsxPassword` — ESXi connection
- `RemoteHosts` — SSH/WinRM connection strings
- `WakeOnLanMacs` — MAC addresses for WoL

---

## 6. JsonSerializerOptions Used by Agent

```csharp
new JsonSerializerOptions {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
}
```

**Important:** All property names are camelCase on the wire. Server-side deserialization must use `PropertyNameCaseInsensitive = true` or matching camelCase DTOs.

---

## 7. Known Gaps (from README + code review)

1. **No API-key validation on server** — Agent sends `X-Api-Key` but server doesn't check
2. **No deploy manifest signing** — Trusts SHA-256 only
3. **ESX SOAP-only fields** — BIOS version/CPU string null from REST API
4. **DarabanAgent.Server** (standalone Blazor+API) exists in original repo but NOT in this codebase — our Phase 4 replaces it with integrated Host API controllers
5. **Collect task** — Was initially missing but now implemented with Registry/WMI/File/Command collectors
6. **Deep per-OS inventory** — Core categories covered; 148+ submodules from daraban-agent not included

---

## 8. Phase 4 Task Mapping

| Phase 4 Task | What to Build | Agent Contract to Match |
|-------------|--------------|------------------------|
| 4.2 OAuth2 Auth | OpenIddict client_credentials validation | OAuthTokenProvider's token request format |
| 4.3 Inventory Upload | Server-side reception of all POST routes | DarabanClient's 10 routes + envelope shape |
| 4.4 Agent Commands | Command dispatch via SignalR | AgentControlHub's hub methods |
| 4.5 Agent Dashboard | Angular fleet management UI | Agent entity model from Task 4.1 |

---

## 9. File Inventory (55 files)

```
src/Agent/
├── README.md                          # Original package documentation
├── Daraban.Agent.slnx                 # Agent's own solution file
├── Daraban.Agent.Core/
│   ├── Daraban.Agent.Core.csproj      # Library (net10.0, SharpSnmpLib, SSH.NET, System.CommandLine)
│   ├── Agents/                        # Task system
│   │   ├── IAgentTask.cs              # Interface: Name + RunAsync
│   │   ├── AgentRunner.cs             # Scheduler (PeriodicTimer, prolog + task loop)
│   │   ├── AgentStatusTracker.cs      # Thread-safe /status backing
│   │   ├── TaskRegistry.cs            # Static list of 8 tasks
│   │   ├── LocalInventoryTask.cs      # local
│   │   ├── NetDiscoveryTask.cs        # netdiscovery (ICMP+ARP+SNMP)
│   │   ├── NetInventoryTask.cs        # netinventory (SNMP full)
│   │   ├── RemoteInventoryTask.cs     # remote (SSH/WinRM)
│   │   ├── WakeOnLanTask.cs           # wakeonlan
│   │   ├── DeployTask.cs              # deploy (download+verify+install)
│   │   ├── EsxInventoryTask.cs        # esx (vCenter REST)
│   │   ├── CollectTask.cs             # collect (registry/WMI/file/command)
│   │   └── RemoteHostSpec.cs          # URI parser for ssh:// / winrm://
│   ├── Collectors/                    # Platform-specific data collection
│   │   ├── LocalCollectorFactory.cs   # OS detection → right collector
│   │   ├── LocalWindowsCollector.cs   # WMI-based Windows inventory
│   │   ├── LocalLinuxCollector.cs     # /proc, /sys, dmidecode, lsblk
│   │   ├── LocalMacCollector.cs       # system_profiler, sysctl
│   │   ├── SnmpNetworkCollector.cs    # SNMP walk + discovery
│   │   ├── SshRemoteCollector.cs      # SSH.NET remote inventory
│   │   ├── WinrmRemoteCollector.cs    # WSMan remote inventory
│   │   ├── EsxRestCollector.cs        # vSphere REST API
│   │   ├── CollectCollector.cs        # Registry/WMI/File/Command executor
│   │   └── WakeOnLanSender.cs         # UDP magic packet
│   ├── Config/
│   │   └── AgentOptions.cs            # All configuration properties
│   ├── Http/
│   │   └── StatusEndpoint.cs          # Kestrel /status + CIDR trust
│   ├── Models/                        # All DTOs (see Section 3)
│   │   ├── DeviceInventory.cs         # + DeviceContent + 20 sub-models
│   │   ├── DiscoveredHost.cs          # Network discovery result
│   │   ├── NetworkDeviceInventory.cs  # SNMP device record
│   │   ├── EsxHostInfo.cs             # ESXi host + VMs
│   │   ├── DeployJob.cs               # Deploy job + files + result
│   │   ├── DeployStatus.cs            # Enum
│   │   ├── CollectJob.cs              # + CollectJobType + JsonConverter
│   │   ├── CollectResult.cs           # Single result
│   │   ├── CollectResultsPayload.cs   # Batch wrapper
│   │   ├── WakeOnLanTarget.cs         # + WakeOnLanResult
│   │   └── RemoteEntry.cs             # Remote host entry
│   ├── Transport/                     # HTTP client layer
│   │   ├── IDarabanClient.cs          # Interface (11 methods)
│   │   ├── DarabanClient.cs           # Implementation (envelope, gzip, auth)
│   │   ├── DarabanClientFactory.cs    # Factory (BaseAddress + timeout)
│   │   └── OAuthTokenProvider.cs      # client_credentials token cache
│   └── Tools/
│       └── StringExtensions.cs        # Truncate helper
├── Daraban.Agent.Cli/
│   ├── Daraban.Agent.Cli.csproj       # Console app (net10.0)
│   ├── Program.cs                     # 20+ CLI options, list-tasks subcommand
│   └── my_local_inventory.json        # Test data
└── Daraban.Agent.Service/
    ├── Daraban.Agent.Service.csproj   # Worker service (net10.0)
    ├── Program.cs                     # DI registration, HostedService
    ├── Worker.cs                      # BackgroundService → AgentRunner
    ├── appsettings.json               # Default config
    ├── appsettings.Development.json   # Dev config
    ├── Properties/
    │   └── launchSettings.json
    └── Installer/
        ├── daraban-agent.service      # systemd unit
        └── Install-Service.ps1        # Windows service installer
```
