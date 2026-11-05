using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;

namespace Edith.Screening.Core.Search;

public sealed record OpenSearchOptions
{
    public string BaseUrl { get; init; } = "https://localhost:9200";
    public string Username { get; init; } = "admin";
    public string Password { get; init; } = "Tr@ns@ct10n$2026!#";
    public string WatchlistIndex { get; init; } = "edith_watchlist_v1";
    public string WatchlistAlias { get; init; } = "edith_watchlist";
    public string WhitelistIndex { get; init; } = "edith_whitelist_v1";
    public string WhitelistAlias { get; init; } = "edith_whitelist";
    public string CustomersIndex { get; init; } = "edith_customers_v1";
    public bool SkipSslValidation { get; init; } = true;
}

/// <summary>
/// OpenSearch 2.x candidate retrieval engine implementing Pass 2 multi-match DisMax and phonetic token filtering.
/// </summary>
public sealed class OpenSearchWatchlistSearchEngine : IWatchlistSearchEngine
{
    private readonly HttpClient _httpClient;
    private readonly ITextNormalizer _normalizer;
    private readonly OpenSearchOptions _options;
    private readonly InMemoryWatchlistSearchEngine _fallback;

    public OpenSearchWatchlistSearchEngine(
        HttpClient httpClient,
        ITextNormalizer normalizer,
        OpenSearchOptions? options = null,
        InMemoryWatchlistSearchEngine? fallback = null)
    {
        _httpClient = httpClient;
        _normalizer = normalizer;
        _options = options ?? new OpenSearchOptions();
        _fallback = fallback ?? new InMemoryWatchlistSearchEngine(normalizer);

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        }

        var authBytes = Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
    }

    public async Task<IReadOnlyList<WatchlistEntity>> SearchCandidatesAsync(
        string query,
        int maxResults = 500,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = _normalizer.Normalize(query);
        var tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var phoneticTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tokens)
        {
            var meta = DoubleMetaphone.Encode(t);
            if (!string.IsNullOrEmpty(meta.Primary)) phoneticTokens.Add(meta.Primary);
            if (!string.IsNullOrEmpty(meta.Alternate)) phoneticTokens.Add(meta.Alternate);
        }

        // Build OpenSearch DisMax query targeting full name, ngrams, aliases and phonetic tokens
        var shouldClauses = new JsonArray
        {
            new JsonObject
            {
                ["dis_max"] = new JsonObject
                {
                    ["queries"] = new JsonArray
                    {
                        new JsonObject { ["match"] = new JsonObject { ["full_name"] = new JsonObject { ["query"] = query, ["boost"] = 3.0 } } },
                        new JsonObject { ["match"] = new JsonObject { ["full_name.ngram"] = new JsonObject { ["query"] = query, ["boost"] = 2.0 } } },
                        new JsonObject { ["match"] = new JsonObject { ["aliases"] = new JsonObject { ["query"] = query, ["boost"] = 2.0 } } },
                        new JsonObject { ["match"] = new JsonObject { ["aliases.ngram"] = new JsonObject { ["query"] = query, ["boost"] = 1.5 } } }
                    }
                }
            }
        };

        if (phoneticTokens.Count > 0)
        {
            var phoneticArray = new JsonArray();
            foreach (var pt in phoneticTokens)
            {
                phoneticArray.Add(pt);
            }

            shouldClauses.Add(new JsonObject
            {
                ["terms"] = new JsonObject
                {
                    ["phonetic_tokens"] = phoneticArray,
                    ["boost"] = 1.5
                }
            });
        }

        var searchBody = new JsonObject
        {
            ["size"] = maxResults,
            ["query"] = new JsonObject
            {
                ["bool"] = new JsonObject
                {
                    ["should"] = shouldClauses,
                    ["filter"] = new JsonArray
                    {
                        new JsonObject { ["term"] = new JsonObject { ["is_active"] = true } }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{_options.WatchlistIndex}/_search")
        {
            Content = new StringContent(searchBody.ToJsonString(), Encoding.UTF8, "application/json")
        };

        string json;
        using var response = await _httpClient.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"OpenSearch index '{_options.WatchlistIndex}' not found.");
        }
        response.EnsureSuccessStatusCode();
        json = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("hits", out var hitsProp) ||
            !hitsProp.TryGetProperty("hits", out var hitArray))
        {
            return [];
        }

        var candidates = new List<WatchlistEntity>();
        foreach (var hit in hitArray.EnumerateArray())
        {
            if (hit.TryGetProperty("_source", out var src))
            {
                var id = src.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var gid)
                    ? gid
                    : Guid.NewGuid();

                var extId = src.TryGetProperty("entity_external_id", out var extProp) ? extProp.GetString() ?? "" : "";
                var entityType = src.TryGetProperty("entity_type", out var typeProp) ? typeProp.GetString() ?? "INDIVIDUAL" : "INDIVIDUAL";
                var fullName = src.TryGetProperty("full_name", out var fnProp) ? fnProp.GetString() ?? "" : "";
                var nationality = src.TryGetProperty("nationality", out var natProp) ? natProp.GetString() : null;
                var sourceList = src.TryGetProperty("source_list", out var slProp) ? slProp.GetString() ?? "UNKNOWN" : "UNKNOWN";
                var category = src.TryGetProperty("category", out var catProp) ? catProp.GetString() ?? "SANCTION" : "SANCTION";
                var isBlocked = !src.TryGetProperty("is_blocked", out var blkProp) || blkProp.GetBoolean();
                var isActive = !src.TryGetProperty("is_active", out var actProp) || actProp.GetBoolean();
                if (!isActive)
                {
                    continue;
                }

                DateOnly? dob = null;
                if (src.TryGetProperty("date_of_birth", out var dobProp) &&
                    DateOnly.TryParse(dobProp.GetString(), out var parsedDob))
                {
                    dob = parsedDob;
                }

                var idDetails = src.TryGetProperty("designation_details", out var ddProp) ? ddProp.GetString() : null;
                string? aliasName = null;
                if (src.TryGetProperty("aliases", out var aliasesProp) && aliasesProp.ValueKind == JsonValueKind.Array)
                {
                    var aliasList = aliasesProp.EnumerateArray()
                        .Select(a => a.GetString())
                        .Where(a => !string.IsNullOrWhiteSpace(a) && !string.Equals(a, fullName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (aliasList.Count > 0) aliasName = string.Join(", ", aliasList);
                }

                candidates.Add(new WatchlistEntity
                {
                    Id = id,
                    EntityExternalId = extId,
                    EntityType = entityType,
                    FullName = fullName,
                    Nationality = nationality,
                    Dob = dob,
                    SourceList = sourceList,
                    Category = category,
                    IdDetails = idDetails,
                    AliasName = aliasName,
                    IsBlocked = isBlocked,
                    IsActive = isActive
                });
            }
        }

        return candidates;
    }

    public Task IndexEntityAsync(WatchlistEntity entity, CancellationToken ct = default)
    {
        return IndexBatchAsync([entity], ct);
    }

    public async Task IndexBatchAsync(IEnumerable<WatchlistEntity> entities, CancellationToken ct = default)
    {
        var list = entities.ToList();
        if (list.Count == 0) return;

        if (list.Count <= 1000)
        {
            await _fallback.IndexBatchAsync(list, ct);
        }

        var sb = new StringBuilder();

        foreach (var entity in list)
        {
            var norm = _normalizer.Normalize(entity.FullName, entity.EntityType == "CORPORATE");
            var tokens = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var phoneticTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tokens)
            {
                var meta = DoubleMetaphone.Encode(t);
                if (!string.IsNullOrEmpty(meta.Primary)) phoneticTokens.Add(meta.Primary);
                if (!string.IsNullOrEmpty(meta.Alternate)) phoneticTokens.Add(meta.Alternate);
            }

            var aliases = new List<string> { entity.FullName };
            if (!string.IsNullOrWhiteSpace(entity.AliasName))
            {
                foreach (var alias in entity.AliasName.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    aliases.Add(alias);
                    var normAlias = _normalizer.Normalize(alias, entity.EntityType == "CORPORATE");
                    foreach (var at in normAlias.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var meta = DoubleMetaphone.Encode(at);
                        if (!string.IsNullOrEmpty(meta.Primary)) phoneticTokens.Add(meta.Primary);
                        if (!string.IsNullOrEmpty(meta.Alternate)) phoneticTokens.Add(meta.Alternate);
                    }
                }
            }

            var docId = string.IsNullOrWhiteSpace(entity.EntityExternalId)
                ? entity.Id.ToString()
                : $"{entity.EntityExternalId}:{norm.Replace(' ', '_')}";
            var header = JsonSerializer.Serialize(new { index = new { _index = _options.WatchlistIndex, _id = docId } });

            var doc = new
            {
                id = entity.Id.ToString(),
                entity_external_id = entity.EntityExternalId,
                entity_type = entity.EntityType,
                full_name = entity.FullName,
                aliases = aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                phonetic_tokens = phoneticTokens.ToArray(),
                date_of_birth = entity.Dob?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                birth_year = entity.Dob?.Year,
                nationality = entity.Nationality,
                country = entity.Nationality,
                source_list = entity.SourceList,
                category = entity.Category,
                designation_details = entity.IdDetails,
                is_blocked = entity.IsBlocked,
                is_active = entity.IsActive,
                updated_at = DateTimeOffset.UtcNow
            };

            sb.AppendLine(header);
            sb.AppendLine(JsonSerializer.Serialize(doc));
        }

        var bulkUrl = list.Count < 100 ? "/_bulk?refresh=true" : "/_bulk";
        using var request = new HttpRequestMessage(HttpMethod.Post, bulkUrl)
        {
            Content = new StringContent(sb.ToString(), Encoding.UTF8, "application/x-ndjson")
        };

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            // Indexed into in-memory fallback when OpenSearch is offline
        }
    }

    public async Task<bool> EnsureIndexCreatedAsync(CancellationToken ct = default)
    {
        try
        {
            using var headReq = new HttpRequestMessage(HttpMethod.Head, $"/{_options.WatchlistIndex}");
            using var headRes = await _httpClient.SendAsync(headReq, ct);
            if (headRes.IsSuccessStatusCode)
            {
                return false;
            }

            var indexDef = new
            {
                settings = new
                {
                    index = new
                    {
                        number_of_shards = 1,
                        number_of_replicas = 0,
                        refresh_interval = "1s",
                        max_ngram_diff = 7
                    },
                    analysis = new
                    {
                        tokenizer = new
                        {
                            edge_ngram_tokenizer = new
                            {
                                type = "edge_ngram",
                                min_gram = 3,
                                max_gram = 10,
                                token_chars = new[] { "letter", "digit" }
                            }
                        },
                        analyzer = new
                        {
                            watchlist_ngram_analyzer = new
                            {
                                type = "custom",
                                tokenizer = "edge_ngram_tokenizer",
                                filter = new[] { "lowercase" }
                            }
                        }
                    }
                },
                mappings = new
                {
                    properties = new
                    {
                        id = new { type = "keyword" },
                        entity_external_id = new { type = "keyword" },
                        entity_type = new { type = "keyword" },
                        full_name = new
                        {
                            type = "text",
                            fields = new
                            {
                                ngram = new
                                {
                                    type = "text",
                                    analyzer = "watchlist_ngram_analyzer",
                                    search_analyzer = "standard"
                                }
                            }
                        },
                        aliases = new
                        {
                            type = "text",
                            fields = new
                            {
                                ngram = new
                                {
                                    type = "text",
                                    analyzer = "watchlist_ngram_analyzer",
                                    search_analyzer = "standard"
                                }
                            }
                        },
                        phonetic_tokens = new { type = "keyword" },
                        date_of_birth = new { type = "keyword" },
                        birth_year = new { type = "integer" },
                        nationality = new { type = "keyword" },
                        country = new { type = "keyword" },
                        source_list = new { type = "keyword" },
                        category = new { type = "keyword" },
                        designation_details = new { type = "text" },
                        is_blocked = new { type = "boolean" },
                        is_active = new { type = "boolean" },
                        updated_at = new { type = "date" }
                    }
                },
                aliases = new Dictionary<string, object>
                {
                    [_options.WatchlistAlias] = new { }
                }
            };

            var json = JsonSerializer.Serialize(indexDef);
            using var putReq = new HttpRequestMessage(HttpMethod.Put, $"/{_options.WatchlistIndex}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var putRes = await _httpClient.SendAsync(putReq, ct);
            putRes.EnsureSuccessStatusCode();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EnsureWhitelistIndexCreatedAsync(CancellationToken ct = default)
    {
        try
        {
            using var headReq = new HttpRequestMessage(HttpMethod.Head, $"/{_options.WhitelistIndex}");
            using var headRes = await _httpClient.SendAsync(headReq, ct);
            if (headRes.IsSuccessStatusCode)
            {
                return false;
            }

            var indexDef = new
            {
                settings = new
                {
                    index = new
                    {
                        number_of_shards = 1,
                        number_of_replicas = 0,
                        refresh_interval = "1s"
                    }
                },
                mappings = new
                {
                    properties = new
                    {
                        id = new { type = "keyword" },
                        tenant_id = new { type = "keyword" },
                        customer_id = new { type = "keyword" },
                        entity_identifier = new { type = "keyword" },
                        match_scope = new { type = "keyword" },
                        reason = new { type = "text" },
                        expires_at = new { type = "date" },
                        is_active = new { type = "boolean" },
                        created_at = new { type = "date" }
                    }
                },
                aliases = new Dictionary<string, object>
                {
                    [_options.WhitelistAlias] = new { }
                }
            };

            var json = JsonSerializer.Serialize(indexDef);
            using var putReq = new HttpRequestMessage(HttpMethod.Put, $"/{_options.WhitelistIndex}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var putRes = await _httpClient.SendAsync(putReq, ct);
            putRes.EnsureSuccessStatusCode();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task SetRefreshIntervalAsync(string interval, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { index = new { refresh_interval = interval } });
        using var req = new HttpRequestMessage(HttpMethod.Put, $"/{_options.WatchlistIndex}/_settings")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        using var res = await _httpClient.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task ForceMergeAsync(int maxNumSegments = 1, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/{_options.WatchlistIndex}/_forcemerge?max_num_segments={maxNumSegments}");
        using var res = await _httpClient.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task IndexWhitelistBatchAsync(IEnumerable<WhitelistEntry> entries, CancellationToken ct = default)
    {
        var list = entries.ToList();
        if (list.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var entry in list)
        {
            var header = JsonSerializer.Serialize(new { index = new { _index = _options.WhitelistIndex, _id = entry.Id.ToString() } });
            var doc = new
            {
                id = entry.Id.ToString(),
                tenant_id = entry.TenantId.ToString(),
                customer_id = entry.CustomerId?.ToString(),
                entity_identifier = entry.EntityIdentifier,
                match_scope = entry.MatchScope,
                reason = entry.Reason,
                expires_at = entry.ExpiresAt?.ToString("o"),
                is_active = entry.IsActive,
                created_at = DateTimeOffset.UtcNow.ToString("o")
            };
            sb.AppendLine(header);
            sb.AppendLine(JsonSerializer.Serialize(doc));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/_bulk?refresh=true")
        {
            Content = new StringContent(sb.ToString(), Encoding.UTF8, "application/x-ndjson")
        };

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            // Non-fatal
        }
    }
}
