// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Polly;
using Polly.Extensions.Http;

var builder = new HostBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddTransient<RandomSuccessHttpHandler>();
        services.AddHttpClient(Options.DefaultName)
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(GetRetryPolicy())
            .AddHttpMessageHandler<RandomSuccessHttpHandler>();
        
        services.AddScoped<IFakeApiClient, FakeApiClient>();

    }).UseConsoleLifetime();

var host = builder.Build();

var service = host.Services.GetRequiredService<IFakeApiClient>();

try
{

    var result = await service.GetRandomNumberAsync();

    Console.WriteLine("Hello, Result := {0}", result);
}
catch (Exception x)
{
    Console.WriteLine("Failed to get result := {0}", x.Message);
}

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
        .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}
    
static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}

public interface IFakeApiClient
{
    public Task<int> GetRandomNumberAsync();
}

public class FakeApiClient : IFakeApiClient
{
    private readonly HttpClient _httpClient;
    
    public FakeApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("http://localhost/fake");
    }

    public Task<int> GetRandomNumberAsync()
    {
        return _httpClient.GetFromJsonAsync<int>("/foo");
    }
}

public class RandomSuccessHttpHandler : DelegatingHandler
{

    private readonly double _failRate = 0.7;
    
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Console.WriteLine("attempting ...");
        
        if (Random.Shared.NextDouble() <= _failRate)
        {
            return Task.FromResult(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.GatewayTimeout,
                Content = new StringContent("random failed")
            });
        }

        return Task.FromResult(new HttpResponseMessage()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(Random.Shared.Next(1, 100).ToString())
        });
    }
}

