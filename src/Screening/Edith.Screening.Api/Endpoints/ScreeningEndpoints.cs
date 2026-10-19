using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Edith.Cases.Models;
using Edith.Cases.Services;
using Edith.Screening.Api.Contracts;
using Edith.Screening.Api.Services;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;
using Edith.Screening.Core.Search;
using Edith.Screening.Bulk.Parsing;
using Edith.Screening.Bulk.Processing;
using Edith.Screening.Bulk.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Screening.Api.Endpoints;

public static class ScreeningEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("01931000-0000-7000-8000-000000000000");

    public static IEndpointRouteBuilder MapScreeningEndpoints(this IEndpointRouteBuilder app, bool includeHealth = true)
    {
        var group = app.MapGroup("/api/v1");

        group.MapPost("/screen/transaction", async (
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromBody] ScreenTransactionRequest request,
            [FromServices] IScreeningEngine engine,
            [FromServices] IIdempotencyStore idempotencyStore,
            [FromServices] IWhitelistService whitelistService,
            [FromServices] ICaseService? caseService,
            CancellationToken ct) =>
        {
            // 1. Validate Idempotency-Key Header
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Missing Idempotency-Key",
                    detail: "HTTP header 'Idempotency-Key' is required for all transaction screening requests.");
            }

            // 2. Resolve Tenant ID
            Guid tenantId = DefaultTenantId;
            if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsedTenant))
            {
                tenantId = parsedTenant;
            }
            else if (request.TenantId.HasValue)
            {
                tenantId = request.TenantId.Value;
            }

            // 3. Check Idempotency Replay Cache
            var cached = await idempotencyStore.GetCachedDecisionAsync(tenantId, idempotencyKey, ct);
            if (cached != null)
            {
                return Results.Ok(cached);
            }

            // 4. Validate Request Model
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(request);
            if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
            {
                var errors = validationResults.ToDictionary(
                    r => r.MemberNames.FirstOrDefault() ?? "Request",
                    r => new[] { r.ErrorMessage ?? "Invalid input." });

                return Results.ValidationProblem(errors, title: "Validation Failed", statusCode: StatusCodes.Status400BadRequest);
            }

            // 5. Build Core Screening Transaction
            var transaction = new ScreeningTransaction
            {
                TenantId = tenantId,
                CustomerId = request.CustomerId ?? Guid.Empty,
                TransactionRefNo = request.TransactionRefNo,
                IdempotencyKey = idempotencyKey,
                TransactionType = request.TransactionType,
                Amount = request.Amount,
                Currency = request.Currency.ToUpperInvariant(),
                LcyAmount = request.LcyAmount ?? request.Amount,
                SenderName = request.SenderName,
                SenderNationality = request.SenderNationality ?? string.Empty,
                SenderCountry = request.SenderCountry ?? string.Empty,
                BeneficiaryName = request.BeneficiaryName,
                BeneficiaryCountry = request.BeneficiaryCountry ?? string.Empty,
                IntermediaryBankName = request.IntermediaryBankName,
                TransactionDate = request.TransactionDate ?? DateTimeOffset.UtcNow
            };

            // 6. Config & Whitelist Lookup
            var config = new ScoreConfig
            {
                NameWeightage = 60.00m,
                NationalityWeightage = 10.00m,
                DobWeightage = 15.00m,
                IdWeightage = 15.00m,
                MinThresholdScore = request.MinThresholdScore ?? 80.00m
            };

            var activeWhitelists = await whitelistService.GetActiveWhitelistsAsync(tenantId, ct);

            // 7. Execute Multi-pass Screening Engine
            var decision = await engine.ScreenTransactionAsync(transaction, config, activeWhitelists, ct);

            // 8. Map to Contract Response (Only over-threshold candidates count as matches)
            var effectiveThreshold = request.MinThresholdScore ?? 80.00m;
            var actualMatches = decision.IsHeld || decision.IsWhitelisted
                ? decision.CandidateMatches.Where(m => m.CompositeScore >= effectiveThreshold).ToList()
                : new List<CandidateMatch>();

            if (decision.IsHeld && actualMatches.Count == 0 && decision.CandidateMatches.Count > 0)
            {
                actualMatches.Add(decision.CandidateMatches[0]);
            }

            var response = new ScreenTransactionResponse
            {
                Status = decision.Status.ToString().ToUpperInvariant(),
                Decision = decision.Status.ToString().ToUpperInvariant(),
                OverallScore = decision.OverallScore,
                IsHeld = decision.IsHeld,
                HoldReason = decision.HoldReason,
                TriggeredList = decision.TriggeredList,
                CaseNumber = decision.CaseNumber,
                IsWhitelisted = decision.IsWhitelisted,
                LatencyMs = Math.Round(decision.ElapsedMilliseconds, 2),
                IdempotencyReplayed = false,
                Matches = actualMatches.Select(m => new CandidateMatchDto
                {
                    Role = m.Role.ToString(),
                    MatchedEntityId = m.Entity.EntityExternalId,
                    MatchedEntityName = m.Entity.FullName,
                    Category = m.Entity.Category,
                    SourceList = m.Entity.SourceList,
                    Score = m.CompositeScore,
                    NameSimilarity = Math.Round(m.NameSimilarityScore, 2),
                    PhoneticSimilarity = Math.Round(m.PhoneticMatchScore * 100, 2),
                    MatchDetails = m.MatchDetails
                }).ToList(),
                PassLogs = decision.PassLogs.Select(p => new PassLogDto
                {
                    PassNumber = p.PassNumber,
                    Score = p.Score,
                    MatchType = p.MatchType,
                    Details = p.Details
                }).ToList()
            };

            // 9. Auto-provision Case if Transaction is Held
            if (decision.IsHeld && caseService != null)
            {
                var caseDetail = new CaseDetail
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CaseNumber = decision.CaseNumber ?? $"CASE-{DateTime.UtcNow:yyyyMMdd}-{Math.Abs(transaction.TransactionRefNo.GetHashCode()):X6}",
                    TransactionId = Guid.NewGuid(),
                    TransactionRefNo = transaction.TransactionRefNo,
                    CustomerId = transaction.CustomerId != Guid.Empty ? transaction.CustomerId : null,
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    SenderName = transaction.SenderName,
                    BeneficiaryName = transaction.BeneficiaryName,
                    RiskScore = decision.OverallScore,
                    Priority = decision.OverallScore >= 90m ? CasePriority.Critical : (decision.OverallScore >= 80m ? CasePriority.High : CasePriority.Medium),
                    Status = CaseStatus.Open,
                    TriggeredList = decision.TriggeredList,
                    HoldReason = decision.HoldReason ?? "Sanctions Match Triggered",
                    SlaDueDate = DateTimeOffset.UtcNow.AddHours(decision.OverallScore >= 90m ? 24 : 72)
                };

                await caseService.CreateCaseAsync(caseDetail, ct);
            }

            // 10. Persist to Idempotency Store
            await idempotencyStore.SaveDecisionAsync(tenantId, idempotencyKey, response, ct: ct);

            return Results.Ok(response);
        });

        group.MapPost("/screen/name-search", async (
            [FromBody] NameSearchRequest req,
            [FromServices] IWatchlistSearchEngine searchEngine,
            [FromServices] IBulkJobRepository jobRepo,
            [FromServices] ITextNormalizer normalizer,
            [FromServices] INameSimilarityScorer nameScorer,
            CancellationToken ct) =>
        {
            var sw = Stopwatch.StartNew();
            var mode = (req.SearchMode ?? "FUZZY").Trim().ToUpperInvariant();
            var rawName = req.Name?.Trim() ?? string.Empty;
            var rawId = req.IdNumber?.Trim() ?? string.Empty;
            var normQuery = normalizer.Normalize(rawName);
            var maxResults = req.MaxResults > 0 ? Math.Min(req.MaxResults, 100) : 25;

            if (string.IsNullOrWhiteSpace(rawName) && string.IsNullOrWhiteSpace(rawId))
            {
                return Results.BadRequest(new { error = "Please provide a Name or ID Number to search." });
            }

            // Retrieve candidates from both PostgreSQL (watchlist_entities) and OpenSearch (edith_watchlist_v1)
            var pgCandidates = await jobRepo.SearchWatchlistEntitiesAsync(
                mode == "EXACT_NAME_AND_ID" ? rawName : (string.IsNullOrWhiteSpace(rawName) ? null : rawName),
                string.IsNullOrWhiteSpace(rawId) ? null : rawId,
                maxResults: 60,
                ct: ct);

            IReadOnlyList<WatchlistEntity> osCandidates = [];
            if (!string.IsNullOrWhiteSpace(normQuery))
            {
                osCandidates = await searchEngine.SearchCandidatesAsync(normQuery, maxResults: 60, ct: ct);
            }

            // Merge candidates by (EntityExternalId, FullName), preferring PG row when available for full IdDetails/AliasName
            var mergedMap = new Dictionary<string, WatchlistEntity>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in osCandidates)
            {
                var key = $"{c.EntityExternalId}:{normalizer.Normalize(c.FullName)}";
                mergedMap[key] = c;
            }
            foreach (var c in pgCandidates)
            {
                var key = $"{c.EntityExternalId}:{normalizer.Normalize(c.FullName)}";
                mergedMap[key] = c;
            }

            var matches = new List<NameSearchMatchDto>();

            foreach (var candidate in mergedMap.Values)
            {
                var normTarget = normalizer.Normalize(candidate.FullName, candidate.EntityType == "CORPORATE");
                var scoreRes = !string.IsNullOrWhiteSpace(normQuery)
                    ? nameScorer.Score(normQuery, normTarget)
                    : new NameScoreResult { FinalScore = 100.0, StringSimilarity = 1.0, PhoneticSimilarity = 1.0, MatchRationate = "ID Lookup Match" };

                // Also check aliases if present
                if (!string.IsNullOrWhiteSpace(normQuery) && !string.IsNullOrWhiteSpace(candidate.AliasName))
                {
                    foreach (var alias in candidate.AliasName.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var normAlias = normalizer.Normalize(alias, candidate.EntityType == "CORPORATE");
                        var aliasScore = nameScorer.Score(normQuery, normAlias);
                        if (aliasScore.FinalScore > scoreRes.FinalScore)
                        {
                            scoreRes = aliasScore;
                        }
                    }
                }

                bool isExactName = !string.IsNullOrWhiteSpace(normQuery) &&
                    (string.Equals(normQuery, normTarget, StringComparison.OrdinalIgnoreCase) || scoreRes.FinalScore >= 98.0);

                bool isIdMatch = !string.IsNullOrWhiteSpace(rawId) &&
                    (string.Equals(candidate.EntityExternalId, rawId, StringComparison.OrdinalIgnoreCase) ||
                     candidate.EntityExternalId.Contains(rawId, StringComparison.OrdinalIgnoreCase) ||
                     (!string.IsNullOrWhiteSpace(candidate.IdDetails) && candidate.IdDetails.Contains(rawId, StringComparison.OrdinalIgnoreCase)));

                string whyMatched;
                decimal finalScore;

                if (mode == "EXACT")
                {
                    if (!isExactName) continue;
                    finalScore = 100.0m;
                    whyMatched = $"Exact Normalized Name Match (100%): Input '{normQuery}' exactly matches watchlist entry '{normTarget}'.";
                }
                else if (mode == "EXACT_NAME_AND_ID")
                {
                    if (!string.IsNullOrWhiteSpace(rawName) && !string.IsNullOrWhiteSpace(rawId))
                    {
                        var nameMatches = isExactName || normTarget.Contains(normQuery, StringComparison.OrdinalIgnoreCase);
                        if (!nameMatches || !isIdMatch) continue;
                        finalScore = 100.0m;
                        whyMatched = $"Dual-Factor Exact Name & ID Verified (100%): Name '{candidate.FullName}' AND Identifier '{rawId}' matched (Watchlist ID: {candidate.EntityExternalId}).";
                    }
                    else if (!string.IsNullOrWhiteSpace(rawId))
                    {
                        if (!isIdMatch) continue;
                        finalScore = 100.0m;
                        whyMatched = $"Exact Sanctions / Document ID Match (100%): Identifier '{rawId}' matched against '{candidate.EntityExternalId}' / '{candidate.IdDetails ?? "N/A"}'.";
                    }
                    else
                    {
                        if (!isExactName) continue;
                        finalScore = 100.0m;
                        whyMatched = $"Exact Name Match (100%): '{candidate.FullName}' (External ID: {candidate.EntityExternalId}).";
                    }
                }
                else // FUZZY
                {
                    finalScore = (decimal)Math.Round(scoreRes.FinalScore, 2);
                    if (isIdMatch && finalScore < 95m)
                    {
                        finalScore = 95.0m;
                    }
                    if (finalScore < req.MinScore && !isIdMatch) continue;
                    whyMatched = $"Fuzzy & Phonetic Composite Score {finalScore:F1}% (String Similarity: {scoreRes.StringSimilarity * 100:F1}%, Double Metaphone Phonetic: {scoreRes.PhoneticSimilarity * 100:F1}% — {scoreRes.MatchRationate}).";
                }

                var (authority, whySanctioned) = DescribeSanctionsSource(candidate);

                matches.Add(new NameSearchMatchDto
                {
                    EntityId = candidate.Id,
                    ExternalId = candidate.EntityExternalId,
                    FullName = candidate.FullName,
                    NormalizedTargetName = normTarget,
                    EntityType = candidate.EntityType,
                    AliasName = candidate.AliasName,
                    IdDetails = candidate.IdDetails,
                    Nationality = candidate.Nationality,
                    Dob = candidate.Dob?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    SanctionsList = FormatListBadge(candidate.SourceList, candidate.EntityExternalId),
                    SanctionsAuthority = authority,
                    Category = candidate.Category,
                    IsBlocked = candidate.IsBlocked,
                    Score = finalScore,
                    StringSimilarity = Math.Round(scoreRes.StringSimilarity * 100.0, 2),
                    PhoneticSimilarity = Math.Round(scoreRes.PhoneticSimilarity * 100.0, 2),
                    MatchModeUsed = mode,
                    WhySanctioned = whySanctioned,
                    WhyMatched = whyMatched,
                    TechnicalBreakdown = scoreRes.MatchRationate
                });
            }

            var ordered = matches
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.FullName)
                .Take(maxResults)
                .ToList();

            sw.Stop();

            return Results.Ok(new NameSearchResponse
            {
                QueryName = rawName,
                NormalizedQueryName = normQuery,
                QueryId = string.IsNullOrWhiteSpace(rawId) ? null : rawId,
                SearchMode = mode,
                TotalMatches = ordered.Count,
                LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                Matches = ordered
            });
        }).AllowAnonymous();

        group.MapGet("/whitelists", async (
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IWhitelistService whitelistService,
            CancellationToken ct) =>
        {
            Guid tenantId = DefaultTenantId;
            if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsedTenant))
            {
                tenantId = parsedTenant;
            }

            var list = await whitelistService.GetActiveWhitelistsAsync(tenantId, ct);
            return Results.Ok(list);
        }).AllowAnonymous();

        group.MapPost("/whitelists", async (
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromBody] CreateWhitelistRequest req,
            [FromServices] IWhitelistService whitelistService,
            CancellationToken ct) =>
        {
            Guid tenantId = DefaultTenantId;
            if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsedTenant))
            {
                tenantId = parsedTenant;
            }
            else if (req.TenantId.HasValue)
            {
                tenantId = req.TenantId.Value;
            }

            var entry = new WhitelistEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = req.CustomerId,
                EntityIdentifier = req.EntityIdentifier,
                MatchScope = req.MatchScope,
                Reason = req.Reason,
                ExpiresAt = req.ExpiresAt,
                IsActive = true
            };

            var created = await whitelistService.AddWhitelistAsync(entry, ct);
            return Results.Created($"/api/v1/whitelists/{created.Id}", created);
        }).AllowAnonymous();

        group.MapDelete("/whitelists/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IWhitelistService whitelistService,
            CancellationToken ct) =>
        {
            Guid tenantId = DefaultTenantId;
            if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsedTenant))
            {
                tenantId = parsedTenant;
            }

            var removed = await whitelistService.RemoveWhitelistAsync(tenantId, id, ct);
            return removed ? Results.NoContent() : Results.NotFound(new { error = $"Whitelist entry '{id}' not found." });
        }).AllowAnonymous();

        group.MapPost("/watchlist/sync", async (
            [FromBody] WatchlistSyncRequest req,
            [FromServices] IWatchlistSearchEngine searchEngine,
            [FromServices] IBulkJobRepository jobRepo,
            CancellationToken ct) =>
        {
            var entities = req.Entities.Select(e => new WatchlistEntity
            {
                Id = e.Id ?? Guid.NewGuid(),
                EntityExternalId = e.EntityExternalId,
                EntityType = e.EntityType,
                FullName = e.FullName,
                Dob = e.Dob,
                Nationality = e.Nationality,
                SourceList = e.SourceList,
                Category = e.Category,
                IsBlocked = e.IsBlocked,
                IsActive = e.IsActive
            }).ToList();

            await jobRepo.UpsertWatchlistEntitiesBatchAsync(entities, ct);
            await searchEngine.IndexBatchAsync(entities, ct);
            return Results.Ok(new { indexedCount = entities.Count, timestamp = DateTimeOffset.UtcNow });
        });

        group.MapPost("/bulk/upload", async (
            HttpRequest httpRequest,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromHeader(Name = "X-User-ID")] string? userHeader,
            [FromServices] IBulkJobRepository jobRepo,
            [FromServices] IBulkJobChannel jobChannel,
            CancellationToken ct) =>
        {
            Guid tenantId = DefaultTenantId;
            if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsedTenant))
            {
                tenantId = parsedTenant;
            }

            Guid userId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            if (!string.IsNullOrWhiteSpace(userHeader) && Guid.TryParse(userHeader, out var parsedUser))
            {
                userId = parsedUser;
            }

            string fileName = "bulk_upload.csv";
            byte[] fileBytes;

            if (httpRequest.HasFormContentType)
            {
                var form = await httpRequest.ReadFormAsync(ct);
                if (form.Files.Count == 0 || form.Files[0].Length == 0)
                {
                    return Results.BadRequest(new { error = "No file was uploaded." });
                }

                var file = form.Files[0];
                fileName = file.FileName;
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                fileBytes = ms.ToArray();
            }
            else
            {
                if (httpRequest.Headers.TryGetValue("X-File-Name", out var customName) && !string.IsNullOrWhiteSpace(customName))
                {
                    fileName = customName.ToString();
                }

                using var ms = new MemoryStream();
                await httpRequest.Body.CopyToAsync(ms, ct);
                fileBytes = ms.ToArray();

                if (fileBytes.Length == 0)
                {
                    return Results.BadRequest(new { error = "Request body is empty." });
                }
            }

            var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            if (httpRequest.Query.TryGetValue("format", out var qFmt) && !string.IsNullOrWhiteSpace(qFmt))
            {
                ext = qFmt.ToString().Trim().TrimStart('.').ToLowerInvariant();
            }
            else if (httpRequest.Headers.TryGetValue("X-Format", out var hFmt) && !string.IsNullOrWhiteSpace(hFmt))
            {
                ext = hFmt.ToString().Trim().TrimStart('.').ToLowerInvariant();
            }

            if (string.IsNullOrEmpty(ext))
            {
                ext = "csv";
            }

            if (ext != "csv" && ext != "json" && ext != "xml")
            {
                return Results.BadRequest(new { error = $"Unsupported format '{ext}'. Must be CSV, XML, or JSON." });
            }

            bool keepSkipped = true;
            if (httpRequest.Query.TryGetValue("keepSkipped", out var keepVal) && bool.TryParse(keepVal, out var parsedKeep))
            {
                keepSkipped = parsedKeep;
            }
            else if (httpRequest.Headers.TryGetValue("X-Keep-Skipped", out var keepHdr) && bool.TryParse(keepHdr, out var parsedKeepHdr))
            {
                keepSkipped = parsedKeepHdr;
            }

            string skippedFormat = ext;
            if (httpRequest.Query.TryGetValue("skippedFormat", out var sfVal) && !string.IsNullOrWhiteSpace(sfVal))
            {
                skippedFormat = sfVal.ToString().Trim().ToLowerInvariant();
            }
            else if (httpRequest.Headers.TryGetValue("X-Skipped-Format", out var sfHdr) && !string.IsNullOrWhiteSpace(sfHdr))
            {
                skippedFormat = sfHdr.ToString().Trim().ToLowerInvariant();
            }

            var jobId = Guid.NewGuid();

            var jobRecord = new BulkJobRecord
            {
                Id = jobId,
                UserId = userId,
                FileName = fileName,
                TotalRecords = 0,
                ProcessedRecords = 0,
                SkippedRecords = 0,
                Status = "PENDING",
                KeepSkipped = keepSkipped,
                SkippedFormat = skippedFormat
            };

            await jobRepo.CreateJobAsync(jobRecord, ct);

            await jobChannel.EnqueueAsync(new BulkJobPayload
            {
                JobId = jobId,
                TenantId = tenantId,
                UserId = userId,
                FileName = fileName,
                Format = ext,
                Content = fileBytes,
                KeepSkipped = keepSkipped,
                SkippedFormat = skippedFormat
            }, ct);

            var response = new BulkUploadAcceptedResponse
            {
                JobId = jobId,
                FileName = fileName,
                Format = ext.ToUpperInvariant(),
                Status = "ACCEPTED",
                Message = "File accepted for background streaming ingestion.",
                ProgressUrl = $"/api/v1/bulk/jobs/{jobId}",
                KeepSkipped = keepSkipped,
                SkippedFormat = skippedFormat.ToUpperInvariant(),
                SkippedDownloadUrl = keepSkipped ? $"/api/v1/bulk/jobs/{jobId}/skipped" : null
            };

            return Results.Accepted($"/api/v1/bulk/jobs/{jobId}", response);
        }).AllowAnonymous();

        group.MapGet("/bulk/jobs", async (
            [FromServices] IBulkJobRepository jobRepo,
            CancellationToken ct) =>
        {
            var jobs = await jobRepo.ListJobsAsync(50, ct);
            var list = jobs.Select(job => new BulkJobStatusResponse
            {
                JobId = job.Id,
                FileName = job.FileName,
                Status = job.Status,
                TotalRecords = job.TotalRecords,
                ProcessedRecords = job.ProcessedRecords,
                SkippedRecords = job.SkippedRecords,
                SkippedDownloadUrl = job.SkippedRecords > 0 ? $"/api/v1/bulk/jobs/{job.Id}/skipped" : null
            }).ToList();
            return Results.Ok(list);
        }).AllowAnonymous();

        group.MapGet("/bulk/jobs/{id:guid}", async (
            Guid id,
            [FromServices] IBulkJobRepository jobRepo,
            CancellationToken ct) =>
        {
            var job = await jobRepo.GetJobAsync(id, ct);
            if (job == null)
            {
                return Results.NotFound(new { error = $"Bulk job '{id}' not found." });
            }

            return Results.Ok(new BulkJobStatusResponse
            {
                JobId = job.Id,
                FileName = job.FileName,
                Status = job.Status,
                TotalRecords = job.TotalRecords,
                ProcessedRecords = job.ProcessedRecords,
                SkippedRecords = job.SkippedRecords,
                SkippedDownloadUrl = job.SkippedRecords > 0 ? $"/api/v1/bulk/jobs/{id}/skipped" : null
            });
        }).AllowAnonymous();

        group.MapGet("/bulk/jobs/{id:guid}/skipped", async (
            Guid id,
            [FromQuery] string? format,
            [FromServices] IBulkJobRepository jobRepo,
            CancellationToken ct) =>
        {
            var job = await jobRepo.GetJobAsync(id, ct);
            if (job == null)
            {
                return Results.NotFound(new { error = $"Bulk job '{id}' not found." });
            }

            var skipped = await jobRepo.GetSkippedRecordsAsync(id, ct);
            if (skipped.Count == 0)
            {
                return Results.NotFound(new { error = $"No skipped records found for job '{id}'." });
            }

            var exportFormat = !string.IsNullOrWhiteSpace(format) ? format : job.SkippedFormat;
            var (bytes, contentType, extension) = AmlFormatExporter.Export(skipped, exportFormat);

            return Results.File(bytes, contentType, fileDownloadName: $"skipped_{id}.{extension}");
        }).AllowAnonymous();

        if (includeHealth)
        {
            app.MapGet("/health", () => Results.Ok(new
            {
                status = "Healthy",
                service = "Edith.Screening.Api",
                version = "1.0.0",
                timestamp = DateTimeOffset.UtcNow
            }));
        }

        return app;
    }

    private static string FormatListBadge(string sourceList, string externalId)
    {
        if (string.Equals(sourceList, "BULK_INGEST", StringComparison.OrdinalIgnoreCase))
        {
            if (externalId.StartsWith("WC-", StringComparison.OrdinalIgnoreCase))
                return "WORLD_CHECK_GLOBAL";
            return "UAE_EOCN_WATCHLIST";
        }
        return sourceList;
    }

    private static (string Authority, string WhySanctioned) DescribeSanctionsSource(WatchlistEntity entity)
    {
        var extId = entity.EntityExternalId;
        var details = !string.IsNullOrWhiteSpace(entity.IdDetails) ? $" Recorded ID / Remarks: {entity.IdDetails}." : string.Empty;

        if (string.Equals(entity.SourceList, "OFAC_SDN", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "U.S. Department of the Treasury — Office of Foreign Assets Control (OFAC SDN)",
                $"Designated on the OFAC Specially Designated Nationals and Blocked Persons List (ID: {extId}). Subject to mandatory statutory asset freeze and prohibition of USD/correspondent clearing.{details}"
            );
        }

        if (string.Equals(entity.SourceList, "UN_CONSOLIDATED", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "United Nations Security Council Consolidated Sanctions Committee",
                $"Listed under UN Security Council Chapter VII Sanctions Resolutions (ID: {extId}). Member states and financial institutions must freeze funds and economic resources without delay.{details}"
            );
        }

        if (string.Equals(entity.SourceList, "LOCAL_PEP_REGISTRY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entity.Category, "PEP", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "UAE / GCC Domestic Politically Exposed Persons (PEP) Registry",
                $"Classified as a Politically Exposed Person (ID: {extId}, Category: {entity.Category}). Requires Enhanced Due Diligence (EDD), Senior Management / MLRO sign-off, and source-of-wealth corroboration.{details}"
            );
        }

        if (extId.StartsWith("WC-", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "Refinitiv World-Check Global Regulatory & Sanctions Intelligence",
                $"Flagged in World-Check Global Sanctions & Regulatory Watchlist (UID: {extId}, Category: {entity.Category}, Program: {entity.SourceList}). Requires compliance review against applicable international sanctions and CBUAE AML/CFT directives.{details}"
            );
        }

        return (
            "UAE Executive Office for Control & Non-Proliferation (EOCN / Regional Watchlist)",
            $"Listed on the UAE / Regional Sanctions & Watchlist Registry (UID: {extId}, Category: {entity.Category}, List: {entity.SourceList}). Subject to UAE Cabinet Resolution No. 74 & CBUAE targeted financial sanctions screening.{details}"
        );
    }
}

