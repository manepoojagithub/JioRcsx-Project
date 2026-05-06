using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JioCxRcsWrapper.Infrastructure.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var webProjectPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "JioCxRcsWrapper.Web"));
        if (!Directory.Exists(webProjectPath))
        {
            webProjectPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "JioCxRcsWrapper.Web"));
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? ReadConnectionString(Path.Combine(webProjectPath, "appsettings.json"))
            ?? throw new InvalidOperationException("DefaultConnection is required.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;

        return new AppDbContext(options);
    }

    private static string? ReadConnectionString(string appsettingsPath)
    {
        using var stream = File.OpenRead(appsettingsPath);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();
    }
}
