using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="GenericFontFamilies"/>: the classification of a family name as a generic
///     family (CSS keyword or common alias) and the splitting of a CSS <c>font-family</c> list.
/// </summary>
public class GenericFontFamiliesTests
{
    /// <summary>
    ///     Proves that the CSS generic keywords are classified, in any case, with or without the common aliases.
    /// </summary>
    /// <param name="name">The family name.</param>
    /// <param name="serif">Whether the serif design is expected.</param>
    /// <param name="fixedPitch">Whether the monospace design is expected.</param>
    [Theory]
    [InlineData("sans-serif", false, false)]
    [InlineData("system-ui", false, false)]
    [InlineData("ui-sans-serif", false, false)]
    [InlineData("cursive", false, false)]
    [InlineData("fantasy", false, false)]
    [InlineData("SANS-SERIF", false, false)]
    [InlineData("serif", true, false)]
    [InlineData("ui-serif", true, false)]
    [InlineData("Serif", true, false)]
    [InlineData("monospace", false, true)]
    [InlineData("ui-monospace", false, true)]
    [InlineData("MONOSPACE", false, true)]
    public void GenericFontFamilies_TryClassify_Keywords_AreClassifiedWithAndWithoutAliases(string name, bool serif, bool fixedPitch)
    {
        foreach (var includeAliases in new[] { false, true })
        {
            var recognized = GenericFontFamilies.TryClassify(name, includeAliases, out var isSerif, out var isFixedPitch);

            Assert.True(recognized);
            Assert.Equal((serif, fixedPitch), (isSerif, isFixedPitch));
        }
    }

    /// <summary>
    ///     Proves that the common aliases (including the bundled Liberation family names) are classified only when
    ///     asked for, and that any other name, and a null or empty name, is not classified.
    /// </summary>
    /// <param name="name">The family name.</param>
    /// <param name="serif">Whether the serif design is expected.</param>
    /// <param name="fixedPitch">Whether the monospace design is expected.</param>
    [Theory]
    [InlineData("Arial", false, false)]
    [InlineData("helvetica neue", false, false)]
    [InlineData("Segoe UI", false, false)]
    [InlineData("Liberation Sans", false, false)]
    [InlineData("Times New Roman", true, false)]
    [InlineData("GEORGIA", true, false)]
    [InlineData("Liberation Serif", true, false)]
    [InlineData("Courier New", false, true)]
    [InlineData("Consolas", false, true)]
    [InlineData("Liberation Mono", false, true)]
    public void GenericFontFamilies_TryClassify_Aliases_AreClassifiedOnlyWhenRequested(string name, bool serif, bool fixedPitch)
    {
        var without = GenericFontFamilies.TryClassify(name, includeCommonAliases: false, out var withoutSerif, out var withoutFixed);
        var with = GenericFontFamilies.TryClassify(name, includeCommonAliases: true, out var withSerif, out var withFixed);

        Assert.False(without);
        Assert.False(withoutSerif || withoutFixed);
        Assert.True(with);
        Assert.Equal((serif, fixedPitch), (withSerif, withFixed));
    }

    /// <summary>
    ///     Proves that an unknown, null, empty or padded name is not classified (nothing is trimmed or folded).
    /// </summary>
    /// <param name="name">The family name.</param>
    [Theory]
    [InlineData("No Such Family")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(" serif")]
    [InlineData("'serif'")]
    public void GenericFontFamilies_TryClassify_OtherNames_AreNotClassified(string? name)
    {
        var recognized = GenericFontFamilies.TryClassify(name, includeCommonAliases: true, out var serif, out var fixedPitch);

        Assert.False(recognized);
        Assert.False(serif);
        Assert.False(fixedPitch);
    }

    /// <summary>
    ///     Proves that a family list is split at commas outside quotes, with the quotes and the surrounding white
    ///     space dropped and empty names omitted.
    /// </summary>
    [Fact]
    public void GenericFontFamilies_SplitFamilyList_HonorsQuotesAndDropsEmptyNames()
    {
        Assert.Equal(["Helvetica Neue", "Arial", "sans-serif"], GenericFontFamilies.SplitFamilyList("\"Helvetica Neue\", Arial ,  sans-serif"));
        Assert.Equal(["A, B", "serif"], GenericFontFamilies.SplitFamilyList("'A, B', serif"));
        Assert.Equal(["Times New Roman"], GenericFontFamilies.SplitFamilyList("  Times New Roman  "));
        Assert.Equal(["a", "b"], GenericFontFamilies.SplitFamilyList("a,,b,"));
        Assert.Equal(["It's"], GenericFontFamilies.SplitFamilyList("\"It's\""));
        Assert.Empty(GenericFontFamilies.SplitFamilyList(" , ,"));
        Assert.Empty(GenericFontFamilies.SplitFamilyList(""));
        Assert.Empty(GenericFontFamilies.SplitFamilyList("   "));
        Assert.Empty(GenericFontFamilies.SplitFamilyList(null));
    }

    /// <summary>
    ///     Proves CSS backslash escapes are decoded: an escaped quote or comma neither ends a string nor splits the
    ///     list, an escaped backslash is a backslash, a hexadecimal escape (with its one optional trailing white-space
    ///     character) is that code point, an invalid code point becomes U+FFFD, and a trailing lone backslash is dropped.
    /// </summary>
    [Fact]
    public void GenericFontFamilies_SplitFamilyList_HonorsBackslashEscapes()
    {
        Assert.Equal(["Say \"hi\"", "serif"], GenericFontFamilies.SplitFamilyList("\"Say \\\"hi\\\"\", serif"));
        Assert.Equal(["It's"], GenericFontFamilies.SplitFamilyList("'It\\'s'"));
        Assert.Equal(["Acme, Sans", "serif"], GenericFontFamilies.SplitFamilyList("Acme\\, Sans, serif"));
        Assert.Equal(["a\\b"], GenericFontFamilies.SplitFamilyList("\"a\\\\b\""));
        Assert.Equal(["Acme"], GenericFontFamilies.SplitFamilyList("\\41 cme"));
        Assert.Equal(["Acme"], GenericFontFamilies.SplitFamilyList("\\000041cme"));
        Assert.Equal(["A\u00E9"], GenericFontFamilies.SplitFamilyList("\"A\\e9\""));
        Assert.Equal(["\uFFFD"], GenericFontFamilies.SplitFamilyList("\\110000"));
        Assert.Equal(["abc"], GenericFontFamilies.SplitFamilyList("abc\\"));
    }

    /// <summary>
    ///     Proves a backslash before a newline (line feed, carriage return, form feed or CR LF) is a line
    ///     continuation inside a quoted string - it contributes nothing - and an invalid escape outside one, which
    ///     makes the whole list empty; and that the white space a hexadecimal escape consumes is CSS white space
    ///     (CR LF as one unit), not a no-break space.
    /// </summary>
    [Fact]
    public void GenericFontFamilies_SplitFamilyList_HandlesBackslashNewline()
    {
        // Line continuation inside strings: every newline form joins the lines, CR LF as one
        Assert.Equal(["FooBar"], GenericFontFamilies.SplitFamilyList("\"Foo\\\nBar\""));
        Assert.Equal(["FooBar"], GenericFontFamilies.SplitFamilyList("\"Foo\\\rBar\""));
        Assert.Equal(["FooBar"], GenericFontFamilies.SplitFamilyList("\"Foo\\\fBar\""));
        Assert.Equal(["FooBar"], GenericFontFamilies.SplitFamilyList("\"Foo\\\r\nBar\""));
        Assert.Equal(["AB", "serif"], GenericFontFamilies.SplitFamilyList("'A\\\nB', serif"));

        // Not a valid escape in an identifier: the whole list is invalid
        Assert.Empty(GenericFontFamilies.SplitFamilyList("Foo\\\nBar, serif"));
        Assert.Empty(GenericFontFamilies.SplitFamilyList("serif, Foo\\\r\nBar"));

        // A hexadecimal escape consumes one CSS white space (CR LF is one), and nothing else
        Assert.Equal(["Acme"], GenericFontFamilies.SplitFamilyList("\"\\41\r\ncme\""));
        Assert.Equal(["Acme"], GenericFontFamilies.SplitFamilyList("\"\\41\tcme\""));
        Assert.Equal(["A\u00A0cme"], GenericFontFamilies.SplitFamilyList("\"\\41\u00A0cme\""));
        Assert.Equal(["A\u2028cme"], GenericFontFamilies.SplitFamilyList("\"\\41\u2028cme\""));

        // The existing trailing lone backslash behavior is unchanged
        Assert.Equal(["abc"], GenericFontFamilies.SplitFamilyList("abc\\"));
    }
}
