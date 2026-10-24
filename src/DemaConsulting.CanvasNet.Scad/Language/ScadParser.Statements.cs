namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>
///     The statement grammar of <see cref="ScadParser"/>.
/// </summary>
internal sealed partial class ScadParser
{
    /// <summary>
    ///     Parses a statement at file scope, where <c>include</c> and <c>use</c> directives are also legal.
    /// </summary>
    /// <returns>The statement, or null for an empty statement (<c>;</c>).</returns>
    private ScadStatement? ParseTopLevelStatement()
    {
        var token = Current;
        if (token.Kind is not (ScadTokenKind.Include or ScadTokenKind.Use))
        {
            return ParseStatement();
        }

        Advance();
        var kind = token.Kind == ScadTokenKind.Include ? ScadDirectiveKind.Include : ScadDirectiveKind.Use;
        return new ScadDirectiveStatement(kind, token.Text!) { Location = token.Location };
    }

    /// <summary>
    ///     Parses one statement where declarations are allowed (file scope, inside braces).
    /// </summary>
    /// <returns>The statement, or null for an empty statement (<c>;</c>).</returns>
    private ScadStatement? ParseStatement()
    {
        Enter();
        var statement = ParseStatementCore();
        Leave();
        return statement;
    }

    /// <summary>
    ///     Dispatches on the first token of a statement.
    /// </summary>
    /// <returns>The statement, or null for an empty statement.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when the token cannot start a statement.</exception>
    private ScadStatement? ParseStatementCore()
    {
        var token = Current;
        switch (token.Kind)
        {
            case ScadTokenKind.Semicolon:
                Advance();
                return null;
            case ScadTokenKind.LeftBrace:
                return ParseBlock();
            case ScadTokenKind.Module:
                return ParseModuleDefinition();
            case ScadTokenKind.Function:
                return ParseFunctionDefinition();
            case ScadTokenKind.Identifier:
                return ParseIdentifierStatement();
            case ScadTokenKind.Include or ScadTokenKind.Use:
                throw Error(token, "include/use is only allowed at file scope");
            default:
                return IsModifier(token.Kind) || IsInstantiationKeyword(token.Kind)
                    ? ParseModifiedOrInstantiationLike()
                    : throw ErrorUnexpected(token);
        }
    }

    /// <summary>
    ///     Parses a statement that begins with an identifier: an assignment or a module instantiation.
    /// </summary>
    /// <returns>The statement.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when the identifier is followed by neither <c>=</c> nor <c>(</c>.</exception>
    private ScadStatement ParseIdentifierStatement()
    {
        var next = PeekAt(1);
        if (next.Kind == ScadTokenKind.Assign)
        {
            return ParseAssignmentStatement();
        }

        if (next.Kind == ScadTokenKind.LeftParen)
        {
            return ParseModuleInstantiation();
        }

        // Point at the token after the name, which is where '=' or '(' was expected
        _index++;
        throw ErrorExpected($"'=' or '(' after '{PeekAt(-1).Text}'");
    }

    /// <summary>
    ///     Parses <c>name = expression ;</c>.
    /// </summary>
    /// <returns>The assignment statement.</returns>
    private ScadAssignmentStatement ParseAssignmentStatement()
    {
        var name = Advance();
        Advance();
        var value = ParseExpression();
        Expect(ScadTokenKind.Semicolon);
        return new ScadAssignmentStatement(name.Text!, value) { Location = name.Location };
    }

    /// <summary>
    ///     Determines whether a token kind is one of the four modifier characters.
    /// </summary>
    /// <param name="kind">The token kind.</param>
    /// <returns><see langword="true"/> for <c>*</c>, <c>!</c>, <c>#</c> and <c>%</c>.</returns>
    private static bool IsModifier(ScadTokenKind kind) =>
        kind is ScadTokenKind.Star or ScadTokenKind.Bang or ScadTokenKind.Hash or ScadTokenKind.Percent;

    /// <summary>
    ///     Determines whether a token kind starts an instantiation-like statement other than a plain module call.
    /// </summary>
    /// <param name="kind">The token kind.</param>
    /// <returns><see langword="true"/> for <c>for</c>, <c>intersection_for</c>, <c>if</c>, <c>let</c>, <c>echo</c> and <c>assert</c>.</returns>
    private static bool IsInstantiationKeyword(ScadTokenKind kind) =>
        kind is ScadTokenKind.For or ScadTokenKind.IntersectionFor or ScadTokenKind.If or ScadTokenKind.Let
            or ScadTokenKind.Echo or ScadTokenKind.Assert;

    /// <summary>
    ///     Parses optional modifiers followed by an instantiation-like statement or a block.
    /// </summary>
    /// <returns>The statement, wrapped in a modified statement when modifiers are present.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when modifiers are followed by anything else.</exception>
    private ScadStatement ParseModifiedOrInstantiationLike()
    {
        var first = Current;
        var modifiers = ScadModifiers.None;
        while (IsModifier(Current.Kind))
        {
            modifiers |= ToModifier(Advance().Kind);
        }

        var target = ParseModifierTarget();
        return modifiers == ScadModifiers.None
            ? target
            : new ScadModifiedStatement(modifiers, target) { Location = first.Location };
    }

    /// <summary>
    ///     Maps a modifier token kind to its flag.
    /// </summary>
    /// <param name="kind">The modifier token kind.</param>
    /// <returns>The flag.</returns>
    private static ScadModifiers ToModifier(ScadTokenKind kind) => kind switch
    {
        ScadTokenKind.Star => ScadModifiers.Disable,
        ScadTokenKind.Bang => ScadModifiers.ShowOnly,
        ScadTokenKind.Hash => ScadModifiers.Highlight,
        _ => ScadModifiers.Background,
    };

    /// <summary>
    ///     Parses the statement that follows the modifiers (or the statement itself when there are none).
    /// </summary>
    /// <returns>An instantiation-like statement or a block.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when modifiers precede anything that is not an instantiation or block.</exception>
    private ScadStatement ParseModifierTarget()
    {
        var token = Current;
        if (token.Kind == ScadTokenKind.LeftBrace)
        {
            return ParseBlock();
        }

        if (token.Kind == ScadTokenKind.Identifier && PeekAt(1).Kind == ScadTokenKind.LeftParen)
        {
            return ParseModuleInstantiation();
        }

        if (IsInstantiationKeyword(token.Kind))
        {
            return ParseKeywordStatement();
        }

        // This is only reached after at least one modifier, because plain keywords returned above
        throw Error(token, "A modifier must be followed by a module instantiation or block");
    }

    /// <summary>
    ///     Parses an instantiation-like statement that starts with a keyword.
    /// </summary>
    /// <returns>The statement.</returns>
    private ScadStatement ParseKeywordStatement() => Current.Kind switch
    {
        ScadTokenKind.For => ParseForStatement(),
        ScadTokenKind.IntersectionFor => ParseIntersectionForStatement(),
        ScadTokenKind.If => ParseIfStatement(),
        ScadTokenKind.Let => ParseLetStatement(),
        ScadTokenKind.Echo => ParseEchoStatement(),
        _ => ParseAssertStatement(),
    };

    /// <summary>
    ///     Parses a braced block <c>{ statement* }</c> as a standalone statement.
    /// </summary>
    /// <returns>The block statement.</returns>
    private ScadBlockStatement ParseBlock()
    {
        var open = Current;
        return new ScadBlockStatement(ParseBlockStatements()) { Location = open.Location };
    }

    /// <summary>
    ///     Parses <c>{ statement* }</c> and returns the statements without any wrapper node.
    /// </summary>
    /// <returns>The statements inside the braces.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when the closing brace is missing.</exception>
    private IReadOnlyList<ScadStatement> ParseBlockStatements()
    {
        Expect(ScadTokenKind.LeftBrace);
        var statements = new List<ScadStatement>();
        while (!At(ScadTokenKind.RightBrace))
        {
            // Running out of input inside a block reports the missing brace
            if (At(ScadTokenKind.EndOfFile))
            {
                throw ErrorExpected(ExpectedNames[ScadTokenKind.RightBrace]);
            }

            var statement = ParseStatement();
            if (statement is not null)
            {
                statements.Add(statement);
            }
        }

        Advance();
        return Freeze(statements);
    }

    /// <summary>
    ///     Parses the body of an instantiation-like statement: <c>;</c> (no children), a block (children are its
    ///     contents), or a single modified or plain instantiation-like statement.
    /// </summary>
    /// <returns>The child statements.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when a declaration or an unrelated token appears.</exception>
    private IReadOnlyList<ScadStatement> ParseChildren()
    {
        if (Accept(ScadTokenKind.Semicolon))
        {
            return Freeze(new List<ScadStatement>());
        }

        // A braced body needs no extra depth: every statement inside it is parsed through ParseStatement
        if (At(ScadTokenKind.LeftBrace))
        {
            return ParseBlockStatements();
        }

        Enter();
        var child = ParseSingleChild();
        Leave();
        return Freeze(new List<ScadStatement> { child });
    }

    /// <summary>
    ///     Parses an unbraced single child, which must be an instantiation-like statement (optionally modified).
    /// </summary>
    /// <returns>The child statement.</returns>
    /// <exception cref="ScadSyntaxException">Thrown for declarations and for tokens that cannot start a child.</exception>
    private ScadStatement ParseSingleChild()
    {
        var token = Current;
        if (token.Kind is ScadTokenKind.Module or ScadTokenKind.Function)
        {
            throw Error(token, "Use braces around a declaration");
        }

        if (token.Kind == ScadTokenKind.Identifier)
        {
            return ParseIdentifierChild();
        }

        if (IsModifier(token.Kind) || IsInstantiationKeyword(token.Kind))
        {
            return ParseModifiedOrInstantiationLike();
        }

        throw token.Kind == ScadTokenKind.EndOfFile
            ? ErrorExpected("';'")
            : Error(token, $"Expected ';', '{{' or a module instantiation but found {Describe(token)}");
    }

    /// <summary>
    ///     Parses a child that starts with an identifier, which must be a module instantiation.
    /// </summary>
    /// <returns>The instantiation.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when the identifier starts an assignment or is not followed by <c>(</c>.</exception>
    private ScadStatement ParseIdentifierChild()
    {
        var next = PeekAt(1);
        if (next.Kind == ScadTokenKind.LeftParen)
        {
            return ParseModuleInstantiation();
        }

        throw next.Kind == ScadTokenKind.Assign
            ? Error(Current, "Use braces around a declaration")
            : Error(Current, $"Expected '(' after '{Current.Text}'");
    }

    /// <summary>
    ///     Parses <c>name ( arguments ) children</c>.
    /// </summary>
    /// <returns>The instantiation.</returns>
    private ScadModuleInstantiation ParseModuleInstantiation()
    {
        var name = Advance();
        Expect(ScadTokenKind.LeftParen);
        var arguments = ParseArgumentsAfterParen();
        var children = ParseChildren();
        return new ScadModuleInstantiation(name.Text!, arguments, children) { Location = name.Location };
    }

    /// <summary>
    ///     Parses <c>module name ( parameters ) body</c>.
    /// </summary>
    /// <returns>The module definition.</returns>
    private ScadModuleDefinition ParseModuleDefinition()
    {
        var keyword = Advance();
        var name = Expect(ScadTokenKind.Identifier);
        var parameters = ParseParameters();
        var body = ParseChildren();
        return new ScadModuleDefinition(name.Text!, parameters, body) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses <c>function name ( parameters ) = expression ;</c>.
    /// </summary>
    /// <returns>The function definition.</returns>
    /// <exception cref="ScadSyntaxException">Thrown when the name is missing (for example <c>function (x) …</c> at statement start).</exception>
    private ScadFunctionDefinition ParseFunctionDefinition()
    {
        var keyword = Advance();
        if (!At(ScadTokenKind.Identifier))
        {
            throw ErrorExpected("a function name after 'function'");
        }

        var name = Advance();
        var parameters = ParseParameters();
        Expect(ScadTokenKind.Assign);
        var body = ParseExpression();
        Expect(ScadTokenKind.Semicolon);
        return new ScadFunctionDefinition(name.Text!, parameters, body) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses <c>if ( condition ) children [else children]</c>.
    /// </summary>
    /// <returns>The if statement.</returns>
    private ScadIfStatement ParseIfStatement()
    {
        var keyword = Advance();
        Expect(ScadTokenKind.LeftParen);
        var condition = ParseExpression();
        Expect(ScadTokenKind.RightParen);
        var thenPart = ParseChildren();

        // An else binds to the nearest if because the nested statement has already consumed its own
        var elsePart = Accept(ScadTokenKind.Else) ? ParseChildren() : null;
        return new ScadIfStatement(condition, thenPart, elsePart) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses <c>for ( iterators ) children</c>.
    /// </summary>
    /// <returns>The for statement.</returns>
    /// <exception cref="ScadSyntaxException">Thrown for a C-style loop, which is only legal inside a list comprehension.</exception>
    private ScadForStatement ParseForStatement()
    {
        var keyword = Advance();
        var iterators = ParseLoopHeader();
        return new ScadForStatement(iterators, ParseChildren()) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses <c>intersection_for ( iterators ) children</c>.
    /// </summary>
    /// <returns>The intersection_for statement.</returns>
    private ScadIntersectionForStatement ParseIntersectionForStatement()
    {
        var keyword = Advance();
        var iterators = ParseLoopHeader();
        return new ScadIntersectionForStatement(iterators, ParseChildren()) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses <c>( iterator {, iterator} [,] )</c> of a statement-level loop.
    /// </summary>
    /// <returns>The iterators.</returns>
    /// <exception cref="ScadSyntaxException">Thrown for an empty list, a C-style header, or a missing parenthesis.</exception>
    private IReadOnlyList<ScadAssignment> ParseLoopHeader()
    {
        Expect(ScadTokenKind.LeftParen);
        var iterators = ParseAssignments(requireOne: true);
        if (At(ScadTokenKind.Semicolon))
        {
            throw Error(Current, "A C-style 'for' loop is only allowed inside a list comprehension");
        }

        Expect(ScadTokenKind.RightParen);
        return iterators;
    }

    /// <summary>
    ///     Parses <c>let ( assignments ) children</c>.
    /// </summary>
    /// <returns>The let statement.</returns>
    private ScadLetStatement ParseLetStatement()
    {
        var keyword = Advance();
        Expect(ScadTokenKind.LeftParen);
        var assignments = ParseAssignments(requireOne: false);
        Expect(ScadTokenKind.RightParen);
        return new ScadLetStatement(assignments, ParseChildren()) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses <c>echo ( arguments ) children</c>.
    /// </summary>
    /// <returns>The echo statement.</returns>
    private ScadEchoStatement ParseEchoStatement()
    {
        var keyword = Advance();
        Expect(ScadTokenKind.LeftParen);
        var arguments = ParseArgumentsAfterParen();
        return new ScadEchoStatement(arguments, ParseChildren()) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses <c>assert ( arguments ) children</c>.
    /// </summary>
    /// <returns>The assert statement.</returns>
    private ScadAssertStatement ParseAssertStatement()
    {
        var keyword = Advance();
        Expect(ScadTokenKind.LeftParen);
        var arguments = ParseArgumentsAfterParen();
        return new ScadAssertStatement(arguments, ParseChildren()) { Location = keyword.Location };
    }

    /// <summary>
    ///     Parses a comma-separated list of <c>name = expression</c> bindings, allowing one trailing comma.
    ///     The list ends before a closing parenthesis or a semicolon, which the caller consumes.
    /// </summary>
    /// <param name="requireOne">Whether at least one binding is required.</param>
    /// <returns>The bindings.</returns>
    private IReadOnlyList<ScadAssignment> ParseAssignments(bool requireOne)
    {
        var list = new List<ScadAssignment>();
        if (!requireOne && (At(ScadTokenKind.RightParen) || At(ScadTokenKind.Semicolon)))
        {
            return Freeze(list);
        }

        do
        {
            list.Add(ParseAssignment());
        }
        while (Accept(ScadTokenKind.Comma) && !At(ScadTokenKind.RightParen) && !At(ScadTokenKind.Semicolon));

        return Freeze(list);
    }

    /// <summary>
    ///     Parses one <c>name = expression</c> binding.
    /// </summary>
    /// <returns>The binding.</returns>
    private ScadAssignment ParseAssignment()
    {
        var name = Expect(ScadTokenKind.Identifier);
        Expect(ScadTokenKind.Assign);
        var value = ParseExpression();
        return new ScadAssignment(name.Text!, value) { Location = name.Location };
    }

    /// <summary>
    ///     Parses <c>( parameter {, parameter} [,] )</c> of a module, function or function literal.
    /// </summary>
    /// <returns>The parameters.</returns>
    private IReadOnlyList<ScadParameter> ParseParameters()
    {
        Expect(ScadTokenKind.LeftParen);
        var list = new List<ScadParameter>();
        while (!At(ScadTokenKind.RightParen))
        {
            list.Add(ParseParameter());
            if (!Accept(ScadTokenKind.Comma))
            {
                break;
            }
        }

        Expect(ScadTokenKind.RightParen);
        return Freeze(list);
    }

    /// <summary>
    ///     Parses <c>name [= default]</c>.
    /// </summary>
    /// <returns>The parameter.</returns>
    private ScadParameter ParseParameter()
    {
        var name = Expect(ScadTokenKind.Identifier);
        var defaultValue = Accept(ScadTokenKind.Assign) ? ParseExpression() : null;
        return new ScadParameter(name.Text!, defaultValue) { Location = name.Location };
    }

    /// <summary>
    ///     Parses call arguments after the opening parenthesis has been consumed, through the closing one,
    ///     allowing one trailing comma.
    /// </summary>
    /// <returns>The arguments.</returns>
    private IReadOnlyList<ScadArgument> ParseArgumentsAfterParen()
    {
        var list = new List<ScadArgument>();
        while (!At(ScadTokenKind.RightParen))
        {
            list.Add(ParseArgument());
            if (!Accept(ScadTokenKind.Comma))
            {
                break;
            }
        }

        Expect(ScadTokenKind.RightParen);
        return Freeze(list);
    }

    /// <summary>
    ///     Parses <c>[name =] expression</c>.
    /// </summary>
    /// <returns>The argument.</returns>
    private ScadArgument ParseArgument()
    {
        var first = Current;

        // Only an identifier directly followed by '=' makes the argument named ('==' is a different token)
        if (first.Kind == ScadTokenKind.Identifier && PeekAt(1).Kind == ScadTokenKind.Assign)
        {
            _index += 2;
            return new ScadArgument(first.Text, ParseExpression()) { Location = first.Location };
        }

        return new ScadArgument(null, ParseExpression()) { Location = first.Location };
    }
}
