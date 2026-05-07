using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AltTextGen.Server.Configuration;
using AltTextGen.Server.Services;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace AltTextGen.Server.Composition;

public sealed class AltTextGenerationComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services
            .AddOptions<AltTextGenerationOptions>()
            .Bind(builder.Config.GetSection(AltTextGenerationOptions.SectionName))
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.AltTextPropertyAlias),
                "AltTextGeneration:AltTextPropertyAlias must be configured when alt text generation is enabled.")
            .Validate(
                options => options.Endpoint.TimeoutSeconds > 0,
                "AltTextGeneration:Endpoint:TimeoutSeconds must be greater than zero.")
            .Validate(
                options => !options.Enabled || IsTwoLetterIsoCode(options.OutputLanguage),
                "AltTextGeneration:OutputLanguage must be a two-letter ISO 639-1 language code (for example: en, nl, fr).")
            .Validate(
                options => !options.Enabled || !IsXAiProvider(options.Provider) || !string.IsNullOrWhiteSpace(options.Endpoint.Model),
                "AltTextGeneration:Endpoint:Model must be configured when using the xAI provider.")
            .Validate(
                options => !options.Enabled || !IsXAiProvider(options.Provider) || HasXAiApiKeySource(options),
                "AltTextGeneration:Endpoint:ApiKey or AltTextGeneration:Endpoint:ApiKeyEnvironmentVariable must be configured when using the xAI provider.")
            .ValidateOnStart();

        builder.Services.AddTransient<MockAltTextGenerationService>();
        builder.Services.AddTransient<UmbracoAiAltTextGenerationService>();
        builder.Services.AddTransient<XAiAltTextGenerationService>();
        builder.Services.AddTransient<IAltTextGenerationService>(serviceProvider =>
        {
            AltTextGenerationOptions options = serviceProvider
                .GetRequiredService<IOptionsMonitor<AltTextGenerationOptions>>()
                .CurrentValue;

            if (IsUmbracoAiProvider(options.Provider))
            {
                return serviceProvider.GetRequiredService<UmbracoAiAltTextGenerationService>();
            }

            if (IsXAiProvider(options.Provider))
            {
                return serviceProvider.GetRequiredService<XAiAltTextGenerationService>();
            }

            return serviceProvider.GetRequiredService<MockAltTextGenerationService>();
        });
    }

    private static bool IsUmbracoAiProvider(string provider)
    {
        return provider.Equals("UmbracoAI", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("Umbraco.AI", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("MicrosoftFoundry", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("Foundry", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsXAiProvider(string provider)
    {
        return provider.Equals("xAI", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("XAI", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("Grok", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasXAiApiKeySource(AltTextGenerationOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.Endpoint.ApiKey)
            || !string.IsNullOrWhiteSpace(options.Endpoint.ApiKeyEnvironmentVariable);
    }

    private static bool IsTwoLetterIsoCode(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length == 2
            && char.IsLetter(value[0])
            && char.IsLetter(value[1]);
    }
}
