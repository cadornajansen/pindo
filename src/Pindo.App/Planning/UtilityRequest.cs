using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pindo.App.Planning;

public sealed record ToolProposal(string ToolId, JsonElement Arguments);

public sealed record UtilityResponse(string Kind, string Message, ToolProposal? Tool = null)
{
    public void Validate()
    {
        if (Kind is not ("clarification" or "tool" or "unsupported") ||
            string.IsNullOrWhiteSpace(Message) || Message.Length > 2000 ||
            (Kind != "tool" && Tool is not null) ||
            (Kind == "tool" && (Tool is null || string.IsNullOrWhiteSpace(Tool.ToolId) ||
                Tool.Arguments.ValueKind != JsonValueKind.Object)))
            throw new InvalidOperationException("Pindo received an invalid utility proposal. Please try again.");
    }
}

public static class UtilityJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>A new identity invalidates every response from a cancelled request or old selection.</summary>
public sealed class UtilitySession
{
    private readonly List<string> _conversation = [];
    public long Identity { get; private set; }
    public IReadOnlyList<string> Conversation => _conversation;
    public long Begin(string question)
    {
        Identity++;
        _conversation.Add(question);
        return Identity;
    }
    public bool IsCurrent(long identity) => Identity == identity;
    public void Clarify(string message) => _conversation.Add("Pindo: " + message);
    public void Reset() { Identity++; _conversation.Clear(); }
}
