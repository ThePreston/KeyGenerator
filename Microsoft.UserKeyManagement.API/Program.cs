using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register SecretClient for Azure Key Vault
builder.Services.AddSingleton(provider =>
{
    var keyVaultUrl = Environment.GetEnvironmentVariable("KeyVaultUri");
    return new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
});

// Register Redis ConnectionMultiplexer
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    //var configuration = sp.GetRequiredService<IConfiguration>();

    var redisConnectionString = Environment.GetEnvironmentVariable("RedisConn"); //configuration.GetConnectionString("RedisConn");
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
