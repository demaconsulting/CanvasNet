using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Guides;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of <see cref="LabelExpressions"/>: the datum of a guide label, the coercion of the result like Vega's text mark, the limits of the shared evaluator and the compilation of configured text.
/// </summary>
public class LabelExpressionsTests
{
    /// <summary>The location the tests report at.</summary>
    private static readonly VegaLiteLocation At = new("t.vl.json", "/encoding/x/axis/labelExpr", 3, 9);

    /// <summary>
    ///     Evaluates a label expression against a datum.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="datum">The datum.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The text of the label.</returns>
    private static LabelText Run(string text, DataValue datum, VegaLiteLimits? limits = null)
    {
        var labels = new LabelExpressions(null, limits);
        return labels.Evaluate(labels.Resolve(null, text, At)!, datum);
    }

    /// <summary>
    ///     Validates that <c>datum.value</c> and <c>datum.label</c> are bound as given.
    /// </summary>
    [Fact]
    public void LabelExpressions_Evaluate_ValueAndLabelAreBound()
    {
        // Act
        var result = Run("datum.label + ':' + datum.value", LabelExpressions.Datum(DataValue.FromNumber(20), "twenty"));

        // Assert
        Assert.Equal("twenty:20", result.Text);
        Assert.Null(result.Lines);
    }

    /// <summary>
    ///     Validates that the axis index is the fraction of Vega and the tick index a whole number, and that members a guide does not give are null.
    /// </summary>
    [Fact]
    public void LabelExpressions_Evaluate_AxisIndexIsFractionAndTickIndexIsInteger()
    {
        // Arrange: the third of five ticks has the fraction 0.5 and the index 2
        var datum = LabelExpressions.Datum(DataValue.FromNumber(3), "3", 2 / 4.0, 2);

        // Act
        var result = Run("datum.index + '/' + datum.tickIndex + '/' + datum.perc + '/' + datum.other", datum);

        // Assert
        Assert.Equal("0.5/2/null/null", result.Text);
    }

    /// <summary>
    ///     Validates that a null result is the empty text and a missing member reads as null (an expression that chooses blank labels).
    /// </summary>
    [Fact]
    public void LabelExpressions_Evaluate_NullAndMissingMemberGiveEmptyText()
    {
        // Act
        var blank = Run("null", LabelExpressions.Datum(DataValue.FromNumber(1), "1"));
        var missing = Run("datum.nothing", LabelExpressions.Datum(DataValue.FromNumber(1), "1"));

        // Assert
        Assert.Equal(string.Empty, blank.Text);
        Assert.Equal(string.Empty, missing.Text);
    }

    /// <summary>
    ///     Validates that numbers, booleans and dates are converted with JavaScript's string conversion.
    /// </summary>
    [Fact]
    public void LabelExpressions_Evaluate_NumberBooleanAndDateAreCoercedLikeJavaScript()
    {
        // Arrange
        var datum = LabelExpressions.Datum(DataValue.FromDate(Date(2024, 3, 5)), "x");

        // Act
        var number = Run("1e21", datum);
        var flag = Run("datum.value > 0", datum);
        var date = Run("datum.value", datum);
        var year = Run("year(datum.value) + '/' + timeFormat(datum.value, '%b')", datum);

        // Assert: the date is a date (not a number); the engine writes a date as its ISO text
        Assert.Equal("1e+21", number.Text);
        Assert.Equal("true", flag.Text);
        Assert.Equal("2024-03-05T00:00:00.000Z", date.Text);
        Assert.Equal("2024/Mar", year.Text);
    }

    /// <summary>
    ///     Validates that an array gives the lines of a multi-line label, each element trimmed and a null element empty.
    /// </summary>
    [Fact]
    public void LabelExpressions_Evaluate_ArrayGivesLinesWithElementsTrimmed()
    {
        // Act
        var result = Run("[' one ', null, 2]", LabelExpressions.Datum(DataValue.FromNumber(1), "1"));

        // Assert
        Assert.Equal(["one", string.Empty, "2"], result.Lines);
        Assert.Equal("one\n\n2", result.Text);
    }

    /// <summary>
    ///     Validates that the white space around a text result is removed, as the text mark does.
    /// </summary>
    [Fact]
    public void LabelExpressions_Evaluate_ResultIsTrimmed()
    {
        // Act
        var result = Run("'  \\u00A0a b\\t '", LabelExpressions.Datum(DataValue.FromNumber(1), "1"));

        // Assert
        Assert.Equal("a b", result.Text);
    }

    /// <summary>
    ///     Validates that a parameter of the call can be read by a label expression and that the steps are charged to the budget of the call.
    /// </summary>
    [Fact]
    public void LabelExpressions_Evaluate_ParameterIsReadAndStepsAreChargedToTheCall()
    {
        // Arrange: the expressions of a chart with a variable parameter
        var parts = ChartTestHelpers.Parts("""
            { "params": [ { "name": "unit", "value": " kg" } ],
              "data": { "values": [ { "v": 1 } ] }, "mark": "point",
              "encoding": { "x": { "field": "v", "type": "quantitative" } } }
            """);
        var labels = new LabelExpressions(parts.Expressions, null);
        var before = parts.Expressions!.Evaluator.StepsUsed;

        // Act
        var result = labels.Evaluate(labels.Resolve(null, "datum.label + unit", At)!, LabelExpressions.Datum(DataValue.FromNumber(5), "5"));

        // Assert
        Assert.Equal("5 kg", result.Text);
        Assert.True(parts.Expressions.Evaluator.StepsUsed > before);
    }

    /// <summary>
    ///     Validates that the step budget is the one of the shared evaluator: an expression beyond it is a located render exception.
    /// </summary>
    [Fact]
    public void LabelExpressions_Evaluate_StepBudgetIsEnforcedAsRenderException()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxEvaluationSteps = 5 };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Run("1 + 2 + 3 + 4 + 5 + 6 + 7 + 8", LabelExpressions.Datum(DataValue.FromNumber(1), "1"), limits));

        // Assert
        Assert.Contains("MaxEvaluationSteps", error.Message, StringComparison.Ordinal);
        Assert.Equal(At, error.Location);
    }

    /// <summary>
    ///     Validates that a result longer than the string limit is a located render exception.
    /// </summary>
    [Fact]
    public void LabelExpressions_Evaluate_StringLimitIsEnforcedAsRenderException()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxStringLength = 8 };

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Run("datum.label + datum.label", LabelExpressions.Datum(DataValue.FromNumber(1), "12345"), limits));

        // Assert
        Assert.Contains("MaxStringLength", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the text of a configured expression is compiled once and reused, and that no text gives no expression.
    /// </summary>
    [Fact]
    public void LabelExpressions_Resolve_ConfiguredTextIsCompiledOnceAndCached()
    {
        // Arrange
        var labels = new LabelExpressions(null, null);

        // Act
        var first = labels.Resolve(null, "datum.label", At);
        var second = labels.Resolve(null, "datum.label", At);
        var none = labels.Resolve(null, null, At);
        var empty = labels.Resolve(null, string.Empty, At);

        // Assert
        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Null(none);
        Assert.Null(empty);
    }

    /// <summary>
    ///     Validates that configured text that is not an expression is a specification error at the location of the guide.
    /// </summary>
    [Fact]
    public void LabelExpressions_Resolve_ConfiguredSyntaxErrorIsSpecException()
    {
        // Arrange
        var labels = new LabelExpressions(null, null);

        // Act
        var error = Assert.Throws<VegaLiteSpecException>(() => labels.Resolve(null, "datum.label +", At));

        // Assert
        Assert.Equal(At, error.Location);
    }

    /// <summary>
    ///     Validates that a function outside the table (the number format) evaluates to null and so to the empty text.
    /// </summary>
    [Fact]
    public void LabelExpressions_Evaluate_UnsupportedFunctionIsNullAndEmpty()
    {
        // Act
        var result = Run("format(datum.value, ',.0f')", LabelExpressions.Datum(DataValue.FromNumber(1200), "1,200"));

        // Assert
        Assert.Equal(string.Empty, result.Text);
    }

    /// <summary>
    ///     Gets the milliseconds of a UTC date.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month, 1 to 12.</param>
    /// <param name="day">The day.</param>
    /// <returns>The milliseconds since the epoch.</returns>
    private static double Date(int year, int month, int day) => DataValue.MillisecondsSinceEpoch(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));
}
