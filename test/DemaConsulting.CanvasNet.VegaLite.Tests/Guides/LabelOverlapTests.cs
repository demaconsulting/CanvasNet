using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of <see cref="LabelOverlap"/>: Vega's parity and greedy overlap removal, with every expectation derived by hand from the
///     measured width of the labels and the rules of the Overlap transform.
/// </summary>
public class LabelOverlapTests
{
    /// <summary>
    ///     Builds a label of the text <c>1000</c> (22.24 pixels wide at 10 pixels, four digits of 0.556 em) at an x position, left aligned.
    /// </summary>
    /// <param name="x">The x position.</param>
    /// <returns>The label.</returns>
    private static SceneText Label(double x) => new("axis-label", x, 0, ["1000"]) { FontSize = 10, Baseline = "top" };

    /// <summary>
    ///     Builds labels at positions.
    /// </summary>
    /// <param name="positions">The x positions.</param>
    /// <returns>The labels.</returns>
    private static List<SceneText> Labels(params double[] positions) => [.. positions.Select(Label)];

    /// <summary>
    ///     Validates the parity method: every other label is hidden, and again, until nothing overlaps or fewer than three labels are left.
    /// </summary>
    [Fact]
    public void LabelOverlap_Hidden_ParityKeepsEveryOtherLabelUntilNothingOverlaps()
    {
        // Arrange: five labels 10 pixels apart, each 22.24 pixels wide, so neighbors at 20 pixels still overlap after the first round
        var labels = Labels(0, 10, 20, 30, 40);

        // Act
        var hidden = LabelOverlap.Hidden(labels, new TextMeasurer(), LabelOverlap.Parity, 0, TestContext.Current.CancellationToken);

        // Assert: round one keeps 0, 20 and 40 (the labels at 20 pixels still overlap), round two keeps 0 and 40
        Assert.Equal([labels[1], labels[2], labels[3]], labels.Where(hidden.Contains));
    }

    /// <summary>
    ///     Validates the greedy method: a label that overlaps the last label that stays is hidden.
    /// </summary>
    [Fact]
    public void LabelOverlap_Hidden_GreedyHidesLabelsThatOverlapTheLastOneKept()
    {
        // Arrange: 0 stays, 10 overlaps it, 30 is clear of 0 (which ends at 22.24), 40 overlaps 30 (which ends at 52.24), 70 is clear
        var labels = Labels(0, 10, 30, 40, 70);

        // Act
        var hidden = LabelOverlap.Hidden(labels, new TextMeasurer(), LabelOverlap.Greedy, 0, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([labels[1], labels[3]], labels.Where(hidden.Contains));
    }

    /// <summary>
    ///     Validates that the last label stays visible: when the rounds would hide it, it replaces the last label that was kept.
    /// </summary>
    [Fact]
    public void LabelOverlap_Hidden_TheLastLabelIsKeptVisible()
    {
        // Arrange: four overlapping labels; parity keeps the first and the third and would hide the fourth
        var labels = Labels(0, 5, 10, 15);

        // Act
        var hidden = LabelOverlap.Hidden(labels, new TextMeasurer(), LabelOverlap.Parity, 0, TestContext.Current.CancellationToken);

        // Assert: fewer than three labels were left, so the last one replaces the third
        Assert.Equal([labels[1], labels[2]], labels.Where(hidden.Contains));
    }

    /// <summary>
    ///     Validates the conditions under which nothing is hidden: no method, fewer than three labels, labels that do not overlap, and a
    ///     separation that is met.
    /// </summary>
    [Fact]
    public void LabelOverlap_Hidden_NoMethodFewLabelsOrNoOverlapHidesNothing()
    {
        // Arrange
        var measurer = new TextMeasurer();
        var token = TestContext.Current.CancellationToken;

        // Act / Assert: no method
        Assert.Empty(LabelOverlap.Hidden(Labels(0, 1, 2, 3), measurer, null, 0, token));

        // Act / Assert: two labels overlap but a layout needs three to remove any
        Assert.Empty(LabelOverlap.Hidden(Labels(0, 1), measurer, LabelOverlap.Parity, 0, token));

        // Act / Assert: three labels 30 pixels apart are clear of each other, with no separation and with a separation of 7
        Assert.Empty(LabelOverlap.Hidden(Labels(0, 30, 60), measurer, LabelOverlap.Parity, 0, token));
        Assert.Empty(LabelOverlap.Hidden(Labels(0, 30, 60), measurer, LabelOverlap.Parity, 7, token));

        // Act / Assert: a separation of 8 or more makes the 7.76 pixel gaps overlap
        Assert.NotEmpty(LabelOverlap.Hidden(Labels(0, 30, 60), measurer, LabelOverlap.Parity, 8, token));
    }

    /// <summary>
    ///     Validates that labels without content (an empty text) do not take part: they neither overlap nor count.
    /// </summary>
    [Fact]
    public void LabelOverlap_Hidden_LabelsWithoutContentAreSkipped()
    {
        // Arrange: three labels with text that overlap, and one empty label in the middle
        var labels = Labels(0, 10, 20);
        labels.Insert(1, new SceneText("axis-label", 5, 0, [string.Empty]) { FontSize = 10 });

        // Act
        var hidden = LabelOverlap.Hidden(labels, new TextMeasurer(), LabelOverlap.Parity, 0, TestContext.Current.CancellationToken);

        // Assert: the empty label is never hidden; of the other three the middle one goes
        Assert.DoesNotContain(labels[1], hidden);
        Assert.Contains(labels[2], hidden);
        Assert.Single(hidden);
    }

    /// <summary>
    ///     Validates the reading of the <c>labelOverlap</c> setting: true is parity, the two names are themselves, anything else is off.
    /// </summary>
    [Fact]
    public void LabelOverlap_FromSetting_ReadsBooleansAndMethodNames()
    {
        // Arrange
        var config = ConfigResolver.Defaults;

        // Act / Assert: the Vega default of a legend is true
        Assert.Equal(LabelOverlap.Parity, LabelOverlap.FromSetting(config.Get("legend.labelOverlap")));
        Assert.Null(LabelOverlap.FromSetting(null));
        Assert.Null(LabelOverlap.FromSetting(config.Get("legend.gradientDirection")));
        Assert.Null(LabelOverlap.FromSetting(config.Get("axis.grid")));
        Assert.Equal(LabelOverlap.Parity, LabelOverlap.FromSetting(ConfigNode.FromString("parity")));
        Assert.Equal(LabelOverlap.Greedy, LabelOverlap.FromSetting(ConfigNode.FromString("greedy")));
        Assert.Null(LabelOverlap.FromSetting(ConfigNode.FromString("other")));
    }

    /// <summary>
    ///     Validates that the removal polls the cancellation token, and that two runs give the same result.
    /// </summary>
    [Fact]
    public void LabelOverlap_Hidden_PollsCancellationAndIsDeterministic()
    {
        // Arrange
        var labels = Labels(0, 10, 20, 30, 40);
        var measurer = new TextMeasurer();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        // Act
        var first = LabelOverlap.Hidden(labels, measurer, LabelOverlap.Greedy, 0, TestContext.Current.CancellationToken);
        var second = LabelOverlap.Hidden(labels, measurer, LabelOverlap.Greedy, 0, TestContext.Current.CancellationToken);

        // Assert: the same labels are hidden, and a canceled token stops the work as soon as a round is needed
        Assert.Equal(labels.Where(first.Contains), labels.Where(second.Contains));
        Assert.Throws<OperationCanceledException>(() => LabelOverlap.Hidden(labels, measurer, LabelOverlap.Parity, 0, cancelled.Token));
    }
}
