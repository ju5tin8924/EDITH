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

var pgConn = Environment.GetEnvironmentVariable("EDITH_PG_CONNECTION")
    ?? builder.Configuration.GetConnectionString("EdithPostgres");

if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(pgConn) || pgConn.Contains("EdithSecurePassword2026!"))
    {
        throw new InvalidOperationException("CRITICAL SECURITY ERROR: Database credentials must be provided for Screening API in Production.");
    }
}

bool usePostgres = false;
if (!string.IsNullOrWhiteSpace(pgConn))
{
    try
    {
        using var probeConn = new Npgsql.NpgsqlConnection(pgConn + ";Timeout=2");
        probeConn.Open();
        usePostgres = true;
    }
    catch (Exception ex)
    {
        if (builder.Environment.IsProduction())
        {
            throw new InvalidOperationException(
                "CRITICAL: Failed to connect to PostgreSQL in Production environment. In-memory fallback is disabled in Production to prevent silent data divergence and unpersisted state.", ex);
        }
        usePostgres = false;
    }
}
else if (builder.Environment.IsProduction())
{
    throw new InvalidOperationException("CRITICAL: PostgreSQL connection string must be configured in Production environment.");
}

// Core Screening Services
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
builder.Services.AddHttpClient("OpenSearch", client =>
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

builder.Services.AddSingleton<OpenSearchWatchlistSearchEngine>(sp =>
{
    var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = clientFactory.CreateClient("OpenSearch");
    var normalizer = sp.GetRequiredService<ITextNormalizer>();
    var memSearch = sp.GetRequiredService<InMemoryWatchlistSearchEngine>();
    return new OpenSearchWatchlistSearchEngine(httpClient, normalizer, osOpts, memSearch);
});

builder.Services.AddSingleton<IWatchlistSearchEngine>(sp =>
{
    var openSearch = sp.GetRequiredService<OpenSearchWatchlistSearchEngine>();
    var memSearch = sp.GetRequiredService<InMemoryWatchlistSearchEngine>();
    var jobRepo = sp.GetService<IBulkJobRepository>();
    return new HybridWatchlistSearchEngine(openSearch, jobRepo, memSearch);
});

builder.Services.AddSingleton<IWatchlistOpenSearchSyncService>(sp =>
{
    var openSearch = sp.GetRequiredService<OpenSearchWatchlistSearchEngine>();
    return new WatchlistOpenSearchSyncService(pgConn, openSearch);
});

builder.Services.AddScoped<IScreeningEngine, ScreeningEngine>();

// State, Cache, Case & Bulk Services (PostgreSQL with In-Memory Fallback)
if (usePostgres && !string.IsNullOrWhiteSpace(pgConn))
{
    builder.Services.AddSingleton<IIdempotencyStore>(new PostgreSqlIdempotencyStore(pgConn));
    builder.Services.AddSingleton<IWhitelistService>(new PostgreSqlWhitelistService(pgConn));

    var pgLockMgr = new PostgreSqlCaseLockManager(pgConn);
    builder.Services.AddSingleton<ICaseLockManager>(pgLockMgr);
    builder.Services.AddSingleton<ICaseService>(new PostgreSqlCaseService(pgConn, pgLockMgr));
    builder.Services.AddSingleton<IBulkJobRepository>(new PostgreSqlBulkJobRepository(pgConn));
}
else
{
    builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
    builder.Services.AddSingleton<IWhitelistService, InMemoryWhitelistService>();

    var memLockMgr = new CaseLockManager();
    builder.Services.AddSingleton<ICaseLockManager>(memLockMgr);
    builder.Services.AddSingleton<ICaseService>(new CaseService(memLockMgr));
    builder.Services.AddSingleton<IBulkJobRepository, InMemoryBulkJobRepository>();
}

// Bulk Ingestion Services
builder.Services.AddSingleton<IAmlStreamingParser, AmlStreamingParser>();
builder.Services.AddSingleton<IBulkJobChannel, ChannelBulkJobDispatcher>();
builder.Services.AddSingleton<IBulkProgressNotifier, CallbackProgressNotifier>();

builder.Services.AddScoped<IBulkIngestionPipeline, BulkIngestionPipeline>();
builder.Services.AddHostedService<BulkScreeningBackgroundWorker>();

// Background seed primes the search engine with baseline sanctions (development or explicit seed flag)
if (builder.Environment.IsDevelopment() || string.Equals(Environment.GetEnvironmentVariable("EDITH_ENABLE_BASELINE_SEED"), "true", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHostedService<WatchlistSeedService>();
}

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
