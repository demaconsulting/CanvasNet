// cspell:ignore scn
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     A resolved <c>/Pattern</c> resource (PDF 32000-1 §8.7.3): either a <c>/PatternType 2</c>
    ///     shading pattern (built by <c>PdfDocument.Patterns.Shading.cs</c>'s <c>BuildShadingPattern</c>)
    ///     or a <c>/PatternType 1</c> tiling pattern (built by <c>PdfDocument.Patterns.Tiling.cs</c>'s
    ///     <c>BuildTilingPattern</c>), carrying exactly the fields its own <see cref="Kind"/> needs
    ///     and leaving the other half <see langword="null"/>/default.
    /// </summary>
    internal sealed class ResolvedPattern
    {
        /// <summary>Distinguishes which half of this pattern's fields are populated.</summary>
        internal enum PatternKind
        {
            /// <summary>A <c>/PatternType 2</c> shading pattern - see the <c>Shading*</c>/<see cref="Coords"/>/<see cref="Domain"/>/<see cref="Evaluate"/> fields.</summary>
            Shading,

            /// <summary>A <c>/PatternType 1</c> tiling pattern - see the <see cref="BBox"/>/<see cref="XStep"/>/<see cref="YStep"/>/<see cref="PaintType"/>/<see cref="Resources"/>/<see cref="ContentBytes"/> fields.</summary>
            Tiling,
        }

        /// <summary>Gets which half of this pattern's fields are populated.</summary>
        internal required PatternKind Kind { get; init; }

        /// <summary>
        ///     Gets the pattern's own <c>/Matrix</c> (identity when absent), mapping pattern-space
        ///     coordinates into the page's default (initial) coordinate system - see
        ///     <see cref="PatternToDeviceTransform"/>.
        /// </summary>
        internal required Matrix3x2 Matrix { get; init; }

        /// <summary>Gets the decoded mesh for a <c>/ShadingType 4</c>-<c>7</c> shading, or <see langword="null"/> otherwise. Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Shading"/>.</summary>
        internal MeshShading? Mesh { get; init; }

        /// <summary>Gets the shading's <c>/ShadingType</c> (<c>2</c> axial, <c>3</c> radial, or <c>4</c>-<c>7</c> mesh). Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Shading"/>.</summary>
        internal int ShadingType { get; init; }

        /// <summary>Gets the shading's resolved <c>/ColorSpace</c> (restricted to <c>DeviceGray</c>/<c>DeviceRGB</c>/<c>DeviceCMYK</c>). Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Shading"/>.</summary>
        internal PdfColorSpace? ShadingColorSpace { get; init; }

        /// <summary>Gets the shading's <c>/Coords</c> (4 elements for axial, 6 for radial). Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Shading"/>.</summary>
        internal double[]? Coords { get; init; }

        /// <summary>Gets the function input domain to sample across (2 elements). Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Shading"/>.</summary>
        internal double[]? Domain { get; init; }

        /// <summary>Gets the resolved function (or function-array) evaluation delegate. Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Shading"/>.</summary>
        internal Func<double, double[]>? Evaluate { get; init; }

        /// <summary>Gets the tiling pattern's <c>/BBox</c> (4 elements: <c>[llx lly urx ury]</c>). Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Tiling"/>.</summary>
        internal double[]? BBox { get; init; }

        /// <summary>Gets the tiling pattern's pattern-space horizontal tile pitch. Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Tiling"/>.</summary>
        internal float XStep { get; init; }

        /// <summary>Gets the tiling pattern's pattern-space vertical tile pitch. Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Tiling"/>.</summary>
        internal float YStep { get; init; }

        /// <summary>Gets the tiling pattern's <c>/PaintType</c> (<c>1</c> colored or <c>2</c> uncolored). Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Tiling"/>.</summary>
        internal int PaintType { get; init; }

        /// <summary>Gets the tiling pattern's own resolved <c>/Resources</c>, or the invoking stream's own resources when absent (resource-scope fallback). Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Tiling"/>.</summary>
        internal PdfObject? Resources { get; init; }

        /// <summary>Gets the tiling pattern's own decoded content-stream bytes. Only meaningful when <see cref="Kind"/> is <see cref="PatternKind.Tiling"/>.</summary>
        internal byte[]? ContentBytes { get; init; }
    }

    /// <summary>
    ///     Resolves a named <c>/Pattern</c> resource from the current page's
    ///     <c>/Resources/Pattern</c> dictionary and dispatches on its <c>/PatternType</c> to
    ///     <c>BuildShadingPattern</c>/<c>BuildTilingPattern</c>.
    /// </summary>
    /// <param name="name">The pattern resource name (the <c>scn</c>/<c>SCN</c> operand's trailing name operand, without the leading slash).</param>
    /// <returns>The resolved pattern.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved value is not a dictionary/stream, when <c>/PatternType</c> is
    ///     missing/malformed, or propagated from the type-specific builder for malformed pattern
    ///     content.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="name"/> is not declared in the current page's
    ///     <c>/Resources/Pattern</c> dictionary (feature <c>pdf-pattern-not-declared</c>), or when
    ///     <c>/PatternType</c> is not <c>1</c> or <c>2</c> (feature <c>pdf-pattern-type-{n}</c>),
    ///     or propagated from the type-specific builder for a further-unsupported shape (for
    ///     example an unsupported <c>/ShadingType</c>).
    /// </exception>
    internal ResolvedPattern ResolvePattern(string name)
    {
        var patternDictionaryEntry = _resources?.Get("Pattern");
        var resolvedDictionary = patternDictionaryEntry is null ? null : Resolve(patternDictionaryEntry);
        var entry = resolvedDictionary?.Get(name);
        if (entry is null)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-pattern-not-declared",
                $"Undefined pattern '/{name}' (not declared in the current page's /Resources/Pattern).");
        }

        var pattern = Resolve(entry);
        var patternType = RequireIntEntry(pattern, "PatternType");
        return patternType switch
        {
            1 => BuildTilingPattern(pattern),
            2 => BuildShadingPattern(pattern),
            _ => throw new UnsupportedImageFeatureException(
                $"pdf-pattern-type-{patternType}",
                $"/PatternType {patternType} is not supported; only 1 (tiling) and 2 (shading) are supported."),
        };
    }

    /// <summary>
    ///     Computes <paramref name="pattern"/>'s own pattern-space-to-device-space transform.
    /// </summary>
    /// <remarks>
    ///     Per PDF 32000-1 §8.7.3.1, "the pattern matrix maps pattern space to the default
    ///     (initial) coordinate system of the page" - <em>not</em> whatever CTM happens to be
    ///     active when the pattern is actually painted with (which may have been further
    ///     transformed by any number of intervening <c>cm</c> operators since the page began).
    ///     This is why this method composes against <see cref="_pageInitialCtm"/> rather than
    ///     <see cref="_gs"/>'s current <see cref="GraphicsState.CurrentTransform"/>.
    ///     <para>
    ///         <strong>Documented limitation</strong>: a pattern resolved from inside a nested
    ///         <c>/Subtype /Form</c> XObject's own <c>/Resources/Pattern</c> dictionary still
    ///         composes against the top-level page's own <see cref="_pageInitialCtm"/>, not the
    ///         Form's own default coordinate system (which the PDF specification's own, more
    ///         precise reading would require) - this phase does not track a separate
    ///         per-Form-nesting-level "default CTM", matching this phase's documented
    ///         Form-XObject scope (no <c>/BBox</c> clipping, no <c>/Group</c> handling either).
    ///     </para>
    /// </remarks>
    private Matrix3x2 PatternToDeviceTransform(ResolvedPattern pattern) => pattern.Matrix * _pageInitialCtm;
}
