using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tests.TestSupport;

// cspell:ignore agrave

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

    /// <summary>The opaque black color every Phase 2 fill/stroke operator paints with.</summary>
    private static readonly Canvas.Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>
    ///     Builds an in-memory, single-page, classic-xref PDF (matching
    ///     <c>PdfFixtures\README.md</c>'s hand-authored template) whose one page declares the
    ///     given <c>/MediaBox</c>, optional <c>/Rotate</c>, and a single <c>/Contents</c> stream
    ///     holding <paramref name="content"/> verbatim - used by tests that only need arbitrary
    ///     content-stream text against a fixed page size, without a dedicated fixture file on disk.
    /// </summary>
    private static byte[] BuildSinglePagePdf(double mediaBoxWidth, double mediaBoxHeight, string content, int rotate = 0)
    {
        var contentBytes = System.Text.Encoding.ASCII.GetBytes(content);
        var rotateEntry = rotate == 0 ? string.Empty : $" /Rotate {rotate}";
        var streamHeader = System.Text.Encoding.ASCII.GetBytes($"<< /Length {contentBytes.Length} >>\nstream\n");
        var streamFooter = "\nendstream"u8.ToArray();
        var streamBody = new byte[streamHeader.Length + contentBytes.Length + streamFooter.Length];
        streamHeader.CopyTo(streamBody, 0);
        contentBytes.CopyTo(streamBody, streamHeader.Length);
        streamFooter.CopyTo(streamBody, streamHeader.Length + contentBytes.Length);

        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            System.Text.Encoding.ASCII.GetBytes(
                $"<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 {mediaBoxWidth} {mediaBoxHeight}] >>"),
            System.Text.Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R{rotateEntry} /Contents 4 0 R >>"),
            streamBody,
        };

        var buffer = new List<byte>();
        buffer.AddRange("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange("\nendobj\n"u8.ToArray());
        }

        var xrefOffset = buffer.Count;
        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n"));
        buffer.AddRange("0000000000 65535 f \n"u8.ToArray());
        foreach (var offset in offsets)
        {
            buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
        }

        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
        return [.. buffer];
    }

    /// <summary>
    ///     Renders <paramref name="content"/> against a single-page PDF built by
    ///     <see cref="BuildSinglePagePdf"/>, returning the resulting <see cref="Canvas.Surface"/>.
    /// </summary>
    private static Canvas.Surface RenderContent(
        string content,
        int width = 100,
        int height = 100,
        double mediaBoxWidth = 100,
        double mediaBoxHeight = 100,
        int rotate = 0)
    {
        var bytes = BuildSinglePagePdf(mediaBoxWidth, mediaBoxHeight, content, rotate);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        return document.Render(0, width, height);
    }

    /// <summary>
    ///     Builds an in-memory, single-page, classic-xref PDF exactly like
    ///     <see cref="BuildSinglePagePdf"/>, but with the page declaring a
    ///     <c>/Resources &lt;&lt; ... &gt;&gt;</c> dictionary (<paramref name="resourcesBody"/>,
    ///     inserted verbatim between the outer <c>&lt;&lt; &gt;&gt;</c>) and 0 or more extra
    ///     indirect objects (<paramref name="extraObjectBodies"/>, numbered <c>5 0 obj</c>,
    ///     <c>6 0 obj</c>, ... in order) that <paramref name="resourcesBody"/> may reference by
    ///     number - used by every color-space-resource and image-XObject test that needs a
    ///     <c>/Resources</c> dictionary (plain color-operator tests needing no <c>/Resources</c>
    ///     keep using <see cref="BuildSinglePagePdf"/>/<see cref="RenderContent"/> unchanged).
    /// </summary>
    private static byte[] BuildSinglePagePdfWithResources(
        double mediaBoxWidth,
        double mediaBoxHeight,
        string content,
        string resourcesBody,
        IReadOnlyList<byte[]> extraObjectBodies,
        int rotate = 0)
    {
        var contentBytes = System.Text.Encoding.ASCII.GetBytes(content);
        var rotateEntry = rotate == 0 ? string.Empty : $" /Rotate {rotate}";
        var streamHeader = System.Text.Encoding.ASCII.GetBytes($"<< /Length {contentBytes.Length} >>\nstream\n");
        var streamFooter = "\nendstream"u8.ToArray();
        var streamBody = new byte[streamHeader.Length + contentBytes.Length + streamFooter.Length];
        streamHeader.CopyTo(streamBody, 0);
        contentBytes.CopyTo(streamBody, streamHeader.Length);
        streamFooter.CopyTo(streamBody, streamHeader.Length + contentBytes.Length);

        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            System.Text.Encoding.ASCII.GetBytes(
                $"<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 {mediaBoxWidth} {mediaBoxHeight}] >>"),
            System.Text.Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R{rotateEntry} /Contents 4 0 R /Resources << {resourcesBody} >> >>"),
            streamBody,
        };
        bodies.AddRange(extraObjectBodies);

        var buffer = new List<byte>();
        buffer.AddRange("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange("\nendobj\n"u8.ToArray());
        }

        var xrefOffset = buffer.Count;
        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n"));
        buffer.AddRange("0000000000 65535 f \n"u8.ToArray());
        foreach (var offset in offsets)
        {
            buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
        }

        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
        return [.. buffer];
    }

    /// <summary>
    ///     Builds an indirect-object body (dictionary header + raw stream bytes) for use as one of
    ///     <see cref="BuildSinglePagePdfWithResources"/>'s <c>extraObjectBodies</c> - the
    ///     <paramref name="dictionaryEntries"/> text is inserted verbatim between the dictionary's
    ///     outer <c>&lt;&lt; &gt;&gt;</c> alongside an automatically computed <c>/Length</c>.
    /// </summary>
    private static byte[] BuildStreamObjectBody(string dictionaryEntries, byte[] streamData)
    {
        var header = System.Text.Encoding.ASCII.GetBytes($"<< {dictionaryEntries} /Length {streamData.Length} >>\nstream\n");
        var footer = "\nendstream"u8.ToArray();
        var body = new byte[header.Length + streamData.Length + footer.Length];
        header.CopyTo(body, 0);
        streamData.CopyTo(body, header.Length);
        footer.CopyTo(body, header.Length + streamData.Length);
        return body;
    }

    /// <summary>Compresses <paramref name="data"/> into a standards-conformant zlib stream (header + deflate + Adler-32), as <c>FlateDecode</c> expects.</summary>
    private static byte[] ZlibCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    /// <summary>Renders page 0 of an in-memory PDF built by <see cref="BuildSinglePagePdfWithResources"/>.</summary>
    private static Canvas.Surface RenderPdfBytes(byte[] bytes, int width = 100, int height = 100)
    {
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        return document.Render(0, width, height);
    }

    /// <summary>
    ///     Builds a minimal, well-formed synthetic embedded TrueType font (see
    ///     <see cref="SyntheticFontBuilder"/>): a 1000-unit em square, glyph 0 the
    ///     (empty-outline) <c>.notdef</c>, and every glyph from index 1 onward a filled square
    ///     outline spanning font-design-space <c>(100, 100)</c>-<c>(500, 500)</c> with a fixed
    ///     600-unit advance width - used by every Phase 4 unit test that needs a real, loadable
    ///     <c>TrueTypeFont</c> without depending on a real-world font file (see
    ///     <see cref="PdfSystemIntegrationTests"/> for the one required real-embedded-font,
    ///     real-glyph-shape pixel-level test, using the shared Open Sans fixture instead).
    /// </summary>
    /// <param name="cmapMappings">The <c>cmap</c> table's codepoint-to-glyph-index mappings.</param>
    /// <param name="glyphCount">The total number of glyphs (including glyph 0).</param>
    private static byte[] BuildEmbeddedFontBytes(
        IReadOnlyList<(int Codepoint, int GlyphId)> cmapMappings,
        int glyphCount = 3)
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(100, 100, true), (500, 100, true), (500, 500, true), (100, 500, true)],
        ]);

        var glyphLengths = new List<int> { 0 };
        var glyf = new List<byte>();
        var advanceWidths = new List<int> { 0 };
        for (var glyphIndex = 1; glyphIndex < glyphCount; glyphIndex++)
        {
            glyphLengths.Add(square.Length);
            glyf.AddRange(square);
            advanceWidths.Add(600);
        }

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, cmapMappings);

        return new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(glyphCount))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, glyphCount))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx(advanceWidths))
            .AddTable("loca", SyntheticFontBuilder.Loca(glyphLengths, longFormat: false))
            .AddTable("glyf", [.. glyf])
            .AddTable("cmap", cmap)
            .Build();
    }

    /// <summary>
    ///     Builds a <c>/Resources/Font</c> dictionary (as a <see cref="BuildSinglePagePdfWithResources"/>-
    ///     compatible <c>resourcesBody</c>/<c>extraObjectBodies</c> pair) declaring a single simple
    ///     TrueType font resource named <c>/F1</c>, with the given embedded <c>/FontFile2</c> bytes
    ///     and font-dictionary/font-descriptor entries appended verbatim.
    /// </summary>
    /// <remarks>
    ///     Numbered so <paramref name="fontFileBytes"/> is always object <c>7</c> (referenced as
    ///     <c>7 0 R</c> by the descriptor, object <c>6</c>, in turn referenced as <c>6 0 R</c> by
    ///     the font dictionary, object <c>5</c>, in turn referenced as <c>5 0 R</c> by the
    ///     returned <c>/Font</c> resources entry) - matching every other Phase 3 image-XObject
    ///     test's own "extra objects start at 5" convention.
    /// </remarks>
    private static (string ResourcesBody, List<byte[]> ExtraObjects) BuildSimpleTrueTypeFontResources(
        byte[] fontFileBytes,
        string fontDictExtra = "/FirstChar 65 /LastChar 66 /Widths [600 600]",
        string descriptorExtra = "",
        string fontResourceName = "F1")
    {
        var fontFileObj = BuildStreamObjectBody(string.Empty, fontFileBytes);
        var descriptorObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /FontDescriptor /FontName /Test {descriptorExtra} /FontFile2 7 0 R >>");
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /Font /Subtype /TrueType /BaseFont /Test {fontDictExtra} /FontDescriptor 6 0 R >>");

        return ($"/Font << /{fontResourceName} 5 0 R >>", [fontDictObj, descriptorObj, fontFileObj]);
    }

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

    #region ContentStream

    /// <summary>Proves that an unknown/unimplemented operator is silently skipped and does not stop subsequent operators from executing.</summary>
    [Fact]
    public void PdfDocument_ContentStream_UnknownOperator_IsSkippedWithoutThrowing()
    {
        // Arrange
        const string content = "/GS1 gs 2 w 10 10 m 10 90 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert: the unrecognized 'gs' (ExtGState) operator was skipped, and the stroke after it still painted.
        Assert.Equal(Black, surface[10, 50]);
    }

    /// <summary>Proves that a <c>/Contents</c> array of streams is concatenated with a space separator, rather than merging adjacent tokens.</summary>
    [Fact]
    public void PdfDocument_ContentStream_ContentsArray_ConcatenatesStreamsWithSpaceSeparator()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("contents-array-two-streams.pdf"));

        // Act: stream 4 holds "10 10 40" and stream 5 holds "40 re f"; correct concatenation
        // with a space separator reconstructs "10 10 40 40 re f" (a filled 40x40 square).
        using var surface = document.Render(0, 100, 100);

        // Assert
        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[90, 90]);
    }

    /// <summary>Proves that a page with no <c>/Contents</c> key at all renders as a fully blank surface, without throwing.</summary>
    [Fact]
    public void PdfDocument_ContentStream_NoContents_RendersBlankSurface()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("no-contents-page.pdf"));

        // Act
        using var surface = document.Render(0, 20, 20);

        // Assert
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }

    #endregion

    #region GraphicsState

    /// <summary>Proves that <c>q</c>/<c>cm</c>/<c>Q</c> restores the prior transform after the matching pop.</summary>
    [Fact]
    public void PdfDocument_GraphicsState_QPushCmThenQPop_RestoresPriorTransform()
    {
        // Arrange: inside q/Q, 'cm' doubles the scale, so the first diagonal segment is drawn
        // through a different device location than the second, identical, segment issued after Q.
        const string content = "2 w q 2 0 0 2 0 0 cm 10 10 m 20 20 l S Q 10 10 m 20 20 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert: the second segment (after Q) is drawn at the unscaled location - proving the
        // CTM was restored, not left doubled.
        Assert.NotEqual(default, surface[15, 85]);
    }

    /// <summary>Proves that nested <c>q</c>/<c>cm</c>/<c>q</c>/<c>cm</c>/.../<c>Q</c>/<c>Q</c> composes both matrices, in order, onto the same drawing operation.</summary>
    [Fact]
    public void PdfDocument_GraphicsState_NestedQQ_ComposesTransformsInOrder()
    {
        // Arrange: an outer cm scales X only (x2), an inner cm scales Y only (x2); only their
        // composition scales both axes, placing the drawn segment at (30,70)-ish. A bug applying
        // only one of the two cm's would place it at (30,85) (X-only) or (15,70) (Y-only) instead.
        const string content = "2 w q 2 0 0 1 0 0 cm q 1 0 0 2 0 0 cm 10 10 m 20 20 l S Q Q";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.NotEqual(default, surface[30, 70]);
        Assert.Equal(default, surface[30, 85]);
        Assert.Equal(default, surface[15, 70]);
    }

    /// <summary>Proves that an unbalanced <c>Q</c> with no matching prior <c>q</c> is tolerated as a no-op, rather than throwing.</summary>
    [Fact]
    public void PdfDocument_GraphicsState_UnbalancedQWithNoMatchingPush_DoesNotThrow()
    {
        // Arrange
        const string content = "Q 2 w 10 10 m 20 20 l S";

        // Act
        var exception = Record.Exception(() => RenderContent(content).Dispose());

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Proves that a malformed <c>cm</c> operand count throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_GraphicsState_MalformedCmOperandCount_ThrowsInvalidDataException()
    {
        // Arrange
        const string content = "1 2 3 cm";

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    #endregion

    #region PathOps

    /// <summary>Proves that <c>m</c>/<c>l</c>/<c>c</c>/<c>v</c>/<c>y</c>/<c>re</c> each build the geometry the PDF specification documents for them.</summary>
    [Fact]
    public void PdfDocument_PathOps_MoveLineRectCurve_BuildExpectedGeometry()
    {
        // m/l/h: an explicit square built from move/line/closepath, filled.
        using (var surface = RenderContent("10 10 m 90 10 l 90 90 l 10 90 l h f"))
        {
            Assert.Equal(Black, surface[50, 50]);
        }

        // re: a rectangle built via the single-operator shorthand, filled.
        using (var surface = RenderContent("20 20 30 30 re f"))
        {
            Assert.Equal(Black, surface[30, 65]);
            Assert.Equal(default, surface[5, 5]);
        }

        // c: a full cubic Bezier curve with two explicit control points, stroked.
        using (var surface = RenderContent("3 w 10 50 m 10 10 90 10 90 50 c S"))
        {
            Assert.Equal(Black, surface[50, 80]);
        }

        // v: the first control point defaults to the current point.
        using (var surface = RenderContent("3 w 10 50 m 90 10 90 50 v S"))
        {
            Assert.Equal(Black, surface[50, 65]);
        }

        // y: the second control point defaults to the curve's own endpoint.
        using (var surface = RenderContent("3 w 10 50 m 30 10 90 50 y S"))
        {
            Assert.Equal(Black, surface[58, 65]);
        }
    }

    /// <summary>Proves that a malformed operand count for a path-construction operator throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("m")]
    [InlineData("1 m")]
    [InlineData("1 2 3 m")]
    [InlineData("10 10 m 1 l")]
    [InlineData("10 10 m 1 2 3 4 5 c")]
    [InlineData("10 10 m 1 2 3 v")]
    [InlineData("10 10 m 1 2 3 y")]
    [InlineData("1 2 3 re")]
    public void PdfDocument_PathOps_MalformedOperandCount_ThrowsInvalidDataException(string content)
    {
        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    /// <summary>Proves that issuing a draw operator before any <c>m</c>/<c>re</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_PathOps_DrawBeforeMoveTo_ThrowsInvalidDataException()
    {
        // Arrange
        const string content = "10 10 l";

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    /// <summary>Proves that <c>f</c> fills using the nonzero winding rule.</summary>
    [Fact]
    public void PdfDocument_PathOps_FillNonZero_PaintsExpectedPixels()
    {
        // Arrange
        const string content = "10 10 40 40 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves that <c>f*</c> fills using the even-odd rule, leaving a nested same-winding rectangle unfilled as a "hole".</summary>
    [Fact]
    public void PdfDocument_PathOps_FillEvenOdd_PaintsExpectedPixels()
    {
        // Arrange
        const string content = "10 10 80 80 re 30 30 40 40 re f*";

        // Act
        using var surface = RenderContent(content);

        // Assert: the outer ring is filled, but the doubly-covered inner region is not (even-odd hole).
        Assert.Equal(Black, surface[15, 50]);
        Assert.Equal(default, surface[50, 50]);
    }

    /// <summary>Proves that <c>S</c> strokes the current path without filling it.</summary>
    [Fact]
    public void PdfDocument_PathOps_Stroke_PaintsExpectedPixels()
    {
        // Arrange
        const string content = "2 w 60 10 m 60 90 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(Black, surface[60, 50]);
        Assert.Equal(default, surface[70, 50]);
    }

    /// <summary>Proves that <c>b</c> closes the current (still-open) subpath before both filling and stroking it.</summary>
    [Fact]
    public void PdfDocument_PathOps_CloseAndFillAndStroke_PaintsExpectedPixels()
    {
        // Arrange: an open triangle (no 'h'); 'b' must close it before painting.
        const string closed = "2 w 20 20 m 80 20 l 50 70 l b";
        const string open = "2 w 20 20 m 80 20 l 50 70 l S";

        // Act
        using var closedSurface = RenderContent(closed);
        using var openSurface = RenderContent(open);

        // Assert: the interior is filled, and the implicit closing edge is stroked - proven by
        // comparison against a plain 'S' (no closing), which leaves that same pixel unpainted.
        Assert.Equal(Black, closedSurface[50, 63]);
        Assert.Equal(Black, closedSurface[35, 55]);
        Assert.Equal(default, openSurface[35, 55]);
    }

    /// <summary>Proves that <c>n</c> discards the current path without painting anything.</summary>
    [Fact]
    public void PdfDocument_PathOps_NoOp_DiscardsPathWithoutPainting()
    {
        // Arrange
        const string content = "10 10 40 40 re n";

        // Act
        using var surface = RenderContent(content);

        // Assert
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }

    /// <summary>Proves that a path-painting operator clears the current path, but leaves the surrounding graphics state (CTM/line width) untouched for the next path.</summary>
    [Fact]
    public void PdfDocument_PathOps_PaintOperator_ClearsPathButPreservesGraphicsState()
    {
        // Arrange: the first rectangle is filled, then a second, unrelated rectangle is filled
        // under the same (still-scaled) graphics state - if the first path were not cleared, the
        // second 're' would incorrectly append to (rather than replace) it.
        const string content = "2 0 0 2 0 0 cm 5 5 10 10 re f 20 20 10 10 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert: second rectangle (user 20..30,20..30, scaled x2 = device 40..60, then flipped
        // y => device y 40..60) is painted, and the first rectangle's area does not bleed into it.
        Assert.Equal(Black, surface[50, 50]);
        Assert.Equal(default, surface[15, 15]);
    }

    #endregion

    #region Color

    /// <summary>Proves that <c>g</c> sets the fill color to the expected gray RGBA value.</summary>
    [Fact]
    public void PdfDocument_Color_SetGrayFill_SetsExpectedRgbaColor()
    {
        // Arrange: 0.5 gray rounds (round-half-to-even) to byte 128.
        const string content = "0.5 g 10 10 80 80 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(128, 128, 128, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>G</c> sets the stroke color to the expected gray RGBA value.</summary>
    [Fact]
    public void PdfDocument_Color_SetGrayStroke_SetsExpectedRgbaColor()
    {
        // Arrange
        const string content = "0.5 G 4 w 10 50 m 90 50 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(128, 128, 128, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>rg</c> sets the fill color to the expected RGB value.</summary>
    [Fact]
    public void PdfDocument_Color_SetRgbFill_SetsExpectedRgbaColor()
    {
        // Arrange
        const string content = "1 0 0 rg 10 10 80 80 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>RG</c> sets the stroke color to the expected RGB value.</summary>
    [Fact]
    public void PdfDocument_Color_SetRgbStroke_SetsExpectedRgbaColor()
    {
        // Arrange
        const string content = "0 1 0 RG 4 w 10 50 m 90 50 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>k</c> converts CMYK to the expected RGB fill color.</summary>
    [Fact]
    public void PdfDocument_Color_SetCmykFill_ConvertsToExpectedRgbaColor()
    {
        // Arrange: C=0 M=1 Y=1 K=0 -> R=255*(1-0)*(1-0)=255, G=255*(1-1)*(1-0)=0, B=255*(1-1)*(1-0)=0 (red).
        const string content = "0 1 1 0 k 10 10 80 80 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>K</c> converts CMYK to the expected RGB stroke color.</summary>
    [Fact]
    public void PdfDocument_Color_SetCmykStroke_ConvertsToExpectedRgbaColor()
    {
        // Arrange: C=1 M=0 Y=0 K=0 -> R=0, G=255, B=255 (cyan).
        const string content = "1 0 0 0 K 4 w 10 50 m 90 50 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 255, 255, 255), surface[50, 50]);
    }

    /// <summary>Proves that color-component values outside <c>[0, 1]</c> are clamped, not rejected.</summary>
    [Theory]
    [InlineData("-1 2 -0.5 rg 10 10 80 80 re f", 0, 255, 0)]
    [InlineData("2 -1 -1 rg 10 10 80 80 re f", 255, 0, 0)]
    public void PdfDocument_Color_ComponentValuesOutsideZeroToOne_AreClamped(string content, byte r, byte g, byte b)
    {
        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(r, g, b, 255), surface[50, 50]);
    }

    /// <summary>Proves that a malformed operand count for every device color operator throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("0.5 0.5 g")]
    [InlineData("0.5 0.5 G")]
    [InlineData("1 0 rg")]
    [InlineData("1 0 RG")]
    [InlineData("1 0 0 k")]
    [InlineData("1 0 0 K")]
    [InlineData("/DeviceRGB /DeviceGray cs")]
    [InlineData("/DeviceRGB /DeviceGray CS")]
    [InlineData("1 0 sc")]
    [InlineData("1 0 SC")]
    [InlineData("1 0 scn")]
    [InlineData("1 0 SCN")]
    public void PdfDocument_Color_MalformedOperandCount_ThrowsInvalidDataException(string content)
    {
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    /// <summary>Proves that <c>cs</c> accepts the three device color-space names and resets the fill color to black.</summary>
    [Theory]
    [InlineData("DeviceGray")]
    [InlineData("DeviceRGB")]
    [InlineData("DeviceCMYK")]
    public void PdfDocument_Color_SetColorSpaceFill_DeviceNames_ResetsColorToBlack(string colorSpaceName)
    {
        // Arrange: paint red first, then switch color space (resetting to black), then fill.
        var content = $"1 0 0 rg 10 10 40 80 re f /{colorSpaceName} cs 60 10 20 80 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[20, 50]);
        Assert.Equal(Black, surface[70, 50]);
    }

    /// <summary>Proves that <c>CS</c> accepts the three device color-space names and resets the stroke color to black.</summary>
    [Theory]
    [InlineData("DeviceGray")]
    [InlineData("DeviceRGB")]
    [InlineData("DeviceCMYK")]
    public void PdfDocument_Color_SetColorSpaceStroke_DeviceNames_ResetsColorToBlack(string colorSpaceName)
    {
        // Arrange: stroke red first, then switch stroke color space (resetting to black), then stroke again.
        var content = $"1 0 0 RG 4 w 10 30 m 90 30 l S /{colorSpaceName} CS 10 70 m 90 70 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert: PDF y-up user space flips to device y-down, so user y=30 -> device y=70, and
        // user y=70 -> device y=30.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 70]);
        Assert.Equal(Black, surface[50, 30]);
    }

    /// <summary>Proves that <c>sc</c> paints using the current fill color space's component count.</summary>
    [Fact]
    public void PdfDocument_Color_SetColorFillUsingCurrentColorSpace_Sc_PaintsExpectedColor()
    {
        // Arrange: switch to DeviceRGB (resets to black), then 'sc' with 3 components.
        const string content = "/DeviceRGB cs 0 0 1 sc 10 10 80 80 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>SC</c> paints using the current stroke color space's component count.</summary>
    [Fact]
    public void PdfDocument_Color_SetColorStrokeUsingCurrentColorSpace_SC_PaintsExpectedColor()
    {
        // Arrange
        const string content = "/DeviceRGB CS 0 0 1 SC 4 w 10 50 m 90 50 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>scn</c> with a trailing pattern name throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Color_ScnWithPatternName_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        const string content = "/P0 scn";

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderContent(content));
    }

    /// <summary>Proves that an unsupported named color space (resolved via <c>/Resources/ColorSpace</c>) throws <see cref="UnsupportedImageFeatureException"/> when selected with <c>cs</c>.</summary>
    [Theory]
    [InlineData("[/Indexed /DeviceRGB 1 <00FFFFFF>]")]
    [InlineData("[/Separation /Spot /DeviceGray 4 0 R]")]
    [InlineData("[/DeviceN [/Spot] /DeviceGray 4 0 R]")]
    [InlineData("[/ICCBased 4 0 R]")]
    [InlineData("[/CalRGB << >>]")]
    [InlineData("[/CalGray << >>]")]
    [InlineData("[/Lab << >>]")]
    public void PdfDocument_Color_UnsupportedNamedColorSpace_ThrowsUnsupportedImageFeatureException(string colorSpaceArray)
    {
        // Arrange: an unused placeholder function stream, referenced only when the color space
        // array under test declares /Separation or /DeviceN (both name a tint-transform function
        // by indirect reference); the other cases in this theory never dereference object 5.
        var functionStream = BuildStreamObjectBody("/FunctionType 4", "{ }"u8.ToArray());
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/CS0 cs",
            $"/ColorSpace << /CS0 {colorSpaceArray} >>",
            [functionStream]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    #endregion

    #region Filters

    /// <summary>Proves that a <c>FlateDecode</c>+PNG-predictor image XObject decodes the expected raw pixels.</summary>
    [Fact]
    public void PdfDocument_Images_FlateDecodePngPredictor_DecodesExpectedPixels()
    {
        // Arrange: a 2x2 DeviceRGB image, PNG predictor 15 (adaptive). Row 0 uses filter type 0
        // (None): raw pixels (255,0,0) (0,255,0). Row 1 uses filter type 2 (Up): stored as the
        // difference from row 0, encoding raw pixels (0,0,255) (255,255,0).
        byte[] row0 = [0, 255, 0, 0, 0, 255, 0];
        byte[] row1raw = [0, 0, 255, 255, 255, 0];
        var row1Filtered = new byte[row1raw.Length];
        for (var i = 0; i < row1raw.Length; i++)
        {
            row1Filtered[i] = (byte)(row1raw[i] - row0[i + 1]);
        }

        var rawRows = new List<byte>();
        rawRows.AddRange(row0);
        rawRows.Add(2); // filter type: Up
        rawRows.AddRange(row1Filtered);
        var compressed = ZlibCompress([.. rawRows]);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 "
            + "/Filter /FlateDecode /DecodeParms << /Predictor 15 /Colors 3 /BitsPerComponent 8 /Columns 2 >>",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: row 0 (top of unit square) -> device top half; row 1 -> device bottom half.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(255, 255, 0, 255), surface[75, 75]);
    }

    /// <summary>Proves that a <c>FlateDecode</c>+TIFF-predictor image XObject decodes the expected raw pixels.</summary>
    [Fact]
    public void PdfDocument_Images_FlateDecodeTiffPredictor_DecodesExpectedPixels()
    {
        // Arrange: a 2x2 DeviceGray image, TIFF predictor 2. Raw pixels per row: (10, 200).
        // TIFF-encoded: byte 0 verbatim, byte 1 = raw[1] - raw[0].
        byte[] rawRow = [10, 200];
        var encodedRow = new byte[] { rawRow[0], (byte)(rawRow[1] - rawRow[0]) };
        var rawBytes = new List<byte>();
        rawBytes.AddRange(encodedRow);
        rawBytes.AddRange(encodedRow);
        var compressed = ZlibCompress([.. rawBytes]);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 "
            + "/Filter /FlateDecode /DecodeParms << /Predictor 2 /Colors 1 /BitsPerComponent 8 /Columns 2 >>",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: both source pixels in a row decode to (10) then (200); both rows identical.
        Assert.Equal(new Canvas.Rgba32(10, 10, 10, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(200, 200, 200, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(10, 10, 10, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(200, 200, 200, 255), surface[75, 75]);
    }

    /// <summary>Proves that an image XObject declaring an unsupported filter throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_UnsupportedFilter_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /LZWDecode",
            [1, 2, 3, 4]);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    #endregion

    #region Images

    /// <summary>Proves that <c>Do</c> decodes and places a raw 8-bit <c>DeviceGray</c> <c>FlateDecode</c> image XObject at the expected device pixels.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_DeviceGrayFlateDecode_PlacesExpectedPixels()
    {
        // Arrange: a 2x2 DeviceGray image: row0 = (0, 255), row1 = (64, 192).
        byte[] raw = [0, 255, 64, 192];
        var compressed = ZlibCompress(raw);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(255, 255, 255, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(64, 64, 64, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(192, 192, 192, 255), surface[75, 75]);
    }

    /// <summary>Proves that <c>Do</c> decodes and places a raw 8-bit <c>DeviceRGB</c> <c>FlateDecode</c> image XObject at the expected device pixels.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_DeviceRgbFlateDecode_PlacesExpectedPixels()
    {
        // Arrange: a 2x2 DeviceRGB image: row0 = red, green; row1 = blue, white.
        byte[] raw = [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255];
        var compressed = ZlibCompress(raw);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(255, 255, 255, 255), surface[75, 75]);
    }

    /// <summary>Proves that <c>Do</c> decodes and places a raw 8-bit <c>DeviceCMYK</c> <c>FlateDecode</c> image XObject at the expected device pixels.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_DeviceCmykFlateDecode_PlacesExpectedPixels()
    {
        // Arrange: a single-pixel 1x1 DeviceCMYK image: C=0 M=1 Y=1 K=0 -> red.
        byte[] raw = [0, 255, 255, 0];
        var compressed = ZlibCompress(raw);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceCMYK /BitsPerComponent 8 /Filter /FlateDecode",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>Do</c> decodes a bare <c>DCTDecode</c> (JPEG) image XObject via <see cref="JpegCodec.Load(Stream)"/> and places the expected (solid-color, lossless-for-flat-blocks) pixels.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_DctDecodeJpeg_PlacesExpectedPixels()
    {
        // Arrange: an 8x8 (one MCU block), solid blue surface - a flat color block's DCT has only
        // a DC coefficient, so a high-quality encode/decode round-trip reproduces it exactly (or
        // within a tiny rounding tolerance).
        var solid = new Canvas.Surface(8, 8);
        var blue = new Canvas.Rgba32(0, 0, 255, 255);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                solid[x, y] = blue;
            }
        }

        using var jpegStream = new MemoryStream();
        JpegCodec.Save(solid, jpegStream, quality: 100);
        var jpegBytes = jpegStream.ToArray();

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode",
            jpegBytes);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: within a small per-channel tolerance of solid blue.
        var pixel = surface[50, 50];
        Assert.True(Math.Abs(pixel.R - blue.R) <= 8, $"R={pixel.R}");
        Assert.True(Math.Abs(pixel.G - blue.G) <= 8, $"G={pixel.G}");
        Assert.True(Math.Abs(pixel.B - blue.B) <= 8, $"B={pixel.B}");
        Assert.Equal(255, pixel.A);
    }

    /// <summary>Proves that an image XObject with an unsupported <c>/BitsPerComponent</c> throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_UnsupportedBitsPerComponent_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 1 /Filter /FlateDecode",
            ZlibCompress([0x00]));

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that an image XObject with an unsupported <c>/ColorSpace</c> throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_UnsupportedColorSpace_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace [/Indexed /DeviceRGB 1 <00FFFFFF>] /BitsPerComponent 8 /Filter /FlateDecode",
            ZlibCompress([0x00]));

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that <c>Do</c> with a name not declared in <c>/Resources/XObject</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_UndefinedXObjectName_ThrowsInvalidDataException()
    {
        // Arrange
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/NotDeclared Do",
            "/XObject << >>",
            []);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that <c>Do</c> on a <c>/Subtype /Form</c> XObject throws <see cref="UnsupportedImageFeatureException"/> rather than being silently skipped.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_FormXObject_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var formStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 1 1]",
            "q Q"u8.ToArray());

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/Fm0 Do",
            "/XObject << /Fm0 5 0 R >>",
            [formStream]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that <c>Do</c> with a malformed (non-1, non-name) operand count/type throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("Do")]
    [InlineData("/Im0 /Im1 Do")]
    [InlineData("1 Do")]
    public void PdfDocument_Images_DoOperator_MalformedOperandCount_ThrowsInvalidDataException(string content)
    {
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }


    #endregion

    #region Fonts

    /// <summary>Proves that a non-<c>TrueType</c> simple/composite font <c>/Subtype</c> throws <see cref="UnsupportedImageFeatureException"/> rather than being silently substituted.</summary>
    [Theory]
    [InlineData("Type0")]
    [InlineData("Type1")]
    [InlineData("MMType1")]
    [InlineData("Type3")]
    public void PdfDocument_Fonts_UnsupportedSubtype_ThrowsUnsupportedImageFeatureException(string subtype)
    {
        // Arrange
        var fontFileObj = BuildStreamObjectBody(string.Empty, BuildEmbeddedFontBytes([(65, 1)]));
        var descriptorObj = "<< /Type /FontDescriptor /FontFile2 7 0 R >>"u8.ToArray();
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /Font /Subtype /{subtype} /BaseFont /Test /FontDescriptor 6 0 R >>");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj, fontFileObj]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a simple <c>/Subtype /TrueType</c> font with no embedded <c>/FontFile2</c> throws <see cref="UnsupportedImageFeatureException"/> (no standard-14/substitute-font fallback).</summary>
    [Fact]
    public void PdfDocument_Fonts_MissingFontFile2_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var descriptorObj = "<< /Type /FontDescriptor >>"u8.ToArray();
        var fontDictObj = "<< /Type /Font /Subtype /TrueType /BaseFont /Test /FontDescriptor 6 0 R >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that an unrecognized <c>/Encoding</c> base-encoding name throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_UnrecognizedEncoding_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [600 600] /Encoding /PDFDocEncoding");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a <c>Tf</c> operator naming a font absent from <c>/Resources/Font</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_UndefinedFontName_ThrowsInvalidDataException()
    {
        // Arrange
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /NotDeclared 20 Tf (A) Tj ET", "/Font << >>", []);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a font with no <c>/Encoding</c> entry defaults to <c>/WinAnsiEncoding</c>: code <c>0xE0</c> maps to Unicode codepoint <c>224</c>.</summary>
    [Fact]
    public void PdfDocument_Fonts_DefaultEncoding_IsWinAnsiEncoding()
    {
        // Arrange: the font's only mapped codepoint (224) is WinAnsiEncoding's mapping for code
        // 0xE0 ('\340' octal) - MacRomanEncoding maps that same code to codepoint 8225 instead.
        var fontBytes = BuildEmbeddedFontBytes([(224, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 0 /LastChar 255 /Widths [" + string.Join(' ', Enumerable.Repeat(600, 256)) + "]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (\\340) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: glyph 1's square is painted at the position code 0xE0 resolves to under WinAnsiEncoding.
        Assert.NotEqual(default, surface[11, 44]);
    }

    /// <summary>Proves that an explicit <c>/Encoding /MacRomanEncoding</c> resolves code <c>0xE0</c> to a different codepoint than <c>/WinAnsiEncoding</c> does.</summary>
    [Fact]
    public void PdfDocument_Fonts_MacRomanEncoding_DiffersFromWinAnsiEncoding()
    {
        // Arrange: same font as the WinAnsiEncoding test (only codepoint 224 mapped), but this
        // font dictionary declares /MacRomanEncoding, under which code 0xE0 maps to codepoint
        // 8225 (not 224) - so the code resolves to the unmapped .notdef glyph and paints nothing.
        var fontBytes = BuildEmbeddedFontBytes([(224, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 0 /LastChar 255 /Widths [" + string.Join(' ', Enumerable.Repeat(600, 256)) + "] /Encoding /MacRomanEncoding");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (\\340) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: no glyph outline is painted anywhere in the expected region.
        Assert.Equal(default, surface[11, 44]);
    }

    /// <summary>Proves that an <c>/Encoding/Differences</c> override changes a code's mapped codepoint away from its base encoding's own mapping.</summary>
    [Fact]
    public void PdfDocument_Fonts_Differences_OverridesBaseEncodingCode()
    {
        // Arrange: the font's only mapped codepoint is 224 ('agrave'). Code 65 ('A') would
        // ordinarily resolve (via the default WinAnsiEncoding) to codepoint 65, which the font
        // has no glyph for - but /Differences remaps code 65 to the "agrave" glyph name (U+00E0).
        var fontBytes = BuildEmbeddedFontBytes([(224, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [600 600] " +
                           "/Encoding << /Differences [65 /agrave] >>");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.NotEqual(default, surface[11, 44]);
    }

    /// <summary>Proves that without the <c>/Differences</c> override, the same code paints nothing (the font has no glyph for its base-encoding codepoint).</summary>
    [Fact]
    public void PdfDocument_Fonts_Differences_Absent_LeavesBaseEncodingCodeUnmapped()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(224, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [600 600]");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(default, surface[11, 44]);
    }

    /// <summary>Proves that a <c>/Differences</c> array containing an unrecognized glyph name throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_Differences_UnrecognizedGlyphName_ThrowsInvalidDataException()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [600 600] " +
                           "/Encoding << /Differences [65 /thisGlyphNameDoesNotExist] >>");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a <c>/Differences</c> array starting with a glyph name (before any starting code number) throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_Differences_NameBeforeStartingCode_ThrowsInvalidDataException()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [600 600] " +
                           "/Encoding << /Differences [/A 65 /B] >>");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that an explicit <c>/Widths</c> entry determines a glyph's advance width.</summary>
    [Fact]
    public void PdfDocument_Fonts_Widths_ExplicitEntry_DeterminesAdvance()
    {
        // Arrange: code 65's declared width is 1000 (a full em) -> advance = 1.0 * 20 = 20.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [1000 600]");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 5 Td (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'A' paints at text x [7, 15); 'B' starts at Tm.x = 5 + 20 = 25, painting at
        // text x [27, 35) - clearly past the "no explicit width" 12-unit-advance gap boundary.
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[31, 89]);
    }

    /// <summary>Proves that <c>/FontDescriptor/MissingWidth</c> determines a code's advance width when it has no explicit <c>/Widths</c> entry.</summary>
    [Fact]
    public void PdfDocument_Fonts_Widths_MissingWidthFallback_DeterminesAdvance()
    {
        // Arrange: code 65 is outside [FirstChar, LastChar] (so has no explicit width), and
        // /MissingWidth is 500 -> advance = 0.5 * 20 = 10.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 66 /LastChar 66 /Widths [600]", descriptorExtra: "/MissingWidth 500");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 5 Td (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'B' starts at Tm.x = 5 + 10 = 15, painting at text x [17, 25).
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[21, 89]);
    }

    /// <summary>Proves that a code with no explicit <c>/Widths</c> entry and no <c>/MissingWidth</c> falls back to the embedded font's own advance width.</summary>
    [Fact]
    public void PdfDocument_Fonts_Widths_FontOwnAdvanceFallback_DeterminesAdvance()
    {
        // Arrange: code 65 is outside [FirstChar, LastChar], and /MissingWidth is not declared -
        // falls back to the embedded font's own 600-unit hmtx advance -> advance = 0.6 * 20 = 12.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 66 /LastChar 66 /Widths [600]");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 5 Td (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'B' starts at Tm.x = 5 + 12 = 17, painting at text x [19, 27).
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[23, 89]);
    }

    #endregion

    #region Text

    /// <summary>Proves that <c>Tj</c> paints a glyph outline through the composed text-rendering matrix, positioned per <c>Tf</c>/<c>Td</c>.</summary>
    [Fact]
    public void PdfDocument_Text_ShowText_PaintsGlyphAtComposedTextRenderingMatrix()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: text x [7, 15), text y [52, 60) -> device x [7, 15), device y [40, 48).
        Assert.NotEqual(default, surface[10, 44]);
        Assert.Equal(default, surface[50, 50]);
    }

    /// <summary>Proves that a text-showing operator issued before any <c>Tf</c> throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("BT (A) Tj ET")]
    [InlineData("BT (A) ' ET")]
    [InlineData("BT 0 0 (A) \" ET")]
    [InlineData("BT [(A)] TJ ET")]
    public void PdfDocument_Text_ShowText_NoFontSelected_ThrowsInvalidDataException(string content)
    {
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    /// <summary>Proves that render mode 3 (invisible) lays out but does not paint a glyph.</summary>
    [Fact]
    public void PdfDocument_Text_RenderMode3_Invisible_DoesNotPaintGlyph()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 3 Tr 5 50 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: nowhere in the surface has any painted pixel.
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }

    /// <summary>Proves that render mode 3 still advances the text position (glyphs are laid out, just not painted).</summary>
    [Fact]
    public void PdfDocument_Text_RenderMode3_Invisible_StillAdvancesTextPosition()
    {
        // Arrange: 'A' is invisible (mode 3); 'B' switches back to mode 0 (fill) and should
        // still appear offset by 'A's own advance, proving 'A' was laid out, not skipped.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td 3 Tr (A) Tj 0 Tr (B) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'A' paints nothing; 'B' starts at Tm.x = 5 + 12 = 17, painting at text x [19, 27).
        Assert.Equal(default, surface[11, 89]);
        Assert.NotEqual(default, surface[23, 89]);
    }

    /// <summary>Proves that a defined but unsupported text-rendering mode throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void PdfDocument_Text_RenderMode_UnsupportedDefinedMode_ThrowsUnsupportedImageFeatureException(int mode)
    {
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderContent($"BT {mode} Tr ET"));
    }

    /// <summary>Proves that a text-rendering mode outside the PDF specification's defined [0, 7] range throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void PdfDocument_Text_RenderMode_OutOfDefinedRange_ThrowsInvalidDataException(int mode)
    {
        Assert.Throws<InvalidDataException>(() => RenderContent($"BT {mode} Tr ET"));
    }

    /// <summary>Proves that <c>BT</c> resets the text/line matrix to identity, but leaves the selected font (and every other text-state graphics parameter) untouched.</summary>
    [Fact]
    public void PdfDocument_Text_BeginText_ResetsTextMatrixButPreservesFontAndTextState()
    {
        // Arrange: the second BT/ET block issues no Tf of its own - it must still have a font
        // selected (from the first block) to show text without throwing, and its Tm/Tlm must
        // have reset to identity (not still be positioned at (5, 50) from the first block).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj ET BT (B) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the second block's glyph paints at text x [2, 10), y [2, 10) -> device y [90, 98).
        Assert.NotEqual(default, surface[6, 94]);
    }

    /// <summary>Proves that <c>q</c>/<c>Q</c> save/restore the font size (part of the graphics state), independently of the (never saved/restored) text/line matrix.</summary>
    [Fact]
    public void PdfDocument_Text_PushPopGraphicsState_RestoresFontSize()
    {
        // Arrange: 'q' saves FontSize=20; the nested '40 Tf' changes it to 40; 'Q' restores 20 -
        // if it did not, the glyph painted afterward would be twice as large (and shifted).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td q /F1 40 Tf Q (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: with FontSize correctly restored to 20, the glyph paints at text y [52, 60) ->
        // device y [40, 48) - device y = 47 is inside that restored-size band, but outside the
        // much taller band an un-restored FontSize = 40 would have produced (device y [30, 46)).
        Assert.NotEqual(default, surface[11, 47]);
    }

    /// <summary>Proves that <c>Td</c> offsets the line matrix (not the text matrix as last advanced by a preceding show operator), and both are made equal.</summary>
    [Fact]
    public void PdfDocument_Text_Td_OffsetsLineMatrixNotLastTextMatrix()
    {
        // Arrange: after showing 'A', the text matrix has advanced past the line matrix's own
        // (5, 5) position - '10 0 Td' must offset the *line* matrix's (5, 5), landing 'B' at
        // Tm.x = 15, not at (5 + 'A's own advance + 10).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (A) Tj 10 0 Td (B) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'B' paints at text x [17, 25) -> device x [17, 25); a buggy "offset from the
        // last text matrix" implementation would instead land it at text x [29, 37).
        Assert.NotEqual(default, surface[20, 89]);
        Assert.Equal(default, surface[32, 89]);
    }

    /// <summary>Proves that <c>TD</c> behaves like <c>Td</c> and additionally sets the leading to <c>-ty</c>, observable via a subsequent <c>T*</c>.</summary>
    [Fact]
    public void PdfDocument_Text_TD_SetsLeadingToNegativeTy()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj 0 -7 TD (A) Tj T* (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: line y = 50, then 43 (TD), then 36 (T* using leading = 7 set by TD) -> device
        // y centers approximately 44, 51, 58 respectively.
        Assert.NotEqual(default, surface[11, 44]);
        Assert.NotEqual(default, surface[11, 51]);
        Assert.NotEqual(default, surface[11, 58]);
    }

    /// <summary>Proves that <c>Tm</c> replaces (rather than composes with) the text/line matrix.</summary>
    [Fact]
    public void PdfDocument_Text_Tm_ReplacesTextAndLineMatrix()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj 1 0 0 1 30 30 Tm (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'Tm' replaces (5, 50) outright with (30, 30) - text x/y [32, 40) -> device y
        // [60, 68) - not composed with the prior (5, 50) (which would instead land at (35, 80)).
        Assert.NotEqual(default, surface[36, 64]);
        Assert.Equal(default, surface[41, 14]);
    }

    /// <summary>Proves that <c>T*</c> moves to the next line using the current leading (set via <c>TL</c>).</summary>
    [Fact]
    public void PdfDocument_Text_TStar_UsesCurrentLeading()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 7 TL 5 50 Td (A) Tj T* (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: line y = 50, then 43 (T* using leading = 7) -> device y centers 44 and 51.
        Assert.NotEqual(default, surface[11, 44]);
        Assert.NotEqual(default, surface[11, 51]);
    }

    /// <summary>Proves that <c>Tc</c> (character spacing) is added to every glyph's advance.</summary>
    [Fact]
    public void PdfDocument_Text_Tc_AddsToGlyphAdvance()
    {
        // Arrange: 'A's own advance is 0.6 * 20 = 12; with Tc = 3, 'B' starts at Tm.x = 5 + 15 = 20.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td 3 Tc (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'B' paints at text x [22, 30); without Tc it would paint at [19, 27) instead.
        Assert.NotEqual(default, surface[29, 44]);
        Assert.Equal(default, surface[20, 44]);
    }

    /// <summary>Proves that <c>Tw</c> (word spacing) applies only to single-byte character code 32 (space), not to any other code.</summary>
    [Fact]
    public void PdfDocument_Text_Tw_AppliesOnlyToCode32()
    {
        // Arrange: "A A" is codes 65, 32, 65. 'A's own advance is 12; the space (code 32, no
        // glyph, zero declared/font advance) gets an extra 5 units from Tw. The final 'A'
        // starts at Tm.x = 5 + 12 + 5 = 22, painting at text x [24, 32).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td 5 Tw (A A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: without Tw applied to the space, the final 'A' would instead paint at [19, 27).
        Assert.NotEqual(default, surface[30, 44]);
        Assert.Equal(default, surface[20, 44]);
    }

    /// <summary>Proves that <c>Tz</c> (horizontal scaling) scales both glyph shape and advance along the x-axis only.</summary>
    [Fact]
    public void PdfDocument_Text_Tz_ScalesHorizontalShapeAndAdvance()
    {
        // Arrange: Th = 0.5 halves the glyph's horizontal extent (font-space x scale factor
        // becomes 0.01 * 20 * 0.5 = 0.01, so the 100-500 unit square spans text x [1, 5) instead
        // of the unscaled [2, 10)) and halves 'A's own advance (0.6 * 20 * 0.5 = 6).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 50 Tz 5 50 Td (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'A' paints narrowly at text x [6, 10); the unscaled width would instead extend
        // well past text x 11, but the correctly-narrowed shape leaves the gap before 'B' empty.
        // 'B' starts at Tm.x = 5 + 6 = 11, painting at text x [12, 16).
        Assert.NotEqual(default, surface[8, 44]);
        Assert.Equal(default, surface[11, 44]);
        Assert.NotEqual(default, surface[14, 44]);
    }

    /// <summary>Proves that <c>TJ</c>'s numeric array elements pre-adjust the text position, in thousandths of text-space units.</summary>
    [Fact]
    public void PdfDocument_Text_TJ_AppliesPositionAdjustments()
    {
        // Arrange: after showing 'A' (advance 12), a "-250" adjustment adds
        // -(-250 / 1000) * 20 = 5 further units before 'B' - landing 'B' at Tm.x = 5 + 12 + 5 = 22.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td [(A) -250 (B)] TJ ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'B' paints at text x [24, 32); without the adjustment it would paint at [19, 27).
        Assert.NotEqual(default, surface[28, 44]);
        Assert.Equal(default, surface[20, 44]);
    }

    /// <summary>Proves that a <c>TJ</c> array entry that is neither a string nor a number throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Text_TJ_ArrayEntryNotStringOrNumber_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => RenderContent("BT [true] TJ ET"));
    }

    /// <summary>Proves that the <c>'</c> operator moves to the next line (using the current leading) and then shows its string operand.</summary>
    [Fact]
    public void PdfDocument_Text_QuoteOperator_MovesToNextLineThenShows()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj 7 TL (B) ' ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'A' at device y 44; ' moves to the next line (leading 7, from the line
        // matrix's own (5, 50), not the text matrix advanced by 'A') and shows 'B' at text x
        // [7, 15) again, device y 51 - a buggy "advance from the text matrix" would instead
        // shift 'B' rightward, out of this x window.
        Assert.NotEqual(default, surface[11, 44]);
        Assert.NotEqual(default, surface[11, 51]);
    }

    /// <summary>Proves that the <c>"</c> operator sets word/character spacing, then behaves like <c>'</c>.</summary>
    [Fact]
    public void PdfDocument_Text_DoubleQuoteOperator_SetsSpacingThenMovesAndShows()
    {
        // Arrange: '4 2 (A) "' sets Tw=4/Tc=2 then shows 'A' (advance 12 + 2 = 14); the
        // following 'Tj' for 'B' starts at Tm.x = 5 + 14 = 19, painting at text x [21, 29).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td 4 2 (A) \" (B) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: without Tc applied, 'B' would instead paint at text x [19, 27) only.
        Assert.NotEqual(default, surface[28, 44]);
    }

    /// <summary>Proves that every new text operator's malformed operand count/type throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("BT 1 2 ET")]
    [InlineData("1 2 Tc")]
    [InlineData("1 2 Tw")]
    [InlineData("1 2 Tz")]
    [InlineData("1 2 TL")]
    [InlineData("1 2 Ts")]
    [InlineData("/F1 Tf")]
    [InlineData("24 /F1 Tf")]
    [InlineData("Tr")]
    [InlineData("1 2 Tr")]
    [InlineData("1 Td")]
    [InlineData("1 2 3 Td")]
    [InlineData("1 TD")]
    [InlineData("1 2 3 4 5 Tm")]
    [InlineData("1 T*")]
    [InlineData("(A) (B) Tj")]
    [InlineData("1 Tj")]
    [InlineData("1 '")]
    [InlineData("1 2 \"")]
    [InlineData("1 2 (A) 3 \"")]
    [InlineData("(A) TJ")]
    public void PdfDocument_Text_MalformedOperandCount_ThrowsInvalidDataException(string content)
    {
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    #endregion
}
