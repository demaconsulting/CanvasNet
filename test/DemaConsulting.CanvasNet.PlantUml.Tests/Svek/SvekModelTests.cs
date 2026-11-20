using DemaConsulting.CanvasNet.PlantUml.Svek;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Svek.SvekTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Svek;

/// <summary>
///     Unit tests of the SvekModel unit: <see cref="SvekDiagram"/> with its members, facts, and validation, and the
///     element types.
/// </summary>
public class SvekModelTests
{
    /// <summary>
    ///     Validates the defaults of a link: length 2, weight 1, solid, no decorations, no hint.
    /// </summary>
    [Fact]
    public void PlantUmlSvekModel_Link_Defaults()
    {
        // Act
        var link = new SvekLink("l", Entity("A"), Entity("B"));

        // Assert
        Assert.Equal((2, 1.0, LinkStyle.Solid, LinkDecor.None, LinkDecor.None, LinkDirection.None), (link.Length, link.Weight, link.Style, link.TailDecor, link.HeadDecor, link.Direction));
        Assert.Null(link.Label);
    }

    /// <summary>
    ///     Validates that the members of a cluster are its entities, then its floating notes, then its child clusters,
    ///     in the order of the diagram, and that attached notes are not members.
    /// </summary>
    [Fact]
    public void PlantUmlSvekModel_MembersOf_EntitiesNotesThenClusters()
    {
        // Arrange
        var package = new SvekCluster("P");
        var inner = new SvekCluster("P.Q") { Parent = package };
        var a = Entity("A", parent: package);
        var b = Entity("B");
        var floating = new SvekNote("N", "n") { Parent = package };
        var attached = new SvekNote("M", "m") { Target = a };
        var diagram = new SvekDiagram { Entities = [a, b], Clusters = [package, inner], Notes = [floating, attached] };

        // Act
        var members = diagram.MembersOf(package).Select(m => m.Id).ToList();
        var top = diagram.MembersOf(null).Select(m => m.Id).ToList();

        // Assert
        Assert.Equal(["A", "N", "P.Q"], members);
        Assert.Equal(["B", "P"], top);
    }

    /// <summary>
    ///     Validates the facts: the entities, the links that do not touch a note, and the clusters with members, regions
    ///     not counted.
    /// </summary>
    [Fact]
    public void PlantUmlSvekModel_CountFacts_EntitiesLinksAndClusters()
    {
        // Arrange
        var composite = new SvekCluster("S") { Concurrency = ClusterConcurrency.Stacked };
        var region = new SvekCluster("S.1") { Parent = composite };
        var empty = new SvekCluster("E");
        var a = Entity("A", parent: region);
        var b = Entity("B");
        var note = new SvekNote("N", "n");
        var diagram = new SvekDiagram
        {
            Entities = [a, b],
            Clusters = [composite, region, empty],
            Notes = [note],
            Links = [new SvekLink("l1", a, b), new SvekLink("l2", note, b)],
        };

        // Act
        var facts = diagram.CountFacts();

        // Assert
        Assert.Equal(new SvekFacts(2, 1, 1), facts);
    }

    /// <summary>
    ///     Validates that a consistent diagram passes the validation.
    /// </summary>
    [Fact]
    public void PlantUmlSvekModel_Validate_ConsistentDiagram_Passes()
    {
        // Arrange
        var package = new SvekCluster("P");
        var a = Entity("A", parent: package);
        var b = Entity("B");
        var diagram = new SvekDiagram
        {
            Entities = [a, b],
            Clusters = [package],
            Notes = [new SvekNote("N", "n") { Target = a }],
            Links = [new SvekLink("l", a, b), new SvekLink("m", b, package)],
        };

        // Act
        var ex = Record.Exception(diagram.Validate);

        // Assert
        Assert.Null(ex);
    }

    /// <summary>
    ///     Validates that every kind of inconsistency is refused with an argument exception.
    /// </summary>
    [Fact]
    public void PlantUmlSvekModel_Validate_Inconsistencies_Throw()
    {
        // Arrange
        var a = Entity("A");
        var b = Entity("B");
        var stranger = Entity("X");
        var attached = new SvekNote("N", "n") { Target = a };
        var concurrent = new SvekCluster("S") { Concurrency = ClusterConcurrency.SideBySide };
        var inConcurrent = Entity("C", parent: concurrent);
        var outside = new SvekCluster("O");
        var orphan = Entity("D", parent: outside);
        var diagrams = new[]
        {
            new SvekDiagram { Entities = [a, Entity("A")] },
            new SvekDiagram { Entities = [a, b], Links = [new SvekLink("l", a, b), new SvekLink("l", b, a)] },
            new SvekDiagram { Entities = [a], Links = [new SvekLink("l", a, stranger)] },
            new SvekDiagram { Entities = [a], Notes = [attached], Links = [new SvekLink("l", attached, a)] },
            new SvekDiagram { Entities = [a, b], Links = [new SvekLink("l", a, b) { Length = 0 }] },
            new SvekDiagram { Entities = [a, b], Links = [new SvekLink("l", a, b) { Weight = 0 }] },
            new SvekDiagram { Entities = [inConcurrent], Clusters = [concurrent] },
            new SvekDiagram { Entities = [orphan] },
            new SvekDiagram { Entities = [a], Notes = [new SvekNote("M", "m") { Target = stranger }] },
        };

        // Act / Assert
        foreach (var diagram in diagrams)
        {
            Assert.Throws<ArgumentException>(diagram.Validate);
        }
    }

    /// <summary>
    ///     Validates that the constructors refuse empty ids and missing parts.
    /// </summary>
    [Fact]
    public void PlantUmlSvekModel_Constructors_RefuseMissingParts()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => new SvekCluster(string.Empty));
        Assert.Throws<ArgumentNullException>(() => new SvekEntity("A", null!));
        Assert.Throws<ArgumentNullException>(() => new SvekNote("N", null!));
        Assert.Throws<ArgumentException>(() => new SvekLink(string.Empty, Entity("A"), Entity("B")));
        Assert.Throws<ArgumentNullException>(() => new SvekLink("l", Entity("A"), null!));
    }
}
