using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The destination surface for the content stream currently being executed by
    ///     <see cref="ExecuteContentStream"/>.
    /// </summary>
    private Surface _surface = null!;

    /// <summary>
    ///     The current page's resolved <c>/Resources</c> dictionary (inherited from the nearest
    ///     ancestor that declares one, exactly like <c>/MediaBox</c>/<c>/Rotate</c>), or
    ///     <see langword="null"/> when no ancestor declares one - used by the <c>cs</c>/<c>CS</c>
    ///     color-space operators and the <c>Do</c> image-XObject operator to resolve a named
    ///     <c>/ColorSpace</c>/<c>/XObject</c> resource. Reset at the start of every
    ///     <see cref="ExecuteContentStream"/> call.
    /// </summary>
    private PdfObject? _resources;

    /// <summary>
    ///     The page's own initial current transformation matrix (the base CTM the top-level
    ///     render call was invoked with), captured once per top-level <see cref="Render(int, int, int, PdfRenderOptions?)"/>
    ///     call and never mutated by any subsequent <c>cm</c>/Form-XObject-matrix composition -
    ///     used by <c>PdfDocument.Patterns.cs</c>'s <c>PatternToDeviceTransform</c> to anchor a
    ///     pattern's own <c>/Matrix</c> against the page's default coordinate system, per
    ///     PDF 32000-1 §8.7.3.1 ("the pattern matrix maps pattern space to the default
    ///     (initial) coordinate system of the page"), rather than against whatever CTM happens to
    ///     be active when the pattern is actually painted with.
    /// </summary>
    private Matrix3x2 _pageInitialCtm;

    /// <summary>
    ///     Resets every piece of per-render content-stream state (destination surface, resources,
    ///     graphics state/stack, path-construction state, font cache, text matrices, and the Form
    ///     XObject nesting-depth counter) and then tokenizes and executes
    ///     <paramref name="contentBytes"/> as the page's top-level content stream via
    ///     <see cref="ExecuteOperators"/>, painting recognized path-construction/painting
    ///     operators onto <paramref name="surface"/> and ignoring every other operator, starting
    ///     from <paramref name="baseCtm"/> as the initial graphics state's current transformation
    ///     matrix.
    /// </summary>
    /// <param name="contentBytes">The already-decoded (filter-applied) content-stream bytes.</param>
    /// <param name="surface">The destination surface every painting operator draws onto.</param>
    /// <param name="baseCtm">
    ///     The initial current transformation matrix, mapping PDF user-space points to device
    ///     pixel-space points, before any content-stream <c>cm</c> operator is applied.
    /// </param>
    /// <param name="resources">
    ///     The current page's resolved <c>/Resources</c> dictionary, or <see langword="null"/>
    ///     when none is declared anywhere in the page's ancestry.
    /// </param>
    /// <remarks>
    ///     This method is the single entry point called once per <see cref="Render(int, int, int, PdfRenderOptions?)"/>
    ///     call. A nested <c>/Subtype /Form</c> XObject (see <see cref="OpDrawFormXObject"/>) does
    ///     <em>not</em> call this method again - it re-enters <see cref="ExecuteOperators"/>
    ///     directly, so that only the Form-specific state it explicitly saves/swaps/restores is
    ///     affected, and every other piece of state reset here is left untouched by nested
    ///     execution.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the content stream is not lexically well-formed, or when a recognized
    ///     operator's operand count/type does not match its documented requirement.
    /// </exception>
    private void ExecuteContentStream(byte[] contentBytes, Surface surface, Matrix3x2 baseCtm, PdfObject? resources)
    {
        _surface = surface;
        _resources = resources;
        _gsStack = new Stack<GraphicsState>();
        _gs = new GraphicsState { CurrentTransform = baseCtm };
        _pageInitialCtm = baseCtm;
        _pathBuilder = new PathBuilder();
        _currentPoint = default;
        _subpathStart = default;
        _hasOpenSubpath = false;
        _pendingClipFillRule = null;
        _fontCache = new Dictionary<PdfObject, IResolvedFont>();
        _textMatrix = Matrix3x2.Identity;
        _lineMatrix = Matrix3x2.Identity;
        _textClipBuilder = null;
        _textClipPending = false;
        _formNestingDepth = 0;
        _type3NestingDepth = 0;
        _colorSpaceRecursionDepth = 0;
        _functionRecursionDepth = 0;

        ExecuteOperators(contentBytes);
    }

    /// <summary>
    ///     Tokenizes and executes <paramref name="contentBytes"/> against the currently active
    ///     graphics state/resources/surface, dispatching each recognized keyword operator in turn.
    /// </summary>
    /// <param name="contentBytes">The already-decoded (filter-applied) content-stream bytes.</param>
    /// <remarks>
    ///     Re-entrant: <see cref="OpDrawFormXObject"/> calls this method recursively (bounded by
    ///     <see cref="MaxFormNestingDepth"/>) to execute a nested <c>/Subtype /Form</c> XObject's
    ///     own content stream against a temporarily swapped-in graphics state/resources, without
    ///     re-running <see cref="ExecuteContentStream"/>'s full state reset; <c>PaintType3Glyph</c>
    ///     (<c>PdfDocument.Fonts.Type3.cs</c>) calls it recursively in exactly the same way
    ///     (bounded by its own, independent <see cref="MaxType3NestingDepth"/>) to execute a
    ///     <c>/Subtype /Type3</c> glyph procedure's own content stream.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the content stream is not lexically well-formed, or when a recognized
    ///     operator's operand count/type does not match its documented requirement.
    /// </exception>
    private void ExecuteOperators(byte[] contentBytes)
    {
        var tokenizer = new PdfTokenizer(contentBytes);
        var operands = new List<PdfObject>();
        while (true)
        {
            var token = tokenizer.NextToken();
            if (token.Kind == PdfTokenKind.EndOfFile)
            {
                break;
            }

            switch (token.Kind)
            {
                case PdfTokenKind.Number:
                    operands.Add(PdfObject.FromNumber(token.Number));
                    break;

                case PdfTokenKind.Name:
                    operands.Add(PdfObject.FromName(token.Text ?? string.Empty));
                    break;

                case PdfTokenKind.LiteralString:
                case PdfTokenKind.HexString:
                    operands.Add(PdfObject.FromString(token.Bytes ?? []));
                    break;

                case PdfTokenKind.ArrayStart:
                case PdfTokenKind.DictStart:
                    // Inline dictionary operands occur legitimately in content streams (e.g. the
                    // BDC/DP marked-content operators' inline "properties" dictionary); parse them
                    // with the same generic object parser used elsewhere rather than rejecting them.
                    operands.Add(ParseValue(tokenizer, token));
                    break;

                case PdfTokenKind.Keyword:
                    DispatchOperator(token.Text ?? string.Empty, operands);
                    operands.Clear();
                    break;

                default:
                    // A content stream never legitimately contains a bare '>>'/']' delimiter or
                    // any other unrecognized token kind here (well-formed '['/'<<' operands are
                    // consumed above, alongside their matching close token) - this class's own
                    // convention (see PdfDocument.Xref.cs) is to throw on truly unparseable
                    // structure rather than silently continuing.
                    throw new InvalidDataException(
                        $"Unexpected token '{token.Kind}' in PDF content stream.");
            }
        }
    }

    /// <summary>
    ///     Dispatches one recognized content-stream keyword operator (per the fixed set this
    ///     phase implements) against its accumulated operand stack, silently ignoring any other
    ///     keyword (ExtGState, inline images, and every other operator not yet implemented).
    /// </summary>
    /// <param name="operatorName">The operator keyword.</param>
    /// <param name="operands">The operands accumulated since the previous operator.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a recognized operator's operand count/type does not match its documented
    ///     requirement.
    /// </exception>
    /// <exception cref="Codecs.UnsupportedImageFeatureException">
    ///     Propagated from <see cref="OpSetFont"/> (an unsupported font), <c>ShowGlyph</c> (a Type 3
    ///     glyph shown under a clipping text-rendering mode <c>4</c>-<c>7</c>), or
    ///     <see cref="OpPaintShading"/> (an undeclared shading name, or a defined but unsupported
    ///     <c>/ShadingType</c>/<c>/ColorSpace</c> - the same exception the <c>scn</c>/<c>SCN</c>
    ///     Pattern-color-space path already throws for the identical underlying condition).
    /// </exception>
    private void DispatchOperator(string operatorName, List<PdfObject> operands)
    {
        switch (operatorName)
        {
            // Graphics-state operators (PdfDocument.GraphicsState.cs).
            case "q":
                RequireOperandCount(operands, "q", 0);
                OpPushGraphicsState();
                break;
            case "Q":
                RequireOperandCount(operands, "Q", 0);
                OpPopGraphicsState();
                break;
            case "cm":
                OpConcatMatrix(operands);
                break;
            case "w":
                OpSetLineWidth(operands);
                break;
            case "J":
                OpSetLineCap(operands);
                break;
            case "j":
                OpSetLineJoin(operands);
                break;
            case "M":
                OpSetMiterLimit(operands);
                break;
            case "d":
                OpSetDashPattern(operands);
                break;

            // Path-construction operators (PdfDocument.PathOps.cs).
            case "m":
                OpMoveTo(operands);
                break;
            case "l":
                OpLineTo(operands);
                break;
            case "c":
                OpCurveTo(operands);
                break;
            case "v":
                OpCurveToV(operands);
                break;
            case "y":
                OpCurveToY(operands);
                break;
            case "h":
                OpClosePath(operands);
                break;
            case "re":
                OpRectangle(operands);
                break;

            // Path-painting operators (PdfDocument.PathOps.cs).
            case "f":
            case "F":
                RequireOperandCount(operands, operatorName, 0);
                PaintCurrentPath(fill: true, FillRule.NonZero, stroke: false, closeFirst: false);
                break;
            case "f*":
                RequireOperandCount(operands, "f*", 0);
                PaintCurrentPath(fill: true, FillRule.EvenOdd, stroke: false, closeFirst: false);
                break;
            case "S":
                RequireOperandCount(operands, "S", 0);
                PaintCurrentPath(fill: false, FillRule.NonZero, stroke: true, closeFirst: false);
                break;
            case "s":
                RequireOperandCount(operands, "s", 0);
                PaintCurrentPath(fill: false, FillRule.NonZero, stroke: true, closeFirst: true);
                break;
            case "B":
                RequireOperandCount(operands, "B", 0);
                PaintCurrentPath(fill: true, FillRule.NonZero, stroke: true, closeFirst: false);
                break;
            case "B*":
                RequireOperandCount(operands, "B*", 0);
                PaintCurrentPath(fill: true, FillRule.EvenOdd, stroke: true, closeFirst: false);
                break;
            case "b":
                RequireOperandCount(operands, "b", 0);
                PaintCurrentPath(fill: true, FillRule.NonZero, stroke: true, closeFirst: true);
                break;
            case "b*":
                RequireOperandCount(operands, "b*", 0);
                PaintCurrentPath(fill: true, FillRule.EvenOdd, stroke: true, closeFirst: true);
                break;
            case "n":
                RequireOperandCount(operands, "n", 0);
                PaintCurrentPath(fill: false, FillRule.NonZero, stroke: false, closeFirst: false);
                break;

            // Clipping-path operators (PdfDocument.PathOps.cs): mark that the current path under
            // construction becomes the new clipping path the next time a path-painting operator
            // executes - see OpMarkPendingClip's remarks for the full deferred-apply timing (PDF
            // 32000-1 8.5.4).
            case "W":
                RequireOperandCount(operands, "W", 0);
                OpMarkPendingClip(FillRule.NonZero);
                break;
            case "W*":
                RequireOperandCount(operands, "W*", 0);
                OpMarkPendingClip(FillRule.EvenOdd);
                break;

            // Device color operators (PdfDocument.Color.cs).
            case "g":
                OpSetGrayFill(operands);
                break;
            case "G":
                OpSetGrayStroke(operands);
                break;
            case "rg":
                OpSetRgbFill(operands);
                break;
            case "RG":
                OpSetRgbStroke(operands);
                break;
            case "k":
                OpSetCmykFill(operands);
                break;
            case "K":
                OpSetCmykStroke(operands);
                break;
            case "cs":
                OpSetColorSpaceFill(operands);
                break;
            case "CS":
                OpSetColorSpaceStroke(operands);
                break;
            case "sc":
            case "scn":
                OpSetColorFill(operands, operatorName);
                break;
            case "SC":
            case "SCN":
                OpSetColorStroke(operands, operatorName);
                break;

            // Image XObject operator (PdfDocument.Images.cs).
            case "Do":
                OpDrawXObject(operands);
                break;

            // Shading operator (PdfDocument.Patterns.Shading.cs): paints a named /Resources
            // /Shading dictionary's gradient directly within the current clipping path (or the
            // shading's own /BBox when no clip is active), without constructing/consuming "the
            // current path" and without going through a /Pattern color-space selection at all -
            // a distinct mechanism from the scn/SCN + /Pattern + /PatternType 2 path above.
            case "sh":
                OpPaintShading(operands);
                break;

            // Text object operators (PdfDocument.Text.cs).
            case "BT":
                RequireOperandCount(operands, "BT", 0);
                OpBeginText();
                break;
            case "ET":
                RequireOperandCount(operands, "ET", 0);
                OpEndText();
                break;

            // Text-state operators (PdfDocument.Text.cs).
            case "Tc":
                OpSetCharSpacing(operands);
                break;
            case "Tw":
                OpSetWordSpacing(operands);
                break;
            case "Tz":
                OpSetHorizontalScaling(operands);
                break;
            case "TL":
                OpSetLeading(operands);
                break;
            case "Tf":
                OpSetFont(operands);
                break;
            case "Tr":
                OpSetTextRenderMode(operands);
                break;
            case "Ts":
                OpSetTextRise(operands);
                break;

            // Text-positioning operators (PdfDocument.Text.cs).
            case "Td":
                OpTextMoveTo(operands);
                break;
            case "TD":
                OpTextMoveToSetLeading(operands);
                break;
            case "Tm":
                OpSetTextMatrix(operands);
                break;
            case "T*":
                RequireOperandCount(operands, "T*", 0);
                OpTextNextLine();
                break;

            // Text-showing operators (PdfDocument.Text.cs).
            case "Tj":
                OpShowText(operands);
                break;
            case "'":
                OpShowTextNextLine(operands);
                break;
            case "\"":
                OpShowTextNextLineWithSpacing(operands);
                break;
            case "TJ":
                OpShowTextArray(operands);
                break;

            // Type 3 glyph-description operators (PdfDocument.Fonts.Type3.cs) - parsed/validated
            // for operand-count/type only; never consulted for layout (see OpType3SetWidth's own
            // remarks).
            case "d0":
                OpType3SetWidth(operands);
                break;
            case "d1":
                OpType3SetWidthAndBBox(operands);
                break;

            default:
                // Any other keyword (gs, BI/ID/EI, Tc/Td/.../TJ's own undefined siblings, or any
                // other undefined keyword) is silently skipped - out of this phase's scope
                // (ExtGState, inline images) per this phase's documented lenient-consumer posture
                // toward unrecognized operators. W/W* (clipping) and sh (shading) are handled
                // above, not skipped here.
                break;
        }
    }

    /// <summary>
    ///     Resolves a leaf page node's <c>/Contents</c> entry to fully decoded content-stream
    ///     bytes, whether declared as a single stream or an array of streams (concatenated with a
    ///     single space byte between each, per the PDF specification's own requirement that a
    ///     multi-stream <c>/Contents</c> array never allow adjacent tokens from different streams
    ///     to merge).
    /// </summary>
    /// <param name="pageNode">The already-resolved leaf page dictionary.</param>
    /// <returns>
    ///     The concatenated, fully decoded content bytes, or an empty array when the page
    ///     declares no <c>/Contents</c> at all (a page with no content is valid: nothing is
    ///     painted over whatever background color <c>Render</c> clears the surface to).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Contents</c> is neither a stream nor an array of streams, or when an
    ///     array entry does not resolve to a stream.
    /// </exception>
    private byte[] ResolvePageContentBytes(PdfObject pageNode)
    {
        var contents = pageNode.Get("Contents");
        if (contents is null)
        {
            return [];
        }

        var resolved = Resolve(contents);
        if (resolved.Kind == PdfKind.Stream)
        {
            return GetStreamDecodedBytes(resolved);
        }

        if (resolved.Kind == PdfKind.Array)
        {
            var parts = new List<byte>();
            foreach (var item in resolved.Items)
            {
                var streamObject = Resolve(item);
                if (streamObject.Kind != PdfKind.Stream)
                {
                    throw new InvalidDataException("/Contents array entry does not resolve to a stream.");
                }

                if (parts.Count > 0)
                {
                    parts.Add((byte)' ');
                }

                parts.AddRange(GetStreamDecodedBytes(streamObject));
            }

            return [.. parts];
        }

        throw new InvalidDataException("/Contents must be a stream or an array of streams.");
    }
}
