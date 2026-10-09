// cspell:ignore charprocs fontmatrix
using System.Numerics;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     A procedure-painted <c>/Subtype /Type3</c> font, fully resolved from its
    ///     <c>/Resources/Font</c> dictionary: its <c>/FontMatrix</c> (glyph-space-to-text-space
    ///     transform), its code-to-glyph-name <c>/Encoding</c> map, its glyph-name-to-content-
    ///     stream <c>/CharProcs</c> dictionary, its own optional <c>/Resources</c> (falls back to
    ///     the invoking page's resources at paint time when absent - see
    ///     <see cref="PaintType3Glyph"/>), and its code-to-declared-advance-width table.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="ResolvedSimpleFont"/>/<see cref="ResolvedCompositeFont"/>, a Type 3
    ///     font has no outline-glyph font program at all: each glyph is itself a small content
    ///     stream (a <c>/CharProcs</c> entry) that paints its own ink via ordinary path-
    ///     construction/painting operators, interpreted by re-entering
    ///     <see cref="ExecuteOperators"/> exactly like a Form XObject's own content stream (see
    ///     <see cref="PaintType3Glyph"/>, which mirrors <see cref="OpDrawFormXObject"/>'s own
    ///     save/restore/recurse pattern). Instances are built once by
    ///     <see cref="BuildResolvedType3Font"/> and cached by <see cref="ResolveFont"/> in
    ///     <see cref="_fontCache"/>, exactly like <see cref="ResolvedSimpleFont"/>.
    /// </remarks>
    private sealed class ResolvedType3Font : IResolvedFont
    {
        /// <summary>
        ///     Gets the font's required <c>/FontMatrix</c>: the glyph-space-to-text-space
        ///     transform every glyph procedure's path-construction coordinates (and the declared
        ///     advance width - see <see cref="Resolve"/>) are mapped through, in place of the
        ///     fixed <c>1/1000</c> (or <c>1/UnitsPerEm</c>) scale a simple/composite font's own
        ///     outline uses.
        /// </summary>
        internal required Matrix3x2 FontMatrix { get; init; }

        /// <summary>
        ///     Gets the code (0-255) to glyph-name map, resolved from <c>/Encoding/Differences</c>
        ///     (see <see cref="ResolveType3Encoding"/>). A code absent from this map has no mapped
        ///     glyph name - <see cref="PaintType3Glyph"/> treats this leniently (paints nothing,
        ///     still advances by the code's declared width), not as an error.
        /// </summary>
        internal required IReadOnlyDictionary<int, string> Encoding { get; init; }

        /// <summary>
        ///     Gets the font's resolved, required <c>/CharProcs</c> dictionary, mapping a glyph
        ///     name (from <see cref="Encoding"/>) to its own content-stream reference. A glyph
        ///     name absent from this dictionary is treated identically to an unmapped code (see
        ///     <see cref="Encoding"/>'s own remarks) - a documented leniency, not an error.
        /// </summary>
        internal required PdfObject CharProcs { get; init; }

        /// <summary>
        ///     Gets the font's own, already-resolved <c>/Resources</c> dictionary, or
        ///     <see langword="null"/> when absent - <see cref="PaintType3Glyph"/> falls back to
        ///     the invoking page/Form's own currently active resources in that case, exactly like
        ///     a Form XObject with no <c>/Resources</c> of its own (see
        ///     <see cref="OpDrawFormXObject"/>).
        /// </summary>
        internal required PdfObject? Resources { get; init; }

        /// <summary>
        ///     Gets the code (0-255) to declared advance width map, in the font's own glyph-space
        ///     units (mapped through <see cref="FontMatrix"/> by <see cref="Resolve"/> - never
        ///     divided by a fixed <c>1000</c>, unlike <see cref="ResolvedSimpleFont.Widths"/>),
        ///     from the font dictionary's <c>/FirstChar</c>/<c>/LastChar</c>/<c>/Widths</c>
        ///     entries (via the same <see cref="ResolveWidths"/> helper
        ///     <see cref="ResolvedSimpleFont"/> uses).
        /// </summary>
        internal required IReadOnlyDictionary<int, double> Widths { get; init; }

        /// <summary>Gets the font's resolved <c>/FontDescriptor/MissingWidth</c> fallback (in the same glyph-space units as <see cref="Widths"/>), or <c>0</c> when not declared.</summary>
        internal required double MissingWidth { get; init; }

        /// <summary>A Type 3 font is always a simple, single-byte-code font per the PDF specification.</summary>
        int IResolvedFont.CodeByteWidth => 1;

        /// <summary>
        ///     Resolves <paramref name="code"/>'s declared advance width (an explicit
        ///     <see cref="Widths"/> entry, else <see cref="MissingWidth"/>) from the font's own
        ///     glyph-space units into text-space units by transforming it, as a displacement
        ///     vector (via <see cref="Vector2.TransformNormal(Vector2, Matrix3x2)"/>, which applies
        ///     only <see cref="FontMatrix"/>'s linear scale/rotation/skew part, never its
        ///     translation - the mathematically correct operation for an advance width, which is a
        ///     vector, not a point), through <see cref="FontMatrix"/> - <em>not</em> divided by a
        ///     fixed <c>1000</c> like <see cref="ResolvedSimpleFont.Resolve"/>, since a Type 3
        ///     font's own <c>/FontMatrix</c> (not a fixed convention) is its documented glyph-
        ///     space-to-text-space mapping for every glyph-space quantity. The returned font is
        ///     always <see langword="null"/> (a Type 3 font has no outline-glyph font program at
        ///     all) and the returned glyph index is always <c>0</c> - neither is ever consulted by
        ///     <see cref="ShowGlyph"/>'s own <c>ResolvedType3Font</c> branch - see
        ///     <see cref="PaintType3Glyph"/>'s own glyph-name-keyed lookup instead.
        /// </summary>
        public (TrueTypeFont? Font, int GlyphIndex, double Width) Resolve(int code)
        {
            var glyphSpaceWidth = Widths.TryGetValue(code, out var declaredWidth) ? declaredWidth : MissingWidth;
            var textSpaceWidth = Vector2.TransformNormal(new Vector2((float)glyphSpaceWidth, 0f), FontMatrix).X;
            return (null, 0, textSpaceWidth);
        }
    }

    /// <summary>
    ///     The maximum number of nested Type 3 glyph-procedure invocations
    ///     <see cref="PaintType3Glyph"/> allows before failing closed - a dedicated counter
    ///     distinct from <see cref="MaxFormNestingDepth"/>/<see cref="_formNestingDepth"/>,
    ///     bounding a Type 3 glyph procedure's own re-entrance boundary (a glyph proc invoking
    ///     <c>Tj</c> against the same or another Type 3 font) independently of a Form XObject's
    ///     (crossed via <c>Do</c>) - each policy's own fail-closed message stays unambiguous about
    ///     which kind of nesting was exceeded, while the combined worst-case recursion depth (12
    ///     Type 3 + 12 Form, alternating) remains small and finite.
    /// </summary>
    private const int MaxType3NestingDepth = 12;

    /// <summary>
    ///     The current Type 3 glyph-procedure nesting depth, incremented/decremented around every
    ///     <see cref="PaintType3Glyph"/> call. Reset to <c>0</c> at the start of every top-level
    ///     <see cref="ExecuteContentStream"/> call, alongside <see cref="_formNestingDepth"/>.
    /// </summary>
    private int _type3NestingDepth;

    /// <summary>
    ///     Builds a <see cref="ResolvedType3Font"/> from a <c>/Subtype /Type3</c> font dictionary:
    ///     requires and parses <c>/FontMatrix</c> (via <see cref="ReadFontMatrix"/>), requires and
    ///     resolves <c>/CharProcs</c> to a dictionary, resolves <c>/Encoding</c> (via
    ///     <see cref="ResolveType3Encoding"/> - lenient, see that method's own remarks),
    ///     resolves <c>/Widths</c>/<c>/FirstChar</c>/the descriptor's <c>/MissingWidth</c> via the
    ///     existing, unmodified <see cref="ResolveWidths"/> helper (the descriptor is resolved
    ///     from <c>/FontDescriptor</c> when present, else <see cref="PdfObject.NullValue"/>,
    ///     exactly like <see cref="BuildResolvedSimpleFont"/>'s own pattern - a Type 3 font's
    ///     <c>/FontDescriptor</c> is optional per the PDF specification), and resolves
    ///     <c>/Resources</c> (optional, <see langword="null"/> when absent).
    /// </summary>
    /// <param name="fontDict">The font dictionary being resolved.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/FontMatrix</c> is missing, is not an array of exactly 6 numbers, or
    ///     contains a non-number (propagated from <see cref="ReadFontMatrix"/>), or when
    ///     <c>/CharProcs</c> is missing or does not resolve to a dictionary.
    /// </exception>
    private IResolvedFont BuildResolvedType3Font(PdfObject fontDict)
    {
        var fontMatrix = ReadFontMatrix(fontDict);

        var charProcsEntry = fontDict.Get("CharProcs")
            ?? throw new InvalidDataException("Type3 font dictionary is missing required /CharProcs.");
        var charProcs = Resolve(charProcsEntry);
        if (charProcs.Kind != PdfKind.Dictionary)
        {
            throw new InvalidDataException("/CharProcs must resolve to a dictionary.");
        }

        var encoding = ResolveType3Encoding(fontDict.Get("Encoding"));

        var descriptorEntry = fontDict.Get("FontDescriptor");
        var descriptor = descriptorEntry is null ? PdfObject.NullValue : Resolve(descriptorEntry);
        var (widths, missingWidth) = ResolveWidths(fontDict, descriptor);

        var resourcesEntry = fontDict.Get("Resources");
        var resources = resourcesEntry is null ? null : Resolve(resourcesEntry);

        return new ResolvedType3Font
        {
            FontMatrix = fontMatrix,
            Encoding = encoding,
            CharProcs = charProcs,
            Resources = resources,
            Widths = widths,
            MissingWidth = missingWidth,
        };
    }

    /// <summary>
    ///     Reads a <c>/Subtype /Type3</c> font dictionary's required <c>/FontMatrix</c> entry: an
    ///     array of exactly 6 numbers, interpreted exactly like the <c>cm</c> operator's 6
    ///     operands. Deliberately independently implemented from (not shared with)
    ///     <see cref="ReadFormMatrix"/> - <c>/FontMatrix</c> is required (a Form XObject's
    ///     <c>/Matrix</c> is optional, defaulting to the identity matrix), so the two methods'
    ///     error-handling shape differs; keeping them separate also confines this new file's own
    ///     diff, avoiding any risk to the already-tested, unrelated Form-XObject path.
    /// </summary>
    /// <param name="fontDict">The font dictionary being resolved.</param>
    /// <returns>The font's declared <c>/FontMatrix</c>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/FontMatrix</c> is missing, is not an array of exactly 6 numbers, or
    ///     contains a non-number entry.
    /// </exception>
    private Matrix3x2 ReadFontMatrix(PdfObject fontDict)
    {
        var matrixEntry = fontDict.Get("FontMatrix")
            ?? throw new InvalidDataException("Type3 font dictionary is missing required /FontMatrix.");

        var resolved = Resolve(matrixEntry);
        if (resolved.Kind != PdfKind.Array || resolved.Items.Count != 6)
        {
            throw new InvalidDataException("/FontMatrix must be an array of exactly 6 numbers.");
        }

        var values = new double[6];
        for (var i = 0; i < 6; i++)
        {
            var item = Resolve(resolved.Items[i]);
            if (item.Kind != PdfKind.Number)
            {
                throw new InvalidDataException("/FontMatrix entries must all be numbers.");
            }

            values[i] = item.Number;
        }

        return new Matrix3x2(
            (float)values[0],
            (float)values[1],
            (float)values[2],
            (float)values[3],
            (float)values[4],
            (float)values[5]);
    }

    /// <summary>
    ///     Resolves a Type 3 font dictionary's <c>/Encoding</c> entry into a code-to-glyph-name
    ///     map (rather than a code-to-Unicode-codepoint map, which <see cref="ResolveEncoding"/>
    ///     builds for a simple font): extracts the dictionary's own <c>/Differences</c> array (if
    ///     any) and parses it via the shared <see cref="ParseDifferences"/> helper, keeping each
    ///     glyph name verbatim (never validated against <see cref="StandardGlyphNames"/>'s Adobe
    ///     Glyph List subset, unlike <see cref="ApplyDifferences"/>'s own policy) - a Type 3
    ///     glyph name is a direct, font-specific key into <c>/CharProcs</c>, which may use
    ///     entirely arbitrary names outside any standard glyph-name vocabulary.
    /// </summary>
    /// <param name="encodingEntry">
    ///     The font dictionary's <c>/Encoding</c> entry (expected to be a dictionary bearing
    ///     <c>/Differences</c>, or <see langword="null"/> when absent).
    /// </param>
    /// <returns>
    ///     The code-to-glyph-name map - empty when <c>/Encoding</c> is absent entirely, does not
    ///     resolve to a dictionary, or has no <c>/Differences</c> entry. This is a deliberate
    ///     leniency (every such code simply paints nothing, see <see cref="PaintType3Glyph"/>),
    ///     not a fail-closed policy - the PDF specification does not explicitly prescribe either
    ///     choice for this exact case, and this codebase's own established posture (see
    ///     <see cref="ResolveWidths"/>'s own missing-<c>/Widths</c> leniency) favors graceful
    ///     degradation here.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Propagated from <see cref="ParseDifferences"/> when a resolved <c>/Differences</c>
    ///     entry is present but is not an array, when an array entry is neither a number nor a
    ///     name, or when a name entry appears before any starting code.
    /// </exception>
    private IReadOnlyDictionary<int, string> ResolveType3Encoding(PdfObject? encodingEntry)
    {
        var map = new Dictionary<int, string>();
        if (encodingEntry is null)
        {
            return map;
        }

        var resolved = Resolve(encodingEntry);
        if (resolved.Kind != PdfKind.Dictionary)
        {
            return map;
        }

        var differencesEntry = resolved.Get("Differences");
        if (differencesEntry is null)
        {
            return map;
        }

        ParseDifferences(Resolve(differencesEntry), (code, name) => map[code] = name);
        return map;
    }

    /// <summary>
    ///     Paints a single Type 3 glyph procedure for <paramref name="code"/> - the Type 3
    ///     counterpart of the outline-glyph fill path <see cref="ShowGlyph"/>'s non-Type3 branch
    ///     performs, called from that same method's early <c>ResolvedType3Font</c> branch (guarded
    ///     identically by <c>_gs.RenderMode != 3</c>, so an invisible-mode glyph proc never
    ///     executes at all - parity with the outline path, which likewise skips
    ///     <c>GetGlyphOutline</c>/fill entirely in that mode).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Looks up <paramref name="code"/> in <paramref name="font"/>'s own
    ///         <see cref="ResolvedType3Font.Encoding"/>; an unmapped code paints nothing (a
    ///         documented leniency - the caller's own shared displacement/advance logic in
    ///         <see cref="ShowGlyph"/> still runs unconditionally, so the text position still
    ///         advances by the code's declared width). Looks up the resolved glyph name in
    ///         <see cref="ResolvedType3Font.CharProcs"/>; a glyph name absent from
    ///         <c>/CharProcs</c> is the identical leniency. Otherwise resolves the <c>/CharProcs</c>
    ///         entry and requires it to be a stream (<see cref="InvalidDataException"/> if not).
    ///     </para>
    ///     <para>
    ///         The glyph procedure's own path-construction coordinate space is mapped to device
    ///         pixel-space by composing <see cref="ResolvedType3Font.FontMatrix"/> (applied
    ///         first/innermost, mapping the glyph proc's own glyph-space coordinates into text
    ///         space) with <see cref="ComputeTextRenderingMatrix"/> (applied second/outermost,
    ///         mapping text space the rest of the way to device pixel space) - the same
    ///         left-multiply premultiply convention <c>cm</c>/<c>Trm</c> composition already
    ///         establishes. This <em>replaces</em> the outline path's own fixed
    ///         <c>1/UnitsPerEm</c> (or classic Type 1's <c>1/1000</c>) scale entirely - a Type 3
    ///         font's own <c>/FontMatrix</c>, not a fixed convention, is its documented glyph-
    ///         space-to-text-space mapping.
    ///     </para>
    ///     <para>
    ///         Executes the decoded glyph procedure bytes by re-entering
    ///         <see cref="ExecuteOperators"/>, following the exact same re-entrance pattern
    ///         <see cref="OpDrawFormXObject"/> establishes for a Form XObject: a
    ///         <c>try</c>/<c>finally</c> saves and unconditionally restores
    ///         <see cref="_resources"/>/<see cref="_gs"/>/<see cref="_gsStack"/>, bracketing the
    ///         glyph procedure's own CTM/color/font-selection/path-construction-state mutations
    ///         exactly like an implicit <c>q</c> ... <c>Q</c> - so, in particular, a <c>cm</c> or
    ///         color-setting operator inside a glyph procedure can never leak back out into the
    ///         invoking text-showing operator's own graphics state. <see cref="_resources"/> is
    ///         set to the Type 3 font's own <see cref="ResolvedType3Font.Resources"/> when
    ///         present, else falls back to the invoking stream's currently active
    ///         <see cref="_resources"/> unchanged - exactly like a Form XObject with no
    ///         <c>/Resources</c> of its own. The nested graphics state is a clone of the invoking
    ///         <see cref="_gs"/> (so the glyph proc inherits the caller's fill color, line style,
    ///         etc., as its own starting state) with <see cref="GraphicsState.CurrentTransform"/>
    ///         overridden to the composed glyph matrix above, and a fresh, empty
    ///         <see cref="_gsStack"/> (so an unbalanced <c>q</c>/<c>Q</c> inside the glyph proc can
    ///         never touch the invoking stream's own saved states).
    ///     </para>
    ///     <para>
    ///         <strong>Scope boundary (deliberate, named limitation)</strong>: this phase does
    ///         <em>not</em> enforce the PDF specification's own <c>d1</c> "uncolored glyph" color-
    ///         suppression semantics - a <c>d1</c> glyph procedure that itself sets a fill/stroke
    ///         color (via <c>rg</c>/<c>g</c>/<c>k</c>/<c>sc</c>/<c>scn</c> or their stroking
    ///         counterparts) is honored exactly like ordinary content, painting with its own
    ///         declared color rather than being forced to reuse the invoking text's own fill
    ///         color. This is the lighter-weight of the two PDF-specification-permitted options,
    ///         chosen to bound this phase's scope/risk; a future phase could add true <c>d1</c>
    ///         color suppression without altering this phase's public contract.
    ///     </para>
    /// </remarks>
    /// <param name="font">The Type 3 font the glyph belongs to.</param>
    /// <param name="code">The character code being shown.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the current Type 3 nesting depth already equals
    ///     <see cref="MaxType3NestingDepth"/>, when the resolved <c>/CharProcs</c> entry does not
    ///     resolve to a stream, or propagated from nested execution of the glyph procedure's own
    ///     content stream.
    /// </exception>
    private void PaintType3Glyph(ResolvedType3Font font, int code)
    {
        if (!font.Encoding.TryGetValue(code, out var glyphName))
        {
            return;
        }

        var charProcEntry = font.CharProcs.Get(glyphName);
        if (charProcEntry is null)
        {
            return;
        }

        if (_type3NestingDepth >= MaxType3NestingDepth)
        {
            throw new InvalidDataException(
                $"Type3 glyph procedure nesting exceeds the maximum supported depth of {MaxType3NestingDepth}.");
        }

        var charProc = Resolve(charProcEntry);
        if (charProc.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException($"/CharProcs entry '/{glyphName}' does not resolve to a stream.");
        }

        var glyphMatrix = font.FontMatrix * ComputeTextRenderingMatrix();
        var contentBytes = GetStreamDecodedBytes(charProc);

        var savedResources = _resources;
        var savedGs = _gs;
        var savedGsStack = _gsStack;
        var savedPendingClipFillRule = _pendingClipFillRule;
        _type3NestingDepth++;
        try
        {
            _resources = font.Resources ?? _resources;
            var nestedGs = savedGs.Clone();
            nestedGs.CurrentTransform = glyphMatrix;
            _gs = nestedGs;
            _gsStack = new Stack<GraphicsState>();
            _pendingClipFillRule = null;
            ExecuteOperators(contentBytes);
        }
        finally
        {
            _type3NestingDepth--;
            _resources = savedResources;
            _gs = savedGs;
            _gsStack = savedGsStack;
            _pendingClipFillRule = savedPendingClipFillRule;
        }
    }

    /// <summary>
    ///     Handles the <c>d0</c> glyph-width operator (<c>wx wy d0</c>), used inside a "colored"
    ///     Type 3 glyph procedure to declare its own advance width. Parses and validates its 2
    ///     numeric operands (via the existing, shared <see cref="RequireNumbers"/> helper) purely
    ///     for operand-count/type validation; the operands themselves are discarded and never
    ///     consulted for layout - <c>/Widths</c> (scaled through <c>/FontMatrix</c> - see
    ///     <see cref="ResolvedType3Font.Resolve"/>) remains the sole authoritative advance-width
    ///     source. A stray <c>d0</c> outside any glyph procedure parses/validates identically and
    ///     has no further effect - no new validation burden beyond this already-established
    ///     per-operator operand-count convention.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the operand count/type does not match (exactly 2 numbers).</exception>
    private static void OpType3SetWidth(IReadOnlyList<PdfObject> operands) => RequireNumbers(operands, "d0", 2);

    /// <summary>
    ///     Handles the <c>d1</c> glyph-width-and-bounding-box operator
    ///     (<c>wx wy llx lly urx ury d1</c>), used inside an "uncolored" Type 3 glyph procedure.
    ///     Parses and validates its 6 numeric operands (via <see cref="RequireNumbers"/>) purely
    ///     for operand-count/type validation; like <see cref="OpType3SetWidth"/>, the operands
    ///     (including <c>wx</c>/<c>wy</c> and the declared bounding box) are discarded and never
    ///     consulted for layout or caching - see <see cref="ResolvedType3Font.Resolve"/>'s own
    ///     remarks. <strong>This phase does not enforce <c>d1</c>'s own "uncolored glyph" color-
    ///     suppression semantics</strong> - see <see cref="PaintType3Glyph"/>'s own remarks for
    ///     this deliberate, named scope boundary.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the operand count/type does not match (exactly 6 numbers).</exception>
    private static void OpType3SetWidthAndBBox(IReadOnlyList<PdfObject> operands) => RequireNumbers(operands, "d1", 6);
}
