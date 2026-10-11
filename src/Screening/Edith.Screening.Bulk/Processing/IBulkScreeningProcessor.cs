using System.Collections.Concurrent;
using Edith.Screening.Bulk.Models;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Models;

namespace Edith.Screening.Bulk.Processing;

public interface IBulkScreeningProcessor
{
    Task<BulkScreeningJob> SubmitJobAsync(Guid tenantId, string jobName, string fileName, Stream csvStream, CancellationToken ct = default);
    Task<BulkScreeningJob> ExecuteJobAsync(Guid tenantId, Guid jobId, IReadOnlyList<BulkRowItem> rows, ScoreConfig? config = null, Action<BulkProgressUpdate>? progressCallback = null, CancellationToken ct = default);
    Task<BulkScreeningJob?> GetJobAsync(Guid tenantId, Guid jobId, CancellationToken ct = default);
}

public sealed class BulkScreeningProcessor : IBulkScreeningProcessor
{
    private readonly IScreeningEngine _screeningEngine;
    private readonly ConcurrentDictionary<Guid, BulkScreeningJob> _jobs = new();

    public BulkScreeningProcessor(IScreeningEngine screeningEngine)
    {
        _screeningEngine = screeningEngine;
    }

    public async Task<BulkScreeningJob> SubmitJobAsync(
        Guid tenantId,
        string jobName,
        string fileName,
        Stream csvStream,
        CancellationToken ct = default)
    {
        using var reader = new StreamReader(csvStream);
        var rows = new List<BulkRowItem>();

        await foreach (var row in BulkCsvParser.ParseCsvStreamAsync(reader, ct))
        {
            rows.Add(row);
        }

        var job = new BulkScreeningJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JobName = jobName,
            FileName = fileName,
            TotalRows = rows.Count,
            Status = BulkJobStatus.Submitted
        };

        _jobs[job.Id] = job;
        return job;
    }

    public async Task<BulkScreeningJob> ExecuteJobAsync(
        Guid tenantId,
        Guid jobId,
        IReadOnlyList<BulkRowItem> rows,
        ScoreConfig? config = null,
        Action<BulkProgressUpdate>? progressCallback = null,
        CancellationToken ct = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job) || job.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Bulk screening job '{jobId}' not found.");
        }

        job.Status = BulkJobStatus.Processing;
        job.TotalRows = rows.Count;

        var scoreConfig = config ?? new ScoreConfig
        {
            NameWeightage = 60m,
            NationalityWeightage = 10m,
            DobWeightage = 15m,
            IdWeightage = 15m,
            MinThresholdScore = 80m
        };

        var chunkSize = 500;
        var chunks = rows.Chunk(chunkSize).ToList();

        var threadSafeResults = new ConcurrentBag<BulkRowResult>();
        int totalProcessed = 0;
        int hitCount = 0;
        int cleanCount = 0;

        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();

            var chunkResults = new List<BulkRowResult>(chunk.Length);

            foreach (var item in chunk)
            {
                var transaction = new ScreeningTransaction
                {
                    TenantId = tenantId,
                    CustomerId = Guid.Empty,
                    TransactionRefNo = item.TransactionRefNo,
                    IdempotencyKey = $"BULK-{jobId:N}-{item.RowIndex}",
                    TransactionType = "BULK_BATCH",
                    Amount = item.Amount,
                    Currency = item.Currency,
                    LcyAmount = item.Amount,
                    SenderName = item.SenderName,
                    SenderNationality = string.Empty,
                    SenderCountry = item.SenderCountry ?? string.Empty,
                    BeneficiaryName = item.BeneficiaryName,
                    BeneficiaryCountry = item.BeneficiaryCountry ?? string.Empty,
                    IntermediaryBankName = item.IntermediaryBankName
                };

                var decision = await _screeningEngine.ScreenTransactionAsync(transaction, scoreConfig, ct: ct);

                var rowResult = new BulkRowResult
                {
                    RowIndex = item.RowIndex,
                    TransactionRefNo = item.TransactionRefNo,
                    Status = decision.Status.ToString().ToUpperInvariant(),
                    Score = decision.OverallScore,
                    HoldReason = decision.HoldReason,
                    MatchedEntity = decision.CandidateMatches.Count > 0 ? decision.CandidateMatches[0].Entity.FullName : null,
                    CaseNumber = decision.CaseNumber,
                    LatencyMs = decision.ElapsedMilliseconds
                };

                chunkResults.Add(rowResult);
                threadSafeResults.Add(rowResult);

                if (decision.IsHeld)
                {
                    Interlocked.Increment(ref hitCount);
                }
                else
                {
                    Interlocked.Increment(ref cleanCount);
                }

                Interlocked.Increment(ref totalProcessed);
            }

            job.ProcessedRows = totalProcessed;
            job.HitCount = hitCount;
            job.CleanCount = cleanCount;

            var percentage = rows.Count > 0 ? (double)totalProcessed / rows.Count * 100.0 : 100.0;
            progressCallback?.Invoke(new BulkProgressUpdate
            {
                JobId = jobId,
                Status = "PROCESSING",
                ProcessedRows = totalProcessed,
                TotalRows = rows.Count,
                Percentage = Math.Round(percentage, 2),
                HitCount = hitCount,
                CleanCount = cleanCount
            });
        }

        job.Results.Clear();
        job.Results.AddRange(threadSafeResults.OrderBy(r => r.RowIndex));
        job.Status = BulkJobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;

        progressCallback?.Invoke(new BulkProgressUpdate
        {
            JobId = jobId,
            Status = "COMPLETED",
            ProcessedRows = totalProcessed,
            TotalRows = rows.Count,
            Percentage = 100.0,
            HitCount = hitCount,
            CleanCount = cleanCount
        });

        return job;
    }

    public Task<BulkScreeningJob?> GetJobAsync(Guid tenantId, Guid jobId, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.TenantId == tenantId)
        {
            return Task.FromResult<BulkScreeningJob?>(job);
        }

        return Task.FromResult<BulkScreeningJob?>(null);
    }
}
