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
        var debugMode = false;
        var useCredentials = false;
        var validCredentials = new List<NanoUserCredentials>();
        var docker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER").ToBooleanSafe();
        if (docker)
        {
            // Multiple users can be added in Docker as follows
            // admin:123456;user:pass;burak:oner
            debugMode = Environment.GetEnvironmentVariable("DEBUG_MODE").ToBooleanSafe();
            useCredentials = Environment.GetEnvironmentVariable("USE_CREDENTIALS").ToBooleanSafe();
            var credentials = Environment.GetEnvironmentVariable("VALID_CREDENTIALS").Trim().Split(';');
            if (credentials != null && credentials.Length > 0)
            {
                foreach (var credential in credentials)
                {
                    var userpass = credential.Split(':');
                    if (userpass != null && userpass.Length == 2)
                    {
                        validCredentials.Add(new NanoUserCredentials(userpass[0].Trim(), userpass[1].Trim()));
                    }
                }
            }
        }
        else
        {
            debugMode = configuration.GetSection("DebugMode").Value.ToBooleanSafe();
            useCredentials = configuration.GetSection("UseCredentials").Value.ToBooleanSafe();
            validCredentials = configuration.GetSection("ValidCredentials").Get<List<NanoUserCredentials>>();
        }

        // Run
        this._cacheServer = new NanoCacheServer(this._cache, 5566, useCredentials, validCredentials, debugMode);

        // Log
        _logger.LogInformation($"Caching Service has started in {host.EnvironmentName} mode at {DateTime.Now}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._cacheServer.StartListening();
        await Task.CompletedTask;
    }
}
