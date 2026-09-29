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
    ///     Tokenizes and executes <paramref name="contentBytes"/> as a page content stream,
    ///     painting recognized path-construction/painting operators onto <paramref name="surface"/>
    ///     and ignoring every other operator, starting from <paramref name="baseCtm"/> as the
    ///     initial graphics state's current transformation matrix.
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
        _pathBuilder = new PathBuilder();
        _currentPoint = default;
        _subpathStart = default;
        _hasOpenSubpath = false;

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
                    operands.Add(ParseValue(tokenizer, token));
                    break;

                case PdfTokenKind.Keyword:
                    DispatchOperator(token.Text ?? string.Empty, operands);
                    operands.Clear();
                    break;

                default:
                    // A content stream never legitimately contains a bare '>>'/']'/dictionary
                    // delimiter here - this class's own convention (see PdfDocument.Xref.cs) is
                    // to throw on truly unparseable structure rather than silently continuing.
                    throw new InvalidDataException(
                        $"Unexpected token '{token.Kind}' in PDF content stream.");
            }
        }
    }

    /// <summary>
    ///     Dispatches one recognized content-stream keyword operator (per the fixed set this
    ///     phase implements) against its accumulated operand stack, silently ignoring any other
    ///     keyword (text, clipping, ExtGState, shading, inline images, and every other operator
    ///     not yet implemented).
    /// </summary>
    /// <param name="operatorName">The operator keyword.</param>
    /// <param name="operands">The operands accumulated since the previous operator.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a recognized operator's operand count/type does not match its documented
    ///     requirement.
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

            default:
                // Any other keyword (BT/ET/Tf/Tj/TJ, gs, W/W*, sh, BI/ID/EI, or any other
                // undefined keyword) is silently skipped - out of Phase 3 scope (text/fonts, Form
                // XObjects, shading/patterns, ExtGState, clipping) per this phase's documented
                // lenient-consumer posture toward unrecognized operators.
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
    ///     declares no <c>/Contents</c> at all (a page with no content is valid: a blank page).
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
