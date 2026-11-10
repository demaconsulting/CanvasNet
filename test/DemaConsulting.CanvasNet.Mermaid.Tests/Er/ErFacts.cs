using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Er;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Er;

/// <summary>
///     Test support of the Er subsystem: parses a text as the document will, and writes a parsed model as the facts the
///     vector generator records from Mermaid's ER database and its <c>getData()</c>, with the keys and the
///     <c>undefined</c> omissions of <c>JSON.stringify</c>.
/// </summary>
internal static class ErFacts
{
    /// <summary>
    ///     Parses an ER text: preprocessing, detection, then the ER parser.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="sourceName">The source name, or null.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="settings">The detector settings, or null for the defaults.</param>
    /// <returns>The model.</returns>
    public static ErModel Parse(string text, string? sourceName = null, MermaidLimits? limits = null, DetectorSettings? settings = null)
    {
        var effective = limits ?? MermaidLimits.Default;
        var source = Preprocessor.Run(text, sourceName, effective);
        var detected = DiagramDetector.Detect(source.Code, settings ?? DetectorSettings.Default);
        return ErParser.Parse(source, detected, effective);
    }

    /// <summary>
    ///     Resolves the default configuration of an ER text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The configuration.</returns>
    public static MermaidConfig DefaultConfig(string text) =>
        MermaidConfigResolver.Resolve(Preprocessor.Run(text, null, MermaidLimits.Default), "er", new MermaidRenderOptions(), null);

    /// <summary>
    ///     Writes the facts of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="config">The effective configuration.</param>
    /// <returns>The facts.</returns>
    public static JsonObject Of(ErModel model, MermaidConfig config)
    {
        var data = model.GetData(config);
        var entities = model.Entities.ToDictionary(e => e.Id, StringComparer.Ordinal);
        return new JsonObject
        {
            ["type"] = ErModel.MermaidId,
            ["direction"] = model.Direction,
            ["title"] = string.Empty,
            ["accTitle"] = model.AccessibleTitle,
            ["accDescr"] = model.AccessibleDescription,
            ["entities"] = Array(model.Entities.Select(Entity)),
            ["relationships"] = Array(model.Relationships.Select(r => new JsonObject
            {
                ["entityA"] = r.EntityA,
                ["roleA"] = r.RoleA,
                ["entityB"] = r.EntityB,
                ["relSpec"] = new JsonObject { ["cardA"] = r.RelSpec.CardA, ["relType"] = r.RelSpec.RelType, ["cardB"] = r.RelSpec.CardB },
            })),
            ["styleClasses"] = Array(model.StyleClasses.Select(s => new JsonObject
            {
                ["id"] = s.Id,
                ["styles"] = Strings(s.Styles),
                ["textStyles"] = Strings(s.TextStyles),
            })),
            ["subgraphs"] = Array(model.Subgraphs.Select(Subgraph)),
            ["data"] = new JsonObject
            {
                ["direction"] = data.Direction,
                ["nodes"] = Array(data.Nodes.Select(node => Node(node, node.IsGroup ? null : entities[node.Id]))),
                ["edges"] = Array(data.Edges.Select(Edge)),
            },
        };
    }

    /// <summary>Writes an entity of the database.</summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Entity(ErEntity entity) => new()
    {
        ["name"] = entity.Name,
        ["id"] = entity.Id,
        ["label"] = entity.Name,
        ["alias"] = entity.Alias,
        ["shape"] = "erBox",
        ["cssClasses"] = entity.CssClasses,
        ["cssStyles"] = Strings(entity.CssStyles),
        ["labelType"] = "markdown",
        ["attributes"] = Array(entity.Attributes.Select(a => Attribute(a.Type, a.Name, a.Keys, a.Comment))),
    };

    /// <summary>Writes a subgraph of the database; a subgraph without a direction has no <c>dir</c>.</summary>
    /// <param name="subgraph">The subgraph.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Subgraph(ErSubgraph subgraph)
    {
        var result = new JsonObject
        {
            ["id"] = subgraph.Id,
            ["title"] = subgraph.Title,
            ["nodes"] = Strings(subgraph.Nodes),
            ["classes"] = Strings(subgraph.Classes),
            ["cssStyles"] = Strings(subgraph.CssStyles),
        };
        Optional(result, "dir", subgraph.Dir);
        result["labelType"] = "markdown";
        return result;
    }

    /// <summary>Writes an attribute.</summary>
    /// <param name="type">The type.</param>
    /// <param name="name">The name.</param>
    /// <param name="keys">The keys.</param>
    /// <param name="comment">The comment.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Attribute(string type, string name, IReadOnlyList<string> keys, string comment) => new()
    {
        ["type"] = type,
        ["name"] = name,
        ["keys"] = Strings(keys),
        ["comment"] = comment,
    };

    /// <summary>
    ///     Writes a node of the layout data: a cluster with the keys of Mermaid's subgraph node, an entity with those of its
    ///     entity node and the color index the model gave it.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="entity">The entity of an entity node, or null for a cluster.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Node(LayoutNode node, ErEntity? entity)
    {
        var result = new JsonObject { ["id"] = node.Id, ["label"] = node.Label };
        if (entity is not null)
        {
            result["alias"] = node.ErBox!.Alias;
        }

        result["shape"] = node.Shape;
        result["isGroup"] = node.IsGroup;
        Optional(result, "parentId", node.ParentId);
        Optional(result, "dir", node.Dir);
        if (entity is null)
        {
            result["padding"] = node.Padding;
        }

        result["cssClasses"] = node.CssClasses;
        result["cssStyles"] = Strings(node.CssStyles ?? []);
        result["cssCompiledStyles"] = Strings(node.CssCompiledStyles);
        if (entity is not null)
        {
            result["colorIndex"] = (double?)entity.ColorIndex;
        }
        else
        {
            result["labelStyle"] = node.LabelStyle;
        }

        result["labelType"] = node.LabelType;
        result["look"] = node.Look;
        if (entity is not null)
        {
            result["attributes"] = Array(node.ErBox!.Attributes.Select(a => Attribute(a.Type, a.Name, a.Keys, a.Comment)));
        }

        return result;
    }

    /// <summary>Writes an edge of the layout data.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Edge(LayoutEdge edge)
    {
        Assert.Equal(1, edge.MinLen);
        return new JsonObject
        {
            ["id"] = edge.Id,
            ["start"] = edge.Start,
            ["end"] = edge.End,
            ["type"] = edge.Type,
            ["curve"] = edge.Curve,
            ["label"] = edge.Label,
            ["labelpos"] = edge.LabelPos,
            ["labelType"] = edge.LabelType,
            ["thickness"] = edge.Thickness,
            ["classes"] = edge.Classes,
            ["arrowTypeStart"] = edge.ArrowTypeStart,
            ["arrowTypeEnd"] = edge.ArrowTypeEnd,
            ["pattern"] = edge.Pattern,
            ["look"] = edge.Look,
        };
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
