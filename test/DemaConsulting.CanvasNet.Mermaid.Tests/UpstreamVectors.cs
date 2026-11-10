using System.Text.Json;
using System.Text.Json.Nodes;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Access to the upstream vectors under <c>Fixtures/upstream-vectors/</c>: expectations recorded from the pinned
///     Mermaid by the generator scripts of <c>benchmarks/visual-diff/mermaid/</c>.
/// </summary>
internal static class UpstreamVectors
{
    /// <summary>The theme, configuration and color vectors, read once.</summary>
    private static readonly Lazy<JsonObject> ThemeVectors = new(() => Load("themes.json"));

    /// <summary>The flowchart parser vectors, read once.</summary>
    private static readonly Lazy<JsonObject> FlowchartVectors = new(() => Load("flowchart-parser.json"));

    /// <summary>The sequence parser vectors, read once.</summary>
    private static readonly Lazy<JsonObject> SequenceVectors = new(() => Load("sequence-parser.json"));

    /// <summary>The class parser vectors, read once.</summary>
    private static readonly Lazy<JsonObject> ClassVectors = new(() => Load("class-parser.json"));

    /// <summary>The state parser vectors, read once.</summary>
    private static readonly Lazy<JsonObject> StateVectors = new(() => Load("state-parser.json"));

    /// <summary>The ER parser vectors, read once.</summary>
    private static readonly Lazy<JsonObject> ErVectors = new(() => Load("er-parser.json"));

    /// <summary>
    ///     Gets the flowchart parser cases of <c>flowchart-parser.json</c>.
    /// </summary>
    public static JsonArray Flowcharts => FlowchartVectors.Value["cases"]!.AsArray();

    /// <summary>
    ///     Gets the sequence parser cases of <c>sequence-parser.json</c>.
    /// </summary>
    public static JsonArray Sequences => SequenceVectors.Value["cases"]!.AsArray();

    /// <summary>
    ///     Gets the class parser cases of <c>class-parser.json</c>.
    /// </summary>
    public static JsonArray Classes => ClassVectors.Value["cases"]!.AsArray();

    /// <summary>
    ///     Gets the state parser cases of <c>state-parser.json</c>.
    /// </summary>
    public static JsonArray States => StateVectors.Value["cases"]!.AsArray();

    /// <summary>
    ///     Gets the ER parser cases of <c>er-parser.json</c>.
    /// </summary>
    public static JsonArray Ers => ErVectors.Value["cases"]!.AsArray();

    /// <summary>
    ///     Gets the theme, configuration and color vectors (<c>themes.json</c>).
    /// </summary>
    public static JsonObject Themes => ThemeVectors.Value;

    /// <summary>
    ///     Gets the color cases of <c>themes.json</c>.
    /// </summary>
    public static JsonArray Colors => Themes["colors"]!.AsArray();

    /// <summary>
    ///     Gets the configuration cases of <c>themes.json</c>.
    /// </summary>
    public static JsonArray Configs => Themes["configs"]!.AsArray();

    /// <summary>
    ///     Gets the configuration case of a name.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <returns>The case.</returns>
    public static JsonObject Config(string name) =>
        Configs.Select(item => item!.AsObject()).Single(item => (string?)item["name"] == name);

    /// <summary>
    ///     Gets the indexes of a list of cases, as theory data.
    /// </summary>
    /// <param name="cases">The cases.</param>
    /// <returns>The indexes.</returns>
    public static TheoryData<int> Indexes(JsonArray cases)
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < cases.Count; i++)
        {
            data.Add(i);
        }

        return data;
    }

    /// <summary>
    ///     Gets whether two JSON values are equal: objects by their keys regardless of order, arrays element by element,
    ///     numbers by value.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="path">The path of the values, for the message.</param>
    /// <returns>Null when equal, else a description of the first difference.</returns>
    public static string? Difference(JsonNode? expected, JsonNode? actual, string path = "$")
    {
        switch (expected)
        {
            case null:
                return actual is null ? null : $"{path}: expected null, got {actual.ToJsonString()}";
            case JsonObject obj:
                if (actual is not JsonObject other)
                {
                    return $"{path}: expected an object, got {actual?.ToJsonString() ?? "null"}";
                }

                foreach (var (key, member) in obj)
                {
                    if (!other.ContainsKey(key))
                    {
                        return $"{path}.{key}: missing (expected {member?.ToJsonString() ?? "null"})";
                    }

                    var difference = Difference(member, other[key], $"{path}.{key}");
                    if (difference is not null)
                    {
                        return difference;
                    }
                }

                var extra = other.Select(pair => pair.Key).FirstOrDefault(key => !obj.ContainsKey(key));
                return extra is null ? null : $"{path}.{extra}: unexpected {other[extra]?.ToJsonString() ?? "null"}";
            case JsonArray array:
                if (actual is not JsonArray items || items.Count != array.Count)
                {
                    return $"{path}: expected {array.ToJsonString()}, got {actual?.ToJsonString() ?? "null"}";
                }

                for (var i = 0; i < array.Count; i++)
                {
                    var difference = Difference(array[i], items[i], $"{path}[{i}]");
                    if (difference is not null)
                    {
                        return difference;
                    }
                }

                return null;
            default:
                if (actual is not JsonValue value)
                {
                    return $"{path}: expected {expected.ToJsonString()}, got {actual?.ToJsonString() ?? "null"}";
                }

                if (expected.GetValueKind() == JsonValueKind.Number && value.GetValueKind() == JsonValueKind.Number)
                {
                    return BitConverter.DoubleToInt64Bits(expected.GetValue<double>()) == BitConverter.DoubleToInt64Bits(value.GetValue<double>())
                        ? null
                        : $"{path}: expected {expected.ToJsonString()}, got {value.ToJsonString()}";
                }

                return JsonNode.DeepEquals(expected, value) ? null : $"{path}: expected {expected.ToJsonString()}, got {value.ToJsonString()}";
        }
    }

    /// <summary>
    ///     Reads a vector file.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <returns>The JSON object.</returns>
    internal static JsonObject Load(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "upstream-vectors", name);
        return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }
}
