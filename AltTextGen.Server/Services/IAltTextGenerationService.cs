namespace AltTextGen.Server.Services;

public interface IAltTextGenerationService
{
    Task<AltTextGenerationResult> GenerateAsync(AltTextGenerationContext context, CancellationToken cancellationToken);
}
