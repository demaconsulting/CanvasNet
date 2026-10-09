using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     A snapshot of every content-stream graphics parameter the <c>q</c>/<c>Q</c> operators
    ///     push and pop as a unit, and the <c>cm</c>/<c>w</c>/<c>J</c>/<c>j</c>/<c>M</c>/<c>d</c>
    ///     operators mutate on the currently active instance.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Every field's initial value is the PDF specification's own documented default for
    ///         a freshly started content stream, <em>not</em> necessarily the corresponding
    ///         <see cref="Drawing.StrokeStyle"/> constructor default (most notably
    ///         <see cref="MiterLimit"/>: the PDF default is <c>10</c>, while
    ///         <see cref="Drawing.StrokeStyle"/>'s own C# default is <c>4</c>) - every
    ///         <see cref="Drawing.StrokeStyle"/> constructed while painting a stroke must pass
    ///         every one of these fields explicitly, never relying on
    ///         <see cref="Drawing.StrokeStyle"/>'s own defaults.
    ///     </para>
    ///     <para>
    ///         Phase 3 adds real device color state: <see cref="FillColor"/>/<see cref="StrokeColor"/>
    ///         (the actual painted color, updated by <c>g</c>/<c>rg</c>/<c>k</c>/<c>sc</c>/<c>scn</c>
    ///         and their stroking counterparts) and <see cref="FillColorSpace"/>/
    ///         <see cref="StrokeColorSpace"/> (which device color space <c>sc</c>/<c>scn</c>'s
    ///         operand count is currently interpreted against). Every field's default matches the
    ///         PDF specification's own documented initial state for a freshly started content
    ///         stream: <c>DeviceGray</c> at value <c>0</c>, i.e. opaque black - identical to Phase
    ///         2's fixed-black behavior, so no Phase 2 test regresses.
    ///     </para>
    /// </remarks>
    private sealed class GraphicsState
    {
        /// <summary>
        ///     Gets or sets the current transformation matrix, mapping PDF user-space points
        ///     (as they appear in path-construction operands) to device pixel-space points.
        /// </summary>
        internal Matrix3x2 CurrentTransform { get; set; }

        /// <summary>
        ///     Gets or sets the line width, in user-space units (not yet scaled by the CTM). The
        ///     PDF specification's default is <c>1</c>.
        /// </summary>
        internal float LineWidth { get; set; } = 1f;

        /// <summary>
        ///     Gets or sets the line cap style. The PDF specification's default is
        ///     <see cref="Drawing.LineCap.Butt"/> (numeric value <c>0</c>).
        /// </summary>
        internal LineCap LineCap { get; set; } = LineCap.Butt;

        /// <summary>
        ///     Gets or sets the line join style. The PDF specification's default is
        ///     <see cref="Drawing.LineJoin.Miter"/> (numeric value <c>0</c>).
        /// </summary>
        internal LineJoin LineJoin { get; set; } = LineJoin.Miter;

        /// <summary>
        ///     Gets or sets the miter limit. The PDF specification's default is <c>10</c> -
        ///     distinct from <see cref="Drawing.StrokeStyle"/>'s own C# default of <c>4</c>.
        /// </summary>
        internal float MiterLimit { get; set; } = 10f;

        /// <summary>
        ///     Gets or sets the dash pattern's on/off lengths, in user-space units (not yet
        ///     scaled by the CTM), or <see langword="null"/> for a solid stroke (the PDF
        ///     specification's default).
        /// </summary>
        internal IReadOnlyList<float>? DashArray { get; set; }

        /// <summary>
        ///     Gets or sets the dash pattern's phase, in user-space units (not yet scaled by the
        ///     CTM). The PDF specification's default is <c>0</c>.
        /// </summary>
        internal float DashPhase { get; set; }

        /// <summary>
        ///     Gets or sets the current fill color. The PDF specification's default is opaque
        ///     black (<c>DeviceGray</c> value <c>0</c>).
        /// </summary>
        internal Rgba32 FillColor { get; set; } = new(0, 0, 0, 255);

        /// <summary>
        ///     Gets or sets the current stroke color. The PDF specification's default is opaque
        ///     black (<c>DeviceGray</c> value <c>0</c>).
        /// </summary>
        internal Rgba32 StrokeColor { get; set; } = new(0, 0, 0, 255);

        /// <summary>
        ///     Gets or sets the current fill color space, used to determine how many numeric
        ///     operands <c>sc</c>/<c>scn</c> requires. The PDF specification's default is
        ///     <c>DeviceGray</c>.
        /// </summary>
        internal PdfColorSpace FillColorSpace { get; set; } = PdfColorSpace.DeviceGray;

        /// <summary>
        ///     Gets or sets the current stroke color space, used to determine how many numeric
        ///     operands <c>SC</c>/<c>SCN</c> requires. The PDF specification's default is
        ///     <c>DeviceGray</c>.
        /// </summary>
        internal PdfColorSpace StrokeColorSpace { get; set; } = PdfColorSpace.DeviceGray;

        /// <summary>
        ///     Gets or sets the currently selected fill pattern, resolved by the most recent
        ///     <c>scn</c> operator against a <c>/Pattern</c> <see cref="FillColorSpace"/>, or
        ///     <see langword="null"/> when <see cref="FillColorSpace"/>'s <see cref="PdfColorSpace.Kind"/>
        ///     is not <see cref="PdfColorSpace.Family.Pattern"/> (set back to <see langword="null"/>
        ///     by <c>cs</c>/<c>g</c>/<c>rg</c>/<c>k</c> and their fill counterparts).
        /// </summary>
        internal ResolvedPattern? FillPattern { get; set; }

        /// <summary>
        ///     Gets or sets the currently selected stroke pattern - see <see cref="FillPattern"/>'s
        ///     remarks, which apply identically here against <see cref="StrokeColorSpace"/>.
        /// </summary>
        internal ResolvedPattern? StrokePattern { get; set; }

        /// <summary>
        ///     Gets or sets the currently selected font, resolved by the most recent <c>Tf</c>
        ///     operator, or <see langword="null"/> when no <c>Tf</c> has been issued yet (in
        ///     which case a text-showing operator fails closed - see
        ///     <see cref="PdfDocument.ShowText"/>). Phase 4 adds this and every other field below;
        ///     every one of them is, per the PDF specification, part of the graphics state
        ///     proper (saved/restored by <c>q</c>/<c>Q</c>, and persisting across <c>BT</c>/
        ///     <c>ET</c>) - a deliberate, documented distinction from
        ///     <see cref="PdfDocument._textMatrix"/>/<see cref="PdfDocument._lineMatrix"/>, which
        ///     are reset by <c>BT</c> and live outside this class entirely (see that field's own
        ///     remarks).
        /// </summary>
        internal IResolvedFont? Font { get; set; }

        /// <summary>Gets or sets the current font size, in unscaled text-space units. The PDF specification defines no default (a <c>Tf</c> is required before text is shown).</summary>
        internal double FontSize { get; set; }

        /// <summary>Gets or sets the character spacing, in unscaled text-space units. The PDF specification's default is <c>0</c>.</summary>
        internal double CharSpacing { get; set; }

        /// <summary>Gets or sets the word spacing, in unscaled text-space units. The PDF specification's default is <c>0</c>.</summary>
        internal double WordSpacing { get; set; }

        /// <summary>Gets or sets the horizontal scaling, as a percentage. The PDF specification's default is <c>100</c>.</summary>
        internal double HorizontalScaling { get; set; } = 100;

        /// <summary>Gets or sets the leading, in unscaled text-space units. The PDF specification's default is <c>0</c>.</summary>
        internal double Leading { get; set; }

        /// <summary>
        ///     Gets or sets the text-rendering mode. The PDF specification's default is <c>0</c>
        ///     (fill) - see <see cref="PdfDocument.OpSetTextRenderMode"/> for which modes this
        ///     phase actually supports.
        /// </summary>
        internal int RenderMode { get; set; }

        /// <summary>Gets or sets the text rise, in unscaled text-space units. The PDF specification's default is <c>0</c>.</summary>
        internal double TextRise { get; set; }

        /// <summary>
        ///     Gets or sets the current clipping path's antialiased coverage mask (PDF 32000-1
        ///     &#xA7;8.5.4), or <see langword="null"/> when no clipping path has been set yet (the
        ///     PDF specification's default: "the entire output device"). Set by
        ///     <see cref="PdfDocument.PaintCurrentPath"/> once a path-painting operator executes
        ///     after a pending <c>W</c>/<c>W*</c> (see <see cref="PdfDocument._pendingClipFillRule"/>'s
        ///     remarks for why the clip is applied only then, not immediately on <c>W</c>/<c>W*</c>
        ///     themselves) - never set directly by <c>W</c>/<c>W*</c>. A newly computed clip is
        ///     always intersected with this field's previous value via <see cref="ClipMask.Intersect"/>,
        ///     never a wholesale replacement, per PDF 32000-1 &#xA7;8.5.4: "the new clipping path
        ///     ... shall be the intersection of the current clipping path and the newly
        ///     constructed path".
        /// </summary>
        internal ClipMask? Clip { get; set; }

        /// <summary>
        ///     Produces an independent copy of this graphics state, for <c>q</c> to push onto the
        ///     graphics-state stack.
        /// </summary>
        /// <returns>A new <see cref="GraphicsState"/> instance with the same field values.</returns>
        /// <remarks>
        ///     <see cref="DashArray"/> is shared, not deep-copied: it is only ever replaced
        ///     wholesale by <c>d</c> (never mutated in place), so sharing the same
        ///     <see cref="IReadOnlyList{T}"/> reference across a clone is safe. <see cref="Font"/>
        ///     is likewise shared, not deep-copied: <see cref="IResolvedFont"/> instances are
        ///     immutable once resolved by <see cref="PdfDocument.ResolveFont"/>. <see cref="Clip"/>
        ///     is shared the same way, for the same reason: <see cref="ClipMask"/> is itself
        ///     immutable (see its own remarks), and every subsequent clip narrowing produces a
        ///     brand-new instance via <see cref="ClipMask.Intersect"/> rather than mutating the
        ///     shared one - which is exactly what gives a clip set inside a <c>q</c>/<c>Q</c>
        ///     block its correct save/restore scoping (PDF 32000-1 &#xA7;8.4.2) for free, with no
        ///     special-case logic in <see cref="PdfDocument.OpPushGraphicsState"/>/
        ///     <see cref="PdfDocument.OpPopGraphicsState"/> beyond this field simply being part of
        ///     the cloned snapshot like every other graphics-state parameter.
        /// </remarks>
        internal GraphicsState Clone() => new()
        {
            CurrentTransform = CurrentTransform,
            LineWidth = LineWidth,
            LineCap = LineCap,
            LineJoin = LineJoin,
            MiterLimit = MiterLimit,
            DashArray = DashArray,
            DashPhase = DashPhase,
            FillColor = FillColor,
            StrokeColor = StrokeColor,
            FillColorSpace = FillColorSpace,
            StrokeColorSpace = StrokeColorSpace,
            FillPattern = FillPattern,
            StrokePattern = StrokePattern,
            Font = Font,
            FontSize = FontSize,
            CharSpacing = CharSpacing,
            WordSpacing = WordSpacing,
            HorizontalScaling = HorizontalScaling,
            Leading = Leading,
            RenderMode = RenderMode,
            TextRise = TextRise,
            Clip = Clip,
        };
    }

    /// <summary>
    ///     The graphics-state stack pushed/popped by <c>q</c>/<c>Q</c>, for the content stream
    ///     currently being executed by <see cref="ExecuteContentStream"/>. Reinitialized fresh at
    ///     the start of every <see cref="Render(int, int, int, PdfRenderOptions?)"/> call; never shared or reused
    ///     across calls.
    /// </summary>
    private Stack<GraphicsState> _gsStack = null!;

    /// <summary>
    ///     The currently active graphics state, for the content stream currently being executed
    ///     by <see cref="ExecuteContentStream"/>.
    /// </summary>
    private GraphicsState _gs = null!;

    /// <summary>
    ///     The maximum number of entries <see cref="OpPushGraphicsState"/> (<c>q</c>) allows the
    ///     graphics-state stack to grow to before failing closed, bounding a crafted content
    ///     stream that issues an enormous run of unmatched <c>q</c> operators (each pushing a
    ///     cloned <see cref="GraphicsState"/> instance) from exhausting memory. 256 is generous
    ///     for any legitimate content stream's save/restore nesting, matching the
    ///     <c>Max*NestingDepth</c> naming/exception-message convention used for Form XObject,
    ///     Type3 glyph, and page-tree recursion elsewhere in this class.
    /// </summary>
    private const int MaxGraphicsStateStackDepth = 256;

    /// <summary>
    ///     Handles the <c>q</c> operator: pushes a copy of the current graphics state, so a
    ///     matching <c>Q</c> can later restore exactly what was active before this <c>q</c>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the graphics-state stack already holds <see cref="MaxGraphicsStateStackDepth"/>
    ///     entries.
    /// </exception>
    private void OpPushGraphicsState()
    {
        if (_gsStack.Count >= MaxGraphicsStateStackDepth)
        {
            throw new InvalidDataException(
                $"Graphics state stack depth exceeds the maximum supported depth of {MaxGraphicsStateStackDepth}.");
        }

        _gsStack.Push(_gs.Clone());
    }

    /// <summary>
    ///     Handles the <c>Q</c> operator: pops the most recently pushed graphics state and makes
    ///     it current again.
    /// </summary>
    /// <remarks>
    ///     A bare <c>Q</c> with no matching <c>q</c> (an empty stack) is tolerated as a no-op,
    ///     not an error - this is a deliberate leniency decision (distinct from the strict
    ///     "malformed operand count/type" throw cases mandated for every implemented operator),
    ///     matching this phase's general lenient-consumer posture toward structurally unusual but
    ///     not operand-malformed input.
    /// </remarks>
    private void OpPopGraphicsState()
    {
        if (_gsStack.Count > 0)
        {
            _gs = _gsStack.Pop();
        }
    }

    /// <summary>
    ///     Handles the <c>cm a b c d e f</c> operator: composes a new matrix onto the current
    ///     transformation matrix by premultiplication (the new matrix is applied first, the
    ///     previously current transform is applied second/outermost).
    /// </summary>
    /// <param name="operands">The operator's accumulated operand stack. Must contain exactly 6 numbers.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 6 numbers.
    /// </exception>
    private void OpConcatMatrix(IReadOnlyList<PdfObject> operands)
    {
        var values = RequireNumbers(operands, "cm", 6);
        var matrix = new Matrix3x2(
            (float)values[0],
            (float)values[1],
            (float)values[2],
            (float)values[3],
            (float)values[4],
            (float)values[5]);
        _gs.CurrentTransform = matrix * _gs.CurrentTransform;
    }

    /// <summary>Handles the <c>w</c> operator: sets the current line width (raw user-space units).</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 1 number.
    /// </exception>
    private void OpSetLineWidth(IReadOnlyList<PdfObject> operands) =>
        _gs.LineWidth = (float)RequireNumbers(operands, "w", 1)[0];

    /// <summary>Handles the <c>J</c> operator: sets the current line cap style.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 1 number, or when
    ///     that number is not <c>0</c>, <c>1</c>, or <c>2</c>.
    /// </exception>
    private void OpSetLineCap(IReadOnlyList<PdfObject> operands)
    {
        var code = (int)RequireNumbers(operands, "J", 1)[0];
        _gs.LineCap = code switch
        {
            0 => LineCap.Butt,
            1 => LineCap.Round,
            2 => LineCap.Square,
            _ => throw new InvalidDataException($"Operator 'J' requires a cap code of 0, 1, or 2; got {code}."),
        };
    }

    /// <summary>Handles the <c>j</c> operator: sets the current line join style.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 1 number, or when
    ///     that number is not <c>0</c>, <c>1</c>, or <c>2</c>.
    /// </exception>
    private void OpSetLineJoin(IReadOnlyList<PdfObject> operands)
    {
        var code = (int)RequireNumbers(operands, "j", 1)[0];
        _gs.LineJoin = code switch
        {
            0 => LineJoin.Miter,
            1 => LineJoin.Round,
            2 => LineJoin.Bevel,
            _ => throw new InvalidDataException($"Operator 'j' requires a join code of 0, 1, or 2; got {code}."),
        };
    }

    /// <summary>Handles the <c>M</c> operator: sets the current miter limit.</summary>
    /// <remarks>
    ///     A value below the PDF/<see cref="Drawing.StrokeStyle"/>-required minimum of <c>1</c>
    ///     is clamped to <c>1</c> rather than rejected - a deliberate, documented leniency for
    ///     producers that occasionally emit a slightly-under-1 miter limit due to floating-point
    ///     rounding, distinct from the strict "malformed operand count/type" throw cases.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 1 number.
    /// </exception>
    private void OpSetMiterLimit(IReadOnlyList<PdfObject> operands) =>
        _gs.MiterLimit = Math.Max((float)RequireNumbers(operands, "M", 1)[0], 1f);

    /// <summary>Handles the <c>d array phase</c> operator: sets the current dash pattern.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 2 operands (an array
    ///     followed by a number), when any array entry is not a finite, non-negative number, or
    ///     when the phase is not a number.
    /// </exception>
    private void OpSetDashPattern(IReadOnlyList<PdfObject> operands)
    {
        if (operands.Count != 2 || operands[0].Kind != PdfKind.Array || operands[1].Kind != PdfKind.Number)
        {
            throw new InvalidDataException(
                $"Operator 'd' requires exactly 2 operands (an array, then a number); got {operands.Count}.");
        }

        var array = operands[0];
        var dashArray = new float[array.Items.Count];
        for (var i = 0; i < array.Items.Count; i++)
        {
            var item = array.Items[i];
            if (item.Kind != PdfKind.Number || !double.IsFinite(item.Number) || item.Number < 0)
            {
                throw new InvalidDataException(
                    "Operator 'd' dash array entries must be finite numbers greater than or equal to zero.");
            }

            dashArray[i] = (float)item.Number;
        }

        // An empty array ("[] 0 d") is the documented "reset to solid" form.
        _gs.DashArray = dashArray.Length == 0 ? null : dashArray;
        _gs.DashPhase = (float)operands[1].Number;
    }

    /// <summary>
    ///     Validates that <paramref name="operands"/> contains exactly <paramref name="count"/>
    ///     entries, each of kind <see cref="PdfKind.Number"/> and finite, and returns their
    ///     numeric values in order.
    /// </summary>
    /// <param name="operands">The operator's accumulated operand stack.</param>
    /// <param name="operatorName">The operator name, for the exception message.</param>
    /// <param name="count">The exact number of numeric operands required.</param>
    /// <returns>The operands' numeric values, in order.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly <paramref name="count"/>
    ///     entries, when any entry is not of kind <see cref="PdfKind.Number"/>, or when any
    ///     entry's numeric value is not finite (<c>NaN</c> or an infinity).
    /// </exception>
    private static double[] RequireNumbers(IReadOnlyList<PdfObject> operands, string operatorName, int count)
    {
        if (operands.Count != count)
        {
            throw new InvalidDataException(
                $"Operator '{operatorName}' requires exactly {count} operand(s); got {operands.Count}.");
        }

        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            if (operands[i].Kind != PdfKind.Number)
            {
                throw new InvalidDataException($"Operator '{operatorName}' requires numeric operands.");
            }

            // Reject NaN/Infinity before any caller can use it in a transform or path
            // coordinate - a crafted content stream supplying a non-finite operand to, e.g.,
            // `cm` or a path-construction operator could otherwise propagate NaN/Infinity into
            // downstream geometry/rendering code that assumes finite input.
            if (!double.IsFinite(operands[i].Number))
            {
                throw new InvalidDataException(
                    $"Operator '{operatorName}' requires finite numeric operands; got {operands[i].Number}.");
            }

            values[i] = operands[i].Number;
        }

        return values;
    }
}
