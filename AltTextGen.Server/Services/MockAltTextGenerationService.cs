using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AltTextGen.Server.Configuration;

namespace AltTextGen.Server.Services;

public sealed class MockAltTextGenerationService(
    IOptionsMonitor<AltTextGenerationOptions> optionsMonitor,
    ILogger<MockAltTextGenerationService> logger) : IAltTextGenerationService
{
    private static readonly string[] MockAltTexts =
    [
        "A bright image with clear visual detail.",
        "A photo showing people and objects in context.",
        "An illustrative image suitable for editorial content.",
        "A descriptive media image with a clear main subject.",
        "A visual asset prepared for accessible content."
    ];

    public Task<AltTextGenerationResult> GenerateAsync(
        AltTextGenerationContext context,
        CancellationToken cancellationToken)
    {
        AltTextGenerationOptions options = optionsMonitor.CurrentValue;

        logger.LogInformation(
            "AltTextGen generating alt text (mock). MediaKey={MediaKey} MediaName={MediaName} FileValue={FileValue} Provider={Provider}",
            context.MediaKey,
            context.MediaName,
            context.FileValue,
            options.Provider);

        string altText = MockAltTexts[Random.Shared.Next(MockAltTexts.Length)];

        return Task.FromResult(new AltTextGenerationResult(altText, options.Provider));
    }
}
