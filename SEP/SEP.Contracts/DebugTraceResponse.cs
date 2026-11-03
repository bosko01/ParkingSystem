
namespace SEP.Contracts;

public sealed record DebugTraceResponse(
    string Service,
    string? TraceId,
    string Message,
    DebugTraceResponse? Downstream = null
);
