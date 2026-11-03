using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        services.AddOptions<ClaudeOptions>()
            .BindConfiguration(ClaudeOptions.SectionName);
        services.AddOptions<BrandOptions>()
            .BindConfiguration(BrandOptions.SectionName);
        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .PostConfigure<IConfiguration>(EmailOptions.ApplySmtpSection)
            .ValidateDataAnnotations();

        // Redis, если задан Redis__ConnectionString, иначе кэш в памяти. Как и строку подключения к базе,
        // настройку читаем при создании, а не при регистрации, чтобы её можно было подменить в тестах.
        services.AddOptions<RedisOptions>().BindConfiguration(RedisOptions.SectionName);
        services.AddSingleton(sp => RedisOptions.CreateCache(sp.GetRequiredService<IOptions<RedisOptions>>().Value));
        services.AddSingleton<CatalogCache>();
        services.AddSingleton<StatsCache>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddSingleton<EncryptionService>();

        services.AddScoped<JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());
        services.AddScoped<StaffPolicy>();
        services.AddScoped<StaffAccounts>();
        services.AddScoped<ITariffService, TariffService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<IOrgApplicationService, OrgApplicationService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddSingleton<IChatModel, ClaudeChatModel>();
        services.AddScoped<IAiAssistant, AiAssistant>();
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
        services.AddScoped<ICashService, CashService>();
        services.AddScoped<IWeeklyReportService, WeeklyReportService>();
        services.AddScoped<IFloorPlanService, FloorPlanService>();
        services.AddScoped<WeeklyOwnerReportJob>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IConstructionService, ConstructionService>();
        services.AddSingleton<FileLinkSigner>();
        services.AddScoped<IFileLinkService, FileLinkService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDocumentVerifier, ClaudeDocumentVerifier>();
        services.AddScoped<IAiDocumentService, AiDocumentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddSingleton<IPresenceTracker, PresenceTracker>();

        services.AddScoped<INotificationSender, TelegramNotificationSender>();
        services.AddOptions<TelegramOptions>().BindConfiguration(TelegramOptions.SectionName);
        // Токен бота — часть адреса запроса: без журнала HttpClient он не попадает в логи.
        services.AddHttpClient(TelegramBotTransport.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15)).RemoveAllLoggers();
        services.AddSingleton<ITelegramTransport, TelegramBotTransport>();
        services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
        services.AddScoped<TelegramAlertService>();
        services.AddSingleton<ISmsSender, SmsSender>();
        AddEmailSender(services);
        services.AddHttpClient(NbtRateProvider.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(20));
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
        services.AddScoped<PaymentAlertService>();
        services.AddScoped<OverduePaymentJob>();
        services.AddScoped<PaymentReminderJob>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<HealthAlertState>();

        return services;
    }

    public static void ScheduleRecurringJobs(this IServiceProvider services)
    {
        var jobs = services.GetRequiredService<IRecurringJobManager>();
        jobs.AddOrUpdate<ExpireBookingsJob>(ExpireBookingsJob.Id, job => job.RunAsync(CancellationToken.None), ExpireBookingsJob.Schedule);
        jobs.AddOrUpdate<UpdateRatesJob>(UpdateRatesJob.Id, job => job.RunAsync(CancellationToken.None), UpdateRatesJob.Schedule);
        jobs.AddOrUpdate<HealthAlertJob>(HealthAlertJob.Id, job => job.RunAsync(CancellationToken.None), HealthAlertJob.Schedule);
        jobs.AddOrUpdate<OverduePaymentJob>(OverduePaymentJob.Id, job => job.RunAsync(CancellationToken.None), OverduePaymentJob.Schedule);
        jobs.AddOrUpdate<PaymentReminderJob>(PaymentReminderJob.Id, job => job.RunAsync(CancellationToken.None), PaymentReminderJob.Schedule);
        jobs.AddOrUpdate<WeeklyOwnerReportJob>(WeeklyOwnerReportJob.Id, job => job.RunAsync(CancellationToken.None), WeeklyOwnerReportJob.Schedule);
    }

    // Почтовый сервис выбирается настройкой Email__Provider: Smtp, Resend или Dev (письма в лог, только для разработки).
    // Заданный Smtp__Host (короткие переменные) включает SMTP сам.
    private static void AddEmailSender(IServiceCollection services)
    {
        services.AddHttpClient<ResendEmailSender>();
        services.AddSingleton<DevEmailSender>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddScoped<IEmailSender>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var provider = EmailOptions.HasSmtpSection(configuration) ? "smtp" : configuration[$"{EmailOptions.SectionName}:Provider"];
            return provider?.Trim().ToLowerInvariant() switch
            {
                "smtp" => sp.GetRequiredService<SmtpEmailSender>(),
                "resend" => sp.GetRequiredService<ResendEmailSender>(),
                _ => (IEmailSender)sp.GetRequiredService<DevEmailSender>()
            };
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
