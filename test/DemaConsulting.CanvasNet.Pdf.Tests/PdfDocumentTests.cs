using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Unit-level tests for the <see cref="PdfDocument"/> parsing internals (tokenizer, object
///     model, cross-reference resolution, page-tree traversal) and public API surface.
///     Complements <see cref="PdfSystemIntegrationTests"/>, which proves the same behaviors
///     end-to-end through the public API only.
/// </summary>
public class PdfDocumentTests
{
    /// <summary>The directory containing the hand-authored PDF fixtures (see <c>PdfFixtures\README.md</c>).</summary>
    private static string FixturesPath => Path.Join(AppContext.BaseDirectory, "PdfFixtures");

    private static string Fixture(string name) => Path.Join(FixturesPath, name);

    #region Tokenizer

    /// <summary>Proves that the tokenizer parses integer, negative, leading-dot, and zero number forms.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_Numbers_ParsesIntegerAndRealForms()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("12 -3.5 +.5 0"u8.ToArray());

        // Act
        var first = tokenizer.NextToken();
        var second = tokenizer.NextToken();
        var third = tokenizer.NextToken();
        var fourth = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.Number, first.Kind);
        Assert.Equal(12, first.Number);
        Assert.Equal(-3.5, second.Number);
        Assert.Equal(0.5, third.Number);
        Assert.Equal(0, fourth.Number);
    }

    /// <summary>Proves that literal strings decode nested parentheses and backslash/octal escapes.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_LiteralString_HandlesNestedParensAndEscapes()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("(He said (\\\"hi\\\") \\n \\061)"u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.LiteralString, token.Kind);
        var text = System.Text.Encoding.Latin1.GetString(token.Bytes!);
        Assert.Equal("He said (\"hi\") \n 1", text);
    }

    /// <summary>Proves that a hex string with a full, even digit count decodes to the exact byte sequence.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_HexString_DecodesFullBytes()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("<48656C6C6F>"u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal("Hello", System.Text.Encoding.ASCII.GetString(token.Bytes!));
    }

    /// <summary>Proves that an odd hex-digit count is padded with a trailing zero nibble per spec.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_HexString_PadsOddDigitCountWithTrailingZero()
    {
        // Arrange: an odd number of hex digits is padded with a trailing '0' per the PDF
        // specification, so "<4>" decodes as if it were "<40>" (0x40).
        var tokenizer = new PdfDocument.PdfTokenizer("<4>"u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal([0x40], token.Bytes);
    }

    /// <summary>Proves that <c>#xx</c> hash-escapes within a name token are decoded to the literal character.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_Name_DecodesHashEscapes()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("/A#20Name"u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.Name, token.Kind);
        Assert.Equal("A Name", token.Text);
    }

    /// <summary>Proves that array (<c>[ ]</c>) and dictionary (<c>&lt;&lt; &gt;&gt;</c>) delimiters are recognized as distinct token kinds.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_Delimiters_RecognizesArrayAndDictionaryBrackets()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("[ ] << >>"u8.ToArray());

        // Act & Assert
        Assert.Equal(PdfDocument.PdfTokenKind.ArrayStart, tokenizer.NextToken().Kind);
        Assert.Equal(PdfDocument.PdfTokenKind.ArrayEnd, tokenizer.NextToken().Kind);
        Assert.Equal(PdfDocument.PdfTokenKind.DictStart, tokenizer.NextToken().Kind);
        Assert.Equal(PdfDocument.PdfTokenKind.DictEnd, tokenizer.NextToken().Kind);
    }

    /// <summary>Proves that a <c>%</c>-comment is skipped entirely and does not interrupt subsequent tokenization.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_Comments_AreSkipped()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("% a comment\n42"u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.Number, token.Kind);
        Assert.Equal(42, token.Number);
    }

    /// <summary>Proves that each reserved PDF keyword is recognized verbatim as a <c>Keyword</c> token.</summary>
    /// <param name="keyword">The keyword text under test.</param>
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("obj")]
    [InlineData("endobj")]
    [InlineData("stream")]
    [InlineData("endstream")]
    [InlineData("xref")]
    [InlineData("trailer")]
    [InlineData("startxref")]
    [InlineData("R")]
    public void PdfDocument_Tokenizer_Keywords_AreRecognizedVerbatim(string keyword)
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer(System.Text.Encoding.ASCII.GetBytes(keyword));

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.Keyword, token.Kind);
        Assert.Equal(keyword, token.Text);
    }

    /// <summary>Proves that tokenizing whitespace-only (or empty) input yields an <c>EndOfFile</c> token.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_EndOfInput_ReturnsEndOfFileToken()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("   "u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.EndOfFile, token.Kind);
    }

    #endregion

    #region Object model

    /// <summary>Proves that a dictionary parses nested name/number/dictionary entries by key.</summary>
    [Fact]
    public void PdfDocument_ObjectModel_Dictionary_ParsesNestedEntries()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("<< /Type /Catalog /Count 3 /Nested << /A 1 >> >>"u8.ToArray());

        // Act
        var value = PdfDocument.ParseValue(tokenizer);

        // Assert
        Assert.Equal(PdfDocument.PdfKind.Dictionary, value.Kind);
        Assert.Equal(PdfDocument.PdfKind.Name, value.Get("Type")!.Kind);
        Assert.Equal("Catalog", value.Get("Type")!.Text);
        Assert.Equal(3, value.Get("Count")!.Number);
        Assert.Equal(1, value.Get("Nested")!.Get("A")!.Number);
    }

    /// <summary>Proves that an array parses mixed element kinds (number, name, string, boolean, null, nested array) in order.</summary>
    [Fact]
    public void PdfDocument_ObjectModel_Array_ParsesMixedElementTypes()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("[1 2.5 /Name (str) true null [9]]"u8.ToArray());

        // Act
        var value = PdfDocument.ParseValue(tokenizer);

        // Assert
        Assert.Equal(PdfDocument.PdfKind.Array, value.Kind);
        Assert.Equal(7, value.Items.Count);
        Assert.Equal(PdfDocument.PdfKind.Number, value.Items[0].Kind);
        Assert.Equal(PdfDocument.PdfKind.Name, value.Items[2].Kind);
        Assert.Equal(PdfDocument.PdfKind.Boolean, value.Items[4].Kind);
        Assert.Equal(PdfDocument.PdfKind.Array, value.Items[6].Kind);
    }

    /// <summary>Proves that an <c>N G R</c> sequence parses as an indirect reference, capturing both the object and generation numbers.</summary>
    [Fact]
    public void PdfDocument_ObjectModel_IndirectReference_ParsesObjectAndGenerationNumbers()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("12 0 R"u8.ToArray());

        // Act
        var value = PdfDocument.ParseValue(tokenizer);

        // Assert
        Assert.Equal(PdfDocument.PdfKind.Reference, value.Kind);
        Assert.Equal(12, value.RefNumber);
        Assert.Equal(0, value.RefGeneration);
    }

    /// <summary>Proves that a bare number followed by <c>N obj</c> (an object definition header) is parsed as a plain number, not misread as a reference.</summary>
    [Fact]
    public void PdfDocument_ObjectModel_BareNumber_IsNotMisreadAsReference()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("12 0 obj"u8.ToArray());

        // Act
        var value = PdfDocument.ParseValue(tokenizer);

        // Assert
        Assert.Equal(PdfDocument.PdfKind.Number, value.Kind);
        Assert.Equal(12, value.Number);
    }

    #endregion

    #region Cross-reference forms

    /// <summary>Proves that a classic <c>xref</c>/<c>trailer</c> document resolves the catalog and its single page.</summary>
    [Fact]
    public void PdfDocument_Open_ClassicXref_ResolvesRootAndPage()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Assert
        Assert.Equal(1, document.PageCount);
        var info = document.GetPageInfo(0);
        Assert.Equal(200, info.Width);
        Assert.Equal(300, info.Height);
        Assert.Equal(0, info.Rotation);
    }

    /// <summary>Proves that a <c>/Type /XRef</c> cross-reference stream (no classic table) resolves the catalog and its single page.</summary>
    [Fact]
    public void PdfDocument_Open_XrefStream_ResolvesRootAndPage()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("xref-stream-single-page.pdf"));

        // Assert
        Assert.Equal(1, document.PageCount);
        var info = document.GetPageInfo(0);
        Assert.Equal(200, info.Width);
        Assert.Equal(300, info.Height);
    }

    /// <summary>Proves that a page object compressed inside a <c>/Type /ObjStm</c> object stream is decompressed and resolved correctly.</summary>
    [Fact]
    public void PdfDocument_Open_ObjectStream_DecompressesAndResolvesCompressedPage()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("object-stream.pdf"));

        // Assert
        Assert.Equal(1, document.PageCount);
        var info = document.GetPageInfo(0);
        Assert.Equal(220, info.Width);
        Assert.Equal(320, info.Height);
    }

    /// <summary>Proves that a hybrid classic trailer with an <c>/XRefStm</c> link resolves an entry that only the supplementary xref stream describes.</summary>
    [Fact]
    public void PdfDocument_Open_HybridXref_ResolvesCompressedEntryViaXRefStm()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("hybrid-xref.pdf"));

        // Assert
        Assert.Equal(1, document.PageCount);
        var info = document.GetPageInfo(0);
        Assert.Equal(250, info.Width);
        Assert.Equal(350, info.Height);
    }

    /// <summary>Proves that a document with no <c>startxref</c>/<c>xref</c>/<c>trailer</c> at all still resolves via the linear-scan fallback.</summary>
    [Fact]
    public void PdfDocument_Open_MalformedStartxref_FallsBackToLinearScan()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("malformed-startxref.pdf"));

        // Assert
        Assert.Equal(1, document.PageCount);
        var info = document.GetPageInfo(0);
        Assert.Equal(180, info.Width);
        Assert.Equal(260, info.Height);
    }

    #endregion

    #region Page tree

    /// <summary>Proves that all pages across a multi-level page tree are reported, in document order.</summary>
    [Fact]
    public void PdfDocument_PageTree_Traversal_ReportsAllPagesInDocumentOrder()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));

        // Assert
        Assert.Equal(3, document.PageCount);
    }

    /// <summary>Proves that a page with its own explicit <c>/MediaBox</c> and no <c>/Rotate</c> reports that MediaBox unchanged.</summary>
    [Fact]
    public void PdfDocument_PageTree_Inheritance_ExplicitMediaBoxAndNoRotationIsUnchanged()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));
        var info = document.GetPageInfo(0);

        // Assert
        Assert.Equal(200, info.Width);
        Assert.Equal(300, info.Height);
        Assert.Equal(0, info.Rotation);
    }

    /// <summary>Proves that a <c>/Rotate 90</c> page swaps its displayed width and height relative to its raw <c>/MediaBox</c>.</summary>
    [Fact]
    public void PdfDocument_PageTree_Inheritance_Rotation90SwapsDisplayedWidthAndHeight()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));
        var info = document.GetPageInfo(1);

        // Assert: raw /MediaBox is [0 0 400 100]; /Rotate 90 swaps it for display
        Assert.Equal(100, info.Width);
        Assert.Equal(400, info.Height);
        Assert.Equal(90, info.Rotation);
    }

    /// <summary>Proves that a page omitting its own <c>/MediaBox</c> inherits its ancestor <c>/Pages</c> node's MediaBox.</summary>
    [Fact]
    public void PdfDocument_PageTree_Inheritance_MediaBoxInheritedFromPagesNodeWhenPageOmitsIt()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));
        var info = document.GetPageInfo(2);

        // Assert: the third page declares no MediaBox of its own, so it inherits its ancestor
        // Pages node's box (width 150, height 250); Rotate 180 does not swap width/height.
        Assert.Equal(150, info.Width);
        Assert.Equal(250, info.Height);
        Assert.Equal(180, info.Rotation);
    }

    /// <summary>Proves that a <c>/Kids</c> reference forming a cycle back to an ancestor throws instead of looping forever.</summary>
    [Fact]
    public void PdfDocument_PageTree_CycleRejection_ThrowsInvalidDataException()
    {
        // Arrange, Act & Assert
        Assert.Throws<InvalidDataException>(() => PdfDocument.Open(Fixture("cyclic-page-tree.pdf")));
    }

    #endregion

    #region Encryption detection

    /// <summary>Proves that a trailer containing an <c>/Encrypt</c> key throws <see cref="UnsupportedImageFeatureException"/> without decoding any content.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedTrailer_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<UnsupportedImageFeatureException>(
            () => PdfDocument.Open(Fixture("encrypted-trailer.pdf")));
        Assert.Equal("pdf-encrypted", exception.Feature);
    }

    #endregion

    #region Open validation

    /// <summary>Proves that <see cref="PdfDocument.Open(System.IO.Stream)"/> rejects a null stream.</summary>
    [Fact]
    public void PdfDocument_Open_NullStream_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => PdfDocument.Open((Stream)null!));
    }

    /// <summary>Proves that <see cref="PdfDocument.Open(string)"/> rejects a null path.</summary>
    [Fact]
    public void PdfDocument_Open_NullPath_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => PdfDocument.Open((string)null!));
    }

    /// <summary>Proves that <see cref="PdfDocument.Open(string)"/> rejects an empty or whitespace-only path.</summary>
    /// <param name="path">The invalid path under test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PdfDocument_Open_EmptyOrWhitespacePath_ThrowsArgumentException(string path)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => PdfDocument.Open(path));
    }

    #endregion

    #region GetPageInfo validation

    /// <summary>Proves that <see cref="PdfDocument.GetPageInfo"/> rejects a negative or too-large page index.</summary>
    /// <param name="pageIndex">The out-of-range page index under test.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void PdfDocument_GetPageInfo_OutOfRangeIndex_ThrowsArgumentOutOfRangeException(int pageIndex)
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetPageInfo(pageIndex));
    }

    #endregion

    #region Render

    /// <summary>Proves that <see cref="PdfDocument.Render"/> returns a fully transparent surface of the caller-requested size (Phase 1 renders no content).</summary>
    [Fact]
    public void PdfDocument_Render_ValidPageIndex_ReturnsCorrectlySizedBlankSurface()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act
        using var surface = document.Render(0, 64, 48);

        // Assert
        Assert.Equal(64, surface.Width);
        Assert.Equal(48, surface.Height);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }

    /// <summary>Proves that <see cref="PdfDocument.Render"/> rejects an out-of-range page index the same way <see cref="PdfDocument.GetPageInfo"/> does.</summary>
    [Fact]
    public void PdfDocument_Render_OutOfRangePageIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(5, 10, 10));
    }

    /// <summary>Proves that <see cref="PdfDocument.Render"/> lets <see cref="Canvas.Surface"/>'s own constructor validate width/height rather than duplicating that check.</summary>
    [Fact]
    public void PdfDocument_Render_InvalidWidth_PropagatesSurfaceArgumentOutOfRangeException()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 0, 10));
    }

    #endregion

    #region Dispose

    /// <summary>Proves that calling <see cref="PdfDocument.Dispose"/> a second time is a no-op rather than throwing.</summary>
    [Fact]
    public void PdfDocument_Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act
        var exception = Record.Exception(() =>
        {
            document.Dispose();
            document.Dispose();
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Proves that <see cref="PdfDocument.PageCount"/> throws <see cref="ObjectDisposedException"/> once the document is disposed.</summary>
    [Fact]
    public void PdfDocument_PageCount_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));
        document.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => document.PageCount);
    }

    /// <summary>Proves that <see cref="PdfDocument.GetPageInfo"/> throws <see cref="ObjectDisposedException"/> once the document is disposed.</summary>
    [Fact]
    public void PdfDocument_GetPageInfo_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));
        document.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => document.GetPageInfo(0));
    }

    /// <summary>Proves that <see cref="PdfDocument.Render"/> throws <see cref="ObjectDisposedException"/> once the document is disposed.</summary>
    [Fact]
    public void PdfDocument_Render_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));
        document.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => document.Render(0, 10, 10));
    }

    #endregion
}
