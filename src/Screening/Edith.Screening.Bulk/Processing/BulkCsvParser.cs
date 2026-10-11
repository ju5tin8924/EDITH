using System.Globalization;
using System.Runtime.CompilerServices;
using Edith.Screening.Bulk.Models;

namespace Edith.Screening.Bulk.Processing;

public static class BulkCsvParser
{
    public static async IAsyncEnumerable<BulkRowItem> ParseCsvStreamAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string? headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null)
        {
            yield break;
        }

        var headers = headerLine.Split(',').Select(h => h.Trim().ToUpperInvariant()).ToList();
        var txRefIdx = headers.IndexOf("TRANSACTIONREFNO");
        var amountIdx = headers.IndexOf("AMOUNT");
        var currencyIdx = headers.IndexOf("CURRENCY");
        var senderIdx = headers.IndexOf("SENDERNAME");
        var beneficiaryIdx = headers.IndexOf("BENEFICIARYNAME");
        var bankIdx = headers.IndexOf("INTERMEDIARYBANKNAME");

        if (txRefIdx < 0 || senderIdx < 0 || beneficiaryIdx < 0)
        {
            throw new FormatException("CSV header must contain at least 'TransactionRefNo', 'SenderName', and 'BeneficiaryName'.");
        }

        int rowIndex = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(',');
            if (cols.Length <= Math.Max(senderIdx, beneficiaryIdx)) continue;

            var txRef = cols[txRefIdx].Trim();
            var sender = cols[senderIdx].Trim();
            var beneficiary = cols[beneficiaryIdx].Trim();

            decimal amount = 1000m;
            if (amountIdx >= 0 && amountIdx < cols.Length && decimal.TryParse(cols[amountIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedAmount))
            {
                amount = parsedAmount;
            }

            var currency = currencyIdx >= 0 && currencyIdx < cols.Length ? cols[currencyIdx].Trim().ToUpperInvariant() : "USD";
            var bank = bankIdx >= 0 && bankIdx < cols.Length ? cols[bankIdx].Trim() : null;

            rowIndex++;
            yield return new BulkRowItem
            {
                RowIndex = rowIndex,
                TransactionRefNo = txRef,
                Amount = amount,
                Currency = currency,
                SenderName = sender,
                BeneficiaryName = beneficiary,
                IntermediaryBankName = string.IsNullOrWhiteSpace(bank) ? null : bank
            };
        }
    }
}
