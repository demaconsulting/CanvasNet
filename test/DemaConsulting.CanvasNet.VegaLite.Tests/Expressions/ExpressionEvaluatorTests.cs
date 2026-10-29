using System.Collections.Concurrent;
using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Expressions.ExpressionTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

/// <summary>
///     Tests of the expression evaluator: JavaScript coercion and comparison semantics, short-circuit evaluation,
///     datum and parameter access, the step budget, cancellation, culture invariance and determinism, with expected
///     values written out by hand from the ECMAScript rules.
/// </summary>
public class ExpressionEvaluatorTests
{
    /// <summary>
    ///     Validates arithmetic and string concatenation with JavaScript coercions.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected value as JavaScript text.</param>
    [Theory]
    [InlineData("1 + 2", "3")]
    [InlineData("'a' + 1", "a1")]
    [InlineData("1 + '2'", "12")]
    [InlineData("'3' * '4'", "12")]
    [InlineData("'5' - 2", "3")]
    [InlineData("'6' / '3'", "2")]
    [InlineData("null + 1", "1")]
    [InlineData("true + 1", "2")]
    [InlineData("true + true", "2")]
    [InlineData("null + null", "0")]
    [InlineData("'a ' + null", "a null")]
    [InlineData("'a ' + true", "a true")]
    [InlineData("[1, 2] + 3", "1,23")]
    [InlineData("[] + []", "")]
    [InlineData("[] + 1", "1")]
    [InlineData("({}) + 1", "[object Object]1")]
    [InlineData("'x' - 1", "NaN")]
    [InlineData("'' - 1", "-1")]
    [InlineData("' 12 ' * 2", "24")]
    [InlineData("'0x10' - 0", "16")]
    [InlineData("'1e2' - 0", "100")]
    [InlineData("'Infinity' - 0", "Infinity")]
    [InlineData("'infinity' - 0", "NaN")]
    [InlineData("'1_0' - 0", "NaN")]
    [InlineData("1 / 0", "Infinity")]
    [InlineData("-1 / 0", "-Infinity")]
    [InlineData("0 / 0", "NaN")]
    [InlineData("5 % 0", "NaN")]
    [InlineData("-5 % 3", "-2")]
    [InlineData("5 % -3", "2")]
    [InlineData("5.5 % 2", "1.5")]
    [InlineData("Infinity % 2", "NaN")]
    [InlineData("5 % Infinity", "5")]
    [InlineData("0.1 + 0.2", "0.30000000000000004")]
    [InlineData("1e21 + 1", "1e+21")]
    [InlineData("2 * 0.5", "1")]
    public void ExpressionEvaluator_Evaluate_ArithmeticAndConcatenation_FollowJavaScript(string text, string expected)
    {
        // Arrange / Act
        var value = EvalText(text);

        // Assert
        Assert.Equal(expected, value);
    }

    /// <summary>
    ///     Validates the unary operators; <c>~</c> is a deferred construct and is tested with the parser.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected value as JavaScript text.</param>
    [Theory]
    [InlineData("+'5'", "5")]
    [InlineData("+true", "1")]
    [InlineData("+null", "0")]
    [InlineData("+'x'", "NaN")]
    [InlineData("-'5'", "-5")]
    [InlineData("-null", "0")]
    [InlineData("!0", "true")]
    [InlineData("!''", "true")]
    [InlineData("!NaN", "true")]
    [InlineData("!'0'", "false")]
    [InlineData("![]", "false")]
    [InlineData("!!{}", "true")]
    public void ExpressionEvaluator_Evaluate_UnaryOperators_FollowJavaScript(string text, string expected)
    {
        // Arrange / Act: evaluate twice to show that the result does not depend on evaluator state
        var first = EvalText(text);
        var second = EvalText(text);

        // Assert
        Assert.Equal(expected, first);
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Validates that negative zero is kept by multiplication and unary minus where JavaScript keeps it.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_NegativeZero_IsPreserved()
    {
        // Arrange / Act / Assert: the sign of zero shows through the sign of the infinity of a reciprocal
        Assert.True(double.IsNegativeInfinity(EvalNumber("1 / (-0)")));
        Assert.True(double.IsNegativeInfinity(EvalNumber("1 / (0 * -1)")));
        Assert.True(double.IsPositiveInfinity(EvalNumber("1 / (0 * 1)")));
        Assert.Equal("0", EvalText("-0"));
    }

    /// <summary>
    ///     Validates strict and loose equality against the ECMAScript tables.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("1 === 1.0", true)]
    [InlineData("'1' === 1", false)]
    [InlineData("'a' === 'a'", true)]
    [InlineData("null === null", true)]
    [InlineData("NaN === NaN", false)]
    [InlineData("0 === -0", true)]
    [InlineData("true === 1", false)]
    [InlineData("[1] === [1]", false)]
    [InlineData("'1' !== 1", true)]
    [InlineData("'1' == 1", true)]
    [InlineData("1 == '1'", true)]
    [InlineData("null == 0", false)]
    [InlineData("null == null", true)]
    [InlineData("null == ''", false)]
    [InlineData("null != 0", true)]
    [InlineData("true == 1", true)]
    [InlineData("true == '1'", true)]
    [InlineData("true == 'true'", false)]
    [InlineData("'' == 0", true)]
    [InlineData("'0' == false", true)]
    [InlineData("'abc' == 'abc'", true)]
    [InlineData("NaN == NaN", false)]
    [InlineData("NaN != NaN", true)]
    [InlineData("[1] == 1", true)]
    [InlineData("[1, 2] == '1,2'", true)]
    [InlineData("[] == ''", true)]
    [InlineData("[] == 0", true)]
    [InlineData("[1] == [1]", false)]
    [InlineData("({}) == '[object Object]'", true)]
    [InlineData("({}) == ({})", false)]
    [InlineData("1 != 2", true)]
    public void ExpressionEvaluator_Evaluate_Equality_FollowsEcmaScriptTables(string text, bool expected)
    {
        // Arrange / Act
        var value = Eval(text);

        // Assert
        Assert.True(value.IsBoolean);
        Assert.Equal(expected, value.Boolean);
    }

    /// <summary>
    ///     Validates relational comparison: numeric, string (ordinal) and mixed operands, NaN and null.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("1 < 2", true)]
    [InlineData("2 < 1", false)]
    [InlineData("1 <= 1", true)]
    [InlineData("1 >= 2", false)]
    [InlineData("2 > 1", true)]
    [InlineData("'a' < 'b'", true)]
    [InlineData("'B' < 'a'", true)]
    [InlineData("'10' < '9'", true)]
    [InlineData("10 < '9'", false)]
    [InlineData("'10' < 9", false)]
    [InlineData("'a' < 1", false)]
    [InlineData("'a' >= 1", false)]
    [InlineData("NaN <= NaN", false)]
    [InlineData("NaN >= NaN", false)]
    [InlineData("1 < NaN", false)]
    [InlineData("null < 1", true)]
    [InlineData("null >= 0", true)]
    [InlineData("null > -1", true)]
    [InlineData("true > false", true)]
    [InlineData("[2] > 1", true)]
    [InlineData("'abc' <= 'abc'", true)]
    [InlineData("'abc' < 'abcd'", true)]
    [InlineData("Infinity > 1e308", true)]
    public void ExpressionEvaluator_Evaluate_RelationalComparison_FollowsEcmaScript(string text, bool expected)
    {
        // Arrange / Act: the comparison and its logical negation
        var value = Eval(text);
        var negated = Eval("!(" + text + ")");

        // Assert
        Assert.True(value.IsBoolean);
        Assert.Equal(expected, value.Boolean);
        Assert.Equal(!expected, negated.Boolean);
    }

    /// <summary>
    ///     Validates that the logical operators return an operand rather than a boolean.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected value as JavaScript text.</param>
    [Theory]
    [InlineData("0 || 'x'", "x")]
    [InlineData("'a' || 'b'", "a")]
    [InlineData("'a' && 'b'", "b")]
    [InlineData("null && 1", "null")]
    [InlineData("'' || 0", "0")]
    [InlineData("0 && 'x'", "0")]
    [InlineData("null || null", "null")]
    [InlineData("1 && 2 && 3", "3")]
    [InlineData("1 && 0 && 3", "0")]
    [InlineData("0 || null || 'z'", "z")]
    [InlineData("NaN || 5", "5")]
    [InlineData("0 ? 1 : 2", "2")]
    [InlineData("'x' ? 1 : 2", "1")]
    [InlineData("null ? 1 : 2", "2")]
    [InlineData("[] ? 1 : 2", "1")]
    public void ExpressionEvaluator_Evaluate_LogicalOperators_ReturnOperands(string text, string expected)
    {
        // Arrange / Act
        var value = Eval(text);

        // Assert: the text of the operand, and null exactly when the text says so
        Assert.Equal(expected, value.ToText());
        Assert.Equal(expected == "null", value.IsNull);
    }

    /// <summary>
    ///     Validates that short-circuit operators and the conditional skip the operand they do not need, proven by a step
    ///     budget that the skipped operand would exceed.
    /// </summary>
    /// <param name="text">The expression text; the operand after the operator costs 17 instructions.</param>
    /// <param name="skipped">Whether the expensive operand is skipped.</param>
    [Theory]
    [InlineData("0 && (1+2+3+4+5+6+7+8+9)", true)]
    [InlineData("1 || (1+2+3+4+5+6+7+8+9)", true)]
    [InlineData("1 ? 5 : (1+2+3+4+5+6+7+8+9)", true)]
    [InlineData("0 ? (1+2+3+4+5+6+7+8+9) : 5", true)]
    [InlineData("1 && (1+2+3+4+5+6+7+8+9)", false)]
    [InlineData("0 || (1+2+3+4+5+6+7+8+9)", false)]
    [InlineData("0 ? 5 : (1+2+3+4+5+6+7+8+9)", false)]
    public void ExpressionEvaluator_Evaluate_ShortCircuit_SkipsTheOtherOperand(string text, bool skipped)
    {
        // Arrange: a budget of five steps; the skipped paths use at most four
        var limits = Steps(5);
        var evaluator = new ExpressionEvaluator(limits, CancellationToken.None);
        var compiled = Compile(text, limits);

        // Act / Assert
        if (skipped)
        {
            evaluator.Evaluate(compiled);
            Assert.True(evaluator.StepsUsed <= 5);
            return;
        }

        var exception = Assert.Throws<VegaLiteRenderException>(() => evaluator.Evaluate(compiled));
        Assert.Contains("MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that array indexing, member access and string properties follow JavaScript, with null for anything
    ///     missing.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected value as JavaScript text.</param>
    [Theory]
    [InlineData("[10, 20, 30][1]", "20")]
    [InlineData("[10, 20, 30][3]", "null")]
    [InlineData("[10, 20, 30][-1]", "null")]
    [InlineData("[10, 20, 30][1.5]", "null")]
    [InlineData("[10, 20, 30]['1']", "20")]
    [InlineData("[10, 20, 30]['01']", "null")]
    [InlineData("[1, 2, 3].length", "3")]
    [InlineData("[1, 2]['length']", "2")]
    [InlineData("'abc'.length", "3")]
    [InlineData("'abc'[1]", "b")]
    [InlineData("'abc'[5]", "null")]
    [InlineData("{a: {b: 2}}.a.b", "2")]
    [InlineData("{'k j': 5}['k j']", "5")]
    [InlineData("{a: 1}['a']", "1")]
    [InlineData("{a: 1}.b", "null")]
    [InlineData("{a: 1, b: 2, a: 3}.a", "3")]
    [InlineData("{1: 'one'}[1]", "one")]
    [InlineData("null.x", "null")]
    [InlineData("(1).x", "null")]
    [InlineData("true.x", "null")]
    [InlineData("[[1, 2], [3]][0][1]", "2")]
    [InlineData("/ab/gi.source", "ab")]
    [InlineData("/ab/gi.flags", "gi")]
    [InlineData("/ab/g.global", "true")]
    [InlineData("/ab/.global", "false")]
    [InlineData("/ab/.nothing", "null")]
    public void ExpressionEvaluator_Evaluate_MemberAccess_FollowsJavaScriptAndIsNullSafe(string text, string expected)
    {
        // Arrange: a compiled expression and an evaluator that serves several evaluations
        var compiled = Compile(text);
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act
        var first = evaluator.Evaluate(compiled).ToText();
        var second = evaluator.Evaluate(compiled).ToText();

        // Assert
        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
    }

    /// <summary>
    ///     Validates that an object literal keeps the position of a repeated key and takes its last value.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_ObjectLiteral_KeepsFirstPositionAndLastValue()
    {
        // Arrange / Act
        var value = Eval("{a: 1, b: 2, a: 3}");

        // Assert
        Assert.Equal(["a", "b"], value.ObjectValue!.Keys);
        Assert.Equal(3, value.ObjectValue.ValueAt(0).Number);
    }

    /// <summary>
    ///     Validates that composite values compare by identity and that the same object read twice is the same value.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_Composites_CompareByIdentity()
    {
        // Arrange: a datum object
        var datum = Obj(("o", Obj(("k", DataValue.FromNumber(1)))));

        // Act / Assert: one read is identical to another read of the same object, a literal is never identical
        Assert.True(EvalWith("datum.o === datum.o", datum).Boolean);
        Assert.True(EvalWith("datum === datum", datum).Boolean);
        Assert.False(EvalWith("datum.o === {k: 1}", datum).Boolean);
    }

    /// <summary>
    ///     Validates reading fields of a table row, including a flattened nested object, an escaped dot and missing
    ///     fields, and that the whole datum is rebuilt as an object.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_TableRow_ReadsFlattenedFields()
    {
        // Arrange: fields "a.b", "a.c", "a\.d" (a literal dot), "x" and "name with space"
        var fields = new List<string> { "a.b", "a.c", "a\\.d", "x", "name with space" };
        var rows = new List<IReadOnlyList<DataValue>>
        {
            new List<DataValue> { DataValue.FromNumber(1), DataValue.FromString("c1"), DataValue.FromNumber(4), DataValue.FromNumber(10), DataValue.FromBoolean(true) },
            new List<DataValue> { DataValue.FromNumber(2), DataValue.Null, DataValue.FromNumber(5), DataValue.FromNumber(20), DataValue.FromBoolean(false) },
        };
        var table = DataTable.Create(fields, rows);
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act / Assert: plain, nested, escaped and spaced fields of the second row
        Assert.Equal(20, evaluator.Evaluate(Compile("datum.x"), table, 1).Number);
        Assert.Equal(20, evaluator.Evaluate(Compile("datum['x']"), table, 1).Number);
        Assert.Equal(2, evaluator.Evaluate(Compile("datum.a.b"), table, 1).Number);
        Assert.Equal(5, evaluator.Evaluate(Compile("datum['a.d']"), table, 1).Number);
        Assert.False(evaluator.Evaluate(Compile("datum['name with space']"), table, 1).Boolean);
        Assert.True(evaluator.Evaluate(Compile("datum.a.c"), table, 1).IsNull);
        Assert.True(evaluator.Evaluate(Compile("datum.missing"), table, 0).IsNull);
        Assert.True(evaluator.Evaluate(Compile("datum.a.missing"), table, 0).IsNull);

        // Assert: the nested object has its two members in field order
        var nested = evaluator.Evaluate(Compile("datum.a"), table, 0);
        Assert.True(nested.IsObject);
        Assert.Equal(["b", "c"], nested.ObjectValue!.Keys);
        Assert.Equal("c1", nested.ObjectValue.ValueAt(1).Text);

        // Assert: dynamic keys and the whole datum
        Assert.Equal(10, evaluator.Evaluate(Compile("datum['x' + '']"), table, 0).Number);
        var whole = evaluator.Evaluate(Compile("datum"), table, 0);
        Assert.True(whole.IsObject);
        Assert.Equal(["a", "a.d", "name with space", "x"], whole.ObjectValue!.Keys);
        Assert.Equal("1,c1", evaluator.Evaluate(Compile("[datum.a.b, datum.a.c]"), table, 0).ToText());
    }

    /// <summary>
    ///     Validates that the fields <c>tags.0</c> and <c>tags.1</c> that the data loader makes of an array are an array
    ///     again (<c>isArray</c>, <c>length</c>, indexing), also below an object, and that the whole datum holds them as arrays.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_TableRow_RebuildsIndexedFieldsAsArrays()
    {
        // Arrange: tags: ["a", "b"], o: {list: [1, 2, 3], k: "v"} and an object whose keys only look like a partial index set
        var fields = new List<string> { "gap.0", "gap.2", "o.k", "o.list.0", "o.list.1", "o.list.2", "tags.0", "tags.1" };
        var row = new List<DataValue>
        {
            DataValue.FromNumber(7), DataValue.FromNumber(9), DataValue.FromString("v"), DataValue.FromNumber(1), DataValue.FromNumber(2),
            DataValue.FromNumber(3), DataValue.FromString("a"), DataValue.FromString("b"),
        };
        var table = DataTable.Create(fields, [row]);
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act / Assert: an array has the array kind, its length and its elements
        Assert.True(evaluator.Evaluate(Compile("isArray(datum.tags)"), table, 0).Boolean);
        Assert.Equal(2, evaluator.Evaluate(Compile("datum.tags.length"), table, 0).Number);
        Assert.Equal("b", evaluator.Evaluate(Compile("datum.tags[1]"), table, 0).Text);
        Assert.Equal("a,b", evaluator.Evaluate(Compile("toString(datum.tags)"), table, 0).Text);
        Assert.Equal(3, evaluator.Evaluate(Compile("length(datum.o.list)"), table, 0).Number);
        Assert.True(evaluator.Evaluate(Compile("isArray(datum['o'].list) && !isArray(datum.o)"), table, 0).Boolean);

        // Assert: names that are not exactly the indexes 0 to n-1 stay an object
        Assert.False(evaluator.Evaluate(Compile("isArray(datum.gap)"), table, 0).Boolean);
        Assert.Equal(9, evaluator.Evaluate(Compile("datum.gap['2']"), table, 0).Number);

        // Assert: the whole datum is an object whose members are the rebuilt arrays
        var whole = evaluator.Evaluate(Compile("datum"), table, 0);
        Assert.True(whole.IsObject);
        Assert.True(whole.ObjectValue!.TryGetValue("tags", out var tags));
        Assert.True(tags.IsArray);
        Assert.Equal(["a", "b"], tags.Elements.ToArray().Select(e => e.Text));
    }

    /// <summary>
    ///     Validates that a composite that is rebuilt from flattened fields is the same value every time it is read in one
    ///     evaluation (JavaScript identity), whatever the way it is reached, and a different one in another row.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_TableRow_RebuiltCompositesHaveStableIdentity()
    {
        // Arrange: a: {b: {c: 1}}, tags: ["a", "b"] in two rows
        var fields = new List<string> { "a.b.c", "tags.0", "tags.1" };
        var rows = new List<IReadOnlyList<DataValue>>
        {
            new List<DataValue> { DataValue.FromNumber(1), DataValue.FromString("a"), DataValue.FromString("b") },
            new List<DataValue> { DataValue.FromNumber(2), DataValue.FromString("c"), DataValue.FromString("d") },
        };
        var table = DataTable.Create(fields, rows);
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act / Assert: the same path read twice, by member, by index and nested
        Assert.True(evaluator.Evaluate(Compile("datum.a === datum.a"), table, 0).Boolean);
        Assert.True(evaluator.Evaluate(Compile("datum.a === datum['a']"), table, 0).Boolean);
        Assert.True(evaluator.Evaluate(Compile("datum.a.b === datum.a.b"), table, 0).Boolean);
        Assert.True(evaluator.Evaluate(Compile("datum.a.b === datum['a'].b"), table, 0).Boolean);
        Assert.True(evaluator.Evaluate(Compile("datum.tags === datum.tags"), table, 0).Boolean);
        Assert.True(evaluator.Evaluate(Compile("datum === datum"), table, 0).Boolean);
        Assert.True(evaluator.Evaluate(Compile("datum.tags !== [datum.tags[0], datum.tags[1]]"), table, 0).Boolean);

        // Assert: a path read inside the whole datum and on its own, in both orders, is one value
        var wholeFirst = evaluator.Evaluate(Compile("[datum, datum.tags, datum.a.b]"), table, 0).Elements;
        var wholeObject = wholeFirst[0].ObjectValue!;
        Assert.True(wholeObject.TryGetValue("tags", out var tagsInWhole));
        Assert.True(ExpressionEvaluator.StrictEquals(tagsInWhole, wholeFirst[1]));
        Assert.True(wholeObject.TryGetValue("a", out var aInWhole));
        Assert.True(aInWhole.ObjectValue!.TryGetValue("b", out var bInWhole));
        Assert.True(ExpressionEvaluator.StrictEquals(bInWhole, wholeFirst[2]));
        var tagsFirst = evaluator.Evaluate(Compile("[datum.tags, datum]"), table, 0).Elements;
        Assert.True(tagsFirst[1].ObjectValue!.TryGetValue("tags", out var tagsLater));
        Assert.True(ExpressionEvaluator.StrictEquals(tagsFirst[0], tagsLater));

        // Assert: another row gives its own values, and the first row is unaffected by what the cache held
        Assert.Equal("c,d", evaluator.Evaluate(Compile("toString(datum.tags)"), table, 1).Text);
        Assert.Equal("a,b", evaluator.Evaluate(Compile("toString(datum.tags)"), table, 0).Text);
        Assert.Equal(2, evaluator.Evaluate(Compile("datum.a.b.c"), table, 1).Number);
    }

    /// <summary>
    ///     Validates a field that is null in a row and has fields below it (a column that holds a scalar in some rows and an
    ///     array in others): the row that is nested reads as the array, the others as their scalar or null.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_TableRow_NullFieldWithDescendantsReadsAsComposite()
    {
        // Arrange: row 0 has the array ["x"] (tags is null, tags.0 is "x"), row 1 the scalar "s", row 2 nothing
        var fields = new List<string> { "tags", "tags.0" };
        var rows = new List<IReadOnlyList<DataValue>>
        {
            new List<DataValue> { DataValue.Null, DataValue.FromString("x") },
            new List<DataValue> { DataValue.FromString("s"), DataValue.Null },
            new List<DataValue> { DataValue.Null, DataValue.Null },
        };
        var table = DataTable.Create(fields, rows);
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act / Assert
        Assert.True(evaluator.Evaluate(Compile("isArray(datum.tags) && datum.tags.length === 1 && datum.tags === datum.tags"), table, 0).Boolean);
        Assert.Equal("s", evaluator.Evaluate(Compile("datum.tags"), table, 1).Text);
        Assert.True(evaluator.Evaluate(Compile("datum.tags"), table, 2).IsNull);
    }

    /// <summary>
    ///     Validates that a row without any field (an inline empty object) is the empty object as a whole datum, not null, and
    ///     that every read of it is the same value.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_TableRow_EmptyRowIsTheEmptyObject()
    {
        // Arrange: one row, no fields (inline [{}])
        var table = DataTable.Create([], [new List<DataValue>()]);
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act
        var whole = evaluator.Evaluate(Compile("datum"), table, 0);

        // Assert: an object without members; identity is stable; a field of it is null
        Assert.True(whole.IsObject);
        Assert.Empty(whole.ObjectValue!.Keys);
        Assert.True(evaluator.Evaluate(Compile("isObject(datum)"), table, 0).Boolean);
        Assert.False(evaluator.Evaluate(Compile("isArray(datum)"), table, 0).Boolean);
        Assert.True(evaluator.Evaluate(Compile("datum === datum"), table, 0).Boolean);
        Assert.True(evaluator.Evaluate(Compile("datum.a === null"), table, 0).Boolean);
        Assert.Equal("[object Object]", evaluator.Evaluate(Compile("toString(datum)"), table, 0).Text);
    }

    /// <summary>
    ///     Validates that a row whose field names are the indexes 0 to n-1 is still an object as a whole datum.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_TableRow_WholeDatumWithIndexNamesIsObject()
    {
        // Arrange
        var table = DataTable.Create(["0", "1"], [new List<DataValue> { DataValue.FromNumber(5), DataValue.FromNumber(6) }]);
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act
        var whole = evaluator.Evaluate(Compile("datum"), table, 0);

        // Assert
        Assert.True(whole.IsObject);
        Assert.Equal(["0", "1"], whole.ObjectValue!.Keys);
    }

    /// <summary>
    ///     Validates that a text that comes from outside the evaluation (a parameter, a field of the data) is bounded by
    ///     <see cref="VegaLiteLimits.MaxStringLength"/> when it is the result or is converted to text.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_ExternalStringLongerThanLimit_IsLocatedRenderError()
    {
        // Arrange: a parameter and a field of 12 characters under a limit of 10
        var limits = new VegaLiteLimits { MaxStringLength = 10 };
        var parameters = new Dictionary<string, DataValue> { ["p"] = DataValue.FromString(new string('x', 12)) };
        var table = DataTable.Create(["s"], [new List<DataValue> { DataValue.FromString(new string('y', 12)) }]);
        var evaluator = new ExpressionEvaluator(limits, CancellationToken.None, parameters);

        // Act
        var result = Assert.Throws<VegaLiteRenderException>(() => Eval("p", limits, parameters));
        var converted = Assert.Throws<VegaLiteRenderException>(() => Eval("toString(p)", limits, parameters));
        var field = Assert.Throws<VegaLiteRenderException>(() => evaluator.Evaluate(Compile("datum.s", limits), table, 0));
        var kept = Eval("length(p)", limits, parameters);

        // Assert: located at the expression, naming the limit; reading the text without keeping it is not a failure
        Assert.Equal(Location, result.Location);
        Assert.Contains("the limit of 10 characters (VegaLiteLimits.MaxStringLength)", result.Message, StringComparison.Ordinal);
        Assert.Contains("the limit of 10 characters (VegaLiteLimits.MaxStringLength)", converted.Message, StringComparison.Ordinal);
        Assert.Contains("the limit of 10 characters (VegaLiteLimits.MaxStringLength)", field.Message, StringComparison.Ordinal);
        Assert.Equal(12, kept.Number);
    }

    /// <summary>
    ///     Validates that the texts inside an array or object literal are bounded by <see cref="VegaLiteLimits.MaxStringLength"/>
    ///     at any depth, like a scalar result, and that the failure is located at the expression.
    /// </summary>
    /// <param name="text">The expression text.</param>
    [Theory]
    [InlineData("['0123456789']")]
    [InlineData("{a: '0123456789'}")]
    [InlineData("[1, [2, {k: ['0123456789']}]]")]
    [InlineData("{'0123456789': 1}")]
    public void ExpressionEvaluator_Evaluate_CompositeWithTextLongerThanLimit_IsLocatedRenderError(string text)
    {
        // Arrange: compiled under the default limits and evaluated under a limit of 5
        var limits = new VegaLiteLimits { MaxStringLength = 5 };
        var evaluator = new ExpressionEvaluator(limits, CancellationToken.None);

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => evaluator.Evaluate(Compile(text)));
        var fits = evaluator.Evaluate(Compile("[1, ['abc'], {k: 'abcde'}]"));

        // Assert
        Assert.Equal(Location, exception.Location);
        Assert.Contains("the limit of 5 characters (VegaLiteLimits.MaxStringLength)", exception.Message, StringComparison.Ordinal);
        Assert.True(fits.IsArray);
    }

    /// <summary>
    ///     Validates that every conversion to text is bounded by <see cref="VegaLiteLimits.MaxStringLength"/>, not only a
    ///     string or an array: a number, a boolean and a regular expression converted to text longer than the limit fail even
    ///     when the text is only measured.
    /// </summary>
    /// <param name="text">The expression text.</param>
    [Theory]
    [InlineData("length(toString(123))")]
    [InlineData("length(toString(true))")]
    [InlineData("length(toString(/abc/))")]
    [InlineData("length(123 + '')")]
    public void ExpressionEvaluator_Evaluate_ConversionLongerThanLimit_IsRenderError(string text)
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxStringLength = 2 };

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Eval(text, limits));
        var fits = Eval("length(toString(12))", limits);

        // Assert
        Assert.Contains("the limit of 2 characters (VegaLiteLimits.MaxStringLength)", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, fits.Number);
    }

    /// <summary>
    ///     Validates that a table row and an out-of-range row are checked.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_TableRowOutOfRange_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var table = DataTable.Create(["x"], [new List<DataValue> { DataValue.FromNumber(1) }]);
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);
        var compiled = Compile("datum.x");

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => evaluator.Evaluate(compiled, table, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => evaluator.Evaluate(compiled, table, -1));
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(compiled, (DataTable)null!, 0));
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(compiled, (IExpressionDatum)null!));
    }

    /// <summary>
    ///     Validates that the datum is null when the evaluation has none, and that a non-object datum has no fields.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_WithoutDatum_ReadsNull()
    {
        // Arrange / Act / Assert
        Assert.True(Eval("datum").IsNull);
        Assert.True(Eval("datum.x").IsNull);
        Assert.True(EvalWith("datum.x", DataValue.FromNumber(3)).IsNull);
        Assert.Equal(3, EvalWith("datum", DataValue.FromNumber(3)).Number);
    }

    /// <summary>
    ///     Validates dates: the milliseconds in arithmetic and comparison, the ISO text in concatenation.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_Dates_UseMillisecondsAndIsoText()
    {
        // Arrange: 2024-03-05T00:00:00Z
        var datum = Obj(("d", DataValue.FromDate(1_709_596_800_000)));

        // Act / Assert
        Assert.Equal(1_709_596_800_000.0, EvalWith("datum.d - 0", datum).Number);
        Assert.Equal(1_709_596_800_000.0, EvalWith("+datum.d", datum).Number);
        Assert.Equal("2024-03-05T00:00:00.000Z!", EvalWith("datum.d + '!'", datum).Text);
        Assert.True(EvalWith("datum.d > 1000", datum).Boolean);
        Assert.True(EvalWith("datum.d == datum.d", datum).Boolean);
        Assert.False(EvalWith("datum.d == 1709596800000", datum).Boolean);
        Assert.True(EvalWith("isDate(datum.d) && isObject(datum.d)", datum).Boolean);
    }

    /// <summary>
    ///     Validates parameter lookup: values come from the supplied dictionary by ordinal name, unknown names are null.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_Parameters_AreLookedUpByName()
    {
        // Arrange
        var parameters = new Dictionary<string, DataValue>
        {
            ["p"] = DataValue.FromNumber(21),
            ["Text"] = DataValue.FromString("hi"),
            ["obj"] = Obj(("k", DataValue.FromNumber(3))),
        };

        // Act / Assert
        Assert.Equal(42, Eval("p * 2", parameters: parameters).Number);
        Assert.Equal("hi!", Eval("Text + '!'", parameters: parameters).Text);
        Assert.Equal(3, Eval("obj.k", parameters: parameters).Number);
        Assert.True(Eval("P", parameters: parameters).IsNull);
        Assert.True(Eval("unknown", parameters: parameters).IsNull);
        Assert.True(Eval("p").IsNull);
    }

    /// <summary>
    ///     Validates that unsupported constructs evaluate to null instead of failing.
    /// </summary>
    /// <param name="text">The expression text.</param>
    [Theory]
    [InlineData("unknownFunction(1, 2)")]
    [InlineData("random()")]
    [InlineData("now()")]
    [InlineData("signal")]
    [InlineData("event.x")]
    [InlineData("item()")]
    [InlineData("1 << 2")]
    [InlineData("5 & 3")]
    [InlineData("'a' in {a: 1}")]
    public void ExpressionEvaluator_Evaluate_UnsupportedConstruct_IsNull(string text)
    {
        // Arrange / Act
        var value = Eval(text);

        // Assert
        Assert.True(value.IsNull);
        Assert.NotEmpty(Compile(text).UnsupportedConstructs);
    }

    /// <summary>
    ///     Validates that every executed instruction costs exactly one step and that the budget is exact: the budget of
    ///     the program succeeds and one less fails with a located error naming the failing offset.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_StepBudget_IsExact()
    {
        // Arrange: "1 + 2" is push, push, add
        var compiled = Compile("1 + 2");
        var exactly = new ExpressionEvaluator(Steps(3), CancellationToken.None);
        var tooFew = new ExpressionEvaluator(Steps(2), CancellationToken.None);

        // Act
        var value = exactly.Evaluate(compiled);
        var exception = Assert.Throws<VegaLiteRenderException>(() => tooFew.Evaluate(compiled));

        // Assert: three steps used, and the failure is the add at offset 2 with the location of the expression
        Assert.Equal(3, value.Number);
        Assert.Equal(3, exactly.StepsUsed);
        Assert.Equal(Location, exception.Location);
        Assert.Equal(
            $"{Location}: The evaluation exceeded the limit of 2 steps (VegaLiteLimits.MaxEvaluationSteps) at expression offset 2.",
            exception.Message);
    }

    /// <summary>
    ///     Validates that the budget belongs to the evaluator and is shared by consecutive evaluations of a call.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_StepBudget_AccumulatesAcrossEvaluations()
    {
        // Arrange: a budget for one and a half evaluations of a three-step program
        var compiled = Compile("1 + 2");
        var evaluator = new ExpressionEvaluator(Steps(5), CancellationToken.None);

        // Act
        evaluator.Evaluate(compiled);

        // Assert: the second evaluation runs out; a new evaluator starts with a fresh budget
        Assert.Throws<VegaLiteRenderException>(() => evaluator.Evaluate(compiled));
        Assert.Equal(3, new ExpressionEvaluator(Steps(5), CancellationToken.None).Evaluate(compiled).Number);
    }

    /// <summary>
    ///     Validates that work proportional to the size of a string is charged to the step budget, so a hostile
    ///     expression cannot do unbounded work with few instructions.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_SizeProportionalWork_IsCharged()
    {
        // Arrange: a 640 character string searched by indexof costs 10 extra steps
        var text = "indexof('" + new string('a', 640) + "', 'b')";
        var compiled = Compile(text);
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act
        var value = evaluator.Evaluate(compiled);

        // Assert: the result is -1, the instructions cost 3 steps and the search 10 more
        Assert.Equal(-1, value.Number);
        Assert.Equal(compiled.InstructionCount + 10, evaluator.StepsUsed);
        Assert.Throws<VegaLiteRenderException>(() => new ExpressionEvaluator(Steps(5), CancellationToken.None).Evaluate(Compile(text)));
    }

    /// <summary>
    ///     Validates the string limit on concatenation: a result of exactly the limit is accepted, one more is a located
    ///     render error.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_StringLimit_IsEnforcedOnConcatenation()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxStringLength = 10 };

        // Act
        var atLimit = Eval("'abcde' + '12345'", limits);
        var exception = Assert.Throws<VegaLiteRenderException>(() => Eval("'abcde' + '123456'", limits));

        // Assert
        Assert.Equal("abcde12345", atLimit.Text);
        Assert.Equal(Location, exception.Location);
        Assert.Contains("MaxStringLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled token stops an evaluation before it starts.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_CanceledBeforeStart_ThrowsOperationCanceled()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var evaluator = new ExpressionEvaluator(null, cancellation.Token);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => evaluator.Evaluate(Compile("1")));
    }

    /// <summary>
    ///     Validates that cancellation during a long evaluation is noticed within 1,024 steps, deterministically, by
    ///     canceling from the datum on the first field read.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_CanceledDuringEvaluation_ThrowsOperationCanceled()
    {
        // Arrange: a program of about 1,200 instructions whose first instruction reads a field
        using var cancellation = new CancellationTokenSource();
        var datum = new CallbackDatum(cancellation.Cancel);
        var text = "datum.a" + string.Concat(Enumerable.Repeat("+1", 600));
        var compiled = Compile(text);
        var evaluator = new ExpressionEvaluator(null, cancellation.Token);

        // Act / Assert: the token is only observed at the next check point, so the evaluation stops after step 1,024
        Assert.Throws<OperationCanceledException>(() => evaluator.Evaluate(compiled, datum));
        Assert.Equal(1, datum.Reads);
        Assert.Equal(1_024, evaluator.StepsUsed);
    }

    /// <summary>
    ///     Validates that a short evaluation after a cancellation request that came too late for a check point still
    ///     completes, and that the next evaluation of the canceled call is stopped.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_CancellationBetweenCheckPoints_StopsNextEvaluation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var datum = new CallbackDatum(cancellation.Cancel);
        var evaluator = new ExpressionEvaluator(null, cancellation.Token);

        // Act: the first evaluation finishes in a few steps, before any check point
        var first = evaluator.Evaluate(Compile("datum.a + 1"), datum);

        // Assert
        Assert.Equal(2, first.Number);
        Assert.Throws<OperationCanceledException>(() => evaluator.Evaluate(Compile("1"), datum));
    }

    /// <summary>
    ///     Validates that results do not depend on the current culture: numbers, text conversion, case mapping and
    ///     comparison give identical results under several cultures, including Turkish and Arabic.
    /// </summary>
    /// <param name="culture">The culture name.</param>
    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    [InlineData("ja-JP")]
    [InlineData("sv-SE")]
    public void ExpressionEvaluator_Evaluate_UnderAnyCulture_IsInvariant(string culture)
    {
        // Arrange: expressions with decimal separators, exponents, case mapping and ordering
        string[] expressions =
        [
            "1.5 + ''",
            "1 / 3 + ''",
            "1e21 + ''",
            "toString(-1234567.891)",
            "toNumber('1.5') + toNumber(' 2e1 ')",
            "upper('abc i') + '|' + lower('XYZ I')",
            "'I' < 'i' ? 'lt' : 'ge'",
            "'\u00e4' < 'z' ? 'lt' : 'ge'",
            "indexof(upper('quit'), 'I')",
            "[1.5, 2.25] + ''",
            "pad(toString(7.5), 6, '0', 'left')",
            "test(/^[a-z]+$/i, 'TITLE')",
            "replace('I\u0130\u0131', /i/gi, '_')",
            "split('a,b;c', ',') + ''",
            "substring('\u00dc\u00ef\u00e4', 1, 3)",
        ];
        var baseline = expressions.Select(e => EvalText(e)).ToArray();

        // Act: evaluate every expression under the culture
        var actual = UnderCulture(culture, () => expressions.Select(e => EvalText(e)).ToArray());

        // Assert
        Assert.Equal(baseline, actual);
        Assert.Equal("1.5", actual[0]);
        Assert.Equal("0.3333333333333333", actual[1]);
        Assert.Equal("1e+21", actual[2]);
        Assert.Equal("-1234567.891", actual[3]);
        Assert.Equal("21.5", actual[4]);
        Assert.Equal("ABC I|xyz i", actual[5]);
        Assert.Equal("lt", actual[6]);
        Assert.Equal("ge", actual[7]);
        Assert.Equal("2", actual[8]);
        Assert.Equal("1.5,2.25", actual[9]);
        Assert.Equal("0007.5", actual[10]);
        Assert.Equal("true", actual[11]);
        Assert.Equal("\u00ef\u00e4", actual[14]);
    }

    /// <summary>
    ///     Validates that the culture helper applies the culture to a dedicated worker thread only: the cultures of the
    ///     calling thread are untouched and an exception of the callback is rethrown with its type.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_CultureHelper_IsolatesTheCultureOnAWorkerThread()
    {
        // Arrange
        var callerCulture = CultureInfo.CurrentCulture;
        var callerUiCulture = CultureInfo.CurrentUICulture;
        var callerThread = Environment.CurrentManagedThreadId;

        // Act
        var inside = UnderCulture("tr-TR", () => (Thread: Environment.CurrentManagedThreadId, Culture: CultureInfo.CurrentCulture, Ui: CultureInfo.CurrentUICulture));

        // Assert: the worker ran on another thread with the same culture for both properties (the invariant one without culture data)
        Assert.NotEqual(callerThread, inside.Thread);
        Assert.Equal(inside.Culture.Name, inside.Ui.Name);
        Assert.Same(callerCulture, CultureInfo.CurrentCulture);
        Assert.Same(callerUiCulture, CultureInfo.CurrentUICulture);
        Assert.Throws<InvalidOperationException>(() => UnderCulture<int>("tr-TR", () => throw new InvalidOperationException("boom")));
    }

    /// <summary>
    ///     Validates that evaluating the same compiled expression repeatedly gives identical results.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_Repeated_IsDeterministic()
    {
        // Arrange
        var compiled = Compile("datum.x * 2 + (datum.y > 1 ? lower('A') + upper('b') : 'none') + [1, 2].length");
        var datum = Obj(("x", DataValue.FromNumber(4.5)), ("y", DataValue.FromNumber(2)));
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act
        var results = Enumerable.Range(0, 200).Select(_ => evaluator.Evaluate(compiled, datum).ToText()).Distinct().ToArray();

        // Assert
        Assert.Equal(["9aB2"], results);
    }

    /// <summary>
    ///     Validates that one compiled expression can be evaluated concurrently by independent evaluators with
    ///     identical results.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_Concurrent_IsDeterministic()
    {
        // Arrange: an expression that uses a regular expression, string building and arithmetic
        var compiled = Compile("replace(datum.s, /o+/g, '0') + length(split(datum.s, '')) + (datum.n % 7)");
        var results = new ConcurrentBag<string>();

        // Act: 8 threads, each with its own evaluator and datum
        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, _ =>
        {
            var datum = Obj(("s", DataValue.FromString("foo boo")), ("n", DataValue.FromNumber(100)));
            var evaluator = new ExpressionEvaluator(null, CancellationToken.None);
            for (var i = 0; i < 100; i++)
            {
                results.Add(evaluator.Evaluate(compiled, datum).ToText());
            }
        });

        // Assert: 800 identical results
        Assert.Equal(800, results.Count);
        Assert.Equal(["f0 b072"], results.Distinct().ToArray());
    }

    /// <summary>
    ///     Validates that a chain of thousands of operators is evaluated iteratively and an array literal is built with
    ///     the stack of exactly the size the parser computed.
    /// </summary>
    [Fact]
    public void ExpressionEvaluator_Evaluate_LargeProgramsAndReusedStack_AreCorrect()
    {
        // Arrange: a program that needs a deep stack followed by one that needs a shallow stack on the same evaluator
        var deep = Compile("[" + string.Join(",", Enumerable.Range(0, 400)) + "]");
        var shallow = Compile("1");
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act
        var array = evaluator.Evaluate(deep);
        var one = evaluator.Evaluate(shallow);

        // Assert
        Assert.Equal(400, array.Elements.Length);
        Assert.Equal(399, array.Elements[399].Number);
        Assert.Equal(1, one.Number);
    }
}
