using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio Rgba davehoward IX Themed

/// <summary>
///     A single, as-parsed (not yet style-resolved) marker-delimited segment of a shape's
///     <c>&lt;Text&gt;</c> element: the literal text between one <c>&lt;cp&gt;</c>/<c>&lt;pp&gt;</c>
///     marker pair and the next (or the end of the element) - see <c>VsdxDocument.Text.cs</c>'s
///     <c>BuildRawRuns</c> for the parsing algorithm.
/// </summary>
/// <param name="Text">
///     The run's own literal text, exactly as it appears between markers (including any literal
///     whitespace/newline, per the element's <c>xml:space="preserve"</c> convention).
/// </param>
/// <param name="CharacterRowIndex">
///     The <c>Section N="Character"</c> row index (<c>&lt;cp IX="k"/&gt;</c>) in effect for this
///     run, or <see langword="null"/> when no <c>&lt;cp&gt;</c> marker precedes it anywhere in the
///     element (resolved through row <c>0</c>'s own StyleSheet-chain default - see
///     <c>VsdxDocument.TextStyle.cs</c>).
/// </param>
/// <param name="ParagraphRowIndex">
///     The <c>Section N="Paragraph"</c> row index (<c>&lt;pp IX="k"/&gt;</c>) in effect for this
///     run, or <see langword="null"/> for the same reason as <see cref="CharacterRowIndex"/>.
/// </param>
internal sealed record VsdxRawTextRun(string Text, int? CharacterRowIndex, int? ParagraphRowIndex);

/// <summary>
///     A shape's raw, as-parsed <c>&lt;Text&gt;</c> element: its ordered marker-delimited runs,
///     before any StyleSheet/character/paragraph-row resolution.
/// </summary>
/// <param name="Runs">The element's parsed runs, in document order. Empty when the shape has no <c>&lt;Text&gt;</c> element, or the element is present but empty.</param>
/// <param name="HasElement">
///     Whether the shape declared an own direct <c>&lt;Text&gt;</c> element at all (regardless of
///     whether that element carried any runs) - distinct from <paramref name="Runs"/> being empty,
///     which is also true for a genuinely absent <c>&lt;Text&gt;</c> element. PR #42 review round
///     2 (Finding #2): an instance shape's explicit, empty <c>&lt;Text/&gt;</c> element
///     (intentionally suppressing its own inherited text) must not be treated the same as a
///     genuinely absent <c>&lt;Text&gt;</c> element, which falls back to the Master's own text -
///     see <c>VsdxDocument.Groups.cs</c>'s <c>ResolveShapeRecursive</c>, which consults this flag
///     rather than <paramref name="Runs"/>'s own count.
/// </param>
internal sealed record VsdxRawText(IReadOnlyList<VsdxRawTextRun> Runs, bool HasElement)
{
    /// <summary>The shared, immutable empty instance, used for a shape with no <c>&lt;Text&gt;</c> element at all.</summary>
    public static readonly VsdxRawText Empty = new([], HasElement: false);
}

/// <summary>
///     The discriminator for a paragraph's resolved <c>HorzAlign</c> cell (format reference §8.2):
///     only <c>0</c> (left) and <c>1</c> (center) are evidenced anywhere in the corpus - every
///     other value (including the documented-but-unobserved <c>2</c>/<c>3</c>) degrades to
///     <see cref="Left"/> rather than throwing, mirroring <c>PptxEffectiveParagraphProperties</c>'s
///     own <c>"just"</c> -&gt; <c>"l"</c> simplification precedent.
/// </summary>
internal enum VsdxHorizontalAlign
{
    /// <summary>Left-aligned (<c>HorzAlign="0"</c>, or any unrecognized/absent value).</summary>
    Left,

    /// <summary>Center-aligned (<c>HorzAlign="1"</c>).</summary>
    Center,
}

/// <summary>
///     A paragraph's fully resolved, effective <c>Section N="Paragraph"</c> properties, produced
///     by <c>VsdxDocument.TextStyle.cs</c>'s <c>ResolveEffectiveRun</c>.
/// </summary>
/// <param name="Align">The resolved horizontal alignment.</param>
/// <param name="SpLine">
///     The resolved line spacing: a negative value is a "multiple of single-spacing" encoding
///     (for example <c>-1.2</c> = 120% of a single line's natural height); a non-negative value
///     is an absolute height, in inches (format reference §8.2). <c>0</c> means "unresolved
///     anywhere in the chain", treated by the layout engine as "use natural single-spacing".
/// </param>
/// <param name="SpBefore">The resolved paragraph spacing-before, in inches.</param>
/// <param name="SpAfter">The resolved paragraph spacing-after, in inches.</param>
/// <param name="IndFirst">The resolved first-line indent, in inches.</param>
/// <param name="IndLeft">The resolved left indent, in inches.</param>
/// <param name="IndRight">The resolved right indent, in inches.</param>
internal sealed record VsdxEffectiveParagraphProperties(
    VsdxHorizontalAlign Align,
    double SpLine,
    double SpBefore,
    double SpAfter,
    double IndFirst,
    double IndLeft,
    double IndRight);

/// <summary>
///     The discriminator for a shape's resolved <c>VerticalAlign</c> cell: <c>0</c>=top,
///     <c>1</c>=middle (Visio's own documented default), <c>2</c>=bottom - any other/absent value
///     degrades to <see cref="Middle"/>, never throwing.
/// </summary>
internal enum VsdxVerticalAlign
{
    /// <summary>Top-anchored (<c>VerticalAlign="0"</c>).</summary>
    Top,

    /// <summary>Middle-anchored (<c>VerticalAlign="1"</c>, or any unrecognized/absent value).</summary>
    Middle,

    /// <summary>Bottom-anchored (<c>VerticalAlign="2"</c>).</summary>
    Bottom,
}

/// <summary>
///     A shape's resolved, flat (non-row-indexed) text-box style cells - <c>VerticalAlign</c> and
///     the four margin cells - resolved via the shape's own literal cell, else the
///     <c>TextStyle</c> StyleSheet chain (format reference §8.2/Evidence #7; mirrors
///     <c>ResolveLineCellValue</c>/<c>ResolveFillCellValue</c>'s own two-tier resolution pattern).
/// </summary>
/// <param name="VerticalAlign">The resolved vertical text anchor.</param>
/// <param name="LeftMargin">The resolved left inset, in inches.</param>
/// <param name="RightMargin">The resolved right inset, in inches.</param>
/// <param name="TopMargin">The resolved top inset, in inches.</param>
/// <param name="BottomMargin">The resolved bottom inset, in inches.</param>
internal sealed record VsdxEffectiveTextBoxStyle(
    VsdxVerticalAlign VerticalAlign,
    double LeftMargin,
    double RightMargin,
    double TopMargin,
    double BottomMargin);

/// <summary>
///     A run's fully resolved, effective <c>Section N="Character"</c> properties plus its owning
///     paragraph's effective properties, produced by <c>VsdxDocument.TextStyle.cs</c>'s
///     <c>ResolveEffectiveRun</c> by walking the shape's own direct row override, then the
///     <c>TextStyle</c> StyleSheet chain (mirroring <c>LineStyle</c>/<c>FillStyle</c>'s own
///     resolution pattern).
/// </summary>
/// <param name="Text">The run's own literal text (see <see cref="VsdxRawTextRun.Text"/>).</param>
/// <param name="FontFamily">The resolved typeface name (the literal sentinel <c>"Themed"</c> resolves to a neutral default font name - see <c>VsdxDocument.TextStyle.cs</c>).</param>
/// <param name="SizeInches">The resolved font em-height, in inches (Visio's own native unit for this cell).</param>
/// <param name="Bold">Whether the run is resolved bold (<c>Style</c> bit 0).</param>
/// <param name="Italic">Whether the run is resolved italic (<c>Style</c> bit 1).</param>
/// <param name="Color">The resolved ink color (see <c>VsdxColorPalette.Resolve</c>).</param>
/// <param name="Paragraph">The owning paragraph's resolved effective properties.</param>
internal sealed record VsdxEffectiveTextRun(
    string Text,
    string FontFamily,
    double SizeInches,
    bool Bold,
    bool Italic,
    Rgba32 Color,
    VsdxEffectiveParagraphProperties Paragraph);
