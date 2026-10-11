using Edith.Cases.Models;

namespace Edith.Addons.Graph;

public sealed record GraphNodeData
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string NodeType { get; init; } // SENDER | BENEFICIARY | BANK | UBO | SHARED_IDENTIFIER
    public required decimal RiskScore { get; init; }
    public string? Country { get; init; }
}

public sealed record GraphNode
{
    public required GraphNodeData Data { get; init; }
}

public sealed record GraphEdgeData
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Target { get; init; }
    public required string Relationship { get; init; } // TRANSFERS_TO | CORRESPONDENT_FOR | BENEFICIAL_OWNER_OF | SHARED_LINK
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
}

public sealed record GraphEdge
{
    public required GraphEdgeData Data { get; init; }
}

public sealed record CytoscapeGraph
{
    public required IReadOnlyList<GraphNode> Nodes { get; init; }
    public required IReadOnlyList<GraphEdge> Edges { get; init; }
}

public interface IEntityGraphVisualizer
{
    CytoscapeGraph BuildCaseEntityGraph(CaseDetail caseDetail, string? uboName = null, string? sharedPhone = null);
}

public sealed class EntityGraphVisualizer : IEntityGraphVisualizer
{
    public CytoscapeGraph BuildCaseEntityGraph(CaseDetail caseDetail, string? uboName = null, string? sharedPhone = null)
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();

        var senderId = $"node-sender-{Math.Abs(caseDetail.SenderName.GetHashCode()):X6}";
        var beneId = $"node-bene-{Math.Abs(caseDetail.BeneficiaryName.GetHashCode()):X6}";

        nodes.Add(new GraphNode
        {
            Data = new GraphNodeData
            {
                Id = senderId,
                Label = caseDetail.SenderName,
                NodeType = "SENDER",
                RiskScore = Math.Min(50m, caseDetail.RiskScore)
            }
        });

        nodes.Add(new GraphNode
        {
            Data = new GraphNodeData
            {
                Id = beneId,
                Label = caseDetail.BeneficiaryName,
                NodeType = "BENEFICIARY",
                RiskScore = caseDetail.RiskScore
            }
        });

        edges.Add(new GraphEdge
        {
            Data = new GraphEdgeData
            {
                Id = $"edge-tx-{caseDetail.Id:N}",
                Source = senderId,
                Target = beneId,
                Relationship = "TRANSFERS_TO",
                Amount = caseDetail.Amount,
                Currency = caseDetail.Currency
            }
        });

        if (!string.IsNullOrWhiteSpace(uboName))
        {
            var uboId = $"node-ubo-{Math.Abs(uboName.GetHashCode()):X6}";
            nodes.Add(new GraphNode
            {
                Data = new GraphNodeData
                {
                    Id = uboId,
                    Label = uboName,
                    NodeType = "UBO",
                    RiskScore = 90m
                }
            });

            edges.Add(new GraphEdge
            {
                Data = new GraphEdgeData
                {
                    Id = $"edge-ubo-{Math.Abs(uboName.GetHashCode()):X6}",
                    Source = uboId,
                    Target = senderId,
                    Relationship = "BENEFICIAL_OWNER_OF"
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(sharedPhone))
        {
            var phoneId = $"node-phone-{Math.Abs(sharedPhone.GetHashCode()):X6}";
            nodes.Add(new GraphNode
            {
                Data = new GraphNodeData
                {
                    Id = phoneId,
                    Label = sharedPhone,
                    NodeType = "SHARED_IDENTIFIER",
                    RiskScore = 75m
                }
            });

            edges.Add(new GraphEdge
            {
                Data = new GraphEdgeData
                {
                    Id = $"edge-phone-sender-{Math.Abs(sharedPhone.GetHashCode()):X6}",
                    Source = senderId,
                    Target = phoneId,
                    Relationship = "SHARED_LINK"
                }
            });

            edges.Add(new GraphEdge
            {
                Data = new GraphEdgeData
                {
                    Id = $"edge-phone-bene-{Math.Abs(sharedPhone.GetHashCode()):X6}",
                    Source = beneId,
                    Target = phoneId,
                    Relationship = "SHARED_LINK"
                }
            });
        }

        return new CytoscapeGraph
        {
            Nodes = nodes,
            Edges = edges
        };
    }
}
