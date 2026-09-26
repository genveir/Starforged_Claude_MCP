using System.Text.Encodings.Web;
using System.Text.Json;

namespace StarForged_Claude_MCP.Server;

/// <summary>
/// One set of serializer options for everything the server writes: the JSON-RPC envelope and the result
/// text of each tool alike, so a tool's output reads the same whichever class produced it.
/// </summary>
public static class McpJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        // What this serializes is read by a model, never embedded in HTML, so there is nothing to guard
        // against by writing an apostrophe as '; it only makes messages harder to read.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
