using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AltTextGen.Server.Configuration;
using System.ClientModel;

namespace AltTextGen.Server.Services;

public sealed class XAiAltTextGenerationService(
    IOptionsMonitor<AltTextGenerationOptions> optionsMonitor,
    ILogger<XAiAltTextGenerationService> logger) : IAltTextGenerationService
{
    private const string DefaultBaseUrl = "https://api.x.ai/v1";
    private const string ProviderName = "xAI";

    public async Task<AltTextGenerationResult> GenerateAsync(
        AltTextGenerationContext context,
        CancellationToken cancellationToken)
    {
        AltTextGenerationOptions options = optionsMonitor.CurrentValue;
        string apiKey = ResolveApiKey(options.Endpoint);
        string model = ResolveModel(options.Endpoint);
        Uri baseUrl = ResolveBaseUrl(options.Endpoint);

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
            MaxOutputTokens = options.MaxOutputTokens,
            ModelId = model
        };

        logger.LogInformation(
            "AltTextGen invoking xAI. MediaKey={MediaKey} Model={Model} BaseUrl={BaseUrl} MediaType={MediaType} Bytes={ByteCount}",
            context.MediaKey,
            model,
            baseUrl,
            context.ImageMediaType,
            context.ImageBytes.Length);

        OpenAI.OpenAIClientOptions clientOptions = new()
        {
            Endpoint = baseUrl,
            NetworkTimeout = TimeSpan.FromSeconds(options.Endpoint.TimeoutSeconds)
        };

        OpenAI.Chat.ChatClient openAiChatClient = new(model, new ApiKeyCredential(apiKey), clientOptions);
        using IChatClient chatClient = openAiChatClient.AsIChatClient();

        ChatResponse response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        ChatResponseTokenUsage? usage = ChatResponseTokenUsageReader.Read(response);
        if (usage is not null)
        {
            logger.LogInformation(
                "AltTextGen xAI token usage. MediaKey={MediaKey} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens}",
                context.MediaKey,
                usage.InputTokens,
                usage.OutputTokens,
                usage.TotalTokens);
        }
        else
        {
            logger.LogInformation(
                "AltTextGen xAI token usage not available. MediaKey={MediaKey} Model={Model}",
                context.MediaKey,
                model);
        }

        string altText = response.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(altText))
        {
            throw new InvalidOperationException("The xAI provider returned an empty alt text response.");
        }

        return new AltTextGenerationResult(altText, ProviderName);
    }

    private static string ResolveApiKey(AiEndpointOptions endpointOptions)
    {
        if (!string.IsNullOrWhiteSpace(endpointOptions.ApiKey))
        {
            return endpointOptions.ApiKey;
        }

        if (!string.IsNullOrWhiteSpace(endpointOptions.ApiKeyEnvironmentVariable))
        {
            string? apiKey = Environment.GetEnvironmentVariable(endpointOptions.ApiKeyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                return apiKey;
            }

            throw new InvalidOperationException(
                $"The configured xAI API key environment variable '{endpointOptions.ApiKeyEnvironmentVariable}' is not set.");
        }

        throw new InvalidOperationException(
            "AltTextGeneration:Endpoint:ApiKey or AltTextGeneration:Endpoint:ApiKeyEnvironmentVariable must be configured for xAI.");
    }

    private static string ResolveModel(AiEndpointOptions endpointOptions)
    {
        if (!string.IsNullOrWhiteSpace(endpointOptions.Model))
        {
            return endpointOptions.Model;
        }

        throw new InvalidOperationException("AltTextGeneration:Endpoint:Model must be configured for xAI.");
    }

    private static Uri ResolveBaseUrl(AiEndpointOptions endpointOptions)
    {
        string baseUrl = string.IsNullOrWhiteSpace(endpointOptions.BaseUrl)
            ? DefaultBaseUrl
            : endpointOptions.BaseUrl;

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        throw new InvalidOperationException("AltTextGeneration:Endpoint:BaseUrl must be an absolute URL for xAI.");
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
