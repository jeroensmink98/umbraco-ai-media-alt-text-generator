namespace AltTextGen.Server.Configuration;

public sealed class AltTextGenerationOptions
{
    public const string SectionName = "AltTextGeneration";

    public bool Enabled { get; set; } = true;

    public string Provider { get; set; } = "Mock";

    public string? ProfileAlias { get; set; }

    public string AltTextPropertyAlias { get; set; } = "altText";

    public string[] AllowedMediaTypeAliases { get; set; } = ["Image"];
    public string[] AllowedFileExtensions { get; set; } = ["png", "webp", "jpg", "jpeg"];

    public string SystemPrompt { get; set; } =
        "You generate WCAG-aligned alt text for images. Provide a short text alternative that conveys the image's meaning and purpose. Include any essential words that appear in the image when they are important to understanding the content. Avoid 'image of'/'picture of', avoid speculation, and do not add extra commentary. Return only the alt text.";

    public string UserPrompt { get; set; } =
        "Write a concise alt text for this image. Focus on the meaningful visual content and avoid phrases like 'image of' or 'picture of'.";

    public string OutputLanguage { get; set; } = "en";

    /// <summary>
    /// Maximum number of output tokens allowed from the AI alt text response.
    /// This caps the response length and helps control cost and prevent runaway outputs.
    /// For alt text, 80 tokens is usually plenty (roughly 50–70 words, already longer than most accessibility guidance expects).
    /// If you notice generated alt text being truncated, consider increasing to 120–200.
    /// Keep around 60–120 for "properly short" alt text combined with a prompt that asks for 1 sentence or max X characters.
    /// For longer image descriptions beyond alt text, 80 can feel tight.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 128;
    public int BatchConcurrency { get; set; } = 5;

    public AiEndpointOptions Endpoint { get; set; } = new();
}

public sealed class AiEndpointOptions
{
    public string? BaseUrl { get; set; }

    public string? Model { get; set; }

    public string? ApiKey { get; set; }

    public string? ApiKeyEnvironmentVariable { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}
