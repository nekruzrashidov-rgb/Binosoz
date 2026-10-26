using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.Entities;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;
using Sitora.Infrastructure.Jobs;
using Sitora.Infrastructure.Security;
using Sitora.Infrastructure.Services;

namespace Sitora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // Строку подключения читаем при создании контекста, а не при регистрации,
        // чтобы её можно было подменить в тестах и окружениях.
        services.AddDbContext<AppDbContext>((sp, options) => options.UseNpgsql(GetConnectionString(sp)));

        services.AddIdentityCore<AppUser>(options =>
            {
                // У покупателей нет почты; уникальность почты сотрудников держит индекс EmailIndex.
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddErrorDescriber<RussianIdentityErrorDescriber>()
            .AddEntityFrameworkStores<AppDbContext>()
            // TOTP-коды приложения-аутентификатора — второй фактор создателя платформы.
            .AddTokenProvider<AuthenticatorTokenProvider<AppUser>>(TokenOptions.DefaultAuthenticatorProvider);

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<EncryptionOptions>()
            .BindConfiguration(EncryptionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.SectionName)
            .ValidateDataAnnotations();
        services.AddOptions<BrandOptions>()
            .BindConfiguration(BrandOptions.SectionName);
        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddMemoryCache();
        services.AddSingleton<CatalogCache>();
        services.AddSingleton<EncryptionService>();

        services.AddScoped<JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());
        services.AddScoped<StaffPolicy>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ApartmentImporter>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDealService, DealService>();
        services.AddScoped<ICrmService, CrmService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<DealAccess>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddScoped<StatementService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IConstructionService, ConstructionService>();
        services.AddSingleton<FileLinkSigner>();
        services.AddScoped<IFileLinkService, FileLinkService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IChatService, ChatService>();

        services.AddScoped<INotificationSender, TelegramNotificationSender>();
        services.AddSingleton<ISmsSender, SmsSender>();
        AddEmailSender(services);
        services.AddSingleton<IRateProvider, NbtRateProvider>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IPdfGenerator, QuestPdfGenerator>();

        services.AddHangfire((sp, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(GetConnectionString(sp))));
        services.AddScoped<ExpireBookingsJob>();
        services.AddScoped<UpdateRatesJob>();
        services.AddScoped<HealthAlertJob>();
        services.AddSingleton<HealthAlertState>();

        return services;
    }

    public static void ScheduleRecurringJobs(this IServiceProvider services)
    {
        var jobs = services.GetRequiredService<IRecurringJobManager>();
        jobs.AddOrUpdate<ExpireBookingsJob>(ExpireBookingsJob.Id, job => job.RunAsync(CancellationToken.None), ExpireBookingsJob.Schedule);
        jobs.AddOrUpdate<UpdateRatesJob>(UpdateRatesJob.Id, job => job.RunAsync(CancellationToken.None), UpdateRatesJob.Schedule);
        jobs.AddOrUpdate<HealthAlertJob>(HealthAlertJob.Id, job => job.RunAsync(CancellationToken.None), HealthAlertJob.Schedule);
    }

    // Почтовый сервис выбирается настройкой Email__Provider: Smtp, Resend или Dev (письма в лог, только для разработки).
    private static void AddEmailSender(IServiceCollection services)
    {
        services.AddHttpClient<ResendEmailSender>();
        services.AddSingleton<DevEmailSender>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddScoped<IEmailSender>(sp =>
            sp.GetRequiredService<IConfiguration>()[$"{EmailOptions.SectionName}:Provider"]?.Trim().ToLowerInvariant() switch
            {
                "smtp" => sp.GetRequiredService<SmtpEmailSender>(),
                "resend" => sp.GetRequiredService<ResendEmailSender>(),
                _ => sp.GetRequiredService<DevEmailSender>()
            });
    }

    private static string GetConnectionString(IServiceProvider sp)
    {
        var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Не задана строка подключения (переменная окружения ConnectionStrings__Default)");
        return connectionString;
    }
}
