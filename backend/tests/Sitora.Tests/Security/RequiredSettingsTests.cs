using System.Diagnostics;

namespace Sitora.Tests.Security;

public class RequiredSettingsTests
{
    private static readonly string[] Secrets = ["ConnectionStrings__Default", "Jwt__Key", "Encryption__Key"];

    // Не Development: там API берёт секреты ещё и из user secrets машины, и тест зависел бы от них.
    [Theory]
    [InlineData("Staging", new string[0], "ConnectionStrings__Default, Jwt__Key, Encryption__Key")]
    [InlineData("Production", new[] { "--migrate" }, "ConnectionStrings__Default")]
    public async Task MissingSecrets_AreListedInOneLine_AndApiExitsWithCode1(string environment, string[] args, string expectedList)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Sitora.WebApi.dll"));
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        start.Environment["ASPNETCORE_HTTP_PORTS"] = Random.Shared.Next(40000, 50000).ToString();
        // Процесс наследует окружение тестов — убираем секреты явно, даже если они заданы на машине.
        foreach (var name in Secrets)
            start.Environment.Remove(name);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(1));
        var output = await stdout + await stderr;

        Assert.True(process.ExitCode == 1, output);
        Assert.Contains($"Не заданы переменные окружения: {expectedList}.", output);
        Assert.DoesNotContain("Unhandled exception", output);
    }
}
