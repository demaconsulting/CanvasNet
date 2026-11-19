// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Styling;

/// <summary>
///     One skin parameter of a diagram: its full name (a block's name and the inner name joined), its value, and its
///     location.
/// </summary>
/// <param name="Name">The full parameter name as written (for example <c>participantBackgroundColor</c>).</param>
/// <param name="Value">The value text, trimmed.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SkinParamEntry(string Name, string Value, PlantUmlLocation Location);

/// <summary>
///     Reads the <c>skinparam</c> statements of a diagram and applies the supported subset to a <see cref="SkinTheme"/>.
/// </summary>
/// <remarks>
///     <para>
///         A skin parameter is written on one line (<c>skinparam participantBackgroundColor #EEE</c>) or as a block whose
///         inner names are joined to the block's name (<c>skinparam participant {</c> ... <c>BackgroundColor #EEE</c>
///         ... <c>}</c>). Names are not case-sensitive. The supported subset is the document settings
///         (<c>backgroundColor</c>, <c>defaultFontName</c>, <c>defaultFontSize</c>, <c>defaultFontColor</c>,
///         <c>roundCorner</c>, <c>shadowing</c>), the sequence settings (<c>sequenceArrowThickness</c>,
///         <c>sequenceMessageAlign</c>, <c>responseMessageBelowArrow</c>, <c>maxMessageSize</c>, <c>footbox</c>), and the
///         colors, fonts, and line widths of the elements: the eight participant kinds, <c>arrow</c>, <c>lifeLine</c>,
///         <c>note</c>, <c>sequenceGroup</c>, <c>sequenceBox</c>, <c>sequenceDivider</c>, <c>activity</c>,
///         <c>activityDiamond</c>, <c>swimlane</c>, <c>partition</c>, <c>title</c>, <c>header</c>, <c>footer</c>,
///         <c>legend</c>, and <c>caption</c>. The diagrams PlantUML lays out with Graphviz (class and object diagrams)
///         also read <c>class</c>, <c>classStereotype</c>, <c>classAttribute</c>, <c>object</c>, <c>objectStereotype</c>, <c>map</c>, and
///         <c>package</c>, apply <c>arrow</c> to their links, and read <c>classAttributeIconSize</c> and
///         <c>circledCharacterRadius</c>. Every other parameter, a stereotyped parameter, and a value that cannot be
///         read is reported as the warning <see cref="PlantUmlDiagnosticCodes.SkinParamIgnored"/> and has no effect. A
///         <c>&lt;style&gt;</c> block is reported as <see cref="PlantUmlDiagnosticCodes.StyleUnsupported"/> and skipped.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class SkinParams
{
    /// <summary>The largest font size that is accepted, in pixels.</summary>
    private const double MaxFontSize = 500;

    /// <summary>The largest line width that is accepted, in pixels.</summary>
    private const double MaxLineWidth = 100;

    /// <summary>The largest wrapping width that is accepted, in pixels.</summary>
    private const double MaxWrapWidth = 100_000;

    /// <summary>The element prefixes of the parameter names, with the elements each one styles.</summary>
    private static readonly (string Prefix, SkinElement[] Elements)[] Prefixes =
    [
        ("participant", [SkinElement.Participant]),
        ("actor", [SkinElement.Actor]),
        ("boundary", [SkinElement.Boundary]),
        ("control", [SkinElement.Control]),
        ("entity", [SkinElement.Entity]),
        ("database", [SkinElement.Database]),
        ("collections", [SkinElement.Collections]),
        ("queue", [SkinElement.Queue]),
        ("note", [SkinElement.Note]),
        ("sequenceGroupHeader", [SkinElement.GroupHeader]),
        ("sequenceGroup", [SkinElement.Group, SkinElement.GroupHeader]),
        ("sequenceBox", [SkinElement.Box]),
        ("sequenceDivider", [SkinElement.Separator]),
        ("sequenceDelay", [SkinElement.Delay]),
        ("sequenceLifeLine", [SkinElement.LifeLine]),
        ("lifeLine", [SkinElement.LifeLine]),
        ("arrow", [SkinElement.Arrow, SkinElement.ActivityArrow]),
        ("activityDiamond", [SkinElement.ActivityDiamond]),
        ("activity", [SkinElement.Activity]),
        ("swimlaneTitle", [SkinElement.Swimlane]),
        ("swimlane", [SkinElement.Swimlane]),
        ("partition", [SkinElement.Partition]),
        ("title", [SkinElement.Title]),
        ("header", [SkinElement.Header]),
        ("footer", [SkinElement.Footer]),
        ("legend", [SkinElement.Legend]),
        ("caption", [SkinElement.Caption]),
    ];

    /// <summary>The elements whose text inherits the document's default font size.</summary>
    private static readonly SkinElement[] RootSizedElements =
    [
        SkinElement.Participant, SkinElement.Actor, SkinElement.Boundary, SkinElement.Control, SkinElement.Entity,
        SkinElement.Database, SkinElement.Collections, SkinElement.Queue, SkinElement.Legend, SkinElement.Caption,
        SkinElement.Class, SkinElement.ClassAttribute, SkinElement.Object, SkinElement.Package, SkinElement.Container,
    ];

    /// <summary>
    ///     The element prefixes that only the diagrams PlantUML lays out with Graphviz read (class and object diagrams),
    ///     with the elements each one styles; <c>arrow</c> also styles their links.
    /// </summary>
    private static readonly (string Prefix, SkinElement[] Elements)[] GraphPrefixes =
    [
        ("classStereotype", [SkinElement.ClassStereotype]),
        ("classAttribute", [SkinElement.ClassAttribute]),
        ("class", [SkinElement.Class]),
        ("objectStereotype", [SkinElement.ClassStereotype]),
        ("object", [SkinElement.Object]),
        ("map", [SkinElement.Object]),
        ("package", [SkinElement.Package]),
        ("arrow", [SkinElement.Arrow, SkinElement.ActivityArrow, SkinElement.SvekArrow]),
    ];

    /// <summary>
    ///     Reads a <c>skinparam</c> statement (one line, or a block up to its closing brace).
    /// </summary>
    /// <param name="statements">The statements of the diagram.</param>
    /// <param name="index">The index of the statement; advanced to the last statement read.</param>
    /// <param name="entries">The list that receives the parameters.</param>
    /// <returns>True when the statement is a <c>skinparam</c> statement.</returns>
    public static bool TryRead(IReadOnlyList<SourceStatement> statements, ref int index, List<SkinParamEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(entries);
        var statement = statements[index];
        var text = statement.Text;
        if (!IsKeyword(text, "skinparam"))
        {
            return false;
        }

        // skinparam name value, or skinparam name { ... }
        var rest = text["skinparam".Length..].Trim();
        if (rest.EndsWith('{'))
        {
            var prefix = rest[..^1].Trim();
            while (++index < statements.Count && statements[index].Text != "}")
            {
                var inner = statements[index];
                if (inner.Text.Length > 0)
                {
                    var (name, value) = Split(inner.Text);
                    entries.Add(new SkinParamEntry(prefix + name, value, inner.Location));
                }
            }

            return true;
        }

        var (single, singleValue) = Split(rest);
        entries.Add(new SkinParamEntry(single, singleValue, statement.Location));
        return true;
    }

    /// <summary>
    ///     Skips a <c>&lt;style&gt;</c> block and reports it.
    /// </summary>
    /// <param name="statements">The statements of the diagram.</param>
    /// <param name="index">The index of the statement; advanced to the closing <c>&lt;/style&gt;</c>.</param>
    /// <param name="reports">The pending reports.</param>
    /// <returns>True when the statement opens a style block.</returns>
    public static bool TrySkipStyle(IReadOnlyList<SourceStatement> statements, ref int index, List<PlantUmlPendingReport> reports)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(reports);
        var statement = statements[index];
        if (!statement.Text.StartsWith("<style", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        reports.Add(PlantUmlPendingReport.Unsupported(
            PlantUmlDiagnosticCodes.StyleUnsupported,
            "<style> blocks are not supported; the default look is drawn",
            statement.Location,
            "set the look with 'skinparam' statements"));
        while (index < statements.Count && !statements[index].Text.Contains("</style>", StringComparison.OrdinalIgnoreCase))
        {
            index++;
        }

        return true;
    }

    /// <summary>
    ///     Applies skin parameters to a theme in order (a later value wins).
    /// </summary>
    /// <param name="theme">The theme to start from.</param>
    /// <param name="entries">The parameters.</param>
    /// <param name="reports">The pending reports, which receive a warning for every parameter that has no effect.</param>
    /// <param name="graph">
    ///     Whether the diagram is one that PlantUML lays out with Graphviz (a class or object diagram), which also reads
    ///     the <c>class</c>, <c>object</c>, <c>map</c>, and <c>package</c> prefixes and the settings
    ///     <c>classAttributeIconSize</c> and <c>circledCharacterRadius</c>; false by default.
    /// </param>
    /// <returns>The theme with the parameters applied.</returns>
    public static SkinTheme Apply(SkinTheme theme, IEnumerable<SkinParamEntry> entries, List<PlantUmlPendingReport> reports, bool graph = false)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(reports);
        foreach (var entry in entries)
        {
            var applied = TryApply(theme, entry, graph, out var reason);
            if (applied is null)
            {
                reports.Add(PlantUmlPendingReport.Warning(
                    PlantUmlDiagnosticCodes.SkinParamIgnored,
                    $"skinparam '{entry.Name}' is ignored: {reason}",
                    entry.Location));
                continue;
            }

            theme = applied;
        }

        return theme;
    }

    /// <summary>
    ///     The document and sequence settings.
    /// </summary>
    private enum Setting
    {
        /// <summary><c>backgroundColor</c>.</summary>
        BackgroundColor,

        /// <summary><c>defaultFontName</c>.</summary>
        DefaultFontName,

        /// <summary><c>defaultFontSize</c>.</summary>
        DefaultFontSize,

        /// <summary><c>defaultFontColor</c>.</summary>
        DefaultFontColor,

        /// <summary><c>roundCorner</c>.</summary>
        RoundCorner,

        /// <summary><c>shadowing</c>.</summary>
        Shadowing,

        /// <summary><c>sequenceArrowThickness</c> (also <c>arrowThickness</c>).</summary>
        ArrowThickness,

        /// <summary><c>sequenceMessageAlign</c>.</summary>
        MessageAlign,

        /// <summary><c>responseMessageBelowArrow</c>.</summary>
        ResponseBelowArrow,

        /// <summary><c>maxMessageSize</c>.</summary>
        MaxMessageSize,

        /// <summary><c>footbox</c>.</summary>
        Footbox,
    }

    /// <summary>
    ///     The properties of an element.
    /// </summary>
    private enum Property
    {
        /// <summary>The fill (<c>BackgroundColor</c>).</summary>
        Fill,

        /// <summary>The line (<c>BorderColor</c>, <c>Color</c>, <c>LineColor</c>).</summary>
        Line,

        /// <summary>The text color (<c>FontColor</c>).</summary>
        FontColor,

        /// <summary>The text size (<c>FontSize</c>).</summary>
        FontSize,

        /// <summary>The text family (<c>FontName</c>).</summary>
        FontName,

        /// <summary>The text style (<c>FontStyle</c>).</summary>
        FontStyle,

        /// <summary>The line width (<c>BorderThickness</c>, <c>Thickness</c>).</summary>
        LineWidth,
    }

    /// <summary>The setting names, without regard to case.</summary>
    private static readonly Dictionary<string, Setting> Settings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["backgroundColor"] = Setting.BackgroundColor,
        ["defaultFontName"] = Setting.DefaultFontName,
        ["defaultFontSize"] = Setting.DefaultFontSize,
        ["defaultFontColor"] = Setting.DefaultFontColor,
        ["roundCorner"] = Setting.RoundCorner,
        ["shadowing"] = Setting.Shadowing,
        ["sequenceArrowThickness"] = Setting.ArrowThickness,
        ["arrowThickness"] = Setting.ArrowThickness,
        ["sequenceMessageAlign"] = Setting.MessageAlign,
        ["responseMessageBelowArrow"] = Setting.ResponseBelowArrow,
        ["maxMessageSize"] = Setting.MaxMessageSize,
        ["footbox"] = Setting.Footbox,
    };

    /// <summary>The property names, without regard to case.</summary>
    private static readonly Dictionary<string, Property> Properties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BackgroundColor"] = Property.Fill,
        ["BorderColor"] = Property.Line,
        ["Color"] = Property.Line,
        ["LineColor"] = Property.Line,
        ["FontColor"] = Property.FontColor,
        ["FontSize"] = Property.FontSize,
        ["FontName"] = Property.FontName,
        ["FontStyle"] = Property.FontStyle,
        ["BorderThickness"] = Property.LineWidth,
        ["Thickness"] = Property.LineWidth,
    };

    /// <summary>The message alignments, in their canonical spelling.</summary>
    private static readonly string[] MessageAlignments = ["left", "center", "right", "direction", "reverseDirection"];

    /// <summary>
    ///     Applies one parameter.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="entry">The parameter.</param>
    /// <param name="graph">Whether the diagram also reads the graph prefixes and settings.</param>
    /// <param name="reason">Receives why the parameter has no effect.</param>
    /// <returns>The new theme, or null when the parameter has no effect.</returns>
    private static SkinTheme? TryApply(SkinTheme theme, SkinParamEntry entry, bool graph, out string reason)
    {
        reason = "it is not supported by this version";
        var name = entry.Name;
        var value = Unquote(entry.Value);
        if (name.Contains("<<", StringComparison.Ordinal))
        {
            reason = "stereotyped skin parameters are not supported";
            return null;
        }

        // The document and sequence settings
        if (Settings.TryGetValue(name, out var setting))
        {
            return ApplySetting(theme, setting, value, ref reason);
        }

        // The class settings of the graph diagrams
        if (graph && name.Equals("classAttributeIconSize", StringComparison.OrdinalIgnoreCase))
        {
            return Number(value, 0, 100, ref reason) is { } iconSize ? theme with { ClassAttributeIconSize = iconSize } : null;
        }

        if (graph && name.Equals("circledCharacterRadius", StringComparison.OrdinalIgnoreCase))
        {
            return Number(value, 1, 100, ref reason) is { } radius ? theme with { CircledCharacterRadius = radius } : null;
        }

        // An element property: the longest element prefix whose rest is a property name (a graph prefix replaces the
        // shared one of the same name)
        var prefixes = graph
            ? GraphPrefixes.Concat(Prefixes.Where(p => !GraphPrefixes.Any(g => g.Prefix == p.Prefix)))
            : Prefixes;
        foreach (var (prefix, elements) in prefixes.OrderByDescending(p => p.Prefix.Length))
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Properties.TryGetValue(name[prefix.Length..], out var property))
            {
                return ApplyProperty(theme, Targets(prefix, elements, property), property, value, ref reason);
            }
        }

        return null;
    }

    /// <summary>
    ///     Applies a document or sequence setting.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="setting">The setting.</param>
    /// <param name="value">The value.</param>
    /// <param name="reason">Receives why the value has no effect.</param>
    /// <returns>The new theme, or null.</returns>
    private static SkinTheme? ApplySetting(SkinTheme theme, Setting setting, string value, ref string reason)
    {
        switch (setting)
        {
            case Setting.BackgroundColor:
                return Color(value, ref reason) is { } background ? theme with { Background = Visible(background) } : null;
            case Setting.DefaultFontName:
                return AllElements(theme, s => s with { FontFamily = value });
            case Setting.DefaultFontSize:
                return Number(value, 1, MaxFontSize, ref reason) is { } size
                    ? RootSizedElements.Aggregate(theme, (t, e) => t.With(e, s => s with { FontSize = size }))
                    : null;
            case Setting.DefaultFontColor:
                return Color(value, ref reason) is { } fontColor ? AllElements(theme, s => s with { FontColor = fontColor }) : null;
            case Setting.RoundCorner:
                return Number(value, 0, MaxWrapWidth, ref reason) is { } corner
                    ? SkinTheme.ParticipantElements.Aggregate(theme, (t, e) => t.With(e, s => s with { RoundCorner = corner }))
                    : null;
            case Setting.Shadowing:
                return Flag(value, ref reason) is { } shadowing ? theme with { Shadowing = shadowing } : null;
            case Setting.ArrowThickness:
                return Number(value, 0, MaxLineWidth, ref reason) is { } thickness ? theme with { ArrowThickness = thickness } : null;
            case Setting.MessageAlign:
                var align = MessageAlignments.FirstOrDefault(a => a.Equals(value, StringComparison.OrdinalIgnoreCase));
                if (align is not null)
                {
                    return theme with { MessageAlign = align };
                }

                reason = $"'{value}' is not one of {string.Join(", ", MessageAlignments)}";
                return null;
            case Setting.ResponseBelowArrow:
                return Flag(value, ref reason) is { } below ? theme with { ResponseBelowArrow = below } : null;
            case Setting.MaxMessageSize:
                return Number(value, 1, MaxWrapWidth, ref reason) is { } wrap ? theme with { MaxMessageSize = wrap } : null;
            default:
                var footbox = value.ToLowerInvariant();
                if (footbox is "show" or "true" or "hide" or "false")
                {
                    return theme with { Footbox = footbox is "show" or "true" };
                }

                reason = $"'{value}' is not show or hide";
                return null;
        }
    }

    /// <summary>
    ///     Gets the elements a property of a prefix styles: a group's background is its header's, its font its
    ///     condition's, and a life line's background is its activation boxes'.
    /// </summary>
    /// <param name="prefix">The element prefix.</param>
    /// <param name="elements">The elements of the prefix.</param>
    /// <param name="property">The property.</param>
    /// <returns>The elements to change.</returns>
    private static SkinElement[] Targets(string prefix, SkinElement[] elements, Property property)
    {
        if (prefix == "sequenceGroup" && property == Property.Fill)
        {
            return [SkinElement.GroupHeader];
        }

        if (prefix == "sequenceGroup" && property is Property.FontSize or Property.FontColor or Property.FontName or Property.FontStyle)
        {
            return [SkinElement.Group];
        }

        return prefix is "lifeLine" or "sequenceLifeLine" && property == Property.Fill ? [SkinElement.ActivationBox] : elements;
    }

    /// <summary>
    ///     Applies a property to elements.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="elements">The elements.</param>
    /// <param name="property">The property.</param>
    /// <param name="value">The value.</param>
    /// <param name="reason">Receives why the value has no effect.</param>
    /// <returns>The new theme, or null.</returns>
    private static SkinTheme? ApplyProperty(SkinTheme theme, SkinElement[] elements, Property property, string value, ref string reason)
    {
        Func<ElementStyle, ElementStyle>? change = property switch
        {
            Property.Fill => Color(value, ref reason) is { } fill ? s => s with { Fill = Visible(fill) } : null,
            Property.Line => Color(value, ref reason) is { } line ? s => s with { Line = Visible(line) } : null,
            Property.FontColor => Color(value, ref reason) is { } font ? s => s with { FontColor = font } : null,
            Property.FontSize => Number(value, 1, MaxFontSize, ref reason) is { } size ? s => s with { FontSize = size } : null,
            Property.FontName => s => s with { FontFamily = value },
            Property.FontStyle => FontStyle(value, ref reason) is { } style ? s => s with { Bold = style.Bold, Italic = style.Italic } : null,
            _ => Number(value, 0, MaxLineWidth, ref reason) is { } width ? s => s with { LineWidth = width } : null,
        };

        return change is null ? null : elements.Aggregate(theme, (t, e) => t.With(e, change));
    }
    /// <summary>
    ///     Changes the style of every element.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="change">The change.</param>
    /// <returns>The new theme.</returns>
    private static SkinTheme AllElements(SkinTheme theme, Func<ElementStyle, ElementStyle> change) =>
        Enum.GetValues<SkinElement>().Aggregate(theme, (t, e) => t.With(e, change));

    /// <summary>
    ///     Maps a fully transparent color to no paint.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The color, or null when it is fully transparent.</returns>
    private static Rgba32? Visible(Rgba32 color) => color.A == 0 ? null : color;

    /// <summary>
    ///     Reads a color value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="reason">Receives why it cannot be read.</param>
    /// <returns>The color, or null.</returns>
    private static Rgba32? Color(string value, ref string reason)
    {
        if (TryParseColor(value, out var color))
        {
            return color;
        }

        reason = $"'{value}' is not a color";
        return null;
    }

    /// <summary>
    ///     Reads a number within a range.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="minimum">The smallest accepted value.</param>
    /// <param name="maximum">The largest accepted value.</param>
    /// <param name="reason">Receives why it cannot be read.</param>
    /// <returns>The number, or null.</returns>
    private static double? Number(string value, double minimum, double maximum, ref string reason)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
            double.IsFinite(number) && number >= minimum && number <= maximum)
        {
            return number;
        }

        reason = string.Create(CultureInfo.InvariantCulture, $"'{value}' is not a number from {minimum} to {maximum}");
        return null;
    }

    /// <summary>
    ///     Reads a boolean value (<c>true</c>, <c>false</c>, <c>on</c>, <c>off</c>, <c>yes</c>, <c>no</c>).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="reason">Receives why it cannot be read.</param>
    /// <returns>The flag, or null.</returns>
    private static bool? Flag(string value, ref string reason)
    {
        switch (value.ToLowerInvariant())
        {
            case "true" or "on" or "yes":
                return true;
            case "false" or "off" or "no":
                return false;
            default:
                reason = $"'{value}' is not true or false";
                return null;
        }
    }

    /// <summary>
    ///     Reads a font style (<c>plain</c>, <c>bold</c>, <c>italic</c>, or both).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="reason">Receives why it cannot be read.</param>
    /// <returns>The style, or null.</returns>
    private static (bool Bold, bool Italic)? FontStyle(string value, ref string reason)
    {
        var words = value.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0 && words.All(w => w is "plain" or "normal" or "bold" or "italic"))
        {
            return (words.Contains("bold"), words.Contains("italic"));
        }

        reason = $"'{value}' is not plain, bold or italic";
        return null;
    }

    /// <summary>
    ///     Splits a parameter text into its name and value.
    /// </summary>
    /// <param name="text">The text after <c>skinparam</c> (or a line of a block).</param>
    /// <returns>The name and the trimmed value.</returns>
    private static (string Name, string Value) Split(string text)
    {
        var end = 0;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        return (text[..end], text[end..].Trim());
    }

    /// <summary>
    ///     Removes the double quotes around a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value without its quotes.</returns>
    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    /// <summary>
    ///     Checks whether a statement starts with a keyword followed by white space or its end.
    /// </summary>
    /// <param name="text">The statement text.</param>
    /// <param name="keyword">The keyword.</param>
    /// <returns>True when it does.</returns>
    internal static bool IsKeyword(string text, string keyword) =>
        text.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) &&
        (text.Length == keyword.Length || char.IsWhiteSpace(text[keyword.Length]));
}
