// cspell:ignore Trm Trise
using System.Numerics;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The current text matrix, mapping text-space points (as produced by the text-rendering
    ///     matrix composition - see <see cref="ComputeTextRenderingMatrix"/>) into PDF user space.
    /// </summary>
    /// <remarks>
    ///     Per the PDF specification (Table 105), <see cref="_textMatrix"/>/<see cref="_lineMatrix"/>
    ///     are <em>not</em> part of the graphics state at all: they are reset to the identity
    ///     matrix only by <c>BT</c> (see <see cref="OpBeginText"/>), never saved/restored by
    ///     <c>q</c>/<c>Q</c>, and never touched by <c>ET</c> - a documented, deliberate distinction
    ///     from every field <see cref="GraphicsState"/> itself owns (<see cref="GraphicsState.Font"/>,
    ///     <see cref="GraphicsState.FontSize"/>, <see cref="GraphicsState.CharSpacing"/>,
    ///     <see cref="GraphicsState.WordSpacing"/>, <see cref="GraphicsState.HorizontalScaling"/>,
    ///     <see cref="GraphicsState.Leading"/>, <see cref="GraphicsState.RenderMode"/>, and
    ///     <see cref="GraphicsState.TextRise"/>, all of which the specification explicitly
    ///     <em>does</em> include in the graphics state, and therefore persist across <c>BT</c>/
    ///     <c>ET</c> and are properly saved/restored by <c>q</c>/<c>Q</c>). This is, by a wide
    ///     margin, the single most common correctness bug in a from-scratch PDF text-rendering
    ///     implementation, so it is called out explicitly here rather than left implicit.
    /// </remarks>
    private Matrix3x2 _textMatrix;

    /// <summary>
    ///     The current line matrix: the text matrix's value at the start of the current line,
    ///     updated by <c>Td</c>/<c>TD</c>/<c>Tm</c>/<c>T*</c> - see <see cref="_textMatrix"/>'s own
    ///     remarks for why this field, too, lives outside <see cref="GraphicsState"/>.
    /// </summary>
    private Matrix3x2 _lineMatrix;

    /// <summary>
    ///     Handles the <c>BT</c> operator: resets <see cref="_textMatrix"/>/<see cref="_lineMatrix"/>
    ///     to the identity matrix. Every other text-state parameter (<see cref="GraphicsState.Font"/>,
    ///     etc.) is part of the graphics state and is therefore left untouched - see
    ///     <see cref="_textMatrix"/>'s remarks.
    /// </summary>
    private void OpBeginText()
    {
        _textMatrix = Matrix3x2.Identity;
        _lineMatrix = Matrix3x2.Identity;
    }

    /// <summary>
    ///     Handles the <c>ET</c> operator. Per the PDF specification, ending a text object has no
    ///     effect on any persisted state (every text-state graphics-state parameter survives
    ///     unchanged into the next <c>BT</c>, and <see cref="_textMatrix"/>/<see cref="_lineMatrix"/>
    ///     are simply left stale and unused until the next <c>BT</c> resets them) - this handler
    ///     exists only so <c>ET</c> is recognized and dispatched (with its own operand-count
    ///     validation) rather than silently ignored as an unrecognized keyword.
    /// </summary>
    private static void OpEndText()
    {
        // Intentionally a no-op - see remarks.
    }

    /// <summary>Handles the <c>Tc</c> operator: sets the character spacing, in unscaled text-space units.</summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="operands"/> does not contain exactly 1 number.</exception>
    private void OpSetCharSpacing(IReadOnlyList<PdfObject> operands) =>
        _gs.CharSpacing = RequireNumbers(operands, "Tc", 1)[0];

    /// <summary>Handles the <c>Tw</c> operator: sets the word spacing, in unscaled text-space units.</summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="operands"/> does not contain exactly 1 number.</exception>
    private void OpSetWordSpacing(IReadOnlyList<PdfObject> operands) =>
        _gs.WordSpacing = RequireNumbers(operands, "Tw", 1)[0];

    /// <summary>Handles the <c>Tz</c> operator: sets the horizontal scaling, as a percentage (the PDF specification's default is <c>100</c>).</summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="operands"/> does not contain exactly 1 number.</exception>
    private void OpSetHorizontalScaling(IReadOnlyList<PdfObject> operands) =>
        _gs.HorizontalScaling = RequireNumbers(operands, "Tz", 1)[0];

    /// <summary>Handles the <c>TL</c> operator: sets the leading, in unscaled text-space units.</summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="operands"/> does not contain exactly 1 number.</exception>
    private void OpSetLeading(IReadOnlyList<PdfObject> operands) =>
        _gs.Leading = RequireNumbers(operands, "TL", 1)[0];

    /// <summary>Handles the <c>Ts</c> operator: sets the text rise, in unscaled text-space units.</summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="operands"/> does not contain exactly 1 number.</exception>
    private void OpSetTextRise(IReadOnlyList<PdfObject> operands) =>
        _gs.TextRise = RequireNumbers(operands, "Ts", 1)[0];

    /// <summary>
    ///     Handles the <c>/name size Tf</c> operator: resolves and selects the named simple
    ///     TrueType font (via <see cref="ResolveFont"/>) at the given size.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly a name followed by a number, or
    ///     when the font name is undefined (propagated from <see cref="ResolveFont"/>).
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Propagated from <see cref="ResolveFont"/>/<see cref="BuildResolvedFont"/> for an
    ///     unsupported font.
    /// </exception>
    private void OpSetFont(IReadOnlyList<PdfObject> operands)
    {
        if (operands.Count != 2 || operands[0].Kind != PdfKind.Name || operands[1].Kind != PdfKind.Number)
        {
            throw new InvalidDataException(
                $"Operator 'Tf' requires exactly 2 operands (a name, then a number); got {operands.Count}.");
        }

        _gs.Font = ResolveFont(operands[0].Text);
        _gs.FontSize = operands[1].Number;
    }

    /// <summary>
    ///     Handles the <c>Tr</c> operator: sets the text-rendering mode. Modes <c>0</c> (fill, the
    ///     PDF specification's default), <c>1</c> (stroke), <c>2</c> (fill, then stroke), and
    ///     <c>3</c> (invisible - glyphs are laid out and advance the text position, but nothing is
    ///     painted) are all supported; the four clipping variants (<c>4</c>-<c>7</c>, which add a
    ///     glyph outline to the clipping path alongside modes <c>0</c>-<c>3</c>'s own fill/stroke/
    ///     invisible behavior) fail closed rather than being silently treated as their non-clipping
    ///     counterpart.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 1 number, or when that
    ///     number is not one of the PDF specification's seven defined render modes (<c>0</c>-<c>7</c>).
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the mode is a defined but unsupported clipping mode (<c>4</c>-<c>7</c>).
    /// </exception>
    private void OpSetTextRenderMode(IReadOnlyList<PdfObject> operands)
    {
        var mode = (int)RequireNumbers(operands, "Tr", 1)[0];
        switch (mode)
        {
            case 0:
            case 1:
            case 2:
            case 3:
                _gs.RenderMode = mode;
                break;

            case 4 or 5 or 6 or 7:
                throw new UnsupportedImageFeatureException(
                    $"pdf-text-render-mode-{mode}",
                    $"Text-rendering mode {mode} is not supported; only modes 0 (fill), 1 (stroke), " +
                    "2 (fill+stroke), and 3 (invisible) are supported. Clipping render modes are not " +
                    "implemented.");

            default:
                throw new InvalidDataException(
                    $"Operator 'Tr' requires a render mode in [0, 7]; got {mode}.");
        }
    }

    /// <summary>Handles the <c>Td tx ty</c> operator: moves to the start of the next line, offset by <c>(tx, ty)</c> from the start of the current line.</summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="operands"/> does not contain exactly 2 numbers.</exception>
    private void OpTextMoveTo(IReadOnlyList<PdfObject> operands)
    {
        var values = RequireNumbers(operands, "Td", 2);
        MoveToNextLine(values[0], values[1]);
    }

    /// <summary>Handles the <c>TD tx ty</c> operator: identical to <c>Td</c>, but also sets the leading to <c>-ty</c>.</summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="operands"/> does not contain exactly 2 numbers.</exception>
    private void OpTextMoveToSetLeading(IReadOnlyList<PdfObject> operands)
    {
        var values = RequireNumbers(operands, "TD", 2);
        _gs.Leading = -values[1];
        MoveToNextLine(values[0], values[1]);
    }

    /// <summary>Shared core of <c>Td</c>/<c>TD</c>: offsets the line matrix by <c>(tx, ty)</c> and makes the text matrix match it.</summary>
    private void MoveToNextLine(double tx, double ty)
    {
        var translation = Matrix3x2.CreateTranslation((float)tx, (float)ty);
        _lineMatrix = translation * _lineMatrix;
        _textMatrix = _lineMatrix;
    }

    /// <summary>Handles the <c>a b c d e f Tm</c> operator: replaces (does not compose with) both the text matrix and the line matrix.</summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="operands"/> does not contain exactly 6 numbers.</exception>
    private void OpSetTextMatrix(IReadOnlyList<PdfObject> operands)
    {
        var values = RequireNumbers(operands, "Tm", 6);
        var matrix = new Matrix3x2(
            (float)values[0], (float)values[1], (float)values[2], (float)values[3], (float)values[4], (float)values[5]);
        _textMatrix = matrix;
        _lineMatrix = matrix;
    }

    /// <summary>Handles the <c>T*</c> operator: equivalent to <c>0 -TL Td</c>, moving to the start of the next line using the current leading.</summary>
    private void OpTextNextLine() => MoveToNextLine(0, -_gs.Leading);

    /// <summary>Handles the <c>string Tj</c> operator: shows the given string using the current text state.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 string operand, or when no
    ///     font has been selected via <c>Tf</c> (propagated from <see cref="ShowText"/>).
    /// </exception>
    private void OpShowText(IReadOnlyList<PdfObject> operands)
    {
        RequireOperandCount(operands, "Tj", 1);
        if (operands[0].Kind != PdfKind.String)
        {
            throw new InvalidDataException("Operator 'Tj' requires a string operand.");
        }

        ShowText(operands[0].Bytes);
    }

    /// <summary>Handles the <c>string '</c> operator: equivalent to <c>T* string Tj</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 string operand, or when no
    ///     font has been selected via <c>Tf</c> (propagated from <see cref="ShowText"/>).
    /// </exception>
    private void OpShowTextNextLine(IReadOnlyList<PdfObject> operands)
    {
        RequireOperandCount(operands, "'", 1);
        if (operands[0].Kind != PdfKind.String)
        {
            throw new InvalidDataException("Operator ''' requires a string operand.");
        }

        OpTextNextLine();
        ShowText(operands[0].Bytes);
    }

    /// <summary>Handles the <c>aw ac string "</c> operator: sets the word/character spacing, then is equivalent to <c>T* string Tj</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly a number, a number, then a
    ///     string, or when no font has been selected via <c>Tf</c> (propagated from
    ///     <see cref="ShowText"/>).
    /// </exception>
    private void OpShowTextNextLineWithSpacing(IReadOnlyList<PdfObject> operands)
    {
        if (operands.Count != 3 || operands[0].Kind != PdfKind.Number || operands[1].Kind != PdfKind.Number ||
            operands[2].Kind != PdfKind.String)
        {
            throw new InvalidDataException(
                $"Operator '\"' requires exactly 3 operands (a number, a number, then a string); got {operands.Count}.");
        }

        _gs.WordSpacing = operands[0].Number;
        _gs.CharSpacing = operands[1].Number;
        OpTextNextLine();
        ShowText(operands[2].Bytes);
    }

    /// <summary>
    ///     Handles the <c>array TJ</c> operator: shows each string element using the current text
    ///     state, and pre-advances the text position for each numeric element (a position
    ///     adjustment, in thousandths of text-space units, subtracted from the pen position -
    ///     applied without any accompanying glyph).
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 array operand, when an array
    ///     entry is neither a string nor a number, or when no font has been selected via
    ///     <c>Tf</c> (propagated from <see cref="ShowText"/>).
    /// </exception>
    private void OpShowTextArray(IReadOnlyList<PdfObject> operands)
    {
        RequireOperandCount(operands, "TJ", 1);
        if (operands[0].Kind != PdfKind.Array)
        {
            throw new InvalidDataException("Operator 'TJ' requires an array operand.");
        }

        foreach (var item in operands[0].Items)
        {
            switch (item.Kind)
            {
                case PdfKind.String:
                    ShowText(item.Bytes);
                    break;

                case PdfKind.Number:
                    ApplyTextSpaceAdjustment(item.Number);
                    break;

                default:
                    throw new InvalidDataException("Operator 'TJ' array entries must be strings or numbers.");
            }
        }
    }

    /// <summary>
    ///     Applies a <c>TJ</c> array's numeric position adjustment: a positive value moves the
    ///     next glyph closer to the previous one (subtracted from the pen advance), per the PDF
    ///     specification's own documented sign convention.
    /// </summary>
    private void ApplyTextSpaceAdjustment(double adjustmentInThousandths)
    {
        var horizontalScale = _gs.HorizontalScaling / 100.0;
        var displacement = -(adjustmentInThousandths / 1000.0) * _gs.FontSize * horizontalScale;
        _textMatrix = Matrix3x2.CreateTranslation((float)displacement, 0f) * _textMatrix;
    }

    /// <summary>
    ///     Shared core of every text-showing operator (<c>Tj</c>/<c>'</c>/<c>"</c>/<c>TJ</c>):
    ///     decodes <paramref name="bytes"/> into character codes using the selected font's
    ///     <see cref="IResolvedFont.CodeByteWidth"/> (1 byte per code for a simple font, 2
    ///     bytes - big-endian - per code for a composite <c>/Identity-H</c> font), showing each
    ///     decoded code in turn via <see cref="ShowGlyph"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when no font has been selected yet via <c>Tf</c> (<see cref="GraphicsState.Font"/>
    ///     is <see langword="null"/>), or when a composite font's string byte length is not a
    ///     multiple of 2.
    /// </exception>
    private void ShowText(byte[] bytes)
    {
        var font = _gs.Font ?? throw new InvalidDataException(
            "A text-showing operator was used before a font was selected via 'Tf'.");

        if (font.CodeByteWidth == 1)
        {
            foreach (var code in bytes)
            {
                ShowGlyph(font, code);
            }

            return;
        }

        if (bytes.Length % 2 != 0)
        {
            throw new InvalidDataException(
                "A Type0 (Identity-H) string's byte length must be a multiple of 2.");
        }

        for (var i = 0; i < bytes.Length; i += 2)
        {
            var code = (bytes[i] << 8) | bytes[i + 1];
            ShowGlyph(font, code);
        }
    }

    /// <summary>
    ///     Lays out and (unless the current render mode is invisible) paints a single glyph for
    ///     character code <paramref name="code"/>, then advances <see cref="_textMatrix"/> by the
    ///     glyph's displacement (per PDF specification section 9.3.3, word spacing is only added
    ///     for the single-byte code <c>32</c> of a simple font - never for any code decoded from
    ///     a composite font, even one that numerically equals <c>32</c>, since word spacing
    ///     "shall not apply to occurrences of the byte value 32 in multiple-byte codes"). Branches
    ///     early, before using <see cref="IResolvedFont.Resolve"/>'s resolved font, on whether
    ///     <paramref name="font"/> is a <c>ResolvedType3Font</c> (painted via
    ///     <c>PaintType3Glyph</c>'s own content-stream re-entrance - see
    ///     <c>PdfDocument.Fonts.Type3.cs</c>; a Type 3 glyph procedure has no outline, so it has no
    ///     fill/stroke distinction and paints identically for render modes <c>0</c>-<c>2</c>) or any
    ///     other concrete implementation (painted via the outline-based path below, mode-aware
    ///     since the addition of <c>Tr</c> modes <c>1</c>/<c>2</c>: fill for modes <c>0</c>/<c>2</c>,
    ///     then stroke - via the same <see cref="PaintStroke"/> helper shared with the
    ///     path-painting operators - for modes <c>1</c>/<c>2</c>) - the shared trailing
    ///     displacement/advance logic runs unconditionally either way.
    /// </summary>
    private void ShowGlyph(IResolvedFont font, int code)
    {
        var (ttf, glyphIndex, w0) = font.Resolve(code);

        if (font is ResolvedType3Font type3Font)
        {
            // A Type 3 font has no outline-glyph font program at all - its glyphs are content-
            // stream procedures, painted via PaintType3Glyph's own Form-XObject-style re-entrant
            // execution, never via the font Resolve returns (which is null for a
            // ResolvedType3Font - see IResolvedFont.Resolve's own remarks). Render-mode-3
            // (invisible) skips glyph-procedure execution entirely, exactly like the non-Type3
            // branch below skips GetGlyphOutline/fill entirely in that mode.
            if (_gs.RenderMode != 3)
            {
                PaintType3Glyph(type3Font, code);
            }
        }
        else if (_gs.RenderMode != 3)
        {
            // Every non-Type3 IResolvedFont implementation's Resolve returns a non-null font -
            // see ResolvedSimpleFont/ResolvedCompositeFont - so this null-forgiving use is safe.
            var resolvedTtf = ttf!;
            var outline = resolvedTtf.GetGlyphOutline(glyphIndex);
            if (outline.Subpaths.Count > 0)
            {
                var trm = ComputeTextRenderingMatrix();
                var glyphMatrix = Matrix3x2.CreateScale(1f / resolvedTtf.UnitsPerEm) * trm;
                var builder = new PathBuilder();
                AppendTransformedGlyphOutline(builder, outline, glyphMatrix);
                var path = builder.Build();

                if (_gs.RenderMode is 0 or 2)
                {
                    // Known pre-existing simplification, out of scope for this change: unlike
                    // the glyph-stroke step below (which does mirror a Pattern stroke color
                    // space, via the shared PaintStroke helper), this glyph fill always uses the
                    // flat fill color and does not mirror a Pattern fill color space the way the
                    // ordinary path-painting operators do.
                    PathFiller.Fill(_surface, path, _gs.FillColor, FillRule.NonZero);
                }

                if (_gs.RenderMode is 1 or 2)
                {
                    PaintStroke(path);
                }
            }
        }

        var horizontalScale = _gs.HorizontalScaling / 100.0;
        var applyWordSpacing = font.CodeByteWidth == 1 && code == 32;
        var displacement = ((w0 * _gs.FontSize) + _gs.CharSpacing + (applyWordSpacing ? _gs.WordSpacing : 0)) * horizontalScale;
        _textMatrix = Matrix3x2.CreateTranslation((float)displacement, 0f) * _textMatrix;
    }

    /// <summary>
    ///     Computes the text-rendering matrix (<c>Trm</c>), mapping text-space points (the space
    ///     glyph outlines are placed in after their own <c>1/UnitsPerEm</c> scale) to device
    ///     pixel-space points, per PDF specification section 9.4.4:
    ///     <c>Trm = [Tfs*Th 0 0; 0 Tfs 0; 0 Trise 1] x Tm x CTM</c>.
    /// </summary>
    /// <remarks>
    ///     Composed via the same premultiply convention <see cref="OpConcatMatrix"/> already
    ///     established for <c>cm</c> (<c>matrix * outerTransform</c>: the left operand is applied
    ///     first/innermost). Unlike <c>SvgCodec.Text.cs</c>'s glyph-placement matrix (which flips
    ///     the font's Y-up design units into that codec's own Y-down pixel-space convention as
    ///     part of the same step), no Y-flip is applied here: PDF user space is already Y-up (per
    ///     specification section 8.3.2.3), matching the font's own Y-up glyph-space convention -
    ///     the single Y-up-to-Y-down flip already happens exactly once, in <c>BuildBaseCtm</c>'s
    ///     own base CTM, which every glyph's <c>Trm</c> is composed through via
    ///     <see cref="GraphicsState.CurrentTransform"/>. This is a documented, deliberate,
    ///     pixel-level-tested difference from the SVG codec's own text-rendering matrix, not an
    ///     oversight.
    /// </remarks>
    private Matrix3x2 ComputeTextRenderingMatrix()
    {
        var horizontalScale = _gs.HorizontalScaling / 100.0;
        var parameterMatrix = new Matrix3x2(
            (float)(_gs.FontSize * horizontalScale), 0f,
            0f, (float)_gs.FontSize,
            0f, (float)_gs.TextRise);
        return parameterMatrix * _textMatrix * _gs.CurrentTransform;
    }

    /// <summary>
    ///     Re-issues a glyph outline's subpaths/commands into <paramref name="builder"/>, mapping
    ///     every point through <paramref name="transform"/> - the same "re-issue through a fresh
    ///     <see cref="PathBuilder"/>" pattern <c>SvgCodec.PathRender.cs</c>'s own
    ///     <c>AppendTransformedPathInto</c> uses (this package cannot call that method directly,
    ///     since it is private to the <c>DemaConsulting.CanvasNet.Svg</c> assembly).
    /// </summary>
    /// <exception cref="NotSupportedException">
    ///     Thrown for a <see cref="PathCommandType.ArcTo"/> command - <see cref="Fonts.TrueTypeFont.GetGlyphOutline"/>
    ///     never produces one (TrueType glyph outlines are built only from lines and quadratic
    ///     Bezier curves), so encountering one indicates an internal defect, not a data-driven
    ///     condition.
    /// </exception>
    private static void AppendTransformedGlyphOutline(PathBuilder builder, Path outline, Matrix3x2 transform)
    {
        foreach (var subpath in outline.Subpaths)
        {
            builder.MoveTo(Vector2.Transform(subpath.Start, transform));
            foreach (var command in subpath.Commands)
            {
                switch (command.Type)
                {
                    case PathCommandType.LineTo:
                        builder.LineTo(Vector2.Transform(command.EndPoint, transform));
                        break;

                    case PathCommandType.QuadraticBezierTo:
                        builder.QuadraticBezierTo(
                            Vector2.Transform(command.Control1, transform),
                            Vector2.Transform(command.EndPoint, transform));
                        break;

                    case PathCommandType.CubicBezierTo:
                        builder.CubicBezierTo(
                            Vector2.Transform(command.Control1, transform),
                            Vector2.Transform(command.Control2, transform),
                            Vector2.Transform(command.EndPoint, transform));
                        break;

                    case PathCommandType.Close:
                        builder.Close();
                        break;

                    default:
                        throw new NotSupportedException(
                            $"Unsupported path command type '{command.Type}' encountered while transforming a glyph outline.");
                }
            }
        }
    }
}
