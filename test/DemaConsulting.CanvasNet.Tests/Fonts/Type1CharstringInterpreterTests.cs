// cspell:ignore charstring charstrings subr subrs hsbw rmoveto hmoveto vmoveto rlineto hlineto
// cspell:ignore vlineto rrcurveto vhcurveto hvcurveto callsubr callothersubr dotsection vstem
// cspell:ignore hstem endchar seac setcurrentpoint othersubr sbw hstem3 vstem3
using System.Numerics;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Tests.TestSupport;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="Type1CharstringInterpreter"/>.
/// </summary>
public class Type1CharstringInterpreterTests
{
    private static (Path Outline, double Width) Decode(byte[] charstring, IReadOnlyList<(int Offset, int Length)>? subrs = null) =>
        Type1CharstringInterpreter.Decode(charstring, (0, charstring.Length), subrs ?? []);

    private static byte[] Build(Action<List<byte>> write)
    {
        var buf = new List<byte>();
        write(buf);
        return [.. buf];
    }

    private static void Number(List<byte> buf, int value) => SyntheticFontBuilder.WriteType1CharstringNumber(buf, value);

    private static void Op(List<byte> buf, int op) => SyntheticFontBuilder.WriteType1CharstringOperator(buf, op);

    /// <summary>
    ///     Proves that Type1CharstringInterpreter EmptyCharstring ProducesEmptyPathAndZeroWidth.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_EmptyCharstring_ProducesEmptyPathAndZeroWidth()
    {
        var (outline, width) = Decode([]);
        Assert.Empty(outline.Subpaths);
        Assert.Equal(0, width);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter Hsbw CapturesWidthAndSideBearing.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_Hsbw_CapturesWidthAndSideBearing()
    {
        var cs = Build(buf =>
        {
            Number(buf, 50);
            Number(buf, 600);
            Op(buf, 13); // hsbw
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto - relative to (sbx, 0)
            Op(buf, 14); // endchar
        });

        var (outline, width) = Decode(cs);
        Assert.Equal(600, width);
        Assert.Equal(new Vector2(50, 0), Assert.Single(outline.Subpaths).Start);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter Sbw CapturesWidthAndBothSideBearings.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_Sbw_CapturesWidthAndBothSideBearings()
    {
        var cs = Build(buf =>
        {
            Number(buf, 30);
            Number(buf, 40);
            Number(buf, 700);
            Number(buf, 0);
            Op(buf, 1207); // escape 12 7 = sbw
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Op(buf, 14);
        });

        var (outline, width) = Decode(cs);
        Assert.Equal(700, width);
        Assert.Equal(new Vector2(30, 40), Assert.Single(outline.Subpaths).Start);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter RLineTo HLineTo VLineTo ProduceLines.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_RLineTo_HLineTo_VLineTo_ProduceLines()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 5); // rlineto
            Number(buf, 5);
            Op(buf, 6); // hlineto
            Number(buf, 7);
            Op(buf, 7); // vlineto
            Op(buf, 14);
        });

        var (outline, _) = Decode(cs);
        var lineTos = Assert.Single(outline.Subpaths).Commands.Where(c => c.Type == PathCommandType.LineTo).ToArray();
        Assert.Equal(3, lineTos.Length);
        Assert.Equal(new Vector2(10, 20), lineTos[0].EndPoint);
        Assert.Equal(new Vector2(15, 20), lineTos[1].EndPoint);
        Assert.Equal(new Vector2(15, 27), lineTos[2].EndPoint);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter RRCurveTo ProducesCubicBezier.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_RRCurveTo_ProducesCubicBezier()
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

        var (outline, _) = Decode(cs);
        var curve = Assert.Single(Assert.Single(outline.Subpaths).Commands, c => c.Type == PathCommandType.CubicBezierTo);
        Assert.Equal(new Vector2(20, 20), curve.EndPoint);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter VhCurveTo HvCurveTo AlternateStartTangent.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_VhCurveTo_HvCurveTo_AlternateStartTangent()
    {
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
        var (vhOutline, _) = Decode(vhCs);
        Assert.Contains(Assert.Single(vhOutline.Subpaths).Commands, c => c.Type == PathCommandType.CubicBezierTo);

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
        var (hvOutline, _) = Decode(hvCs);
        Assert.Contains(Assert.Single(hvOutline.Subpaths).Commands, c => c.Type == PathCommandType.CubicBezierTo);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter ClosePath ClosesSubpath.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_ClosePath_ClosesSubpath()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 10);
            Number(buf, 10);
            Op(buf, 5); // rlineto
            Op(buf, 9); // closepath
            Op(buf, 14);
        });

        var (outline, _) = Decode(cs);
        Assert.True(Assert.Single(outline.Subpaths).IsClosed);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter HStem VStem Hstem3 Vstem3 Dotsection ConsumedWithoutError.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_HStem_VStem_Hstem3_Vstem3_Dotsection_ConsumedWithoutError()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 10);
            Op(buf, 1); // hstem
            Number(buf, 0);
            Number(buf, 10);
            Op(buf, 3); // vstem
            Op(buf, 1200); // dotsection
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 20);
            Number(buf, 30);
            Number(buf, 40);
            Number(buf, 50);
            Op(buf, 1202); // hstem3
            Number(buf, 0);
            Number(buf, 10);
            Number(buf, 20);
            Number(buf, 30);
            Number(buf, 40);
            Number(buf, 50);
            Op(buf, 1201); // vstem3
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Op(buf, 14);
        });

        var (outline, _) = Decode(cs);
        Assert.Single(outline.Subpaths);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter CallSubr Return NoBias DirectIndex.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_CallSubr_Return_NoBias_DirectIndex()
    {
        var subr = Build(buf =>
        {
            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto
            Op(buf, 11); // return
        });

        var main = Build(buf =>
        {
            Number(buf, 0); // subr index 0 - no bias in Type 1
            Op(buf, 10); // callsubr
            Op(buf, 14);
        });

        var combined = new List<byte>();
        combined.AddRange(subr);
        combined.AddRange(main);

        var (outline, _) = Type1CharstringInterpreter.Decode(
            [.. combined], (subr.Length, main.Length), [(0, subr.Length)]);

        Assert.Equal(new Vector2(10, 20), Assert.Single(outline.Subpaths).Start);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter CallSubr OutOfRangeIndex ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_CallSubr_OutOfRangeIndex_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 5);
            Op(buf, 10); // callsubr - no subroutines exist
            Op(buf, 14);
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter CallSubr ExceedsMaxDepth ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_CallSubr_ExceedsMaxDepth_ThrowsInvalidDataException()
    {
        var subr = Build(buf =>
        {
            Number(buf, 0);
            Op(buf, 10); // callsubr (self-recursive)
        });

        var main = Build(buf =>
        {
            Number(buf, 0);
            Op(buf, 10); // callsubr
            Op(buf, 14);
        });

        var combined = new List<byte>();
        combined.AddRange(subr);
        combined.AddRange(main);

        Assert.Throws<InvalidDataException>(() =>
            Type1CharstringInterpreter.Decode([.. combined], (subr.Length, main.Length), [(0, subr.Length)]));
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter Div ResultFeedsSubsequentOperator.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_Div_ResultFeedsSubsequentOperator()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 20);
            Number(buf, 4);
            Op(buf, 1212); // div -> 5
            Number(buf, 7);
            Op(buf, 5); // rlineto: (5, 7)
            Op(buf, 14);
        });

        var (outline, _) = Decode(cs);
        var lineTo = Assert.Single(Assert.Single(outline.Subpaths).Commands, c => c.Type == PathCommandType.LineTo);
        Assert.Equal(new Vector2(5, 7), lineTo.EndPoint);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter Flex EndToEnd ProducesTwoRealCubicBeziers.
    /// </summary>
    /// <remarks>
    ///     Exercises the complete Type 1 flex idiom via <c>callsubr</c>-wrapped OtherSubrs calls
    ///     (subr 1 = begin flex, subr 2 = flex point mark, subr 0 = end flex), buffering seven
    ///     <c>rmoveto</c> deltas and asserting the exact resulting curve geometry - not merely
    ///     that decoding succeeds without an exception.
    /// </remarks>
    [Fact]
    public void Type1CharstringInterpreter_Flex_EndToEnd_ProducesTwoRealCubicBeziers()
    {
        // Subr 0: end flex - "3 0 callothersubr pop pop setcurrentpoint return".
        var subr0 = Build(buf =>
        {
            Number(buf, 3);
            Number(buf, 0);
            Op(buf, 1216); // callothersubr
            Op(buf, 1217); // pop
            Op(buf, 1217); // pop
            Op(buf, 1233); // setcurrentpoint
            Op(buf, 11); // return
        });

        // Subr 1: begin flex - "0 1 callothersubr return".
        var subr1 = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 1);
            Op(buf, 1216); // callothersubr
            Op(buf, 11); // return
        });

        // Subr 2: flex point mark - "0 2 callothersubr return".
        var subr2 = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 2);
            Op(buf, 1216); // callothersubr
            Op(buf, 11); // return
        });

        var main = Build(buf =>
        {
            Number(buf, 50);
            Number(buf, 600);
            Op(buf, 13); // hsbw - current point becomes (50, 0)

            Number(buf, 50);
            Number(buf, 100);
            Op(buf, 21); // rmoveto -> (100, 100) - starts the open subpath

            Number(buf, 1);
            Op(buf, 10); // callsubr 1 - begin flex

            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto -> reference point (100, 100), buffered and later discarded
            Number(buf, 2);
            Op(buf, 10); // callsubr 2

            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto -> (110, 120) - curve 1 control point 1
            Number(buf, 2);
            Op(buf, 10);

            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto -> (120, 140) - curve 1 control point 2
            Number(buf, 2);
            Op(buf, 10);

            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto -> (130, 160) - curve 1 end point / curve 2 start reference
            Number(buf, 2);
            Op(buf, 10);

            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto -> (140, 180) - curve 2 control point 1
            Number(buf, 2);
            Op(buf, 10);

            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto -> (150, 200) - curve 2 control point 2
            Number(buf, 2);
            Op(buf, 10);

            Number(buf, 10);
            Number(buf, 20);
            Op(buf, 21); // rmoveto -> (160, 220) - curve 2 end point
            Number(buf, 2);
            Op(buf, 10);

            Number(buf, 50); // flexHeight (unused by geometry, only by a real PS flex decision)
            Number(buf, 160); // finalX - must match the 7th buffered point
            Number(buf, 220); // finalY - must match the 7th buffered point
            Number(buf, 0);
            Op(buf, 10); // callsubr 0 - end flex

            Number(buf, 20);
            Number(buf, -20);
            Op(buf, 5); // rlineto -> (180, 200)
            Op(buf, 9); // closepath
            Op(buf, 14); // endchar
        });

        var combined = new List<byte>();
        var subr0Range = (0, subr0.Length);
        combined.AddRange(subr0);
        var subr1Range = (combined.Count, subr1.Length);
        combined.AddRange(subr1);
        var subr2Range = (combined.Count, subr2.Length);
        combined.AddRange(subr2);
        var mainRange = (combined.Count, main.Length);
        combined.AddRange(main);

        var subrs = new List<(int Offset, int Length)> { subr0Range, subr1Range, subr2Range };

        var (outline, width) = Type1CharstringInterpreter.Decode([.. combined], mainRange, subrs);

        Assert.Equal(600, width);
        var subpath = Assert.Single(outline.Subpaths);
        Assert.Equal(new Vector2(100, 100), subpath.Start);

        var curves = subpath.Commands.Where(c => c.Type == PathCommandType.CubicBezierTo).ToArray();
        Assert.Equal(2, curves.Length);

        // Curve 1: control points (110,120)/(120,140), end (130,160).
        Assert.Equal(new Vector2(110, 120), curves[0].Control1);
        Assert.Equal(new Vector2(120, 140), curves[0].Control2);
        Assert.Equal(new Vector2(130, 160), curves[0].EndPoint);

        // Curve 2: control points (140,180)/(150,200), end (160,220).
        Assert.Equal(new Vector2(140, 180), curves[1].Control1);
        Assert.Equal(new Vector2(150, 200), curves[1].Control2);
        Assert.Equal(new Vector2(160, 220), curves[1].EndPoint);

        // The trailing rlineto/closepath after 'setcurrentpoint' resets (x, y) to (160, 220)
        // prove setcurrentpoint's (x, y) stack order was honored correctly.
        var lineTo = Assert.Single(subpath.Commands, c => c.Type == PathCommandType.LineTo);
        Assert.Equal(new Vector2(180, 200), lineTo.EndPoint);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter HintReplacement OtherSubr3 IsTransparentPassThrough.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_HintReplacement_OtherSubr3_IsTransparentPassThrough()
    {
        // Subr 5: the "replacement hint" subroutine - just more (discarded) stem hints.
        var subr5 = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 10);
            Op(buf, 1); // hstem
            Op(buf, 11); // return
        });

        var main = Build(buf =>
        {
            Number(buf, 5); // the subr number to hint-replace with
            Number(buf, 1);
            Number(buf, 3);
            Op(buf, 1216); // callothersubr: 1 arg, othersubr 3
            Op(buf, 1217); // pop - retrieves the subr number (5) back onto the operand stack
            Op(buf, 10); // callsubr - invokes subr 5 (pure pass-through)
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Op(buf, 14);
        });

        var combined = new List<byte>();
        var paddingSubrCount = 5;
        for (var i = 0; i < paddingSubrCount; i++)
        {
            combined.Add(11); // 'return' - unused filler subroutines just to occupy indices 0..4
        }

        var subr5Range = (combined.Count, subr5.Length);
        combined.AddRange(subr5);
        var mainRange = (combined.Count, main.Length);
        combined.AddRange(main);

        var subrs = new List<(int Offset, int Length)>();
        for (var i = 0; i < paddingSubrCount; i++)
        {
            subrs.Add((i, 1));
        }

        subrs.Add(subr5Range);

        var (outline, _) = Type1CharstringInterpreter.Decode([.. combined], mainRange, subrs);
        Assert.Single(outline.Subpaths);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter Seac ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_Seac_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Number(buf, 0);
            Number(buf, 65);
            Number(buf, 66);
            Op(buf, 1206); // escape 12 6 = seac
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter UnsupportedOperator ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_UnsupportedOperator_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Op(buf, 16); // unassigned Type 1 single-byte operator code
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter UnsupportedEscapeOperator ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_UnsupportedEscapeOperator_ThrowsInvalidDataException()
    {
        var cs = Build(buf => Op(buf, 1299)); // escape 12 99 - not a recognized Type 1 escape operator

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter CallOtherSubr UnsupportedIndex ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_CallOtherSubr_UnsupportedIndex_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 99); // an unsupported OtherSubrs index
            Op(buf, 1216); // callothersubr
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter RLineTo WrongOperandCount ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_RLineTo_WrongOperandCount_ThrowsInvalidDataException()
    {
        var cs = Build(buf =>
        {
            Number(buf, 0);
            Number(buf, 0);
            Op(buf, 21); // rmoveto
            Number(buf, 5);
            Op(buf, 5); // rlineto - requires exactly 2 operands
        });

        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter FivByteInteger DecodesAsPlainInt32.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_FiveByteInteger_DecodesAsPlainInt32()
    {
        var cs = Build(buf =>
        {
            Number(buf, 5000); // forces the 5-byte '255' plain-int32 form
            Number(buf, -5000);
            Op(buf, 21); // rmoveto
            Op(buf, 14);
        });

        var (outline, _) = Decode(cs);
        Assert.Equal(new Vector2(5000, -5000), Assert.Single(outline.Subpaths).Start);
    }

    /// <summary>
    ///     Proves that Type1CharstringInterpreter TruncatedCharstring ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1CharstringInterpreter_TruncatedCharstring_ThrowsInvalidDataException()
    {
        byte[] cs = [247]; // 2-byte numeric form, but only 1 byte present
        Assert.Throws<InvalidDataException>(() => Decode(cs));
    }
}
