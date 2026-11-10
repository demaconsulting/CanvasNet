using DemaConsulting.CanvasNet.Mermaid.Class;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Class;

/// <summary>
///     Unit tests of <see cref="ClassModel"/>: the class database operations (members, annotations, relations,
///     namespaces, styles and links), its located errors and limits, and the mapping to the layout data.
/// </summary>
public class ClassModelTests
{
    /// <summary>Member texts and the display text, drawn text and classifier style Mermaid's <c>ClassMember</c> gives them.</summary>
    public static TheoryData<string, string, string, string> MemberCases => new()
    {
        { "+String name", "+String name", "\\+String name", string.Empty },
        { "-int count$", "-int count", "\\-int count", "text-decoration:underline;" },
        { "String label*", "String label", "String label", "font-style:italic;" },
        { "#List~int~ items", "#List<int> items", "\\#List&lt;int&gt; items", string.Empty },
        { "+draw()*", "+draw()", "\\+draw()", "font-style:italic;" },
        { "+create()$ Shape", "+create() : Shape", "\\+create() : Shape", "text-decoration:underline;" },
        { "someAbstract()* int", "someAbstract() : int", "someAbstract() : int", "font-style:italic;" },
        { "~describe() String$", "~describe() : String", "\\~describe() : String", "text-decoration:underline;" },
        { "+map(Function~T,R~ f) Box~R~", "+map(Function<T,R> f) : Box<R>", "\\+map(Function&lt;T,R&gt; f) : Box&lt;R&gt;", string.Empty },
    };

    /// <summary>Generic texts and the TypeScript syntax Mermaid's <c>parseGenericTypes</c> gives them.</summary>
    public static TheoryData<string, string> GenericCases => new()
    {
        { "List~int~", "List<int>" },
        { "List~List~int~~", "List<List<int>>" },
        { "Map~K, V~", "Map<K, V>" },
        { "~T", "~T" },
        { "Plain", "Plain" },
    };

    /// <summary>Links and what Mermaid's <c>strict</c> URL check makes of them.</summary>
    public static TheoryData<string, string?> UrlCases => new()
    {
        { "google.com", "google.com" },
        { "https://Example.com", "https://example.com/" },
        { "/relative/page", "/relative/page" },
        { "mailto:someone@example.com", "mailto:someone@example.com" },
        { "javascript:alert(1)", "about:blank" },
        { "  ", null },
    };

    /// <summary>
    ///     Validates that a member text is split into visibility, name, parameters, return type and classifier as Mermaid
    ///     does.
    /// </summary>
    /// <param name="member">The member text.</param>
    /// <param name="display">The expected display text.</param>
    /// <param name="text">The expected drawn text.</param>
    /// <param name="cssStyle">The expected classifier style.</param>
    [Theory]
    [MemberData(nameof(MemberCases))]
    public void ClassModel_AddMember_MemberText_SplitsLikeMermaid(string member, string display, string text, string cssStyle)
    {
        // Arrange
        var model = CreateModel();

        // Act
        model.AddMember("Shape", member, default);

        // Assert
        var shape = Assert.Single(model.Classes);
        var added = Assert.Single(shape.Members.Concat(shape.Methods));
        Assert.Equal((display, text, cssStyle), (added.DisplayText, added.Text, added.CssStyle));
    }

    /// <summary>
    ///     Validates that a member in angle quotes is an annotation and an empty member adds nothing.
    /// </summary>
    [Fact]
    public void ClassModel_AddMember_AnnotationAndEmptyText_AreNotMembers()
    {
        // Arrange
        var model = CreateModel();

        // Act
        model.AddMember("Shape", "<<interface>>", default);
        model.AddMember("Shape", "   ", default);

        // Assert
        var shape = Assert.Single(model.Classes);
        Assert.Equal(["interface"], shape.Annotations);
        Assert.Empty(shape.Members);
        Assert.Empty(shape.Methods);
    }

    /// <summary>
    ///     Validates that a member with a closing parenthesis that Mermaid's method expression cannot read fails as
    ///     Mermaid does, located at the statement.
    /// </summary>
    [Fact]
    public void ClassModel_AddMember_UnreadableMethod_ThrowsLocatedError()
    {
        // Arrange
        var model = CreateModel();
        var location = new MermaidLocation("c.mmd", 3, 5);

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => model.AddMember("Shape", "(x)", location));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, location), (ex.Code, ex.Location));
        Assert.Contains("Cannot read properties of undefined (reading 'startsWith')", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an annotation for a class that does not exist fails as Mermaid does, located at the class name.
    /// </summary>
    [Fact]
    public void ClassModel_AddAnnotation_UnknownClass_ThrowsLocatedError()
    {
        // Arrange
        var model = CreateModel();
        var location = new MermaidLocation("c.mmd", 2, 15);

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => model.AddAnnotation("Missing", "interface", location));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, location), (ex.Code, ex.Location));
        Assert.Contains("Cannot read properties of undefined (reading 'annotations')", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that generic tildes become angle brackets as Mermaid's <c>parseGenericTypes</c> writes them.
    /// </summary>
    /// <param name="input">The text.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [MemberData(nameof(GenericCases))]
    public void ClassModel_ParseGenericTypes_Text_FollowsMermaid(string input, string expected)
    {
        // Act
        var actual = ClassModel.ParseGenericTypes(input);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates that classes get Mermaid's generic types, texts and DOM ids, numbered per model from 0.
    /// </summary>
    [Fact]
    public void ClassModel_AddClass_GenericName_SplitsTypeAndNumbersDomIds()
    {
        // Arrange
        var model = CreateModel();

        // Act
        model.AddClass("Box~T~", default);
        model.AddClass("Box", default);
        model.AddClass("Pair", default);

        // Assert
        Assert.Equal(
            [("Box", "T", "Box&lt;T&gt;", "classId-Box-0"), ("Pair", string.Empty, "Pair", "classId-Pair-1")],
            model.Classes.Select(c => (c.Id, c.Type, c.Text, c.DomId)));
    }

    /// <summary>
    ///     Validates that a dotted namespace creates its implicit ancestors, and that a nested declaration is qualified by
    ///     the enclosing namespace.
    /// </summary>
    [Fact]
    public void ClassModel_AddNamespace_DottedAndNested_CreatesQualifiedNamespaces()
    {
        // Arrange
        var model = CreateModel();

        // Act
        var outer = model.AddNamespace("Company.Project", null, default);
        var inner = model.AddNamespace("Module", "Modules", default);
        model.PopNamespace();
        model.PopNamespace();

        // Assert
        Assert.Equal(("Company.Project", "Company.Project.Module"), (outer, inner));
        Assert.Equal(
            [("Company", false, (string?)null), ("Company.Project", true, "Company"), ("Company.Project.Module", true, "Company.Project")],
            model.Namespaces.Select(n => (n.Id, n.Explicit, n.Parent)));
        Assert.Equal("Modules", model.Namespaces[2].Label);
        Assert.Equal(["Company.Project"], model.Namespaces[0].Children);
    }

    /// <summary>
    ///     Validates that a style class applies only to the classes that already carry it when it is defined, and keeps
    ///     its color styles as text styles.
    /// </summary>
    [Fact]
    public void ClassModel_DefineClass_AppliesToClassesThatCarryIt()
    {
        // Arrange
        var model = CreateModel();
        model.AddClass("Hot", default);
        model.AddClass("Later", default);
        model.SetCssClass("Hot", "warm", default);

        // Act
        model.DefineClass(["warm"], ["fill:#f96,color:#036"], default);
        model.SetCssClass("Later", "warm", default);

        // Assert
        Assert.Equal(["fill:#f96", "color:#036"], model.Classes[0].Styles);
        Assert.Empty(model.Classes[1].Styles);
        Assert.Equal(["bgFill:#f96,color:#036"], Assert.Single(model.StyleClasses).TextStyles);
    }

    /// <summary>
    ///     Validates that a CSS class for an id that starts with a digit matches no class, because of Mermaid's prefix.
    /// </summary>
    [Fact]
    public void ClassModel_SetCssClass_DigitId_MatchesNoClass()
    {
        // Arrange
        var model = CreateModel();
        model.AddClass("1Alpha", default);
        model.AddClass("Beta", default);

        // Act
        model.SetCssClass("1Alpha,Beta", "hot", default);

        // Assert
        Assert.Equal(["default", "default hot"], model.Classes.Select(c => c.CssClasses));
    }

    /// <summary>
    ///     Validates that a class listed again in a <c>cssClass</c> statement gets the CSS class again, as in Mermaid, and
    ///     that CSS classes that grow longer than <see cref="MermaidLimits.MaxLabelLength"/> that way are refused, located
    ///     at the statement.
    /// </summary>
    [Fact]
    public void ClassModel_SetCssClass_RepeatedIds_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 20 };

        // Act: default and seven " hot" are 35 characters; default and three are 19
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram\nclass A\ncssClass \"A,A,A,A,A,A,A\" hot\n", null, limits));
        var accepted = ClassFacts.Parse("classDiagram\nclass A\ncssClass \"A,A,A\" hot\n", null, limits);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 3, 1), ex.Location);
        Assert.Contains("a label is 23 characters long", ex.Message, StringComparison.Ordinal);
        Assert.Equal("default hot hot hot", Assert.Single(accepted.Classes).CssClasses);
    }

    /// <summary>
    ///     Validates that a style class listed again in a <c>classDef</c> statement gets the styles again, as in Mermaid,
    ///     and that the styles of a style class or of a class that grow longer than
    ///     <see cref="MermaidLimits.MaxLabelLength"/> (joined with semicolons) that way are refused, located at the
    ///     statement.
    /// </summary>
    [Fact]
    public void ClassModel_DefineClass_RepeatedNames_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 20 };

        // Act: three copies of the 7-character style are 23 characters; two are 15
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram\nclassDef c,c,c a:b,c:d\n", null, limits));
        var applied = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram\nclass A:::c\nclassDef c a:b,c:d\nclassDef c e:f,g:h\nclassDef c i:j,k:l\n", null, limits));
        var accepted = ClassFacts.Parse("classDiagram\nclass A:::c\nclassDef c,c a:b,c:d\n", null, limits);

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, new MermaidLocation(null, 2, 1)), (ex.Code, ex.Location));
        Assert.Contains("a label is 23 characters long", ex.Message, StringComparison.Ordinal);
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, new MermaidLocation(null, 5, 1)), (applied.Code, applied.Location));
        Assert.Equal(["a:b,c:d", "a:b,c:d"], Assert.Single(accepted.StyleClasses).Styles);
        Assert.Equal(["a:b", "c:d", "a:b", "c:d"], Assert.Single(accepted.Classes).Styles);
    }

    /// <summary>
    ///     Validates that the styles of <c>style</c> statements accumulate on a class, as in Mermaid, and that styles that
    ///     grow longer than <see cref="MermaidLimits.MaxLabelLength"/> (joined with semicolons) are refused, located at the
    ///     statement that makes them too long.
    /// </summary>
    [Fact]
    public void ClassModel_SetCssStyle_RepeatedStyles_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 20 };
        const string Style = "style A a:b,c:d\n";

        // Act: three statements are 23 characters of styles; two are 15
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram\nclass A\n" + Style + Style + Style, null, limits));
        var accepted = ClassFacts.Parse("classDiagram\nclass A\n" + Style + Style, null, limits);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 5, 1), ex.Location);
        Assert.Equal(["a:b", "c:d", "a:b", "c:d"], Assert.Single(accepted.Classes).Styles);
    }

    /// <summary>
    ///     Validates that links are checked as Mermaid's <c>strict</c> security level does.
    /// </summary>
    /// <param name="link">The link as written.</param>
    /// <param name="expected">The expected link, or null for none.</param>
    [Theory]
    [MemberData(nameof(UrlCases))]
    public void ClassModel_FormatUrl_Link_IsCheckedLikeMermaid(string link, string? expected)
    {
        // Act
        var actual = ClassModel.FormatUrl(link);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates that a lollipop end with no end on the other side becomes an interface node, and both lollipop ends
    ///     do not.
    /// </summary>
    [Fact]
    public void ClassModel_AddRelation_Lollipop_AddsInterfaceNode()
    {
        // Arrange
        var model = CreateModel();

        // Act
        model.AddRelation(new ClassRelationInput("Ignition", "Engine", "none", "none", new(ClassModel.Lollipop, null, ClassModel.SolidLine), null), default);
        model.AddRelation(new ClassRelationInput("Left", "Right", "none", "none", new(ClassModel.Lollipop, ClassModel.Lollipop, ClassModel.SolidLine), null), default);

        // Assert
        var face = Assert.Single(model.Interfaces);
        Assert.Equal(("interface0", "Ignition", "Engine"), (face.Id, face.Label, face.ClassId));
        Assert.Equal(["Engine", "Left", "Right"], model.Classes.Select(c => c.Id));
        Assert.Equal(("interface0", "Engine"), (model.Relations[0].Id1, model.Relations[0].Id2));
    }

    /// <summary>
    ///     Validates that relations beyond <see cref="MermaidLimits.MaxEdges"/> and nodes beyond
    ///     <see cref="MermaidLimits.MaxNodes"/> are refused, located.
    /// </summary>
    [Fact]
    public void ClassModel_Limits_TooManyRelationsOrNodes_ThrowLimitExceeded()
    {
        // Arrange
        var model = new ClassModel(new MermaidLimits { MaxEdges = 1, MaxNodes = 3 }, default, "classDiagram");
        var location = new MermaidLocation("c.mmd", 4, 1);
        model.AddRelation(new ClassRelationInput("A", "B", "none", "none", new(null, null, ClassModel.SolidLine), null), default);
        model.AddClass("C", default);

        // Act
        var edges = Assert.Throws<MermaidSyntaxException>(() =>
            model.AddRelation(new ClassRelationInput("A", "C", "none", "none", new(null, null, ClassModel.SolidLine), null), location));
        var nodes = Assert.Throws<MermaidSyntaxException>(() => model.AddNote("text", null, location));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, location), (edges.Code, edges.Location));
        Assert.Contains("MaxEdges", edges.Message, StringComparison.Ordinal);
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, location), (nodes.Code, nodes.Location));
        Assert.Contains("MaxNodes", nodes.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a namespace counts against <see cref="MermaidLimits.MaxNodes"/> together with the classes, as a
    ///     flowchart subgraph does: a namespace that adds one too many is refused at its name.
    /// </summary>
    [Fact]
    public void ClassModel_AddNamespace_TooManyNamespaces_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNodes = 3 };
        const string Text = "classDiagram\n    class A\n    namespace One {\n        class B\n    }\n" +
            "    namespace Two {\n        class C\n    }\n";

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse(Text, null, limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 6, 15), ex.Location);
        Assert.Contains("more than 3 nodes and subgraphs (MermaidLimits.MaxNodes)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the implicit ancestors of a dotted namespace count against <see cref="MermaidLimits.MaxNodes"/>
    ///     too, whether they are drawn or not.
    /// </summary>
    [Fact]
    public void ClassModel_AddNamespace_ImplicitAncestors_CountAgainstMaxNodes()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNodes = 2 };
        const string Text = "classDiagram\n    namespace Geo.Plane.Shapes {\n        class A\n    }\n";

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse(Text, null, limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 2, 15), ex.Location);
        Assert.Contains("MermaidLimits.MaxNodes", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that classes and namespaces, implicit ancestors included, that together reach
    ///     <see cref="MermaidLimits.MaxNodes"/> exactly are accepted.
    /// </summary>
    [Fact]
    public void ClassModel_Limits_NodesAndNamespacesAtTheLimit_AreAccepted()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNodes = 3 };

        // Act
        var model = ClassFacts.Parse("classDiagram\n    namespace Geo.Plane {\n        class A\n    }\n", null, limits);

        // Assert
        Assert.Equal((1, 2), (model.NodeCount, model.SubgraphCount));
    }

    /// <summary>
    ///     Validates that the qualified id of a nested namespace, which is its drawn label when
    ///     <c>class.hierarchicalNamespaces</c> is false, is refused when longer than
    ///     <see cref="MermaidLimits.MaxLabelLength"/>, located at its name, while one at the limit is accepted.
    /// </summary>
    [Fact]
    public void ClassModel_AddNamespace_LongQualifiedId_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 10 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse(
            "classDiagram\nnamespace Alpha {\n    namespace Bravo {\n        class C\n    }\n}\n", null, limits));
        var accepted = ClassFacts.Parse("classDiagram\nnamespace Alpha {\n    namespace Beta {\n        class C\n    }\n}\n", null, limits);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 3, 15), ex.Location);
        Assert.Contains("a label is 11 characters long", ex.Message, StringComparison.Ordinal);
        Assert.Contains("MermaidLimits.MaxLabelLength", ex.Message, StringComparison.Ordinal);
        Assert.Equal("Alpha.Beta", accepted.Namespaces[^1].Id);
    }

    /// <summary>
    ///     Validates that the dotted parts of a namespace name count against <see cref="MermaidLimits.MaxNestingDepth"/>,
    ///     because each part nests one more namespace: a name with one part too many is refused, located at its name,
    ///     while one at the limit is accepted.
    /// </summary>
    [Fact]
    public void ClassModel_AddNamespace_DottedIdTooDeep_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNestingDepth = 3 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse(
            "classDiagram\n    namespace A.B.C.D {\n        class X\n    }\n", null, limits));
        var accepted = ClassFacts.Parse("classDiagram\n    namespace A.B.C {\n        class X\n    }\n", null, limits);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 2, 15), ex.Location);
        Assert.Contains("namespaces are nested deeper than 3 levels (MermaidLimits.MaxNestingDepth)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(["A", "A.B", "A.B.C"], accepted.Namespaces.Select(n => n.Id));
    }

    /// <summary>
    ///     Validates that the drawn name of a labeled generic class, the label with the type of the name
    ///     (<c>Labels&lt;Type&gt;</c>), is refused when longer than <see cref="MermaidLimits.MaxLabelLength"/>, although
    ///     the name and the label each fit, while one at the limit is accepted.
    /// </summary>
    [Fact]
    public void ClassModel_SetClassLabel_LabelWithGenericType_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 10 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ClassFacts.Parse("classDiagram\nclass A~Type~[\"Labels\"]\n", null, limits));
        var accepted = ClassFacts.Parse("classDiagram\nclass A~Type~[\"Name\"]\n", null, limits);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 2, 7), ex.Location);
        Assert.Contains("a label is 12 characters long", ex.Message, StringComparison.Ordinal);
        Assert.Equal("Name<Type>", Assert.Single(accepted.Classes).Text);
    }

    /// <summary>
    ///     Validates that namespaces chained by class names (namespace <c>Ni</c> holds a class named like namespace
    ///     <c>N(i+1)</c>, which nests that namespace in it) count against <see cref="MermaidLimits.MaxNestingDepth"/> in the
    ///     layout data: a class four namespaces deep is refused with a limit of 3, located at the class statement that
    ///     gives the namespace its parent, while three namespaces deep are accepted.
    /// </summary>
    [Fact]
    public void ClassModel_GetData_NamespacesChainedByClassNames_ThrowsLimitExceeded()
    {
        // Arrange: namespace Ni holds class N(i+1) on line 3 + 3i; the last holds X
        static string Chain(int count) =>
            "classDiagram\n" + string.Concat(Enumerable.Range(0, count).Select(i => $"namespace N{i} {{\nclass {(i == count - 1 ? "X" : $"N{i + 1}")}\n}}\n"));
        var limits = new MermaidLimits { MaxNestingDepth = 3 };
        var refused = ClassFacts.Parse(Chain(4), null, limits);
        var accepted = ClassFacts.Parse(Chain(3), null, limits);

        // Act
        var ex = Assert.Throws<MermaidRenderException>(() => refused.GetData(ClassFacts.DefaultConfig(Chain(4))));
        var data = accepted.GetData(ClassFacts.DefaultConfig(Chain(3)));

        // Assert: X, in N3, is inside N3, N2, N1 and N0
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 12, 7), ex.Location);
        Assert.Contains("namespaces are nested deeper than 3 levels (MermaidLimits.MaxNestingDepth)", ex.Message, StringComparison.Ordinal);
        Assert.Equal("N2", data.Nodes.Last(node => node.Id == "X").ParentId);
    }

    /// <summary>
    ///     Validates that namespaces nested in each other through class names (a cycle) are left to the layout, which
    ///     fails on them located as before, rather than being taken for a nesting too deep.
    /// </summary>
    [Fact]
    public void ClassModel_GetData_NamespaceCycle_IsLeftToTheLayout()
    {
        // Arrange
        const string Text = "classDiagram\nnamespace A {\nclass B\n}\nnamespace B {\nclass A\n}\n";
        var model = ClassFacts.Parse(Text);
        var document = MermaidDocument.Parse(Text);

        // Act
        var data = model.GetData(ClassFacts.DefaultConfig(Text));
        var ex = Assert.Throws<MermaidRenderException>(() => document.Measure(null, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(4, data.Nodes.Count);
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, ex.Code);
    }

    /// <summary>
    ///     Validates the layout data of a relation: Mermaid's id, the markers, a minimum length of 1, the label and the
    ///     cardinalities as terminal labels, with <c>none</c> as no label.
    /// </summary>
    [Fact]
    public void ClassModel_GetData_Relation_MapsMarkersAndTerminalLabels()
    {
        // Arrange
        const string Text = "classDiagram\n    Customer \"1\" *--> Order : places\n";
        var model = ClassFacts.Parse(Text);

        // Act
        var data = model.GetData(ClassFacts.DefaultConfig(Text));

        // Assert
        var edge = Assert.Single(data.Edges);
        Assert.Equal(("id_Customer_Order_1", "composition", "dependency", 1), (edge.Id, edge.ArrowTypeStart, edge.ArrowTypeEnd, edge.MinLen));
        Assert.Equal(("places", "1", string.Empty, "solid"), (edge.Label, edge.StartLabelRight, edge.EndLabelLeft, edge.Pattern));
        Assert.Equal(("classDiagram", "TB"), (data.DiagramType, data.Direction));
        Assert.Equal(["aggregation", "extension", "composition", "dependency", "lollipop"], data.Markers);
    }

    /// <summary>
    ///     Validates the layout data of notes, namespaces and classes: the note link to an existing class, the namespace
    ///     cluster with its padding, and the class box texts.
    /// </summary>
    [Fact]
    public void ClassModel_GetData_NotesNamespacesAndClasses_MapToNodes()
    {
        // Arrange
        const string Text = "classDiagram\n    namespace Shop {\n        class Order {\n            +int id\n            +total()$ double\n        }\n    }\n" +
            "    note for Order \"kept\"\n    note for Missing \"orphan\"\n";
        var model = ClassFacts.Parse(Text);

        // Act
        var data = model.GetData(ClassFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(["Shop", "Order", "note0", "note1"], data.Nodes.Select(n => n.Id));
        Assert.Equal(("rect", true, 16.0), (data.Nodes[0].Shape, data.Nodes[0].IsGroup, data.Nodes[0].Padding));
        var box = data.Nodes[1].ClassBox!;
        Assert.Equal(("Shop", "classId-Order-0"), (data.Nodes[1].ParentId, data.Nodes[1].DomId));
        Assert.Equal(["\\+int id"], box.Members.Select(m => m.Text));
        Assert.Equal([("\\+total() : double", "text-decoration:underline;")], box.Methods.Select(m => (m.Text, m.CssStyle)));
        var link = Assert.Single(data.Edges);
        Assert.Equal(("edgeNote0", "note0", "Order", "dotted", 1), (link.Id, link.Start, link.End, link.Pattern, link.MinLen));
        Assert.Equal(["text-align: left", "white-space: nowrap", "fill: #fff5ad", "stroke: #aaaa33"], data.Nodes[2].CssStyles);
        Assert.Equal((false, false, true), (data.Nodes[0].TrustedStyles, data.Nodes[1].TrustedStyles, data.Nodes[2].TrustedStyles));
    }

    /// <summary>
    ///     Validates that a note's class is looked up by the name as written, as Mermaid's getData does: a note for the
    ///     generic <c>Box~T~</c> (the class is <c>Box</c>) is not linked, while notes for <c>Box</c> and for the
    ///     backticked <c>My Class</c> are.
    /// </summary>
    /// <param name="text">The class diagram text.</param>
    /// <param name="expectedEnd">The class the note is linked to, or null for no link.</param>
    [Theory]
    [InlineData("classDiagram\n    class Box~T~\n    note for Box~T~ \"n\"\n", null)]
    [InlineData("classDiagram\n    class Box~T~\n    note for Box \"n\"\n", "Box")]
    [InlineData("classDiagram\n    class `My Class`\n    note for `My Class` \"n\"\n", "My Class")]
    public void ClassModel_GetData_NoteTarget_IsLookedUpAsWritten(string text, string? expectedEnd)
    {
        // Arrange
        var model = ClassFacts.Parse(text);

        // Act
        var data = model.GetData(ClassFacts.DefaultConfig(text));

        // Assert
        (string, string)[] expected = expectedEnd is null ? [] : [("note0", expectedEnd)];
        Assert.Equal(expected, data.Edges.Select(edge => (edge.Start, edge.End)));
    }

    /// <summary>
    ///     Validates that the ends of a relation are the class ids, as Mermaid's addRelation sets them: a generic end
    ///     <c>Box~T~</c> is the class <c>Box</c>, and a backticked end <c>My Class</c> is the class <c>My Class</c>.
    /// </summary>
    [Fact]
    public void ClassModel_AddRelation_GenericAndBacktickedEnds_AreTheClassIds()
    {
        // Arrange
        const string Text = "classDiagram\n    Box~T~ --> Other\n    Box --> Other2\n    `My Class` --> B\n";
        var model = ClassFacts.Parse(Text);

        // Act
        var data = model.GetData(ClassFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(["Box", "Other", "Other2", "My Class", "B"], data.Nodes.Select(node => node.Id));
        Assert.Equal(
            [("id_Box_Other_1", "Box", "Other"), ("id_Box_Other2_2", "Box", "Other2"), ("id_My Class_B_3", "My Class", "B")],
            data.Edges.Select(edge => (edge.Id, edge.Start, edge.End)));
    }

    /// <summary>
    ///     Validates that note theme variables a directive sets to a number or a boolean are stringified as Mermaid's
    ///     template strings do, instead of failing.
    /// </summary>
    /// <param name="fill">The <c>noteBkgColor</c> value as JSON.</param>
    /// <param name="stroke">The <c>noteBorderColor</c> value as JSON.</param>
    /// <param name="expectedFill">The expected fill text.</param>
    /// <param name="expectedStroke">The expected stroke text.</param>
    [Theory]
    [InlineData("5", "true", "5", "true")]
    [InlineData("false", "1.5", "false", "1.5")]
    public void ClassModel_GetData_NonStringNoteThemeVariables_AreStringified(string fill, string stroke, string expectedFill, string expectedStroke)
    {
        // Arrange
        var text = $"%%{{init: {{'themeVariables': {{'noteBkgColor': {fill}, 'noteBorderColor': {stroke}}}}}}}%%\nclassDiagram\n    note \"x\"\n";
        var model = ClassFacts.Parse(text);

        // Act
        var data = model.GetData(ClassFacts.DefaultConfig(text));

        // Assert
        var note = Assert.Single(data.Nodes);
        Assert.Equal(["text-align: left", "white-space: nowrap", $"fill: {expectedFill}", $"stroke: {expectedStroke}"], note.CssStyles);
    }

    /// <summary>
    ///     Validates the metadata the model reports: the classes, relations and namespaces are counted (notes, lollipop
    ///     interfaces and note links are not), the direction is the last statement's, the accessible title is kept,
    ///     and the grammar declares no title.
    /// </summary>
    [Fact]
    public void ClassModel_Metadata_CountsClassesRelationsAndNamespaces()
    {
        // Arrange
        const string Text = "classDiagram\n    accTitle: Shop\n    direction RL\n    namespace Shop {\n        class Order\n    }\n" +
            "    Order --> Item\n    Item --() Priced\n    note for Order \"n\"\n";

        // Act
        var model = ClassFacts.Parse(Text);

        // Assert
        Assert.Equal((2, 2, 1), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
        Assert.Equal(("RL", "Shop"), (model.Direction, model.AccTitle));
        Assert.Null(model.Title);
        Assert.True(model.GetData(ClassFacts.DefaultConfig(Text)).Nodes.Single(node => node.Id == "interface0").TrustedStyles);
    }

    /// <summary>
    ///     Validates that with <c>class.hierarchicalNamespaces</c> false only the declared namespaces are clusters,
    ///     labeled with their qualified id, and classes move to the nearest declared namespace.
    /// </summary>
    [Fact]
    public void ClassModel_GetData_CompactNamespaces_KeepDeclaredOnly()
    {
        // Arrange
        const string Text = "---\nconfig:\n  class:\n    hierarchicalNamespaces: false\n---\nclassDiagram\n    namespace A.B.C {\n        class Deep\n    }\n    namespace A {\n        class Top\n    }\n";
        var model = ClassFacts.Parse(Text);

        // Act
        var data = model.GetData(ClassFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(
            [("A", "A", (string?)null), ("A.B.C", "A.B.C", null), ("Deep", "Deep", "A.B.C"), ("Top", "Top", "A")],
            data.Nodes.Select(n => (n.Id, n.Label, n.ParentId)));
    }

    /// <summary>
    ///     Validates that the namespace count follows the namespaces the layout data keeps: every namespace with
    ///     hierarchical namespaces, and only the declared ones without.
    /// </summary>
    /// <param name="hierarchical">Whether namespaces are hierarchical.</param>
    /// <param name="expected">The expected number of namespaces.</param>
    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 2)]
    public void ClassModel_SubgraphCount_HierarchicalNamespaces_CountsTheKeptNamespaces(bool hierarchical, int expected)
    {
        // Arrange
        var model = new ClassModel(MermaidLimits.Default, default, "classDiagram") { HierarchicalNamespaces = hierarchical };

        // Act
        model.AddNamespace("A.B.C", null, default);
        model.PopNamespace();
        model.AddNamespace("D", null, default);
        model.PopNamespace();

        // Assert
        Assert.Equal(expected, model.SubgraphCount);
        Assert.Equal(4, model.Namespaces.Count);
    }

    /// <summary>
    ///     Validates that the class spacing comes from the state section, as Mermaid's class renderer reads it.
    /// </summary>
    [Fact]
    public void ClassModel_GetData_Spacing_FollowsTheStateSection()
    {
        // Arrange
        const string Text = "---\nconfig:\n  state:\n    nodeSpacing: 80\n    rankSpacing: 70\n---\nclassDiagram\n    class A\n";
        var model = ClassFacts.Parse(Text);

        // Act
        var data = model.GetData(ClassFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal((80.0, 70.0), (data.NodeSpacing, data.RankSpacing));
    }

    /// <summary>
    ///     Validates that a look other than classic and a layout other than dagre are reported in order, located at their
    ///     keys, with hints.
    /// </summary>
    [Fact]
    public void ClassModel_ReportUnsupported_LookAndLayout_AreReportedInOrder()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: handDrawn\n  layout: elk\n---\nclassDiagram\n    class A\n";
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        ClassModel.ReportUnsupported(ClassFacts.DefaultConfig(Text), sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Equal(
            [(MermaidDiagnosticCodes.LookUnsupported, 3), (MermaidDiagnosticCodes.LayoutUnsupported, 4)],
            reports.Select(r => (r.Feature, r.Location.Line)));
        Assert.All(reports, r => Assert.NotNull(r.Hint));
    }

    /// <summary>
    ///     Validates the class renderer's frame of the drawing: the frontmatter title is drawn centered above the drawing
    ///     at the state section's title margin, and the view box is the drawn bounds grown by the padding of 8.
    /// </summary>
    [Fact]
    public void ClassModel_Layout_Title_IsAboveTheDrawingInsideThePaddedViewBox()
    {
        // Arrange
        var document = MermaidDocument.Parse("---\ntitle: Shapes\n---\nclassDiagram\n    class Shape\n");

        // Act
        var scene = document.LayoutScene(null, TestContext.Current.CancellationToken);

        // Assert
        var title = Assert.IsType<SceneText>(Assert.Single(SceneQuery.OfRole(scene, "title")));
        Assert.Equal((SceneTextAnchor.Middle, -25.0), (title.Anchor, title.Baseline));
        Assert.True(scene.ViewBox.Encloses(title.Box));
        var box = SceneQuery.Nodes(scene)["classId-Shape-0"].Box;
        Assert.Equal(box.X - 8, scene.ViewBox.X, 3);
        Assert.Equal(box.X + box.Width + 8, scene.ViewBox.X + scene.ViewBox.Width, 3);
        Assert.Equal(box.Y + box.Height + 8, scene.ViewBox.Y + scene.ViewBox.Height, 3);
    }

    /// <summary>
    ///     Creates an empty model with the default limits.
    /// </summary>
    /// <returns>The model.</returns>
    private static ClassModel CreateModel() => new(MermaidLimits.Default, default, "classDiagram");
}
