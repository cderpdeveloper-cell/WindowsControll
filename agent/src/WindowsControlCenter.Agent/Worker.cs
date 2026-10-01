using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WindowsControlCenter.Agent.Core.Collectors;

namespace WindowsControlCenter.Agent;

/// <summary>
/// Main agent worker service. Connects to the server, authenticates,
/// sends heartbeats and metrics, and executes commands.
/// </summary>
public class AgentWorker : BackgroundService
{
    private readonly ILogger<AgentWorker> _logger;
    private readonly AgentConfig _config;
    private readonly SystemCollector _systemCollector;
    private readonly MonitoringCollector _monitoringCollector;
    private readonly ProcessManager _processManager;
    private readonly ServiceManager _serviceManager;
    private readonly FileManager _fileManager;
    private readonly SoftwareCollector _softwareCollector;
    private HubConnection? _connection;
    private bool _isAuthenticated;

    public AgentWorker(ILogger<AgentWorker> logger, AgentConfig config)
    {
        _logger = logger;
        _config = config;
        _systemCollector = new SystemCollector(new Logger<SystemCollector>(new LoggerFactory()));
        _monitoringCollector = new MonitoringCollector(new Logger<MonitoringCollector>(new LoggerFactory()));
        _processManager = new ProcessManager(new Logger<ProcessManager>(new LoggerFactory()));
        _serviceManager = new ServiceManager(new Logger<ServiceManager>(new LoggerFactory()));
        _fileManager = new FileManager(new Logger<FileManager>(new LoggerFactory()));
        _softwareCollector = new SoftwareCollector(new Logger<SoftwareCollector>(new LoggerFactory()));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Windows Control Center Agent starting...");

        // If not registered, try to register
        if (string.IsNullOrEmpty(_config.AgentId) || string.IsNullOrEmpty(_config.ApiToken))
        {
            _logger.LogInformation("Agent not registered. Waiting for enrollment...");
            await RegisterAgent(stoppingToken);
        }

        // Connect to server
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndRun(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Connection failed. Reconnecting in 10 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task RegisterAgent(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (string.IsNullOrEmpty(_config.ServerUrl) || string.IsNullOrEmpty(_config.EnrollmentCode))
            {
                _logger.LogWarning("Server URL and enrollment code must be configured. Check agentsettings.json");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                continue;
            }

            try
            {
                var systemInfo = _systemCollector.CollectSystemInfo();
                using var httpClient = new HttpClient();
                var payload = new
                {
                    enrollmentCode = _config.EnrollmentCode,
                    computerName = systemInfo.ComputerName,
                    hostname = systemInfo.Hostname,
                    machineId = systemInfo.MachineId,
                    installationId = _config.InstallationId,
                    publicKey = "",
                    osName = systemInfo.OsName,
                    osVersion = systemInfo.OsVersion,
                    osBuild = systemInfo.OsBuild,
                    osEdition = systemInfo.OsEdition,
                    osArchitecture = systemInfo.OsArchitecture,
                    cpuName = systemInfo.CpuName,
                    cpuCores = systemInfo.CpuCores,
                    totalMemoryBytes = systemInfo.TotalMemoryBytes,
                    ipAddress = systemInfo.IpAddress,
                    macAddress = systemInfo.MacAddress
                };

                var response = await httpClient.PostAsJsonAsync(
                    $"{_config.ServerUrl}/api/agents/register", payload, ct);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RegistrationResult>(ct);
                    if (result != null)
                    {
                        _config.AgentId = result.AgentId.ToString();
                        _config.ApiToken = result.ApiToken;
                        _config.ComputerId = result.ComputerId.ToString();
                        _config.EnrollmentCode = ""; // Clear used code
                        _config.Save();

                        _logger.LogInformation("Agent registered successfully! AgentId: {AgentId}", _config.AgentId);
                        return;
                    }
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Registration failed ({StatusCode}): {Error}", response.StatusCode, error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private async Task ConnectAndRun(CancellationToken ct)
    {
        var hubUrl = $"{_config.ServerUrl}/hubs/agent?agentId={_config.AgentId}";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60) })
            .Build();

        // Register command handlers
        RegisterCommandHandlers();

        _connection.Reconnecting += error =>
        {
            _logger.LogWarning("Reconnecting to server... Error: {Error}", error?.Message);
            _isAuthenticated = false;
            return Task.CompletedTask;
        };

        _connection.Reconnected += async _ =>
        {
            _logger.LogInformation("Reconnected to server");
            await AuthenticateAsync();
        };

        _connection.Closed += error =>
        {
            _logger.LogWarning("Connection closed: {Error}", error?.Message);
            _isAuthenticated = false;
            return Task.CompletedTask;
        };

        await _connection.StartAsync(ct);
        _logger.LogInformation("Connected to server at {Url}", _config.ServerUrl);

        await AuthenticateAsync();

        // Main loop — send heartbeats and metrics
        while (_connection.State == HubConnectionState.Connected && !ct.IsCancellationRequested)
        {
            try
            {
                if (_isAuthenticated)
                {
                    // Heartbeat
                    await _connection.InvokeAsync("Heartbeat", new
                    {
                        agentId = Guid.Parse(_config.AgentId!),
                        ipAddress = GetCurrentIp(),
                        agentVersion = "1.0.0"
                    }, ct);

                    // Metrics
                    var metrics = _monitoringCollector.Collect();
                    await _connection.InvokeAsync("SendMetrics", new
                    {
                        computerId = Guid.Parse(_config.ComputerId!),
                        timestamp = metrics.Timestamp,
                        cpuUsagePercent = metrics.CpuUsagePercent,
                        memoryUsagePercent = metrics.MemoryUsagePercent,
                        memoryUsedBytes = metrics.MemoryUsedBytes,
                        memoryTotalBytes = metrics.MemoryTotalBytes,
                        diskUsagePercent = metrics.DiskUsagePercent,
                        diskUsedBytes = metrics.DiskUsedBytes,
                        diskTotalBytes = metrics.DiskTotalBytes,
                        networkSentBytesPerSec = metrics.NetworkSentBytesPerSec,
                        networkReceivedBytesPerSec = metrics.NetworkReceivedBytesPerSec,
                        cpuTemperature = metrics.CpuTemperature,
                        gpuUsagePercent = metrics.GpuUsagePercent,
                        gpuTemperature = metrics.GpuTemperature,
                        uptimeSeconds = metrics.UptimeSeconds,
                        batteryPercent = metrics.BatteryPercent,
                        isCharging = metrics.IsCharging
                    }, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error sending data: {Error}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(15), ct);
        }
    }

    private async Task AuthenticateAsync()
    {
        if (_connection == null || string.IsNullOrEmpty(_config.AgentId) || string.IsNullOrEmpty(_config.ApiToken))
            return;

        var result = await _connection.InvokeAsync<bool>("AuthenticateAgent",
            Guid.Parse(_config.AgentId), _config.ApiToken);

        _isAuthenticated = result;
        if (result)
            _logger.LogInformation("Agent authenticated successfully");
        else
            _logger.LogError("Agent authentication failed!");
    }

    private void RegisterCommandHandlers()
    {
        if (_connection == null) return;

        _connection.On<string, string, Dictionary<string, string>?>("ExecuteCommand",
            async (commandId, commandType, parameters) =>
        {
            _logger.LogInformation("Executing command {CommandId}: {Type}", commandId, commandType);
            try
            {
                var result = commandType.ToLower() switch
                {
                    "restart" => ExecutePowerCommand("shutdown /r /t 5 /f"),
                    "shutdown" => ExecutePowerCommand("shutdown /s /t 5 /f"),
                    "lock" => ExecutePowerCommand("rundll32.exe user32.dll,LockWorkStation"),
                    "logoff" => ExecutePowerCommand("shutdown /l /f"),
                    _ => $"Unknown command type: {commandType}"
                };

                await _connection.InvokeAsync("CommandResult",
                    Guid.Parse(_config.ComputerId!), commandId, result, true);
            }
            catch (Exception ex)
            {
                await _connection.InvokeAsync("CommandResult",
                    Guid.Parse(_config.ComputerId!), commandId, ex.Message, false);
            }
        });

        _connection.On<Guid>("RequestProcessList", async computerId =>
        {
            var processes = _processManager.GetProcesses();
            var dtos = processes.Select(p => new
            {
                pid = p.Pid, name = p.Name, cpuPercent = p.CpuPercent,
                memoryBytes = p.MemoryBytes, user = p.User, status = p.Status, parentPid = p.ParentPid
            }).ToList();
            await _connection!.InvokeAsync("SendProcessList", computerId, dtos);
        });

        _connection.On<Guid>("RequestServiceList", async computerId =>
        {
            var services = _serviceManager.GetServices();
            var dtos = services.Select(s => new
            {
                serviceName = s.ServiceName, displayName = s.DisplayName,
                status = s.Status, startupType = s.StartupType,
                account = s.Account, description = s.Description
            }).ToList();
            await _connection!.InvokeAsync("SendServiceList", computerId, dtos);
        });

        _connection.On<Guid, string>("RequestFileList", async (computerId, path) =>
        {
            try
            {
                var files = _fileManager.ListDirectory(path);
                var dtos = files.Select(f => new
                {
                    name = f.Name, path = f.Path, type = f.Type,
                    size = f.Size, modified = f.Modified, created = f.Created
                }).ToList();
                await _connection!.InvokeAsync("SendFileList", computerId, path, dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files at {Path}", path);
            }
        });

        _connection.On<Guid>("RequestDriveList", async computerId =>
        {
            var drives = _fileManager.GetDrives();
            var dtos = drives.Select(d => new
            {
                name = d.Name, path = d.Name, totalBytes = d.TotalBytes,
                freeBytes = d.FreeBytes, fileSystem = d.FileSystem, driveType = d.DriveType
            }).ToList();
            await _connection!.InvokeAsync("SendDriveList", computerId, dtos);
        });
    }

    private string ExecutePowerCommand(string command)
    {
        try
        {
            var parts = command.Split(' ', 2);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = parts[0],
                Arguments = parts.Length > 1 ? parts[1] : "",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(psi);
            return "Command executed successfully";
        }
        catch (Exception ex)
        {
            return $"Command failed: {ex.Message}";
        }
    }

    private string? GetCurrentIp()
    {
        try
        {
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                    && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback);

            foreach (var ni in interfaces)
            {
                var props = ni.GetIPProperties();
                if (props.GatewayAddresses.Count == 0) continue;
                var ipv4 = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (ipv4 != null) return ipv4.Address.ToString();
            }
        }
        catch { }
        return null;
    }
}

public record RegistrationResult(Guid AgentId, string ApiToken, DateTime ApiTokenExpiresAt, Guid ComputerId);

/// <summary>
/// Agent configuration persisted to agentsettings.json.
/// </summary>
public class AgentConfig
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "WindowsControlCenter", "Agent", "agentsettings.json");

    public string? ServerUrl { get; set; }
    public string? EnrollmentCode { get; set; }
    public string? AgentId { get; set; }
    public string? ApiToken { get; set; }
    public string? ComputerId { get; set; }
    public string InstallationId { get; set; } = Guid.NewGuid().ToString();

    public static AgentConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AgentConfig>(json) ?? new AgentConfig();
            }

            var localPath = Path.Combine(AppContext.BaseDirectory, "agentsettings.json");
            if (File.Exists(localPath))
            {
                var json = File.ReadAllText(localPath);
                return JsonSerializer.Deserialize<AgentConfig>(json) ?? new AgentConfig();
            }
        }
        catch { }
        return new AgentConfig();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save agent config: {ex.Message}");
        }
    }
}
