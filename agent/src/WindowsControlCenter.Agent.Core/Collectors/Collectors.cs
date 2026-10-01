using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace WindowsControlCenter.Agent.Core.Collectors;

/// <summary>
/// Collects real system information using Windows APIs and WMI.
/// </summary>
public class SystemCollector
{
    private readonly ILogger<SystemCollector> _logger;

    public SystemCollector(ILogger<SystemCollector> logger) => _logger = logger;

    public SystemInfo CollectSystemInfo()
    {
        var info = new SystemInfo();

        try
        {
            info.ComputerName = Environment.MachineName;
            info.Hostname = System.Net.Dns.GetHostName();
            info.OsArchitecture = RuntimeInformation.OSArchitecture.ToString();
            info.OsDescription = RuntimeInformation.OSDescription;

            // Get OS info from WMI
            using var osSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            foreach (var os in osSearcher.Get())
            {
                info.OsName = os["Caption"]?.ToString()?.Trim();
                info.OsVersion = os["Version"]?.ToString();
                info.OsBuild = os["BuildNumber"]?.ToString();
                info.OsEdition = os["Caption"]?.ToString()?.Trim();
                info.TotalMemoryBytes = Convert.ToInt64(os["TotalVisibleMemorySize"]) * 1024; // KB to bytes
            }

            // Get CPU info
            using var cpuSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (var cpu in cpuSearcher.Get())
            {
                info.CpuName = cpu["Name"]?.ToString()?.Trim();
                info.CpuCores = Convert.ToInt32(cpu["NumberOfCores"]);
                info.CpuThreads = Convert.ToInt32(cpu["NumberOfLogicalProcessors"]);
            }

            // Get GPU info
            using var gpuSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (var gpu in gpuSearcher.Get())
            {
                info.GpuName = gpu["Name"]?.ToString()?.Trim();
                break;
            }

            // Get Motherboard
            using var mbSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (var mb in mbSearcher.Get())
            {
                info.Motherboard = $"{mb["Manufacturer"]} {mb["Product"]}".Trim();
                info.SerialNumber = mb["SerialNumber"]?.ToString();
            }

            // Get BIOS
            using var biosSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (var bios in biosSearcher.Get())
            {
                info.BiosVersion = bios["SMBIOSBIOSVersion"]?.ToString();
            }

            // Get Machine ID (SMBIOS UUID)
            using var csSearcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
            foreach (var cs in csSearcher.Get())
            {
                info.MachineId = cs["UUID"]?.ToString();
            }

            // Get Domain
            using var domainSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (var cs in domainSearcher.Get())
            {
                info.Domain = cs["Domain"]?.ToString();
            }

            // Network info
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            var primaryNic = networkInterfaces.FirstOrDefault(n =>
                n.GetIPProperties().GatewayAddresses.Count > 0);

            if (primaryNic != null)
            {
                var ipProps = primaryNic.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                info.IpAddress = ipv4?.Address.ToString();
                info.MacAddress = FormatMacAddress(primaryNic.GetPhysicalAddress().GetAddressBytes());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting system information");
        }

        return info;
    }

    private static string FormatMacAddress(byte[] bytes)
    {
        if (bytes.Length == 0) return string.Empty;
        return string.Join("-", bytes.Select(b => b.ToString("X2")));
    }
}

/// <summary>
/// Collects real-time monitoring metrics using Windows performance counters and WMI.
/// </summary>
public class MonitoringCollector
{
    private readonly ILogger<MonitoringCollector> _logger;
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _ramCounter;

    public MonitoringCollector(ILogger<MonitoringCollector> logger)
    {
        _logger = logger;
        InitializeCounters();
    }

    private void InitializeCounters()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            // First call always returns 0 for CPU, so call once to prime
            _cpuCounter.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize performance counters. Falling back to WMI.");
        }
    }

    public MonitoringData Collect()
    {
        var data = new MonitoringData { Timestamp = DateTime.UtcNow };

        try
        {
            // CPU usage
            if (_cpuCounter != null)
            {
                data.CpuUsagePercent = Math.Round(_cpuCounter.NextValue(), 1);
            }
            else
            {
                data.CpuUsagePercent = GetCpuUsageWmi();
            }

            // Memory
            var memInfo = GetMemoryInfo();
            data.MemoryTotalBytes = memInfo.Total;
            data.MemoryUsedBytes = memInfo.Used;
            data.MemoryUsagePercent = memInfo.Total > 0
                ? Math.Round((double)memInfo.Used / memInfo.Total * 100, 1)
                : 0;

            // Disk
            var diskInfo = GetPrimaryDiskInfo();
            data.DiskTotalBytes = diskInfo.Total;
            data.DiskUsedBytes = diskInfo.Used;
            data.DiskUsagePercent = diskInfo.Total > 0
                ? Math.Round((double)diskInfo.Used / diskInfo.Total * 100, 1)
                : 0;

            // Network
            var netInfo = GetNetworkThroughput();
            data.NetworkSentBytesPerSec = netInfo.Sent;
            data.NetworkReceivedBytesPerSec = netInfo.Received;

            // Uptime
            data.UptimeSeconds = (long)TimeSpan.FromMilliseconds(Environment.TickCount64).TotalSeconds;

            // Battery
            var battery = GetBatteryInfo();
            data.BatteryPercent = battery.Percent;
            data.IsCharging = battery.IsCharging;

            // Temperature (best effort, may not be available)
            data.CpuTemperature = GetCpuTemperature();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting monitoring data");
        }

        return data;
    }

    private double GetCpuUsageWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                return Convert.ToDouble(obj["LoadPercentage"]);
            }
        }
        catch { }
        return 0;
    }

    private (long Total, long Used) GetMemoryInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var total = Convert.ToInt64(obj["TotalVisibleMemorySize"]) * 1024;
                var free = Convert.ToInt64(obj["FreePhysicalMemory"]) * 1024;
                return (total, total - free);
            }
        }
        catch { }
        return (0, 0);
    }

    private (long Total, long Used) GetPrimaryDiskInfo()
    {
        try
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var driveInfo = new DriveInfo(systemDrive);
            if (driveInfo.IsReady)
            {
                return (driveInfo.TotalSize, driveInfo.TotalSize - driveInfo.TotalFreeSpace);
            }
        }
        catch { }
        return (0, 0);
    }

    private long _lastSent;
    private long _lastReceived;
    private DateTime _lastNetworkCheck = DateTime.MinValue;

    private (double Sent, double Received) GetNetworkThroughput()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            long totalSent = 0, totalReceived = 0;
            foreach (var ni in interfaces)
            {
                var stats = ni.GetIPv4Statistics();
                totalSent += stats.BytesSent;
                totalReceived += stats.BytesReceived;
            }

            var now = DateTime.UtcNow;
            var elapsed = (now - _lastNetworkCheck).TotalSeconds;

            double sentPerSec = 0, receivedPerSec = 0;
            if (_lastNetworkCheck != DateTime.MinValue && elapsed > 0)
            {
                sentPerSec = (totalSent - _lastSent) / elapsed;
                receivedPerSec = (totalReceived - _lastReceived) / elapsed;
            }

            _lastSent = totalSent;
            _lastReceived = totalReceived;
            _lastNetworkCheck = now;

            return (Math.Max(0, sentPerSec), Math.Max(0, receivedPerSec));
        }
        catch { }
        return (0, 0);
    }

    private (double? Percent, bool? IsCharging) GetBatteryInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            foreach (var obj in searcher.Get())
            {
                var percent = Convert.ToDouble(obj["EstimatedChargeRemaining"]);
                var status = Convert.ToInt32(obj["BatteryStatus"]);
                // BatteryStatus: 2 = AC, 1 = discharging
                return (percent, status == 2);
            }
        }
        catch { }
        return (null, null); // No battery
    }

    private double? GetCpuTemperature()
    {
        try
        {
            // MSAcpi_ThermalZoneTemperature requires admin privileges
            using var searcher = new ManagementObjectSearcher(@"root\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (var obj in searcher.Get())
            {
                var tempKelvin = Convert.ToDouble(obj["CurrentTemperature"]) / 10.0;
                return Math.Round(tempKelvin - 273.15, 1); // Kelvin to Celsius
            }
        }
        catch { }
        return null;
    }
}

/// <summary>
/// Manages Windows processes.
/// </summary>
public class ProcessManager
{
    private readonly ILogger<ProcessManager> _logger;

    public ProcessManager(ILogger<ProcessManager> logger) => _logger = logger;

    public List<ProcessInfo> GetProcesses()
    {
        var result = new List<ProcessInfo>();
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    result.Add(new ProcessInfo
                    {
                        Pid = proc.Id,
                        Name = proc.ProcessName,
                        MemoryBytes = proc.WorkingSet64,
                        Status = proc.Responding ? "Running" : "Not Responding",
                        // CPU requires PerformanceCounter for real-time calculation, which is expensive per-process
                        CpuPercent = 0
                    });
                }
                catch { }
                finally { proc.Dispose(); }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process list");
        }
        return result;
    }

    public bool KillProcess(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            proc.Kill();
            _logger.LogInformation("Killed process {Pid} ({Name})", pid, proc.ProcessName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to kill process {Pid}", pid);
            return false;
        }
    }
}

/// <summary>
/// Manages Windows services.
/// </summary>
public class ServiceManager
{
    private readonly ILogger<ServiceManager> _logger;

    public ServiceManager(ILogger<ServiceManager> logger) => _logger = logger;

    public List<ServiceInfo> GetServices()
    {
        var result = new List<ServiceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service");
            foreach (var svc in searcher.Get())
            {
                result.Add(new ServiceInfo
                {
                    ServiceName = svc["Name"]?.ToString() ?? "",
                    DisplayName = svc["DisplayName"]?.ToString() ?? "",
                    Status = svc["State"]?.ToString() ?? "Unknown",
                    StartupType = svc["StartMode"]?.ToString() ?? "Unknown",
                    Account = svc["StartName"]?.ToString(),
                    Description = svc["Description"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service list");
        }
        return result;
    }

    public bool ControlService(string serviceName, string action)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Service WHERE Name='{serviceName}'");
            foreach (ManagementObject svc in searcher.Get())
            {
                var result = action.ToLower() switch
                {
                    "start" => svc.InvokeMethod("StartService", null),
                    "stop" => svc.InvokeMethod("StopService", null),
                    "restart" => RestartService(svc),
                    "pause" => svc.InvokeMethod("PauseService", null),
                    "resume" => svc.InvokeMethod("ResumeService", null),
                    _ => throw new ArgumentException($"Unknown action: {action}")
                };

                _logger.LogInformation("Service {Action} {Service}: result = {Result}", action, serviceName, result);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to {Action} service {Service}", action, serviceName);
        }
        return false;
    }

    private static object? RestartService(ManagementObject svc)
    {
        svc.InvokeMethod("StopService", null);
        Thread.Sleep(2000);
        return svc.InvokeMethod("StartService", null);
    }
}

/// <summary>
/// Manages files and directories on the remote computer.
/// </summary>
public class FileManager
{
    private readonly ILogger<FileManager> _logger;

    public FileManager(ILogger<FileManager> logger) => _logger = logger;

    public List<DriveInfoDto> GetDrives()
    {
        var result = new List<DriveInfoDto>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            result.Add(new DriveInfoDto
            {
                Name = drive.Name,
                Label = drive.VolumeLabel,
                FileSystem = drive.DriveFormat,
                TotalBytes = drive.TotalSize,
                FreeBytes = drive.TotalFreeSpace,
                DriveType = drive.DriveType.ToString()
            });
        }
        return result;
    }

    public List<FileEntryInfo> ListDirectory(string path)
    {
        var result = new List<FileEntryInfo>();
        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException($"Directory not found: {path}");

            foreach (var dir in dirInfo.GetDirectories())
            {
                try
                {
                    result.Add(new FileEntryInfo
                    {
                        Name = dir.Name,
                        Path = dir.FullName,
                        Type = "Directory",
                        Modified = dir.LastWriteTimeUtc,
                        Created = dir.CreationTimeUtc
                    });
                }
                catch { } // Access denied
            }

            foreach (var file in dirInfo.GetFiles())
            {
                try
                {
                    result.Add(new FileEntryInfo
                    {
                        Name = file.Name,
                        Path = file.FullName,
                        Type = "File",
                        Size = file.Length,
                        Modified = file.LastWriteTimeUtc,
                        Created = file.CreationTimeUtc
                    });
                }
                catch { } // Access denied
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing directory {Path}", path);
            throw;
        }
        return result;
    }
}

/// <summary>
/// Collects installed software inventory.
/// </summary>
public class SoftwareCollector
{
    private readonly ILogger<SoftwareCollector> _logger;

    public SoftwareCollector(ILogger<SoftwareCollector> logger) => _logger = logger;

    public List<SoftwareInfo> GetInstalledSoftware()
    {
        var result = new List<SoftwareInfo>();
        try
        {
            var registryPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var path in registryPaths)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var name = subKey.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    result.Add(new SoftwareInfo
                    {
                        Name = name,
                        Version = subKey.GetValue("DisplayVersion")?.ToString(),
                        Publisher = subKey.GetValue("Publisher")?.ToString(),
                        InstallDate = subKey.GetValue("InstallDate")?.ToString(),
                        Architecture = path.Contains("WOW6432Node") ? "x86" : "x64"
                    });
                }
            }

            // Deduplicate by name
            result = result.DistinctBy(s => s.Name).OrderBy(s => s.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting software inventory");
        }
        return result;
    }
}

// ===== Data Models =====

public class SystemInfo
{
    public string ComputerName { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string? Domain { get; set; }
    public string? MachineId { get; set; }
    public string? OsName { get; set; }
    public string? OsVersion { get; set; }
    public string? OsBuild { get; set; }
    public string? OsEdition { get; set; }
    public string? OsDescription { get; set; }
    public string? OsArchitecture { get; set; }
    public string? CpuName { get; set; }
    public int? CpuCores { get; set; }
    public int? CpuThreads { get; set; }
    public long? TotalMemoryBytes { get; set; }
    public string? GpuName { get; set; }
    public string? Motherboard { get; set; }
    public string? BiosVersion { get; set; }
    public string? SerialNumber { get; set; }
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
}

public class MonitoringData
{
    public DateTime Timestamp { get; set; }
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long MemoryTotalBytes { get; set; }
    public double DiskUsagePercent { get; set; }
    public long DiskUsedBytes { get; set; }
    public long DiskTotalBytes { get; set; }
    public double NetworkSentBytesPerSec { get; set; }
    public double NetworkReceivedBytesPerSec { get; set; }
    public double? CpuTemperature { get; set; }
    public double? GpuUsagePercent { get; set; }
    public double? GpuTemperature { get; set; }
    public long UptimeSeconds { get; set; }
    public double? BatteryPercent { get; set; }
    public bool? IsCharging { get; set; }
}

public class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public double CpuPercent { get; set; }
    public long MemoryBytes { get; set; }
    public string? User { get; set; }
    public string? Status { get; set; }
    public int? ParentPid { get; set; }
}

public class ServiceInfo
{
    public string ServiceName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "";
    public string StartupType { get; set; } = "";
    public string? Account { get; set; }
    public string? Description { get; set; }
}

public class DriveInfoDto
{
    public string Name { get; set; } = "";
    public string? Label { get; set; }
    public string FileSystem { get; set; } = "";
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public string DriveType { get; set; } = "";
}

public class FileEntryInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Type { get; set; } = "";
    public long? Size { get; set; }
    public DateTime? Modified { get; set; }
    public DateTime? Created { get; set; }
}

public class SoftwareInfo
{
    public string Name { get; set; } = "";
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public string? InstallDate { get; set; }
    public string? Architecture { get; set; }
}
