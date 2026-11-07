using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of a selection that is projected over a field with a <c>timeUnit</c> (Phase 12): the selection constrains the derived field (<c>month_d</c>, the start of the month),
///     which Vega-Lite's time unit node adds above every filter and every unit whose conditions test the selection. The charts have one date in each of January, February and March; the selected
///     range is from the first to the twenty-eighth of February, so the start of the month of the February row (the first) is inside it and those of the other two are not.
/// </summary>
public class VegaLiteSelectionTimeUnitIntegrationTests
{
    /// <summary>The selection parameter: x over the month of <c>d</c>, from the first to the twenty-eighth of February of 2012, the year that a time unit without a year uses (a leap year).</summary>
    private const string Brush = """
        { "name": "brush", "select": { "type": "interval", "encodings": ["x"] },
          "value": { "x": [ { "year": 2012, "month": "feb", "date": 1 }, { "year": 2012, "month": "feb", "date": 28 } ] } }
        """;

    /// <summary>The three rows.</summary>
    private const string Rows = """ "data": { "values": [ { "d": "2020-01-15" }, { "d": "2020-02-15" }, { "d": "2020-03-15" } ] } """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates that a <c>filter</c> on the selection tests the month of each date: only the February row is kept.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_TimeUnitSelection_AFilterTestsTheDerivedField()
    {
        // Arrange
        var json = "{ \"params\": [ " + Brush + " ], " + Rows + ", \"transform\": [ { \"filter\": { \"param\": \"brush\" } } ], \"width\": 300, \"height\": 200, \"mark\": \"point\", "
            + "\"encoding\": { \"x\": { \"field\": \"d\", \"type\": \"temporal\", \"timeUnit\": \"month\" } } }";

        // Act
        var scene = VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);

        // Assert
        Assert.Single(scene.FindAll("mark-point"));
    }

    /// <summary>
    ///     Validates that the filter finds the derived field in a view that does not declare the selection, through the view above it: the view below has no time unit of its own.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_TimeUnitSelection_AFilterInAnotherViewAddsTheTimeUnit()
    {
        // Arrange: the first view declares the selection, the second filters its dates by it
        var json = "{ " + Rows + ", \"vconcat\": [ "
            + "{ \"params\": [ " + Brush + " ], \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"d\", \"type\": \"temporal\", \"timeUnit\": \"month\" } } }, "
            + "{ \"transform\": [ { \"filter\": { \"param\": \"brush\" } } ], \"mark\": \"circle\", \"encoding\": { \"x\": { \"field\": \"d\", \"type\": \"temporal\" } } } ] }";

        // Act
        var scene = VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);

        // Assert: three points above, one circle below
        Assert.Equal(3, scene.FindAll("mark-point").Count());
        Assert.Single(scene.FindAll("mark-circle"));
    }

    /// <summary>
    ///     Validates that a condition on the selection in a view without a time unit is tested on the derived field too: the February circle is the one that is picked.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_TimeUnitSelection_AConditionAddsTheTimeUnitToItsView()
    {
        // Arrange
        var json = "{ " + Rows + ", \"vconcat\": [ "
            + "{ \"params\": [ " + Brush + " ], \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"d\", \"type\": \"temporal\", \"timeUnit\": \"month\" } } }, "
            + "{ \"mark\": \"circle\", \"encoding\": { \"x\": { \"field\": \"d\", \"type\": \"temporal\" }, "
            + "\"fill\": { \"condition\": { \"param\": \"brush\", \"value\": \"#d73027\" }, \"value\": \"#4575b4\" } } } ] }";

        // Act
        var scene = VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);
        var fills = scene.FindAll("mark-circle").Select(e => ((SceneSymbol)e.Item).Fill).ToList();

        // Assert: January, February, March
        Assert.Equal(["#4575b4", "#d73027", "#4575b4"], fills);
    }
}
