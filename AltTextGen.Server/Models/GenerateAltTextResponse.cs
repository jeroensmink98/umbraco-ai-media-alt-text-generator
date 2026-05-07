namespace AltTextGen.Server.Models;

public sealed record GenerateAltTextResponse(
    Guid MediaKey,
    string PropertyAlias,
    string AltText,
    string Provider,
    bool Saved);
