using System.Text;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Syntax;

/// <summary>
///     Unit tests for the include directives of the <c>Preprocessor</c> unit: local files read in place under the
///     file-access policy, their locations, the include forms that are never followed, and the include limits.
/// </summary>
public sealed class PreprocessorIncludeTests : IDisposable
{
    /// <summary>The temporary folder of the test.</summary>
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("plantuml-include-");

    /// <summary>
    ///     Deletes the temporary folder.
    /// </summary>
    public void Dispose() => _folder.Delete(true);

    /// <summary>
    ///     Validates that an included file is read in place: its block content or its whole text, its variables shared,
    ///     its statements located in the included file, and its own includes relative to its folder.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Include_ReadsTheFileInPlace()
    {
        // Arrange
        Write("lib/defs.iuml", "!$who = \"Bob\"\n!include ../more.iuml\n");
        Write("more.iuml", "@startuml\nA -> C : more\n@enduml\n");

        // Act
        var source = Run("@startuml\n!include lib/defs.iuml\nA -> $who\n@enduml\n");

        // Assert
        var statements = source.Block.Statements.Where(s => s.Text.Length > 0).ToList();
        Assert.Equal(["A -> C : more", "A -> Bob"], statements.Select(s => s.Text));
        Assert.Equal(new PlantUmlLocation("more.iuml", 2, 1), statements[0].Location);
        Assert.Equal(new PlantUmlLocation("main.puml", 3, 1), statements[1].Location);
        Assert.Empty(source.Reports);
    }

    /// <summary>
    ///     Validates that the block of an included file is framed outside its comments: tags in a block comment before
    ///     its <c>@startuml</c> and an <c>@enduml</c> in a block comment inside it are comment text.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Include_TagsInComments_AreIgnored()
    {
        // Arrange
        Write("part.iuml", "/'\n@startuml\nX -> Y\n@enduml\n'/\n@startuml\nA -> C\n/' @enduml\n'/\nC -> A\n@enduml\n");

        // Act
        var source = Run("@startuml\n!include part.iuml\n@enduml\n");

        // Assert
        var statements = source.Block.Statements.Where(s => s.Text.Length > 0).ToList();
        Assert.Equal(["A -> C", "C -> A"], statements.Select(s => s.Text));
        Assert.Equal(new PlantUmlLocation("part.iuml", 10, 1), statements[1].Location);
    }

    /// <summary>
    ///     Validates the three directives: a second <c>!include</c> of a file is skipped (also the diagram's own file),
    ///     <c>!include_many</c> includes it again, and a second <c>!include_once</c> is an error.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Include_RepeatsFollowTheDirective()
    {
        // Arrange
        Write("step.iuml", "A -> B\n");
        Write("main.puml", "@startuml\nX -> Y\n@enduml\n");

        // Act
        var plain = Run("!include step.iuml\n!include step.iuml\n!include main.puml\n");
        var many = Run("!include_many step.iuml\n!include_many step.iuml\n");
        var once = Assert.Throws<PlantUmlSyntaxException>(() => Run("!include_once step.iuml\n!include_once step.iuml\n"));

        // Assert
        Assert.Single(plain.Block.Statements, s => s.Text == "A -> B");
        Assert.DoesNotContain(plain.Block.Statements, s => s.Text == "X -> Y");
        Assert.Equal(2, many.Block.Statements.Count(s => s.Text == "A -> B"));
        Assert.Equal((PlantUmlDiagnosticCodes.IncludeFailed, 2), (once.Code, once.Location.Line));
    }

    /// <summary>
    ///     Validates that a missing file, a text without a folder, and a file that is not valid UTF-8 fail with
    ///     plantuml-include-failed at the directive and a hint.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Include_FailuresAreLocated()
    {
        // Arrange
        File.WriteAllBytes(Path.Combine(_folder.FullName, "bad.iuml"), [0x41, 0xC3, 0x28]);

        // Act
        var missing = Assert.Throws<PlantUmlSyntaxException>(() => Run("A -> B\n  !include nothing.iuml\n"));
        var noFolder = Assert.Throws<PlantUmlSyntaxException>(() => Preprocessor.Run("!include x.iuml\n", "text", PlantUmlLimits.Default));
        var invalid = Assert.Throws<PlantUmlSyntaxException>(() => Run("!include bad.iuml\n"));

        // Assert
        Assert.All([missing, noFolder, invalid], e => Assert.Equal(PlantUmlDiagnosticCodes.IncludeFailed, e.Code));
        Assert.Equal(new PlantUmlLocation("main.puml", 2, 3), missing.Location);
        Assert.Contains("BaseDirectory", noFolder.Hint, StringComparison.Ordinal);
        Assert.Contains("UTF-8", invalid.Message, StringComparison.Ordinal);
        Assert.All([missing, noFolder, invalid], e => Assert.False(string.IsNullOrEmpty(e.Hint)));
    }

    /// <summary>
    ///     Validates that a file the file-access policy denies fails at the directive with the denial as the inner
    ///     exception.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Include_DeniedFile_CarriesTheRefusal()
    {
        // Arrange
        Write("secret.iuml", "A -> B\n");
        var allowed = Directory.CreateDirectory(Path.Combine(_folder.FullName, "allowed")).FullName;
        var includes = new IncludeResolver(_folder.FullName, null, FileAccessPolicy.UnderRoots(allowed), PlantUmlLimits.Default);

        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => Preprocessor.Run("!include secret.iuml\n", "main.puml", PlantUmlLimits.Default, includes));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.IncludeFailed, ex.Code);
        var refusal = Assert.IsType<FileReferenceException>(ex.InnerException);
        Assert.Equal(FileReferenceFailureKind.AccessDenied, refusal.Kind);
        Assert.DoesNotContain(_folder.FullName, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Validates that a URL, the standard library, a part of a file, and the other include directives are reported
    ///     as plantuml-include-unsupported and never read.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Include_UnfollowedForms_AreReported()
    {
        // Act
        var source = Run("!include https://example.com/x.puml\n!include <C4/C4>\n!include parts.puml!2\n!includesub lib.puml!BASIC\n!includeurl http://x/y\nA -> B\n");

        // Assert
        Assert.Equal([1, 2, 3, 4, 5], source.Reports.Select(r => r.Location.Line));
        Assert.All(source.Reports, r => Assert.Equal(PlantUmlDiagnosticCodes.IncludeUnsupported, r.Code));
        Assert.Equal(["A -> B"], source.Block.Statements.Select(s => s.Text).Where(t => t.Length > 0));
    }

    /// <summary>
    ///     Validates the include limits: the nesting depth, the number of includes (every followed directive counts), a
    ///     cycle that ends because a file is included once, and a file that includes itself many times.
    /// </summary>
    [Fact]
    public void PlantUmlPreprocessor_Include_LimitsAndCycles()
    {
        // Arrange
        Write("a.iuml", "!include b.iuml\nA -> B\n");
        Write("b.iuml", "!include a.iuml\nB -> C\n");
        Write("c.iuml", "C -> D\n");
        Write("loop.iuml", "!include_many loop.iuml\n");

        // Act
        var cycle = Run("!include a.iuml\n");
        var deep = Assert.Throws<PlantUmlSyntaxException>(() => Run("!include a.iuml\n", new PlantUmlLimits { MaxIncludeDepth = 1 }));
        var many = Assert.Throws<PlantUmlSyntaxException>(() => Run("!include a.iuml\n!include c.iuml\n", new PlantUmlLimits { MaxIncludes = 3 }));
        var looping = Assert.Throws<PlantUmlSyntaxException>(() => Run("!include loop.iuml\n"));

        // Assert
        Assert.Equal(["B -> C", "A -> B"], cycle.Block.Statements.Select(s => s.Text).Where(t => t.Length > 0));
        Assert.All([deep, many, looping], e => Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, e.Code));
        Assert.Equal(new PlantUmlLocation("a.iuml", 1, 1), deep.Location);
        Assert.Equal(new PlantUmlLocation("main.puml", 2, 1), many.Location);
        Assert.Equal(new PlantUmlLocation("loop.iuml", 1, 1), looping.Location);
    }

    /// <summary>
    ///     Writes a file of the test folder.
    /// </summary>
    /// <param name="name">The relative name.</param>
    /// <param name="text">The text.</param>
    private void Write(string name, string text)
    {
        var path = Path.Combine(_folder.FullName, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    /// <summary>
    ///     Preprocesses a text as the file <c>main.puml</c> of the test folder.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The preprocessed source.</returns>
    private PreprocessedSource Run(string text, PlantUmlLimits? limits = null)
    {
        var effective = limits ?? PlantUmlLimits.Default;
        var includes = new IncludeResolver(_folder.FullName, Path.Combine(_folder.FullName, "main.puml"), FileAccessPolicy.AllowAll, effective);
        return Preprocessor.Run(text, "main.puml", effective, includes);
    }
}
