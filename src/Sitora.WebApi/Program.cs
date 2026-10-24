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
using Sitora.Infrastructure.Security;
using Sitora.WebApi.Extensions;
using Sitora.WebApi.Hubs;
using Sitora.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);
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

ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("ru");
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
if (builder.Configuration.GetValue("Hangfire:ServerEnabled", true))
    builder.Services.AddHangfireServer();

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

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

// Режим команды при деплое: применить миграции, создать роли и первого админа и выйти, не поднимая сервер.
if (args.Contains(DatabaseInitializer.MigrateCommand))
{
    await app.Services.InitializeDatabaseAsync();
    app.Logger.LogInformation("Миграции применены, база готова");
    return;
}

if (forwardedHeadersEnabled)
    app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();

// В продакшене TLS завершает обратный прокси; приложение только требует HTTPS и шлёт HSTS.
if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

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
app.UseRateLimiter();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHealthChecks("/health");

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
        Environment.ExitCode = 1;
        return;
    }
}
app.Services.ScheduleRecurringJobs();

app.Run();

public partial class Program;
