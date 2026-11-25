using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Syntax;

/// <summary>
///     Unit tests for <see cref="CharacterReferences"/>: the rewrite of named references, where it applies, its errors,
///     the mapping of positions back to the original text, and the built-in table.
/// </summary>
public class CharacterReferencesTests
{
    /// <summary>
    ///     Validates that a named reference becomes a numeric reference.
    /// </summary>
    [Fact]
    public void CharacterReferences_Rewrite_NamedReference_BecomesNumericReference()
    {
        // Act
        var result = CharacterReferences.Rewrite("<mi>&alpha;</mi>", null);

        // Assert
        Assert.Equal("<mi>&#x3B1;</mi>", result.Text);
    }

    /// <summary>
    ///     Validates that a reference of two code points becomes two numeric references.
    /// </summary>
    [Fact]
    public void CharacterReferences_Rewrite_TwoCodePointReference_BecomesTwoReferences()
    {
        // Act
        var result = CharacterReferences.Rewrite("<mo>&nvlt;</mo>", null);

        // Assert
        Assert.Equal("<mo>&#x3C;&#x20D2;</mo>", result.Text);
    }

    /// <summary>
    ///     Validates that the five predefined XML names and numeric references are kept as written.
    /// </summary>
    [Fact]
    public void CharacterReferences_Rewrite_XmlPredefinedNames_AreKept()
    {
        // Arrange
        const string text = "<mo>&lt;&gt;&amp;&quot;&apos;&#x3B1;&#945;</mo>";

        // Act
        var result = CharacterReferences.Rewrite(text, null);

        // Assert
        Assert.Equal(text, result.Text);
    }

    /// <summary>
    ///     Validates that references inside comments, CDATA sections and processing instructions are left alone, even
    ///     unknown ones.
    /// </summary>
    [Fact]
    public void CharacterReferences_Rewrite_InsideCommentCdataAndPi_IsUnchanged()
    {
        // Arrange
        const string text = "<math><!-- &alpha; &bogus; --><mtext><![CDATA[&alpha;]]></mtext><?pi &beta;?></math>";

        // Act
        var result = CharacterReferences.Rewrite(text, null);

        // Assert
        Assert.Equal(text, result.Text);
    }

    /// <summary>
    ///     Validates that references inside attribute values are rewritten, in both kinds of quotes.
    /// </summary>
    [Fact]
    public void CharacterReferences_Rewrite_InsideAttributeValue_IsReplaced()
    {
        // Act
        var result = CharacterReferences.Rewrite("<math alttext=\"&alpha;\" title='&beta;'/>", null);

        // Assert
        Assert.Equal("<math alttext=\"&#x3B1;\" title='&#x3B2;'/>", result.Text);
    }

    /// <summary>
    ///     Validates that an unknown name fails at its ampersand with a hint.
    /// </summary>
    [Fact]
    public void CharacterReferences_Rewrite_UnknownName_FailsAtAmpersand()
    {
        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => CharacterReferences.Rewrite("<math>\r\n  <mi>&Alpha;&alfa;</mi>", "a.mml"));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MathMlLocation("a.mml", 2, 14), ex.Location);
        Assert.Contains("&alfa;", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.Hint);
    }

    /// <summary>
    ///     Validates that a document type declaration stops the rewriting and is recorded with its location.
    /// </summary>
    [Fact]
    public void CharacterReferences_Rewrite_Doctype_StopsRewriting()
    {
        // Arrange
        const string text = "\n<!DOCTYPE math><math><mi>&unknown;</mi></math>";

        // Act
        var result = CharacterReferences.Rewrite(text, null);

        // Assert
        Assert.Equal(text, result.Text);
        Assert.Equal(new MathMlLocation(null, 2, 1), result.DoctypeLocation);
    }

    /// <summary>
    ///     Validates that an XML declaration of an encoding other than UTF-8 or US-ASCII fails at its encoding with a hint,
    ///     and that UTF-8 and US-ASCII are accepted in any letter case.
    /// </summary>
    [Fact]
    public void CharacterReferences_Rewrite_ForeignEncodingDeclaration_FailsAtEncoding()
    {
        // Arrange
        const string foreign = "<?xml version=\"1.0\" encoding=\"windows-1252\"?><math/>";

        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => CharacterReferences.Rewrite(foreign, "f.mml"));
        var utf8 = CharacterReferences.Rewrite("<?xml version=\"1.0\" encoding=\"Utf-8\"?><math/>", null);
        var ascii = CharacterReferences.Rewrite("<?xml version='1.0' encoding='us-ascii'?><math/>", null);

        // Assert
        Assert.Equal((MathMlDiagnosticCodes.SyntaxError, new MathMlLocation("f.mml", 1, 21)), (ex.Code, ex.Location));
        Assert.Contains("UTF-8", ex.Hint, StringComparison.Ordinal);
        Assert.EndsWith("<math/>", utf8.Text, StringComparison.Ordinal);
        Assert.EndsWith("<math/>", ascii.Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that columns after and inside replacements map back to the original columns.
    /// </summary>
    [Fact]
    public void CharacterReferences_MapBack_AfterReplacements_ReturnsOriginalColumn()
    {
        // Arrange: "&alpha;" (7 characters) becomes "&#x3B1;" (7) and "&Bopf;" (6) becomes "&#x1D539;" (9)
        var result = CharacterReferences.Rewrite("<mi>&Bopf;&InvisibleTimes;</mi><mo>x</mo>", null);
        var outputMo = result.Text.IndexOf("<mo>", StringComparison.Ordinal) + 2;
        var originalMo = "<mi>&Bopf;&InvisibleTimes;</mi><mo>x</mo>".IndexOf("<mo>", StringComparison.Ordinal) + 2;

        // Act
        var after = result.Map.MapBack(1, outputMo);
        var inside = result.Map.MapBack(1, 5 + 3);
        var before = result.Map.MapBack(1, 2);

        // Assert
        Assert.Equal(originalMo, after.Column);
        Assert.Equal(5, inside.Column);
        Assert.Equal(2, before.Column);
    }

    /// <summary>
    ///     Validates that a character outside the basic plane counts as two columns (UTF-16 code units), as the XML reader
    ///     counts it, before a replacement on the same line.
    /// </summary>
    [Fact]
    public void CharacterReferences_MapBack_AstralCharacterBeforeReference_CountsUtf16Units()
    {
        // Arrange: U+1D400 takes columns 5 and 6, so the ampersand is at column 7
        const string text = "<mi>\U0001D400&alpha;&bogus;</mi>";

        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => CharacterReferences.Rewrite(text, null));
        var mapped = CharacterReferences.Rewrite("<mi>\U0001D400&Bopf;</mi>", null).Map.MapBack(1, 7 + 9);

        // Assert
        Assert.Equal(14, ex.Location.Column);
        Assert.Equal(13, mapped.Column);
    }

    /// <summary>
    ///     Validates that a lone carriage return counts as one line break, as the XML reader counts it, for the location
    ///     of an error, for the mapping back of a position, and for the position of an index.
    /// </summary>
    [Fact]
    public void CharacterReferences_MapBack_LoneCarriageReturn_CountsAsLineBreak()
    {
        // Arrange: after the lone CR, "&Bopf;" starts at column 5 of line 2 and "&bogus;" at column 11
        const string failing = "<math>\r<mi>&Bopf;&bogus;</mi></math>";
        const string valid = "<math>\r<mi>&Bopf;</mi></math>";

        // Act: "&Bopf;" (6) becomes "&#x1D539;" (9), so output column 14 of line 2 is original column 11
        var ex = Assert.Throws<MathMlSyntaxException>(() => CharacterReferences.Rewrite(failing, "c.mml"));
        var mapped = CharacterReferences.Rewrite(valid, null).Map.MapBack(2, 5 + 9);
        var position = CharacterReferences.PositionOf("a\rb\r\nc", 5);

        // Assert
        Assert.Equal(new MathMlLocation("c.mml", 2, 11), ex.Location);
        Assert.Equal(new MathMlLocation(null, 2, 11), mapped);
        Assert.Equal((3, 1), position);
    }

    /// <summary>
    ///     Validates the built-in table: the full W3C HTML+MathML set of 2125 names, with its two-code-point and
    ///     double-escaped values.
    /// </summary>
    [Fact]
    public void CharacterReferences_Table_HoldsTheFullW3cSet()
    {
        // Act / Assert
        Assert.Equal(2125, CharacterReferences.Count);
        Assert.True(CharacterReferences.TryGetValue("nvlt", out var nvlt));
        Assert.Equal("\u003C\u20D2", nvlt);
        Assert.True(CharacterReferences.TryGetValue("amp", out var amp));
        Assert.Equal("&", amp);
        Assert.True(CharacterReferences.TryGetValue("InvisibleTimes", out var invisibleTimes));
        Assert.Equal("\u2062", invisibleTimes);
        Assert.False(CharacterReferences.TryGetValue("ALPHA", out _));
    }
}
