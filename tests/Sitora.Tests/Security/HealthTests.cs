using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Jobs;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

public record HealthBody(string Status, List<HealthCheckBody> Checks);

public record HealthCheckBody(string Name, string Status, int DurationMs);

public class HealthTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Live_HasNoChecks_ReadyChecksDatabaseAndStorage()
    {
        var client = factory.CreateClient();

        var live = await client.GetFromJsonAsync<HealthBody>("/health/live", TestJson.Options);
        Assert.Equal("Healthy", live!.Status);
        Assert.Empty(live.Checks);

        foreach (var url in new[] { "/health/ready", "/health" })
        {
            var ready = await client.GetFromJsonAsync<HealthBody>(url, TestJson.Options);
            Assert.Equal("Healthy", ready!.Status);
            // Сервер фоновых задач в тестах выключен, поэтому и его проверки нет.
            Assert.Equal(["database", "storage"], ready.Checks.Select(c => c.Name).Order());
        }
    }
}

// Хранилище «сломано»: корневая папка указывает на файл, писать в неё нельзя.
public class BrokenStorageFactory : SitoraApiFactory
{
    public readonly string BlockingFile = Path.Combine(Path.GetTempPath(), $"sitora-not-a-folder-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        File.WriteAllText(BlockingFile, "это файл, а не папка");
        base.ConfigureWebHost(builder);
        builder.UseSetting("Storage:RootPath", Path.Combine(BlockingFile, "storage"));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(BlockingFile);
    }
}

public class BrokenStorageHealthTests(BrokenStorageFactory factory) : IClassFixture<BrokenStorageFactory>
{
    [Fact]
    public async Task Ready_Returns503WithFailedCheck_ButLiveStaysUp_AndNoErrorDetailsLeak()
    {
        var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        var body = await ready.Content.ReadAsStringAsync();
        Assert.Contains("\"name\":\"storage\",\"status\":\"Unhealthy\"", body);
        Assert.Contains("\"name\":\"database\",\"status\":\"Healthy\"", body);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain(factory.BlockingFile, body);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
}

public class HealthAlertJobTests
{
    [Fact]
    public async Task Notifies_OnlyOnTransitions()
    {
        var health = new FakeHealthService();
        var sender = new CapturingSender();
        var job = new HealthAlertJob(health, sender, new HealthAlertState(), NullLogger<HealthAlertJob>.Instance);

        await job.RunAsync(CancellationToken.None);
        Assert.Empty(sender.Sent);

        health.Status = HealthStatus.Unhealthy;
        await job.RunAsync(CancellationToken.None);
        await job.RunAsync(CancellationToken.None);
        var alert = Assert.Single(sender.Sent);
        Assert.Contains("не работает", alert);
        Assert.Contains("database", alert);

        health.Status = HealthStatus.Healthy;
        await job.RunAsync(CancellationToken.None);
        await job.RunAsync(CancellationToken.None);
        Assert.Equal(2, sender.Sent.Count);
        Assert.Contains("снова работает", sender.Sent[1]);
    }

    private class FakeHealthService : HealthCheckService
    {
        public HealthStatus Status { get; set; } = HealthStatus.Healthy;

        public override Task<HealthReport> CheckHealthAsync(Func<HealthCheckRegistration, bool>? predicate, CancellationToken ct = default) =>
            Task.FromResult(new HealthReport(
                new Dictionary<string, HealthReportEntry>
                {
                    ["database"] = new(Status, null, TimeSpan.Zero, null, null)
                },
                TimeSpan.Zero));
    }

    private class CapturingSender : INotificationSender
    {
        public List<string> Sent { get; } = [];

        public Task SendAsync(Guid? userId, string text, CancellationToken ct = default)
        {
            Sent.Add(text);
            return Task.CompletedTask;
        }
    }
}

public class HealthProbeTests
{
    [Fact]
    public async Task HealthcheckCommand_ReturnsZeroWhenLiveResponds_AndOneWhenNothingListens()
    {
        var port = Random.Shared.Next(30000, 40000);
        string? receivedHost = null;

        // Мини-сервер вместо API: проверяем, что команда стучится в /health/live с доменом из AllowedHosts.
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        await using var stub = builder.Build();
        stub.MapGet("/health/live", (HttpContext http) =>
        {
            receivedHost = http.Request.Host.Value;
            return Results.Ok();
        });
        await stub.StartAsync();

        Assert.Equal(0, await RunProbeAsync(port, "api.sitora.test"));
        Assert.Equal("api.sitora.test", receivedHost);

        await stub.StopAsync();
        Assert.Equal(1, await RunProbeAsync(port, "api.sitora.test"));
    }

    private static async Task<int> RunProbeAsync(int port, string allowedHosts)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = AppContext.BaseDirectory };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Sitora.WebApi.dll"));
        start.ArgumentList.Add("--healthcheck");
        start.Environment["ASPNETCORE_HTTP_PORTS"] = port.ToString();
        start.Environment["AllowedHosts"] = allowedHosts;

        using var process = Process.Start(start)!;
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        return process.ExitCode;
    }
}
