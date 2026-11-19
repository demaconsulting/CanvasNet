using DemaConsulting.CanvasNet.PlantUml.Class;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Class.ClassTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Class;

/// <summary>
///     Unit tests for the <c>ClassParser</c> unit: declarations, bodies and members, qualified names and packages,
///     relationships, notes, hide and show, the reports of what is not drawn, the located errors, and the limits.
/// </summary>
public class ClassParserTests
{
    /// <summary>
    ///     Validates that every declaration keyword gives its kind.
    /// </summary>
    /// <param name="keyword">The keyword.</param>
    /// <param name="kind">The expected kind.</param>
    [Theory]
    [InlineData("class", "Class")]
    [InlineData("abstract class", "AbstractClass")]
    [InlineData("abstract", "AbstractClass")]
    [InlineData("static class", "Class")]
    [InlineData("interface", "Interface")]
    [InlineData("enum", "Enum")]
    [InlineData("annotation", "Annotation")]
    [InlineData("entity", "Entity")]
    [InlineData("exception", "Exception")]
    [InlineData("struct", "Struct")]
    [InlineData("protocol", "Protocol")]
    [InlineData("metaclass", "Metaclass")]
    [InlineData("stereotype", "Stereotype")]
    [InlineData("record", "Record")]
    [InlineData("dataclass", "DataClass")]
    [InlineData("object", "Object")]
    public void PlantUmlClassParser_Declaration_ReadsTheKind(string keyword, string kind)
    {
        // Act
        var model = Parse($"{keyword} Thing");

        // Assert
        var entity = Assert.Single(model.Entities);
        Assert.Equal(("Thing", "Thing", Enum.Parse<ClassKind>(kind)), (entity.Id, entity.Display, entity.Kind));
    }

    /// <summary>
    ///     Validates a declaration with every part: a display with <c>as</c>, generic parameters, stereotypes with a
    ///     spot, colors, and an empty body; and the other order of the display and the name.
    /// </summary>
    [Fact]
    public void PlantUmlClassParser_Declaration_ReadsDisplayGenericStereotypesAndColors()
    {
        // Act
        var model = Parse("class \"Long name\" as L<K, List<V>> << (S,#FF7700) special >> <<other>> #pink;line:red;text:blue {}\nclass R as \"Right\"");

        // Assert
        var entity = model.Entities[0];
        Assert.Equal(("L", "Long name", "K, List<V>"), (entity.Id, entity.Display, entity.Generic));
        Assert.Equal(["special", "other"], entity.Stereotypes);
        Assert.Equal(('S', new Canvas.Rgba32(0xFF, 0x77, 0x00, 255)), (entity.SpotCharacter, entity.SpotColor));
        Assert.Equal((new Canvas.Rgba32(0xFF, 0xC0, 0xCB, 255), new Canvas.Rgba32(0xFF, 0, 0, 255), new Canvas.Rgba32(0, 0, 0xFF, 255)), (entity.Fill, entity.Line, entity.TextColor));
        Assert.Equal(("R", "Right"), (model.Entities[1].Id, model.Entities[1].Display));
    }

    /// <summary>
    ///     Validates that a body keeps its member and separator lines in order, that <c>Name : text</c> adds a member
    ///     (creating the class), and that an object reads its fields the same way.
    /// </summary>
    [Fact]
    public void PlantUmlClassParser_Body_KeepsMembersAndSeparators()
    {
        // Act
        var model = Parse("class A {\n  - x : int\n  --\n  + run() : void\n}\nA : # y : long\nB : field\nobject o {\n  k = 1\n}\no : j = 2");

        // Assert
        Assert.Equal(["- x : int", "--", "+ run() : void", "# y : long"], model.Entities.Single(e => e.Id == "A").Body);
        Assert.Equal(["field"], model.Entities.Single(e => e.Id == "B").Body);
        Assert.Equal(["k = 1", "j = 2"], model.Entities.Single(e => e.Id == "o").Body);
    }

    /// <summary>
    ///     Validates that a map reads its entries, and that a link from an entry is drawn from the map and reported.
    /// </summary>
    [Fact]
    public void PlantUmlClassParser_Map_ReadsEntriesAndReportsEntryLinks()
    {
        // Act
        var model = Parse("object target\nmap M {\n  a => 1\n  +b => two words\n  c *--> target\n}");

        // Assert
        var map = model.Entities.Single(e => e.Id == "M");
        Assert.Equal(ClassKind.Map, map.Kind);
        Assert.Equal([("a", "1"), ("+b", "two words")], map.Entries);
        var link = Assert.Single(model.Links);
        Assert.Equal(("M", "target", LinkDecor.Composition), (link.From.Id, link.To.Id, link.TailDecor));
        Assert.Contains(model.Reports, r => r.Message.Contains("map entry", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates qualified names: a dotted name creates its packages, a name in a package is relative to it (unless
    ///     it starts with a package at the top), a short name finds its entity, and <c>set namespaceSeparator none</c>
    ///     keeps dots in names.
    /// </summary>
    [Fact]
    public void PlantUmlClassParser_QualifiedNames_CreateAndResolvePackages()
    {
        // Act
        var model = Parse("class a.b.C\npackage p {\n  class x.Q\n  Q --> R\n}\npackage p.s {\n}\nC --> D");
        var flat = Parse("set namespaceSeparator none\nclass a.b.C");

        // Assert
        Assert.Equal(["a", "a.b", "p", "p.x", "p.s"], model.Packages.Select(p => p.Id));
        Assert.Equal(["a.b.C", "p.x.Q", "p.R", "D"], model.Entities.Select(e => e.Id));
        Assert.Equal(("C", "a.b"), (model.Entities[0].Display, model.Entities[0].Package!.Id));
        Assert.Equal([("p.x.Q", "p.R"), ("a.b.C", "D")], model.Links.Select(l => (l.From.Id, l.To.Id)));
        Assert.Equal(("a.b.C", 0), (flat.Entities.Single().Id, flat.Packages.Count));
    }

    /// <summary>
    ///     Validates packages and namespaces: nested, with a title, a symbol chosen by a stereotype, other stereotypes,
    ///     and a color.
    /// </summary>
    [Fact]
    public void PlantUmlClassParser_Packages_ReadSymbolsStereotypesAndColors()
    {
        // Act
        var model = Parse("package \"Data Layer\" as data <<Node>> <<tier>> #EEEEEE {\n  namespace store {\n    class Row\n  }\n}\npackage empty\npackage folder <<Rectangle>> {\n}");

        // Assert
        var data = model.Packages[0];
        Assert.Equal(("data", "Data Layer", ClusterSymbol.Node), (data.Id, data.Title, data.Symbol));
        Assert.Equal(["tier"], data.Stereotypes);
        Assert.Equal(new Canvas.Rgba32(0xEE, 0xEE, 0xEE, 255), data.Fill);
        Assert.Equal(("data.store", data), (model.Packages[1].Id, model.Packages[1].Package));
        Assert.Equal("data.store.Row", model.Entities.Single().Id);
        Assert.Equal(ClusterSymbol.Rectangle, model.Packages.Single(p => p.Id == "folder").Symbol);
        Assert.Contains(model.Packages, p => p.Id == "empty");
    }

    /// <summary>
    ///     Validates relationships: the decorations at both ends, the style, the length and the direction hint, the
    ///     end labels and the label with its direction mark, the color, and <c>extends</c> and <c>implements</c>.
    /// </summary>
    [Fact]
    public void PlantUmlClassParser_Links_ReadDecorationsStylesAndLabels()
    {
        // Act
        var model = Parse("A \"1\" *-- \"many\" B : holds >\nC .up.|> D\nE -[#red,bold]-> F\nG -[hidden]- H\nI ---o J\nclass K extends L implements M, N");

        // Assert
        var links = model.Links;
        Assert.Equal((LinkDecor.Composition, LinkDecor.None, "1", "many", "holds", LinkLabelArrow.Forward), (links[0].TailDecor, links[0].HeadDecor, links[0].TailLabel, links[0].HeadLabel, links[0].Label, links[0].LabelArrow));
        Assert.Equal((LinkDecor.Extends, LinkStyle.Dashed, LinkDirection.Up), (links[1].HeadDecor, links[1].Style, links[1].Direction));
        Assert.Equal((LinkStyle.Bold, new Canvas.Rgba32(0xFF, 0, 0, 255)), (links[2].Style, links[2].Color));
        Assert.Equal(LinkStyle.Hidden, links[3].Style);
        Assert.Equal((3, LinkDecor.Aggregation), (links[4].Length, links[4].HeadDecor));
        Assert.Equal([("L", "K", LinkStyle.Solid), ("M", "K", LinkStyle.Dashed), ("N", "K", LinkStyle.Dashed)], links.Skip(5).Select(l => (l.From.Id, l.To.Id, l.Style)));
        Assert.All(links.Skip(5), l => Assert.Equal(LinkDecor.Extends, l.TailDecor));
    }

    /// <summary>
    ///     Validates that the parts of a link that are not drawn are reported at the statement and dropped: a crow's
    ///     foot, roles, qualifiers, a middle decoration, <c>norank</c>, a link stereotype, a link to a member, and an
    ///     association class.
    /// </summary>
    [Fact]
    public void PlantUmlClassParser_Links_ReportWhatIsNotDrawn()
    {
        // Act
        var model = Parse("A }o--|| B\nA /r1 --> /r2 B\nA [k] --> B\nA -0- B\nA -[norank]-> B\nA --> B <<uses>>\nA::x --> B\n(A, B) .. C");

        // Assert
        Assert.Equal([2, 2, 3, 4, 5, 6, 7, 8, 9], model.Reports.Select(r => r.Location.Line));
        Assert.All(model.Reports, r => Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, r.Code));
        Assert.Equal((LinkDecor.None, LinkDecor.None), (model.Links[0].TailDecor, model.Links[0].HeadDecor));
        Assert.Equal(7, model.Links.Count);
        Assert.Equal("A", model.Links[6].From.Id);
    }

    /// <summary>
    ///     Validates notes: on a side of a named entity, of the last declared entity, on the last link, and floating
    ///     with an alias that links connect.
    /// </summary>
    [Fact]
    public void PlantUmlClassParser_Notes_AttachToEntitiesLinksOrFloat()
    {
        // Act
        var model = Parse("class A\nnote left : last\nclass B\nnote top of A : named\nA --> B\nnote on link : on it\nnote \"free\" as N\nN .. B\nnote as M #yellow\n  block\nend note");

        // Assert
        var notes = model.Notes;
        Assert.Equal(("last", "A", NotePosition.Left), (notes[0].Text, notes[0].Target!.Id, notes[0].Position));
        Assert.Equal(("named", "A", NotePosition.Top), (notes[1].Text, notes[1].Target!.Id, notes[1].Position));
        Assert.Same(model.Links[0], notes[2].OnLink);
        Assert.Equal(("N", true), (notes[3].Id, notes[3].IsFloating));
        Assert.Same(notes[3], model.Links[1].From);
        Assert.Equal(("M", "block"), (notes[4].Id, notes[4].Text));
        Assert.NotNull(notes[4].Fill);
    }

    /// <summary>
    ///     Validates hide and show: the rules are kept in order with their gender and the empty flag, and the forms
    ///     that are not applied are reported.
    /// </summary>
    [Fact]
    public void PlantUmlClassParser_HideShow_ReadsRulesAndReportsTheRest()
    {
        // Act
        var model = Parse("hide empty members\nshow fields\nhide interface methods\nhide <<tag>> circle\nhide Foo stereotype\nhide @unlinked\nhide private members\nhide Foo\nclass Foo");

        // Assert
        Assert.Equal(
            [
                new ClassHideRule(false, null, true, ClassPortion.Members), new ClassHideRule(true, null, false, ClassPortion.Fields),
                new ClassHideRule(false, "interface", false, ClassPortion.Methods), new ClassHideRule(false, "<<tag>>", false, ClassPortion.Circle),
                new ClassHideRule(false, "Foo", false, ClassPortion.Stereotype),
            ],
            model.Rules);
        Assert.Equal([7, 8, 9], model.Reports.Select(r => r.Location.Line));
    }

    /// <summary>
    ///     Validates the common statements: the direction, the scale, the layout skin parameters, and the page texts.
    /// </summary>
    [Fact]
    public void PlantUmlClassParser_CommonStatements_AreRead()
    {
        // Act
        var model = Parse("left to right direction\nscale 2\nskinparam nodesep 50\nskinparam ranksep 70\nskinparam classBackgroundColor #FFEEDD\nskinparam roundedness 4\ntitle T\nclass A");

        // Assert
        Assert.Equal((SvekRankDir.LeftToRight, 2.0, 50.0, 70.0), (model.RankDir, model.Scale, model.NodeSep!.Value, model.RankSep!.Value));
        Assert.Equal(new Canvas.Rgba32(0xFF, 0xEE, 0xDD, 255), model.Theme[SkinElement.Class].Fill);
        Assert.Equal("T", model.Title);
        Assert.Equal(PlantUmlDiagnosticCodes.SkinParamIgnored, Assert.Single(model.Reports).Code);
    }

    /// <summary>
    ///     Validates the statements that are recognized and reported, not drawn.
    /// </summary>
    /// <param name="statement">The statement.</param>
    [Theory]
    [InlineData("together {\nclass X\n}")]
    [InlineData("remove A")]
    [InlineData("allowmixing")]
    [InlineData("skin rose")]
    [InlineData("scale max 200 width")]
    [InlineData("json J {\n\"a\": {\"b\": 1}\n}")]
    [InlineData("diamond D")]
    [InlineData("+class V")]
    [InlineData("class W ##[dashed]blue")]
    public void PlantUmlClassParser_RecognizedStatements_AreReported(string statement)
    {
        // Act
        var model = Parse("class A\n" + statement);

        // Assert
        var report = Assert.Single(model.Reports);
        Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, report.Code);
        Assert.Equal(3, report.Location.Line);
    }

    /// <summary>
    ///     Validates that malformed statements fail with a located syntax error and a hint.
    /// </summary>
    /// <param name="body">The diagram body.</param>
    /// <param name="line">The line of the error (1 is the first body line).</param>
    /// <param name="hint">A fragment of the hint.</param>
    [Theory]
    [InlineData("class A {\n+ x", 1, "}")]
    [InlineData("package P {\nclass A", 1, "}")]
    [InlineData("class A\n}", 2, "package")]
    [InlineData("class A\nactor B", 2, "allowmixing")]
    [InlineData("class A\nA --> (B)", 2, "allowmixing")]
    [InlineData("class A #notacolor", 1, "color")]
    [InlineData("class A\nwhat is this", 2, "class")]
    [InlineData("class A\nnote on link : x", 2, "link")]
    [InlineData("class A <T> extends", 1, "extends")]
    [InlineData("class A xyz", 1, "member")]
    [InlineData("map M {\nnot an entry\n}", 2, "key => value")]
    [InlineData("package A\nclass A", 2, "name")]
    public void PlantUmlClassParser_MalformedStatements_AreLocatedErrors(string body, int line, string hint)
    {
        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Parse("class First\n" + body));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(line + 2, ex.Location.Line);
        Assert.Contains(hint, ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the structural limits: elements and notes, links, label lengths, and package nesting (implicit
    ///     packages included) fail closed at the line that crosses them.
    /// </summary>
    /// <param name="limit">The limit.</param>
    /// <param name="value">Its value.</param>
    /// <param name="body">The diagram body.</param>
    /// <param name="line">The line that crosses it.</param>
    [Theory]
    [InlineData("MaxNodes", 2, "class A\nclass B\nnote \"n\" as N", 3)]
    [InlineData("MaxNodes", 2, "A --> B\nB --> C", 2)]
    [InlineData("MaxEdges", 1, "A --> B\nB --> C", 2)]
    [InlineData("MaxLabelLength", 5, "class A {\nverylongmember\n}", 2)]
    [InlineData("MaxLabelLength", 5, "A --> B : longlabel", 1)]
    [InlineData("MaxNestingDepth", 2, "class a.b.c.D", 1)]
    [InlineData("MaxNestingDepth", 1, "package p {\npackage q {\n}\n}", 2)]
    public void PlantUmlClassParser_Limits_FailClosedAtTheLine(string limit, int value, string body, int line)
    {
        // Arrange
        var limits = limit switch
        {
            "MaxNodes" => new PlantUmlLimits { MaxNodes = value },
            "MaxEdges" => new PlantUmlLimits { MaxEdges = value },
            "MaxLabelLength" => new PlantUmlLimits { MaxLabelLength = value },
            _ => new PlantUmlLimits { MaxNestingDepth = value },
        };

        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Parse(body, limits));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(line + 1, ex.Location.Line);
        Assert.Contains(limit, ex.Message, StringComparison.Ordinal);
    }
}
