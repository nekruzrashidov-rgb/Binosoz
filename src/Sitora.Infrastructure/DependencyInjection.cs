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
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<EncryptionOptions>()
            .BindConfiguration(EncryptionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMemoryCache();
        services.AddSingleton<CatalogCache>();
        services.AddSingleton<EncryptionService>();

        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
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
        services.AddScoped<IChatService, ChatService>();

        services.AddScoped<INotificationSender, TelegramNotificationSender>();
        services.AddSingleton<ISmsSender, SmsSender>();
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

        return services;
    }

    public static void ScheduleRecurringJobs(this IServiceProvider services)
    {
        var jobs = services.GetRequiredService<IRecurringJobManager>();
        jobs.AddOrUpdate<ExpireBookingsJob>(ExpireBookingsJob.Id, job => job.RunAsync(CancellationToken.None), ExpireBookingsJob.Schedule);
        jobs.AddOrUpdate<UpdateRatesJob>(UpdateRatesJob.Id, job => job.RunAsync(CancellationToken.None), UpdateRatesJob.Schedule);
    }

    private static string GetConnectionString(IServiceProvider sp)
    {
        var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Не задана строка подключения (переменная окружения ConnectionStrings__Default)");
        return connectionString;
    }
}
