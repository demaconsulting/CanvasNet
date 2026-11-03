using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Snapshot tests that pin the scene of single and layered views while the rendering pipeline is refactored for compositions (Phase 11): the whole scene (every item, its
///     offset and every property) of a set of specifications is written as text, and the text must not change. The snapshots in <c>Fixtures/SceneSnapshots</c> were captured from
///     the Phase 10 pipeline before the refactoring; they are original works, like the specifications that are written in this file with made-up data.
/// </summary>
/// <remarks>
///     To capture the snapshots again (after a deliberate change of the output), set the environment variable <c>CANVASNET_SCENE_SNAPSHOT_DIR</c> to the folder
///     <c>test/DemaConsulting.CanvasNet.VegaLite.Tests/Fixtures/SceneSnapshots</c> and run the test: it then writes the files instead of comparing them.
/// </remarks>
public class VegaLiteRefactorSnapshotTests
{
    /// <summary>The environment variable that switches the test from comparing to writing the snapshots.</summary>
    private const string SnapshotDirectoryVariable = "CANVASNET_SCENE_SNAPSHOT_DIR";

    /// <summary>The deepest nesting of values that the snapshot writes before it stops.</summary>
    private const int MaxValueDepth = 8;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The cases: a name and the specification text, with the size of the container for the cases that fit it.</summary>
    private static readonly Dictionary<string, (string Json, int? Container)> Cases = new(StringComparer.Ordinal)
    {
        ["single-bar-nominal"] = ("""
            {
              "title": "Totals",
              "data": { "values": [ { "a": "A", "v": 4 }, { "a": "B", "v": 8 }, { "a": "C", "v": 6 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "title": "Value" } }
            }
            """, null),
        ["single-point-legends"] = ("""
            {
              "width": 240, "height": 160,
              "data": { "values": [ { "x": 1, "y": 3, "k": "p", "s": 5 }, { "x": 4, "y": 9, "k": "q", "s": 25 }, { "x": 7, "y": null, "k": "p", "s": 15 }, { "x": 9, "y": 5, "k": "r", "s": 40 } ] },
              "mark": { "type": "point", "filled": true },
              "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" }, "size": { "field": "s", "type": "quantitative" } }
            }
            """, null),
        ["single-line-temporal"] = ("""
            {
              "width": 280, "height": 120,
              "data": { "values": [ { "d": "2024-01-01", "v": 3 }, { "d": "2024-02-15", "v": 7 }, { "d": "2024-04-01", "v": 5 }, { "d": "2024-06-30", "v": 9 } ] },
              "mark": { "type": "line", "point": true },
              "encoding": { "x": { "field": "d", "type": "temporal" }, "y": { "field": "v", "type": "quantitative" } }
            }
            """, null),
        ["single-stacked-bars"] = ("""
            {
              "data": { "values": [ { "r": "N", "p": "a", "u": 4 }, { "r": "N", "p": "b", "u": 6 }, { "r": "S", "p": "a", "u": 5 }, { "r": "S", "p": "b", "u": 2 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "r", "type": "nominal" }, "y": { "field": "u", "aggregate": "sum", "type": "quantitative" }, "color": { "field": "p", "type": "nominal" } }
            }
            """, null),
        ["single-histogram"] = ("""
            {
              "data": { "values": [ { "w": 1 }, { "w": 3 }, { "w": 4 }, { "w": 8 }, { "w": 9 }, { "w": 12 }, { "w": 21 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "w", "bin": true, "type": "quantitative" }, "y": { "aggregate": "count", "type": "quantitative" } }
            }
            """, null),
        ["single-container-fit"] = ("""
            {
              "width": "container", "height": "container",
              "data": { "values": [ { "a": "A", "v": 4, "k": "m" }, { "a": "B", "v": 8, "k": "n" }, { "a": "C", "v": 6, "k": "m" } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } }
            }
            """, 420),
        ["layered-bar-rule"] = ("""
            {
              "title": "Bars and mean",
              "data": { "values": [ { "a": "A", "v": 4 }, { "a": "B", "v": 8 }, { "a": "C", "v": 6 } ] },
              "transform": [ { "calculate": "datum.v * 10", "as": "w" } ],
              "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "w", "type": "quantitative" } } },
                { "mark": "rule", "transform": [ { "aggregate": [ { "op": "mean", "field": "w", "as": "m" } ] } ],
                  "encoding": { "y": { "field": "m", "type": "quantitative" }, "color": { "value": "red" } } }
              ]
            }
            """, null),
        ["layered-line-point-legend"] = ("""
            {
              "width": 260, "height": 140,
              "data": { "values": [ { "x": 1, "y": 2, "k": "p" }, { "x": 2, "y": 5, "k": "p" }, { "x": 3, "y": 4, "k": "p" }, { "x": 1, "y": 6, "k": "q" }, { "x": 2, "y": 3, "k": "q" }, { "x": 3, "y": 8, "k": "q" } ] },
              "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } },
              "layer": [ { "mark": "line" }, { "mark": { "type": "point", "filled": true } } ]
            }
            """, null),
        ["layered-dual-axis"] = ("""
            {
              "width": 240, "height": 140,
              "data": { "values": [ { "a": "A", "v": 4, "w": 100 }, { "a": "B", "v": 8, "w": 400 }, { "a": "C", "v": 6, "w": 1000 } ] },
              "encoding": { "x": { "field": "a", "type": "nominal" } },
              "layer": [
                { "mark": "bar", "encoding": { "y": { "field": "v", "type": "quantitative", "axis": { "title": "Value" } } } },
                { "mark": { "type": "line", "color": "firebrick" }, "encoding": { "y": { "field": "w", "type": "quantitative", "axis": { "title": "Weight" } } } }
              ],
              "resolve": { "scale": { "y": "independent" } }
            }
            """, null),
        ["layered-own-data-independent-legend"] = ("""
            {
              "width": 220, "height": 130,
              "data": { "values": [ { "a": "A", "v": 4, "k": "m" }, { "a": "B", "v": 8, "k": "n" } ] },
              "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } },
                { "data": { "values": [ { "a": "A", "v": 6, "j": "t" }, { "a": "B", "v": 3, "j": "u" } ] },
                  "mark": { "type": "tick", "thickness": 3 },
                  "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "j", "type": "nominal" } } }
              ],
              "resolve": { "legend": { "color": "independent" }, "scale": { "color": "independent" } }
            }
            """, null),
        ["layered-boxplot"] = ("""
            {
              "data": { "values": [ { "g": "a", "v": 1 }, { "g": "a", "v": 3 }, { "g": "a", "v": 4 }, { "g": "a", "v": 9 }, { "g": "a", "v": 30 }, { "g": "b", "v": 2 }, { "g": "b", "v": 5 }, { "g": "b", "v": 6 }, { "g": "b", "v": 7 } ] },
              "mark": "boxplot",
              "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }
            }
            """, null),
        ["layered-nested-text"] = ("""
            {
              "width": 200, "height": 120,
              "data": { "values": [ { "a": "A", "v": 4 }, { "a": "B", "v": 8 }, { "a": "C", "v": 6 } ] },
              "layer": [
                { "layer": [ { "mark": "bar" }, { "mark": { "type": "text", "dy": -6 }, "encoding": { "text": { "field": "v", "type": "quantitative" } } } ],
                  "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } },
                { "mark": { "type": "rule", "strokeDash": [4, 2] }, "encoding": { "y": { "datum": 5 } } }
              ]
            }
            """, null),
    };

    /// <summary>Gets the names of the cases.</summary>
    public static TheoryData<string> CaseNames => [.. Cases.Keys];

    /// <summary>
    ///     Validates that the scene of single and layered views is exactly the scene that the Phase 10 pipeline produced: every item, its offset and its properties, for single
    ///     views of several marks, guides and sizes (including a view that fits its container) and for layered views that share or separate their axes and legends.
    /// </summary>
    /// <param name="name">The name of the case.</param>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void VegaLiteDocument_Refactor_SceneIsUnchangedForSingleAndLayeredViews(string name)
    {
        // Arrange
        var (json, container) = Cases[name];
        var options = container is { } c ? new VegaLiteRenderOptions { ContainerWidth = c, ContainerHeight = c * 3 / 5 } : new VegaLiteRenderOptions();

        // Act
        var actual = Dump(VegaLiteDocument.Parse(json).BuildScene(options, Ct));

        // Assert: the snapshot is captured on request, else it is the text the pipeline produced before the refactoring
        var capture = Environment.GetEnvironmentVariable(SnapshotDirectoryVariable);
        if (!string.IsNullOrEmpty(capture))
        {
            File.WriteAllText(Path.Combine(capture, name + ".scene.txt"), actual, new UTF8Encoding(false));
            return;
        }

        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SceneSnapshots", name + ".scene.txt")).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates that the cases are rendered the same way twice (so that a difference to a snapshot is a change of the pipeline, not of the run).
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Refactor_SnapshotIsDeterministic()
    {
        // Arrange
        var (json, _) = Cases["layered-line-point-legend"];
        var options = new VegaLiteRenderOptions();

        // Act
        var first = Dump(VegaLiteDocument.Parse(json).BuildScene(options, Ct));
        var second = Dump(VegaLiteDocument.Parse(json).BuildScene(options, Ct));

        // Assert
        Assert.Equal(first, second);
        Assert.Contains("SceneText", first, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Writes a scene as text: the canvas, then every item in drawing order with its depth, its offset and all of its properties.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The text, with LF line ends.</returns>
    private static string Dump(Scene scene)
    {
        var text = new StringBuilder();
        text.Append("scene ").Append(Number(scene.Width)).Append('x').Append(Number(scene.Height)).Append(" background=").Append(scene.Background ?? "null").Append('\n');
        foreach (var entry in scene.Enumerate())
        {
            text.Append(' ', entry.Depth * 2).Append(entry.Item.GetType().Name).Append(" @").Append(Number(entry.OffsetX)).Append(',').Append(Number(entry.OffsetY));
            foreach (var property in PropertiesOf(entry.Item.GetType()).Where(p => !typeof(IEnumerable<SceneItem>).IsAssignableFrom(p.PropertyType)))
            {
                text.Append(' ').Append(property.Name).Append('=').Append(Format(property.GetValue(entry.Item), 0));
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    ///     Gets the readable public instance properties of a type, ordered by name so that the text does not depend on the order of the metadata.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The properties.</returns>
    private static IEnumerable<PropertyInfo> PropertiesOf(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    /// <summary>
    ///     Writes a value: numbers in the round-trip format of the invariant culture, strings quoted, sequences in brackets and other objects with their properties.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The text.</returns>
    private static string Format(object? value, int depth)
    {
        switch (value)
        {
            case null:
                return "null";
            case string s:
                return JsonSerializer.Serialize(s);
            case double d:
                return Number(d);
            case float f:
                return Number(f);
            case bool or int or long or short or byte or uint or ulong:
                return Convert.ToString(value, CultureInfo.InvariantCulture)!;
            case Enum e:
                return e.ToString();
        }

        if (depth >= MaxValueDepth)
        {
            return "...";
        }

        if (value is IEnumerable sequence)
        {
            return "[" + string.Join(", ", sequence.Cast<object?>().Select(v => Format(v, depth + 1))) + "]";
        }

        var type = value.GetType();
        var members = PropertiesOf(type).Select(p => p.Name + "=" + Format(p.GetValue(value), depth + 1));
        return type.Name + "{" + string.Join(", ", members) + "}";
    }

    /// <summary>
    ///     Writes a number in the round-trip format of the invariant culture.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
