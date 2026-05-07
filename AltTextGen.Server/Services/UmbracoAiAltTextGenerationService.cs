using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AltTextGen.Server.Configuration;
using Umbraco.AI.Core.Chat;
using Umbraco.AI.Core.Profiles;

namespace AltTextGen.Server.Services;

public sealed class UmbracoAiAltTextGenerationService(
    IAIChatService chatService,
    IAIProfileService profileService,
    IOptionsMonitor<AltTextGenerationOptions> optionsMonitor,
    ILogger<UmbracoAiAltTextGenerationService> logger) : IAltTextGenerationService
{
    public async Task<AltTextGenerationResult> GenerateAsync(
        AltTextGenerationContext context,
        CancellationToken cancellationToken)
    {
        AltTextGenerationOptions options = optionsMonitor.CurrentValue;
        Guid? profileId = await ResolveProfileIdAsync(options.ProfileAlias, cancellationToken);

        ChatMessage[] messages =
        [
            new(ChatRole.System, options.SystemPrompt),
            new(
                ChatRole.User,
                new List<AIContent>
                {
                    new TextContent(BuildPrompt(options.UserPrompt, options.OutputLanguage, context)),
                    new DataContent(context.ImageBytes, context.ImageMediaType)
                })
        ];

        ChatOptions chatOptions = new()
        {
            MaxOutputTokens = options.MaxOutputTokens
        };

        logger.LogInformation(
            "AltTextGen invoking Umbraco.AI. MediaKey={MediaKey} ProfileAlias={ProfileAlias} ProfileId={ProfileId} MediaType={MediaType} Bytes={ByteCount}",
            context.MediaKey,
            options.ProfileAlias,
            profileId,
            context.ImageMediaType,
            context.ImageBytes.Length);

        ChatResponse response = await chatService.GetChatResponseAsync(
            chat =>
            {
                chat
                    .WithAlias("alt-text-generation")
                    .WithName("Alt text generation")
                    .WithDescription("Generates accessible alt text for Umbraco media items.")
                    .WithChatOptions(chatOptions);

                if (profileId is Guid id)
                {
                    chat.WithProfile(id);
                }
            },
            messages,
            cancellationToken);
        ChatResponseTokenUsage? usage = ChatResponseTokenUsageReader.Read(response);
        if (usage is not null)
        {
            logger.LogInformation(
                "AltTextGen Umbraco.AI token usage. MediaKey={MediaKey} ProfileAlias={ProfileAlias} ProfileId={ProfileId} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens}",
                context.MediaKey,
                options.ProfileAlias,
                profileId,
                usage.InputTokens,
                usage.OutputTokens,
                usage.TotalTokens);
        }
        else
        {
            logger.LogInformation(
                "AltTextGen Umbraco.AI token usage not available. MediaKey={MediaKey} ProfileAlias={ProfileAlias} ProfileId={ProfileId}",
                context.MediaKey,
                options.ProfileAlias,
                profileId);
        }

        string altText = response.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(altText))
        {
            throw new InvalidOperationException("The AI provider returned an empty alt text response.");
        }

        return new AltTextGenerationResult(altText, options.Provider);
    }

    private async Task<Guid?> ResolveProfileIdAsync(string? profileAlias, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profileAlias))
        {
            return null;
        }

        AIProfile? profile = await profileService.GetProfileByAliasAsync(profileAlias, cancellationToken);
        if (profile is null)
        {
            throw new InvalidOperationException($"AI profile '{profileAlias}' was not found.");
        }

        return profile.Id;
    }

    private static string BuildPrompt(string configuredPrompt, string configuredOutputLanguage, AltTextGenerationContext context)
    {
        string outputLanguage = AltTextLanguageResolver.ResolveOutputLanguageCode(context.Culture, configuredOutputLanguage);

        return $"""
            {configuredPrompt}
            Write the alt text in language code '{outputLanguage}' (ISO 639-1).

            Media name: {context.MediaName}
            Media type alias: {context.MediaTypeAlias}
            Existing alt text: {context.ExistingAltText ?? "(none)"}
            """;
    }
}
