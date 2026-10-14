using System.ComponentModel.DataAnnotations;
using Edith.Screening.Api.Contracts;
using Edith.Screening.Api.Services;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Search;
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

            // 8. Map to Contract Response
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
                Matches = decision.CandidateMatches.Select(m => new CandidateMatchDto
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

            // 9. Persist to Idempotency Store
            await idempotencyStore.SaveDecisionAsync(tenantId, idempotencyKey, response, ct: ct);

            return Results.Ok(response);
        });

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
        });

        group.MapPost("/watchlist/sync", async (
            [FromBody] WatchlistSyncRequest req,
            [FromServices] IWatchlistSearchEngine searchEngine,
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

            await searchEngine.IndexBatchAsync(entities, ct);
            return Results.Ok(new { indexedCount = entities.Count, timestamp = DateTimeOffset.UtcNow });
        });

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
}
