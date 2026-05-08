using AltTextGen.Server.Models;

namespace AltTextGen.Server.Services;

public enum AltTextMediaGenerationStatus
{
    Success,
    Disabled,
    InvalidRequest,
    NotFound,
    Conflict,
    ProviderError
}

public sealed record AltTextMediaGenerationResult(
    AltTextMediaGenerationStatus Status,
    string Message,
    GenerateAltTextResponse? Response = null);
