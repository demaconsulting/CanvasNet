// cspell:ignore vsdx davehoward jgreywolfvsdxjs Visio diagramwithstyles basicshapes

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Smoke-level tests proving this milestone's text-resolution pipeline (run/paragraph marker
///     parsing, <c>TextStyle</c> chain resolution, text-box positioning, word-wrap layout) runs to
///     completion, without throwing, against every real-world <c>.vsdx</c> fixture this
///     milestone's own planning report confirmed to contain non-empty <c>&lt;Text&gt;</c> content:
///     <c>davehoward-test4-connectors.vsdx</c>, <c>davehoward-test5-master.vsdx</c>,
///     <c>davehoward-test6-shape-properties.vsdx</c>, <c>davehoward-test12-colors.vsdx</c>,
///     <c>jgreywolfvsdxjs-diagramwithstyles.vsdx</c>, and <c>jgreywolfvsdxjs-drawing.vsdx</c> -
///     plus a dedicated zero-text fixture (<c>jgreywolfvsdxjs-basicshapes.vsdx</c>) proving a
///     shape with no <c>&lt;Text&gt;</c> element at all resolves empty runs/layout rather than
///     throwing.
/// </summary>
public class VsdxFixtureTextResolutionTests
{
    /// <summary>The root folder a built test project copies the staged <c>.vsdx</c> fixtures into (see the <c>.csproj</c>'s fixture <c>&lt;None&gt;</c> wiring).</summary>
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "VsdxFixtures");

    /// <summary>Resolves a staged fixture's full path by file name, failing clearly if the fixture-copy wiring did not place it in the output directory.</summary>
    private static string FixturePath(string fileName)
    {
        var path = Path.Combine(FixturesDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected staged fixture '{path}' to exist in the test output directory.");
        return path;
    }

    /// <summary>Every fixture this milestone's own plan report confirmed contains non-empty <c>&lt;Text&gt;</c> content somewhere on at least one page.</summary>
    public static TheoryData<string> TextFixtureFileNames =>
    [
        "davehoward-test4-connectors.vsdx",
        "davehoward-test5-master.vsdx",
        "davehoward-test6-shape-properties.vsdx",
        "davehoward-test12-colors.vsdx",
        "jgreywolfvsdxjs-diagramwithstyles.vsdx",
        "jgreywolfvsdxjs-drawing.vsdx"
    ];

    /// <summary>
    ///     Proves every page's top-level shapes resolve <see cref="VsdxShapeNode.TextRuns"/>/
    ///     <see cref="VsdxShapeNode.TextBox"/>/<see cref="VsdxShapeNode.TextLayout"/> without
    ///     throwing, for every text-bearing fixture singled out by the plan report, and that at
    ///     least one shape on at least one page resolves a non-empty, non-whitespace-only run of
    ///     text with a sane (positive, finite) resolved font size.
    /// </summary>
    [Theory]
    [MemberData(nameof(TextFixtureFileNames))]
    public void FixtureTextResolution_RealFixture_ResolvesNonEmptyTextWithoutThrowing(string fileName)
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath(fileName));

        // Act / Assert: resolving every page's shapes must not throw.
        var sawNonWhitespaceText = false;
        var sawLaidOutGlyph = false;
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            var exception = Record.Exception(() => document.GetPageShapes(pageIndex));
            Assert.Null(exception);

            foreach (var shape in document.GetPageShapes(pageIndex))
            {
                Assert.NotNull(shape.TextRuns);
                Assert.NotNull(shape.TextBox);
                Assert.NotNull(shape.TextLayout);

                foreach (var run in shape.TextRuns)
                {
                    Assert.True(double.IsFinite(run.SizeInches) && run.SizeInches > 0, $"Expected a positive, finite resolved font size for shape '{shape.Id}' on page {pageIndex}.");
                    if (!string.IsNullOrWhiteSpace(run.Text))
                    {
                        sawNonWhitespaceText = true;
                    }
                }

                if (shape.TextLayout.Glyphs.Count > 0)
                {
                    sawLaidOutGlyph = true;
                }
            }
        }

        Assert.True(sawNonWhitespaceText, $"Expected at least one shape in '{fileName}' to resolve non-whitespace text.");
        Assert.True(sawLaidOutGlyph, $"Expected at least one shape in '{fileName}' to resolve at least one laid-out glyph.");
    }

    /// <summary>
    ///     Proves a fixture whose shapes declare no <c>&lt;Text&gt;</c> element at all
    ///     (<c>jgreywolfvsdxjs-basicshapes.vsdx</c>) resolves an empty (not <see langword="null"/>)
    ///     <see cref="VsdxShapeNode.TextRuns"/> and an empty <see cref="VsdxTextLayout.Glyphs"/>
    ///     for every shape, without throwing.
    /// </summary>
    [Fact]
    public void FixtureTextResolution_ZeroTextFixture_ResolvesEmptyRunsAndLayoutWithoutThrowing()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("jgreywolfvsdxjs-basicshapes.vsdx"));

        // Act / Assert
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            var exception = Record.Exception(() => document.GetPageShapes(pageIndex));
            Assert.Null(exception);

            foreach (var shape in document.GetPageShapes(pageIndex))
            {
                Assert.NotNull(shape.TextRuns);
                Assert.Empty(shape.TextRuns);
                Assert.NotNull(shape.TextLayout);
                Assert.Empty(shape.TextLayout.Glyphs);
            }
        }
    }

    /// <summary>
    ///     Proves an instance shape with an explicit, empty <c>&lt;Text/&gt;</c> element over a
    ///     Master with non-empty text resolves with NO text of its own, rather than falling back
    ///     to the Master's own text - PR #42 review round 2 (Finding #2, Medium): an explicit
    ///     empty <c>&lt;Text/&gt;</c> (or one containing only markers, yielding a raw run list
    ///     with <c>Count == 0</c>) must not be treated the same as a genuinely absent
    ///     <c>&lt;Text&gt;</c> element - same bug family as the Milestone 10/11
    ///     stencil/master-text-leak fixes, but for the empty-vs-absent distinction specifically.
    ///     See <c>VsdxDocument.Groups.cs</c>'s <c>ResolveShapeRecursive</c>, which now consults
    ///     <see cref="VsdxRawText.HasElement"/> rather than <c>Runs.Count</c>.
    /// </summary>
    [Fact]
    public void FixtureTextResolution_InstanceWithExplicitEmptyTextOverNonEmptyMaster_ResolvesNoText()
    {
        // Arrange: a Master with non-empty text, and a page instance that explicitly declares its
        // own empty <Text/> element (intentionally suppressing its inherited text).
        const string masterShapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="2"/><Cell N="PinY" V="2"/><Cell N="Width" V="2"/><Cell N="Height" V="2"/>
              <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="1"/><Cell N="Angle" V="0"/>
              <Text>Master Label</Text>
            </Shape>
            """;
        const string instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1">
              <Text/>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: masterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert: the instance resolves NO text at all - not the Master's "Master Label".
        Assert.NotNull(shape.TextRuns);
        Assert.Empty(shape.TextRuns);
        Assert.NotNull(shape.TextLayout);
        Assert.Empty(shape.TextLayout.Glyphs);
    }

    /// <summary>
    ///     Proves an instance shape with NO own <c>&lt;Text&gt;</c> element at all (genuinely
    ///     absent, unlike the explicit-empty case above) still correctly falls back to its
    ///     Master's own non-empty text - the companion, "still works" half of Finding #2's fix,
    ///     guarding against an overcorrection that would have broken the pre-existing, intended
    ///     Master-text-inheritance behavior.
    /// </summary>
    [Fact]
    public void FixtureTextResolution_InstanceWithNoTextElementOverNonEmptyMaster_InheritsMasterText()
    {
        // Arrange: a Master with non-empty text, and a page instance with no <Text> element at all.
        const string masterShapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="2"/><Cell N="PinY" V="2"/><Cell N="Width" V="2"/><Cell N="Height" V="2"/>
              <Cell N="LocPinX" V="1"/><Cell N="LocPinY" V="1"/><Cell N="Angle" V="0"/>
              <Text>Master Label</Text>
            </Shape>
            """;
        const string instanceShapeXml =
            """
            <Shape ID="10" Type="Shape" Master="1"/>
            """;
        using var stream = VsdxTestPackages.BuildPackage(instanceShapeXml, mastersXml: masterShapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert: the instance inherits the Master's own text verbatim.
        Assert.NotNull(shape.TextRuns);
        var run = Assert.Single(shape.TextRuns);
        Assert.Equal("Master Label", run.Text);
    }
}
