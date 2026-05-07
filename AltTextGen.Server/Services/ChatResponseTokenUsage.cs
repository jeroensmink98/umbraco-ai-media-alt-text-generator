using Microsoft.Extensions.AI;

namespace AltTextGen.Server.Services;

internal sealed record ChatResponseTokenUsage(long? InputTokens, long? OutputTokens, long? TotalTokens);

internal static class ChatResponseTokenUsageReader
{
    public static ChatResponseTokenUsage? Read(ChatResponse response)
    {
        object? usage = response.Usage;
        if (usage is null)
        {
            return null;
        }

        long? inputTokens = ReadLongProperty(usage, "InputTokenCount") ?? ReadLongProperty(usage, "PromptTokens");
        long? outputTokens = ReadLongProperty(usage, "OutputTokenCount") ?? ReadLongProperty(usage, "CompletionTokens");
        long? totalTokens = ReadLongProperty(usage, "TotalTokenCount") ?? ReadLongProperty(usage, "TotalTokens");

        if (inputTokens is null && outputTokens is null && totalTokens is null)
        {
            return null;
        }

        return new ChatResponseTokenUsage(inputTokens, outputTokens, totalTokens);
    }

    private static long? ReadLongProperty(object source, string propertyName)
    {
        object? value = source.GetType().GetProperty(propertyName)?.GetValue(source);

        return value switch
        {
            null => null,
            long number => number,
            int number => number,
            short number => number,
            byte number => number,
            _ => null
        };
    }
}
