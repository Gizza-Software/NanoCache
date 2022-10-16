using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NanoCache.ServerExample;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        /* Memory Cache */
        services.AddMemoryCache();

        /* Register Hosted Services */
        services.AddHostedService<Worker>();
    })
    .ConfigureAppConfiguration((context, config) =>
    {
        var env = context.HostingEnvironment;
        config
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .Build();

await host.RunAsync();
