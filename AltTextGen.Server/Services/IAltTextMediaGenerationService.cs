using AltTextGen.Server.Models;

namespace AltTextGen.Server.Services;

public interface IAltTextMediaGenerationService
{
    Task<AltTextMediaGenerationResult> GenerateForMediaAsync(GenerateAltTextRequest request, CancellationToken cancellationToken);
}
