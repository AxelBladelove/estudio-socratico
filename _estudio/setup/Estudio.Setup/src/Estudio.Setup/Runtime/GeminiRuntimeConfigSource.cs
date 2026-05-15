namespace Estudio.Setup.Runtime;

public sealed record GeminiRuntimeConfigSource(
    GeminiRuntimeSection Gemini,
    ContentRuntimeSection Content);

public sealed record GeminiRuntimeSection(
    string Mode,
    string Model,
    string KeyEncoding,
    IReadOnlyList<string> KeyParts);

public sealed record ContentRuntimeSection(
    string Provider,
    string CatalogSource);
