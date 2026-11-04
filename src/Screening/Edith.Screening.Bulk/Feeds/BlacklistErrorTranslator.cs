using System.Net;
using System.Xml;
using Npgsql;

namespace Edith.Screening.Bulk.Feeds;

public sealed record TranslatedBlacklistError(
    string ErrorCategory,
    string UserMessage,
    string TechnicalDetails,
    bool IsRecoverable);

public static class BlacklistErrorTranslator
{
    public static TranslatedBlacklistError TranslateHttpStatus(string sourceType, HttpStatusCode statusCode, string? technicalReason = null)
    {
        var src = sourceType.ToUpperInvariant();
        var code = (int)statusCode;

        if (statusCode == HttpStatusCode.NotFound)
        {
            return new TranslatedBlacklistError(
                "HTTP_404_NOT_FOUND",
                $"The {src} data file could not be found at the configured URL. Please verify the source URL in Admin Settings.",
                $"HTTP 404 Not Found returned for source {src}. {technicalReason}".Trim(),
                false);
        }

        if (code >= 500 || statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.RequestTimeout)
        {
            return new TranslatedBlacklistError(
                "UPSTREAM_SERVER_ERROR",
                $"The {src} source server is temporarily unavailable. Please try again later.",
                $"HTTP {code} ({statusCode}) returned from upstream server for {src}. {technicalReason}".Trim(),
                true);
        }

        return new TranslatedBlacklistError(
            "HTTP_CLIENT_ERROR",
            $"Unable to connect to the {src} data source (HTTP {code}). Please verify the source URL and access token in Admin Settings.",
            $"HTTP {code} ({statusCode}) returned for {src}. {technicalReason}".Trim(),
            false);
    }

    public static TranslatedBlacklistError TranslateException(string sourceType, Exception ex, string operationContext = "FETCH")
    {
        var src = sourceType.ToUpperInvariant();

        if (ex is TaskCanceledException or TimeoutException ||
            (ex is HttpRequestException hre && hre.InnerException is TimeoutException or System.Net.Sockets.SocketException))
        {
            return new TranslatedBlacklistError(
                "NETWORK_TIMEOUT",
                $"Unable to connect to the {src} data source because the server did not respond in time. Please try again later or verify the source URL.",
                $"{ex.GetType().Name}: {ex.Message}",
                true);
        }

        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode.HasValue)
            {
                return TranslateHttpStatus(src, httpEx.StatusCode.Value, httpEx.Message);
            }

            return new TranslatedBlacklistError(
                "NETWORK_UNREACHABLE",
                $"Unable to connect to the {src} data source because the server did not respond in time. Please try again later or verify the source URL.",
                $"{httpEx.GetType().Name}: {httpEx.Message}",
                true);
        }

        if (ex is XmlException or InvalidDataException)
        {
            return new TranslatedBlacklistError(
                "INVALID_XML_FORMAT",
                $"The {src} file was downloaded, but its XML format is invalid or incomplete. Existing {src} records were not modified.",
                $"{ex.GetType().Name}: {ex.Message}",
                false);
        }

        if (ex is NpgsqlException or PostgresException)
        {
            if (string.Equals(operationContext, "DUPLICATE_LOOKUP", StringComparison.OrdinalIgnoreCase))
            {
                return new TranslatedBlacklistError(
                    "DUPLICATE_CHECK_FAILURE",
                    "Duplicate verification could not be completed for some records. Affected records have been logged for review.",
                    $"{ex.GetType().Name}: {ex.Message}",
                    true);
            }

            return new TranslatedBlacklistError(
                "DATABASE_WRITE_FAILURE",
                "Blacklist data was retrieved successfully, but an error occurred while saving updates to the database. Please review the update log.",
                $"{ex.GetType().Name}: {ex.Message}",
                true);
        }

        return new TranslatedBlacklistError(
            "UNEXPECTED_APPLICATION_ERROR",
            "An unexpected error occurred during the blacklist update. Please check the update report for details.",
            $"{ex.GetType().Name}: {ex.Message}",
            false);
    }

    public static string BuildRunSummaryMessage(BlacklistSyncRun run)
    {
        if (run.SourcesProcessed == 0)
        {
            return "No enabled blacklist data sources were selected for execution.";
        }

        if (run.SourcesSucceeded == run.SourcesProcessed && run.TotalFailed == 0 && run.Errors.Count == 0)
        {
            return $"All {run.SourcesSucceeded} blacklist data source(s) completed successfully ({run.TotalNew:N0} new, {run.TotalUpdated:N0} updated, {run.TotalSkipped:N0} skipped, {run.TotalRemoved:N0} removed).";
        }

        if (run.SourcesSucceeded == run.SourcesProcessed && (run.TotalFailed > 0 || run.Errors.Count > 0))
        {
            return $"All {run.SourcesSucceeded} source(s) completed with warnings ({run.TotalFailed:N0} malformed record(s) logged for review).";
        }

        if (run.SourcesSucceeded > 0 && run.SourcesFailed > 0)
        {
            var failedNames = string.Join(", ", run.SourceStats.Where(s => s.Status == "FAILED").Select(s => s.SourceType));
            return $"The blacklist update completed for {run.SourcesSucceeded} of {run.SourcesProcessed} sources. One or more sources failed ({failedNames}) and require attention.";
        }

        return "The blacklist update failed for all configured sources. Existing active blacklist records were preserved without modification.";
    }
}
