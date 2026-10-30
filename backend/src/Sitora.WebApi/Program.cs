using System.Globalization;
using System.Text.Json.Serialization;
using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog.Events;
using Sitora.Infrastructure.Security;
using Sitora.WebApi.Extensions;
using Sitora.WebApi.Hubs;
using Sitora.WebApi.Middleware;
using Sitora.WebApi.Validators;

// Проверка контейнера: опросить уже запущенный API и выйти, не поднимая второй экземпляр.
if (args.Contains(HealthProbe.Command))
    return await HealthProbe.RunAsync();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = FrontendSetup.FindWebRoot()
});
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddInfrastructure();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HubCaller>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSignalR();
builder.Services.AddScoped<IChatNotifier, SignalRChatNotifier>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

ValidationSetup.Configure();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwt) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = JwtTokenService.GetSigningKey(jwt.Value.Key),
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = JwtTokenService.RoleClaimType,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // Браузер не умеет ставить заголовок Authorization для WebSocket, поэтому для хабов
        // токен принимается и из строки запроса (access_token).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// В тестах сервер фоновых задач выключают (Hangfire__ServerEnabled=false) и запускают задачи вручную.
var hangfireServerEnabled = builder.Configuration.GetValue("Hangfire:ServerEnabled", true);
if (hangfireServerEnabled)
    builder.Services.AddHangfireServer();

var healthChecks = builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: [HealthTags.Ready])
    .AddCheck<StorageHealthCheck>("storage", tags: [HealthTags.Ready]);
// Сервер фоновых задач проверяем там, где он запущен; его отказ не мешает отвечать на запросы — Degraded.
if (hangfireServerEnabled)
    healthChecks.AddCheck<BackgroundJobsHealthCheck>("background-jobs", HealthStatus.Degraded, [HealthTags.Ready]);

builder.Services.AddSitoraRateLimiting();
builder.Services.AddSitoraCors();
var forwardedHeadersEnabled = builder.Configuration.GetValue("ForwardedHeaders:Enabled", false);
if (forwardedHeadersEnabled)
    builder.Services.AddSitoraForwardedHeaders();
if (builder.Environment.IsProduction())
{
    builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Sitora API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Access-токен из POST /api/v1/auth/login"
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Какой почтовый сервис выбран: без этого не понять, почему коды входа не приходят (Dev пишет их только в лог).
app.Logger.LogInformation("Почтовый сервис: {Provider}", app.Configuration["Email:Provider"] ?? "Dev");

var migrateOnly = args.Contains(DatabaseInitializer.MigrateCommand);
var missingSettings = RequiredSettings.Missing(app.Configuration, migrateOnly, app.Environment.IsDevelopment());
if (missingSettings.Count > 0)
{
    app.Logger.LogCritical("{Reason}", RequiredSettings.Message(missingSettings));
    return 1;
}

// Режим команды при деплое: применить миграции, создать роли и первого админа и выйти, не поднимая сервер.
if (migrateOnly)
{
    await app.Services.InitializeDatabaseAsync();
    app.Logger.LogInformation("Миграции применены, база готова");
    return 0;
}

if (forwardedHeadersEnabled)
    app.UseForwardedHeaders();
// Журнал запросов — снаружи обработчика исключений: так он видит итоговый код ответа.
// Внутри он принимал каждую ошибку домена (404, 409, 422) за 500 и писал её со стеком.
// Настоящие 500 отдельно пишет DomainExceptionHandler.
app.UseSerilogRequestLogging(options =>
{
    // IP клиента (после X-Forwarded-For) и пользователь — в каждой записи о запросе.
    options.EnrichDiagnosticContext = (diagnostics, http) =>
    {
        diagnostics.Set("ClientIp", http.Connection.RemoteIpAddress?.ToString());
        diagnostics.Set("UserId", http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    };
    // Проверки здоровья идут каждые 30 секунд — в обычный лог их не пишем, ошибки пишем всегда.
    options.GetLevel = (http, _, exception) =>
        exception is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
        : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
        : LogEventLevel.Information;
});
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

// В продакшене TLS завершает обратный прокси; приложение только требует HTTPS и шлёт HSTS.
// /health не перенаправляется: его опрашивают изнутри сети docker по HTTP.
if (app.Environment.IsProduction())
{
    app.UseWhen(http => !http.Request.Path.StartsWithSegments("/health"), branch =>
    {
        branch.UseHsts();
        branch.UseHttpsRedirection();
    });
}

// Скрипты, стили и иконка сайта — до локализации, CORS и авторизации: им они не нужны.
var hasFrontend = app.HasFrontend();
if (hasFrontend)
    app.UseStaticFiles();

app.UseRequestLocalization(options => options
    .SetDefaultCulture("ru")
    .AddSupportedCultures("ru", "tg")
    .AddSupportedUICultures("ru", "tg"));

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(SecuritySetup.CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ImpersonationAuditMiddleware>();
app.UseMiddleware<PresenceMiddleware>();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapSitoraHealthChecks();

// Корень — сайт; если папки сайта нет, вне Production корень ведёт на Swagger, чтобы после dotnet run не видеть пустой 404.
if (hasFrontend)
    app.MapSitoraFrontend();
else if (!app.Environment.IsProduction())
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// В Production миграции применяются только отдельной командой --migrate (сервис migrate в docker-compose.prod.yml).
if (app.Configuration.GetValue("Database:MigrateOnStartup", !app.Environment.IsProduction()))
{
    await app.Services.InitializeDatabaseAsync();
}
else
{
    try
    {
        await app.Services.EnsureDatabaseUpToDateAsync();
    }
    catch (InvalidOperationException e)
    {
        // Одна понятная запись в лог контейнера вместо стека необработанного исключения.
        app.Logger.LogCritical("{Reason}", e.Message);
        return 1;
    }
}
app.Services.ScheduleRecurringJobs();

app.Run();
return 0;

public partial class Program;
