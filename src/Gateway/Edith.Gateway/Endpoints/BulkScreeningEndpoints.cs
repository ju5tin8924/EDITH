using Edith.Gateway.Hubs;
using Edith.Screening.Bulk.Delta;
using Edith.Screening.Bulk.Models;
using Edith.Screening.Bulk.Processing;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace Edith.Gateway.Endpoints;

public static class BulkScreeningEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapBulkScreeningEndpoints(this IEndpointRouteBuilder app)
    {
        var bulkGroup = app.MapGroup("/api/v1/bulk");
        var deltaGroup = app.MapGroup("/api/v1/delta");

        // 1. Submit Bulk Screening Job
        bulkGroup.MapPost("/jobs", async (
            [FromBody] SubmitJobRequestDto req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IBulkScreeningProcessor processor,
            [FromServices] IHubContext<JobHub, IJobClient> hubContext,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var jobId = Guid.NewGuid();
            var totalRows = req.TotalRows > 0 ? req.TotalRows : 100;

            // Generate synthetic bulk items
            var rows = new List<BulkRowItem>(totalRows);
            for (int i = 0; i < totalRows; i++)
            {
                var isHit = i % 17 == 0;
                rows.Add(new BulkRowItem
                {
                    RowIndex = i + 1,
                    TransactionRefNo = $"TX-BULK-{jobId:N}"[..18].ToUpperInvariant(),
                    SenderName = $"Trading Entity {i + 1}",
                    BeneficiaryName = isHit ? "Viktor Anatolyevich Bout" : $"Legitimate Supplier {i + 1}",
                    Amount = 150000m + (i * 1000m),
                    Currency = "AED"
                });
            }

            // Return 202 Accepted immediately per architecture spec
            var summary = new
            {
                jobId = jobId.ToString(),
                fileName = req.FileName ?? "Bulk_Batch_20261014.csv",
                status = "PROCESSING",
                totalRecords = totalRows,
                processedRecords = 0,
                hitCount = 0,
                percentComplete = 0.0,
                startedAtUtc = DateTimeOffset.UtcNow.ToString("o")
            };

            // Run in background with SignalR progress streaming
            _ = Task.Run(async () =>
            {
                int processed = 0;
                int hits = 0;
                var chunks = rows.Chunk(20);

                foreach (var chunk in chunks)
                {
                    await Task.Delay(250, CancellationToken.None); // Simulate network & search engine latency
                    processed += chunk.Length;
                    hits += chunk.Count(r => r.BeneficiaryName.Contains("Bout", StringComparison.OrdinalIgnoreCase));
                    double percent = Math.Round((double)processed / totalRows * 100.0, 1);

                    await hubContext.Clients.All.JobProgressUpdated(jobId.ToString(), processed, totalRows, hits, percent);
                }

                await hubContext.Clients.All.JobCompleted(jobId.ToString(), hits);
            }, CancellationToken.None);

            return Results.Accepted($"/api/v1/bulk/jobs/{jobId}", summary);
        }).AllowAnonymous();

        // 2. Delta Rescreening Sync
        deltaGroup.MapPost("/sync", async (
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IDeltaRescreeningService deltaService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);

            var sampleCustomers = new List<CustomerProfile>
            {
                new() { Id = Guid.NewGuid(), TenantId = tenantId, CustomerCode = "CUST-001", FullName = "Viktor Anatolyevich Bout", Nationality = "RU" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, CustomerCode = "CUST-002", FullName = "Al-Baraka General Trading LLC", Nationality = "AE" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, CustomerCode = "CUST-003", FullName = "Falcon Express Cargo FZE", Nationality = "AE" }
            };

            var deltaWatchlist = new List<WatchlistEntity>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    EntityExternalId = "SDN-UN-1521-99",
                    EntityType = "INDIVIDUAL",
                    FullName = "BOUT, Viktor Anatolyevich",
                    Nationality = "RU",
                    SourceList = "UN / OFAC / UAE Local Terror List",
                    Category = "SANCTIONS"
                }
            };

            var hits = await deltaService.ExecuteDeltaRescreenAsync(tenantId, sampleCustomers, deltaWatchlist, ct: ct);

            return Results.Ok(new
            {
                syncBatchId = $"DELTA-{DateTime.UtcNow:yyyyMMddHHmmss}",
                scannedCustomers = sampleCustomers.Count,
                newDeltaHits = hits.Count,
                hits = hits.Select(h => new
                {
                    customerCode = h.Customer.CustomerCode,
                    customerName = h.Customer.FullName,
                    matchedWatchlistName = h.MatchedEntity.FullName,
                    sourceList = h.MatchedEntity.SourceList,
                    compositeScore = h.CompositeScore,
                    matchDetails = h.MatchDetails
                })
            });
        }).AllowAnonymous();

        // 3. Delta Batch Authorization
        deltaGroup.MapPost("/authorize", ([FromBody] DeltaAuthorizeRequest req) =>
        {
            return Results.Ok(new
            {
                status = "AUTHORIZED",
                batchId = req.BatchId ?? $"DELTA-AUTH-{Guid.NewGuid():N}",
                authorizedBy = "Fatima Al-Nuaimi (MLRO)",
                timestampUtc = DateTimeOffset.UtcNow.ToString("o")
            });
        }).AllowAnonymous();

        return app;
    }

    private static Guid ResolveTenant(string? tenantHeader)
    {
        if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsed))
        {
            return parsed;
        }
        return DefaultTenantId;
    }
}

public sealed record SubmitJobRequestDto(string? FileName, int TotalRows);
public sealed record DeltaAuthorizeRequest(string? BatchId, List<string>? ApprovedCustomerCodes);
