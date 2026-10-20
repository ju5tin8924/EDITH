using System.Text;
using Edith.Addons.AiCopilot;
using Edith.Addons.GoAml;
using Edith.Addons.Graph;
using Edith.Cases.Hubs;
using Edith.Cases.Locking;
using Edith.Cases.Services;
using Edith.Events.Webhooks;
using Edith.Gateway.Endpoints;
using Edith.Gateway.Hubs;
using Edith.Gateway.Middleware;
using Edith.Gateway.Models;
using Edith.Gateway.Services;
using Edith.Rules.Engine;
using Edith.Rules.Repository;
using Edith.Rules.Sandbox;
using Edith.Screening.Api.Endpoints;
using Edith.Screening.Api.Services;
using Edith.Screening.Bulk.Delta;
using Edith.Screening.Bulk.Notifications;
using Edith.Screening.Bulk.Parsing;
using Edith.Screening.Bulk.Processing;
using Edith.Screening.Bulk.Repository;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;
using Edith.Screening.Core.Search;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. JWT Bearer Authentication
var secretKey = builder.Configuration["Jwt:Secret"] ?? JwtTokenService.DefaultSecret;
var keyBytes = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Dev / Local container friendly
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? JwtTokenService.Issuer,
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? JwtTokenService.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// 2. Authorization Policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AuthenticatedTenant", policy =>
    {
        policy.RequireAuthenticatedUser();
    });

// 3. CORS Policy for React Vite Frontend & SignalR
builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowVitePortal", policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

// 4. SignalR WebSockets
builder.Services.AddSignalR();

var pgConn = builder.Configuration.GetConnectionString("EdithPostgres")
    ?? Environment.GetEnvironmentVariable("EDITH_PG_CONNECTION")
    ?? "Host=localhost;Port=5432;Database=edith_aml;Username=edith_admin;Password=EdithSecurePassword2026!";

bool usePostgres = false;
try
{
    using var probeConn = new Npgsql.NpgsqlConnection(pgConn + ";Timeout=1");
    probeConn.Open();
    usePostgres = true;
}
catch
{
    usePostgres = false;
}

// 5. IAM & Tenant Resolution Services
builder.Services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IEntitlementsService, EntitlementsService>();

// 6. Screening Engine Services (OpenSearch + PostgreSQL/In-Memory)
builder.Services.AddSingleton<ITextNormalizer, TextNormalizer>();
builder.Services.AddSingleton<INameSimilarityScorer, NameSimilarityScorer>();
builder.Services.AddSingleton<InMemoryWatchlistSearchEngine>();

var openSearchConfig = builder.Configuration.GetSection("OpenSearch");
var osOpts = new OpenSearchOptions
{
    BaseUrl = openSearchConfig.GetValue<string>("BaseUrl") ?? "https://localhost:9200",
    Username = openSearchConfig.GetValue<string>("Username") ?? "admin",
    Password = openSearchConfig.GetValue<string>("Password") ?? "Tr@ns@ct10n$2026!#",
    WatchlistIndex = openSearchConfig.GetValue<string>("WatchlistIndex") ?? "edith_watchlist_v1",
    CustomersIndex = openSearchConfig.GetValue<string>("CustomersIndex") ?? "edith_customers_v1",
    SkipSslValidation = openSearchConfig.GetValue<bool>("SkipSslValidation", true)
};
builder.Services.AddSingleton(osOpts);
builder.Services.AddHttpClient<IWatchlistSearchEngine, OpenSearchWatchlistSearchEngine>(client =>
{
    client.BaseAddress = new Uri(osOpts.BaseUrl);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (osOpts.SkipSslValidation)
    {
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
});

builder.Services.AddScoped<IScreeningEngine, ScreeningEngine>();

if (usePostgres)
{
    builder.Services.AddSingleton<ITenantRegistry>(new PostgreSqlTenantRegistry(pgConn));
    builder.Services.AddSingleton<IIamUserService>(new PostgreSqlIamUserService(pgConn));
    builder.Services.AddSingleton<IIdempotencyStore>(new PostgreSqlIdempotencyStore(pgConn));
    builder.Services.AddSingleton<IWhitelistService>(new PostgreSqlWhitelistService(pgConn));

    var pgLockMgr = new PostgreSqlCaseLockManager(pgConn);
    builder.Services.AddSingleton<ICaseLockManager>(pgLockMgr);
    builder.Services.AddSingleton<ICaseService>(new PostgreSqlCaseService(pgConn, pgLockMgr));
    builder.Services.AddSingleton<IWebhookDeliveryLedger>(new PostgreSqlWebhookDeliveryLedger(pgConn));

    builder.Services.AddSingleton<IRuleRepository>(new PostgreSqlRuleRepository(pgConn));
    builder.Services.AddSingleton<IBulkJobRepository>(new PostgreSqlBulkJobRepository(pgConn));

    builder.Services.AddSingleton<IKycService>(new PostgreSqlKycService(pgConn));
    builder.Services.AddSingleton<ISystemMastersService>(new PostgreSqlSystemMastersService(pgConn));
    builder.Services.AddSingleton<IAuditLedgerService>(new PostgreSqlAuditLedgerService(pgConn));
}
else
{
    builder.Services.AddSingleton<ITenantRegistry, InMemoryTenantRegistry>();
    builder.Services.AddSingleton<IIamUserService, InMemoryIamUserService>();
    builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
    builder.Services.AddSingleton<IWhitelistService, InMemoryWhitelistService>();

    var memLockMgr = new CaseLockManager();
    builder.Services.AddSingleton<ICaseLockManager>(memLockMgr);
    builder.Services.AddSingleton<ICaseService>(new CaseService(memLockMgr));
    builder.Services.AddSingleton<IWebhookDeliveryLedger, InMemoryWebhookDeliveryLedger>();

    builder.Services.AddSingleton<IRuleRepository, InMemoryRuleRepository>();
    builder.Services.AddSingleton<IBulkJobRepository, InMemoryBulkJobRepository>();

    builder.Services.AddSingleton<IKycService, InMemoryKycService>();
    builder.Services.AddSingleton<ISystemMastersService, InMemorySystemMastersService>();
    builder.Services.AddSingleton<IAuditLedgerService, InMemoryAuditLedgerService>();
}

builder.Services.AddHttpClient<ICbsWebhookDispatcher, CbsWebhookDispatcher>();

// 8. AML Rules & Backtesting Sandbox
builder.Services.AddSingleton<IRuleEngine, RuleEngine>();
builder.Services.AddSingleton<IRuleBacktester, RuleBacktester>();

// 9. Bulk Screening & Delta Sync Services
builder.Services.AddScoped<IBulkScreeningProcessor, BulkScreeningProcessor>();
builder.Services.AddSingleton<IDeltaRescreeningService, DeltaRescreeningService>();
builder.Services.AddSingleton<IAmlStreamingParser, AmlStreamingParser>();
builder.Services.AddSingleton<IBulkJobChannel, ChannelBulkJobDispatcher>();
builder.Services.AddSingleton<IBulkProgressNotifier>(sp =>
{
    var hub = sp.GetRequiredService<IHubContext<JobHub, IJobClient>>();
    return new CallbackProgressNotifier(update =>
    {
        _ = hub.Clients.All.JobProgressUpdated(update.JobId.ToString(), update.ProcessedRows, update.TotalRows, update.HitCount, update.Percentage);
        if (update.Status == "COMPLETED")
        {
            _ = hub.Clients.All.JobCompleted(update.JobId.ToString(), update.HitCount);
        }
    });
});
builder.Services.AddScoped<IBulkIngestionPipeline, BulkIngestionPipeline>();
builder.Services.AddHostedService<BulkScreeningBackgroundWorker>();

// 10. Add-on Services (goAML, AI Copilot, Entity Graph)
builder.Services.AddSingleton<IGoAmlExporter, GoAmlExporter>();
builder.Services.AddSingleton<IStrNarrativeCopilot, StrNarrativeCopilot>();
builder.Services.AddSingleton<IEntityGraphVisualizer, EntityGraphVisualizer>();

// 11. Background Seed Service for Development
builder.Services.AddHostedService<DevelopmentSeedService>();

// 12. YARP Reverse Proxy (used in containerized production mode)
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// 13. Pipeline Configuration
app.UseCors("AllowVitePortal");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();

// 14. Map All Modular Minimal API Endpoints
app.MapIamEndpoints();
app.MapScreeningEndpoints(includeHealth: false);
app.MapCaseEndpoints();
app.MapRuleEndpoints();
app.MapBulkScreeningEndpoints(pgConn);
app.MapAddonEndpoints();
app.MapKycEndpoints();
app.MapSettingsEndpoints();

// 15. Map Real-Time SignalR WebSockets Hubs
app.MapHub<CaseHub>("/hubs/cases");
app.MapHub<JobHub>("/hubs/jobs");

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Edith.Gateway",
    version = "1.3.0",
    modules = Edith.Gateway.Program.ActiveModules,
    timestamp = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.Run();

namespace Edith.Gateway
{
    public partial class Program
    {
        protected Program() { }
        internal static readonly string[] ActiveModules = ["IAM", "Screening", "Cases", "Rules", "Bulk", "Addons", "KYC", "Settings"];
    }
}
