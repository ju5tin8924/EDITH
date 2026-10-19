using Edith.Cases.Locking;
using Edith.Cases.Services;
using Edith.Screening.Api.Endpoints;
using Edith.Screening.Api.Services;
using Edith.Screening.Bulk.Notifications;
using Edith.Screening.Bulk.Parsing;
using Edith.Screening.Bulk.Processing;
using Edith.Screening.Bulk.Repository;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;
using Edith.Screening.Core.Search;

var builder = WebApplication.CreateBuilder(args);

var pgConn = builder.Configuration.GetConnectionString("EdithPostgres")
    ?? Environment.GetEnvironmentVariable("EDITH_PG_CONNECTION")
    ?? "Host=localhost;Port=5432;Database=edith_aml;Username=edith_admin;Password=EdithSecurePassword2026!";

// Core Screening Services
builder.Services.AddSingleton<ITextNormalizer, TextNormalizer>();
builder.Services.AddSingleton<INameSimilarityScorer, NameSimilarityScorer>();

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

// State & Cache Services (PostgreSQL)
builder.Services.AddSingleton<IIdempotencyStore>(new PostgreSqlIdempotencyStore(pgConn));
builder.Services.AddSingleton<IWhitelistService>(new PostgreSqlWhitelistService(pgConn));

// Case Management (PostgreSQL)
var pgLockMgr = new PostgreSqlCaseLockManager(pgConn);
builder.Services.AddSingleton<ICaseLockManager>(pgLockMgr);
builder.Services.AddSingleton<ICaseService>(new PostgreSqlCaseService(pgConn, pgLockMgr));

// Bulk Ingestion Services (PostgreSQL)
builder.Services.AddSingleton<IAmlStreamingParser, AmlStreamingParser>();
builder.Services.AddSingleton<IBulkJobChannel, ChannelBulkJobDispatcher>();
builder.Services.AddSingleton<IBulkProgressNotifier, CallbackProgressNotifier>();
builder.Services.AddSingleton<IBulkJobRepository>(new PostgreSqlBulkJobRepository(pgConn));

builder.Services.AddScoped<IBulkIngestionPipeline, BulkIngestionPipeline>();
builder.Services.AddHostedService<BulkScreeningBackgroundWorker>();

// Background seed primes the search engine with baseline sanctions
builder.Services.AddHostedService<WatchlistSeedService>();

var app = builder.Build();

app.UseRouting();

// Map Screening and Health Endpoints
app.MapScreeningEndpoints();

app.Run();

// Required for WebApplicationFactory<Program> in integration test projects
public partial class Program
{
    protected Program() { }
}
