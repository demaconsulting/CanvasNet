// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;
using static DemaConsulting.CanvasNet.Mermaid.Class.ClassTokens;

namespace DemaConsulting.CanvasNet.Mermaid.Class;

/// <summary>
///     The class parser: a hand-written port of Mermaid's <c>classDiagram.jison</c> (its lexer and its grammar) that
///     applies each statement to a <see cref="ClassModel"/> when jison's parser would run its action.
/// </summary>
/// <remarks>
///     <para>
///         <b>Lexer.</b> The 99 lexer rules and 15 start conditions of the generated jison lexer are ported one to one
///         (first matching rule wins, case-sensitive), with hand-written matchers in place of the regular expressions, so
///         names, labels, members, generics, strings and keywords split exactly as in Mermaid, quirks included: a line that
///         contains <c>direction LR</c> anywhere is a direction statement, a class body line is one member token, a colon
///         starts a label up to the next colon, and keywords such as <c>note</c> or <c>link</c> are keywords inside
///         generics and backticks too.
///     </para>
///     <para>
///         <b>Grammar.</b> A recursive-descent parser with one token of lookahead accepts the language of the LALR(1)
///         grammar: the header, class names (dotted, concatenated, backticked and generic), class statements with labels,
///         <c>:::</c> classes, annotations and bodies, member statements, relations with cardinalities and labels,
///         namespaces (nested, dotted and labeled), notes, annotation statements, <c>classDef</c>, <c>style</c>,
///         <c>cssClass</c>, <c>callback</c>, <c>link</c> and <c>click</c>, the directions and the accessibility statements.
///         Names, statements and members are read in loops, so a long text cannot exhaust the stack; only nested
///         namespaces recurse. Each database action runs at the point where jison reduces its rule, after the lookahead
///         that decides the reduction has been checked, so grammar and database errors come in Mermaid's order.
///     </para>
///     <para>
///         <b>Errors.</b> A token the grammar does not accept is a located <see cref="MermaidSyntaxException"/> with code
///         <see cref="MermaidDiagnosticCodes.SyntaxError"/>, a hint, and as <see cref="MermaidSyntaxException.Expected"/>
///         the jison names of the tokens of the LALR state where jison reports the error, in jison's order: where several
///         states share a reduction on a merged lookahead set (after a name token, a class name, a relation end or a style
///         part), the lookahead is checked against the merged set first. Text no rule matches is a located lexical error.
///         Namespaces nested deeper than <see cref="MermaidLimits.MaxNestingDepth"/> and texts longer than
///         <see cref="MermaidLimits.MaxLabelLength"/> are refused with <see cref="MermaidDiagnosticCodes.LimitExceeded"/>.
///     </para>
///     <para>
///         Parsing is linear in the text. An instance parses one text on one thread.
///     </para>
/// </remarks>
internal sealed partial class ClassParser
{
    /// <summary>The tokens of an <c>alphaNumToken</c>.</summary>
    private static readonly string[] NameTokens = [Alpha, Num, Minus, UnicodeText];

    /// <summary>The tokens that start a class or namespace name.</summary>
    private static readonly string[] NameStarts = [.. NameTokens, BquoteStr];

    /// <summary>The relation ends.</summary>
    private static readonly string[] RelationTypes = [Aggregation, Extension, Composition, Dependency, Lollipop];

    /// <summary>The relation lines.</summary>
    private static readonly string[] LineTypes = [Line, DottedLine];

    /// <summary>The tokens that start a statement.</summary>
    private static readonly string[] StatementStarts =
    [
        AccTitle, AccDescr, AccDescrMultilineValue, Namespace, AnnotationStart, ClassTokens.Class, Member, Separator, NoteFor, Note,
        ClassDef, DirectionTb, DirectionBt, DirectionRl, DirectionLr, Callback, Link, Click, Style, CssClass, .. NameStarts,
    ];

    /// <summary>The tokens that may follow a whole statement.</summary>
    private static readonly string[] StatementEnds = [Newline, Eof];

    /// <summary>The tokens that may follow a statement that can also stand in a namespace (<c>$Vy</c>).</summary>
    private static readonly string[] StructEnds = [Newline, Eof, StructStop];

    /// <summary>The tokens that may follow a class name (its merged lookahead, <c>$VA</c>).</summary>
    private static readonly string[] NameFollowers =
    [
        Newline, Eof, Sqs, Str, Label, StructStart, StructStop, StyleSeparator, AnnotationStart, .. RelationTypes, .. LineTypes,
        CallbackName, Href,
    ];

    /// <summary>The tokens that may follow a name token (the merged lookahead of <c>alphaNumToken</c>, <c>$VB</c>).</summary>
    private static readonly string[] NameTokenFollowers = [.. NameFollowers, Dot, GenericType, AnnotationEnd, .. NameStarts];

    /// <summary>The tokens that may follow a name token inside a class name.</summary>
    private static readonly string[] NameContinuations = [.. NameFollowers, Dot, GenericType, .. NameStarts];

    /// <summary>The tokens that may follow a backticked name.</summary>
    private static readonly string[] LiteralFollowers = [.. NameFollowers, GenericType];

    /// <summary>The tokens that may follow a class name that starts a statement.</summary>
    private static readonly string[] NameStatementFollowers = [Newline, Eof, Str, Label, .. RelationTypes, .. LineTypes];

    /// <summary>The tokens that may follow a relation end.</summary>
    private static readonly string[] RelationTypeFollowers = [Str, .. LineTypes, .. NameStarts];

    /// <summary>The tokens that may follow a relation line.</summary>
    private static readonly string[] LineFollowers = [Str, .. RelationTypes, .. NameStarts];

    /// <summary>The tokens that may follow a whole relation.</summary>
    private static readonly string[] RelationFollowers = [Str, .. NameStarts];

    /// <summary>The tokens that may follow the second class of a relation.</summary>
    private static readonly string[] RelationEnds = [Newline, Eof, Label];

    /// <summary>The tokens that may follow the class name of a class statement.</summary>
    private static readonly string[] ClassNameFollowers = [Newline, Eof, Sqs, StructStart, StructStop, StyleSeparator, AnnotationStart];

    /// <summary>The tokens that may follow a class label.</summary>
    private static readonly string[] LabelFollowers = [Newline, Eof, StructStart, StructStop, StyleSeparator, AnnotationStart];

    /// <summary>The tokens that may follow a class statement that can still take a body.</summary>
    private static readonly string[] BodyOrEnd = [Newline, Eof, StructStart, StructStop];

    /// <summary>The tokens that make up a style.</summary>
    private static readonly string[] StyleComponents = [Label, Space, Alpha, Style, Num, Colon, Unit, Brkt, Pct];

    /// <summary>The tokens that may follow a part of a style.</summary>
    private static readonly string[] StyleFollowers = [Newline, Eof, Comma, .. StyleComponents];

    /// <summary>The tokens that may follow a class name of a <c>classDef</c> statement.</summary>
    private static readonly string[] ClassListFollowers = [Comma, .. StyleComponents];

    /// <summary>The tokens that start an item of a namespace.</summary>
    private static readonly string[] NamespaceItems = [Namespace, ClassTokens.Class, NoteFor, Note];

    /// <summary>The tokens that may follow a name token inside a namespace name.</summary>
    private static readonly string[] NamespaceNameContinuations = [Sqs, Dot, StructStart, .. NameStarts];

    /// <summary>The tokens that may follow a namespace name.</summary>
    private static readonly string[] NamespaceNameFollowers = [Sqs, StructStart];

    /// <summary>The model the statements are applied to.</summary>
    private readonly ClassModel _model;

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The lexer.</summary>
    private SourceScanner _scanner = null!;

    /// <summary>The token cursor.</summary>
    private TokenCursor _cursor = null!;

    /// <summary>The current nesting depth of namespaces.</summary>
    private int _depth;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="model">The model to fill.</param>
    /// <param name="limits">The limits of the open call.</param>
    private ClassParser(ClassModel model, MermaidLimits limits)
    {
        _model = model;
        _limits = limits;
    }

    /// <summary>
    ///     Gets the lookahead token.
    /// </summary>
    private ScanToken Current => _cursor.Current;

    /// <summary>
    ///     Parses a class diagram.
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="detected">The detected diagram (its keyword location and Mermaid id).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <param name="hierarchicalNamespaces">
    ///     Whether the text's own configuration nests dotted namespaces (<c>class.hierarchicalNamespaces</c>), which selects
    ///     the namespaces the metadata counts.
    /// </param>
    /// <returns>The model, with every statement applied.</returns>
    /// <exception cref="MermaidSyntaxException">
    ///     Thrown, located, when the text does not follow the grammar, when Mermaid's database fails on a statement, or when
    ///     a limit is exceeded.
    /// </exception>
    public static ClassModel Parse(PreprocessedSource source, DetectedDiagram detected, MermaidLimits limits, bool hierarchicalNamespaces = true)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var model = new ClassModel(limits, detected.Location, detected.MermaidId) { HierarchicalNamespaces = hierarchicalNamespaces };
        new ClassParser(model, limits).Run(source.ParserText);
        return model;
    }

    /// <summary>
    ///     Parses the whole text (<c>start</c>): the keyword, a line break and the statements up to <c>EOF</c>, or, when the
    ///     text does not start with the keyword (a first line that holds a direction), the statements alone.
    /// </summary>
    /// <param name="text">The text.</param>
    private void Run(MappedText text)
    {
        _scanner = new SourceScanner(text, CreateRules(), Conditions);
        _cursor = Lex(() => new TokenCursor(_scanner));
        if (Is(ClassDiagram))
        {
            Advance();
            Expect(Newline);
            ParseStatements([.. StatementStarts]);

            // graphConfig: CLASS_DIAGRAM NEWLINE statements EOF, and then the end of the tokens
            Expect(Eof);
        }
        else if (Is(StatementStarts))
        {
            ParseStatements([ClassDiagram, .. StatementStarts]);
        }
        else
        {
            throw Fail([ClassDiagram, .. StatementStarts], "start the diagram with the classDiagram keyword");
        }

        if (!IsEnd())
        {
            throw Fail([]);
        }
    }

    /// <summary>
    ///     Parses statements separated by line breaks (<c>statements</c>), up to <c>EOF</c> or the end of the tokens.
    /// </summary>
    /// <param name="firstExpected">The tokens jison lists when the first statement is missing.</param>
    private void ParseStatements(string[] firstExpected)
    {
        var expected = firstExpected;
        while (true)
        {
            if (!Is(StatementStarts))
            {
                throw Fail(expected);
            }

            ParseStatement();
            CheckLookahead(StatementEnds, endAllowed: true);
            if (!Is(Newline))
            {
                return;
            }

            // After the line break: the end of the statements, or another statement
            Advance();
            if (Is(Eof) || IsEnd())
            {
                return;
            }

            expected = [Eof, .. StatementStarts];
        }
    }

    /// <summary>
    ///     Parses one statement.
    /// </summary>
    private void ParseStatement()
    {
        var token = Current;
        switch (token.Kind)
        {
            case ClassTokens.Class:
                ParseClassStatement();
                break;
            case Namespace:
                ParseNamespaceStatement();
                break;
            case AnnotationStart:
                ParseAnnotationStatement();
                break;
            case NoteFor or Note:
                ParseNoteStatement();
                break;
            case ClassDef:
                ParseClassDefStatement();
                break;
            case Style:
                ParseStyleStatement();
                break;
            case CssClass:
                ParseCssClassStatement();
                break;
            case Callback or Link or Click:
                ParseClickStatement();
                break;
            case Member or Separator:
                // A member or separator outside a class body is accepted and ignored
                Advance();
                CheckLookahead(StatementEnds, endAllowed: true);
                break;
            case DirectionTb or DirectionBt or DirectionRl or DirectionLr:
                Advance();
                CheckLookahead(StatementEnds, endAllowed: true);
                _model.SetDirection(token.Kind[^2..].ToUpperInvariant());
                break;
            case AccTitle:
                Advance();
                var title = Expect(AccTitleValue);
                CheckLookahead(StatementEnds, endAllowed: true);
                _model.SetAccessibleTitle(Preprocessor.TrimJs(TextOf(title)));
                break;
            case AccDescr:
                Advance();
                var description = Expect(AccDescrValue);
                CheckLookahead(StatementEnds, endAllowed: true);
                _model.SetAccessibleDescription(Preprocessor.TrimJs(TextOf(description)));
                break;
            case AccDescrMultilineValue:
                Advance();
                CheckLookahead(StatementEnds, endAllowed: true);
                _model.SetAccessibleDescription(Preprocessor.TrimJs(TextOf(token)));
                break;
            default:
                ParseNameStatement();
                break;
        }
    }

    /// <summary>
    ///     Parses a statement that starts with a class name: a member statement (<c>X : member</c>, or a lone name, which
    ///     does nothing) or a relation (<c>X ["c1"] relation ["c2"] Y [: label]</c>).
    /// </summary>
    private void ParseNameStatement()
    {
        var first = Current;
        var id1 = ParseClassName();

        // memberStatement: className LABEL adds a member; a lone className does nothing
        if (Is(Label))
        {
            var label = Advance();
            CheckLookahead(StatementEnds, endAllowed: true);
            _model.AddMember(id1, ClassModel.CleanupLabel(TextOf(label)), Locate(first));
            return;
        }

        if (IsStatementEnd())
        {
            return;
        }

        if (!Is(Str) && !Is(RelationTypes) && !Is(LineTypes))
        {
            throw Fail(NameStatementFollowers);
        }

        // relationStatement: the optional first cardinality, the relation, the optional second cardinality, the class
        var title1 = "none";
        if (Is(Str))
        {
            title1 = TextOf(Advance());
            if (!Is(RelationTypes) && !Is(LineTypes))
            {
                throw Fail([.. RelationTypes, .. LineTypes]);
            }
        }

        var relation = ParseRelation();
        var title2 = "none";
        if (Is(Str))
        {
            title2 = TextOf(Advance());
            if (!Is(NameStarts))
            {
                throw Fail(NameStarts);
            }
        }
        else if (!Is(NameStarts))
        {
            throw Fail(RelationFollowers);
        }

        var id2 = ParseClassName();
        CheckLookahead(RelationEnds, endAllowed: true);
        string? title = null;
        if (Is(Label))
        {
            title = ClassModel.CleanupLabel(TextOf(Advance()));
            CheckLookahead(StatementEnds, endAllowed: true);
        }

        _model.AddRelation(new ClassRelationInput(id1, id2, title1, title2, relation, title), Locate(first));
    }

    /// <summary>
    ///     Parses a relation (<c>relation</c>): an optional end, a line and an optional end.
    /// </summary>
    /// <returns>The ends (null for none) and the line, as Mermaid's relation and line type codes.</returns>
    private ClassRelationType ParseRelation()
    {
        int? type1 = null;
        int? type2 = null;
        if (Is(RelationTypes))
        {
            type1 = RelationCode(Advance().Kind);
            CheckLookahead(RelationTypeFollowers);
            if (!Is(LineTypes))
            {
                throw Fail(LineTypes);
            }
        }

        var line = Advance().Kind == Line ? ClassModel.SolidLine : ClassModel.DottedLine;
        CheckLookahead(LineFollowers);
        if (Is(RelationTypes))
        {
            type2 = RelationCode(Advance().Kind);
            CheckLookahead(RelationTypeFollowers);
            CheckLookahead(RelationFollowers);
        }

        return new ClassRelationType(type1, type2, line);
    }

    /// <summary>
    ///     Parses a class name (<c>className</c>): name tokens, concatenated or joined by dots, ending with a name token or a
    ///     backticked name, with an optional generic type at the end. The lookahead must start a name.
    /// </summary>
    /// <returns>The name as the grammar builds it (<c>a.b</c>, <c>ab</c>, <c>Name~T~</c>).</returns>
    private string ParseClassName()
    {
        var start = Current;
        var name = new StringBuilder();
        while (true)
        {
            if (Is(BquoteStr))
            {
                name.Append(TextOf(Advance()));
                CheckLookahead(LiteralFollowers, endAllowed: true);
                AppendGeneric(name);
                break;
            }

            name.Append(Advance().Text);
            CheckLookahead(NameTokenFollowers, endAllowed: true);
            if (Is(Dot))
            {
                Advance();
                name.Append('.');
                if (!Is(NameStarts))
                {
                    throw Fail(NameStarts);
                }

                continue;
            }

            if (Is(GenericType))
            {
                AppendGeneric(name);
                break;
            }

            if (Is(NameStarts))
            {
                continue;
            }

            if (Is(NameFollowers) || IsEnd())
            {
                break;
            }

            throw Fail(NameContinuations);
        }

        return CheckLength(name.ToString(), start);
    }

    /// <summary>
    ///     Appends the generic type that may end a class name (<c>~T~</c>).
    /// </summary>
    /// <param name="name">The name so far.</param>
    private void AppendGeneric(StringBuilder name)
    {
        if (Is(GenericType))
        {
            name.Append('~').Append(TextOf(Advance())).Append('~');
            CheckLookahead(NameFollowers, endAllowed: true);
        }
    }

    /// <summary>
    ///     Reads one name token (<c>alphaNumToken</c>), as an annotation or a CSS class.
    /// </summary>
    /// <returns>The text.</returns>
    private string ReadNameToken()
    {
        if (!Is(NameTokens))
        {
            throw Fail(NameTokens);
        }

        var token = Advance();
        CheckLookahead(NameTokenFollowers, endAllowed: true);
        return TextOf(token);
    }

    /// <summary>
    ///     Parses a class statement (<c>classStatement</c>): <c>class</c>, the name, an optional label, then an optional
    ///     <c>:::</c> class, annotation or body.
    /// </summary>
    /// <returns>The class name as written (the value of the statement in a namespace).</returns>
    private string ParseClassStatement()
    {
        Advance();
        if (!Is(NameStarts))
        {
            throw Fail(NameStarts);
        }

        var nameToken = Current;
        var location = Locate(nameToken);
        var name = ParseClassName();

        // classIdentifier: the class is added when the name (and label) is reduced
        string? label = null;
        if (Is(Sqs))
        {
            label = ParseLabel();
            CheckLookahead(LabelFollowers, endAllowed: true);
        }
        else
        {
            CheckLookahead(ClassNameFollowers, endAllowed: true);
        }

        _model.AddClass(name, location);
        if (label is not null)
        {
            _model.SetClassLabel(name, label, location);
        }

        if (Is(StyleSeparator))
        {
            Advance();
            var cssClass = ReadNameToken();
            if (Is(StructStart))
            {
                var members = ParseBody(allowEmpty: false);
                CheckLookahead(StructEnds, endAllowed: true);
                _model.SetCssClass(name, cssClass);
                _model.AddMembers(name, members, location);
            }
            else
            {
                CheckLookahead(BodyOrEnd, endAllowed: true);
                _model.SetCssClass(name, cssClass);
            }
        }
        else if (Is(AnnotationStart))
        {
            Advance();
            var annotation = ReadNameToken();
            Expect(AnnotationEnd);
            if (Is(StructStart))
            {
                var members = ParseBody(allowEmpty: true);
                CheckLookahead(StructEnds, endAllowed: true);
                _model.AddAnnotation(name, annotation, location);
                _model.AddMembers(name, members, location);
            }
            else
            {
                CheckLookahead(BodyOrEnd, endAllowed: true);
                _model.AddAnnotation(name, annotation, location);
            }
        }
        else if (Is(StructStart))
        {
            var members = ParseBody(allowEmpty: true);
            CheckLookahead(StructEnds, endAllowed: true);
            _model.AddMembers(name, members, location);
        }

        return name;
    }

    /// <summary>
    ///     Parses a label in square brackets (<c>classLabel</c>: <c>["text"]</c>).
    /// </summary>
    /// <returns>The text.</returns>
    private string ParseLabel()
    {
        Advance();
        var text = Expect(Str);
        Expect(Sqe);
        return TextOf(text);
    }

    /// <summary>
    ///     Parses a class body: <c>{</c>, the member lines, <c>}</c>.
    /// </summary>
    /// <param name="allowEmpty">Whether the body may have no member line (it may not after a <c>:::</c> class).</param>
    /// <returns>The member lines in order.</returns>
    private List<string> ParseBody(bool allowEmpty)
    {
        Advance();
        var members = new List<string>();
        if (!Is(Member))
        {
            if (allowEmpty && Is(StructStop))
            {
                Advance();
                return members;
            }

            throw Fail(allowEmpty ? [StructStop, Member] : [Member]);
        }

        while (Is(Member))
        {
            members.Add(TextOf(Advance()));
        }

        Expect(StructStop, Member);
        return members;
    }

    /// <summary>
    ///     Parses an annotation statement (<c>&lt;&lt;annotation&gt;&gt; ClassName</c>).
    /// </summary>
    private void ParseAnnotationStatement()
    {
        Advance();
        var annotation = ReadNameToken();
        Expect(AnnotationEnd);
        if (!Is(NameStarts))
        {
            throw Fail(NameStarts);
        }

        var nameToken = Current;
        var name = ParseClassName();
        CheckLookahead(StatementEnds, endAllowed: true);
        _model.AddAnnotation(name, annotation, Locate(nameToken));
    }

    /// <summary>
    ///     Parses a note (<c>note for Class "text"</c> or <c>note "text"</c>).
    /// </summary>
    /// <returns>The note id (the value of the statement in a namespace).</returns>
    private string ParseNoteStatement()
    {
        var keyword = Advance();
        string? className = null;
        if (keyword.Kind == NoteFor)
        {
            if (!Is(NameStarts))
            {
                throw Fail(NameStarts);
            }

            className = ParseClassName();
        }

        var text = Expect(Str);
        CheckLookahead(StructEnds, endAllowed: true);
        return _model.AddNote(TextOf(text), className, Locate(keyword));
    }

    /// <summary>
    ///     Parses a namespace (<c>namespace Name ["label"] { items }</c>), whose items are classes, notes and namespaces.
    /// </summary>
    private void ParseNamespaceStatement()
    {
        var keyword = Advance();
        Enter(Locate(keyword));
        if (!Is(NameStarts))
        {
            throw Fail(NameStarts);
        }

        var nameToken = Current;
        var name = ParseNamespaceName();
        string? label = null;
        if (Is(Sqs))
        {
            label = ParseLabel();
            CheckLookahead(LabelFollowers, endAllowed: true);
            CheckLookahead([StructStart]);
        }

        // namespaceIdentifier: the namespace is added (and entered) when it is reduced
        var id = _model.AddNamespace(name, label, Locate(nameToken));
        Advance();

        // The items, separated by line breaks
        string[] expected = [Newline, .. NamespaceItems];
        if (Is(Newline))
        {
            Advance();
            expected = NamespaceItems;
        }

        var classes = new List<string>();
        var notes = new List<string>();
        while (true)
        {
            if (!Is(NamespaceItems))
            {
                throw Fail(expected);
            }

            switch (Current.Kind)
            {
                case ClassTokens.Class:
                    classes.Add(ParseClassStatement());
                    break;
                case Namespace:
                    ParseNamespaceStatement();
                    break;
                default:
                    notes.Add(ParseNoteStatement());
                    break;
            }

            if (Is(Newline))
            {
                Advance();
                if (Is(StructStop))
                {
                    break;
                }

                expected = [StructStop, .. NamespaceItems];
                continue;
            }

            if (Is(StructStop))
            {
                break;
            }

            throw Fail([Newline, StructStop]);
        }

        Advance();
        CheckLookahead(StructEnds, endAllowed: true);
        _model.AddClassesToNamespace(id, classes, notes);
        _model.PopNamespace();
        _depth--;
    }

    /// <summary>
    ///     Parses a namespace name (<c>namespaceName</c>): name tokens, concatenated or joined by dots, or a backticked name.
    /// </summary>
    /// <returns>The name.</returns>
    private string ParseNamespaceName()
    {
        var start = Current;
        var name = new StringBuilder();
        while (true)
        {
            if (Is(BquoteStr))
            {
                name.Append(TextOf(Advance()));
                CheckLookahead(LiteralFollowers, endAllowed: true);
                CheckLookahead(NamespaceNameFollowers);
                break;
            }

            name.Append(Advance().Text);
            CheckLookahead(NameTokenFollowers, endAllowed: true);
            if (Is(Dot))
            {
                Advance();
                name.Append('.');
                if (!Is(NameStarts))
                {
                    throw Fail(NameStarts);
                }

                continue;
            }

            if (Is(NameStarts))
            {
                continue;
            }

            CheckLookahead(NamespaceNameContinuations);
            break;
        }

        return CheckLength(name.ToString(), start);
    }

    /// <summary>
    ///     Parses a <c>classDef</c> statement: class names separated by commas, then the styles.
    /// </summary>
    private void ParseClassDefStatement()
    {
        Advance();
        var ids = new List<string>();
        while (true)
        {
            if (!Is(Alpha))
            {
                throw Fail([Alpha]);
            }

            ids.Add(TextOf(Advance()));
            CheckLookahead(ClassListFollowers);
            if (!Is(Comma))
            {
                break;
            }

            Advance();
        }

        if (!Is(StyleComponents))
        {
            throw Fail([.. StyleComponents, Comma]);
        }

        var styles = ParseStyles();
        CheckLookahead(StatementEnds, endAllowed: true);
        _model.DefineClass(ids, styles);
    }

    /// <summary>
    ///     Parses a <c>style</c> statement: a class name, then the styles.
    /// </summary>
    private void ParseStyleStatement()
    {
        Advance();
        var id = Expect(Alpha);
        if (!Is(StyleComponents))
        {
            throw Fail(StyleComponents);
        }

        var styles = ParseStyles();
        CheckLookahead(StatementEnds, endAllowed: true);
        _model.SetCssStyle(TextOf(id), styles);
    }

    /// <summary>
    ///     Parses the styles of a <c>classDef</c> or <c>style</c> statement (<c>stylesOpt</c>): styles separated by commas,
    ///     each the concatenated text of its tokens. The lookahead must start a style.
    /// </summary>
    /// <returns>The styles.</returns>
    private List<string> ParseStyles()
    {
        var styles = new List<string>();
        while (true)
        {
            var start = Current;
            var style = new StringBuilder();
            do
            {
                style.Append(Advance().Text);
                CheckLookahead(StyleFollowers, endAllowed: true);
            }
            while (Is(StyleComponents));

            styles.Add(CheckLength(style.ToString(), start));
            if (!Is(Comma))
            {
                return styles;
            }

            Advance();
            if (!Is(StyleComponents))
            {
                throw Fail(StyleComponents);
            }
        }
    }

    /// <summary>
    ///     Parses a <c>cssClass "A,B" name</c> statement.
    /// </summary>
    private void ParseCssClassStatement()
    {
        Advance();
        var ids = Expect(Str);
        var cssClass = Expect(Alpha);
        CheckLookahead(StatementEnds, endAllowed: true);
        _model.SetCssClass(TextOf(ids), TextOf(cssClass));
    }

    /// <summary>
    ///     Parses a <c>callback</c>, <c>link</c> or <c>click</c> statement. The link data is kept but never drawn.
    /// </summary>
    private void ParseClickStatement()
    {
        var keyword = Advance();
        if (!Is(NameStarts))
        {
            throw Fail(NameStarts);
        }

        var name = ParseClassName();
        switch (keyword.Kind)
        {
            case Callback:
                {
                    // CALLBACK className STR [STR]
                    var function = Expect(Str);
                    var tooltip = Optional(Str, [Newline, Eof, Str]);
                    _model.SetClickEvent(name, TextOf(function));
                    _model.SetTooltip(name, tooltip);
                    break;
                }

            case Link:
                {
                    // LINK className STR [LINK_TARGET | STR [LINK_TARGET]]
                    var link = TextOf(Expect(Str));
                    var tooltip = Optional(Str, [Newline, Eof, Str, LinkTarget]);
                    var target = Optional(LinkTarget, tooltip is null ? [Newline, Eof, Str, LinkTarget] : [Newline, Eof, LinkTarget]);
                    _model.SetLink(name, link, target);
                    _model.SetTooltip(name, tooltip);
                    break;
                }

            default:
                ParseClickAction(name);
                break;
        }
    }

    /// <summary>
    ///     Parses the action of a <c>click</c> statement: <c>call name(args) ["tooltip"]</c> or
    ///     <c>href "link" ["tooltip"] [target]</c>.
    /// </summary>
    /// <param name="name">The class name.</param>
    private void ParseClickAction(string name)
    {
        if (Is(CallbackName))
        {
            var function = TextOf(Advance());
            string? arguments = null;
            if (Is(CallbackArgs))
            {
                arguments = TextOf(Advance());
            }

            var tooltip = Optional(Str, arguments is null ? [Newline, Eof, Str, CallbackArgs] : [Newline, Eof, Str]);
            _model.SetClickEvent(name, function, arguments);
            _model.SetTooltip(name, tooltip);
            return;
        }

        if (!Is(Href))
        {
            throw Fail([CallbackName, Href]);
        }

        Advance();
        var link = TextOf(Expect(Str));
        var linkTooltip = Optional(Str, [Newline, Eof, Str, LinkTarget]);
        var target = Optional(LinkTarget, linkTooltip is null ? [Newline, Eof, Str, LinkTarget] : [Newline, Eof, LinkTarget]);
        _model.SetLink(name, link, target);
        _model.SetTooltip(name, linkTooltip);
    }

    /// <summary>
    ///     Reads an optional last token of a statement: the token when it is of the kind, else nothing, provided the
    ///     statement may end there.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="accepted">The tokens jison accepts at this point.</param>
    /// <returns>The text of the token, or null.</returns>
    private string? Optional(string kind, string[] accepted)
    {
        CheckLookahead(accepted, endAllowed: true);
        return Is(kind) ? TextOf(Advance()) : null;
    }

    /// <summary>
    ///     Gets the code of a relation end token (Mermaid's <c>relationType</c>).
    /// </summary>
    /// <param name="kind">The token kind.</param>
    /// <returns>The code.</returns>
    private static int RelationCode(string kind) => kind switch
    {
        Aggregation => ClassModel.Aggregation,
        Extension => ClassModel.Extension,
        Composition => ClassModel.Composition,
        Dependency => ClassModel.Dependency,
        _ => ClassModel.Lollipop,
    };

    /// <summary>
    ///     Checks the lookahead in a state that reduces on a lookahead set: a token outside the set fails there, with the set
    ///     as jison lists it.
    /// </summary>
    /// <param name="accepted">The set.</param>
    /// <param name="endAllowed">Whether the end of the tokens is in the set (jison never lists it).</param>
    private void CheckLookahead(string[] accepted, bool endAllowed = false)
    {
        if (!Is(accepted) && !(endAllowed && IsEnd()))
        {
            throw Fail(accepted);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the lookahead ends a statement: a line break, <c>EOF</c> or the end of the tokens.
    /// </summary>
    /// <returns>Whether it does.</returns>
    private bool IsStatementEnd() => Is(StatementEnds) || IsEnd();

    /// <summary>
    ///     Enters a namespace, and checks <see cref="MermaidLimits.MaxNestingDepth"/>.
    /// </summary>
    /// <param name="location">The location of its keyword.</param>
    private void Enter(MermaidLocation location)
    {
        _depth++;
        if (_depth > _limits.MaxNestingDepth)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"namespaces are nested deeper than {_limits.MaxNestingDepth} levels (MermaidLimits.MaxNestingDepth)"),
                location,
                "nest fewer namespaces, or raise MermaidLimits.MaxNestingDepth when the source is trusted");
        }
    }

    /// <summary>
    ///     Gets the text of a token that is a label, a member or a name, and checks <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The text.</returns>
    private string TextOf(ScanToken token) => CheckLength(token.Text, token);

    /// <summary>
    ///     Checks a text against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The token the text starts with, for the location.</param>
    /// <returns>The text.</returns>
    private string CheckLength(string text, ScanToken start)
    {
        if (text.Length > _limits.MaxLabelLength)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a text is {text.Length} characters long, more than the limit of {_limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)"),
                Locate(start),
                "shorten the text, or raise MermaidLimits.MaxLabelLength when the source is trusted");
        }

        return text;
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
    private bool Is(params string[] kinds) => Array.IndexOf(kinds, Current.Kind) >= 0;

    /// <summary>
    ///     Consumes the lookahead token.
    /// </summary>
    /// <returns>The token.</returns>
    private ScanToken Advance() => Lex(_cursor.Advance);

    /// <summary>
    ///     Consumes the lookahead token when it is of the first kind, and fails otherwise with all the kinds expected.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="others">The other kinds jison lists at this point.</param>
    /// <returns>The token.</returns>
    private ScanToken Expect(string kind, params string[] others) => Is(kind) ? Advance() : throw Fail([kind, .. others]);

    /// <summary>
    ///     Runs a step that lexes a token, and gives a lexical error a hint for class diagrams.
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
            // The scanner stops at the text no rule matches
            throw new MermaidSyntaxException(
                ex.Code,
                $"unrecognized text {SourceScanner.Describe(_scanner.Input.Text, _scanner.Position)}",
                ex.Location,
                "a class diagram statement ends at the end of its line (without a semicolon); write braces only around a class body or a namespace");
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
            .OrderBy(ClassTokens.Number)
            .ThenBy(kind => kind, StringComparer.Ordinal)
            .Select(JisonName)
            .ToList();
        var token = Current;
        var found = token.Kind == SourceScanner.EndOfInput ? "the end of the text" : $"'{JisonName(token.Kind)}' ({Describe(token.Text)})";
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
    private static string HintFor(ScanToken token, List<string> expected)
    {
        if (token.Kind == EofInStruct)
        {
            return "close every class body and namespace with '}'";
        }

        if (expected.Contains(StructStop))
        {
            return "a class body holds one member per line and ends with '}'; a namespace holds classes, notes and namespaces";
        }

        if (expected.Contains(Str) && expected.Count == 1)
        {
            return "write the text in double quotes, for example note for Order \"text\"";
        }

        if (expected.Contains(Line))
        {
            return "connect two classes with a relation such as <|--, *--, o--, -->, ..> or ..|>, for example Animal <|-- Duck";
        }

        if (expected.Contains(Alpha))
        {
            return "write a class name: letters, digits, underscores and dashes, or a name in backticks";
        }

        return "check the statement at this position against the class diagram syntax";
    }
}
