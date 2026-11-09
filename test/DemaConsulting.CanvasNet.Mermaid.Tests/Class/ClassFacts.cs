using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Class;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Class;

/// <summary>
///     Test support of the Class subsystem: parses a text as the document will, and writes a parsed model as the facts
///     the vector generator records from Mermaid's class database and its <c>getData()</c>, with the keys and the
///     <c>undefined</c> omissions of <c>JSON.stringify</c>.
/// </summary>
internal static class ClassFacts
{
    /// <summary>
    ///     Parses a class text: preprocessing, detection, then the class parser.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="sourceName">The source name, or null.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The model.</returns>
    public static ClassModel Parse(string text, string? sourceName = null, MermaidLimits? limits = null)
    {
        var effective = limits ?? MermaidLimits.Default;
        var source = Preprocessor.Run(text, sourceName, effective);
        var detected = DiagramDetector.Detect(source.Code, DetectorSettings.Default);
        return ClassParser.Parse(source, detected, effective);
    }

    /// <summary>
    ///     Resolves the default configuration of a class text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The configuration.</returns>
    public static MermaidConfig DefaultConfig(string text) =>
        MermaidConfigResolver.Resolve(Preprocessor.Run(text, null, MermaidLimits.Default), "classDiagram", new MermaidRenderOptions(), null);

    /// <summary>
    ///     Writes the facts of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="config">The effective configuration.</param>
    /// <returns>The facts.</returns>
    public static JsonObject Of(ClassModel model, MermaidConfig config)
    {
        var data = model.GetData(config);
        var noteEdges = data.Edges.Count - model.Relations.Count;
        return new JsonObject
        {
            ["type"] = model.MermaidId,
            ["direction"] = model.Direction,
            ["title"] = string.Empty,
            ["accTitle"] = model.AccessibleTitle,
            ["accDescr"] = model.AccessibleDescription,
            ["classes"] = Array(model.Classes.Select(Class)),
            ["relations"] = Array(model.Relations.Select(Relation)),
            ["notes"] = Array(model.Notes.Select(Note)),
            ["namespaces"] = Array(model.Namespaces.Select(Namespace)),
            ["interfaces"] = Array(model.Interfaces.Select(i => new JsonObject { ["id"] = i.Id, ["label"] = i.Label, ["classId"] = i.ClassId })),
            ["styleClasses"] = Array(model.StyleClasses.Select(s => new JsonObject
            {
                ["id"] = s.Id,
                ["styles"] = Strings(s.Styles),
                ["textStyles"] = Strings(s.TextStyles),
            })),
            ["data"] = new JsonObject
            {
                ["direction"] = data.Direction,
                ["nodes"] = Array(data.Nodes.Select(node => Node(model, node))),
                ["edges"] = Array(data.Edges.Select((edge, i) => i < noteEdges ? NoteEdge(edge) : RelationEdge(edge, model.Relations[i - noteEdges]))),
            },
        };
    }

    /// <summary>Writes a class.</summary>
    /// <param name="node">The class.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Class(ClassNode node)
    {
        var result = new JsonObject
        {
            ["id"] = node.Id,
            ["type"] = node.Type,
            ["label"] = node.Label,
            ["text"] = node.Text,
            ["shape"] = "classBox",
            ["cssClasses"] = node.CssClasses,
            ["annotations"] = Strings(node.Annotations),
            ["styles"] = Strings(node.Styles),
            ["domId"] = node.DomId,
            ["members"] = Array(node.Members.Select(Member)),
            ["methods"] = Array(node.Methods.Select(Member)),
        };
        Optional(result, "parent", node.Parent);
        AddLinkData(result, node);
        return result;
    }

    /// <summary>Writes a member.</summary>
    /// <param name="member">The member.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Member(ClassMember member)
    {
        var result = new JsonObject
        {
            ["id"] = member.Id,
            ["memberType"] = member.MemberType,
            ["visibility"] = member.Visibility,
            ["classifier"] = member.Classifier,
            ["text"] = member.Text,
            ["display"] = new JsonObject { ["displayText"] = member.DisplayText, ["cssStyle"] = member.CssStyle },
        };
        Optional(result, "parameters", member.Parameters);
        Optional(result, "returnType", member.ReturnType);
        return result;
    }

    /// <summary>Writes a relation.</summary>
    /// <param name="relation">The relation.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Relation(ClassRelation relation)
    {
        var result = new JsonObject
        {
            ["id1"] = relation.Id1,
            ["id2"] = relation.Id2,
            ["relationTitle1"] = relation.RelationTitle1,
            ["relationTitle2"] = relation.RelationTitle2,
            ["relation"] = new JsonObject
            {
                ["type1"] = TypeCode(relation.Relation.Type1),
                ["type2"] = TypeCode(relation.Relation.Type2),
                ["lineType"] = (double)relation.Relation.LineType,
            },
        };
        Optional(result, "title", relation.Title);
        return result;
    }

    /// <summary>Writes a note.</summary>
    /// <param name="note">The note.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Note(ClassNote note)
    {
        var result = new JsonObject { ["id"] = note.Id, ["text"] = note.Text, ["index"] = (double)note.Index };
        Optional(result, "class", note.Class);
        Optional(result, "parent", note.Parent);
        return result;
    }

    /// <summary>Writes a namespace.</summary>
    /// <param name="space">The namespace.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Namespace(ClassNamespace space)
    {
        var result = new JsonObject
        {
            ["id"] = space.Id,
            ["label"] = space.Label,
            ["domId"] = space.DomId,
            ["explicit"] = space.Explicit,
            ["classes"] = Strings(space.Classes),
            ["notes"] = Strings(space.Notes),
            ["children"] = Strings(space.Children),
        };
        Optional(result, "parent", space.Parent);
        return result;
    }

    /// <summary>Writes a node of the layout data.</summary>
    /// <param name="model">The model, for the link data and display texts that the class box does not carry.</param>
    /// <param name="node">The node.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Node(ClassModel model, LayoutNode node)
    {
        var result = new JsonObject
        {
            ["id"] = node.Id,
            ["label"] = node.Label,
            ["shape"] = node.Shape,
            ["isGroup"] = node.IsGroup,
            ["look"] = node.Look,
        };
        Optional(result, "parentId", node.ParentId);
        Optional(result, "labelType", node.LabelType);
        if (node.CssStyles is not null)
        {
            result["cssStyles"] = Strings(node.CssStyles);
        }

        if (node.ClassBox is not { } box)
        {
            // Namespaces and notes have a padding; the interface nodes have none
            if (node.IsGroup || node.Shape == "note")
            {
                result["padding"] = node.Padding;
            }

            return result;
        }

        // A class: its own fields, with the members as their display details
        var source = model.GetClass(node.Id)!;
        result["text"] = box.Text;
        result["cssClasses"] = node.CssClasses;
        result["styles"] = Strings(box.Styles);
        result["domId"] = node.DomId;
        result["annotations"] = Strings(box.Annotations);
        result["members"] = Array(source.Members.Select((m, i) => Display(m, box.Members[i])));
        result["methods"] = Array(source.Methods.Select((m, i) => Display(m, box.Methods[i])));
        AddLinkData(result, source);
        return result;
    }

    /// <summary>Writes the display details of a member, with the style the class box carries.</summary>
    /// <param name="member">The member.</param>
    /// <param name="drawn">The member of the class box.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Display(ClassMember member, LayoutClassMember drawn)
    {
        Assert.Equal(member.Text, drawn.Text);
        return new JsonObject { ["displayText"] = member.DisplayText, ["cssStyle"] = drawn.CssStyle };
    }

    /// <summary>Writes the edge of a note.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject NoteEdge(LayoutEdge edge)
    {
        Assert.Equal(1, edge.MinLen);
        return new JsonObject
        {
            ["id"] = edge.Id,
            ["start"] = edge.Start,
            ["end"] = edge.End,
            ["type"] = edge.Type,
            ["thickness"] = edge.Thickness,
            ["classes"] = edge.Classes,
            ["arrowTypeStart"] = edge.ArrowTypeStart,
            ["arrowTypeEnd"] = edge.ArrowTypeEnd,
            ["arrowheadStyle"] = edge.ArrowheadStyle,
            ["labelStyle"] = Strings(edge.LabelStyle),
            ["style"] = Strings(edge.Style),
            ["pattern"] = edge.Pattern,
            ["look"] = edge.Look,
        };
    }

    /// <summary>Writes the edge of a relation.</summary>
    /// <param name="edge">The edge.</param>
    /// <param name="relation">The relation, which tells whether the label is defined.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject RelationEdge(LayoutEdge edge, ClassRelation relation)
    {
        Assert.Equal(1, edge.MinLen);
        Assert.Empty(edge.Style);
        var result = new JsonObject
        {
            ["id"] = edge.Id,
            ["start"] = edge.Start,
            ["end"] = edge.End,
            ["type"] = edge.Type,
            ["labelType"] = edge.LabelType,
            ["labelpos"] = edge.LabelPos,
            ["thickness"] = edge.Thickness,
            ["classes"] = edge.Classes,
            ["arrowTypeStart"] = edge.ArrowTypeStart,
            ["arrowTypeEnd"] = edge.ArrowTypeEnd,
            ["startLabelRight"] = edge.StartLabelRight,
            ["endLabelLeft"] = edge.EndLabelLeft,
            ["arrowheadStyle"] = edge.ArrowheadStyle,
            ["labelStyle"] = Strings(edge.LabelStyle),
            ["style"] = string.Empty,
            ["pattern"] = edge.Pattern,
            ["look"] = edge.Look,
        };
        if (relation.Title is not null)
        {
            result["label"] = edge.Label;
        }

        return result;
    }

    /// <summary>Adds the link data of a class that has it.</summary>
    /// <param name="result">The JSON object.</param>
    /// <param name="node">The class.</param>
    private static void AddLinkData(JsonObject result, ClassNode node)
    {
        Optional(result, "link", node.Link);
        Optional(result, "linkTarget", node.LinkTarget);
        Optional(result, "tooltip", node.Tooltip);
        if (node.HaveCallback is { } callback)
        {
            result["haveCallback"] = callback;
        }
    }

    /// <summary>Writes a relation code: the number, or <c>none</c>.</summary>
    /// <param name="code">The code, or null.</param>
    /// <returns>The JSON value.</returns>
    private static JsonNode TypeCode(int? code) => code is { } value ? JsonValue.Create((double)value) : JsonValue.Create("none");

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
