using System.ComponentModel.DataAnnotations;
using Edith.Cases.Models;
using Edith.Cases.Services;
using Edith.Screening.Api.Contracts;
using Edith.Screening.Api.Services;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Models;
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
}
