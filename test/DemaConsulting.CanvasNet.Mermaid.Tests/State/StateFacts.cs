using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.State;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.State;

/// <summary>
///     Test support of the State subsystem: parses a text as the document will, and writes a parsed model as the facts
///     the vector generator records from Mermaid's state database, its statement tree and its <c>getData()</c>, with the
///     keys and the <c>undefined</c> omissions of <c>JSON.stringify</c>.
/// </summary>
internal static class StateFacts
{
    /// <summary>
    ///     Parses a state text: preprocessing, detection, then the state parser.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="sourceName">The source name, or null.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="settings">The detector settings, or null for the defaults.</param>
    /// <returns>The model.</returns>
    public static StateModel Parse(string text, string? sourceName = null, MermaidLimits? limits = null, DetectorSettings? settings = null)
    {
        var effective = limits ?? MermaidLimits.Default;
        var source = Preprocessor.Run(text, sourceName, effective);
        var detected = DiagramDetector.Detect(source.Code, settings ?? DetectorSettings.Default);
        return StateParser.Parse(source, detected, effective);
    }

    /// <summary>
    ///     Resolves the default configuration of a state text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The configuration.</returns>
    public static MermaidConfig DefaultConfig(string text) =>
        MermaidConfigResolver.Resolve(Preprocessor.Run(text, null, MermaidLimits.Default), "stateDiagram", new MermaidRenderOptions(), null);

    /// <summary>
    ///     Writes the facts of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="config">The effective configuration.</param>
    /// <returns>The facts.</returns>
    public static JsonObject Of(StateModel model, MermaidConfig config)
    {
        var data = model.GetData(config);
        return new JsonObject
        {
            ["type"] = model.MermaidId,
            ["direction"] = model.Direction,
            ["title"] = string.Empty,
            ["accTitle"] = model.AccessibleTitle,
            ["accDescr"] = model.AccessibleDescription,
            ["rootDoc"] = Array(model.RootDocument.Select(Statement)),
            ["states"] = Array(model.States.Select(State)),
            ["relations"] = Array(model.Relations.Select(r => new JsonObject { ["id1"] = r.Id1, ["id2"] = r.Id2, ["relationTitle"] = r.RelationTitle })),
            ["styleClasses"] = Array(model.StyleClasses.Select(s => new JsonObject
            {
                ["id"] = s.Id,
                ["styles"] = Strings(s.Styles),
                ["textStyles"] = Strings(s.TextStyles),
            })),
            ["links"] = Array(model.Links.Select(l => new JsonObject { ["id"] = Statement(l.Target), ["url"] = l.Url, ["tooltip"] = l.Tooltip })),
            ["data"] = new JsonObject
            {
                ["direction"] = data.Direction,
                ["nodes"] = Array(data.Nodes.Select((node, i) => Node(node, model.Nodes[i]))),
                ["edges"] = Array(data.Edges.Select(Edge)),
            },
        };
    }

    /// <summary>Writes a statement: a statement the grammar keeps as text has no keys.</summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Statement(StateStatement statement)
    {
        var result = new JsonObject();
        if (statement.Stmt is not { } kind)
        {
            return result;
        }

        result["stmt"] = kind;
        if (kind is "state" or "classDef" or "style" or "applyClass")
        {
            result["id"] = statement.Id;
        }
        else if (kind == "click")
        {
            result["id"] = Statement(statement.Target!);
        }

        Optional(result, "type", statement.Type);
        if (statement.DescriptionParts is { } parts)
        {
            result["description"] = Strings(parts);
        }
        else
        {
            Optional(result, "description", statement.Description);
        }

        if (statement.Note is { } note)
        {
            result["note"] = new JsonObject { ["position"] = note.Position, ["text"] = note.Text };
        }

        if (statement.Classes is { } classes)
        {
            result["classes"] = Strings(classes);
        }

        Optional(result, "classes", statement.Styles);
        if (statement.Start is { } start)
        {
            result["start"] = start;
        }

        Optional(result, "value", statement.Value);
        Optional(result, "styleClass", statement.StyleClass);
        Optional(result, "url", statement.Url);
        Optional(result, "tooltip", statement.Tooltip);
        if (kind == "relation")
        {
            result["state1"] = Statement(statement.State1!);
            result["state2"] = Statement(statement.State2!);
        }

        if (statement.Doc is { } document)
        {
            result["doc"] = Array(document.Select(Statement));
        }

        return result;
    }

    /// <summary>Writes a state of the database.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject State(StateRecord state)
    {
        var result = new JsonObject
        {
            ["id"] = state.Id,
            ["type"] = state.Type,
            ["descriptions"] = Strings(state.Descriptions),
        };
        if (state.Note is { } note)
        {
            result["note"] = new JsonObject { ["position"] = note.Position, ["text"] = note.Text };
        }

        result["classes"] = Strings(state.Classes);
        result["styles"] = Strings(state.Styles);
        result["textStyles"] = Strings(state.TextStyles);
        return result;
    }

    /// <summary>Writes a node of the layout data, with the type and position the layout node does not carry.</summary>
    /// <param name="node">The node.</param>
    /// <param name="source">The model's node of the same index.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Node(LayoutNode node, StateNode source)
    {
        Assert.Equal(source.Id, node.Id);
        var result = new JsonObject { ["id"] = node.Id };
        Optional(result, "label", node.Label);
        if (node.Descriptions is { } descriptions)
        {
            result["description"] = Strings(descriptions);
        }

        if (node.Shape.Length > 0)
        {
            result["shape"] = node.Shape;
        }

        if (node.IsGroup)
        {
            result["type"] = "group";
        }

        result["isGroup"] = node.IsGroup;
        Optional(result, "parentId", node.ParentId);
        Optional(result, "dir", node.Dir);
        Optional(result, "domId", node.DomId);
        result["padding"] = node.Padding;
        if (node.Rx is { } rx)
        {
            result["rx"] = rx;
        }

        if (node.Ry is { } ry)
        {
            result["ry"] = ry;
        }

        if (node.CenterLabel)
        {
            result["centerLabel"] = true;
        }

        result["cssClasses"] = node.CssClasses;
        result["cssStyles"] = Strings(node.CssStyles ?? []);
        result["cssCompiledStyles"] = Strings(node.CssCompiledStyles);
        result["labelStyle"] = node.LabelStyle;
        Optional(result, "labelType", node.LabelType);
        Optional(result, "position", source.Position);
        Optional(result, "look", node.Look);
        return result;
    }

    /// <summary>Writes an edge of the layout data; the edge of a note has no label.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Edge(LayoutEdge edge)
    {
        Assert.Equal(1, edge.MinLen);
        var result = new JsonObject
        {
            ["id"] = edge.Id,
            ["start"] = edge.Start,
            ["end"] = edge.End,
        };
        if (!edge.Classes.Contains("note-edge", StringComparison.Ordinal))
        {
            result["label"] = edge.Label;
        }

        result["arrowhead"] = edge.Type;
        result["arrowTypeEnd"] = edge.ArrowTypeEnd;
        result["style"] = string.Join(";", edge.Style);
        result["labelStyle"] = string.Join(";", edge.LabelStyle);
        result["arrowheadStyle"] = edge.ArrowheadStyle;
        result["labelpos"] = edge.LabelPos;
        result["labelType"] = edge.LabelType;
        result["thickness"] = edge.Thickness;
        result["classes"] = edge.Classes;
        result["look"] = edge.Look;
        return result;
    }

    /// <summary>Adds a key when its value is defined.</summary>
    /// <param name="result">The JSON object.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, or null for <c>undefined</c>.</param>
    private static void Optional(JsonObject result, string key, string? value)
    {
        if (value is not null)
        {
            result[key] = value;
        }
    }

    /// <summary>Writes a list of strings.</summary>
    /// <param name="items">The strings.</param>
    /// <returns>The JSON array.</returns>
    private static JsonArray Strings(IEnumerable<string> items) => Array(items.Select(item => (JsonNode?)JsonValue.Create(item)));

    /// <summary>Writes a list of JSON values.</summary>
    /// <param name="items">The values.</param>
    /// <returns>The JSON array.</returns>
    private static JsonArray Array(IEnumerable<JsonNode?> items) => new([.. items]);
}
