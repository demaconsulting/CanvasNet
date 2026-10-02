// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO CFF charstring charstrings subr subrs
// cspell:ignore hintmask cntrmask rmoveto hmoveto vmoveto rlineto hlineto vlineto
// cspell:ignore rrcurveto hhcurveto vvcurveto hvcurveto vhcurveto callsubr callgsubr
// cspell:ignore endchar seac hstem vstem hstemhm vstemhm rcurveline rlinecurve bchar achar adx ady
// cspell:ignore hflex flex1 hflex1
using System.Numerics;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Tests.TestSupport;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="CffCharstringInterpreter"/>.
/// </summary>
public class CffCharstringInterpreterTests
{
    /// <summary>
    ///     Decodes a single charstring built from a sequence of pre-encoded byte chunks (each
    ///     chunk written via <see cref="SyntheticFontBuilder.WriteCharstringNumber"/>/
    ///     <see cref="SyntheticFontBuilder.WriteCharstringOperator"/> by the caller) with no
    ///     global/local subroutines.
    /// </summary>
    private static Path Decode(byte[] charstring, IReadOnlyList<(int Offset, int Length)>? globalSubrs = null, IReadOnlyList<(int Offset, int Length)>? localSubrs = null) =>
        CffCharstringInterpreter.Decode(charstring, (0, charstring.Length), globalSubrs ?? [], localSubrs ?? []).Outline;

    private static byte[] Build(Action<List<byte>> write)
    {
        var buf = new List<byte>();
        write(buf);
        return [.. buf];
    }

    private static void Number(List<byte> buf, double value) => SyntheticFontBuilder.WriteCharstringNumber(buf, value);

    private static void Op(List<byte> buf, int op) => SyntheticFontBuilder.WriteCharstringOperator(buf, op);

    /// <summary>
    ///     Proves that CffCharstringInterpreter EmptyCharstring ProducesEmptyPath.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_EmptyCharstring_ProducesEmptyPath()
    {
        var path = Decode([]);
        Assert.Empty(path.Subpaths);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RMoveTo LineTo Endchar ProducesClosedContour.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RMoveTo_LineTo_Endchar_ProducesClosedContour()
    {
        var cs = Build(buf =>
        {
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto
            Number(buf, 100);
            Number(buf, 0);
            Op(buf, 5); // rlineto
            Op(buf, 14); // endchar
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(new Vector2(10, 20), subpath.Start);
        Assert.Contains(subpath.Commands, c => c.Type == PathCommandType.LineTo);
        Assert.Contains(subpath.Commands, c => c.Type == PathCommandType.Close);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RMoveTo WithLeadingWidth IgnoresWidthOperand.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RMoveTo_WithLeadingWidth_IgnoresWidthOperand()
    {
        var cs = Build(buf =>
        {
            Number(buf, 500); // width
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto
            Op(buf, 14); // endchar
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(new Vector2(10, 20), subpath.Start);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Decode RMoveToWithLeadingWidth
    ///     ReturnsNominalWidthXPlusDelta.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Decode_RMoveToWithLeadingWidth_ReturnsNominalWidthXPlusDelta()
    {
        var cs = Build(buf =>
        {
            Number(buf, 25); // width delta
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto
            Op(buf, 14); // endchar
        });

        var (_, width) = CffCharstringInterpreter.Decode(cs, (0, cs.Length), [], [], defaultWidthX: 500, nominalWidthX: 100);

        Assert.Equal(125, width);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Decode NoLeadingWidth ReturnsDefaultWidthX.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Decode_NoLeadingWidth_ReturnsDefaultWidthX()
    {
        var cs = Build(buf =>
        {
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto
            Op(buf, 14); // endchar
        });

        var (_, width) = CffCharstringInterpreter.Decode(cs, (0, cs.Length), [], [], defaultWidthX: 500, nominalWidthX: 100);

        Assert.Equal(500, width);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Decode HStemWithLeadingWidth
    ///     ReturnsNominalWidthXPlusDelta.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Decode_HStemWithLeadingWidth_ReturnsNominalWidthXPlusDelta()
    {
        var cs = Build(buf =>
        {
            Number(buf, 25); // width delta (odd operand count before the stem pairs)
            Number(buf, 0);
            Number(buf, 10);
            Op(buf, 1); // hstem
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto
            Op(buf, 14); // endchar
        });

        var (_, width) = CffCharstringInterpreter.Decode(cs, (0, cs.Length), [], [], defaultWidthX: 500, nominalWidthX: 100);

        Assert.Equal(125, width);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Decode EndCharWithLeadingWidth
    ///     ReturnsNominalWidthXPlusDelta.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Decode_EndCharWithLeadingWidth_ReturnsNominalWidthXPlusDelta()
    {
        var cs = Build(buf =>
        {
            Number(buf, 25); // width delta
            Op(buf, 14); // endchar
        });

        var (_, width) = CffCharstringInterpreter.Decode(cs, (0, cs.Length), [], [], defaultWidthX: 500, nominalWidthX: 100);

        Assert.Equal(125, width);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Decode DefaultWidthParameters AreBothZero.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Decode_DefaultWidthParameters_AreBothZero()
    {
        var cs = Build(buf =>
        {
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto
            Op(buf, 14); // endchar
        });

        var (_, width) = CffCharstringInterpreter.Decode(cs, (0, cs.Length), [], []);

        Assert.Equal(0, width);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HMoveTo VMoveTo MoveAlongSingleAxis.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HMoveTo_VMoveTo_MoveAlongSingleAxis()
    {
        var hCs = Build(buf =>
        {
            Number(buf, 30);
            Op(buf, 22); // hmoveto
            Op(buf, 14);
        });
        var hPath = Decode(hCs);
        Assert.Equal(new Vector2(30, 0), Assert.Single(hPath.Subpaths).Start);

        var vCs = Build(buf =>
        {
            Number(buf, 40);
            Op(buf, 4); // vmoveto
            Op(buf, 14);
        });
        var vPath = Decode(vCs);
        Assert.Equal(new Vector2(0, 40), Assert.Single(vPath.Subpaths).Start);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HLineTo VLineTo AlternateAxes.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HLineTo_VLineTo_AlternateAxes()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 10);
            Number(buf, 20);
            Number(buf, 30);
            Op(buf, 6); // hlineto: dx1 dy2 dx3 -> alternating h,v,h
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var lineTos = subpath.Commands.Where(c => c.Type == PathCommandType.LineTo).ToArray();
        Assert.Equal(3, lineTos.Length);
        Assert.Equal(new Vector2(10, 0), lineTos[0].EndPoint);
        Assert.Equal(new Vector2(10, 20), lineTos[1].EndPoint);
        Assert.Equal(new Vector2(40, 20), lineTos[2].EndPoint);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RLineTo OddOperandCount ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RLineTo_OddOperandCount_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 5);
            Op(buf, 5); // rlineto - odd operand count
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RRCurveTo ProducesCubicBezier.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RRCurveTo_ProducesCubicBezier()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Op(buf, 8); // rrcurveto
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var curve = Assert.Single(subpath.Commands, c => c.Type == PathCommandType.CubicBezierTo);
        Assert.Equal(new Vector2(20, 20), curve.EndPoint);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HHCurveTo VVCurveTo ProduceCubicBeziers.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HHCurveTo_VVCurveTo_ProduceCubicBeziers()
    {
        var hhCs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Op(buf, 27); // hhcurveto
            Op(buf, 14);
        });
        var hhPath = Decode(hhCs);
        Assert.Contains(Assert.Single(hhPath.Subpaths).Commands, c => c.Type == PathCommandType.CubicBezierTo);

        var vvCs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Op(buf, 26); // vvcurveto
            Op(buf, 14);
        });
        var vvPath = Decode(vvCs);
        Assert.Contains(Assert.Single(vvPath.Subpaths).Commands, c => c.Type == PathCommandType.CubicBezierTo);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HHCurveTo LeadingOperand AppliesToFirstCurveOnly.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HHCurveTo_LeadingOperand_AppliesToFirstCurveOnly()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21);
            Number(buf, 5); // leading dy1
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Op(buf, 27); // hhcurveto: 4n+1 operands
            Op(buf, 14);
        });

        var path = Decode(cs);
        var curve = Assert.Single(Assert.Single(path.Subpaths).Commands, c => c.Type == PathCommandType.CubicBezierTo);
        Assert.Equal(5, curve.Control1.Y);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HVCurveTo VHCurveTo AlternateStartTangent.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HVCurveTo_VHCurveTo_AlternateStartTangent()
    {
        var hvCs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Op(buf, 31); // hvcurveto
            Op(buf, 14);
        });
        var hvPath = Decode(hvCs);
        Assert.Contains(Assert.Single(hvPath.Subpaths).Commands, c => c.Type == PathCommandType.CubicBezierTo);

        var vhCs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Op(buf, 30); // vhcurveto
            Op(buf, 14);
        });
        var vhPath = Decode(vhCs);
        Assert.Contains(Assert.Single(vhPath.Subpaths).Commands, c => c.Type == PathCommandType.CubicBezierTo);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HVCurveTo TrailingOperand SuppliesFinalAxisDelta.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HVCurveTo_TrailingOperand_SuppliesFinalAxisDelta()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 5); // trailing operand supplies final-axis delta
            Op(buf, 31); // hvcurveto
            Op(buf, 14);
        });

        var path = Decode(cs);
        var curve = Assert.Single(Assert.Single(path.Subpaths).Commands, c => c.Type == PathCommandType.CubicBezierTo);
        Assert.Equal(25, curve.EndPoint.X);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HStem VStem AccumulateStemCountForHintMask.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HStem_VStem_AccumulateStemCountForHintMask()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 10);
            Op(buf, 1); // hstem: one stem hint
            Number(buf, 0);
            Number(buf, 10);
            Op(buf, 3); // vstem: one more stem hint (2 total -> mask is 1 byte)
            Op(buf, 19); // hintmask
            buf.Add(0xFF); // 1 mask byte for 2 stems
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Op(buf, 14);
        });

        var path = Decode(cs);
        Assert.Equal(new Vector2(0, 0), Assert.Single(path.Subpaths).Start);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HintMask ImplicitVStem CountsTowardMaskBytes.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HintMask_ImplicitVStem_CountsTowardMaskBytes()
    {
        // 9 leftover operands on the stack (before hintmask) implicitly declare a vstem group
        // of ceil(9/2)... actually stem operands come in pairs; here we use 16 leftover operands
        // (8 stems) requiring exactly 1 mask byte.
        var cs = Build(buf =>
        {
            for (var i = 0; i < 16; i++)
            {
                Number(buf, i == 0 ? 0 : 1);
            }

            Op(buf, 19); // hintmask - operands still on stack become an implicit vstem (8 stems)
            buf.Add(0xFF); // 1 mask byte
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21);
            Op(buf, 14);
        });

        var path = Decode(cs);
        Assert.Equal(new Vector2(0, 0), Assert.Single(path.Subpaths).Start);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HintMask TruncatedMaskBytes ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HintMask_TruncatedMaskBytes_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 10);
            Op(buf, 1); // hstem
            Op(buf, 19); // hintmask - requires 1 mask byte, but charstring ends here
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter CallSubr AppliesBias AndReturns.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_CallSubr_AppliesBias_AndReturns()
    {
        // A single local subroutine (count=1 -> bias=107): index -107 selects subr[0].
        var subr = Build(buf =>
        {
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto
            Op(buf, 11); // return
        });

        var localSubrs = new List<(int Offset, int Length)>();
        var combined = new List<byte>();
        localSubrs.Add((0, subr.Length));
        combined.AddRange(subr);

        var main = Build(buf =>
        {
            Number(buf, -107); // subr index before bias
            Op(buf, 10); // callsubr
            Op(buf, 14);
        });
        combined.AddRange(main);

        var path = CffCharstringInterpreter.Decode(
            [.. combined], (subr.Length, main.Length), [], localSubrs).Outline;

        Assert.Equal(new Vector2(10, 20), Assert.Single(path.Subpaths).Start);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter CallGSubr AppliesGlobalBias.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_CallGSubr_AppliesGlobalBias()
    {
        var subr = Build(buf =>
        {
            Number(buf, 5);
            Number(buf, 5);
            Op(buf, 21); // rmoveto
            Op(buf, 11); // return
        });

        var globalSubrs = new List<(int Offset, int Length)> { (0, subr.Length) };
        var combined = new List<byte>();
        combined.AddRange(subr);

        var main = Build(buf =>
        {
            Number(buf, -107); // subr index before bias (count=1 -> bias=107)
            Op(buf, 29); // callgsubr
            Op(buf, 14);
        });
        combined.AddRange(main);

        var path = CffCharstringInterpreter.Decode(
            [.. combined], (subr.Length, main.Length), globalSubrs, []).Outline;

        Assert.Equal(new Vector2(5, 5), Assert.Single(path.Subpaths).Start);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter CallSubr OutOfRangeIndex ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_CallSubr_OutOfRangeIndex_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 1000);
            Op(buf, 10); // callsubr - no subroutines exist
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter CallSubr ExceedsMaxDepth ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_CallSubr_ExceedsMaxDepth_ThrowsInvalidDataException()
    {
        // A single subroutine that immediately calls itself (index -107 with bias 107 -> subr 0)
        // recurses without ever returning, so it must be rejected once the call-depth bound is
        // exceeded rather than looping/overflowing the stack forever.
        var subr = Build(buf =>
        {
            Number(buf, -107);
            Op(buf, 10); // callsubr (self-recursive)
        });

        var localSubrs = new List<(int Offset, int Length)> { (0, subr.Length) };

        var main = Build(buf =>
        {
            Number(buf, -107);
            Op(buf, 10); // callsubr
            Op(buf, 14);
        });

        var combined = new List<byte>();
        combined.AddRange(subr);
        combined.AddRange(main);

        Assert.Throws<InvalidDataException>(() =>
            CffCharstringInterpreter.Decode([.. combined], (subr.Length, main.Length), [], localSubrs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter EndChar SeacStyleFourOperands NoResolverSupplied
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_EndChar_SeacStyleFourOperands_NoResolverSupplied_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Number(buf, 1);
            Number(buf, 2);
            Op(buf, 14); // endchar with 4 leftover operands - deprecated seac form
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter EndChar SeacStyleFourOperands WithResolver
    ///     ComposesBaseAndTranslatedAccent.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_EndChar_SeacStyleFourOperands_WithResolver_ComposesBaseAndTranslatedAccent()
    {
        var basePath = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(10, 0))
            .LineTo(new Vector2(10, 10))
            .Close()
            .Build();
        var accentPath = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(5, 0))
            .LineTo(new Vector2(5, 5))
            .Close()
            .Build();

        Path Resolver(int code) => code == 65 ? basePath : accentPath;

        var cs = Build(buf =>
        {
            Number(buf, 20); // adx
            Number(buf, 30); // ady
            Number(buf, 65); // bchar
            Number(buf, 194); // achar
            Op(buf, 14); // endchar with 4 leftover operands - seac-style composition
        });

        var result = CffCharstringInterpreter.Decode(cs, (0, cs.Length), [], [], resolveStandardEncodedGlyph: Resolver).Outline;

        var expectedAccent = accentPath.Transform(Matrix3x2.CreateTranslation(20, 30));
        Assert.Equal(basePath.Subpaths.Count + expectedAccent.Subpaths.Count, result.Subpaths.Count);
        Assert.Equal(basePath.Subpaths[0].Start, result.Subpaths[0].Start);
        Assert.Equal(expectedAccent.Subpaths[0].Start, result.Subpaths[1].Start);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter EndChar SeacStyleFourOperands
    ///     ResolverOmittedOnComponentDecode ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_EndChar_SeacStyleFourOperands_ResolverOmittedOnComponentDecode_ThrowsInvalidDataException()
    {
        // The component "resolver" itself decodes a nested 4-operand-endchar charstring with no
        // resolver of its own - proving the "resolver omitted on component decode" guard, rather
        // than only exercising the "no resolver at all" case.
        var nestedSeac = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Number(buf, 1);
            Number(buf, 2);
            Op(buf, 14); // endchar with 4 leftover operands - deprecated seac form
        });

        Path Resolver(int _) => Decode(nestedSeac);

        var cs = Build(buf =>
        {
            Number(buf, 20);
            Number(buf, 30);
            Number(buf, 65);
            Number(buf, 194);
            Op(buf, 14); // endchar with 4 leftover operands - seac-style composition
        });

        Assert.Throws<InvalidDataException>(() =>
            CffCharstringInterpreter.Decode(cs, (0, cs.Length), [], [], resolveStandardEncodedGlyph: Resolver));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter EndChar UnexpectedOperandCount ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_EndChar_UnexpectedOperandCount_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 1);
            Number(buf, 2);
            Number(buf, 3);
            Op(buf, 14); // endchar with 3 leftover operands - not width(1), not seac(4), not 0
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter UnsupportedOperator ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_UnsupportedOperator_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Op(buf, 9); // closepath - not in the supported operator set (Type 1 heritage)
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter UnsupportedEscapeOperator ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_UnsupportedEscapeOperator_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Op(buf, 1203); // escape operator 12 3 - genuinely unsupported two-byte escape
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter FixedPointOperand DecodesCorrectly.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_FixedPointOperand_DecodesCorrectly()
    {
        var cs = Build(buf =>
        {
            SyntheticFontBuilder.WriteCharstringFixed(buf, 12.5);
            SyntheticFontBuilder.WriteCharstringFixed(buf, 7.25);
            Op(buf, 21); // rmoveto
            Op(buf, 14);
        });

        var path = Decode(cs);
        Assert.Equal(new Vector2(12.5f, 7.25f), Assert.Single(path.Subpaths).Start);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter TruncatedCharstring ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_TruncatedCharstring_ThrowsInvalidDataException()
    {
        byte[] cs = [28, 0]; // 3-byte int16 operand (0x1C), but only 1 byte follows
        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RMoveTo WrongOperandCount ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RMoveTo_WrongOperandCount_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 1);
            Number(buf, 2);
            Number(buf, 3);
            Number(buf, 4);
            Op(buf, 21); // rmoveto expects exactly 2 (or 3 with width)
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RCurveLine OneCurveThenLine ProducesCurveAndLine.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RCurveLine_OneCurveThenLine_ProducesCurveAndLine()
    {
        // Curve (10,0,10,10,0,10) from (0,0): cp1=(10,0), cp2=(20,10), end=(20,20).
        // Trailing line (5,5) from (20,20): final (25,25).
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 5);
            Number(buf, 5);
            Op(buf, 24); // rcurveline
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var curve = Assert.Single(subpath.Commands, c => c.Type == PathCommandType.CubicBezierTo);
        Assert.Equal(new Vector2(20, 20), curve.EndPoint);
        var line = Assert.Single(subpath.Commands, c => c.Type == PathCommandType.LineTo);
        Assert.Equal(new Vector2(25, 25), line.EndPoint);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RCurveLine TwoCurvesThenLine ProducesCurvesAndLine.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RCurveLine_TwoCurvesThenLine_ProducesCurvesAndLine()
    {
        // First curve (10,0,10,10,0,10) from (0,0): end=(20,20).
        // Second curve, same deltas, from (20,20): cp1=(30,20), cp2=(40,30), end=(40,40).
        // Trailing line (5,5) from (40,40): final (45,45).
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 5);
            Number(buf, 5);
            Op(buf, 24); // rcurveline
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var curves = subpath.Commands.Where(c => c.Type == PathCommandType.CubicBezierTo).ToArray();
        Assert.Equal(2, curves.Length);
        Assert.Equal(new Vector2(20, 20), curves[0].EndPoint);
        Assert.Equal(new Vector2(40, 40), curves[1].EndPoint);
        var line = Assert.Single(subpath.Commands, c => c.Type == PathCommandType.LineTo);
        Assert.Equal(new Vector2(45, 45), line.EndPoint);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RCurveLine WrongOperandCount OneShort
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RCurveLine_WrongOperandCount_OneShort_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 7; i++) // 7 operands: one short of the minimum valid 8
            {
                Number(buf, 1);
            }

            Op(buf, 24); // rcurveline
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RCurveLine WrongOperandCount OneOver
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RCurveLine_WrongOperandCount_OneOver_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 9; i++) // 9 operands: not of the form 6n+2
            {
                Number(buf, 1);
            }

            Op(buf, 24); // rcurveline
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RLineCurve OneLineThenCurve ProducesLineAndCurve.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RLineCurve_OneLineThenCurve_ProducesLineAndCurve()
    {
        // Line (10,0) from (0,0): (10,0).
        // Curve (10,0,10,10,0,10) from (10,0): cp1=(20,0), cp2=(30,10), end=(30,20).
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Op(buf, 25); // rlinecurve
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var line = Assert.Single(subpath.Commands, c => c.Type == PathCommandType.LineTo);
        Assert.Equal(new Vector2(10, 0), line.EndPoint);
        var curve = Assert.Single(subpath.Commands, c => c.Type == PathCommandType.CubicBezierTo);
        Assert.Equal(new Vector2(30, 20), curve.EndPoint);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RLineCurve TwoLinesThenCurve ProducesLinesAndCurve.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RLineCurve_TwoLinesThenCurve_ProducesLinesAndCurve()
    {
        // Line (10,0) from (0,0): (10,0). Line (5,5) from (10,0): (15,5).
        // Curve (10,0,10,10,0,10) from (15,5): cp1=(25,5), cp2=(35,15), end=(35,25).
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 5);
            Number(buf, 5);
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 0);
            Number(buf, 10);
            Op(buf, 25); // rlinecurve
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var lines = subpath.Commands.Where(c => c.Type == PathCommandType.LineTo).ToArray();
        Assert.Equal(2, lines.Length);
        Assert.Equal(new Vector2(10, 0), lines[0].EndPoint);
        Assert.Equal(new Vector2(15, 5), lines[1].EndPoint);
        var curve = Assert.Single(subpath.Commands, c => c.Type == PathCommandType.CubicBezierTo);
        Assert.Equal(new Vector2(35, 25), curve.EndPoint);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RLineCurve WrongOperandCount OneShort
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RLineCurve_WrongOperandCount_OneShort_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 7; i++) // 7 operands: one short of the minimum valid 8
            {
                Number(buf, 1);
            }

            Op(buf, 25); // rlinecurve
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter RLineCurve WrongOperandCount OneOver
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_RLineCurve_WrongOperandCount_OneOver_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 9; i++) // 9 operands: not of the form 2n+6
            {
                Number(buf, 1);
            }

            Op(buf, 25); // rlinecurve
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HFlex ProducesTwoCubicBeziersWithHandComputedCoordinates.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HFlex_ProducesTwoCubicBeziersWithHandComputedCoordinates()
    {
        // hflex(dx1=10, dx2=20, dy2=5, dx3=15, dx4=25, dx5=35, dx6=45) from (0,0):
        //   cp1 = (0+10, 0) = (10, 0)
        //   cp2 = (10+20, 0+5) = (30, 5)
        //   end1 = (30+15, 5) = (45, 5)
        //   cp3 = (45+25, 5) = (70, 5)
        //   cp4 = (70+35, 5-5) = (105, 0)
        //   final = (105+45, 0) = (150, 0)
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 10);
            Number(buf, 20);
            Number(buf, 5);
            Number(buf, 15);
            Number(buf, 25);
            Number(buf, 35);
            Number(buf, 45);
            Op(buf, 1234); // hflex (escape 12 34)
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var curves = subpath.Commands.Where(c => c.Type == PathCommandType.CubicBezierTo).ToArray();
        Assert.Equal(2, curves.Length);
        Assert.Equal(new Vector2(10, 0), curves[0].Control1);
        Assert.Equal(new Vector2(30, 5), curves[0].Control2);
        Assert.Equal(new Vector2(45, 5), curves[0].EndPoint);
        Assert.Equal(new Vector2(70, 5), curves[1].Control1);
        Assert.Equal(new Vector2(105, 0), curves[1].Control2);
        Assert.Equal(new Vector2(150, 0), curves[1].EndPoint);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HFlex WrongOperandCount OneShort
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HFlex_WrongOperandCount_OneShort_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 6; i++) // 6 operands: hflex requires exactly 7
            {
                Number(buf, 1);
            }

            Op(buf, 1234); // hflex
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HFlex WrongOperandCount OneOver
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HFlex_WrongOperandCount_OneOver_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 8; i++) // 8 operands: hflex requires exactly 7
            {
                Number(buf, 1);
            }

            Op(buf, 1234); // hflex
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Flex ProducesTwoCubicBeziersWithHandComputedCoordinates.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Flex_ProducesTwoCubicBeziersWithHandComputedCoordinates()
    {
        // flex(dx1..dy6 = 1,2,3,4,5,6,7,8,9,10,11,12, fd=50) from (0,0):
        //   cp1 = (0+1, 0+2) = (1, 2)
        //   cp2 = (1+3, 2+4) = (4, 6)
        //   end1 = (4+5, 6+6) = (9, 12)
        //   cp3 = (9+7, 12+8) = (16, 20)
        //   cp4 = (16+9, 20+10) = (25, 30)
        //   final = (25+11, 30+12) = (36, 42)
        // fd (50) is consumed with no geometric effect.
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 1);
            Number(buf, 2);
            Number(buf, 3);
            Number(buf, 4);
            Number(buf, 5);
            Number(buf, 6);
            Number(buf, 7);
            Number(buf, 8);
            Number(buf, 9);
            Number(buf, 10);
            Number(buf, 11);
            Number(buf, 12);
            Number(buf, 50); // fd
            Op(buf, 1235); // flex (escape 12 35)
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var curves = subpath.Commands.Where(c => c.Type == PathCommandType.CubicBezierTo).ToArray();
        Assert.Equal(2, curves.Length);
        Assert.Equal(new Vector2(1, 2), curves[0].Control1);
        Assert.Equal(new Vector2(4, 6), curves[0].Control2);
        Assert.Equal(new Vector2(9, 12), curves[0].EndPoint);
        Assert.Equal(new Vector2(16, 20), curves[1].Control1);
        Assert.Equal(new Vector2(25, 30), curves[1].Control2);
        Assert.Equal(new Vector2(36, 42), curves[1].EndPoint);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Flex WrongOperandCount OneShort
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Flex_WrongOperandCount_OneShort_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 12; i++) // 12 operands: flex requires exactly 13
            {
                Number(buf, 1);
            }

            Op(buf, 1235); // flex
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Flex WrongOperandCount OneOver
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Flex_WrongOperandCount_OneOver_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 14; i++) // 14 operands: flex requires exactly 13
            {
                Number(buf, 1);
            }

            Op(buf, 1235); // flex
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HFlex1 ProducesTwoCubicBeziersWithHandComputedCoordinates.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HFlex1_ProducesTwoCubicBeziersWithHandComputedCoordinates()
    {
        // hflex1(dx1=10, dy1=10, dx2=20, dy2=-20, dx3=30, dx4=40, dx5=50, dy5=5, dx6=60)
        // from (100,200), originalStartY=200:
        //   cp1 = (100+10, 200+10) = (110, 210)
        //   cp2 = (110+20, 210-20) = (130, 190)
        //   end1 = (130+30, 190) = (160, 190)
        //   cp3 = (160+40, 190) = (200, 190)
        //   cp4 = (200+50, 190+5) = (250, 195)
        //   final = (250+60, originalStartY) = (310, 200)
        var cs = Build(buf =>
        {
            Number(buf, 100);
            Number(buf, 200);
            Op(buf, 21); // rmoveto
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 20);
            Number(buf, -20);
            Number(buf, 30);
            Number(buf, 40);
            Number(buf, 50);
            Number(buf, 5);
            Number(buf, 60);
            Op(buf, 1236); // hflex1 (escape 12 36)
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var curves = subpath.Commands.Where(c => c.Type == PathCommandType.CubicBezierTo).ToArray();
        Assert.Equal(2, curves.Length);
        Assert.Equal(new Vector2(110, 210), curves[0].Control1);
        Assert.Equal(new Vector2(130, 190), curves[0].Control2);
        Assert.Equal(new Vector2(160, 190), curves[0].EndPoint);
        Assert.Equal(new Vector2(200, 190), curves[1].Control1);
        Assert.Equal(new Vector2(250, 195), curves[1].Control2);
        Assert.Equal(new Vector2(310, 200), curves[1].EndPoint); // forced back to originalStartY (200), not naive 195
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HFlex1 WrongOperandCount OneShort
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HFlex1_WrongOperandCount_OneShort_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 8; i++) // 8 operands: hflex1 requires exactly 9
            {
                Number(buf, 1);
            }

            Op(buf, 1236); // hflex1
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter HFlex1 WrongOperandCount OneOver
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_HFlex1_WrongOperandCount_OneOver_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 10; i++) // 10 operands: hflex1 requires exactly 9
            {
                Number(buf, 1);
            }

            Op(buf, 1236); // hflex1
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Flex1 DxDominant ProducesHandComputedCoordinates.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Flex1_DxDominant_ProducesHandComputedCoordinates()
    {
        // flex1(dx1=100,dy1=10, dx2=10,dy2=-5, dx3=10,dy3=5, dx4=10,dy4=-10, dx5=10,dy5=10, d6=5)
        // from (0,0), originalStart=(0,0):
        //   cp1 = (100, 10)
        //   cp2 = (110, 5)
        //   end1 = (120, 10)
        //   cp3 = (130, 0)
        //   cp4 = (140, 10)
        //   dx = 100+10+10+10+10 = 140, dy = 10-5+5-10+10 = 10
        //   |dx| > |dy| -> final = (140+5, originalStartY) = (145, 0)
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 100);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, -5);
            Number(buf, 10);
            Number(buf, 5);
            Number(buf, 10);
            Number(buf, -10);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 5);
            Op(buf, 1237); // flex1 (escape 12 37)
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var curves = subpath.Commands.Where(c => c.Type == PathCommandType.CubicBezierTo).ToArray();
        Assert.Equal(2, curves.Length);
        Assert.Equal(new Vector2(100, 10), curves[0].Control1);
        Assert.Equal(new Vector2(110, 5), curves[0].Control2);
        Assert.Equal(new Vector2(120, 10), curves[0].EndPoint);
        Assert.Equal(new Vector2(130, 0), curves[1].Control1);
        Assert.Equal(new Vector2(140, 10), curves[1].Control2);
        Assert.Equal(new Vector2(145, 0), curves[1].EndPoint);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Flex1 DyDominant ProducesHandComputedCoordinates.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Flex1_DyDominant_ProducesHandComputedCoordinates()
    {
        // flex1(dx1=5,dy1=100, dx2=-5,dy2=10, dx3=5,dy3=10, dx4=-10,dy4=10, dx5=10,dy5=10, d6=7)
        // from (0,0), originalStart=(0,0):
        //   cp1 = (5, 100)
        //   cp2 = (0, 110)
        //   end1 = (5, 120)
        //   cp3 = (-5, 130)
        //   cp4 = (5, 140)
        //   dx = 5-5+5-10+10 = 5, dy = 100+10+10+10+10 = 140
        //   |dy| > |dx| -> final = (originalStartX, 140+7) = (0, 147)
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 5);
            Number(buf, 100);
            Number(buf, -5);
            Number(buf, 10);
            Number(buf, 5);
            Number(buf, 10);
            Number(buf, -10);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 10);
            Number(buf, 7);
            Op(buf, 1237); // flex1 (escape 12 37)
            Op(buf, 14);
        });

        var path = Decode(cs);
        var subpath = Assert.Single(path.Subpaths);
        var curves = subpath.Commands.Where(c => c.Type == PathCommandType.CubicBezierTo).ToArray();
        Assert.Equal(2, curves.Length);
        Assert.Equal(new Vector2(5, 100), curves[0].Control1);
        Assert.Equal(new Vector2(0, 110), curves[0].Control2);
        Assert.Equal(new Vector2(5, 120), curves[0].EndPoint);
        Assert.Equal(new Vector2(-5, 130), curves[1].Control1);
        Assert.Equal(new Vector2(5, 140), curves[1].Control2);
        Assert.Equal(new Vector2(0, 147), curves[1].EndPoint);
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Flex1 WrongOperandCount OneShort
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Flex1_WrongOperandCount_OneShort_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 10; i++) // 10 operands: flex1 requires exactly 11
            {
                Number(buf, 1);
            }

            Op(buf, 1237); // flex1
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter Flex1 WrongOperandCount OneOver
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_Flex1_WrongOperandCount_OneOver_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            for (var i = 0; i < 12; i++) // 12 operands: flex1 requires exactly 11
            {
                Number(buf, 1);
            }

            Op(buf, 1237); // flex1
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that CffCharstringInterpreter DotSection IsNoOp MatchesSameCharstringWithoutDotSection.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_DotSection_IsNoOp_MatchesSameCharstringWithoutDotSection()
    {
        var csWith = Build(buf =>
        {
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto
            Op(buf, 1200); // dotsection (escape 12 0) - no-op
            Number(buf, 100);
            Number(buf, 0);
            Op(buf, 5); // rlineto
            Op(buf, 14); // endchar
        });

        var csWithout = Build(buf =>
        {
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto
            Number(buf, 100);
            Number(buf, 0);
            Op(buf, 5); // rlineto
            Op(buf, 14); // endchar
        });

        var pathWith = Decode(csWith);
        var pathWithout = Decode(csWithout);

        Assert.Equal(pathWithout.Subpaths.Count, pathWith.Subpaths.Count);
        var subpathWith = Assert.Single(pathWith.Subpaths);
        var subpathWithout = Assert.Single(pathWithout.Subpaths);
        Assert.Equal(subpathWithout.Start, subpathWith.Start);
        Assert.Equal(subpathWithout.Commands.Count, subpathWith.Commands.Count);
        for (var i = 0; i < subpathWith.Commands.Count; i++)
        {
            Assert.Equal(subpathWithout.Commands[i].Type, subpathWith.Commands[i].Type);
            Assert.Equal(subpathWithout.Commands[i].EndPoint, subpathWith.Commands[i].EndPoint);
        }
    }
}
