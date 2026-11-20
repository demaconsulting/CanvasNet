using System.Text;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Hardening tests through the public API: every limit fails closed with a documented exception, located where the
///     text crosses it, and a seeded mutation fuzzer over the original fixtures only ever sees the documented
///     exceptions, each with a <c>plantuml-*</c> code, a location, and a hint.
/// </summary>
public class PlantUmlHardeningTests
{
    /// <summary>The codes a syntax exception may carry.</summary>
    private static readonly HashSet<string> SyntaxCodes = new(StringComparer.Ordinal)
    {
        PlantUmlDiagnosticCodes.SyntaxError,
        PlantUmlDiagnosticCodes.UnknownDiagram,
        PlantUmlDiagnosticCodes.IncludeFailed,
        PlantUmlDiagnosticCodes.LimitExceeded,
    };

    /// <summary>
    ///     Gets the structural limits with a text that crosses each of them and the line it crosses at.
    /// </summary>
    /// <returns>The limit name, its value, the text, and the expected line.</returns>
    public static TheoryData<string, int, string, int> StructuralLimits() => new()
    {
        { "MaxNodes", 2, "@startuml\nparticipant A\nparticipant B\nparticipant C\n@enduml\n", 4 },
        { "MaxNodes", 2, "@startuml\nstart\n:a;\n:b;\n@enduml\n", 4 },
        { "MaxEdges", 1, "@startuml\nA -> B\nB -> A\n@enduml\n", 3 },
        { "MaxEdges", 1, "@startuml\nstart\n:a;\n:b;\n@enduml\n", 2 },
        { "MaxLabelLength", 10, "@startuml\nA -> B : " + new string('x', 40) + "\n@enduml\n", 2 },
        { "MaxLabelLength", 10, "@startuml\nstart\n:" + new string('x', 40) + ";\n@enduml\n", 3 },
        { "MaxNestingDepth", 1, "@startuml\nalt one\nalt two\nA -> B\nend\nend\n@enduml\n", 3 },
        { "MaxNestingDepth", 1, "@startuml\nstart\nif (a) then\nif (b) then\n:x;\nendif\nendif\n@enduml\n", 4 },
        { "MaxNestingDepth", 1, "@startuml\n!if 1\n!if 2\n!endif\n!endif\nA -> B\n@enduml\n", 3 },
        { "MaxNodes", 2, "@startuml\nclass A\nclass B\nclass C\n@enduml\n", 4 },
        { "MaxEdges", 1, "@startuml\nA <|-- B\nB *-- C\n@enduml\n", 3 },
        { "MaxLabelLength", 10, "@startuml\nclass A {\n+ " + new string('x', 40) + "\n}\n@enduml\n", 3 },
        { "MaxNestingDepth", 1, "@startuml\npackage p {\npackage q {\nclass A\n}\n}\n@enduml\n", 3 },
        { "MaxNodes", 2, "@startuml\nstate A\nstate B\nstate C\n@enduml\n", 4 },
        { "MaxEdges", 1, "@startuml\n[*] --> A\nA --> B\n@enduml\n", 3 },
        { "MaxLabelLength", 10, "@startuml\nstate A : " + new string('x', 40) + "\n@enduml\n", 2 },
        { "MaxNestingDepth", 1, "@startuml\nstate A {\nstate B {\nstate C\n}\n}\n@enduml\n", 3 },
        { "MaxNodes", 2, "@startuml\n[A]\n[B]\n[C]\n@enduml\n", 4 },
        { "MaxEdges", 1, "@startuml\n[A] --> [B]\n[B] ..> I\n@enduml\n", 3 },
        { "MaxLabelLength", 10, "@startuml\ncomponent \"" + new string('x', 40) + "\" as C\n@enduml\n", 2 },
        { "MaxNestingDepth", 1, "@startuml\nnode A {\nfolder B {\n[C]\n}\n}\n@enduml\n", 3 },
        { "MaxNodes", 2, "@startuml\nactor A\n(U)\n:B:\n@enduml\n", 4 },
        { "MaxEdges", 1, "@startuml\nA --> (U)\n(U) ..> (V) : <<include>>\n@enduml\n", 3 },
        { "MaxLabelLength", 10, "@startuml\nusecase \"" + new string('x', 40) + "\" as U\n@enduml\n", 2 },
        { "MaxNestingDepth", 1, "@startuml\nrectangle A {\npackage B {\n(U)\n}\n}\n@enduml\n", 3 },
        { "MaxNodes", 2, "@startuml\nnode A\nartifact B\nqueue C\n@enduml\n", 4 },
        { "MaxEdges", 1, "@startuml\nnode A\nA --> B\nB ..> C\n@enduml\n", 4 },
        { "MaxLabelLength", 10, "@startuml\nstorage \"" + new string('x', 40) + "\" as S\n@enduml\n", 2 },
        { "MaxNestingDepth", 1, "@startuml\nstack A {\nqueue B {\nfile C\n}\n}\n@enduml\n", 3 },
    };

    /// <summary>
    ///     Validates that a text crossing a structural limit fails at open with plantuml-limit-exceeded at the line
    ///     where it crosses it, and opens under the default limits.
    /// </summary>
    /// <param name="limit">The limit.</param>
    /// <param name="value">Its value.</param>
    /// <param name="text">The text.</param>
    /// <param name="line">The expected line.</param>
    [Theory]
    [MemberData(nameof(StructuralLimits))]
    public void CanvasNetPlantUml_Hardening_StructuralLimits_AreLocated(string limit, int value, string text, int line)
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
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse(text, new PlantUmlOpenOptions { Limits = limits, SourceName = "limit.puml" }));
        var document = PlantUmlDocument.Parse(text);

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(("limit.puml", line), (ex.Location.File, ex.Location.Line));
        Assert.Contains(limit, ex.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(ex.Hint));
        Assert.True(document.IsSupported);
    }

    /// <summary>
    ///     Validates that a text over the size limit is refused before any work, and that a macro that grows the text
    ///     past the limit is refused after preprocessing.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Hardening_TextSize_IsRefused()
    {
        // Arrange
        var options = new PlantUmlOpenOptions { Limits = new PlantUmlLimits { MaxTextSize = 200 } };
        var huge = "@startuml\n" + string.Concat(Enumerable.Repeat("A -> B : hello\n", 100)) + "@enduml\n";
        var growing = "@startuml\n!define X " + new string('y', 150) + "\nA -> B : X\nA -> B : X\n@enduml\n";

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PlantUmlDocument.Parse(huge, options));
        Assert.Throws<InvalidDataException>(() => PlantUmlDocument.Parse(growing, options));
    }

    /// <summary>
    ///     Validates the include limits on the include fixture: one include too many fails at the directive of the main
    ///     file, and an include one level too deep at the directive of the included file.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Hardening_IncludeLimits_AreLocated()
    {
        // Arrange
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "include", "main.puml");

        // Act
        var many = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Open(path, new PlantUmlOpenOptions { SourceName = "main.puml", Limits = new PlantUmlLimits { MaxIncludes = 1 } }));
        var deep = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Open(path, new PlantUmlOpenOptions { Limits = new PlantUmlLimits { MaxIncludeDepth = 1 } }));

        // Assert
        Assert.All([many, deep], e => Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, e.Code));
        Assert.Equal(new PlantUmlLocation("main.puml", 4, 1), many.Location);
        Assert.Equal(new PlantUmlLocation("parts/steps.puml", 3, 1), deep.Location);
    }

    /// <summary>
    ///     Validates the limits of measuring and rendering: the layout work and the painted pixels fail closed with a
    ///     located plantuml-limit-exceeded render error, and reports beyond the cap are collapsed into one notice.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Hardening_RenderLimits_FailClosed()
    {
        // Arrange
        var activity = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "activity", "08-fork.puml"));
        var noisy = PlantUmlDocument.Parse("@startuml\nstart\ngoto a\ngoto b\ngoto c\ngoto d\ngoto e\nstop\n@enduml\n");
        var reports = new List<string>();
        var capped = new PlantUmlRenderOptions
        {
            Limits = new PlantUmlLimits { MaxDiagnostics = 2 },
            WarningHandler = w => reports.Add(w.Code),
            UnsupportedFeatureHandler = u => reports.Add(u.Feature),
        };

        // Act
        var work = Assert.Throws<PlantUmlRenderException>(() => activity.Measure(new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxLayoutWork = 50 } }, TestContext.Current.CancellationToken));
        var pixels = Assert.Throws<PlantUmlRenderException>(() => activity.Render(new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxPaintedPixels = 100 } }, TestContext.Current.CancellationToken));
        using var surface = noisy.Render(capped, TestContext.Current.CancellationToken);

        // Assert
        Assert.All([work, pixels], e => Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, e.Code));
        Assert.True(work.Location.IsKnown);
        Assert.Equal(
            [PlantUmlDiagnosticCodes.StatementUnsupported, PlantUmlDiagnosticCodes.StatementUnsupported, PlantUmlDiagnosticCodes.DiagnosticsSuppressed],
            reports);
    }

    /// <summary>
    ///     Validates that the layout work of a dense class diagram is bounded: a small budget fails closed with a located
    ///     plantuml-limit-exceeded render error, and the default budget lays it out.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Hardening_DenseClassGraph_LayoutWorkIsBounded()
    {
        // Arrange
        var links = Enumerable.Range(0, 60).SelectMany(i => new[] { $"C{i} --> C{(i * 7 + 3) % 60}\n", $"C{i} ..> C{(i * 11 + 5) % 60}\n" });
        var document = PlantUmlDocument.Parse("@startuml\npackage p {\nclass C0\n}\n" + string.Concat(links) + "@enduml\n");

        // Act
        var ex = Assert.Throws<PlantUmlRenderException>(() => document.Measure(new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxLayoutWork = 1000 } }, TestContext.Current.CancellationToken));
        var size = document.Measure(new PlantUmlRenderOptions { Scale = 0.1 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(2, ex.Location.Line);
        Assert.True(size.Width >= 1 && size.Height >= 1);
    }

    /// <summary>
    ///     Validates that the layout work of a dense state diagram with nested composite states is bounded: a small budget
    ///     fails closed with a located plantuml-limit-exceeded render error, and the default budget lays it out.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Hardening_DenseStateGraph_LayoutWorkIsBounded()
    {
        // Arrange
        var inner = string.Concat(Enumerable.Range(0, 30).Select(i => $"S{i} --> S{(i * 7 + 3) % 30} : e{i}\n"));
        var document = PlantUmlDocument.Parse("@startuml\nstate Outer {\nstate Inner {\n[*] --> S0\n" + inner + "}\n--\n[*] --> T\n}\n[*] --> Outer\n@enduml\n");

        // Act
        var ex = Assert.Throws<PlantUmlRenderException>(() => document.Measure(new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxLayoutWork = 1000 } }, TestContext.Current.CancellationToken));
        var size = document.Measure(new PlantUmlRenderOptions { Scale = 0.1 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(2, ex.Location.Line);
        Assert.True(size.Width >= 1 && size.Height >= 1);
    }

    /// <summary>
    ///     Validates that the layout work of a dense component diagram with nested containers is bounded: a small budget
    ///     fails closed with a located plantuml-limit-exceeded render error, and the default budget lays it out.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Hardening_DenseComponentGraph_LayoutWorkIsBounded()
    {
        // Arrange
        var members = string.Concat(Enumerable.Range(0, 20).Select(i => $"[C{i}]\n"));
        var links = string.Concat(Enumerable.Range(0, 40).Select(i => $"[C{i % 20}] --> I{(i * 7 + 3) % 25}\n"));
        var document = PlantUmlDocument.Parse("@startuml\nnode Server {\nfolder Inner {\n" + members + "}\n}\n" + links + "@enduml\n");

        // Act
        var ex = Assert.Throws<PlantUmlRenderException>(() => document.Measure(new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxLayoutWork = 1000 } }, TestContext.Current.CancellationToken));
        var size = document.Measure(new PlantUmlRenderOptions { Scale = 0.1 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(2, ex.Location.Line);
        Assert.True(size.Width >= 1 && size.Height >= 1);
    }

    /// <summary>
    ///     Validates that a use case diagram with many actors and use cases in a boundary is bounded: a small layout
    ///     budget fails closed with a located plantuml-limit-exceeded render error, and the default budget lays it out,
    ///     with the ellipse of a use case of more than a thousand lines (the longest text the default limits allow)
    ///     holding every line.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Hardening_DenseUseCaseGraph_LayoutWorkIsBounded()
    {
        // Arrange
        var members = string.Concat(Enumerable.Range(0, 20).Select(i => $"usecase U{i}\n"));
        var links = string.Concat(Enumerable.Range(0, 40).Select(i => $"A{(i * 7 + 3) % 15} --> U{i % 20}\n"));
        var document = PlantUmlDocument.Parse("@startuml\nrectangle Shop {\n" + members + "}\n" + links + "@enduml\n");
        var tall = PlantUmlDocument.Parse("@startuml\nusecase \"" + string.Concat(Enumerable.Repeat("a\\n", 1300)) + "\" as T\nactor A\nA --> T\n@enduml\n");

        // Act
        var ex = Assert.Throws<PlantUmlRenderException>(() => document.Measure(new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxLayoutWork = 1000 } }, TestContext.Current.CancellationToken));
        var size = document.Measure(new PlantUmlRenderOptions { Scale = 0.1 }, TestContext.Current.CancellationToken);
        var tallSize = tall.Measure(new PlantUmlRenderOptions { Scale = 0.01 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(2, ex.Location.Line);
        Assert.True(size.Width >= 1 && size.Height >= 1);

        // the ellipse holds all 1300 lines (14-pixel font at a scale of 0.01)
        Assert.True(tallSize.Height >= 182, $"height {tallSize.Height}");
    }

    /// <summary>
    ///     Validates that large diagrams within the limits render: a long chain of actions, blocks nested at the
    ///     nesting limit, and many participants.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Hardening_LargeDiagramsWithinTheLimits_Render()
    {
        // Arrange
        var chain = "@startuml\nstart\n" + string.Concat(Enumerable.Range(0, 400).Select(i => $":step {i};\n")) + "stop\n@enduml\n";
        var nested = "@startuml\nstart\n" + string.Concat(Enumerable.Range(0, 32).Select(i => $"if (t{i}) then (yes)\n")) + ":deep;\n" +
                     string.Concat(Enumerable.Repeat("endif\n", 32)) + "stop\n@enduml\n";
        var wide = "@startuml\n" + string.Concat(Enumerable.Range(0, 200).Select(i => $"P{i} -> P{i + 1} : m\n")) + "@enduml\n";
        var options = new PlantUmlRenderOptions { Scale = 0.1 };

        // Act
        var sizes = new[] { chain, nested, wide }.Select(t => PlantUmlDocument.Parse(t).Measure(options, TestContext.Current.CancellationToken)).ToList();

        // Assert
        Assert.All(sizes, s => Assert.True(s.Width >= 1 && s.Height >= 1));
        Assert.True(sizes[0].Height > sizes[0].Width);
        Assert.True(sizes[2].Width > sizes[2].Height);
    }

    /// <summary>
    ///     Validates with a seeded mutation fuzzer over the original fixtures (truncated texts, deleted ranges, inserted
    ///     tokens, duplicated and swapped lines, unbalanced blocks) that opening, measuring, and rendering only ever throw
    ///     the documented exceptions, every syntax error with a code, a location, and a hint, and no layout defect.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Hardening_MutatedFixtures_ThrowOnlyDocumentedExceptions()
    {
        // Arrange
        string[] tokens =
        [
            "->", "-->", "<-", "->>", "-[#red]->", "++", "--", "**", ":", ";", "|", "|Lane|", "(", ")", "{", "}", "[", "]", "#", "#red",
            "start\n", "stop\n", "end\n", "if (x) then (yes)\n", "else (no)\n", "elseif (y) then\n", "endif\n", "while (w)\n",
            "endwhile\n", "repeat\n", "repeat while (r)\n", "backward :b;\n", "fork\n", "fork again\n", "end fork\n", "end merge\n",
            "split\n", "split again\n", "end split\n", "partition P {\n", "detach\n", "kill\n", "note left\n", "end note\n",
            "floating note right: n\n", "(A)\n", "alt x\n", "else\n", "loop\n", "group g\n", "box \"b\"\n", "end box\n",
            "activate A\n", "deactivate A\n", "return r\n", "autonumber 10 5\n", "== d ==\n", "...\n", "||45||\n", "legend\n",
            "endlegend\n", "title t\n", "skinparam activityBackgroundColor #00FF00\n", "skinparam {\n", "!define X Y\n",
            "!$v = \"w\"\n", "$v", "!if 1\n", "!endif\n", "!include x.iuml\n", "!include <C4/C4>\n", "@startuml\n", "@enduml\n",
            "\"", "'", "/'", "'/", "\\\n", "**bold**", "<color:red>", "\t", "\n\n", " ",
            "<|--", "*--", "o--", "..>", "--|>", "-up->", "-[hidden]-", "\"1..*\"", "class X {\n", "interface I\n", "enum E {\n",
            "object o {\n", "map m {\n", "k => v\n", "package P {\n", "namespace N {\n", "note left of A\n", "note on link\n",
            "note \"n\" as N\n", "hide empty members\n", "show circle\n", "--\n", ".. t ..\n", "==\n", "+ x : int\n", "{static}", "<<S>>",
            "<T>", "A : field\n", "left to right direction\n", "together {\n", "a.b.C", "::",
            "[*]", "[H]", "[H*]", "X[H]", "state S {\n", "state \"L\" as S\n", "||\n", "<<choice>>", "<<fork>>", "<<end>>",
            "hide empty description\n", "S : text\n", "end state\n", "-left->", "-[dashed]->",
            "[C]", "() I", "(U)", ":A:", "component C [\n", "]\n", "node N {\n", "component K {\n", "port p\n", "--(", ")--",
            "0)--", "-(0)-", "==>", "<<svc>>", "as \"x\"", "artifact X\n", "cloud C\n", "database D\n", "rectangle {\n",
            "actor/ B\n", "usecase/ W\n", "(U)/", ":A:/", ": <<include>>", ": <<extend>>", "usecase \"a\\nb\" as M\n", ":Guest:\n",
            "storage S {\n", "stack K {\n", "queue Q {\n", "hexagon H {\n", "file F {\n", "artifact A {\n", "person P\n", "boundary B\n",
            "control C\n", "entity E\n", "collections C\n", "label L\n", "agent A\n", "circle O\n", "action X\n", "process P {\n",
        ];
        var random = new Random(20261118);
        var folders = new[] { "sequence", "activity", "class", "object", "state", "usecase", "component", "deployment" };
        var fixtures = folders
            .SelectMany(f => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", f), "*.puml"))
            .Concat(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Gallery"), "plantuml-*.puml"))
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToList();
        var unexpected = new List<string>();

        // Act
        foreach (var fixture in fixtures)
        {
            for (var round = 0; round < 24; round++)
            {
                var text = Mutate(fixture, tokens, random);
                var problem = Exercise(text);
                if (problem is not null)
                {
                    unexpected.Add($"{problem}\n--- text ---\n{text}");
                }
            }
        }

        // Assert
        Assert.True(fixtures.Count >= 60);
        Assert.Empty(unexpected);
    }

    /// <summary>
    ///     Opens, measures, and renders a text and checks every exception against the documented ones.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>A description of an undocumented outcome, or null.</returns>
    private static string? Exercise(string text)
    {
        try
        {
            var document = PlantUmlDocument.Parse(text, new PlantUmlOpenOptions { SourceName = "fuzz.puml" });
            var options = new PlantUmlRenderOptions { Scale = 0.25 };
            document.Measure(options, TestContext.Current.CancellationToken);
            using var surface = document.Render(options, TestContext.Current.CancellationToken);
            return null;
        }
        catch (PlantUmlSyntaxException ex)
        {
            if (!SyntaxCodes.Contains(ex.Code) || !ex.Location.IsKnown || ex.Location.File != "fuzz.puml" || string.IsNullOrEmpty(ex.Hint))
            {
                return $"syntax error without a code, a location, or a hint: {ex.Message} (hint: {ex.Hint})";
            }

            return null;
        }
        catch (PlantUmlRenderException ex)
        {
            return ex.Code == PlantUmlDiagnosticCodes.LimitExceeded && ex.Location.IsKnown ? null : $"render error: {ex.Code} {ex.Message} {ex.InnerException}";
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (Exception ex)
        {
            return $"undocumented {ex.GetType().Name}: {ex}";
        }
    }

    /// <summary>
    ///     Mutates a text: truncates it, deletes a range, inserts a token, duplicates or swaps a line, or flips a
    ///     character, one to three times.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens to insert.</param>
    /// <param name="random">The seeded generator.</param>
    /// <returns>The mutated text.</returns>
    private static string Mutate(string text, string[] tokens, Random random)
    {
        var builder = new StringBuilder(text);
        var count = random.Next(1, 4);
        for (var i = 0; i < count; i++)
        {
            var position = random.Next(builder.Length + 1);
            switch (random.Next(6))
            {
                case 0 when builder.Length > 0:
                    builder.Length = position;
                    break;
                case 1 when builder.Length > 0:
                    var start = Math.Min(position, builder.Length - 1);
                    builder.Remove(start, Math.Min(random.Next(1, 16), builder.Length - start));
                    break;
                case 2:
                    builder.Insert(position, tokens[random.Next(tokens.Length)]);
                    break;
                case 3:
                    var lines = builder.ToString().Split('\n');
                    builder.Insert(position, "\n" + lines[random.Next(lines.Length)] + "\n");
                    break;
                case 4:
                    var all = builder.ToString().Split('\n');
                    if (all.Length > 2)
                    {
                        var a = random.Next(all.Length);
                        var b = random.Next(all.Length);
                        (all[a], all[b]) = (all[b], all[a]);
                        builder.Clear().Append(string.Join('\n', all));
                    }

                    break;
                default:
                    if (builder.Length > 0)
                    {
                        var at = Math.Min(position, builder.Length - 1);
                        const string Flips = "(){}[]:;|#!@\"'-<>/\\*x \n";
                        builder[at] = Flips[random.Next(Flips.Length)];
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
