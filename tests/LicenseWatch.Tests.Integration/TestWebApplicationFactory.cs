using System.Net.Http;
using LicenseWatch.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LicenseWatch.Tests.Integration;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _rootPath;

    public TestWebApplicationFactory()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "LicenseWatchTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        Directory.CreateDirectory(Path.Combine(_rootPath, "App_Data"));
        Directory.CreateDirectory(Path.Combine(_rootPath, "App_Data", "keys"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(_rootPath);
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var appData = Path.Combine(_rootPath, "App_Data");
            var identityDb = Path.Combine(appData, "licensewatch.identity.db");
            var appDb = Path.Combine(appData, "licensewatch.app.db");
            var hangfireDb = Path.Combine(appData, "hangfire.db");

            EnsureFile(identityDb);
            EnsureFile(appDb);
            EnsureFile(hangfireDb);

            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = $"Data Source={identityDb}",
                ["ConnectionStrings:AppDb"] = $"Data Source={appDb}"
            };

            config.AddInMemoryCollection(settings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            Directory.Delete(_rootPath, true);
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private static void EnsureFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, Array.Empty<byte>());
        }
    }
}
