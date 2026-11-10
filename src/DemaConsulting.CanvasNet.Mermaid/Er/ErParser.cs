// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;
using static DemaConsulting.CanvasNet.Mermaid.Er.ErTokens;

namespace DemaConsulting.CanvasNet.Mermaid.Er;

/// <summary>
///     The ER parser: a hand-written port of Mermaid's <c>erDiagram.jison</c> (its lexer and its grammar) that fills an
///     <see cref="ErModel"/> through the calls the grammar's actions make on Mermaid's ER database.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lexer.</b> The 83 lexer rules and the start conditions of the generated jison lexer are ported one to one
///         (first matching rule wins, case-insensitive), with hand-written matchers in place of the regular expressions, so
///         names, cardinalities, attribute words, keys, comments, styles and keywords split exactly as in Mermaid, quirks
///         included: the cardinality words are tried in Mermaid's order before names (<c>one</c> and <c>only one</c> are
///         <c>ONLY_ONE</c>, <c>many</c> is <c>ZERO_OR_MORE</c>, a <c>1</c> before a name, a number or a relationship line
///         is <c>ONLY_ONE</c>), <c>to</c> and <c>optionally to</c> are relationship types, a line that contains
///         <c>direction LR</c> anywhere is a direction statement, and a <c>~</c> in an attribute block takes everything up
///         to the last <c>~</c> of the line as one attribute word.
///     </para>
///     <para>
///         <b>Grammar.</b> A recursive-descent parser with one token of lookahead accepts the language of the LALR(1)
///         grammar: entities with an alias (<c>name[alias]</c>), <c>:::</c> classes and attribute blocks (type with an
///         optional <c>?</c>, name, keys and a comment), relationships (<c>A cardinality type cardinality B : role</c>,
///         with <c>:::</c> classes on either side), <c>classDef</c>, <c>class</c> and <c>style</c> statements, subgraphs
///         (nested, with a title in brackets and their own direction), the directions and the accessibility texts. The
///         relationship's <c>cardA</c> is its right cardinality and <c>cardB</c> its left one, as Mermaid's grammar swaps
///         them. Statements are read in loops; only subgraphs recurse. After each token the lookahead is checked against
///         the actions of the LALR state jison is in, so grammar errors come where Mermaid reports them, with its
///         expected tokens.
///     </para>
///     <para>
///         <b>Values.</b> As in Mermaid, each statement gives a value that a subgraph lists as its nodes: the entity names of
///         an entity or relationship statement, the id of a nested subgraph, the trimmed text of an accessibility statement
///         and the keyword text of a <c>classDef</c>, <c>class</c> or <c>style</c> statement; a direction statement inside
///         a subgraph sets the subgraph's direction instead.
///     </para>
///     <para>
///         <b>Errors.</b> A token the grammar does not accept is a located <see cref="MermaidSyntaxException"/> with code
///         <see cref="MermaidDiagnosticCodes.SyntaxError"/>, a hint, and as <see cref="MermaidSyntaxException.Expected"/>
///         the jison names of the tokens of the LALR state where jison reports the error, in jison's order. Text no rule
///         matches is a located lexical error. Subgraphs nested deeper than <see cref="MermaidLimits.MaxNestingDepth"/> and
///         token texts longer than <see cref="MermaidLimits.MaxLabelLength"/> (and the styles and subgraph titles the
///         grammar joins from them) are refused with <see cref="MermaidDiagnosticCodes.LimitExceeded"/>.
///     </para>
///     <para>
///         Parsing is linear in the text. An instance parses one text on one thread.
///     </para>
/// </remarks>
internal sealed partial class ErParser
{
    /// <summary>The tokens that start an entity name.</summary>
    private static readonly string[] Names = [UnicodeText, Num, EntityName, DecimalNum, EntityOne];

    /// <summary>The cardinality tokens.</summary>
    private static readonly string[] Cardinalities = [ZeroOrOne, ZeroOrMore, OneOrMore, OnlyOne, MdParent];

    /// <summary>The relationship types.</summary>
    private static readonly string[] RelationshipTypes = [NonIdentifying, Identifying];

    /// <summary>The tokens of a role.</summary>
    private static readonly string[] Roles = [UnicodeText, EntityName, Word];

    /// <summary>The tokens of an id list.</summary>
    private static readonly string[] Ids = [UnicodeText, StyleText];

    /// <summary>The tokens that start a statement.</summary>
    private static readonly string[] StatementStarts =
    [
        Title, AccTitle, AccDescr, AccDescrMultilineValue, Subgraph, DirectionTb, DirectionBt, DirectionRl, DirectionLr,
        ClassDef, ClassKeyword, StyleKeyword, .. Names,
    ];

    /// <summary>The tokens that start a line of the top-level document (jison's states after <c>ER_DIAGRAM document</c>).</summary>
    private static readonly string[] TopLineStarts = [Eof, Newline, .. StatementStarts];

    /// <summary>The tokens that may follow a line (the merged lookahead set of every reduction to a line).</summary>
    private static readonly string[] LineFollowers = [.. TopLineStarts, End];

    /// <summary>The tokens that may follow an entity name (its merged lookahead set).</summary>
    private static readonly string[] NameFollowers = [.. LineFollowers, Colon, StyleSeparator, BlockStart, Sqs, Sqe, Semi, .. Cardinalities];

    /// <summary>The tokens that may follow an entity name that starts a statement.</summary>
    private static readonly string[] EntityFollowers = [.. LineFollowers, StyleSeparator, BlockStart, Sqs, .. Cardinalities];

    /// <summary>The tokens that may follow an id of an id list (its merged lookahead set).</summary>
    private static readonly string[] IdFollowers = [.. LineFollowers, Colon, BlockStart, StyleText, Comma, Brkt, .. Cardinalities];

    /// <summary>The tokens that may follow a cardinality (its merged lookahead set).</summary>
    private static readonly string[] CardinalityFollowers = [.. Names, .. RelationshipTypes];

    /// <summary>The tokens of a style component.</summary>
    private static readonly string[] StyleComponents = [Colon, StyleText, Num, Brkt];

    /// <summary>The tokens that may follow the id list of a <c>classDef</c> or <c>style</c> statement.</summary>
    private static readonly string[] StyleIdFollowers = [.. StyleComponents, Comma];

    /// <summary>The tokens that may follow a style component.</summary>
    private static readonly string[] ComponentFollowers = [.. StyleComponents, Eof, Newline, Comma, Semi];

    /// <summary>The tokens that may follow the styles of a statement.</summary>
    private static readonly string[] StylesFollowers = [Eof, Newline, Comma, Semi];

    /// <summary>The tokens that end a <c>classDef</c>, <c>style</c> or <c>subgraph</c> header.</summary>
    private static readonly string[] Separators = [Eof, Newline, Semi];

    /// <summary>The tokens that may follow the id of a subgraph.</summary>
    private static readonly string[] SubgraphIdFollowers = [.. Separators, Sqs];

    /// <summary>The tokens that may follow a word of a subgraph title.</summary>
    private static readonly string[] TitleFollowers = [.. Names, Sqe];

    /// <summary>The tokens that may follow the alias of an entity.</summary>
    private static readonly string[] AliasFollowers = [.. LineFollowers, StyleSeparator, BlockStart];

    /// <summary>The tokens that may follow the classes of an entity statement with an alias.</summary>
    private static readonly string[] AliasClassFollowers = [.. LineFollowers, BlockStart, Comma];

    /// <summary>The tokens that may follow the classes of an entity statement.</summary>
    private static readonly string[] EntityClassFollowers = [.. AliasClassFollowers, .. Cardinalities];

    /// <summary>The tokens that may follow the classes of the second entity of a relationship.</summary>
    private static readonly string[] RoleClassFollowers = [Colon, Comma];

    /// <summary>The tokens that may follow the second entity of a relationship.</summary>
    private static readonly string[] SecondEntityFollowers = [Colon, StyleSeparator];

    /// <summary>The tokens that may start or end the attributes of a block.</summary>
    private static readonly string[] AttributeStarts = [BlockStop, AttributeWord];

    /// <summary>The tokens that may follow an attribute type.</summary>
    private static readonly string[] TypeFollowers = [AttributeWord, Question];

    /// <summary>The tokens that may follow an attribute name.</summary>
    private static readonly string[] AttributeNameFollowers = [.. AttributeStarts, AttributeKey, Comment];

    /// <summary>The tokens that may follow an attribute key.</summary>
    private static readonly string[] KeyFollowers = [.. AttributeStarts, KeyComma, Comment];

    /// <summary>The model the statements fill.</summary>
    private readonly ErModel _model;

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The lexer.</summary>
    private SourceScanner _scanner = null!;

    /// <summary>The token cursor.</summary>
    private TokenCursor _cursor = null!;

    /// <summary>The current nesting depth of subgraphs (Mermaid's <c>subgraphDepth</c>).</summary>
    private int _depth;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="model">The model to fill.</param>
    /// <param name="limits">The limits of the open call.</param>
    private ErParser(ErModel model, MermaidLimits limits)
    {
        _model = model;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the lookahead token.
    /// </summary>
    private ScanToken Current => _cursor.Current;

    /// <summary>
    ///     Parses an ER diagram.
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="detected">The detected diagram (its keyword location).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The model.</returns>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the text does not follow the grammar or when a limit is exceeded.
    /// </exception>
    public static ErModel Parse(PreprocessedSource source, DetectedDiagram detected, MermaidLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var model = new ErModel(limits, detected.Location);
        new ErParser(model, limits).Run(source.ParserText);
        model.Complete();
        return model;
    }

    /// <summary>
    ///     Parses the whole text (<c>start</c>): the keyword, then the lines up to the grammar's <c>EOF</c> and the end of
    ///     the tokens.
    /// </summary>
    /// <param name="text">The text.</param>
    private void Run(MappedText text)
    {
        _scanner = new SourceScanner(text, CreateRules(), Conditions);
        _cursor = Lex(() => new TokenCursor(_scanner));
        if (!Is(ErDiagram))
        {
            throw Fail([ErDiagram], "start the diagram with the erDiagram keyword");
        }

        Advance();
        ParseDocument(inSubgraph: false);
    }

    /// <summary>
    ///     Parses the lines of a document (<c>document</c>): at the top up to the grammar's <c>EOF</c> followed by the end
    ///     of the tokens, inside a subgraph up to its <c>end</c>.
    /// </summary>
    /// <param name="inSubgraph">Whether the document is the body of a subgraph.</param>
    /// <returns>The values of the statements and the last direction of the document.</returns>
    private ErDocument ParseDocument(bool inSubgraph)
    {
        var document = new ErDocument();
        while (true)
        {
            if (Is(Newline))
            {
                Advance();
                CheckLookahead(LineFollowers);
            }
            else if (Is(Eof))
            {
                // At the top, the grammar's EOF ends the diagram; anywhere else it is an empty line
                Advance();
                if (!inSubgraph && IsEnd())
                {
                    return document;
                }

                CheckLookahead(inSubgraph ? LineFollowers : TopLineStarts);
            }
            else if (Is(StatementStarts))
            {
                ParseStatement(document);
            }
            else if (inSubgraph && Is(End))
            {
                return document;
            }
            else
            {
                throw Fail(inSubgraph ? LineFollowers : TopLineStarts);
            }
        }
    }

    /// <summary>
    ///     Parses one statement and adds its value to the document.
    /// </summary>
    /// <param name="document">The document the statement is in.</param>
    private void ParseStatement(ErDocument document)
    {
        var token = Current;
        var location = Locate(token);
        switch (token.Kind)
        {
            case AccTitle:
                {
                    Advance();
                    var title = Preprocessor.TrimJs(TextOf(Expect(AccTitleValue)));
                    CheckLookahead(LineFollowers);
                    _model.SetAccessibleTitle(title);
                    document.Items.Add(title);
                    return;
                }

            case AccDescr:
                {
                    Advance();
                    var description = Preprocessor.TrimJs(TextOf(Expect(AccDescrValue)));
                    CheckLookahead(LineFollowers);
                    _model.SetAccessibleDescription(description);
                    document.Items.Add(description);
                    return;
                }

            case AccDescrMultilineValue:
                {
                    Advance();
                    var description = Preprocessor.TrimJs(TextOf(token));
                    CheckLookahead(LineFollowers);
                    _model.SetAccessibleDescription(description);
                    document.Items.Add(description);
                    return;
                }

            case DirectionTb or DirectionBt or DirectionRl or DirectionLr:
                {
                    Advance();
                    CheckLookahead(LineFollowers);
                    var direction = token.Kind[^2..].ToUpperInvariant();
                    if (_depth == 0)
                    {
                        _model.SetDirection(direction);
                    }
                    else
                    {
                        document.Direction = direction;
                    }

                    return;
                }

            case ClassDef:
                {
                    Advance();
                    var ids = ParseIdList(StyleIdFollowers);
                    var styles = ParseStyles();
                    _model.AddClass(ids, styles, location);
                    document.Items.Add(token.Text);
                    return;
                }

            case ClassKeyword:
                {
                    Advance();
                    var ids = ParseIdList([.. Ids, Comma]);
                    var classes = ParseIdList([.. LineFollowers, Comma]);
                    _model.SetClass(ids, classes, location);
                    document.Items.Add(token.Text);
                    return;
                }

            case StyleKeyword:
                {
                    Advance();
                    var ids = ParseIdList(StyleIdFollowers);
                    var styles = ParseStyles();
                    _model.AddCssStyles(ids, styles, location);
                    document.Items.Add(token.Text);
                    return;
                }

            case Subgraph:
                document.Items.Add(ParseSubgraph());
                return;
            default:
                ParseEntityStatement(document);
                return;
        }
    }

    /// <summary>
    ///     Parses a statement that starts with an entity name: the entity alone, with an alias, classes or an attribute
    ///     block, or a relationship.
    /// </summary>
    /// <param name="document">The document the statement is in.</param>
    private void ParseEntityStatement(ErDocument document)
    {
        var location = Locate(Current);
        var name = ParseEntityName(Names);
        CheckLookahead(EntityFollowers);
        document.Items.Add(name);
        if (Is(Sqs))
        {
            ParseAliasedEntity(name, location);
            return;
        }

        IReadOnlyList<string>? classes = null;
        if (Is(StyleSeparator))
        {
            Advance();
            classes = ParseIdList(EntityClassFollowers);
        }

        if (Is(Cardinalities))
        {
            ParseRelationship(name, classes, location, document);
            return;
        }

        var attributes = Is(BlockStart) ? ParseBlock() : null;
        _model.AddEntity(name, string.Empty, location);
        if (attributes is not null)
        {
            _model.AddAttributes(name, attributes, location);
        }

        if (classes is not null)
        {
            _model.SetClass([name], classes, location);
        }
    }

    /// <summary>
    ///     Parses the rest of an entity statement with an alias (<c>name[alias]</c>), with optional classes and an
    ///     optional attribute block.
    /// </summary>
    /// <param name="name">The entity name.</param>
    /// <param name="location">The location of the statement.</param>
    private void ParseAliasedEntity(string name, MermaidLocation location)
    {
        Advance();
        var alias = ParseEntityName(Names);
        CheckLookahead([Sqe]);
        Advance();
        CheckLookahead(AliasFollowers);
        IReadOnlyList<string>? classes = null;
        if (Is(StyleSeparator))
        {
            Advance();
            classes = ParseIdList(AliasClassFollowers);
        }

        var attributes = Is(BlockStart) ? ParseBlock() : null;
        _model.AddEntity(name, alias, location);
        if (attributes is not null)
        {
            _model.AddAttributes(name, attributes, location);
        }

        if (classes is not null)
        {
            _model.SetClass([name], classes, location);
        }
    }

    /// <summary>
    ///     Parses the rest of a relationship after its first entity and classes: the relationship specification, the
    ///     second entity with its optional classes, and the role.
    /// </summary>
    /// <param name="first">The first entity's name.</param>
    /// <param name="firstClasses">The first entity's classes, or null.</param>
    /// <param name="location">The location of the statement.</param>
    /// <param name="document">The document the statement is in.</param>
    private void ParseRelationship(string first, IReadOnlyList<string>? firstClasses, MermaidLocation location, ErDocument document)
    {
        // relSpec: cardinality relType cardinality, with cardA the right one and cardB the left one
        var left = Advance().Kind;
        CheckLookahead(CardinalityFollowers);
        CheckLookahead(RelationshipTypes);
        var type = Advance().Kind;
        CheckLookahead(Cardinalities);
        var right = Advance().Kind;
        CheckLookahead(CardinalityFollowers);
        CheckLookahead(Names);

        var second = ParseEntityName(Names);
        CheckLookahead(SecondEntityFollowers);
        document.Items.Add(second);
        IReadOnlyList<string>? secondClasses = null;
        if (Is(StyleSeparator))
        {
            Advance();
            secondClasses = ParseIdList(RoleClassFollowers);
        }

        Expect(Colon);
        var role = ExpectAny(Roles);
        CheckLookahead(LineFollowers);
        var roleText = TextOf(role);
        if (role.Kind != UnicodeText)
        {
            roleText = roleText.Replace("\"", string.Empty, StringComparison.Ordinal);
        }

        _model.AddEntity(first, string.Empty, location);
        _model.AddEntity(second, string.Empty, location);
        _model.AddRelationship(first, roleText, second, new ErRelationshipSpec(right, type, left), location);
        if (firstClasses is not null)
        {
            _model.SetClass([first], firstClasses, location);
        }

        if (secondClasses is not null)
        {
            _model.SetClass([second], secondClasses, location);
        }
    }

    /// <summary>
    ///     Parses a subgraph: <c>subgraph id</c> or <c>subgraph id [title words]</c>, its lines and its <c>end</c>.
    /// </summary>
    /// <returns>The id of the subgraph, its value in the enclosing document.</returns>
    private string ParseSubgraph()
    {
        var keyword = Advance();
        var location = Locate(keyword);
        var id = ParseEntityName(Names);
        CheckLookahead(SubgraphIdFollowers);
        var title = id;
        if (Is(Sqs))
        {
            Advance();
            var words = new List<string> { ParseEntityName(Names) };
            CheckLookahead(TitleFollowers);
            while (Is(Names))
            {
                words.Add(ParseEntityName(Names));
                CheckLookahead(TitleFollowers);
            }

            title = string.Join(" ", words);
            CheckDerived(title, location);
            Advance();
            CheckLookahead(Separators);
        }

        Advance();
        CheckLookahead(LineFollowers);

        // The header is reduced: one more subgraph is open
        _depth++;
        if (_depth > _limits.MaxNestingDepth)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"subgraphs are nested deeper than {_limits.MaxNestingDepth} levels (MermaidLimits.MaxNestingDepth)"),
                location,
                "nest fewer subgraphs, or raise MermaidLimits.MaxNestingDepth when the source is trusted");
        }

        var body = ParseDocument(inSubgraph: true);
        Advance();
        CheckLookahead(LineFollowers);
        _depth--;
        return _model.AddSubgraph(id, body.Items, body.Direction, title, location);
    }

    /// <summary>
    ///     Parses an entity name (<c>entityName</c>): a quoted name without its quotes, a word or a number.
    /// </summary>
    /// <param name="expected">The tokens the state before the name accepts.</param>
    /// <returns>The name.</returns>
    private string ParseEntityName(string[] expected)
    {
        var token = ExpectAny(expected);
        CheckLookahead(NameFollowers);
        var text = TextOf(token);
        return token.Kind == EntityName ? text.Replace("\"", string.Empty, StringComparison.Ordinal) : text;
    }

    /// <summary>
    ///     Parses an id list (<c>idList</c>): ids separated by commas.
    /// </summary>
    /// <param name="followers">The tokens the state after the list accepts (always with the comma).</param>
    /// <returns>The ids.</returns>
    private List<string> ParseIdList(string[] followers)
    {
        var ids = new List<string>();
        while (true)
        {
            ids.Add(TextOf(ExpectAny(Ids)));
            CheckLookahead(IdFollowers);
            CheckLookahead(followers);
            if (!Is(Comma))
            {
                return ids;
            }

            Advance();
        }
    }

    /// <summary>
    ///     Parses the styles of a <c>classDef</c> or <c>style</c> statement (<c>stylesOpt</c>) and its separator: styles
    ///     separated by commas, each the concatenation of its components (white space between them is dropped).
    /// </summary>
    /// <returns>The styles.</returns>
    private List<string> ParseStyles()
    {
        var styles = new List<string>();
        while (true)
        {
            var location = Locate(Current);
            var style = new StringBuilder();
            do
            {
                style.Append(TextOf(ExpectAny(StyleComponents)));
                CheckLength(style.Length, location);
                CheckLookahead(ComponentFollowers);
            }
            while (Is(StyleComponents));

            styles.Add(style.ToString());
            CheckLookahead(StylesFollowers);
            if (!Is(Comma))
            {
                break;
            }

            Advance();
        }

        Advance();
        CheckLookahead(LineFollowers);
        return styles;
    }

    /// <summary>
    ///     Parses an attribute block, from its <c>{</c> to its <c>}</c>, and checks what follows the statement.
    /// </summary>
    /// <returns>The attributes in order.</returns>
    private List<ErAttribute> ParseBlock()
    {
        Advance();
        CheckLookahead(AttributeStarts);
        var attributes = new List<ErAttribute>();
        while (Is(AttributeWord))
        {
            attributes.Add(ParseAttribute());
        }

        Expect(BlockStop);
        CheckLookahead(LineFollowers);
        return attributes;
    }

    /// <summary>
    ///     Parses an attribute: its type (with an optional <c>?</c>), its name, its keys and its comment.
    /// </summary>
    /// <returns>The attribute.</returns>
    private ErAttribute ParseAttribute()
    {
        var typeToken = Advance();
        var type = TextOf(typeToken);
        CheckLookahead(TypeFollowers);
        if (Is(Question))
        {
            type += Advance().Text;
            CheckDerived(type, Locate(typeToken));
        }

        var name = TextOf(Expect(AttributeWord));
        CheckLookahead(AttributeNameFollowers);
        var keys = new List<string>();
        if (Is(AttributeKey))
        {
            while (true)
            {
                keys.Add(TextOf(Advance()));
                CheckLookahead(KeyFollowers);
                if (!Is(KeyComma))
                {
                    break;
                }

                Advance();
                CheckLookahead([AttributeKey]);
            }
        }

        var comment = string.Empty;
        if (Is(Comment))
        {
            comment = TextOf(Advance()).Replace("\"", string.Empty, StringComparison.Ordinal);
            CheckLookahead(AttributeStarts);
        }

        return new ErAttribute(type, name, keys, comment);
    }

    /// <summary>
    ///     Checks the lookahead against the actions of the LALR state the parser is in, and fails with them otherwise, as
    ///     jison lists them.
    /// </summary>
    /// <param name="accepted">The tokens the state accepts.</param>
    private void CheckLookahead(string[] accepted)
    {
        if (!Is(accepted))
        {
            throw Fail(accepted);
        }
    }

    /// <summary>
    ///     Gets the text of a token that is kept, and checks <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The text.</returns>
    private string TextOf(ScanToken token)
    {
        CheckLength(token.Text.Length, Locate(token));
        return token.Text;
    }

    /// <summary>
    ///     Checks a text the grammar joins from tokens (a style or a subgraph title) against
    ///     <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location of its first token.</param>
    private void CheckDerived(string text, MermaidLocation location) => CheckLength(text.Length, location);

    /// <summary>
    ///     Checks the length of a text against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <param name="location">The location of the text.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is too long.</exception>
    private void CheckLength(int length, MermaidLocation location)
    {
        if (length > _limits.MaxLabelLength)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a text is {length} characters long, more than the limit of {_limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)"),
                location,
                "shorten the text, or raise MermaidLimits.MaxLabelLength when the source is trusted");
        }
    }

    /// <summary>
    ///     Gets the location of a token in the original text.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The location.</returns>
    private MermaidLocation Locate(ScanToken token) => _scanner.Locate(token.Start);

    /// <summary>
    ///     Gets a value indicating whether the token stream has ended (jison's <c>$end</c>).
    /// </summary>
    /// <returns>Whether it has.</returns>
    private bool IsEnd() => Current.Kind == SourceScanner.EndOfInput;

    /// <summary>
    ///     Gets a value indicating whether the lookahead is of one of the kinds.
    /// </summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>Whether it is.</returns>
    private bool Is(params string[] kinds) => !IsEnd() && Array.IndexOf(kinds, Current.Kind) >= 0;

    /// <summary>
    ///     Consumes the lookahead token.
    /// </summary>
    /// <returns>The token.</returns>
    private ScanToken Advance() => Lex(_cursor.Advance);

    /// <summary>
    ///     Consumes the lookahead token when it is of the kind, and fails otherwise.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The token.</returns>
    private ScanToken Expect(string kind) => Is(kind) ? Advance() : throw Fail([kind]);

    /// <summary>
    ///     Consumes the lookahead token when it is of one of the kinds, and fails otherwise with all of them expected.
    /// </summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>The token.</returns>
    private ScanToken ExpectAny(string[] kinds) => Is(kinds) ? Advance() : throw Fail(kinds);

    /// <summary>
    ///     Runs a step that lexes a token, and gives a lexical error a hint for ER diagrams.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="step">The step.</param>
    /// <returns>The result of the step.</returns>
    private T Lex<T>(Func<T> step)
    {
        try
        {
            return step();
        }
        catch (MermaidSyntaxException ex) when (ex.Code == MermaidDiagnosticCodes.SyntaxError && ex.Expected.Count == 0)
        {
            // The scanner stops at the text no rule matches (only a style statement has such text)
            throw new MermaidSyntaxException(
                ex.Code,
                $"unrecognized text {SourceScanner.Describe(_scanner.Input.Text, _scanner.Position)}",
                ex.Location,
                "write the styles of a style or classDef statement as name:value pairs of letters, digits, '-', '*' and '#', separated by commas");
        }
    }

    /// <summary>
    ///     Creates the exception for a lookahead token the grammar does not accept.
    /// </summary>
    /// <param name="expectedKinds">The tokens jison lists in the state of the error.</param>
    /// <param name="hint">The suggested fix, or null for a hint from the context.</param>
    /// <returns>The located exception.</returns>
    private MermaidSyntaxException Fail(IEnumerable<string> expectedKinds, string? hint = null)
    {
        var expected = expectedKinds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ErTokens.Number)
            .ThenBy(kind => kind, StringComparer.Ordinal)
            .Select(JisonName)
            .ToList();
        var token = Current;
        var found = IsEnd() ? "the end of the text" : $"'{JisonName(token.Kind)}' ({Describe(token.Text)})";
        return new MermaidSyntaxException(
            MermaidDiagnosticCodes.SyntaxError,
            $"expected {TokenCursor.JoinAlternatives([.. expected.Select(e => $"'{e}'")])}, found {found}",
            Locate(token),
            hint ?? HintFor(token, expected),
            expected);
    }

    /// <summary>
    ///     Describes the text of a token for a message.
    /// </summary>
    /// <param name="text">The token text.</param>
    /// <returns>The quoted text, shortened and with line breaks shown.</returns>
    private static string Describe(string text)
    {
        var shown = text.Length > 24 ? text[..24] + "..." : text;
        return shown.Length == 0 ? "no text" : "'" + shown.Replace("\n", "\\n", StringComparison.Ordinal) + "'";
    }

    /// <summary>
    ///     Suggests a fix for an unexpected token from what was expected.
    /// </summary>
    /// <param name="token">The unexpected token.</param>
    /// <param name="expected">The jison names of the expected tokens.</param>
    /// <returns>The hint.</returns>
    private string HintFor(ScanToken token, List<string> expected)
    {
        if (expected.Contains(Colon) && expected.Count <= 2)
        {
            return "end a relationship with a colon and a role: A ||--o{ B : places (use \"\" for an empty role)";
        }

        if (expected.SequenceEqual(Roles))
        {
            return "write the role after the colon as one word or in double quotes; the words 'to', 'one' and 'many' are keywords";
        }

        if (expected.Contains(AttributeWord))
        {
            return "write each attribute as a type and a name, then optional keys (PK, FK, UK) and a \"comment\", and close the block with '}'";
        }

        if (IsEnd() && expected.Contains(End))
        {
            return "close every subgraph with 'end'";
        }

        if (token.Kind == Word || token.Kind.Length == 1)
        {
            return "write names with other characters than letters, digits, '-', '_', '*' and '.' in double quotes, without '%' or '\\'; end statements with a line break, not ';' or a '%%' comment";
        }

        return "check the statement at this position against the ER diagram syntax";
    }

    /// <summary>
    ///     The value of a document: what its statements give (Mermaid's list that a subgraph reads its nodes from) and its
    ///     last direction statement.
    /// </summary>
    private sealed class ErDocument
    {
        /// <summary>Gets the values of the statements in order: entity names, subgraph ids and texts.</summary>
        public List<string> Items { get; } = [];

        /// <summary>Gets or sets the last direction of the document, or null.</summary>
        public string? Direction { get; set; }
    }
}
