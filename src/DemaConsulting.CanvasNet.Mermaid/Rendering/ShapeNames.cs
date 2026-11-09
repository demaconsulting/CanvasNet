// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     The names of Mermaid's node shapes: the 53 documented shapes with their short names, aliases and internal aliases,
///     and the undocumented shapes the diagrams use, each mapped to the shape (its drawing function) it stands for.
/// </summary>
/// <remarks>
///     <para>
///         This is Mermaid's <c>shapes</c> registry (<c>rendering-elements/shapes.ts</c>): <see cref="IsKnown"/> is its
///         <c>isValidShape</c> and <see cref="HandlerOf"/> names the drawing function, which the shape library implements.
///         The flowchart accepts in <c>@{ shape: ... }</c> only names that are lower case and contain no underscore,
///         as Mermaid's <c>flowDb</c> checks; <see cref="Nearest"/> suggests a name for a typo.
///     </para>
///     <para>
///         The class is immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal static class ShapeNames
{
    /// <summary>
    ///     The documented shapes in Mermaid's order: the drawing function, the short name, then the aliases and internal
    ///     aliases.
    /// </summary>
    private static readonly (string Handler, string[] Names)[] Documented =
    [
        ("squareRect", ["rect", "proc", "process", "rectangle", "squareRect"]),
        ("roundedRect", ["rounded", "event", "roundedRect"]),
        ("stadium", ["stadium", "terminal", "pill"]),
        ("subroutine", ["fr-rect", "subprocess", "subproc", "framed-rectangle", "subroutine"]),
        ("cylinder", ["cyl", "db", "database", "cylinder"]),
        ("datastore", ["datastore", "data-store"]),
        ("folder", ["folder", "directory"]),
        ("bucket", ["bucket"]),
        ("consoleWindow", ["console"]),
        ("browser", ["browser"]),
        ("person", ["person"]),
        ("circle", ["circle", "circ"]),
        ("bang", ["bang"]),
        ("cloud", ["cloud"]),
        ("question", ["diam", "decision", "diamond", "question"]),
        ("hexagon", ["hex", "hexagon", "prepare"]),
        ("lean_right", ["lean-r", "lean-right", "in-out", "lean_right"]),
        ("lean_left", ["lean-l", "lean-left", "out-in", "lean_left"]),
        ("trapezoid", ["trap-b", "priority", "trapezoid-bottom", "trapezoid"]),
        ("inv_trapezoid", ["trap-t", "manual", "trapezoid-top", "inv-trapezoid", "inv_trapezoid"]),
        ("doublecircle", ["dbl-circ", "double-circle", "doublecircle"]),
        ("text", ["text"]),
        ("card", ["notch-rect", "card", "notched-rectangle"]),
        ("shadedProcess", ["lin-rect", "lined-rectangle", "lined-process", "lin-proc", "shaded-process"]),
        ("stateStart", ["sm-circ", "start", "small-circle", "stateStart"]),
        ("stateEnd", ["fr-circ", "stop", "framed-circle", "stateEnd"]),
        ("forkJoin", ["fork", "join", "forkJoin"]),
        ("hourglass", ["hourglass", "collate"]),
        ("curlyBraceLeft", ["brace", "comment", "brace-l"]),
        ("curlyBraceRight", ["brace-r"]),
        ("curlyBraces", ["braces"]),
        ("lightningBolt", ["bolt", "com-link", "lightning-bolt"]),
        ("waveEdgedRectangle", ["doc", "document"]),
        ("halfRoundedRectangle", ["delay", "half-rounded-rectangle"]),
        ("tiltedCylinder", ["h-cyl", "das", "horizontal-cylinder"]),
        ("linedCylinder", ["lin-cyl", "disk", "lined-cylinder"]),
        ("curvedTrapezoid", ["curv-trap", "curved-trapezoid", "display"]),
        ("dividedRectangle", ["div-rect", "div-proc", "divided-rectangle", "divided-process"]),
        ("triangle", ["tri", "extract", "triangle"]),
        ("windowPane", ["win-pane", "internal-storage", "window-pane"]),
        ("filledCircle", ["f-circ", "junction", "filled-circle"]),
        ("trapezoidalPentagon", ["notch-pent", "loop-limit", "notched-pentagon"]),
        ("flippedTriangle", ["flip-tri", "manual-file", "flipped-triangle"]),
        ("slopedRect", ["sl-rect", "manual-input", "sloped-rectangle"]),
        ("multiWaveEdgedRectangle", ["docs", "documents", "st-doc", "stacked-document"]),
        ("multiRect", ["st-rect", "procs", "processes", "stacked-rectangle"]),
        ("bowTieRect", ["bow-rect", "stored-data", "bow-tie-rectangle"]),
        ("crossedCircle", ["cross-circ", "summary", "crossed-circle"]),
        ("taggedWaveEdgedRectangle", ["tag-doc", "tagged-document"]),
        ("taggedRect", ["tag-rect", "tagged-rectangle", "tag-proc", "tagged-process"]),
        ("waveRectangle", ["flag", "paper-tape"]),
        ("rect_left_inv_arrow", ["odd", "rect_left_inv_arrow"]),
        ("linedWaveEdgedRect", ["lin-doc", "lined-document"]),
    ];

    /// <summary>The undocumented shapes, whose name is their drawing function.</summary>
    private static readonly string[] Undocumented =
    [
        "state", "choice", "note", "composite", "rectWithTitle", "labelRect", "block_arrow", "collapsedGroup",
        "iconSquare", "iconCircle", "icon", "iconRounded", "imageSquare", "anchor", "kanbanItem", "mindmapCircle",
        "defaultMindmapNode", "classBox", "erBox", "requirementBox",
    ];

    /// <summary>Every shape name and the drawing function it stands for.</summary>
    private static readonly FrozenDictionary<string, string> Handlers = BuildHandlers();

    /// <summary>
    ///     Gets the number of documented shapes (53 in the Mermaid version this package follows).
    /// </summary>
    public static int DocumentedCount => Documented.Length;

    /// <summary>
    ///     Gets the short names of the documented shapes, in Mermaid's order.
    /// </summary>
    public static IReadOnlyList<string> ShortNames { get; } = [.. Documented.Select(d => d.Names[0])];

    /// <summary>
    ///     Gets a value indicating whether a name is a shape of Mermaid's registry (its <c>isValidShape</c>).
    /// </summary>
    /// <param name="name">The name, case-sensitive.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsKnown(string name) => Handlers.ContainsKey(name);

    /// <summary>
    ///     Gets the drawing function a shape name stands for.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The function name (for example <c>squareRect</c> for <c>rect</c> and <c>proc</c>), or null for an unknown name.</returns>
    public static string? HandlerOf(string name) => Handlers.TryGetValue(name, out var handler) ? handler : null;

    /// <summary>
    ///     Finds the shape name a user most likely meant: the nearest name that the flowchart accepts (lower case, no
    ///     underscore) by edit distance, ignoring case.
    /// </summary>
    /// <param name="name">The name as written.</param>
    /// <returns>The nearest accepted name.</returns>
    public static string Nearest(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var probe = name.ToLowerInvariant().Replace('_', '-');
        return Handlers.Keys
            .Where(key => key == key.ToLowerInvariant() && !key.Contains('_', StringComparison.Ordinal))
            .OrderBy(key => DiagramDetector.EditDistance(probe, key))
            .ThenBy(key => key.Length)
            .ThenBy(key => key, StringComparer.Ordinal)
            .First();
    }

    /// <summary>
    ///     Builds the name table: the undocumented shapes first, then every name of the documented shapes.
    /// </summary>
    /// <returns>The table.</returns>
    private static FrozenDictionary<string, string> BuildHandlers()
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in Undocumented)
        {
            table[name] = name;
        }

        foreach (var (handler, names) in Documented)
        {
            foreach (var name in names)
            {
                table[name] = handler;
            }
        }

        return table.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
