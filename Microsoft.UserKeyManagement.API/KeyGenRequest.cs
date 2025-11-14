using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    public async Task <IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
    {

        _logger.LogInformation("C# HTTP trigger function processed a request.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var kv = JsonConvert.DeserializeObject<KeyValModel>(requestBody);
        

        if (kv == null || string.IsNullOrEmpty(kv.userName) || string.IsNullOrEmpty(kv.apiKey))        
            return new BadRequestObjectResult($"Invalid request payload. Body = {req.Body}");

        _logger.LogInformation($"configs kv.name = {kv.userName}; kv.apiKey {kv.apiKey}");

        await SetSecretInKeyVaultAsync(kv.userName, kv.apiKey);
        await SetKeyValueInRedisAsync(kv.apiKey, kv.userName);

        return new OkObjectResult(JsonConvert.SerializeObject(kv));//$"name = {kv.name} apiKey = {kv.apiKey}. This HTTP triggered function executed successfully.");

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

}