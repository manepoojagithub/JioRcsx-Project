using JioCxRcsWrapper.Application.Auth;
using JioCxRcsWrapper.Application.Campaigns;
using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Dashboard;
using JioCxRcsWrapper.Application.Media;
using JioCxRcsWrapper.Application.Messages;
using JioCxRcsWrapper.Application.Permissions;
using JioCxRcsWrapper.Application.Queue;
using JioCxRcsWrapper.Application.Reports;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Application.Templates;
using JioCxRcsWrapper.Application.Users;
using JioCxRcsWrapper.Application.Webhooks;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace JioCxRcsWrapper.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IClientOnboardingService, ClientOnboardingService>();
        services.AddScoped<IBrandingService, BrandingService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddSingleton<IContactCsvParser, ContactCsvParser>();
        services.AddSingleton<IMediaValidator, MediaValidator>();
        services.AddSingleton<IMessagePayloadService, MessagePayloadService>();
        services.AddSingleton<IQueueRetryPolicy, QueueRetryPolicy>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IPermissionManagementService, PermissionManagementService>();
        services.AddScoped<IMessageTemplateService, MessageTemplateService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserCreditService, UserCreditService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        return services;
    }
}
