using DemaConsulting.CanvasNet.PlantUml.Class;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Class.ClassTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Class;

/// <summary>
///     Unit tests for the <c>ClassModel</c> unit: the facts, the hide and show rules, the lowering for the Svek engine,
///     the layout of a call, and the member rules of the elements.
/// </summary>
public class ClassModelTests
{
    /// <summary>
    ///     Validates the facts: the entities (notes not counted), the links that do not touch a note (hidden links
    ///     counted), and the packages that hold something.
    /// </summary>
    [Fact]
    public void PlantUmlClassModel_Facts_CountEntitiesLinksAndPackages()
    {
        // Act
        var model = Parse("package p {\n  class A\n}\npackage empty\nclass B\nA --> B\nA -[hidden]- B\nnote \"n\" as N\nN .. A\nnote left of B : x\ntitle Facts");

        // Assert
        Assert.Equal((2, 2, 1, "Facts"), (model.NodeCount, model.EdgeCount, model.SubgraphCount, model.Title));
    }

    /// <summary>
    ///     Validates the hide and show rules: applied in order, to every entity or to a kind, a stereotype, or a name,
    ///     and <c>empty</c> only to empty compartments.
    /// </summary>
    [Fact]
    public void PlantUmlClassModel_PortionsOf_AppliesRulesInOrder()
    {
        // Arrange
        var model = Parse("hide empty members\nhide interface circle\nhide <<tag>> stereotype\nshow Full methods\nclass Empty\nclass Full {\n  x : int\n}\ninterface I\nclass T <<tag>>\nclass S {\n  a\n  --\n  b()\n}");
        ClassPortions Of(string id) => model.PortionsOf(model.Entities.Single(e => e.Id == id));

        // Act / Assert
        Assert.Equal(new ClassPortions(false, false, true, true), Of("Empty"));
        Assert.Equal(new ClassPortions(true, true, true, true), Of("Full"));
        Assert.Equal(new ClassPortions(false, false, false, true), Of("I"));
        Assert.Equal(new ClassPortions(false, false, true, false), Of("T"));
        Assert.Equal(ClassPortions.All, Of("S"));
    }

    /// <summary>
    ///     Validates the lowering: every package is a cluster under its parent, every entity has its image and package,
    ///     notes keep their targets, links, and order, and links keep their decorations and settings.
    /// </summary>
    [Fact]
    public void PlantUmlClassModel_Lower_MapsEveryElement()
    {
        // Arrange
        var model = Parse("left to right direction\nskinparam nodesep 40\npackage p {\n  package q {\n    class A\n  }\n}\nclass B\nA \"1\" *-- B : has\nnote right of A : side\nnote on link : on\nnote \"free\" as N\nN .. B");

        // Act
        var diagram = model.Lower(Context());

        // Assert
        Assert.Equal(["p", "p.q"], diagram.Clusters.Select(c => c.Id));
        Assert.Same(diagram.Clusters[0], diagram.Clusters[1].Parent);
        Assert.Same(diagram.Clusters[1], diagram.Entities.Single(e => e.Id == "p.q.A").Parent);
        Assert.IsType<ClassImage>(diagram.Entities[0].Image);
        var link = diagram.Links[0];
        Assert.Equal((LinkDecor.Composition, "1", "has"), (link.TailDecor, link.TailLabel, link.Label));
        Assert.Equal(["side", "on", "free"], diagram.Notes.Select(n => n.Text));
        Assert.Equal("p.q.A", diagram.Notes[0].Target!.Id);
        Assert.Same(link, diagram.Notes[1].Link);
        Assert.Same(diagram.Notes[2], diagram.Links[1].From);
        Assert.Equal((SvekRankDir.LeftToRight, 40.0), (diagram.RankDir, diagram.NodeSep!.Value));
        diagram.Validate();
    }

    /// <summary>
    ///     Validates the layout of a call: the scene holds the boxes, the clusters, the links, and the page texts around
    ///     them, at the diagram's own scale.
    /// </summary>
    [Fact]
    public void PlantUmlClassModel_Layout_DrawsBodyAndPageTexts()
    {
        // Arrange
        var model = Parse("scale 2\ntitle Model\nfooter f\npackage p {\n  class A\n}\nclass B\nA --> B");

        // Act
        var scene = model.Layout(Context());

        // Assert
        Assert.Equal(2, scene.Scale);
        Assert.Equal(2, scene.Items.Count(i => i.Role == "class"));
        Assert.Contains(scene.Items, i => i.Role == "cluster" && i.Id == "p");
        Assert.Contains(scene.Items, i => i.Role == "link");
        Assert.Contains(scene.Items, i => i is SceneText { Role: "title", Text: "Model" });
        Assert.Contains(scene.Items, i => i is SceneText { Role: "footer", Text: "f" });
    }

    /// <summary>
    ///     Validates the member rules: the visibility character, the modifiers, the tags, the guillemets, and an empty
    ///     member.
    /// </summary>
    /// <param name="raw">The member line.</param>
    /// <param name="text">The expected text.</param>
    /// <param name="visibility">The expected visibility.</param>
    /// <param name="isStatic">Whether it is static.</param>
    /// <param name="isAbstract">Whether it is abstract.</param>
    [Theory]
    [InlineData("- name : String", "name : String", "Private", false, false)]
    [InlineData("#count", "count", "Protected", false, false)]
    [InlineData("~ sync()", "sync()", "Package", false, false)]
    [InlineData("+ {static} create()", "create()", "Public", true, false)]
    [InlineData("{classifier} * id", "id", "Mandatory", true, false)]
    [InlineData("{abstract} + area()", "area()", "Public", false, true)]
    [InlineData("{method} -- x", "-- x", "None", false, false)]
    [InlineData("<<create>> make()", "\u00ABcreate\u00BB make()", "None", false, false)]
    [InlineData("++x", "++x", "None", false, false)]
    [InlineData("go() [[http://x]]", "go()", "None", false, false)]
    [InlineData("", " ", "None", false, false)]
    public void PlantUmlClassModel_Member_ReadsVisibilityAndModifiers(string raw, string text, string visibility, bool isStatic, bool isAbstract)
    {
        // Act
        var member = ClassMember.Parse(raw);

        // Assert
        Assert.Equal(new ClassMember(text, Enum.Parse<MemberVisibility>(visibility), isStatic, isAbstract), member);
    }

    /// <summary>
    ///     Validates the body rules: separators, their titles, methods and fields, and the split of a body without
    ///     separators.
    /// </summary>
    [Fact]
    public void PlantUmlClassModel_Body_SeparatesFieldsAndMethods()
    {
        // Act
        var (fields, methods) = ClassBody.Split(["", "a", "run()", "", "stop()", "{method} m", "{field} f()", "b", ""]);

        // Assert
        Assert.True(ClassBody.IsSeparator("--") && ClassBody.IsSeparator(".. t ..") && ClassBody.IsSeparator("==") && ClassBody.IsSeparator("__ x __"));
        Assert.False(ClassBody.IsSeparator("...") || ClassBody.IsSeparator("-x") || ClassBody.IsSeparator("a--"));
        Assert.Equal(("t", null), (ClassBody.SeparatorTitle(".. t .."), ClassBody.SeparatorTitle("--")));
        Assert.Equal(["a", "", "{field} f()", "b"], fields);
        Assert.Equal(["run()", "", "stop()", "{method} m"], methods);
    }
}
