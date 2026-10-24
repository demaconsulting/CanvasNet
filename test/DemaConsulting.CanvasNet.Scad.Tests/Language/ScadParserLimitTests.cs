using System.Globalization;
using System.Runtime.ExceptionServices;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for the hostile-input limits of <see cref="ScadParser"/>: nesting depth, tree height and source size.
/// </summary>
/// <remarks>
///     Depth accounting: a statement and each expression entry cost one level, so a file-scope assignment
///     <c>x = ...;</c> already uses two levels before any nesting; every nested parenthesis, prefix operator,
///     <c>^</c>, ternary branch, child or block adds one more. The height of the finished tree is also limited
///     to the same value, which is what bounds left-associative operator and postfix chains.
/// </remarks>
public class ScadParserLimitTests
{
    /// <summary>The nesting limit under test.</summary>
    private const int Limit = ScadParser.MaxNestingDepth;

    /// <summary>
    ///     Parses an assignment of the given expression and returns the result for assertions.
    /// </summary>
    /// <param name="expression">The expression source.</param>
    /// <returns>The parsed program.</returns>
    private static ScadProgram ParseValue(string expression) => ScadParserTestSupport.Parse($"x = {expression};");

    /// <summary>
    ///     Asserts that parsing fails with the nesting error.
    /// </summary>
    /// <param name="source">The source.</param>
    private static void AssertTooDeep(string source)
    {
        var ex = Assert.Throws<ScadSyntaxException>(() => ScadParserTestSupport.Parse(source));
        Assert.Contains($"Nesting too deep (limit {Limit})", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, ex.Location.Line);
        Assert.True(ex.Location.Column > 0);
    }

    /// <summary>
    ///     Validates that parenthesis nesting exactly at the limit is accepted.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_Parentheses_AtLimit_Succeeds()
    {
        // Arrange: the assignment and the outer expression use two levels
        var n = Limit - 2;

        // Act
        var program = ParseValue(ScadParserTestSupport.Repeat("(", n) + "1" + ScadParserTestSupport.Repeat(")", n));

        // Assert: the parentheses leave no nodes, so the value is the bare literal
        Assert.IsType<ScadNumberLiteral>(Assert.IsType<ScadAssignmentStatement>(program.Statements[0]).Value);
    }

    /// <summary>
    ///     Validates that one more parenthesis level is rejected.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_Parentheses_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        var n = Limit - 1;
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("(", n)}1{ScadParserTestSupport.Repeat(")", n)};");
    }

    /// <summary>
    ///     Validates that deeply nested vectors are rejected and moderately nested ones accepted.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_Vectors_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        var ok = ScadParserTestSupport.Repeat("[", 60) + "1" + ScadParserTestSupport.Repeat("]", 60);
        Assert.NotNull(ParseValue(ok));
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("[", 300)}1{ScadParserTestSupport.Repeat("]", 300)};");
    }

    /// <summary>
    ///     Validates a prefix operator chain at and over the limit.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_UnaryChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert: 254 prefix operators plus two levels is exactly the limit
        Assert.NotNull(ParseValue(ScadParserTestSupport.Repeat("-", Limit - 2) + "1"));
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("-", Limit - 1)}1;");
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("!", 100000)}1;");
    }

    /// <summary>
    ///     Validates a right-nested conditional chain.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_TernaryChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ParseValue(ScadParserTestSupport.Repeat("a?b:", 50) + "c"));
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("a?b:", 300)}c;");
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("a?", 300)}b{ScadParserTestSupport.Repeat(":c", 300)};");
    }

    /// <summary>
    ///     Validates a right-associative exponent chain.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_PowerChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ParseValue(ScadParserTestSupport.Repeat("2^", 100) + "2"));
        AssertTooDeep($"x = {ScadParserTestSupport.Repeat("2^", 300)}2;");
    }

    /// <summary>
    ///     Validates nested block statements.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_BlockStatements_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ScadParserTestSupport.Parse(ScadParserTestSupport.Repeat("{", 100) + ScadParserTestSupport.Repeat("}", 100)));
        AssertTooDeep(ScadParserTestSupport.Repeat("{", 300) + ScadParserTestSupport.Repeat("}", 300));
    }

    /// <summary>
    ///     Validates a chain of unbraced single children.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_SingleChildChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ScadParserTestSupport.Parse(ScadParserTestSupport.Repeat("a() ", 100) + ";"));
        AssertTooDeep(ScadParserTestSupport.Repeat("a() ", 300) + ";");
    }

    /// <summary>
    ///     Validates a long else-if chain.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_ElseIfChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ScadParserTestSupport.Parse(ScadParserTestSupport.Repeat("if (a) b(); else ", 50) + "c();"));
        AssertTooDeep(ScadParserTestSupport.Repeat("if (a) b(); else ", 300) + "c();");
    }

    /// <summary>
    ///     Validates nested comprehension elements.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_ComprehensionChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ParseValue("[" + ScadParserTestSupport.Repeat("for (i=a) ", 50) + "1]"));
        AssertTooDeep($"x = [{ScadParserTestSupport.Repeat("for (i=a) ", 300)}1];");
    }

    /// <summary>
    ///     Validates that a left-associative operator chain is limited by the height of the finished tree.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_LeftAssociativeChain_OverLimit_Throws()
    {
        // Arrange: a chain of n operands has tree depth n + 1 below the program (assignment + operators)
        static string Chain(int operands) => "1" + ScadParserTestSupport.Repeat("+1", operands - 1);

        // Act / Assert: 255 operands reach exactly the limit; 256 and a hostile length are rejected
        Assert.NotNull(ParseValue(Chain(Limit - 1)));
        AssertTooDeep($"x = {Chain(Limit)};");
        AssertTooDeep($"x = {Chain(100000)};");
    }

    /// <summary>
    ///     Validates that a long left-associative chain well under the limit is accepted.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_LeftAssociativeChain_UnderLimit_Succeeds()
    {
        // Arrange / Act
        var program = ParseValue("1" + ScadParserTestSupport.Repeat("*2", 199));

        // Assert
        Assert.Single(program.Statements);
    }

    /// <summary>
    ///     Validates that long postfix chains are limited by the height of the finished tree.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_PostfixChain_OverLimit_Throws()
    {
        // Arrange / Act / Assert
        Assert.NotNull(ParseValue("a" + ScadParserTestSupport.Repeat("[0]", 100)));
        AssertTooDeep($"x = a{ScadParserTestSupport.Repeat("[0]", 300)};");
        AssertTooDeep($"x = a{ScadParserTestSupport.Repeat(".x", 300)};");
        AssertTooDeep($"x = a{ScadParserTestSupport.Repeat("(1)", 300)};");
    }

    /// <summary>
    ///     Validates that left-leaning chains stacked through parentheses are limited by tree height even
    ///     though the parser itself recurses only a few levels per chain.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_InteriorLeftChainsAcrossLevels_OverLimit_Throws()
    {
        // Arrange: each level wraps the previous expression as the first operand of a 10-operator chain
        static string Build(int levels)
        {
            var expression = "1";
            for (var i = 0; i < levels; i++)
            {
                expression = "(" + expression + ")" + ScadParserTestSupport.Repeat("+1", 10);
            }

            return expression;
        }

        // Act / Assert: the parser recursion depth is only the number of levels, but the tree height is ~11 per level
        Assert.NotNull(ParseValue(Build(10)));
        AssertTooDeep($"x = {Build(40)};");
    }

    /// <summary>
    ///     Validates the exact failure location: the deepest offending node or token is on the first line.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_MultiLineInput_ReportsLineOfOffendingConstruct()
    {
        // Arrange: the deep chain starts on line 3
        var source = "a = 1;\nb = 2;\nx = " + ScadParserTestSupport.Repeat("-", 400) + "1;";

        // Act
        var ex = Assert.Throws<ScadSyntaxException>(() => ScadParserTestSupport.Parse(source));

        // Assert
        Assert.Equal(3, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that hostile input on a small stack fails closed instead of overflowing the stack.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_OnSmallStackThread_DoesNotOverflow()
    {
        // Arrange: several hostile shapes, each far beyond the limit
        var inputs = new[]
        {
            $"x = {ScadParserTestSupport.Repeat("(", 100000)}1;",
            $"x = {ScadParserTestSupport.Repeat("[", 100000)}1;",
            $"x = {ScadParserTestSupport.Repeat("-", 100000)}1;",
            $"x = {ScadParserTestSupport.Repeat("a?", 100000)}b;",
            ScadParserTestSupport.Repeat("{", 100000),
            ScadParserTestSupport.Repeat("a() ", 100000),
            $"x = {ScadParserTestSupport.Repeat("1+", 100000)}1;",
        };
        var errors = new Exception?[inputs.Length];

        // Act: run each parse on a thread with a 1 MiB stack
        var thread = new Thread(
            () =>
            {
                for (var i = 0; i < inputs.Length; i++)
                {
                    errors[i] = Record.Exception(() => ScadParserTestSupport.Parse(inputs[i]));
                }
            },
            1024 * 1024);
        thread.Start();
        thread.Join();

        // Assert: every input produced the typed syntax exception (reaching here means no stack overflow)
        Assert.All(errors, e => Assert.IsType<ScadSyntaxException>(e));
    }

    /// <summary>
    ///     The pathological shapes used by the caller-stack determinism tests: each maps a repeat count to a source,
    ///     and <c>Max</c> is the largest count accepted at the nominal nesting limit (a count of <c>Max + 1</c> is
    ///     rejected). The counts follow from the depth accounting in the class remarks.
    /// </summary>
    private static readonly Dictionary<string, (Func<int, string> Make, int Max)> Shapes = new()
    {
        ["parentheses"] = (n => $"x = {ScadParserTestSupport.Repeat("(", n)}1{ScadParserTestSupport.Repeat(")", n)};", Limit - 2),
        ["vectors"] = (n => $"x = {ScadParserTestSupport.Repeat("[", n)}1{ScadParserTestSupport.Repeat("]", n)};", (Limit / 2) - 1),
        ["unary"] = (n => $"x = {ScadParserTestSupport.Repeat("-", n)}1;", Limit - 2),
        ["ternary"] = (n => $"x = {ScadParserTestSupport.Repeat("a?b:", n)}c;", Limit - 2),
        ["power"] = (n => $"x = {ScadParserTestSupport.Repeat("2^", n)}2;", Limit - 2),
        ["comprehension"] = (n => $"x = [{ScadParserTestSupport.Repeat("for (i=a) ", n)}1];", Limit - 4),
        ["function-literals"] = (n => $"x = {ScadParserTestSupport.Repeat("function(a) ", n)}1;", Limit - 2),
        ["call-arguments"] = (n => $"x = {ScadParserTestSupport.Repeat("f(", n)}1{ScadParserTestSupport.Repeat(")", n)};", (Limit / 2) - 1),
        ["blocks"] = (n => ScadParserTestSupport.Repeat("{", n) + ScadParserTestSupport.Repeat("}", n), Limit),
        ["children"] = (n => ScadParserTestSupport.Repeat("a() ", n) + ";", Limit),
        ["else-if"] = (n => ScadParserTestSupport.Repeat("if (a) b(); else ", n) + "c();", Limit - 1),
    };

    /// <summary>The names of <see cref="Shapes"/>, as theory data.</summary>
    public static TheoryData<string> ShapeNames => [.. Shapes.Keys];

    /// <summary>The stack size, in bytes, of the deliberately small caller threads.</summary>
    private const int SmallCallerStackSize = 256 * 1024;

    /// <summary>
    ///     Runs a function on a new thread with a small stack and returns its result, propagating any exception.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="function">The function to run.</param>
    /// <param name="stackSize">The stack size of the thread in bytes.</param>
    /// <returns>The result.</returns>
    private static T OnSmallStack<T>(Func<T> function, int stackSize = SmallCallerStackSize)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    result = function();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            },
            stackSize);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result!;
    }

    /// <summary>
    ///     Validates that every shape nested exactly at the limit parses when the caller has only a small stack,
    ///     because the parser restarts on its own sized thread when the caller's stack guard trips.
    /// </summary>
    /// <param name="shape">The name of the shape.</param>
    [Theory]
    [MemberData(nameof(ShapeNames))]
    public void ScadParser_Parse_Nesting_AtLimit_OnSmallCallerStack_Succeeds(string shape)
    {
        // Arrange
        var (make, max) = Shapes[shape];
        var source = make(max);

        // Act
        var program = OnSmallStack(() => ScadParserTestSupport.Parse(source));

        // Assert
        Assert.Single(program.Statements);
    }

    /// <summary>
    ///     Validates that every shape one level over the limit still fails with the nesting error, not a stack
    ///     overflow, when the caller has only a small stack.
    /// </summary>
    /// <param name="shape">The name of the shape.</param>
    [Theory]
    [MemberData(nameof(ShapeNames))]
    public void ScadParser_Parse_Nesting_OverLimit_OnSmallCallerStack_ThrowsNestingError(string shape)
    {
        // Arrange
        var (make, max) = Shapes[shape];
        var source = make(max + 1);

        // Act
        var ex = Assert.Throws<ScadSyntaxException>(() => OnSmallStack(() => ScadParserTestSupport.Parse(source)));

        // Assert
        Assert.Contains($"Nesting too deep (limit {Limit})", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the dedicated parser thread has a stack at least as large as the measured worst case
    ///     needs by parsing a mixed worst-case input at the limit on a caller whose stack is tiny.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Nesting_AtLimit_OnTinyCallerStack_Succeeds()
    {
        // Arrange: parentheses at the limit, parsed by a caller thread that could never hold them itself
        var source = Shapes["parentheses"].Make(Limit - 2);
        ScadProgram? program = null;
        Exception? error = null;

        // Act
        var thread = new Thread(() => error = Record.Exception(() => program = ScadParserTestSupport.Parse(source)), 64 * 1024);
        thread.Start();
        thread.Join();

        // Assert
        Assert.Null(error);
        Assert.NotNull(program);
    }

    /// <summary>
    ///     Validates that a normal parse runs on the calling thread and does not spawn a fallback thread.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_NormalInput_RunsOnCallerThread()
    {
        // Arrange: moderately nested input that fits any reasonable stack
        var source = $"x = {ScadParserTestSupport.Repeat("(", 20)}1{ScadParserTestSupport.Repeat(")", 20)}; module m() {{ cube(1); }}";
        var tokens = ScadLexer.Tokenize(source, null);

        // Act
        var fromSource = ScadParserTestSupport.Parse(source);
        var sourceThread = ScadParser.LastParseThreadId;
        var fromTokens = ScadParser.Parse(tokens);
        var tokenThread = ScadParser.LastParseThreadId;

        // Assert: both entry points completed on this thread
        Assert.Equal(2, fromSource.Statements.Count);
        Assert.Equal(2, fromTokens.Statements.Count);
        Assert.Equal(Environment.CurrentManagedThreadId, sourceThread);
        Assert.Equal(Environment.CurrentManagedThreadId, tokenThread);
    }

    /// <summary>
    ///     Validates that a parse needing more stack than the caller has falls back to a different thread and
    ///     still produces the correct tree.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_AtLimit_OnTinyCallerStack_FallsBackToDedicatedThread()
    {
        // Arrange: a 64 KiB caller is below the runtime guard's reserve, so the inline attempt must give up
        var source = Shapes["parentheses"].Make(Limit - 2);

        // Act
        var (program, callerThread, parseThread) = OnSmallStack(
            () =>
            {
                var parsed = ScadParserTestSupport.Parse(source);
                return (parsed, Environment.CurrentManagedThreadId, ScadParser.LastParseThreadId);
            },
            64 * 1024);

        // Assert
        Assert.Single(program.Statements);
        Assert.NotEqual(callerThread, parseThread);
    }

    /// <summary>
    ///     Validates that the fallback path keeps exception type, location and stack trace, the caller's culture,
    ///     and the nesting error for input over the limit.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_OnTinyCallerStack_PreservesExceptionsAndCulture()
    {
        // Arrange
        var overLimit = Shapes["parentheses"].Make(Limit - 1);
        var decimalSource = $"x = {ScadParserTestSupport.Repeat("(", 10)}1.5{ScadParserTestSupport.Repeat(")", 10)};";

        // Act
        var (error, errorOnCaller, number, numberOnCaller) = OnSmallStack(
            () =>
            {
                var caught = Record.Exception(() => ScadParserTestSupport.Parse(overLimit));
                var failedOnCaller = ScadParser.LastParseThreadId == Environment.CurrentManagedThreadId;
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var program = ScadParserTestSupport.Parse(decimalSource);
                var value = Assert.IsType<ScadNumberLiteral>(Assert.IsType<ScadAssignmentStatement>(program.Statements[0]).Value).Value;
                return (caught, failedOnCaller, value, ScadParser.LastParseThreadId == Environment.CurrentManagedThreadId);
            },
            64 * 1024);

        // Assert
        var syntax = Assert.IsType<ScadSyntaxException>(error);
        Assert.Contains($"Nesting too deep (limit {Limit})", syntax.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ScadParser), syntax.StackTrace, StringComparison.Ordinal);
        Assert.False(errorOnCaller);
        Assert.Equal(1.5, number);
        Assert.False(numberOnCaller);
    }

    /// <summary>
    ///     Validates that many concurrent small-stack callers all succeed while the number of simultaneously
    ///     running fallback threads never exceeds the bound.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_ConcurrentTinyCallerStacks_BoundFallbackThreads()
    {
        // Arrange: several times more callers than the bound allows at once
        var source = Shapes["parentheses"].Make(Limit - 2);
        var callers = new Thread[ScadParser.MaxConcurrentFallbackThreads * 4];
        var failures = new Exception?[callers.Length];
        for (var i = 0; i < callers.Length; i++)
        {
            var index = i;
            callers[i] = new Thread(() => failures[index] = Record.Exception(() => ScadParserTestSupport.Parse(source)), 64 * 1024);
        }

        // Act
        foreach (var caller in callers)
        {
            caller.Start();
        }

        foreach (var caller in callers)
        {
            caller.Join();
        }

        // Assert
        Assert.All(failures, Assert.Null);
        Assert.InRange(ScadParser.PeakConcurrentFallbackThreads, 1, ScadParser.MaxConcurrentFallbackThreads);
        Assert.True(ScadParser.FallbackCount >= callers.Length);
    }

    /// <summary>
    ///     Validates that an exception raised on the parser thread reaches the caller with its type, location and
    ///     original stack trace intact.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Exception_RethrownFromParserThread_PreservesTypeAndStackTrace()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadSyntaxException>(() => ScadParserTestSupport.Parse("x = ;"));

        // Assert: the original frames inside the parser are still present
        Assert.Equal(1, ex.Location.Line);
        Assert.Contains(nameof(ScadParser), ex.StackTrace, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the parser thread uses the calling thread's culture.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_NumberLiteral_UnderGermanCulture_ParsesDotDecimal()
    {
        // Arrange: a culture whose decimal separator is a comma
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // Act
            var program = ScadParserTestSupport.Parse("x = 1.5;");

            // Assert
            var literal = Assert.IsType<ScadNumberLiteral>(Assert.IsType<ScadAssignmentStatement>(program.Statements[0]).Value);
            Assert.Equal(1.5, literal.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    ///     Validates that concurrent parses on the shared static entry point do not interfere.
    /// </summary>
    [Fact]
    public async Task ScadParser_Parse_Concurrent_ProducesIndependentResults()
    {
        // Arrange / Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(i => Task.Run(() => ScadParserTestSupport.Parse($"x = {i};"), TestContext.Current.CancellationToken)));

        // Assert
        for (var i = 0; i < results.Length; i++)
        {
            var value = Assert.IsType<ScadNumberLiteral>(Assert.IsType<ScadAssignmentStatement>(results[i].Statements[0]).Value);
            Assert.Equal((double)i, value.Value);
        }
    }
    /// <summary>
    ///     Validates that a source of exactly the maximum length is accepted.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_SourceLength_AtLimit_Parses()
    {
        // Arrange: a valid statement padded with blanks to the limit
        var source = "x=1;" + new string(' ', ScadParser.MaxSourceLength - 4);

        // Act
        var program = ScadParserTestSupport.Parse(source);

        // Assert
        Assert.Equal(ScadParser.MaxSourceLength, source.Length);
        Assert.Single(program.Statements);
    }

    /// <summary>
    ///     Validates that a longer source is rejected with a plain <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_SourceLength_OverLimit_ThrowsInvalidDataExceptionNotSyntaxException()
    {
        // Arrange
        var source = "x=1;" + new string(' ', ScadParser.MaxSourceLength - 3);

        // Act
        var ex = Assert.ThrowsAny<Exception>(() => ScadParserTestSupport.Parse(source));

        // Assert: the exact type, no location involved
        Assert.IsType<InvalidDataException>(ex);
        Assert.Contains("4194304", ex.Message, StringComparison.Ordinal);
    }
}
