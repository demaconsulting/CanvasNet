using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Flowchart;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Flowchart;

/// <summary>
///     Writes a parsed flowchart as the facts the vector generator records from Mermaid's database: the vertices,
///     edges, subgraphs, classes and the layout data, with the keys and the <c>undefined</c> omissions of
///     <c>JSON.stringify</c>.
/// </summary>
internal static class FlowchartFacts
{
    /// <summary>
    ///     Writes the facts of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="diagramType">Mermaid's diagram type id.</param>
    /// <returns>The facts.</returns>
    public static JsonObject Of(FlowchartModel model, MermaidConfig config, string diagramType)
    {
        var data = model.GetData(config, diagramType);
        return new JsonObject
        {
            ["type"] = diagramType,
            ["direction"] = model.Direction,
            ["accTitle"] = model.AccessibleTitle,
            ["accDescr"] = model.AccessibleDescription,
            ["vertices"] = Array(model.Vertices.Select(Vertex)),
            ["edges"] = Array(model.Edges.Select(Edge)),
            ["defaultInterpolate"] = model.DefaultInterpolate,
            ["defaultStyle"] = model.DefaultStyle is null ? null : Strings(model.DefaultStyle),
            ["subgraphs"] = Array(model.Subgraphs.Select(s => Subgraph(model, s, config))),
            ["classes"] = Array(model.Classes.Select(c => new JsonObject
            {
                ["id"] = c.Id,
                ["styles"] = Strings(c.Styles),
                ["textStyles"] = Strings(c.TextStyles),
            })),
            ["data"] = new JsonObject
            {
                ["nodes"] = Array(data.Nodes.Select(Node)),
                ["edges"] = Array(data.Edges.Select(LayoutEdge)),
            },
        };
    }

    /// <summary>
    ///     Converts a YAML value to the JSON value JavaScript would hold.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The JSON value.</returns>
    public static JsonNode? Yaml(YamlNode? node) => node switch
    {
        null => null,
        YamlScalar { Value: null } => null,
        YamlScalar { Value: bool b } => JsonValue.Create(b),
        YamlScalar { Value: double d } => double.IsFinite(d) ? JsonValue.Create(d) : null,
        YamlScalar scalar => JsonValue.Create((string)scalar.Value),
        YamlSequence sequence => Array(sequence.Items.Select(Yaml)),
        YamlMapping mapping => Object(mapping.Entries.Select(e => (e.Key, Yaml(e.Value)))),
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    /// <summary>Writes a vertex.</summary>
    /// <param name="v">The vertex.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Vertex(FlowVertex v)
    {
        var props = new JsonObject();
        foreach (var key in v.Props.Keys())
        {
            props[key] = v.Props[key];
        }

        var result = new JsonObject
        {
            ["id"] = v.Id,
            ["text"] = v.Text,
            ["labelType"] = v.LabelType,
            ["classes"] = Strings(v.Classes),
            ["styles"] = Strings(v.Styles),
            ["domId"] = v.DomId,
            ["props"] = props,
        };
        Optional(result, "type", v.Type);
        Optional(result, "icon", v.Icon);
        Optional(result, "form", v.Form);
        Optional(result, "pos", v.Pos);
        Optional(result, "img", v.Img);
        Optional(result, "constraint", v.Constraint);
        Optional(result, "assetWidth", v.AssetWidth);
        Optional(result, "assetHeight", v.AssetHeight);
        return result;
    }

    /// <summary>Writes an edge.</summary>
    /// <param name="e">The edge.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Edge(FlowEdge e)
    {
        var result = new JsonObject
        {
            ["start"] = e.Start,
            ["end"] = e.End,
            ["type"] = e.Type,
            ["stroke"] = e.Stroke,
            ["text"] = e.Text,
            ["labelType"] = e.LabelType,
            ["id"] = e.Id,
            ["isUserDefinedId"] = e.IsUserDefinedId,
            ["classes"] = Strings(e.Classes),
        };
        Optional(result, "length", e.Length);
        Optional(result, "interpolate", e.Interpolate);
        if (e.Style is not null)
        {
            result["style"] = Strings(e.Style);
        }

        if (e.Animate is not null)
        {
            result["animate"] = Yaml(e.Animate);
        }

        if (e.Animation is not null)
        {
            result["animation"] = Yaml(e.Animation);
        }

        return result;
    }

    /// <summary>Writes a subgraph.</summary>
    /// <param name="model">The model.</param>
    /// <param name="s">The subgraph.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Subgraph(FlowchartModel model, FlowSubgraph s, MermaidConfig config)
    {
        var result = new JsonObject
        {
            ["id"] = s.Id,
            ["nodes"] = Strings(s.Nodes),
            ["title"] = s.Title,
            ["classes"] = Strings(s.Classes),
            ["labelType"] = s.LabelType,
        };
        Optional(result, "dir", model.SubgraphDirection(s, config.Flowchart.InheritDir));
        if (s.Metadata is not null)
        {
            result["metadata"] = Object(s.Metadata.Select(e => (e.Key, Yaml(e.Value))));
        }

        return result;
    }

    /// <summary>Writes a layout node.</summary>
    /// <param name="n">The node.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Node(LayoutNode n)
    {
        var result = new JsonObject
        {
            ["id"] = n.Id,
            ["label"] = n.Label,
            ["labelType"] = n.LabelType,
            ["labelStyle"] = n.LabelStyle,
            ["padding"] = n.Padding,
            ["cssCompiledStyles"] = Strings(n.CssCompiledStyles),
            ["cssClasses"] = n.CssClasses,
            ["look"] = n.Look,
            ["isGroup"] = n.IsGroup,
            ["shape"] = n.Shape,
        };
        Optional(result, "parentId", n.ParentId);
        if (n.CssStyles is not null)
        {
            result["cssStyles"] = Strings(n.CssStyles);
        }

        Optional(result, "dir", n.Dir);
        Optional(result, "domId", n.DomId);
        Optional(result, "icon", n.Icon);
        Optional(result, "pos", n.Pos);
        Optional(result, "img", n.Img);
        Optional(result, "assetWidth", n.AssetWidth);
        Optional(result, "assetHeight", n.AssetHeight);
        Optional(result, "constraint", n.Constraint);
        return result;
    }

    /// <summary>Writes a layout edge.</summary>
    /// <param name="e">The edge.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject LayoutEdge(LayoutEdge e)
    {
        var result = new JsonObject
        {
            ["id"] = e.Id,
            ["isUserDefinedId"] = e.IsUserDefinedId,
            ["start"] = e.Start,
            ["end"] = e.End,
            ["type"] = e.Type,
            ["label"] = e.Label,
            ["labelType"] = e.LabelType,
            ["labelpos"] = e.LabelPos,
            ["thickness"] = e.Thickness,
            ["classes"] = e.Classes,
            ["arrowTypeStart"] = e.ArrowTypeStart,
            ["arrowTypeEnd"] = e.ArrowTypeEnd,
            ["arrowheadStyle"] = e.ArrowheadStyle,
            ["cssCompiledStyles"] = Strings(e.CssCompiledStyles),
            ["labelStyle"] = Strings(e.LabelStyle),
            ["style"] = Strings(e.Style),
            ["pattern"] = e.Pattern,
            ["look"] = e.Look,
        };
        Optional(result, "minlen", e.MinLen);
        Optional(result, "curve", e.Curve);
        if (e.Animate is not null)
        {
            result["animate"] = Yaml(e.Animate);
        }

        if (e.Animation is not null)
        {
            result["animation"] = Yaml(e.Animation);
        }

        return result;
    }

    /// <summary>Adds a text when it is set.</summary>
    /// <param name="obj">The object.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, or null.</param>
    private static void Optional(JsonObject obj, string key, string? value)
    {
        if (value is not null)
        {
            obj[key] = value;
        }
    }

    /// <summary>Adds a number when it is set; NaN is written as null, as JSON.stringify does.</summary>
    /// <param name="obj">The object.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, or null.</param>
    private static void Optional(JsonObject obj, string key, double? value)
    {
        if (value is { } number)
        {
            obj[key] = double.IsFinite(number) ? JsonValue.Create(number) : null;
        }
    }

    /// <summary>Writes texts as a JSON array.</summary>
    /// <param name="items">The texts.</param>
    /// <returns>The array.</returns>
    private static JsonArray Strings(IEnumerable<string> items) => Array(items.Select(s => (JsonNode?)JsonValue.Create(s)));

    /// <summary>Writes values as a JSON array.</summary>
    /// <param name="items">The values.</param>
    /// <returns>The array.</returns>
    private static JsonArray Array(IEnumerable<JsonNode?> items) => new([.. items]);

    /// <summary>Writes entries as a JSON object; a later key replaces an earlier one.</summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The object.</returns>
    private static JsonObject Object(IEnumerable<(string Key, JsonNode? Value)> entries)
    {
        var result = new JsonObject();
        foreach (var (key, value) in entries)
        {
            result[key] = value;
        }

        return result;
    }
}
