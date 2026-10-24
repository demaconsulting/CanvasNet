namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>
///     The expression grammar of <see cref="ScadParser"/>: precedence climbing for the binary operators, then
///     unary prefix, exponent, postfix and primary levels, plus the bracket forms (vectors, ranges and list
///     comprehensions).
/// </summary>
/// <remarks>
///     From loosest to tightest: conditional (right-associative); <c>||</c>; <c>&amp;&amp;</c>; equality;
///     relational; additive; multiplicative; unary prefix; <c>^</c> (right-associative, with an optionally
///     prefixed right operand so that <c>2^-1</c> works, and binding tighter than unary minus so that
///     <c>-2^2</c> is <c>-(2^2)</c>); postfix call, index and member access; primaries.
/// </remarks>
internal sealed partial class ScadParser
{
    /// <summary>The binary operators handled by precedence climbing, with their precedence (higher binds tighter).</summary>
    private static readonly Dictionary<ScadTokenKind, (int Precedence, ScadBinaryOperator Operator)> BinaryOperators = new()
    {
        [ScadTokenKind.OrOr] = (1, ScadBinaryOperator.Or),
        [ScadTokenKind.AndAnd] = (2, ScadBinaryOperator.And),
        [ScadTokenKind.EqualEqual] = (3, ScadBinaryOperator.Equal),
        [ScadTokenKind.NotEqual] = (3, ScadBinaryOperator.NotEqual),
        [ScadTokenKind.Less] = (4, ScadBinaryOperator.Less),
        [ScadTokenKind.LessEqual] = (4, ScadBinaryOperator.LessEqual),
        [ScadTokenKind.Greater] = (4, ScadBinaryOperator.Greater),
        [ScadTokenKind.GreaterEqual] = (4, ScadBinaryOperator.GreaterEqual),
        [ScadTokenKind.Plus] = (5, ScadBinaryOperator.Add),
        [ScadTokenKind.Minus] = (5, ScadBinaryOperator.Subtract),
        [ScadTokenKind.Star] = (6, ScadBinaryOperator.Multiply),
        [ScadTokenKind.Slash] = (6, ScadBinaryOperator.Divide),
        [ScadTokenKind.Percent] = (6, ScadBinaryOperator.Modulo),
    };

    /// <summary>
    ///     Parses a full expression (the conditional level), the entry point for every nested expression.
    /// </summary>
    /// <returns>The expression.</returns>
    private ScadExpression ParseExpression()
    {
        Enter();
        var expression = ParseConditional();
        Leave();
        return expression;
    }

    /// <summary>
    ///     Parses <c>condition ? whenTrue : whenFalse</c>, which is right-associative.
    /// </summary>
    /// <returns>The conditional or, when there is no <c>?</c>, the underlying expression.</returns>
    private ScadExpression ParseConditional()
    {
        var condition = ParseBinary(1);
        if (!At(ScadTokenKind.Question))
        {
            return condition;
        }

        var question = Advance();
        var whenTrue = ParseExpression();
        Expect(ScadTokenKind.Colon);
        var whenFalse = ParseExpression();
        return new ScadConditionalExpression(condition, whenTrue, whenFalse) { Location = question.Location };
    }

    /// <summary>
    ///     Parses left-associative binary operators whose precedence is at least <paramref name="minimum"/>.
    /// </summary>
    /// <param name="minimum">The lowest precedence this call may consume.</param>
    /// <returns>The expression. The recursion for right operands is bounded by the number of precedence levels.</returns>
    private ScadExpression ParseBinary(int minimum)
    {
        var left = ParseUnary();
        while (BinaryOperators.TryGetValue(Current.Kind, out var info) && info.Precedence >= minimum)
        {
            var operatorToken = Advance();
            var right = ParseBinary(info.Precedence + 1);
            left = new ScadBinaryExpression(info.Operator, left, right) { Location = operatorToken.Location };
        }

        return left;
    }

    /// <summary>
    ///     Parses a chain of prefix operators <c>+ - !</c> applied to an exponent-level operand.
    /// </summary>
    /// <returns>The expression.</returns>
    private ScadExpression ParseUnary()
    {
        var token = Current;
        if (token.Kind is not (ScadTokenKind.Plus or ScadTokenKind.Minus or ScadTokenKind.Bang))
        {
            return ParsePower();
        }

        Advance();
        Enter();
        var operand = ParseUnary();
        Leave();
        return new ScadUnaryExpression(ToUnaryOperator(token.Kind), operand) { Location = token.Location };
    }

    /// <summary>
    ///     Maps a prefix operator token to its operator.
    /// </summary>
    /// <param name="kind">The token kind.</param>
    /// <returns>The operator.</returns>
    private static ScadUnaryOperator ToUnaryOperator(ScadTokenKind kind) => kind switch
    {
        ScadTokenKind.Plus => ScadUnaryOperator.Plus,
        ScadTokenKind.Minus => ScadUnaryOperator.Negate,
        _ => ScadUnaryOperator.Not,
    };

    /// <summary>
    ///     Parses <c>base ^ exponent</c>, which is right-associative; the exponent may carry prefix operators.
    /// </summary>
    /// <returns>The expression.</returns>
    private ScadExpression ParsePower()
    {
        var left = ParsePostfix();
        if (!At(ScadTokenKind.Caret))
        {
            return left;
        }

        var caret = Advance();
        Enter();
        var right = ParseUnary();
        Leave();
        return new ScadBinaryExpression(ScadBinaryOperator.Power, left, right) { Location = caret.Location };
    }

    /// <summary>
    ///     Parses a primary followed by any number of calls, indexings and member accesses, applied left to right.
    /// </summary>
    /// <returns>The expression.</returns>
    private ScadExpression ParsePostfix()
    {
        var expression = ParsePrimary();
        while (true)
        {
            var token = Current;
            switch (token.Kind)
            {
                case ScadTokenKind.LeftParen:
                    Advance();
                    expression = new ScadCallExpression(expression, ParseArgumentsAfterParen()) { Location = token.Location };
                    break;
                case ScadTokenKind.LeftBracket:
                    Advance();
                    expression = new ScadIndexExpression(expression, ParseExpression()) { Location = token.Location };
                    Expect(ScadTokenKind.RightBracket);
                    break;
                case ScadTokenKind.Dot:
                    Advance();
                    var member = Expect(ScadTokenKind.Identifier);
                    expression = new ScadMemberExpression(expression, member.Text!) { Location = token.Location };
                    break;
                default:
                    return expression;
            }
        }
    }

    /// <summary>
    ///     Parses a primary expression: a literal, identifier, parenthesized expression, bracket form, or a
    ///     prefix form (<c>function</c>, <c>let</c>, <c>echo</c>, <c>assert</c>).
    /// </summary>
    /// <returns>The expression.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when the current token cannot start an expression.</exception>
    private ScadExpression ParsePrimary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case ScadTokenKind.Number:
                Advance();
                return new ScadNumberLiteral(token.Number) { Location = token.Location };
            case ScadTokenKind.String:
                Advance();
                return new ScadStringLiteral(token.Text!) { Location = token.Location };
            case ScadTokenKind.True or ScadTokenKind.False:
                Advance();
                return new ScadBooleanLiteral(token.Kind == ScadTokenKind.True) { Location = token.Location };
            case ScadTokenKind.Undef:
                Advance();
                return new ScadUndefLiteral { Location = token.Location };
            case ScadTokenKind.Identifier:
                Advance();
                return new ScadIdentifierExpression(token.Text!) { Location = token.Location };
            case ScadTokenKind.LeftParen:
                return ParseParenthesized();
            case ScadTokenKind.LeftBracket:
                return ParseBracketExpression();
            default:
                return ParsePrefixForm(token);
        }
    }

    /// <summary>
    ///     Parses <c>( expression )</c>; parentheses produce no node of their own.
    /// </summary>
    /// <returns>The inner expression.</returns>
    private ScadExpression ParseParenthesized()
    {
        Advance();
        var inner = ParseExpression();
        Expect(ScadTokenKind.RightParen);
        return inner;
    }

    /// <summary>
    ///     Parses the keyword-introduced expression forms, whose body extends as far right as possible.
    /// </summary>
    /// <param name="token">The current token.</param>
    /// <returns>The expression.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when the token cannot start an expression.</exception>
    private ScadExpression ParsePrefixForm(ScadToken token)
    {
        switch (token.Kind)
        {
            case ScadTokenKind.Function:
                Advance();
                var parameters = ParseParameters();
                return new ScadFunctionLiteral(parameters, ParseExpression()) { Location = token.Location };
            case ScadTokenKind.Let:
                Advance();
                Expect(ScadTokenKind.LeftParen);
                var assignments = ParseAssignments(requireOne: false);
                Expect(ScadTokenKind.RightParen);
                return new ScadLetExpression(assignments, ParseExpression()) { Location = token.Location };
            case ScadTokenKind.Echo:
                Advance();
                Expect(ScadTokenKind.LeftParen);
                var echoed = ParseArgumentsAfterParen();
                return new ScadEchoExpression(echoed, ParseExpression()) { Location = token.Location };
            case ScadTokenKind.Assert:
                Advance();
                Expect(ScadTokenKind.LeftParen);
                var asserted = ParseArgumentsAfterParen();
                return new ScadAssertExpression(asserted, ParseExpression()) { Location = token.Location };
            default:
                throw ErrorExpected("an expression");
        }
    }

    /// <summary>
    ///     Parses a bracketed form: an empty or populated vector, or a range.
    /// </summary>
    /// <returns>The vector or range expression.</returns>
    private ScadExpression ParseBracketExpression()
    {
        var open = Advance();
        var elements = new List<ScadExpression>();
        if (!At(ScadTokenKind.RightBracket))
        {
            var first = ParseListElement();

            // A colon after the first expression turns the form into a range
            if (At(ScadTokenKind.Colon))
            {
                return ParseRangeRest(open, first);
            }

            elements.Add(first);
            while (Accept(ScadTokenKind.Comma) && !At(ScadTokenKind.RightBracket))
            {
                elements.Add(ParseListElement());
            }
        }

        Expect(ScadTokenKind.RightBracket);
        return new ScadVectorExpression(Freeze(elements)) { Location = open.Location };
    }

    /// <summary>
    ///     Parses the remainder of a range after its first expression: <c>: end ]</c> or <c>: step : end ]</c>.
    /// </summary>
    /// <param name="open">The opening bracket token.</param>
    /// <param name="start">The first expression.</param>
    /// <returns>The range.</returns>
    /// <exception cref="ScadSyntaxException">
    ///     Thrown when the first part is a list comprehension, or the range has commas or more than three parts.
    /// </exception>
    private ScadRangeExpression ParseRangeRest(ScadToken open, ScadExpression start)
    {
        if (IsComprehension(start))
        {
            throw ErrorExpected("',' or ']'");
        }

        Advance();
        var second = ParseExpression();
        var third = Accept(ScadTokenKind.Colon) ? ParseExpression() : null;
        Expect(ScadTokenKind.RightBracket);

        // Written order is start:end or start:step:end
        return third is null
            ? new ScadRangeExpression(start, null, second) { Location = open.Location }
            : new ScadRangeExpression(start, second, third) { Location = open.Location };
    }

    /// <summary>
    ///     Determines whether an expression is a list comprehension element.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns><see langword="true"/> for the <c>for</c>, C-style <c>for</c>, <c>if</c>, <c>let</c> and <c>each</c> forms.</returns>
    private static bool IsComprehension(ScadExpression expression) =>
        expression is ScadListForExpression or ScadListCForExpression or ScadListIfExpression
            or ScadListLetExpression or ScadListEachExpression;

    /// <summary>
    ///     Parses one element of a vector: a list comprehension element (optionally parenthesized) or an
    ///     ordinary expression.
    /// </summary>
    /// <returns>The element.</returns>
    private ScadExpression ParseListElement()
    {
        Enter();
        var element = Current.Kind switch
        {
            ScadTokenKind.For => ParseListFor(),
            ScadTokenKind.If => ParseListIf(),
            ScadTokenKind.Let => ParseListLet(),
            ScadTokenKind.Each => ParseListEach(),
            ScadTokenKind.LeftParen when IsParenthesizedElementStart() => ParseParenthesizedListElement(),
            _ => ParseExpression(),
        };
        Leave();
        return element;
    }

    /// <summary>
    ///     Determines whether the current <c>(</c> opens a parenthesized comprehension element, which is the case
    ///     when the next token is one of the comprehension keywords <c>for</c>, <c>if</c>, <c>let</c> or <c>each</c>.
    /// </summary>
    /// <returns><see langword="true"/> when a comprehension keyword follows the parenthesis.</returns>
    private bool IsParenthesizedElementStart()
    {
        // Parentheses nest, so look through any run of them for the keyword
        var offset = 1;
        while (PeekAt(offset).Kind == ScadTokenKind.LeftParen)
        {
            offset++;
        }

        return PeekAt(offset).Kind is ScadTokenKind.For or ScadTokenKind.If or ScadTokenKind.Let or ScadTokenKind.Each;
    }

    /// <summary>
    ///     Parses <c>( element )</c> where the element is a comprehension form; parentheses produce no node of
    ///     their own, so the result is the same as the unparenthesized element.
    /// </summary>
    /// <returns>The element.</returns>
    private ScadExpression ParseParenthesizedListElement()
    {
        var start = _index;
        Advance();
        var element = ParseListElement();
        Expect(ScadTokenKind.RightParen);

        // A parenthesized let is also an ordinary expression, so it may be the operand of a postfix or binary
        // operator, as in (let(a=1) [a])[0]; when something other than an element terminator follows, it is
        // parsed again as an expression
        if (element is ScadListLetExpression && !IsElementTerminator(Current.Kind))
        {
            _index = start;
            return ParseExpression();
        }

        return element;
    }

    /// <summary>
    ///     Determines whether a token can directly follow a complete list element.
    /// </summary>
    /// <param name="kind">The token kind.</param>
    /// <returns><see langword="true"/> for <c>,</c>, <c>]</c>, <c>)</c> and <c>else</c>.</returns>
    private static bool IsElementTerminator(ScadTokenKind kind) =>
        kind is ScadTokenKind.Comma or ScadTokenKind.RightBracket or ScadTokenKind.RightParen or ScadTokenKind.Else;

    /// <summary>
    ///     Parses a comprehension <c>for</c> element, either iterator-style or C-style.
    /// </summary>
    /// <returns>The element.</returns>
    private ScadExpression ParseListFor()
    {
        var keyword = Advance();
        Expect(ScadTokenKind.LeftParen);

        // A header that starts with ';' can only be the C-style form with an empty initializer
        var initializers = ParseAssignments(requireOne: !At(ScadTokenKind.Semicolon));
        if (At(ScadTokenKind.Semicolon))
        {
            return ParseListCForRest(keyword, initializers);
        }

        Expect(ScadTokenKind.RightParen);
        return new ScadListForExpression(initializers, ParseListElement()) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses the remainder of a C-style comprehension header: <c>; condition ; increments ) body</c>.
    /// </summary>
    /// <param name="keyword">The <c>for</c> token.</param>
    /// <param name="initializers">The initializers already parsed.</param>
    /// <returns>The element.</returns>
    private ScadListCForExpression ParseListCForRest(ScadToken keyword, IReadOnlyList<ScadAssignment> initializers)
    {
        Advance();
        var condition = ParseExpression();
        Expect(ScadTokenKind.Semicolon);
        var increments = ParseAssignments(requireOne: false);
        Expect(ScadTokenKind.RightParen);
        return new ScadListCForExpression(initializers, condition, increments, ParseListElement()) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses a comprehension <c>if</c> element with an optional <c>else</c>.
    /// </summary>
    /// <returns>The element.</returns>
    private ScadListIfExpression ParseListIf()
    {
        var keyword = Advance();
        Expect(ScadTokenKind.LeftParen);
        var condition = ParseExpression();
        Expect(ScadTokenKind.RightParen);
        var whenTrue = ParseListElement();

        // An else binds to the nearest if, because a nested element has already consumed its own
        var whenFalse = Accept(ScadTokenKind.Else) ? ParseListElement() : null;
        return new ScadListIfExpression(condition, whenTrue, whenFalse) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses a comprehension <c>let</c> element.
    /// </summary>
    /// <returns>The element.</returns>
    private ScadListLetExpression ParseListLet()
    {
        var keyword = Advance();
        Expect(ScadTokenKind.LeftParen);
        var assignments = ParseAssignments(requireOne: false);
        Expect(ScadTokenKind.RightParen);
        return new ScadListLetExpression(assignments, ParseListElement()) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses a comprehension <c>each</c> element.
    /// </summary>
    /// <returns>The element.</returns>
    private ScadListEachExpression ParseListEach()
    {
        var keyword = Advance();
        return new ScadListEachExpression(ParseExpression()) { Location = keyword.Location };
    }
}
