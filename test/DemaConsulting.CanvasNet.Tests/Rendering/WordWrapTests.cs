using DemaConsulting.CanvasNet.Rendering;

namespace DemaConsulting.CanvasNet.Tests.Rendering;

/// <summary>Unit tests for <see cref="WordWrap"/> and <see cref="WordWrapTokenKind"/>.</summary>
public class WordWrapTests
{
    /// <summary>A synthetic token: its text, its role, and its fixed width.</summary>
    private sealed record Tok(string Text, WordWrapTokenKind Kind, double Width);

    /// <summary>Builds a word token.</summary>
    private static Tok W(string text, double width) => new(text, WordWrapTokenKind.Word, width);

    /// <summary>Builds a whitespace token one unit wide.</summary>
    private static Tok S() => new(" ", WordWrapTokenKind.Whitespace, 1);

    /// <summary>Builds a line-break token.</summary>
    private static Tok Br() => new("\n", WordWrapTokenKind.LineBreak, 0);

    /// <summary>Packs with the double overload, reading the kind and width from the token.</summary>
    private static List<List<Tok>> Pack(IReadOnlyList<Tok> tokens, double available) =>
        WordWrap.Pack(tokens, available, static t => t.Kind, static (t, _) => t.Width);

    /// <summary>Renders lines as text, a "|" between lines, so expectations stay readable.</summary>
    private static string Render(List<List<Tok>> lines) =>
        string.Join("|", lines.Select(line => string.Concat(line.Select(t => t.Text))));

    /// <summary>Proves tokens that fit stay on one line, in order.</summary>
    [Fact]
    public void WordWrap_Pack_TokensFit_ReturnsOneLine()
    {
        // Arrange: "aa bb" is 5 wide
        var tokens = new[] { W("aa", 2), S(), W("bb", 2) };

        // Act
        var lines = Pack(tokens, 5);

        // Assert
        Assert.Equal("aa bb", Render(lines));
    }

    /// <summary>Proves a word group that would overflow starts the next line, carrying the whitespace before it.</summary>
    [Fact]
    public void WordWrap_Pack_WordOverflowsLine_WrapsAtWhitespace()
    {
        // Arrange: widths aa=2 bb=2 cc=2 with one-wide spaces; the line holds 5
        var tokens = new[] { W("aa", 2), S(), W("bb", 2), S(), W("cc", 2) };

        // Act
        var lines = Pack(tokens, 5);

        // Assert: the stranded space moves to the next line instead of leaving a blank one
        Assert.Equal("aa bb| cc", Render(lines));
    }

    /// <summary>Proves adjacent word tokens form one group that is never split, even when it overflows.</summary>
    [Fact]
    public void WordWrap_Pack_AdjacentWordTokens_AreNeverSplit()
    {
        // Arrange: "Hel"+"lo" (two runs of one word) is 5 wide on a 4-wide line
        var tokens = new[] { W("x", 1), S(), W("Hel", 3), W("lo", 2), S(), W("y", 1) };

        // Act
        var lines = Pack(tokens, 4);

        // Assert: "Hello" is kept whole on its own line; "y" fits after the carried space
        Assert.Equal("x |Hello| y", Render(lines));
    }

    /// <summary>Proves a group wider than the line is placed whole on its own line rather than looping or splitting.</summary>
    [Fact]
    public void WordWrap_Pack_GroupWiderThanLine_IsPlacedWholeOnItsOwnLine()
    {
        // Arrange
        var tokens = new[] { W("a", 1), S(), W("supercalifragilistic", 20), S(), W("b", 1) };

        // Act
        var lines = Pack(tokens, 5);

        // Assert
        Assert.Equal("a |supercalifragilistic| b", Render(lines));
    }

    /// <summary>Proves a line break ends the current line, is not placed on any line, and a trailing one leaves an empty line.</summary>
    [Fact]
    public void WordWrap_Pack_LineBreaks_EndLinesAndAreNeverPlaced()
    {
        // Act
        var simple = Pack([W("a", 1), Br(), W("b", 1)], 100);
        var leading = Pack([Br(), W("a", 1)], 100);
        var trailing = Pack([W("a", 1), Br()], 100);
        var doubled = Pack([W("a", 1), Br(), Br(), W("b", 1)], 100);
        var onlyBreak = Pack([Br()], 100);

        // Assert
        Assert.Equal("a|b", Render(simple));
        Assert.Equal("|a", Render(leading));
        Assert.Equal("a|", Render(trailing));
        Assert.Equal("a||b", Render(doubled));
        Assert.Equal(2, onlyBreak.Count);
        Assert.All(onlyBreak, line => Assert.Empty(line));
    }

    /// <summary>Proves a line break also closes a pending word group first, so the group stays before the break.</summary>
    [Fact]
    public void WordWrap_Pack_LineBreakAfterWordGroup_KeepsGroupOnThePrecedingLine()
    {
        // Act
        var lines = Pack([W("ab", 2), W("cd", 2), Br(), W("e", 1)], 100);

        // Assert
        Assert.Equal("abcd|e", Render(lines));
    }

    /// <summary>Proves no tokens give no lines, leaving the caller to decide what an empty paragraph looks like.</summary>
    [Fact]
    public void WordWrap_Pack_NoTokens_ReturnsNoLines()
    {
        // Act / Assert
        Assert.Empty(Pack([], 10));
    }

    /// <summary>Proves a whitespace-only paragraph is one line that is never closed for lack of room.</summary>
    [Fact]
    public void WordWrap_Pack_OnlyWhitespace_StaysOnOneLine()
    {
        // Act
        var lines = Pack([S(), S(), S()], 1);

        // Assert
        Assert.Equal("   ", Render(lines));
    }

    /// <summary>Proves the measure callback sees the width already used on the line, so position-dependent widths (tabs) work.</summary>
    [Fact]
    public void WordWrap_Pack_MeasureReceivesCurrentLineWidth()
    {
        // Arrange: a tab advances to the next multiple of 4 from the current position
        var tokens = new[]
        {
            W("ab", 2),
            new Tok("\t", WordWrapTokenKind.Whitespace, 0),
            W("cd", 2),
            new Tok("\t", WordWrapTokenKind.Whitespace, 0),
            W("e", 1),
        };
        var seen = new List<double>();

        // Act: line width 9; ab(2) tab->4 (width 2) cd(2)=6 tab->8 (width 2)=8 then "e" would reach 9 which fits
        var fits = WordWrap.Pack(
            tokens,
            9d,
            static t => t.Kind,
            (t, x) =>
            {
                if (t.Text != "\t")
                {
                    return t.Width;
                }

                seen.Add(x);
                return (Math.Floor(x / 4) + 1) * 4 - x;
            });
        var wraps = WordWrap.Pack(
            tokens,
            8.5d,
            static t => t.Kind,
            static (t, x) => t.Text == "\t" ? (Math.Floor(x / 4) + 1) * 4 - x : t.Width);

        // Assert
        Assert.Single(fits);
        Assert.Equal([2d, 6d], seen);
        Assert.Equal("ab\tcd\t|e", Render(wraps));
    }

    /// <summary>Proves the single-precision overload packs like the double one for exactly representable widths.</summary>
    [Fact]
    public void WordWrap_Pack_FloatOverload_PacksLikeDoubleOverload()
    {
        // Arrange
        var tokens = new[] { W("aa", 2), S(), W("bb", 2), S(), W("cc", 2) };

        // Act
        var viaFloat = WordWrap.Pack(tokens, 5f, static t => t.Kind, static (t, _) => (float)t.Width);
        var viaDouble = Pack(tokens, 5d);

        // Assert
        Assert.Equal(Render(viaDouble), Render(viaFloat));
    }

    /// <summary>Proves a zero or negative width gives each unit its own line and infinity never wraps.</summary>
    [Fact]
    public void WordWrap_Pack_ZeroNegativeAndInfiniteWidth_BehaveAsDocumented()
    {
        // Arrange
        var tokens = new[] { W("aa", 2), S(), W("bb", 2), S(), W("cc", 2) };

        // Act / Assert
        Assert.Equal("aa| bb| cc", Render(Pack(tokens, 0)));
        Assert.Equal("aa| bb| cc", Render(Pack(tokens, -3)));
        Assert.Equal("aa bb cc", Render(Pack(tokens, double.PositiveInfinity)));
    }

    /// <summary>Proves the input list is not modified and the returned lines are independent of it.</summary>
    [Fact]
    public void WordWrap_Pack_Result_IsIndependentOfInput()
    {
        // Arrange
        var tokens = new List<Tok> { W("aa", 2), S(), W("bb", 2) };

        // Act
        var lines = Pack(tokens, 3);
        lines[0].Clear();

        // Assert
        Assert.Equal(3, tokens.Count);
    }

    /// <summary>Proves arguments are validated up front with the matching parameter names.</summary>
    [Fact]
    public void WordWrap_Pack_InvalidArguments_ThrowWithParameterName()
    {
        // Arrange
        var tokens = new[] { W("a", 1) };

        // Act / Assert
        Assert.Equal("tokens", Assert.Throws<ArgumentNullException>(() => WordWrap.Pack(null!, 1d, static (Tok t) => t.Kind, static (t, _) => t.Width)).ParamName);
        Assert.Equal("classify", Assert.Throws<ArgumentNullException>(() => WordWrap.Pack(tokens, 1d, null!, static (t, _) => t.Width)).ParamName);
        Assert.Equal("measure", Assert.Throws<ArgumentNullException>(() => WordWrap.Pack(tokens, 1d, static t => t.Kind, null!)).ParamName);
        Assert.Equal("tokens", Assert.Throws<ArgumentNullException>(() => WordWrap.Pack(null!, 1f, static (Tok t) => t.Kind, static (t, _) => 1f)).ParamName);
        Assert.Equal("availableWidth", Assert.Throws<ArgumentOutOfRangeException>(() => WordWrap.Pack(tokens, double.NaN, static t => t.Kind, static (t, _) => t.Width)).ParamName);
        Assert.Equal("availableWidth", Assert.Throws<ArgumentOutOfRangeException>(() => WordWrap.Pack(tokens, float.NaN, static t => t.Kind, static (t, _) => 1f)).ParamName);
    }
}
