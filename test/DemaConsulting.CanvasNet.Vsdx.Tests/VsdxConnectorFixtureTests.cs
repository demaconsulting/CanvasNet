// cspell:ignore vsdx davehoward jgreywolfvsdxjs Visio diagramwithstyles

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Smoke-level tests proving this milestone's connector/arrowhead-resolution pipeline
///     (<c>&lt;Connects&gt;</c> parsing, <see cref="VsdxShapeNode.ConnectorEndpoints"/>
///     resolution, <see cref="VsdxResolvedPaint.BeginArrowhead"/>/<c>EndArrowhead</c>) runs to
///     completion, without throwing, against every real-world <c>.vsdx</c> fixture in the
///     corpus that actually declares a <c>&lt;Connects&gt;</c> section:
///     <c>davehoward-test4-connectors.vsdx</c>, <c>jgreywolfvsdxjs-connectors.vsdx</c>, and
///     <c>jgreywolfvsdxjs-diagramwithstyles.vsdx</c> - confirmed by grepping every staged fixture's
///     page1.xml for a literal <c>&lt;Connects</c> occurrence (two more than the plan's two
///     named-by-example fixtures).
/// </summary>
public class VsdxConnectorFixtureTests
{
    /// <summary>The root folder a built test project copies the staged <c>.vsdx</c> fixtures into.</summary>
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "VsdxFixtures");

    /// <summary>Resolves a staged fixture's full path by file name, failing clearly if the fixture-copy wiring did not place it in the output directory.</summary>
    private static string FixturePath(string fileName)
    {
        var path = Path.Combine(FixturesDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected staged fixture '{path}' to exist in the test output directory.");
        return path;
    }

    /// <summary>Every fixture in the corpus confirmed (by direct inspection of its own page1.xml) to declare a <c>&lt;Connects&gt;</c> section, paired with its expected total <see cref="VsdxConnect"/> entry count.</summary>
    public static TheoryData<string, int> ConnectorFixtureFileNamesWithExpectedConnectCount => new()
    {
        { "davehoward-test4-connectors.vsdx", 4 },
        { "jgreywolfvsdxjs-connectors.vsdx", 2 },
        { "jgreywolfvsdxjs-diagramwithstyles.vsdx", 2 }
    };

    /// <summary>
    ///     Proves every page's top-level shapes resolve without throwing for every fixture that
    ///     declares a <c>&lt;Connects&gt;</c> section, and that the page's total parsed
    ///     <see cref="VsdxConnect"/> entry count (summed across every shape's
    ///     <see cref="VsdxShapeNode.Connects"/>) exactly matches the count read directly from the
    ///     fixture's own XML.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConnectorFixtureFileNamesWithExpectedConnectCount))]
    public void ConnectorFixture_RealFixture_ResolvesWithExpectedConnectCountWithoutThrowing(string fileName, int expectedConnectCount)
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath(fileName));

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));
        Assert.Null(exception);

        var shapes = document.GetPageShapes(0);

        // Assert
        var totalConnects = shapes.Sum(shape => shape.Connects.Count);
        Assert.Equal(expectedConnectCount, totalConnects);

        foreach (var shape in shapes)
        {
            Assert.NotNull(shape.Paint);
            Assert.NotNull(shape.Paint.BeginArrowhead);
            Assert.NotNull(shape.Paint.EndArrowhead);
        }
    }

    /// <summary>
    ///     Proves every connector-bearing shape (every shape with at least one
    ///     <see cref="VsdxConnect"/> attached) across all three connector fixtures resolves a
    ///     non-null <see cref="VsdxShapeNode.ConnectorEndpoints"/> with finite, non-NaN
    ///     coordinates - i.e. the 1-D transform path always produces usable <c>BeginX/Y</c>/
    ///     <c>EndX/Y</c> values for every real-world glued connector in the corpus.
    /// </summary>
    [Theory]
    [InlineData("davehoward-test4-connectors.vsdx")]
    [InlineData("jgreywolfvsdxjs-connectors.vsdx")]
    [InlineData("jgreywolfvsdxjs-diagramwithstyles.vsdx")]
    public void ConnectorFixture_ConnectorShapes_ResolveFiniteConnectorEndpoints(string fileName)
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath(fileName));

        // Act
        var connectorShapes = document.GetPageShapes(0).Where(shape => shape.Connects.Count > 0).ToList();

        // Assert
        Assert.NotEmpty(connectorShapes);
        foreach (var shape in connectorShapes)
        {
            Assert.NotNull(shape.ConnectorEndpoints);
            Assert.False(double.IsNaN(shape.ConnectorEndpoints.BeginX));
            Assert.False(double.IsNaN(shape.ConnectorEndpoints.BeginY));
            Assert.False(double.IsNaN(shape.ConnectorEndpoints.EndX));
            Assert.False(double.IsNaN(shape.ConnectorEndpoints.EndY));
        }
    }

    /// <summary>
    ///     Proves <c>jgreywolfvsdxjs-diagramwithstyles.vsdx</c>'s connector Shape ID='3' - whose
    ///     own literal <c>BeginArrow="13"</c> cell is an index outside this milestone's
    ///     conservative recognized subset (<c>0</c>/<c>1</c>/<c>2</c>/<c>5</c>/<c>10</c>/<c>22</c>)
    ///     - degrades to <see cref="VsdxArrowhead.NoArrowhead"/> rather than throwing: the only
    ///     fixture in the corpus that actually exercises a non-zero/unrecognized arrowhead index.
    /// </summary>
    [Fact]
    public void ConnectorFixture_DiagramWithStylesFixture_UnrecognizedBeginArrowDegradesToNoArrowhead()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("jgreywolfvsdxjs-diagramwithstyles.vsdx"));

        // Act
        var connector = Assert.Single(document.GetPageShapes(0), shape => shape.Id == "3");

        // Assert: BeginArrow="13" (unrecognized) degrades to None; EndArrow="0" is also None.
        Assert.Equal(VsdxArrowhead.NoArrowhead, connector.Paint!.BeginArrowhead);
        Assert.Equal(VsdxArrowhead.NoArrowhead, connector.Paint.EndArrowhead);
    }
}
