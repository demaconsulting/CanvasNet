using System.Text;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Unit tests for the reading entry points of <see cref="PlantUmlDocument"/>: <c>Parse</c>, <c>Open(path)</c> and
///     <c>Open(stream)</c>, strict UTF-8, the size limit, the source name and the argument contract.
/// </summary>
public class PlantUmlDocumentTests
{
    /// <summary>A small sequence diagram.</summary>
    private const string SequenceText = "@startuml\nAlice -> Bob : hello\n@enduml\n";

    /// <summary>
    ///     Validates that the three entry points give the same document.
    /// </summary>
    [Fact]
    public void PlantUmlDocument_ParseAndOpen_SameText_Agree()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(SequenceText);
        var path = PlantUmlTestHelpers.WriteTempFile(bytes);
        try
        {
            // Act
            var parsed = PlantUmlDocument.Parse(SequenceText);
            var opened = PlantUmlDocument.Open(path);
            using var stream = new MemoryStream(bytes);
            var streamed = PlantUmlDocument.Open(stream);

            // Assert
            Assert.All([opened, streamed], document =>
            {
                Assert.Equal(parsed.DiagramType, document.DiagramType);
                Assert.Equal(parsed.IsSupported, document.IsSupported);
                Assert.Equal(parsed.Title, document.Title);
                Assert.Equal(parsed.NodeCount, document.NodeCount);
            });
            Assert.Equal("sequence", parsed.DiagramType);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that a file's source name is its path unless the options name it, and that a stream is unnamed by
    ///     default.
    /// </summary>
    [Fact]
    public void PlantUmlDocument_Open_SourceName_DefaultsToPath()
    {
        // Arrange
        var path = PlantUmlTestHelpers.WriteTempFile(Encoding.UTF8.GetBytes(SequenceText));
        try
        {
            // Act
            var byPath = PlantUmlDocument.Open(path);
            var named = PlantUmlDocument.Open(path, new PlantUmlOpenOptions { SourceName = "named.puml" });
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SequenceText));
            var unnamed = PlantUmlDocument.Open(stream);

            // Assert
            Assert.Equal(path, byPath.SourceName);
            Assert.Equal("named.puml", named.SourceName);
            Assert.Null(unnamed.SourceName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that a byte-order mark is removed and invalid UTF-8 is refused with a located syntax error.
    /// </summary>
    [Fact]
    public void PlantUmlDocument_Open_Utf8_IsStrictAndDropsBom()
    {
        // Arrange
        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(SequenceText)).ToArray();
        var invalid = new byte[] { 0x40, 0x73, 0xFF, 0x0A };

        // Act
        using var bomStream = new MemoryStream(withBom);
        var document = PlantUmlDocument.Open(bomStream);
        using var invalidStream = new MemoryStream(invalid);
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Open(invalidStream, new PlantUmlOpenOptions { SourceName = "bad.puml" }));

        // Assert
        Assert.Equal("sequence", document.DiagramType);
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal("bad.puml", ex.Location.File);
    }

    /// <summary>
    ///     Validates that a text longer than the size limit is refused before it is parsed, from a string and from a
    ///     stream.
    /// </summary>
    [Fact]
    public void PlantUmlDocument_Parse_TextOverLimit_ThrowsInvalidData()
    {
        // Arrange
        var options = new PlantUmlOpenOptions { Limits = new PlantUmlLimits { MaxTextSize = 10 } };
        var text = "@startuml\n" + new string('A', 100) + " -> B\n@enduml\n";

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PlantUmlDocument.Parse(text, options));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        Assert.Throws<InvalidDataException>(() => PlantUmlDocument.Open(stream, options));
    }

    /// <summary>
    ///     Validates the argument contract of the entry points.
    /// </summary>
    [Fact]
    public void PlantUmlDocument_EntryPoints_InvalidArguments_Throw()
    {
        // Arrange
        using var unreadable = new UnreadableStream();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PlantUmlDocument.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => PlantUmlDocument.Open((string)null!));
        Assert.Throws<ArgumentException>(() => PlantUmlDocument.Open(" "));
        Assert.Throws<ArgumentNullException>(() => PlantUmlDocument.Open((Stream)null!));
        Assert.Throws<ArgumentException>(() => PlantUmlDocument.Open(unreadable));
        Assert.Throws<ArgumentException>(() => new PlantUmlOpenOptions { SourceName = " " });
        Assert.Throws<FileNotFoundException>(() => PlantUmlDocument.Open(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.puml")));
    }

    /// <summary>
    ///     Validates that a recognized type that is not drawn opens without error and has no metadata.
    /// </summary>
    [Fact]
    public void PlantUmlDocument_Parse_UnsupportedType_OpensWithoutMetadata()
    {
        // Arrange / Act
        var document = PlantUmlDocument.Parse(PlantUmlTestHelpers.UnsupportedText);

        // Assert
        Assert.Equal("class", document.DiagramType);
        Assert.False(document.IsSupported);
        Assert.Null(document.Title);
        Assert.Equal((0, 0, 0), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
    }

    /// <summary>
    ///     Validates that the reports found while reading are delivered by every call, in text order, before the report
    ///     of the type, and that strict mode stops at the first one.
    /// </summary>
    [Fact]
    public void PlantUmlDocument_Render_ReadingReports_DeliveredEveryCallInOrder()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\n!theme cerulean\n!pragma x\nclass A\n@enduml\n");
        var order = new List<string>();
        var options = new PlantUmlRenderOptions { WarningHandler = w => order.Add(w.Code), UnsupportedFeatureHandler = u => order.Add(u.Feature) };
        var strict = new PlantUmlRenderOptions { ThrowOnUnsupportedFeature = true };

        // Act
        document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => document.Render(strict, TestContext.Current.CancellationToken));

        // Assert
        string[] once = [PlantUmlDiagnosticCodes.ThemeUnsupported, PlantUmlDiagnosticCodes.PragmaIgnored, PlantUmlDiagnosticCodes.DiagramUnsupported];
        Assert.Equal([.. once, .. once], order);
        Assert.Equal(PlantUmlDiagnosticCodes.ThemeUnsupported, ex.Feature);
    }

    /// <summary>
    ///     Validates the argument contract of the render entry points: the exact size, the scaled size, and a canceled
    ///     token.
    /// </summary>
    [Fact]
    public void PlantUmlDocument_Render_InvalidArgumentsAndCancellation_Throw()
    {
        // Arrange
        var document = PlantUmlDocument.Parse(PlantUmlTestHelpers.UnsupportedText);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 10, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(10, Canvas.Surface.MaxDimension + 1, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(new PlantUmlRenderOptions { Scale = 1e-3 }, TestContext.Current.CancellationToken));
        Assert.ThrowsAny<OperationCanceledException>(() => document.Measure(null, source.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => document.Render(null, source.Token));
        using var exact = document.Render(30, 10, null, TestContext.Current.CancellationToken);
        Assert.Equal((30, 10), (exact.Width, exact.Height));
        Assert.Equal(new PlantUmlSize(20, 20), document.MeasureNatural(null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     A stream that cannot be read.
    /// </summary>
    private sealed class UnreadableStream : MemoryStream
    {
        /// <summary>
        ///     Gets a value indicating whether the stream can be read: never.
        /// </summary>
        public override bool CanRead => false;
    }
}
