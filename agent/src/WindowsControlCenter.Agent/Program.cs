using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WindowsControlCenter.Agent;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WindowsControlCenter", "Agent", "Logs", "agent-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .MinimumLevel.Information()
    .CreateLogger();

try
{
    Log.Information("Windows Control Center Agent starting");

    var config = AgentConfig.Load();

    // Check for command line args (for initial setup)
    var cmdArgs = Environment.GetCommandLineArgs();
    for (int i = 1; i < cmdArgs.Length; i++)
    {
        switch (cmdArgs[i].ToLower())
        {
            case "--server-url" when i + 1 < cmdArgs.Length:
                config.ServerUrl = cmdArgs[++i];
                break;
            case "--enrollment-code" when i + 1 < cmdArgs.Length:
                config.EnrollmentCode = cmdArgs[++i];
                break;
        }
    }

    if (!string.IsNullOrEmpty(config.ServerUrl))
        config.Save();

    var builder = Host.CreateApplicationBuilder();
    builder.Services.AddSerilog();

    // Can run as Windows Service
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "WindowsControlCenterAgent";
    });

    builder.Services.AddSingleton(config);
    builder.Services.AddHostedService<AgentWorker>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
