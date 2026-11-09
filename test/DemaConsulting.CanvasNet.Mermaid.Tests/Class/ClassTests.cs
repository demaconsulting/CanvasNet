using DemaConsulting.CanvasNet.Mermaid.Class;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Class;

/// <summary>
///     Subsystem tests of the Class subsystem: the parser and the model working together, from a whole text to the
///     database facts, the layout data and the located errors.
/// </summary>
public class ClassTests
{
    /// <summary>
    ///     Gets the names of the original class fixtures.
    /// </summary>
    public static TheoryData<string> Fixtures
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var path in Directory.GetFiles(FixtureFolder, "*.mmd").Order(StringComparer.Ordinal))
            {
                data.Add(Path.GetFileName(path));
            }

            return data;
        }
    }

    /// <summary>
    ///     Gets the folder of the class fixtures.
    /// </summary>
    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "class");

    /// <summary>
    ///     Validates that every original class fixture parses into classes whose layout data has a node for each class.
    /// </summary>
    /// <param name="name">The fixture file name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Class_Parse_Fixture_GivesClassesAndLayoutData(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(FixtureFolder, name));

        // Act
        var model = ClassFacts.Parse(text, name);
        var data = model.GetData(ClassFacts.DefaultConfig(text));

        // Assert
        Assert.NotEmpty(model.Classes);
        Assert.Equal(model.Classes.Count, data.Nodes.Count(node => node.ClassBox is not null));
        Assert.All(data.Edges, edge => Assert.Equal(1, edge.MinLen));
    }

    /// <summary>
    ///     Validates a whole text with frontmatter and comments: classes, members, relations, a namespace, a note and the
    ///     accessibility texts, and the layout data they map to.
    /// </summary>
    [Fact]
    public void Class_Parse_WholeText_GivesModelAndLayoutData()
    {
        // Arrange
        const string Text = "---\ntitle: Shop\n---\nclassDiagram\n    %% a comment\n    accTitle: The shop\n    accDescr: Orders and lines\n" +
            "    direction LR\n    namespace Sales {\n        class Order {\n            +int id\n            +total() double\n        }\n    }\n" +
            "    class Line~T~\n    Order \"1\" *-- \"many\" Line : holds\n    note for Order \"Paid orders only\"\n";

        // Act
        var model = ClassFacts.Parse(Text);
        var data = model.GetData(ClassFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(("LR", "The shop", "Orders and lines"), (model.Direction, model.AccessibleTitle, model.AccessibleDescription));
        Assert.Equal(["Order", "Line"], model.Classes.Select(c => c.Id));
        Assert.Equal("Sales", model.Classes[0].Parent);
        Assert.Equal(["Sales", "Order", "Line", "note0"], data.Nodes.Select(n => n.Id));
        Assert.Equal(["edgeNote0", "id_Order_Line_1"], data.Edges.Select(e => e.Id));
        Assert.Equal(("1", "many", "holds"), (data.Edges[1].StartLabelRight, data.Edges[1].EndLabelLeft, data.Edges[1].Label));
        Assert.Equal("Line&lt;T&gt;", data.Nodes[2].ClassBox!.Text);
    }

    /// <summary>
    ///     Validates that the text fails closed: an error of Mermaid's class database is a located syntax error with
    ///     Mermaid's message, and no partial model is returned.
    /// </summary>
    [Fact]
    public void Class_Parse_DatabaseError_FailsClosedWithLocation()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram\n    <<interface>> Missing\n", "c.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MermaidLocation("c.mmd", 2, 19), ex.Location);
        Assert.Contains("Cannot read properties of undefined (reading 'annotations')", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a grammar error is located in the original text, frontmatter lines included, with jison's expected
    ///     tokens.
    /// </summary>
    [Fact]
    public void Class_Parse_GrammarErrorAfterFrontmatter_IsLocatedInTheOriginalText()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("---\ntitle: T\n---\nclassDiagram\n    note direction\n", "c.mmd"));

        // Assert
        Assert.Equal(new MermaidLocation("c.mmd", 5, 10), ex.Location);
        Assert.Equal(["STR"], ex.Expected);
    }

    /// <summary>
    ///     Validates that the legacy class id (selected by a <c>class.defaultRenderer</c> other than
    ///     <c>dagre-wrapper</c>) parses the same way and keeps its Mermaid type for the layout data.
    /// </summary>
    [Fact]
    public void Class_Parse_LegacyClassId_KeepsItsDiagramType()
    {
        // Arrange
        const string Text = "classDiagram\n    Alpha --> Beta\n";
        var source = Preprocessor.Run(Text, null, MermaidLimits.Default);
        var detected = DiagramDetector.Detect(source.Code, new DetectorSettings(null, "dagre-d3", null));

        // Act
        var model = ClassParser.Parse(source, detected, MermaidLimits.Default);
        var data = model.GetData(ClassFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(("class", "class"), (model.MermaidId, data.DiagramType));
        Assert.Single(data.Edges);
    }
}
