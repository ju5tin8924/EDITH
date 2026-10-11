using Edith.Screening.Api.Endpoints;
using Edith.Screening.Api.Services;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;
using Edith.Screening.Core.Search;

var builder = WebApplication.CreateBuilder(args);

// Core Screening Services
builder.Services.AddSingleton<ITextNormalizer, TextNormalizer>();
builder.Services.AddSingleton<INameSimilarityScorer, NameSimilarityScorer>();
builder.Services.AddSingleton<IWatchlistSearchEngine, InMemoryWatchlistSearchEngine>();
builder.Services.AddScoped<IScreeningEngine, ScreeningEngine>();

// State & Cache Services
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
builder.Services.AddSingleton<IWhitelistService, InMemoryWhitelistService>();

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
