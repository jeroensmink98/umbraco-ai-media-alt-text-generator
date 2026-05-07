using Microsoft.Extensions.DependencyInjection;
using AltTextGen.Server.Configuration;
using AltTextGen.Server.Services;

namespace AltTextGen.Tests;

public class MockAltTextGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_CanBeResolvedThroughDependencyInjection()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.Configure<AltTextGenerationOptions>(options => options.Provider = "Mock");
        services.AddSingleton<MockAltTextGenerationService>();
        services.AddSingleton<IAltTextGenerationService>(
            serviceProvider => serviceProvider.GetRequiredService<MockAltTextGenerationService>());

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        IAltTextGenerationService service = serviceProvider.GetRequiredService<IAltTextGenerationService>();
        AltTextGenerationContext context = new(
            Guid.NewGuid(),
            "test-image.png",
            "Image",
            "/media/test/test-image.png",
            "media/test/test-image.png",
            "image/png",
            [1, 2, 3],
            null,
            null,
            null);

        AltTextGenerationResult result = await service.GenerateAsync(context, CancellationToken.None);

        Assert.Equal("Mock", result.Provider);
        Assert.False(string.IsNullOrWhiteSpace(result.AltText));
    }
}
