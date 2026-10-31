using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Expressions.ExpressionTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

/// <summary>
///     Tests of the expression parser: literals, precedence, <c>datum</c> access, locations of errors, the length and
///     depth limits and the collection of unsupported constructs, with expected values written out by hand from the
///     JavaScript grammar.
/// </summary>
public class ExpressionParserTests
{
    /// <summary>
    ///     Validates that number literals of every supported syntax have their JavaScript values.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected number.</param>
    [Theory]
    [InlineData("0", 0.0)]
    [InlineData("42", 42.0)]
    [InlineData("1.5", 1.5)]
    [InlineData(".5", 0.5)]
    [InlineData("5.", 5.0)]
    [InlineData("1e3", 1000.0)]
    [InlineData("1E3", 1000.0)]
    [InlineData("1.5e-2", 0.015)]
    [InlineData("2e+2", 200.0)]
    [InlineData("0x1F", 31.0)]
    [InlineData("0Xff", 255.0)]
    [InlineData("007", 7.0)]
    public void ExpressionParser_Parse_NumberLiterals_HaveJavaScriptValues(string text, double expected)
    {
        // Arrange / Act: compile and evaluate the literal
        var value = EvalNumber(text);

        // Assert: the value is exactly the parsed number
        Assert.Equal(expected, value);
    }

    /// <summary>
    ///     Validates that string literals decode the JavaScript escape sequences.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected string value.</param>
    [Theory]
    [InlineData("'abc'", "abc")]
    [InlineData("\"abc\"", "abc")]
    [InlineData("\"it's\"", "it's")]
    [InlineData("'it\\'s'", "it's")]
    [InlineData("'a\\nb'", "a\nb")]
    [InlineData("'a\\tb\\\\c'", "a\tb\\c")]
    [InlineData("'\\x41\\u0042\\u{43}'", "ABC")]
    [InlineData("'\\u{1F600}'", "\U0001F600")]
    [InlineData("'\\0'", "\0")]
    [InlineData("'\\q'", "q")]
    [InlineData("'a\\\nb'", "ab")]
    [InlineData("''", "")]
    public void ExpressionParser_Parse_StringLiterals_DecodeEscapes(string text, string expected)
    {
        // Arrange / Act
        var value = Eval(text);

        // Assert: the string kind with the decoded text
        Assert.True(value.IsString);
        Assert.Equal(expected, value.Text);
    }

    /// <summary>
    ///     Validates the keyword literals and the four constants.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_KeywordsAndConstants_HaveTheirValues()
    {
        // Arrange / Act / Assert: keywords
        Assert.True(Eval("null").IsNull);
        Assert.True(Eval("true").Boolean);
        Assert.True(Eval("false").IsBoolean);
        Assert.False(Eval("false").Boolean);

        // Assert: constants
        Assert.Equal(Math.PI, EvalNumber("PI"));
        Assert.Equal(Math.E, EvalNumber("E"));
        Assert.True(double.IsNaN(EvalNumber("NaN")));
        Assert.True(double.IsPositiveInfinity(EvalNumber("Infinity")));
        Assert.True(double.IsNegativeInfinity(EvalNumber("-Infinity")));
    }

    /// <summary>
    ///     Validates that array and object literals nest and keep their order.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_ArrayAndObjectLiterals_BuildCompositeValues()
    {
        // Arrange / Act
        var array = Eval("[1, 'a', [true], {k: 2}, ]");
        var obj = Eval("{a: 1, 'b c': [2], 3: null, \"d\": 'x',}");

        // Assert: the array has four elements, the last one an object
        Assert.True(array.IsArray);
        Assert.Equal(4, array.Elements.Length);
        Assert.Equal(1, array.Elements[0].Number);
        Assert.Equal("a", array.Elements[1].Text);
        Assert.True(array.Elements[2].IsArray);
        Assert.Equal(2, array.Elements[3].ObjectValue!.ValueAt(0).Number);

        // Assert: the object keeps insertion order and converts the number key to JavaScript text
        Assert.True(obj.IsObject);
        Assert.Equal(["a", "b c", "3", "d"], obj.ObjectValue!.Keys);
        Assert.Equal(1, EvalNumber("{a: 1}.a"));
        Assert.True(Eval("{a: 1}.b").IsNull);
    }

    /// <summary>
    ///     Validates operator precedence and associativity against hand-computed JavaScript results.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected value as JavaScript text.</param>
    [Theory]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("(1 + 2) * 3", "9")]
    [InlineData("2 - 3 - 4", "-5")]
    [InlineData("100 / 10 / 5", "2")]
    [InlineData("2 * 3 % 4", "2")]
    [InlineData("-2 * -3", "6")]
    [InlineData("- -3", "3")]
    [InlineData("1 + 2 < 4", "true")]
    [InlineData("1 < 2 === true", "true")]
    [InlineData("1 === 1 && 2 === 2", "true")]
    [InlineData("0 || 1 && 0", "0")]
    [InlineData("1 || 0 && 0", "1")]
    [InlineData("!1 + 1", "1")]
    [InlineData("!(1 + 1)", "false")]
    [InlineData("1 ? 2 : 3 ? 4 : 5", "2")]
    [InlineData("0 ? 2 : 0 ? 4 : 5", "5")]
    [InlineData("0 ? 2 : 1 ? 4 : 5", "4")]
    [InlineData("1 > 0 ? 'y' : 'n'", "y")]
    [InlineData("'a' + 'b' + 1 + 2", "ab12")]
    [InlineData("1 + 2 + 'a'", "3a")]
    [InlineData("8 / 2 / 2", "2")]
    [InlineData("4 / 2 / 1", "2")]
    public void ExpressionParser_Parse_PrecedenceAndAssociativity_MatchJavaScript(string text, string expected)
    {
        // Arrange / Act
        var value = EvalText(text);

        // Assert: the printed value equals the JavaScript result
        Assert.Equal(expected, value);
    }

    /// <summary>
    ///     Validates that <c>datum</c> access compiles to field reads and records that the expression reads the datum.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_DatumAccess_IsRecordedAndReadsFields()
    {
        // Arrange: a datum with a plain, a spaced and a nested member
        var datum = Obj(("x", DataValueOf(5)), ("a b", DataValueOf(6)), ("n", Obj(("m", DataValueOf(7)))));

        // Act
        var dotted = Compile("datum.x");
        var bracketed = Compile("datum['a b']");
        var nested = Compile("datum.n.m + datum['x']");
        var constant = Compile("1 + 2");

        // Assert: datum use is recorded
        Assert.True(dotted.ReferencesDatum);
        Assert.True(bracketed.ReferencesDatum);
        Assert.False(constant.ReferencesDatum);

        // Assert: a field read is one instruction without materializing the row
        Assert.Equal(1, dotted.InstructionCount);
        Assert.Equal(5, EvalWith("datum.x", datum).Number);
        Assert.Equal(6, EvalWith("datum['a b']", datum).Number);
        Assert.Equal(12, EvalWith(nested.Source, datum).Number);
        Assert.True(EvalWith("datum.missing", datum).IsNull);
        Assert.True(EvalWith("datum.n.missing.deeper", datum).IsNull);
    }

    /// <summary>
    ///     Validates that identifiers that are not constants, functions or view state are collected as parameter
    ///     references once per name, in order of first use.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_ParameterReferences_AreCollectedOncePerName()
    {
        // Arrange / Act
        var compiled = Compile("alpha + beta * alpha + abs(gamma) + datum.x");

        // Assert: three names in order of first use with their offsets
        Assert.Equal(
            [new ExpressionReference("alpha", 0), new ExpressionReference("beta", 8), new ExpressionReference("gamma", 27)],
            compiled.ParameterReferences);
        Assert.Empty(compiled.UnsupportedConstructs);
    }

    /// <summary>
    ///     Validates that unknown functions, view state and deferred operators parse and are collected with stable
    ///     identifiers, names and offsets.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_UnsupportedConstructs_AreCollectedInOrder()
    {
        // Arrange / Act
        const string Text = "foo(1) + signal + event.x + item() + (a & b) + (c << 1) + ('k' in d) + geoArea(2020, 1)";
        var compiled = Compile(Text);

        // Assert: kind, name and offset of every construct in order of appearance
        Assert.Equal(
            [
                new ExpressionConstruct(ExpressionConstructKind.Function, "foo", 0),
                new ExpressionConstruct(ExpressionConstructKind.ViewState, "signal", Text.IndexOf("signal", StringComparison.Ordinal)),
                new ExpressionConstruct(ExpressionConstructKind.ViewState, "event", Text.IndexOf("event", StringComparison.Ordinal)),
                new ExpressionConstruct(ExpressionConstructKind.ViewState, "item", Text.IndexOf("item", StringComparison.Ordinal)),
                new ExpressionConstruct(ExpressionConstructKind.Deferred, "bitwise-and", Text.IndexOf('&', StringComparison.Ordinal)),
                new ExpressionConstruct(ExpressionConstructKind.Deferred, "shift-left", Text.IndexOf("<<", StringComparison.Ordinal)),
                new ExpressionConstruct(ExpressionConstructKind.Deferred, "in", Text.IndexOf(" in ", StringComparison.Ordinal) + 1),
                new ExpressionConstruct(ExpressionConstructKind.Function, "geoArea", Text.IndexOf("geoArea", StringComparison.Ordinal)),
            ],
            compiled.UnsupportedConstructs);
        Assert.Equal(
            [
                "vl-expr-function-foo",
                "vl-expr-view-state",
                "vl-expr-view-state",
                "vl-expr-view-state",
                "vl-expr-construct-bitwise-and",
                "vl-expr-construct-shift-left",
                "vl-expr-construct-in",
                "vl-expr-function-geoarea",
            ],
            compiled.UnsupportedConstructs.Select(c => c.Identifier).ToArray());

        // Assert: the parameter names of the operands are still collected
        Assert.Equal(["a", "b", "c", "d"], compiled.ParameterReferences.Select(p => p.Name).ToArray());
    }

    /// <summary>
    ///     Validates that the identifier of an unsupported function is lower case while the name keeps its spelling.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_MixedCaseFunction_HasLowerCaseIdentifierAndOriginalName()
    {
        // Arrange / Act
        var compiled = Compile("geoArea(1, 'x') + GEOAREA(2, 'y')");

        // Assert
        Assert.Equal(["geoArea", "GEOAREA"], compiled.UnsupportedConstructs.Select(c => c.Name).ToArray());
        Assert.All(compiled.UnsupportedConstructs, c => Assert.Equal("vl-expr-function-geoarea", c.Identifier));
    }

    /// <summary>
    ///     Validates that a regular expression literal after the deferred <c>in</c> operator is a literal, not a division, so
    ///     the expression is collected as the deferred construct instead of failing to parse.
    /// </summary>
    /// <param name="text">The expression text.</param>
    [Theory]
    [InlineData("a in /x/")]
    [InlineData("'k' in /x/g")]
    [InlineData("a in /x/ && b")]
    public void ExpressionParser_Parse_RegExpAfterIn_IsALiteral(string text)
    {
        // Arrange / Act
        var compiled = Compile(text);

        // Assert
        Assert.Contains(compiled.UnsupportedConstructs, c => c.Identifier == "vl-expr-construct-in");
    }

    /// <summary>
    ///     Validates that the remaining deferred operators are recognized by name.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="name">The expected construct name.</param>
    [Theory]
    [InlineData("1 | 2", "bitwise-or")]
    [InlineData("1 ^ 2", "bitwise-xor")]
    [InlineData("8 >> 1", "shift-right")]
    [InlineData("8 >>> 1", "shift-right-unsigned")]
    public void ExpressionParser_Parse_DeferredOperators_AreNamed(string text, string name)
    {
        // Arrange / Act
        var compiled = Compile(text);

        // Assert: one deferred construct that evaluates to null
        var construct = Assert.Single(compiled.UnsupportedConstructs);
        Assert.Equal(ExpressionConstructKind.Deferred, construct.Kind);
        Assert.Equal(name, construct.Name);
        Assert.True(Eval(text).IsNull);
    }

    /// <summary>
    ///     Validates that the unary bitwise not is deferred like the binary bitwise operators: it is reported as a construct
    ///     and evaluates to null instead of being executed.
    /// </summary>
    /// <param name="text">The expression text.</param>
    [Theory]
    [InlineData("~5")]
    [InlineData("~~3.7")]
    [InlineData("~'7'")]
    [InlineData("~NaN")]
    public void ExpressionParser_Parse_UnaryBitwiseNot_IsDeferredAndEvaluatesToNull(string text)
    {
        // Arrange / Act
        var compiled = Compile(text);

        // Assert: reported with the deferred identifier, and null
        Assert.All(compiled.UnsupportedConstructs, c =>
        {
            Assert.Equal(ExpressionConstructKind.Deferred, c.Kind);
            Assert.Equal("vl-expr-construct-bitwise-not", c.Identifier);
        });
        Assert.NotEmpty(compiled.UnsupportedConstructs);
        Assert.Equal(text.Count(c => c == '~'), compiled.UnsupportedConstructs.Count);
        Assert.True(Eval(text).IsNull);
        Assert.Equal("true", EvalText("isValid(~5) === false"));
    }

    /// <summary>
    ///     Validates that regular expression literals are recognized after operators and that a slash after an operand
    ///     is a division.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_RegExpLiteralsAndDivision_AreDistinguished()
    {
        // Arrange / Act / Assert: after an operator or a parenthesis a slash starts a regular expression
        Assert.True(Eval("test(/^a[/]b$/, 'a/b')").Boolean);
        Assert.True(Eval("isRegExp(/x/gi)").Boolean);
        Assert.Equal("/x/gi", EvalText("/x/ig"));

        // Assert: after an operand it is a division
        Assert.Equal(1, EvalNumber("(4) / 2 / 2"));
        Assert.Equal(2, EvalNumber("[8][0] / 4"));
    }

    /// <summary>
    ///     Validates that an invalid regular expression literal is a located specification error.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_InvalidRegExpLiteral_IsLocatedSpecError()
    {
        // Arrange / Act: an unbalanced group and an unknown flag
        var group = Assert.Throws<VegaLiteSpecException>(() => Compile("1 + /(/"));
        var flag = Assert.Throws<VegaLiteSpecException>(() => Compile("/a/x"));

        // Assert: both carry the location and the offset of the literal
        Assert.Equal(Location, group.Location);
        Assert.EndsWith("at expression offset 4.", group.Message, StringComparison.Ordinal);
        Assert.Contains("is not valid", group.Message, StringComparison.Ordinal);
        Assert.EndsWith("at expression offset 0.", flag.Message, StringComparison.Ordinal);
        Assert.Contains("flags", flag.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the regular expression flags that the engine does not honor (u, v, y and d) are located
    ///     specification errors rather than being accepted and ignored, so no literal silently matches differently from
    ///     JavaScript.
    /// </summary>
    /// <param name="literal">The regular expression literal.</param>
    [Theory]
    [InlineData("/./u")]
    [InlineData("/./gu")]
    [InlineData("/./v")]
    [InlineData("/./y")]
    [InlineData("/./d")]
    [InlineData("replace('\\ud83d\\ude00', /./gu, 'x')")]
    public void ExpressionParser_Parse_UnhonoredRegExpFlag_IsLocatedSpecError(string literal)
    {
        // Arrange / Act
        var error = Assert.Throws<VegaLiteSpecException>(() => Compile(literal));

        // Assert: located, with the supported flags in the message
        Assert.Equal(Location, error.Location);
        Assert.Contains("the flags g, i, m and s are supported", error.Message, StringComparison.Ordinal);
        Assert.Contains("at expression offset", error.Message, StringComparison.Ordinal);
        Assert.Equal("/a/gis", EvalText("/a/sig"));
    }

    /// <summary>
    ///     Validates that a syntax error carries the owning pointer, the line and column of the string value, and the
    ///     expression offset, with a fixed message text.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_SyntaxError_CarriesLocationAndOffset()
    {
        // Arrange / Act
        var exception = Assert.Throws<VegaLiteSpecException>(() => Compile("1 +"));

        // Assert: pointer, line and column come from the caller and the offset is in the message
        Assert.Equal("/transform/0/calculate", exception.Location.Pointer);
        Assert.Equal(12, exception.Location.Line);
        Assert.Equal(17, exception.Location.Column);
        Assert.Equal("spec.vl.json", exception.Location.File);
        Assert.Equal(
            "spec.vl.json:12:17 /transform/0/calculate: Invalid expression: unexpected end of expression at expression offset 3.",
            exception.Message);
    }

    /// <summary>
    ///     Validates the first problem and its offset for a range of malformed and unsupported expressions.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="description">The expected description.</param>
    /// <param name="offset">The expected offset.</param>
    [Theory]
    [InlineData("", "unexpected end of expression", 0)]
    [InlineData("   ", "unexpected end of expression", 3)]
    [InlineData("1 + * 2", "unexpected '*'", 4)]
    [InlineData("1 2", "unexpected '2'", 2)]
    [InlineData("(1", "expected ')' but found end of expression", 2)]
    [InlineData("[1, 2", "expected ']' but found end of expression", 5)]
    [InlineData("[1,,2]", "unexpected ','", 3)]
    [InlineData("f(1,)", "unexpected ')'", 4)]
    [InlineData("{a 1}", "expected ':' but found '1'", 3)]
    [InlineData("{(a): 1}", "expected a property name but found '('", 1)]
    [InlineData("a = 1", "unexpected '='", 2)]
    [InlineData("a += 1", "unexpected '+='", 2)]
    [InlineData("a++", "unexpected '++'", 1)]
    [InlineData("2 ** 3", "unexpected '**'", 2)]
    [InlineData("a ?? b", "unexpected '??'", 2)]
    [InlineData("x => x", "unexpected '=>'", 2)]
    [InlineData("a, b", "unexpected ','", 1)]
    [InlineData("a; b", "unexpected ';'", 1)]
    [InlineData("1 ? 2", "expected ':' but found end of expression", 5)]
    [InlineData("new Foo()", "the word 'new' is not supported in expressions", 0)]
    [InlineData("function() {}", "the word 'function' is not supported in expressions", 0)]
    [InlineData("typeof 1", "the word 'typeof' is not supported in expressions", 0)]
    [InlineData("this", "the word 'this' is not supported in expressions", 0)]
    [InlineData("x.y()", "only a named function can be called; a method call such as a.b() is not supported", 3)]
    [InlineData("(1)()", "only a named function can be called; a method call such as a.b() is not supported", 3)]
    [InlineData("datum()", "only a named function can be called; a method call such as a.b() is not supported", 5)]
    [InlineData("a.", "expected a property name after '.' but found end of expression", 2)]
    [InlineData("a.1", "unexpected '.1'", 1)]
    [InlineData("'abc", "the string literal is not terminated", 0)]
    [InlineData("1 + 'ab\nc'", "the string literal is not terminated", 4)]
    [InlineData("'\\x4'", "the escape sequence needs hexadecimal digits", 1)]
    [InlineData("'\\u{110000}'", "the code point of the escape sequence is too large", 1)]
    [InlineData("'\\u{D800}'", "the escape sequence is not a valid code point", 1)]
    [InlineData("'\\1'", "octal escape sequences are not supported in string literals", 1)]
    [InlineData("1a", "a number literal must not be followed directly by a letter or digit", 0)]
    [InlineData("0x", "a hexadecimal literal needs at least one digit", 0)]
    [InlineData("1e", "the exponent of a number literal needs at least one digit", 0)]
    [InlineData("1e999", "the number literal is too large", 0)]
    [InlineData("a # b", "the character '#' is not valid in an expression", 2)]
    [InlineData("1 + @", "the character '@' is not valid in an expression", 4)]
    [InlineData("`x`", "the character '`' is not valid in an expression", 0)]
    [InlineData("//", "the regular expression literal is empty", 0)]
    [InlineData("/abc", "the regular expression literal is not terminated", 0)]
    public void ExpressionParser_Parse_MalformedExpression_ReportsFirstProblemAndOffset(string text, string description, int offset)
    {
        // Arrange / Act
        var exception = Assert.Throws<VegaLiteSpecException>(() => Compile(text));

        // Assert: deterministic message text and the location of the owning value
        Assert.Equal($"{Location}: Invalid expression: {description} at expression offset {offset}.", exception.Message);
        Assert.Equal(Location, exception.Location);
    }

    /// <summary>
    ///     Validates that a null expression text is an argument error rather than a specification error.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_NullText_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => ExpressionParser.Parse(null!, Location));

    /// <summary>
    ///     Validates the length limit at one below, at and one above the default of 4,096 characters.
    /// </summary>
    /// <param name="length">The length of the expression text.</param>
    /// <param name="accepted">Whether the text is accepted.</param>
    [Theory]
    [InlineData(4095, true)]
    [InlineData(4096, true)]
    [InlineData(4097, false)]
    public void ExpressionParser_Parse_LengthLimit_AcceptsUpToLimit(int length, bool accepted)
    {
        // Arrange: the number 1 padded with white space to the requested length
        var text = "1".PadRight(length);

        // Act / Assert
        if (accepted)
        {
            Assert.Equal(1, Compile(text).InstructionCount);
            return;
        }

        var exception = Assert.Throws<VegaLiteSpecException>(() => Compile(text));
        Assert.Equal(Location, exception.Location);
        Assert.EndsWith(
            "the expression has 4097 characters, more than the limit of 4096 (MaxExpressionLength) at expression offset 4096.",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the length is checked before the text is tokenized: an over-long text with an invalid character
    ///     is still reported as too long.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_OverlongTextWithInvalidCharacter_ReportsLengthFirst()
    {
        // Arrange: a custom limit and a text that is both too long and invalid
        var limits = new VegaLiteLimits { MaxExpressionLength = 5 };

        // Act
        var exception = Assert.Throws<VegaLiteSpecException>(() => Compile("1 + # + 2", limits));

        // Assert
        Assert.Contains("MaxExpressionLength", exception.Message, StringComparison.Ordinal);
        Assert.EndsWith("at expression offset 5.", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the nesting limit at 63, 64 and 65 levels of parentheses.
    /// </summary>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="accepted">Whether the text is accepted.</param>
    [Theory]
    [InlineData(63, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void ExpressionParser_Parse_DepthLimit_AcceptsUpToLimit(int depth, bool accepted)
    {
        // Arrange: depth nested parentheses around a literal
        var text = new string('(', depth) + "1" + new string(')', depth);

        // Act / Assert
        if (accepted)
        {
            var compiled = Compile(text);
            Assert.Equal(depth, compiled.Depth);
            Assert.Equal(1, Eval(text).Number);
            return;
        }

        var exception = Assert.Throws<VegaLiteSpecException>(() => Compile(text));
        Assert.Equal(Location, exception.Location);
        Assert.EndsWith(
            "the expression is nested more than 64 levels deep (MaxExpressionDepth) at expression offset 64.",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every nesting construct is counted: brackets, call arguments, array and object literals,
    ///     conditional branches and unary operands.
    /// </summary>
    /// <param name="open">The text repeated before the core.</param>
    /// <param name="core">The innermost text.</param>
    /// <param name="close">The text repeated after the core.</param>
    [Theory]
    [InlineData("[", "1", "]")]
    [InlineData("abs(", "1", ")")]
    [InlineData("{a:", "1", "}")]
    [InlineData("- ", "1", "")]
    [InlineData("! ", "1", "")]
    [InlineData("1 ? 1 : ", "1", "")]
    [InlineData("[1][", "0", "]")]
    public void ExpressionParser_Parse_EveryNestingConstruct_IsBoundedByDepthLimit(string open, string core, string close)
    {
        // Arrange: 65 levels exceed the limit of 64 and 5 levels are well within it
        var tooDeep = Repeat(open, 65) + core + Repeat(close, 65);
        var fine = Repeat(open, 5) + core + Repeat(close, 5);

        // Act / Assert
        Assert.Throws<VegaLiteSpecException>(() => Compile(tooDeep));
        Assert.True(Compile(fine).InstructionCount > 0);
    }

    /// <summary>
    ///     Validates that the maximum configurable depth is parsed without exhausting the stack and one more level is
    ///     rejected.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_MaximumConfiguredDepth_DoesNotOverflowTheStack()
    {
        // Arrange: the largest accepted depth limit and expressions that use up to four recursive routines per level
        var limits = new VegaLiteLimits { MaxExpressionDepth = 256, MaxExpressionLength = 100_000 };
        var text = Repeat("-(", 128) + "1" + Repeat(")", 128);

        // Act
        var compiled = Compile(text, limits);
        var tooDeep = Repeat("(", 257) + "1" + Repeat(")", 257);

        // Assert
        Assert.Equal(256, compiled.Depth);
        Assert.Throws<VegaLiteSpecException>(() => Compile(tooDeep, limits));
    }

    /// <summary>
    ///     Validates that a long chain of left-associative operators is parsed and evaluated without recursion
    ///     proportional to its length.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_LongOperatorChain_IsNotRecursive()
    {
        // Arrange: 2048 ones joined by 2047 plus signs, 4095 characters
        var text = "1" + Repeat("+1", 2047);

        // Act
        var compiled = Compile(text);

        // Assert: the nesting depth stays zero, one push per literal plus one add per operator
        Assert.Equal(0, compiled.Depth);
        Assert.Equal(2048 + 2047, compiled.InstructionCount);
        Assert.Equal(2048, Eval(text).Number);
    }

    /// <summary>
    ///     Validates that a long chain of short-circuit and conditional operators is parsed and evaluated.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_LongLogicalChain_EvaluatesWithoutRecursion()
    {
        // Arrange: 1000 alternatives in a chain of || and 500 && operands
        var or = "0" + Repeat("||0", 1000) + "||7";
        var and = "1" + Repeat("&&1", 500) + "&&9";

        // Act / Assert: the last operand is the result
        Assert.Equal(7, Eval(or).Number);
        Assert.Equal(9, Eval(and).Number);
    }

    /// <summary>
    ///     Validates that parsing is deterministic and shares no state between calls or threads.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_RepeatedAndConcurrent_GivesIdenticalResults()
    {
        // Arrange
        const string Text = "foo(a) + (datum.x > 1 ? lower(b) : 'k') + signal";
        var baseline = Compile(Text);
        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Act: parse from several threads many times
        Parallel.For(0, 64, _ =>
        {
            var compiled = Compile(Text);
            if (compiled.InstructionCount != baseline.InstructionCount
                || !compiled.UnsupportedConstructs.SequenceEqual(baseline.UnsupportedConstructs)
                || !compiled.ParameterReferences.SequenceEqual(baseline.ParameterReferences)
                || compiled.Depth != baseline.Depth)
            {
                errors.Add("different result");
            }
        });

        // Assert
        Assert.Empty(errors);
    }

    /// <summary>
    ///     Validates the metadata of a compiled expression: source, location, depth and instruction count.
    /// </summary>
    [Fact]
    public void ExpressionParser_Parse_CompiledExpression_ExposesSourceAndLocation()
    {
        // Arrange / Act
        var compiled = Compile("1 + 2");

        // Assert
        Assert.Equal("1 + 2", compiled.Source);
        Assert.Equal(Location, compiled.Location);
        Assert.Equal(3, compiled.InstructionCount);
        Assert.Equal(0, compiled.Depth);
    }

    /// <summary>
    ///     Repeats a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="count">The number of repetitions.</param>
    /// <returns>The repeated text.</returns>
    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    /// <summary>
    ///     Creates a number value.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The value.</returns>
    private static DataValue DataValueOf(double number) => DataValue.FromNumber(number);
}
