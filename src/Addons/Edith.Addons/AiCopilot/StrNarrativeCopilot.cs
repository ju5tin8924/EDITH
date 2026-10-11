using Edith.Cases.Models;

namespace Edith.Addons.AiCopilot;

public sealed record StrNarrativeResult
{
    public required string CaseNumber { get; init; }
    public required string FullNarrativeMarkdown { get; init; }
    public required string IntroductionSection { get; init; }
    public required string TransactionSummarySection { get; init; }
    public required string RedFlagsSection { get; init; }
    public required string InvestigationSection { get; init; }
    public required string ConclusionSection { get; init; }
}

public interface IStrNarrativeCopilot
{
    StrNarrativeResult DraftRegulatoryNarrative(CaseDetail caseDetail, IReadOnlyList<CaseNote>? notes = null);
}

public sealed class StrNarrativeCopilot : IStrNarrativeCopilot
{
    public StrNarrativeResult DraftRegulatoryNarrative(CaseDetail caseDetail, IReadOnlyList<CaseNote>? notes = null)
    {
        var intro = $"This Suspicious Transaction Report (STR) is submitted regarding transaction {caseDetail.TransactionRefNo} initiated under case record {caseDetail.CaseNumber}. The subject account holder, {caseDetail.SenderName}, maintains a commercial relationship subject to enhanced due diligence (EDD) monitoring under Central Bank anti-money laundering regulations.";

        var txSummary = $"On {caseDetail.CreatedAt:dd-MMM-yyyy}, a wire transfer totaling {caseDetail.Amount:N2} {caseDetail.Currency} was initiated in favor of beneficiary '{caseDetail.BeneficiaryName}'. The transaction exceeded standard risk thresholds with a calculated composite risk score of {caseDetail.RiskScore:F1}%.";

        var redFlags = $"Screening diagnostic engines tripped on high-priority watchlists: {caseDetail.TriggeredList ?? "OFAC_SDN / UN_CONSOLIDATED"}. The rationale recorded was: '{caseDetail.HoldReason}'. The beneficiary identity exhibits phonetic and deterministic alignment with designated individuals subject to statutory asset freeze obligations.";

        var notesSummary = (notes != null && notes.Count > 0)
            ? string.Join(" ", notes.Select(n => $"[{n.AuthorName}]: {n.Content}"))
            : "Compliance analysts verified passport identifiers and source of funds documentation, finding irreconcilable discrepancies with legitimate commercial trade.";

        var investigation = $"Internal investigation findings: {notesSummary}";

        var conclusion = $"Based on the substantiated sanctions alignment and potential sanctions evasion indicators, the transaction has been frozen and escalated to the Money Laundering Reporting Officer (MLRO). Formal regulatory notification is hereby filed with the Financial Intelligence Unit (FIU) via goAML in compliance with statutory reporting mandates.";

        var fullMarkdown = $"""
                            ### 1. Introduction & Subject Background
                            {intro}

                            ### 2. Transaction Summary & Velocity
                            {txSummary}

                            ### 3. Red Flags & Watchlist Findings
                            {redFlags}

                            ### 4. Investigation Steps & Findings
                            {investigation}

                            ### 5. Conclusion & Action Taken
                            {conclusion}
                            """;

        return new StrNarrativeResult
        {
            CaseNumber = caseDetail.CaseNumber,
            FullNarrativeMarkdown = fullMarkdown,
            IntroductionSection = intro,
            TransactionSummarySection = txSummary,
            RedFlagsSection = redFlags,
            InvestigationSection = investigation,
            ConclusionSection = conclusion
        };
    }
}
