using System.Net;
using System.Net.Http;

namespace Pointly.App.Tutor;

public interface ITutorModel
{
    Task<TutorResponse> GetNextStepAsync(TutorRequest request, CancellationToken cancellationToken);
}

public sealed record TutorElement(string Id, string Name, string AutomationId, string ControlType);

public sealed record TutorRequest(
    string UserQuery,
    string ForegroundProcessName,
    string WindowTitle,
    IReadOnlyList<TutorElement> Elements,
    string? CurrentContext);

public enum AnnotationType { Rectangle }

public sealed record TutorResponse(
    string Instruction,
    string TargetElementId,
    AnnotationType AnnotationType,
    string ExpectedNextState,
    double Confidence);

/// <summary>
/// A sanitized AssemblyAI failure. The body and selected headers are safe to
/// show in Pointly's local diagnostic log; the request and API key are never kept.
/// </summary>
public sealed class AssemblyAiGatewayException : HttpRequestException
{
    public AssemblyAiGatewayException(
        HttpStatusCode statusCode,
        string responseBody,
        string relevantHeaders,
        TimeSpan? retryAfter)
        : base($"AssemblyAI gateway returned HTTP {(int)statusCode}.", inner: null, statusCode)
    {
        ResponseBody = responseBody;
        RelevantHeaders = relevantHeaders;
        RetryAfter = retryAfter;
    }

    public string ResponseBody { get; }

    public string RelevantHeaders { get; }

    public TimeSpan? RetryAfter { get; }
}
