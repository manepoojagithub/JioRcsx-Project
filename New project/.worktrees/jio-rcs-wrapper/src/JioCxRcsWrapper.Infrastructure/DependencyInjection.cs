using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Common.Options;
using JioCxRcsWrapper.Application.JioCx;
using JioCxRcsWrapper.Application.Reports;
using JioCxRcsWrapper.Infrastructure.Data;
using JioCxRcsWrapper.Infrastructure.Exports;
using JioCxRcsWrapper.Infrastructure.JioCx;
using JioCxRcsWrapper.Infrastructure.Queue;
using JioCxRcsWrapper.Infrastructure.Repositories;
using JioCxRcsWrapper.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JioCxRcsWrapper.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IApiSettingService, ApiSettingService>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ISecretProtector, ApiKeyProtector>();
        services.AddScoped<ICsvReportExporter, CsvReportExporter>();
        services.AddScoped<IPdfReportExporter, PdfReportExporter>();
        services.AddHttpClient<IJioCxClient, JioCxClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<JioCxOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });
        services.AddHostedService<CampaignQueueWorker>();
        var dataProtectionPath = configuration["DataProtection:KeysPath"];
        var dataProtectionBuilder = services.AddDataProtection().SetApplicationName("AdvaitServicesRcsWrapper");
        if (!string.IsNullOrWhiteSpace(dataProtectionPath))
        {
            dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
        }

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}
