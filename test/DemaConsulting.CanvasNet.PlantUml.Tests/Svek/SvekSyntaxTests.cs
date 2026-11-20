using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Svek.SvekTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Svek;

/// <summary>
///     Unit tests of <see cref="SvekSyntax"/>: the names, the arrow grammar of links, the link statements, the notes,
///     the page texts, and the common statements of the graph diagrams.
/// </summary>
public class SvekSyntaxTests
{
    /// <summary>
    ///     Validates that every decoration PlantUML writes before or after an arrow body is read at its end.
    /// </summary>
    /// <param name="text">The arrow.</param>
    /// <param name="tail">The expected first decoration.</param>
    /// <param name="head">The expected second decoration.</param>
    [Theory]
    [InlineData("<|--", "Extends", "None")]
    [InlineData("^--", "Extends", "None")]
    [InlineData("--|>", "None", "Extends")]
    [InlineData("--^", "None", "Extends")]
    [InlineData("*--", "Composition", "None")]
    [InlineData("--*", "None", "Composition")]
    [InlineData("o--", "Aggregation", "None")]
    [InlineData("--o", "None", "Aggregation")]
    [InlineData("x--", "NotNavigable", "None")]
    [InlineData("--x", "None", "NotNavigable")]
    [InlineData("<--", "Arrow", "None")]
    [InlineData("<_--", "Arrow", "None")]
    [InlineData("-->", "None", "Arrow")]
    [InlineData("--_>", "None", "Arrow")]
    [InlineData("<<--", "ArrowTriangle", "None")]
    [InlineData("-->>", "None", "ArrowTriangle")]
    [InlineData("0--", "Circle", "None")]
    [InlineData("--0", "None", "Circle")]
    [InlineData("@--", "CircleFill", "None")]
    [InlineData("--@", "None", "CircleFill")]
    [InlineData("0)--", "CircleConnect", "None")]
    [InlineData("--(0", "None", "CircleConnect")]
    [InlineData(")--", "Parenthesis", "None")]
    [InlineData("--(", "None", "Parenthesis")]
    [InlineData("#--", "Square", "None")]
    [InlineData("--#", "None", "Square")]
    [InlineData("+--", "Plus", "None")]
    [InlineData("--+", "None", "Plus")]
    [InlineData("--\\\\", "None", "HalfArrowUp")]
    [InlineData("--//", "None", "HalfArrowDown")]
    [InlineData("}--", "Crowfoot", "None")]
    [InlineData("--{", "None", "Crowfoot")]
    [InlineData("}o--", "CircleCrowfoot", "None")]
    [InlineData("--o{", "None", "CircleCrowfoot")]
    [InlineData("|o--", "CircleLine", "None")]
    [InlineData("--o|", "None", "CircleLine")]
    [InlineData("||--", "DoubleLine", "None")]
    [InlineData("--||", "None", "DoubleLine")]
    [InlineData("}|--", "LineCrowfoot", "None")]
    [InlineData("--|{", "None", "LineCrowfoot")]
    [InlineData("<||--", "Redefines", "None")]
    [InlineData("--||>", "None", "Redefines")]
    [InlineData("<|:--", "DefinedBy", "None")]
    [InlineData("--:|>", "None", "DefinedBy")]
    [InlineData("*--o", "Composition", "Aggregation")]
    [InlineData("<-->", "Arrow", "Arrow")]
    public void PlantUmlSvekSyntax_TryReadArrow_EveryDecoration_IsRead(string text, string tail, string head)
    {
        // Act
        var read = SvekSyntax.TryReadArrow(text, 0, out var arrow, out var end);

        // Assert
        Assert.True(read);
        Assert.Equal((Enum.Parse<LinkDecor>(tail), Enum.Parse<LinkDecor>(head), text.Length), (arrow.TailDecor, arrow.HeadDecor, end));
    }

    /// <summary>
    ///     Validates the length and the direction hint of arrows: the body length, one for left and right hints, at
    ///     least two for up and down hints, and the dotted and doubled bodies.
    /// </summary>
    /// <param name="text">The arrow.</param>
    /// <param name="length">The expected length.</param>
    /// <param name="direction">The expected hint.</param>
    /// <param name="dotted">Whether the body is dotted.</param>
    /// <param name="doubled">Whether the body is doubled.</param>
    [Theory]
    [InlineData("-", 1, "None", false, false)]
    [InlineData("--", 2, "None", false, false)]
    [InlineData("--->", 3, "None", false, false)]
    [InlineData("-up->", 2, "Up", false, false)]
    [InlineData("-u->", 2, "Up", false, false)]
    [InlineData("-up-->", 3, "Up", false, false)]
    [InlineData("-down->", 2, "Down", false, false)]
    [InlineData("-do->", 2, "Down", false, false)]
    [InlineData("-d->", 2, "Down", false, false)]
    [InlineData("-left->", 1, "Left", false, false)]
    [InlineData("-le->", 1, "Left", false, false)]
    [InlineData("-l->", 1, "Left", false, false)]
    [InlineData("-right-->", 1, "Right", false, false)]
    [InlineData("-ri->", 1, "Right", false, false)]
    [InlineData("-r->", 1, "Right", false, false)]
    [InlineData("..>", 2, "None", true, false)]
    [InlineData("==>", 2, "None", false, true)]
    public void PlantUmlSvekSyntax_TryReadArrow_LengthAndHint_AreRead(string text, int length, string direction, bool dotted, bool doubled)
    {
        // Act
        var read = SvekSyntax.TryReadArrow(text, 0, out var arrow, out _);

        // Assert
        Assert.True(read);
        Assert.Equal((length, Enum.Parse<LinkDirection>(direction), dotted, doubled), (arrow.Length, arrow.Direction, arrow.Dotted, arrow.Doubled));
    }

    /// <summary>
    ///     Validates that a text without an arrow body is not an arrow.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadArrow_NoBody_ReturnsFalse()
    {
        // Act
        var read = SvekSyntax.TryReadArrow("<|", 0, out _, out var end);

        // Assert
        Assert.False(read);
        Assert.Equal(0, end);
    }

    /// <summary>
    ///     Validates a full link statement: the ends, the decoration, a quoted end label, the color, and a label with its
    ///     direction mark.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadLink_FullStatement_IsRead()
    {
        // Act
        var read = SvekSyntax.TryReadLink(Statement("Circle *-- \"1..*\" Color #red : uses >"), out var link);

        // Assert
        Assert.True(read);
        Assert.Equal(new SvekName("Circle", SvekNameKind.Plain), link.From);
        Assert.Equal(new SvekName("Color", SvekNameKind.Plain), link.To);
        Assert.Equal((LinkDecor.Composition, LinkDecor.None), (link.Arrow.TailDecor, link.Arrow.HeadDecor));
        Assert.Equal((null, "1..*"), (link.TailLabel, link.HeadLabel));
        Assert.Equal(("uses", LinkLabelArrow.Forward), (link.Label, link.LabelArrow));
        Assert.True(SkinParams.TryParseColor("#red", out var red));
        Assert.Equal(red, link.Color);
        Assert.Equal(2, link.Location.Line);
    }

    /// <summary>
    ///     Validates that the ends are read as PlantUML's regular expression backtracks: a dash between two names is an
    ///     arrow, qualified and quoted names are kept whole.
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <param name="from">The expected first end.</param>
    /// <param name="to">The expected second end.</param>
    /// <param name="length">The expected length.</param>
    [Theory]
    [InlineData("A-B", "A", "B", 1)]
    [InlineData("A-->B", "A", "B", 2)]
    [InlineData("geo.Square --> Row", "geo.Square", "Row", 2)]
    [InlineData("a::b ..> c.d", "a::b", "c.d", 2)]
    [InlineData("\"Long name\" --> B", "Long name", "B", 2)]
    [InlineData("A --oB", "A", "oB", 2)]
    public void PlantUmlSvekSyntax_TryReadLink_Ends_AreReadWithBacktracking(string text, string from, string to, int length)
    {
        // Act
        var read = SvekSyntax.TryReadLink(Statement(text), out var link);

        // Assert
        Assert.True(read);
        Assert.Equal((from, to, length), (link.From.Text, link.To.Text, link.Arrow.Length));
    }

    /// <summary>
    ///     Validates the shorthand ends of the description and state diagrams, also before a label (a pseudo-state in brackets is not a qualifier).
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <param name="fromKind">The expected kind of the first end.</param>
    /// <param name="from">The expected text of the first end.</param>
    /// <param name="toKind">The expected kind of the second end.</param>
    /// <param name="to">The expected text of the second end.</param>
    [Theory]
    [InlineData("[Web] --> (Login)", "Bracketed", "Web", "Parenthesized", "Login")]
    [InlineData(":User: --> [*]", "Colon", "User", "Star", "")]
    [InlineData("[*] --> Idle", "Star", "", "Plain", "Idle")]
    [InlineData("A[H] --> [H*]", "History", "A", "DeepHistory", "")]
    [InlineData("A --> B[H*]", "Plain", "A", "DeepHistory", "B")]
    [InlineData("Online --> [*] : logout", "Plain", "Online", "Star", "")]
    [InlineData("Paused --> [H] : resume", "Plain", "Paused", "History", "")]
    [InlineData("[H*] --> Editor : back", "DeepHistory", "", "Plain", "Editor")]
    [InlineData("[Web] --> [Db] : sql", "Bracketed", "Web", "Bracketed", "Db")]
    [InlineData("() HTTP - [Web]", "Interface", "HTTP", "Bracketed", "Web")]
    [InlineData("[Web] --> () \"Message Queue\"", "Bracketed", "Web", "Interface", "Message Queue")]
    public void PlantUmlSvekSyntax_TryReadLink_ShorthandEnds_AreRead(string text, string fromKind, string from, string toKind, string to)
    {
        // Act
        var read = SvekSyntax.TryReadLink(Statement(text), out var link);

        // Assert
        Assert.True(read);
        Assert.Equal((new SvekName(from, Enum.Parse<SvekNameKind>(fromKind)), new SvekName(to, Enum.Parse<SvekNameKind>(toKind))), (link.From, link.To));
    }

    /// <summary>
    ///     Validates the business forms of the description diagrams: a slash after a use case or an actor is part of
    ///     the name (<c>(Pay)/</c>, <c>:Clerk:/</c>), and a name in brackets after an arrow is a qualifier only when
    ///     another name follows it.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadName_BusinessFormsAndQualifiers_AreRead()
    {
        // Act
        var i = 0;
        SvekSyntax.TryReadName("(Pay)/ x", ref i, out var pay);
        SvekSyntax.TryReadLink(Statement(":Clerk:/ --> (Pay)/"), out var business);
        SvekSyntax.TryReadLink(Statement("A [k] --> [q] B"), out var qualified);

        // Assert
        Assert.Equal((new SvekName("Pay", SvekNameKind.Parenthesized) { Business = true }, 6), (pay, i));
        Assert.Equal((true, true), (business.From.Business, business.To.Business));
        Assert.Equal(("k", "q", "B"), (qualified.TailQualifier, qualified.HeadQualifier, qualified.To.Text));
    }

    /// <summary>
    ///     Validates the styles written in brackets: a color, the line styles, a thickness, and <c>norank</c>.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadLink_BracketStyles_AreApplied()
    {
        // Act
        SvekSyntax.TryReadLink(Statement("A -[#red,dashed]-> B"), out var dashed);
        SvekSyntax.TryReadLink(Statement("A -[hidden]- B"), out var hidden);
        SvekSyntax.TryReadLink(Statement("A -[thickness=3]-> B"), out var thick);
        SvekSyntax.TryReadLink(Statement("A -[bold]up-> B"), out var bold);
        SvekSyntax.TryReadLink(Statement("A -[dotted,norank]-> B"), out var dotted);

        // Assert
        Assert.True(SkinParams.TryParseColor("#red", out var red));
        Assert.Equal(((LinkStyle?)LinkStyle.Dashed, (DemaConsulting.CanvasNet.Canvas.Rgba32?)red), (dashed.Arrow.Style, dashed.Arrow.Color));
        Assert.Equal(LinkStyle.Hidden, hidden.Arrow.ResolveStyle(false));
        Assert.Equal(3, thick.Arrow.Thickness);
        Assert.Equal(((LinkStyle?)LinkStyle.Bold, LinkDirection.Up), (bold.Arrow.Style, bold.Arrow.Direction));
        Assert.Equal(((LinkStyle?)LinkStyle.Dotted, true), (dotted.Arrow.Style, dotted.Arrow.NoRank));
        Assert.Null(dotted.Arrow.StyleText);
    }

    /// <summary>
    ///     Validates the line style of a body without brackets: dashed for a dotted body, bold for a doubled body when
    ///     the diagram draws it bold, and solid otherwise.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_ResolveStyle_Body_DecidesTheStyle()
    {
        // Arrange
        SvekSyntax.TryReadArrow("..>", 0, out var dotted, out _);
        SvekSyntax.TryReadArrow("==>", 0, out var doubled, out _);

        // Act / Assert
        Assert.Equal(LinkStyle.Dashed, dotted.ResolveStyle(false));
        Assert.Equal(LinkStyle.Bold, doubled.ResolveStyle(true));
        Assert.Equal(LinkStyle.Solid, doubled.ResolveStyle(false));
    }

    /// <summary>
    ///     Validates that an item in brackets that is not a style is a located syntax error.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadLink_UnknownStyle_ThrowsLocated()
    {
        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => SvekSyntax.TryReadLink(Statement("A -[wavy]-> B"), out _));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal((2, 3), (ex.Location.Line, ex.Location.Column));
        Assert.Contains("wavy", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.Hint);
    }

    /// <summary>
    ///     Validates that a color after a link that is not a color is a located syntax error.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadLink_BadColor_ThrowsLocated()
    {
        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => SvekSyntax.TryReadLink(Statement("A --> B #nocolor"), out _));

        // Assert
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 2, 9), (ex.Code, ex.Location.Line, ex.Location.Column));
    }

    /// <summary>
    ///     Validates that statements that are not links are not read as links.
    /// </summary>
    /// <param name="text">The statement.</param>
    [Theory]
    [InlineData("class A")]
    [InlineData("A")]
    [InlineData("note left of A : x")]
    [InlineData("A --> ")]
    [InlineData("A -> B extra words")]
    public void PlantUmlSvekSyntax_TryReadLink_NotALink_ReturnsFalse(string text)
    {
        // Act
        var read = SvekSyntax.TryReadLink(Statement(text), out _);

        // Assert
        Assert.False(read);
    }

    /// <summary>
    ///     Validates that the backtracking over the ends of a link is bounded: an end of
    ///     <see cref="SvekSyntax.MaxLinkEndParts"/> parts is read, an end of more parts makes the statement no link, and
    ///     a statement of 20,000 parts is no link (an end never takes more than the bound, so the reader does not try
    ///     every split of the statement).
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadLink_EndsOfManyParts_AreBounded()
    {
        // Arrange
        static string Name(char letter, int parts) => string.Join('-', Enumerable.Repeat(letter.ToString(), parts));
        var atBound = $"{Name('a', SvekSyntax.MaxLinkEndParts)} --> {Name('b', SvekSyntax.MaxLinkEndParts)}";
        var overBound = $"{Name('a', SvekSyntax.MaxLinkEndParts + 1)} --> B";
        var hostile = Name('a', 20_000) + " !";

        // Act
        var readAtBound = SvekSyntax.TryReadLink(Statement(atBound), out var link);
        var readOverBound = SvekSyntax.TryReadLink(Statement(overBound), out _);
        var readHostile = SvekSyntax.TryReadLink(Statement(hostile), out _);

        // Assert
        Assert.True(readAtBound);
        Assert.Equal(Name('a', SvekSyntax.MaxLinkEndParts), link.From.Text);
        Assert.Equal(Name('b', SvekSyntax.MaxLinkEndParts), link.To.Text);
        Assert.False(readOverBound);
        Assert.False(readHostile);
    }

    /// <summary>
    ///     Validates the parts of a link that are read but not drawn: the weight, the roles, the qualifiers, the URL, the
    ///     stereotype, and the middle decoration.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadLink_RarelyUsedParts_AreRead()
    {
        // Act
        SvekSyntax.TryReadLink(Statement("@2 A [key] /owner -0-> /item [k2] B [[http://example.com]] <<uses>> : x"), out var link);

        // Assert
        Assert.Equal(2, link.Weight);
        Assert.Equal(("owner", "item"), (link.TailRole, link.HeadRole));
        Assert.Equal(("key", "k2"), (link.TailQualifier, link.HeadQualifier));
        Assert.True(link.HasUrl);
        Assert.Equal(["uses"], link.Stereotypes);
        Assert.Equal("0", link.Arrow.Middle);
        Assert.Equal("x", link.Label);
    }

    /// <summary>
    ///     Validates that the label of a link is split as PlantUML splits it: quoted end labels around the text, and the
    ///     direction marks at its start or end.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_SplitLabel_EndLabelsAndMarks_AreTakenOut()
    {
        // Act
        var both = SvekSyntax.SplitLabel("\"1\" owns \"*\"", true);
        var first = SvekSyntax.SplitLabel("\"1\" owns", true);
        var second = SvekSyntax.SplitLabel("owns \"*\"", true);
        var kept = SvekSyntax.SplitLabel("\"1\" owns \"*\"", false);
        var backward = SvekSyntax.SplitLabel("< contains", true);
        var markOnly = SvekSyntax.SplitLabel(">", true);

        // Assert
        Assert.Equal(("owns", LinkLabelArrow.None, "1", "*"), both);
        Assert.Equal(("owns", LinkLabelArrow.None, "1", (string?)null), first);
        Assert.Equal(("owns", LinkLabelArrow.None, (string?)null, "*"), second);
        Assert.Equal((string?)null, kept.TailLabel);
        Assert.Equal(("contains", LinkLabelArrow.Backward), (backward.Label, backward.Arrow));
        Assert.Equal(((string?)null, LinkLabelArrow.Forward), (markOnly.Label, markOnly.Arrow));
    }

    /// <summary>
    ///     Validates that the stereotype-like parts of a label are written with guillemets as PlantUML writes them:
    ///     <c>&lt;&lt;include&gt;&gt;</c> and <c>&lt;&lt; extend &gt;&gt;</c> (one space taken away on each side), a
    ///     part with a sprite, and not a part over two lines, an empty part, or a part with another bracket.
    /// </summary>
    /// <param name="text">The label.</param>
    /// <param name="expected">The label with guillemets.</param>
    [Theory]
    [InlineData("<<include>>", "\u00ABinclude\u00BB")]
    [InlineData("calls << extend >> later", "calls \u00ABextend\u00BB later")]
    [InlineData("<<  two>>", "\u00AB two\u00BB")]
    [InlineData("<<<a>>", "<\u00ABa\u00BB")]
    [InlineData("<<x <&key> y>>", "\u00ABx <&key> y\u00BB")]
    [InlineData("<<a\\nb>>", "<<a\\nb>>")]
    [InlineData("<<>>", "<<>>")]
    [InlineData("<<a<b>>", "<<a<b>>")]
    [InlineData("a < b >> c", "a < b >> c")]
    public void PlantUmlSvekSyntax_Guillemets_ReplaceStereotypeParts(string text, string expected)
    {
        // Act
        var result = SvekSyntax.Guillemets(text);
        var (label, _, _, _) = SvekSyntax.SplitLabel(text, true);

        // Assert
        Assert.Equal(expected, result);
        Assert.Equal(expected.Trim(), label);
    }

    /// <summary>
    ///     Validates the identifier grammar: the ends of the identifiers at an index, longest first.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_IdentifierEnds_QualifiedName_LongestFirst()
    {
        // Act
        var ends = SvekSyntax.IdentifierEnds("a.b-c d", 0);
        var none = SvekSyntax.IdentifierEnds("-->", 0);

        // Assert
        Assert.Equal([5, 3, 1], ends);
        Assert.Empty(none);
    }

    /// <summary>
    ///     Validates the readers of names, aliases, stereotypes, and colors.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_NameAliasStereotypesColor_AreRead()
    {
        // Arrange
        const string Text = "\"Long name\" as L <<entity>> <<persistent>> #LightBlue {";
        var i = 0;

        // Act
        var named = SvekSyntax.TryReadName(Text, ref i, out var name);
        var aliased = SvekSyntax.TryReadAlias(Text, ref i, out var alias);
        var stereotypes = SvekSyntax.ReadStereotypes(Text, ref i);
        var color = SvekSyntax.ReadColor(Text, ref i, default);

        // Assert
        Assert.True(named && aliased);
        Assert.Equal((new SvekName("Long name", SvekNameKind.Quoted), "L"), (name, alias));
        Assert.Equal(["entity", "persistent"], stereotypes);
        Assert.True(SkinParams.TryParseColor("#LightBlue", out var lightBlue));
        Assert.Equal(lightBlue, color);
        Assert.Equal('{', Text[i]);
    }

    /// <summary>
    ///     Validates that a hash that is not followed by a color is a located syntax error.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_ReadColor_NotAColor_ThrowsLocated()
    {
        // Arrange
        var i = 0;
        var location = new PlantUmlLocation("d.puml", 4, 7);

        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => SvekSyntax.ReadColor("#nope", ref i, location));

        // Assert
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, location), (ex.Code, ex.Location));
    }

    /// <summary>
    ///     Validates every form of note statement: attached on one line and as a block, attached to the last entity,
    ///     floating on one line and as a block, and on a link.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadNote_EveryForm_IsRead()
    {
        // Arrange
        var statements = Statements(string.Join('\n',
            "note left of A : a note",
            "note bottom of \"B c\" #yellow",
            "line 1",
            "line 2",
            "end note",
            "note right : last",
            "note \"floating\" as N1",
            "note as N2",
            "block",
            "endnote",
            "note top on link #red : on link",
            "note on link",
            "x",
            "end note"));
        var notes = new List<SvekNoteStatement>();

        // Act
        var i = 0;
        while (i < statements.Count)
        {
            Assert.True(SvekSyntax.TryReadNote(statements, ref i, out var note));
            notes.Add(note);
            i++;
        }

        // Assert
        Assert.Equal(7, notes.Count);
        Assert.Equal(((NotePosition?)NotePosition.Left, "A", "a note"), (notes[0].Position, notes[0].Target?.Text, notes[0].Text));
        Assert.Equal(((NotePosition?)NotePosition.Bottom, "B c", "line 1\nline 2"), (notes[1].Position, notes[1].Target?.Text, notes[1].Text));
        Assert.NotNull(notes[1].Fill);
        Assert.Equal(((NotePosition?)NotePosition.Right, "last"), (notes[2].Position, notes[2].Text));
        Assert.Null(notes[2].Target);
        Assert.Equal(("N1", "floating", true), (notes[3].Alias, notes[3].Text, notes[3].IsFloating));
        Assert.Equal(("N2", "block"), (notes[4].Alias, notes[4].Text));
        Assert.Equal((true, (NotePosition?)NotePosition.Top, "on link"), (notes[5].OnLink, notes[5].Position, notes[5].Text));
        Assert.Equal(12, notes[5].Location.Line);
        Assert.Equal((true, (NotePosition?)NotePosition.Bottom, "x"), (notes[6].OnLink, notes[6].Position, notes[6].Text));
    }

    /// <summary>
    ///     Validates that a note block without its end is a located syntax error, and that a statement that does not
    ///     start with <c>note</c> is not a note.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadNote_UnclosedOrOther_ErrorOrFalse()
    {
        // Arrange
        var unclosed = Statements("note left of A\ntext");
        var other = Statements("notes are here");
        var i = 0;
        var j = 0;

        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => SvekSyntax.TryReadNote(unclosed, ref i, out _));
        var read = SvekSyntax.TryReadNote(other, ref j, out _);

        // Assert
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 2), (ex.Code, ex.Location.Line));
        Assert.Contains("end note", ex.Hint, StringComparison.Ordinal);
        Assert.False(read);
    }

    /// <summary>
    ///     Validates the page texts: a title, an aligned header and footer, a caption, and a legend block.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadPageText_EveryKind_IsRead()
    {
        // Arrange
        var statements = Statements("title The title\nleft header head\nright footer foot\ncaption cap\nlegend right\nleg\nendlegend\nclass A");
        var page = PageTexts.None;
        var read = new List<bool>();

        // Act
        var i = 0;
        while (i < statements.Count)
        {
            read.Add(SvekSyntax.TryReadPageText(statements, ref i, ref page, PlantUmlLimits.Default));
            i++;
        }

        // Assert
        Assert.Equal([true, true, true, true, true, false], read);
        Assert.Equal(("The title", TextAlign.Center), (page.Title!.Text, page.Title.Align));
        Assert.Equal(("head", TextAlign.Left), (page.Header!.Text, page.Header.Align));
        Assert.Equal(("foot", TextAlign.Right), (page.Footer!.Text, page.Footer.Align));
        Assert.Equal("cap", page.Caption!.Text);
        Assert.Equal(("leg", TextAlign.Right), (page.Legend!.Text, page.Legend.Align));
    }

    /// <summary>
    ///     Validates that a title block without its end is a located syntax error and that a title over the label limit
    ///     is a located limit error.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TryReadPageText_UnclosedOrTooLong_Throws()
    {
        // Arrange
        var unclosed = Statements("title\nline");
        var tooLong = Statements("title " + new string('x', 20));
        var limits = new PlantUmlLimits { MaxLabelLength = 10 };
        var page = PageTexts.None;
        var (i, j) = (0, 0);

        // Act
        var notClosed = Assert.Throws<PlantUmlSyntaxException>(() => SvekSyntax.TryReadPageText(unclosed, ref i, ref page, PlantUmlLimits.Default));
        var overLimit = Assert.Throws<PlantUmlSyntaxException>(() => SvekSyntax.TryReadPageText(tooLong, ref j, ref page, limits));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, notClosed.Code);
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, overLimit.Code);
        Assert.Contains("PlantUmlLimits.MaxLabelLength", overLimit.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the direction, scale, and <c>hide</c> and <c>show</c> statements.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_DirectionScaleHideShow_AreRead()
    {
        // Act
        var leftToRight = SvekSyntax.TryReadDirection("left  to right direction", out var lr);
        var topToBottom = SvekSyntax.TryReadDirection("top to bottom direction", out var tb);
        var other = SvekSyntax.TryReadDirection("left to right", out _);
        var hidden = SvekSyntax.TryReadHideShow(Statement("hide empty   Members"), out var hide);
        var shown = SvekSyntax.TryReadHideShow(Statement("show circle"), out var show);
        var bare = SvekSyntax.TryReadHideShow(Statement("hide"), out _);

        // Assert
        Assert.Equal((true, SvekRankDir.LeftToRight, true, SvekRankDir.TopToBottom, false), (leftToRight, lr, topToBottom, tb, other));
        Assert.Equal((2.0, 0.5), (SvekSyntax.ReadScale("scale 2"), SvekSyntax.ReadScale("scale 1/2")));
        Assert.Null(SvekSyntax.ReadScale("scale 200 width"));
        Assert.Null(SvekSyntax.ReadScale("scale 1000"));
        Assert.Equal((true, false, "empty members"), (hidden, hide.Show, hide.What));
        Assert.Equal((true, true, "circle"), (shown, show.Show, show.What));
        Assert.False(bare);
    }

    /// <summary>
    ///     Validates that the layout skin parameters are taken out of the parameters, a value that is not a number is
    ///     reported, and <c>linetype</c> is reported as not drawn.
    /// </summary>
    [Fact]
    public void PlantUmlSvekSyntax_TakeLayoutSettings_NodesepRanksepLinetype()
    {
        // Arrange
        var entries = new List<SkinParamEntry>
        {
            new("nodesep", "20", default),
            new("classBackgroundColor", "#red", default),
            new("RankSep", "70", default),
            new("ranksep", "abc", default),
            new("linetype", "ortho", default),
        };
        var reports = new List<PlantUmlPendingReport>();

        // Act
        var (nodeSep, rankSep) = SvekSyntax.TakeLayoutSettings(entries, reports);

        // Assert
        Assert.Equal(((double?)20.0, (double?)70.0), (nodeSep, rankSep));
        Assert.Equal(["classBackgroundColor"], entries.Select(e => e.Name));
        Assert.Equal(2, reports.Count);
    }
}
