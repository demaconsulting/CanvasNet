using DemaConsulting.CanvasNet.PlantUml.Description;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Description.DescriptionTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Description;

/// <summary>
///     Unit tests of <c>DescriptionParser</c>: the statements of description diagrams read into a model.
/// </summary>
public class DescriptionParserTests
{
    /// <summary>
    ///     Validates the declarations with a keyword: a name, a quoted display with an alias in either order, a plain
    ///     display before an alias, a bracketed display, and stereotypes before the alias.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_Declarations_ReadNamesAndDisplays()
    {
        // Act
        var model = Parse("component Web\ncomponent \"Payment Service\" as PS\ncomponent API as \"Public API\"\nnode Db as Store\n[Data Access] as DA\ncomponent C1 <<s>> as \"One\"\ndatabase \"Main DB\"");

        // Assert
        Assert.Equal(["Web", "PS", "API", "Store", "DA", "C1", "Main DB"], model.Elements.Select(e => e.Id));
        Assert.Equal(["Web", "Payment Service", "Public API", "Db", "Data Access", "One", "Main DB"], model.Elements.Select(e => e.Text));
        Assert.Equal(DescriptionSymbol.Node, ElementOf(model, "Store").Symbol);
        Assert.Equal(DescriptionSymbol.Component, ElementOf(model, "DA").Symbol);
        Assert.Equal(DescriptionSymbol.Database, ElementOf(model, "Main DB").Symbol);
        Assert.Equal(["s"], ElementOf(model, "C1").Stereotypes);
        Assert.Empty(model.Reports);
    }

    /// <summary>
    ///     Validates the declarations without a keyword and the shapes that override a keyword: <c>[C]</c> is a component,
    ///     <c>() I</c> an interface, <c>(U)</c> a use case and <c>(U)/</c> a business use case, <c>:A:</c> an actor and
    ///     <c>:A:/</c> a business actor, a name with <c>as</c> alone an actor, and the business keywords.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_Shorthands_GiveTheirSymbols()
    {
        // Act
        var model = Parse("[C]\n() I\n() \"Long I\" as LI\n(U)\n(V)/\n:A:\n:B:/\n\"Disp\" as X\nactor/ Biz\nusecase/ Buy\nnode [N2]");

        // Assert
        Assert.Equal(
            [
                DescriptionSymbol.Component, DescriptionSymbol.Interface, DescriptionSymbol.Interface, DescriptionSymbol.UseCase,
                DescriptionSymbol.BusinessUseCase, DescriptionSymbol.Actor, DescriptionSymbol.BusinessActor, DescriptionSymbol.Actor,
                DescriptionSymbol.BusinessActor, DescriptionSymbol.BusinessUseCase, DescriptionSymbol.Component,
            ],
            model.Elements.Select(e => e.Symbol));
        Assert.Equal(["C", "I", "LI", "U", "V", "A", "B", "X", "Biz", "Buy", "N2"], model.Elements.Select(e => e.Id));
        Assert.Equal("Long I", ElementOf(model, "LI").Text);
    }

    /// <summary>
    ///     Validates the use case forms: actors written <c>:name:</c> with an alias and as business actors, quoted
    ///     multi-line displays, use cases in parentheses with an alias and as business use cases, a display after
    ///     <c>as</c>, the <c>&lt;&lt;include&gt;&gt;</c> labels written with guillemets, and the actor and use case skin
    ///     parameters; every symbol is drawn, so nothing is reported.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_UseCaseForms_ReadNamesAndSymbols()
    {
        // Act
        var model = Parse(":Account Owner: as AO\n:Manager:/ as M\nactor \"Support\\nAgent\" as SA\nactor/ Auditor\n(Open Ticket) as OT\n(Archive)/ as AR\nusecase Escalate as \"Escalate\\nto Tier 2\"\nusecase/ Approve\nAO --> OT\nOT ..> Escalate : <<include>>\nApprove .> AR : << extend >>\nskinparam actorBackgroundColor #FFEEAA\nskinparam usecaseBorderColor #336699");

        // Assert
        Assert.Equal(["AO", "M", "SA", "Auditor", "OT", "AR", "Escalate", "Approve"], model.Elements.Select(e => e.Id));
        Assert.Equal(["Account Owner", "Manager", "Support\\nAgent", "Auditor", "Open Ticket", "Archive", "Escalate\\nto Tier 2", "Approve"], model.Elements.Select(e => e.Text));
        Assert.Equal(
            [
                DescriptionSymbol.Actor, DescriptionSymbol.BusinessActor, DescriptionSymbol.Actor, DescriptionSymbol.BusinessActor,
                DescriptionSymbol.UseCase, DescriptionSymbol.BusinessUseCase, DescriptionSymbol.UseCase, DescriptionSymbol.BusinessUseCase,
            ],
            model.Elements.Select(e => e.Symbol));
        Assert.Equal(["\u00ABinclude\u00BB", "\u00ABextend\u00BB"], model.Links.Skip(1).Select(l => l.Label));
        Assert.Equal((LinkStyle.Dashed, 1), (model.Links[2].Style, model.Links[2].Length));
        Assert.Equal(new Canvas.Rgba32(0xFF, 0xEE, 0xAA, 255), model.Theme[SkinElement.Actor].Fill);
        Assert.Equal(new Canvas.Rgba32(0x33, 0x66, 0x99, 255), model.Theme[SkinElement.UseCase].Line);
        Assert.Empty(model.Reports);
    }
    /// <summary>
    ///     Validates the multi-line descriptions: lines in brackets after a component, with text on the opening and the
    ///     closing lines, and a quoted display continued on the next lines; the description replaces the name.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_MultilineDescriptions_ReplaceTheName()
    {
        // Act
        var model = Parse("component C [\nfirst\nsecond\n]\ncomponent D [ start\nmore\nend ]\ncomponent E as \"top\nbottom\"");

        // Assert
        Assert.Equal("first\nsecond", ElementOf(model, "C").Text);
        Assert.Equal("start\nmore\nend", ElementOf(model, "D").Text);
        Assert.Equal("top\nbottom", ElementOf(model, "E").Text);
        Assert.Equal(3, model.NodeCount);
    }

    /// <summary>
    ///     Validates the stereotypes and colors of declarations: several stereotypes, a fill, a fill with line and text
    ///     colors, and a line color (<c>#line:color</c>).
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_StereotypesAndColors_AreRead()
    {
        // Act
        var model = Parse("component A <<s1>> <<s2>> #pink\ncomponent B #back:white;line:blue;text:green\nnode N #line:red\n[C] #LightBlue");

        // Assert
        Assert.Equal(["s1", "s2"], ElementOf(model, "A").Stereotypes);
        Assert.Equal(new Canvas.Rgba32(0xFF, 0xC0, 0xCB, 255), ElementOf(model, "A").Fill);
        Assert.Equal(
            (SkinTheme.White, new Canvas.Rgba32(0, 0, 0xFF, 255), new Canvas.Rgba32(0, 0x80, 0, 255)),
            (ElementOf(model, "B").Fill!.Value, ElementOf(model, "B").Line!.Value, ElementOf(model, "B").TextColor!.Value));
        Assert.Equal(new Canvas.Rgba32(0xFF, 0, 0, 255), ElementOf(model, "N").Line);
        Assert.Equal(new Canvas.Rgba32(0xAD, 0xD8, 0xE6, 255), ElementOf(model, "C").Fill);
    }

    /// <summary>
    ///     Validates the containers: nested containers qualify the ids of their members, an anonymous container has an
    ///     empty title, a container can be opened again, and a component can be a container.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_Containers_NestAndQualify()
    {
        // Act
        var model = Parse("package P {\nnode N {\n[A]\n}\n[B]\n}\nrectangle {\n[C]\n}\nnode N2 {\n[D]\n}\nnode N2 {\n[E]\n}\ncomponent K <<k>> {\n[F]\n}");

        // Assert
        Assert.Equal(["P", "P.N", "P.N.A", "P.B", "group\n1", "group\n1.C", "N2", "N2.D", "N2.E", "K", "K.F"], model.Elements.Select(e => e.Id));
        Assert.Equal(5, model.SubgraphCount);
        Assert.Equal(6, model.NodeCount);
        Assert.Same(ElementOf(model, "P.N"), ElementOf(model, "P.N.A").Container);
        Assert.Equal(2, ElementOf(model, "P.N.A").Depth);
        Assert.Equal(string.Empty, ElementOf(model, "group\n1").Text);
        Assert.Equal(DescriptionSymbol.Package, ElementOf(model, "P").Symbol);
        Assert.True(ElementOf(model, "K").IsContainer);
        Assert.Equal(["k"], ElementOf(model, "K").Stereotypes);
    }

    /// <summary>
    ///     Validates the resolution of the names of links: a short name from outside its container finds the element
    ///     when it is unique, a name inside a container finds the element of an outer scope, and a qualified name finds
    ///     its element.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_Names_ResolveInScope()
    {
        // Act
        var model = Parse("[Top]\nnode N {\ncomponent C\n[B] --> [Q]\n[B] --> [Top]\n}\n[C] --> D\n[Q] --> [R]\nN.C --> [E]");

        // Assert
        Assert.Equal(["N.B->N.Q", "N.B->Top", "N.C->D", "N.Q->R", "N.C->E"], model.Links.Select(l => l.From.Id + "->" + l.To.Id));
        Assert.Equal(DescriptionSymbol.Interface, ElementOf(model, "D").Symbol);
    }

    /// <summary>
    ///     Validates the names only written in links: an interface in a diagram without use cases or actors, an actor
    ///     in a diagram with one.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_BareNames_BecomeInterfacesOrActors()
    {
        // Act
        var component = Parse("[A] --> B\nB --> C");
        var actors = Parse("actor X\nX --> Y");
        var useCases = Parse("(U) --> Z");

        // Assert
        Assert.All(component.Elements.Where(e => e.Id != "A"), e => Assert.Equal(DescriptionSymbol.Interface, e.Symbol));
        Assert.Equal(DescriptionSymbol.Actor, ElementOf(actors, "Y").Symbol);
        Assert.Equal(DescriptionSymbol.Actor, ElementOf(useCases, "Z").Symbol);
    }

    /// <summary>
    ///     Validates the links: solid, dashed, and bold bodies, styles in brackets, decorations at both ends (the
    ///     sockets of required and provided interfaces included), end labels, a label with its direction mark, a
    ///     direction hint, and a hidden link.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_Links_ReadTheirArrows()
    {
        // Act
        var model = Parse("[A] --> [B] : solid\n[A] ..> [C]\n[A] ==> [D]\n[A] -[#red,dashed]-> [E]\n[A] \"1\" o-- \"many\" [F] : has >\n[A] -left-> [G]\n[A] )-- [H1]\n[A] --( [I]\n[A] 0)-- [J]\n[A] -[hidden]- [K]\n[A] -> [L]");
        var links = model.Links;

        // Assert
        Assert.Equal((LinkStyle.Solid, LinkDecor.Arrow, "solid"), (links[0].Style, links[0].HeadDecor, links[0].Label));
        Assert.Equal(LinkStyle.Dashed, links[1].Style);
        Assert.Equal(LinkStyle.Bold, links[2].Style);
        Assert.Equal((LinkStyle.Dashed, new Canvas.Rgba32(0xFF, 0, 0, 255)), (links[3].Style, links[3].Color!.Value));
        Assert.Equal((LinkDecor.Aggregation, "1", "many", "has", LinkLabelArrow.Forward), (links[4].TailDecor, links[4].TailLabel, links[4].HeadLabel, links[4].Label, links[4].LabelArrow));
        Assert.Equal(LinkDirection.Left, links[5].Direction);
        Assert.Equal(LinkDecor.Parenthesis, links[6].TailDecor);
        Assert.Equal(LinkDecor.Parenthesis, links[7].HeadDecor);
        Assert.Equal(LinkDecor.CircleConnect, links[8].TailDecor);
        Assert.Equal(LinkStyle.Hidden, links[9].Style);
        Assert.Equal(1, links[10].Length);
        Assert.Equal(11, model.EdgeCount);
        Assert.Empty(model.Reports);
    }

    /// <summary>
    ///     Validates the notes: attached to a side of an element (named plainly or in brackets), after a declaration,
    ///     on the last link, floating with a link to it, and on a container (drawn floating in it and reported).
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_Notes_AreAttached()
    {
        // Act
        var model = Parse("[Client] --> [Server]\nnote left of Client : browser\nnote right of [Server]\nhandles\nend note\nnote on link : link\nnote \"shared\" as N\nN .. Client\n[Cache]\nnote bottom : last\nnode Box {\n[In]\n}\nnote top of Box : box");

        // Assert
        var notes = model.Notes;
        Assert.Equal(["browser", "handles", "link", "shared", "last", "box"], notes.Select(n => n.Text));
        Assert.Equal(("Client", NotePosition.Left), (notes[0].Target!.Id, notes[0].Position));
        Assert.Equal("Server", notes[1].Target!.Id);
        Assert.Same(model.Links[0], notes[2].OnLink);
        Assert.True(notes[3].IsFloating);
        Assert.Same(notes[3], model.Links[1].From);
        Assert.Equal("Cache", notes[4].Target!.Id);
        Assert.Equal(("Box", true), (notes[5].Container!.Id, notes[5].IsFloating));
        Assert.Equal(1, model.EdgeCount);
        Assert.Equal(15, Assert.Single(model.Reports).Location.Line);
    }

    /// <summary>
    ///     Validates the common statements: the page texts, the direction, the scale, the layout skin parameters, and
    ///     the skin parameters of the description symbols.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_CommonStatements_AreRead()
    {
        // Act
        var model = Parse("title Services\nleft to right direction\nscale 2\nskinparam nodesep 40\nskinparam ranksep 50\nskinparam componentBackgroundColor #ABCDEF\nskinparam nodeBorderColor #FF0000\n[A] --> [B]");

        // Assert
        Assert.Equal("Services", model.Title);
        Assert.Equal(SvekRankDir.LeftToRight, model.RankDir);
        Assert.Equal(2, model.Scale);
        Assert.Equal((40, 50), (model.NodeSep!.Value, model.RankSep!.Value));
        Assert.Equal(new Canvas.Rgba32(0xAB, 0xCD, 0xEF, 255), model.Theme[SkinElement.Component].Fill);
        Assert.Equal(new Canvas.Rgba32(0xFF, 0, 0, 255), model.Theme[SkinElement.Node].Line);
        Assert.Empty(model.Reports);
    }

    /// <summary>
    ///     Validates that the statements that are read but not drawn are reported at their lines: a port and a link to
    ///     it, a middle decoration, <c>together</c>, <c>hide</c>, <c>remove</c>, a symbol without its own drawing, an
    ///     archimate element, a json block, a URL, and a stereotype of a link.
    /// </summary>
    [Fact]
    public void PlantUmlDescriptionParser_Parse_UnsupportedParts_AreReported()
    {
        // Act
        var model = Parse("node N {\nport p1\n}\np1 --> [X]\n[A] -(0)- [B]\ntogether {\n[T]\n}\nhide stereotype\nremove [A]\nprocess Art\narchimate #Business \"Biz\" as biz\njson J {\n\"a\": 1\n}\n[U] [[http://x]]\n[A] --> [B] <<uses>>");

        // Assert
        Assert.Equal([3, 5, 6, 7, 10, 11, 12, 13, 14, 17, 18], model.Reports.Select(r => r.Location.Line));
        Assert.All(model.Reports, r => Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, r.Code));
        Assert.Equal(2, model.Links.Count);
        Assert.Equal(DescriptionSymbol.Archimate, ElementOf(model, "biz").Symbol);
    }

    /// <summary>
    ///     Validates the located syntax errors: an element declared again with another symbol (also after a link named
    ///     it), an unclosed container, a closing brace without a container, a port outside a container, statements of
    ///     class and state diagrams, a start of a state diagram, a description after a colon, a one-line description in
    ///     brackets, a bad color, a line color or style written <c>##</c> (PlantUML refuses it in a description diagram),
    ///     an interface that holds elements, an ambiguous short name, an unclosed multi-line
    ///     description, a note alias that names an element, and a statement no description diagram has.
    /// </summary>
    /// <param name="body">The diagram body.</param>
    /// <param name="line">The line of the error.</param>
    [Theory]
    [InlineData("component A\nnode A", 3)]
    [InlineData("[A] --> B\ncomponent B", 3)]
    [InlineData("component A {\n[B]", 2)]
    [InlineData("[A]\n}", 3)]
    [InlineData("port P", 2)]
    [InlineData("[A]\nclass C", 3)]
    [InlineData("[A]\nstate S", 3)]
    [InlineData("[*] --> A", 2)]
    [InlineData("component C3 : desc", 2)]
    [InlineData("component C2 [single]", 2)]
    [InlineData("component A #notacolor", 2)]
    [InlineData("[A]\nnode N ##Blue", 3)]
    [InlineData("[A]\nnode N ##[dashed]Blue {\n}", 3)]
    [InlineData("[A]\nusecase U ##Blue", 3)]
    [InlineData("interface I {\n}", 2)]
    [InlineData("actor A {\n(U)\n}", 2)]
    [InlineData("node N {\n[B]\n}\nnode M {\n[B]\n}\n[B] --> [X]", 8)]
    [InlineData("component C [\nline", 2)]
    [InlineData("component A\nnote \"x\" as A", 3)]
    [InlineData("[A]\nwhat is this", 3)]
    [InlineData("[A]\nnode A {\n}", 3)]
    public void PlantUmlDescriptionParser_Parse_Errors_AreLocated(string body, int line)
    {
        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Parse(body));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.False(string.IsNullOrEmpty(ex.Hint));
    }

    /// <summary>
    ///     Validates that a link to a pseudo-state of a state diagram (the start or end <c>[*]</c>, a history
    ///     <c>S[H]</c>, or a deep history <c>S[H*]</c>) fails at its statement with an error that names that pseudo-state
    ///     as written.
    /// </summary>
    /// <param name="body">The diagram body.</param>
    /// <param name="mark">The pseudo-state the error names.</param>
    [Theory]
    [InlineData("usecase U\nU --> [*]", "'[*]'")]
    [InlineData("usecase U\nU --> S[H]", "'S[H]'")]
    [InlineData("usecase U\nU --> S[H*]", "'S[H*]'")]
    public void PlantUmlDescriptionParser_Parse_PseudoStateEnd_NamesTheMark(string body, string mark)
    {
        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Parse(body));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(3, ex.Location.Line);
        Assert.Contains(mark, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the limits fail closed at the statement that crosses them: the elements and notes, the links,
    ///     a text length, and the nesting of containers.
    /// </summary>
    /// <param name="limit">The limit.</param>
    /// <param name="body">The diagram body.</param>
    /// <param name="line">The line that crosses the limit.</param>
    [Theory]
    [InlineData("MaxNodes", "[A]\n[B]\nnote \"n\" as N", 4)]
    [InlineData("MaxEdges", "[A] --> [B]\n[B] --> [A]\n[A] --> [A]", 4)]
    [InlineData("MaxLabelLength", "[A]\ncomponent \"a name far too long\" as L", 3)]
    [InlineData("MaxNestingDepth", "node A {\nnode B {\nnode C {\n}\n}\n}", 4)]
    public void PlantUmlDescriptionParser_Limits_FailClosedAtTheLine(string limit, string body, int line)
    {
        // Arrange
        var limits = limit switch
        {
            "MaxNodes" => new PlantUmlLimits { MaxNodes = 2 },
            "MaxEdges" => new PlantUmlLimits { MaxEdges = 2 },
            "MaxLabelLength" => new PlantUmlLimits { MaxLabelLength = 10 },
            _ => new PlantUmlLimits { MaxNestingDepth = 2 },
        };

        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Parse(body, limits));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.Contains(limit, ex.Message, StringComparison.Ordinal);
    }
}
