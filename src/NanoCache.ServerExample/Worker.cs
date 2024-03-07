namespace NanoCache.ServerExample;

public class Worker : BackgroundService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly NanoCacheServer _cacheServer;

    public Worker(IHostEnvironment host, IMemoryCache cache, ILogger<Worker> logger, IConfiguration configuration)
    {
        this._cache = cache;
        this._logger = logger;
        this._configuration = configuration;

        // Get Settings
        var docker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER").ToBooleanSafe();
        var debugMode = docker
            ? Environment.GetEnvironmentVariable("DEBUG_MODE").ToBooleanSafe()
            : configuration.GetSection("DebugMode").Value.ToBooleanSafe();

        // Run
        this._cacheServer = new NanoCacheServer(this._cache, 5566, debugMode);

        // Log
        _logger.LogInformation($"Caching Service has started in {host.EnvironmentName} mode at {DateTime.Now}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._cacheServer.StartListening();
        await Task.CompletedTask;
    }
}
