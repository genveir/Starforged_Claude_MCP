using StarForged_Claude_MCP.Database;
using System.Text.Json;

namespace StarForged_Claude_MCP.Server.Tools;

/// <summary>
/// The arguments of one tool call, read by their camelCase key. A value that is missing, too long or of
/// the wrong type is refused with an <see cref="ArgumentException"/> naming the argument, capitalized.
/// </summary>
public class ToolArguments
{
    private readonly Dictionary<string, object> _arguments;

    public ToolArguments(Dictionary<string, object> arguments)
    {
        _arguments = arguments;
    }

    public string RequireCategory()
    {
        var category = RequireString("category", maxLength: 200);

        if (!CategoryPath.IsWellFormed(category))
            throw new ArgumentException(
                $"Category '{category}' is not a well-formed category path: its levels are separated by '{CategoryPath.Separator}', " +
                "and none of them can be empty or start or end with a space.");

        return category;
    }

    public string RequireString(string key, int maxLength)
    {
        var value = _arguments.TryGetValue(key, out var raw) ? raw?.ToString() : null;

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{DisplayName(key)} cannot be empty");
        if (value.Length > maxLength)
            throw new ArgumentException($"{DisplayName(key)} exceeds maximum length of {maxLength:N0} characters");

        return value;
    }

    /// <summary>
    /// Like RequireString, but an empty value is allowed through rather than refused: newText is
    /// required to be present, but an empty string is a legitimate way to delete oldText outright.
    /// </summary>
    public string RequireStringAllowingEmpty(string key, int maxLength)
    {
        var value = RequirePresent(key).ToString() ?? string.Empty;

        if (value.Length > maxLength)
            throw new ArgumentException($"{DisplayName(key)} exceeds maximum length of {maxLength:N0} characters");

        return value;
    }

    public string? OptionalString(string key, int maxLength)
    {
        var value = ReadOptional(key);

        if (value != null && value.Length > maxLength)
            throw new ArgumentException($"{DisplayName(key)} exceeds maximum length of {maxLength:N0} characters");

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// An absent summary and an empty one mean different things on a write: absent keeps whatever the
    /// document already carries, empty clears it. Both survive as far as the facade, which is where
    /// that distinction is resolved against the stored summary.
    /// </summary>
    public string? OptionalSummary()
    {
        var summary = ReadOptional("summary");

        if (summary != null && summary.Length > 512)
            throw new ArgumentException("Summary exceeds maximum length of 512 characters");

        return summary?.Trim();
    }

    public int RequireInt(string key) =>
        ReadInt(RequirePresent(key), DisplayName(key));

    public int OptionalInt(string key, int defaultValue)
    {
        if (IsAbsent(key, out var raw))
            return defaultValue;

        return ReadInt(raw, DisplayName(key));
    }

    public int[] RequireIntArray(string key)
    {
        var raw = RequirePresent(key);
        var entries = $"{DisplayName(key)} entries";

        if (raw is JsonElement je)
        {
            if (je.ValueKind != JsonValueKind.Array)
                throw WrongType(DisplayName(key), Describe(je), expected: "a list of whole numbers");

            return [.. je.EnumerateArray().Select(element => ReadInt(element, entries))];
        }

        if (raw is not IEnumerable<object> values)
            throw WrongType(DisplayName(key), found: "a single value", expected: "a list of whole numbers");

        return [.. values.Select(value => ReadInt(value, entries))];
    }

    public bool RequireBool(string key)
    {
        var raw = RequirePresent(key);
        const string expected = "true or false, written without quotes";

        if (raw is JsonElement je)
        {
            if (je.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw WrongType(DisplayName(key), Describe(je), expected);

            return je.GetBoolean();
        }

        if (raw is string text)
            throw WrongType(DisplayName(key), $"the text \"{text}\"", expected);

        return Convert.ToBoolean(raw);
    }

    public bool OptionalBool(string key, bool defaultValue)
    {
        if (IsAbsent(key, out _))
            return defaultValue;

        return RequireBool(key);
    }

    /// <summary>
    /// For a flag whose absence means something of its own, such as leaving a setting as it is.
    /// </summary>
    public bool? OptionalBool(string key)
    {
        if (IsAbsent(key, out _))
            return null;

        return RequireBool(key);
    }

    /// <summary>
    /// Argument problems are told to the caller in the same terms as any other refusal, rather than
    /// surfacing as an unhandled conversion failure: a caller that sent "3" for a number learns that
    /// much and can send 3, where a generic failure leaves it nothing to go on.
    /// </summary>
    private static int ReadInt(object raw, string name)
    {
        const string expected = "a whole number, written as a number rather than in quotes";

        if (raw is JsonElement je)
        {
            if (je.ValueKind != JsonValueKind.Number || !je.TryGetInt32(out var value))
                throw WrongType(name, Describe(je), expected);

            return value;
        }

        if (raw is string text)
            throw WrongType(name, $"the text \"{text}\"", expected);

        return Convert.ToInt32(raw);
    }

    private object RequirePresent(string key)
    {
        if (IsAbsent(key, out var raw))
            throw new ArgumentException($"{DisplayName(key)} is required");

        return raw;
    }

    private string? ReadOptional(string key) =>
        IsAbsent(key, out var raw) ? null : raw.ToString();

    private bool IsAbsent(string key, out object raw)
    {
        if (!_arguments.TryGetValue(key, out var value) || value == null || value is JsonElement { ValueKind: JsonValueKind.Null })
        {
            raw = null!;
            return true;
        }

        raw = value;
        return false;
    }

    private static ArgumentException WrongType(string name, string found, string expected) =>
        new($"{name} has to be {expected}, but {found} was sent.");

    private static string Describe(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => $"the text \"{element.GetString()}\"",
        JsonValueKind.True or JsonValueKind.False => "a true/false value",
        JsonValueKind.Array => "a list",
        JsonValueKind.Object => "an object",
        JsonValueKind.Number => "a number that is not whole",
        _ => "nothing"
    };

    private static string DisplayName(string key) => char.ToUpperInvariant(key[0]) + key[1..];
}
