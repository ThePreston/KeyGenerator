using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.UserKeyManagement.API.Models;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Net;

namespace Microsoft.UserKeyManagement.API;

public class KeyGenRequest
{
    private readonly ILogger<KeyGenRequest> _logger;
    private readonly IConfiguration _config;
    private readonly SecretClient _secretClient;
    private readonly IConnectionMultiplexer _redis;

    public KeyGenRequest(ILogger<KeyGenRequest> logger, IConfiguration configuration, SecretClient secretClient, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _config = configuration;
        _secretClient = secretClient;
        _redis = redis;
    }

    [Function("KeyGenRequest")]
    [OpenApiOperation(operationId: "KeyGenRequest", tags: new[] { "keygen" })]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(KeyValModel), Required = true, Description = "Key generation request payload")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "The OK response")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
    {

        _logger.LogInformation("C# HTTP trigger function processed a request.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var kv = JsonConvert.DeserializeObject<KeyValModel>(requestBody);


        if (kv == null || string.IsNullOrEmpty(kv.UserName) || string.IsNullOrEmpty(kv.APIKey))
            return new BadRequestObjectResult($"Invalid request payload. Body = {req.Body}");

        _logger.LogInformation($"configs kv.name = {kv.UserName}; kv.apiKey {kv.APIKey}");

        await SetSecretInKeyVaultAsync(kv.UserName, kv.APIKey);
        
        if (Convert.ToBoolean(_config["SaveToCache"]))
            await SetKeyValueInRedisAsync(kv.APIKey, kv.UserName);

        return new OkObjectResult(JsonConvert.SerializeObject(kv));

    }

    [Function("CacheValueRequest")]
    [OpenApiOperation(operationId: "CacheValueRequest", tags: new[] { "valreq" })]
    [OpenApiParameter(name: "cacheKey", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The cache key to retrieve")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(KeyValModel), Description = "The OK response")]
    public async Task<IActionResult> Valreq([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req)
    {

        _logger.LogInformation("C# HTTP trigger function processed a request.");

        ObjectResult res;
        var kv = new CacheValModel();

        try
        {

            var cacheKey = req.Query["cacheKey"];

            if (string.IsNullOrEmpty(cacheKey))
                res = new BadRequestObjectResult("Missing or empty 'cacheKey' query parameter.");
            else
            {
                kv.CacheKey = cacheKey;

                _logger.LogInformation($"configs kv.cacheKey = {kv.CacheKey}");

                string cacheValue = await GetCacheValueInRedisAsync(kv.CacheKey);

                if (string.IsNullOrEmpty(cacheValue))
                    res = new NotFoundObjectResult($"Cache key '{kv.CacheKey}' not found.");
                else
                    res = new OkObjectResult(new KeyValModel { APIKey = kv.CacheKey, UserName = cacheValue });
            }
        }
        catch (Exception ex)
        {
            res = new BadRequestObjectResult($"Error retrieving cache key '{kv.CacheKey}': {ex.Message}");
        }

        return res;
    }

    public async Task SetSecretInKeyVaultAsync(string key, string value)
    {
        await _secretClient.SetSecretAsync(key, value);
        _logger.LogInformation($"Secret '{key}' was set in Key Vault '{_secretClient.VaultUri}'.");
    }

    public async Task SetKeyValueInRedisAsync(string key, string value)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, value);
        _logger.LogInformation($"Key '{key}' was set in Redis.");
    }

    public async Task<string> GetCacheValueInRedisAsync(string key)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        _logger.LogInformation($"Key '{key}' was retrieved from Redis.");
        return value.ToString() ?? "";
    }

}