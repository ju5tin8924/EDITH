using System.Net;
using System.Text.Json;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Search;

namespace Edith.Screening.Tests;

public sealed class OpenSearchWatchlistSearchEngineTests
{
    private readonly TextNormalizer _normalizer = new();

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _responder(request);
        }
    }

    [Fact]
    public async Task SearchCandidatesAsync_BuildsDisMaxAndPhoneticsQuery()
    {
        var mockResponse = new
        {
            took = 5,
            timed_out = false,
            hits = new
            {
                total = new { value = 1, relation = "eq" },
                max_score = 2.45,
                hits = new[]
                {
                    new
                    {
                        _index = "edith_watchlist_v1",
                        _id = "OFAC-9812",
                        _score = 2.45,
                        _source = new
                        {
                            id = "OFAC-9812",
                            entity_external_id = "OFAC-9812",
                            entity_type = "INDIVIDUAL",
                            full_name = "VIKTOR ANATOLYEVICH BOUT",
                            nationality = "RU",
                            source_list = "OFAC_SDN",
                            category = "SANCTION",
                            is_blocked = true,
                            is_active = true
                        }
                    }
                }
            }
        };

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(mockResponse))
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:9200") };
        var engine = new OpenSearchWatchlistSearchEngine(httpClient, _normalizer);

        var candidates = await engine.SearchCandidatesAsync("Victor Bout", maxResults: 10);

        Assert.Single(candidates);
        Assert.Equal("OFAC-9812", candidates[0].EntityExternalId);
        Assert.Equal("VIKTOR ANATOLYEVICH BOUT", candidates[0].FullName);
        Assert.Equal("SANCTION", candidates[0].Category);

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("dis_max", handler.LastRequestBody);
        Assert.Contains("phonetic_tokens", handler.LastRequestBody);
        Assert.Contains("FKTR", handler.LastRequestBody); // Metaphone for Victor
    }

    [Fact]
    public async Task IndexBatchAsync_SendsNdJsonBulkPayload()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"errors\":false,\"items\":[]}")
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:9200") };
        var engine = new OpenSearchWatchlistSearchEngine(httpClient, _normalizer);

        var entities = new[]
        {
            new WatchlistEntity
            {
                Id = Guid.NewGuid(),
                EntityExternalId = "TEST-100",
                EntityType = "INDIVIDUAL",
                FullName = "JOHN DOE",
                Nationality = "USA",
                SourceList = "TEST_LIST",
                Category = "SANCTION"
            }
        };

        await engine.IndexBatchAsync(entities);

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"_index\":\"edith_watchlist_v1\"", handler.LastRequestBody);
        Assert.Contains("\"_id\":\"TEST-100\"", handler.LastRequestBody);
        Assert.Contains("\"full_name\":\"JOHN DOE\"", handler.LastRequestBody);
        Assert.Contains("\"phonetic_tokens\"", handler.LastRequestBody);
    }
}
