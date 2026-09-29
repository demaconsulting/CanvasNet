// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO CFF charstring charstrings subr subrs
// cspell:ignore hintmask cntrmask rmoveto hmoveto vmoveto rlineto hlineto vlineto
// cspell:ignore rrcurveto hhcurveto vvcurveto hvcurveto vhcurveto callsubr callgsubr
// cspell:ignore endchar seac hstem vstem hstemhm vstemhm rcurveline rlinecurve
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
        CffCharstringInterpreter.Decode(charstring, (0, charstring.Length), globalSubrs ?? [], localSubrs ?? []);

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
            [.. combined], (subr.Length, main.Length), [], localSubrs);

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
            [.. combined], (subr.Length, main.Length), globalSubrs, []);

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
    ///     Proves that CffCharstringInterpreter EndChar SeacStyleFourOperands ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_EndChar_SeacStyleFourOperands_ThrowsInvalidDataException()
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
    ///     Proves that CffCharstringInterpreter FlexEscapeOperator ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void CffCharstringInterpreter_FlexEscapeOperator_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Op(buf, 1234); // escape operator 12 34 (flex) - unsupported two-byte escape
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
}
