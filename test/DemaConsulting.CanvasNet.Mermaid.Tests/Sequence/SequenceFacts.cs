using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Sequence;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Sequence;

/// <summary>
///     Test support of the Sequence subsystem: parses a text as the document will, and writes a parsed model as the facts
///     the vector generator records from Mermaid's sequence database (the participants, boxes, messages and created
///     and destroyed participants, with the keys and the <c>undefined</c> omissions of <c>JSON.stringify</c>).
/// </summary>
internal static class SequenceFacts
{
    /// <summary>
    ///     Parses a sequence text: preprocessing, detection, then the sequence parser.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="sourceName">The source name, or null.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The model.</returns>
    public static SequenceModel Parse(string text, string? sourceName = null, MermaidLimits? limits = null)
    {
        var effective = limits ?? MermaidLimits.Default;
        var source = Preprocessor.Run(text, sourceName, effective);
        var detected = DiagramDetector.Detect(source.Code, DetectorSettings.Default);
        return SequenceParser.Parse(source, detected, effective);
    }

    /// <summary>
    ///     Writes the facts of a model.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="config">The sequence configuration, which resolves the wrap settings.</param>
    /// <returns>The facts.</returns>
    public static JsonObject Of(SequenceModel model, SequenceConfig config)
    {
        var boxes = model.Boxes.ToList();
        return new JsonObject
        {
            ["type"] = "sequence",
            ["title"] = model.DiagramTitle,
            ["accTitle"] = model.AccessibleTitle,
            ["accDescr"] = model.AccessibleDescription,
            ["sequenceNumbers"] = false,
            ["actors"] = new JsonArray([.. model.Actors.Select(actor => Actor(actor, boxes, config))]),
            ["boxes"] = new JsonArray([.. boxes.Select(box => Box(box, config))]),
            ["messages"] = new JsonArray([.. model.Messages.Select(message => Message(message, config))]),
            ["createdActors"] = Indexes(model.CreatedActors),
            ["destroyedActors"] = Indexes(model.DestroyedActors),
        };
    }

    /// <summary>Writes a participant.</summary>
    /// <param name="actor">The participant.</param>
    /// <param name="boxes">The boxes, for the box index.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Actor(SequenceActor actor, List<SequenceBox> boxes, SequenceConfig config)
    {
        var result = new JsonObject
        {
            ["id"] = actor.Id,
            ["name"] = actor.Name,
            ["description"] = actor.Description,
            ["type"] = actor.Type,
            ["wrap"] = SequenceModel.EffectiveWrap(actor.Wrap, config),
            ["links"] = Properties(actor.Links),
            ["properties"] = Properties(actor.Properties),
            ["box"] = actor.Box is null ? null : (double)boxes.IndexOf(actor.Box),
        };
        Optional(result, "prevActor", actor.PrevActor);
        Optional(result, "nextActor", actor.NextActor);
        return result;
    }

    /// <summary>Writes a box.</summary>
    /// <param name="box">The box.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Box(SequenceBox box, SequenceConfig config)
    {
        var result = new JsonObject
        {
            ["fill"] = box.Fill,
            ["wrap"] = SequenceModel.EffectiveWrap(box.Wrap, config),
            ["actorKeys"] = new JsonArray([.. box.ActorKeys.Select(key => (JsonNode?)JsonValue.Create(key))]),
        };
        Optional(result, "name", box.Name);
        return result;
    }

    /// <summary>Writes a message.</summary>
    /// <param name="message">The message.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Message(SequenceMessage message, SequenceConfig config)
    {
        var result = new JsonObject
        {
            ["id"] = message.Id,
            ["wrap"] = SequenceModel.EffectiveWrap(message.Wrap, config),
            ["type"] = (double)message.Type,
        };
        Optional(result, "from", message.From);
        Optional(result, "to", message.To);
        if (message.Autonumber is { } numbering)
        {
            var value = new JsonObject();
            if (numbering.Start is { } start)
            {
                value["start"] = start;
            }

            if (numbering.Step is { } step)
            {
                value["step"] = step;
            }

            value["visible"] = numbering.Visible;
            result["message"] = value;
        }
        else
        {
            result["message"] = message.Text;
        }

        if (message.Activate is { } activate)
        {
            result["activate"] = activate;
        }

        if (message.CentralConnection is { } central)
        {
            result["centralConnection"] = (double)central;
        }

        if (message.Placement is { } placement)
        {
            result["placement"] = (double)placement;
        }

        return result;
    }

    /// <summary>Writes a property map as a JSON object.</summary>
    /// <param name="map">The map.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Properties(SequencePropertyMap map)
    {
        var result = new JsonObject();
        foreach (var key in map.Keys)
        {
            map.TryGetValue(key, out var value);
            result[key] = value?.DeepClone();
        }

        return result;
    }

    /// <summary>Writes the message indexes of created or destroyed participants (numbers are doubles, as in JavaScript).</summary>
    /// <param name="indexes">The indexes by participant.</param>
    /// <returns>The JSON object.</returns>
    private static JsonObject Indexes(IReadOnlyDictionary<string, int> indexes)
    {
        var result = new JsonObject();
        foreach (var (key, value) in indexes)
        {
            result[key] = (double)value;
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
}
