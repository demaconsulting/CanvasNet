using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.GitGraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.GitGraph;

/// <summary>
///     Unit tests for <c>GitGraphParser</c> and the database its statements run against (<c>GitGraphDatabase</c>): the
///     tokens, the grammar and its located errors, the value converters, Mermaid's operations with their errors and
///     messages, the generated ids, the branch order, the sanitizing, the metadata, the limits, and the run of a render
///     whose configuration renames the main branch.
/// </summary>
public sealed class GitGraphParserTests
{
    /// <summary>
    ///     Validates the header forms: <c>gitGraph</c>, <c>gitGraph:</c>, <c>gitGraph :</c> and a direction with its colon
    ///     (a direction without its colon is not part of the header), and a statement on the header's line.
    /// </summary>
    [Fact]
    public void GitGraphParser_Parse_HeaderForms_GiveTheDirection()
    {
        // Act
        var plain = Parse("gitGraph\n  commit\n");
        var colon = Parse("gitGraph:\n  commit\n");
        var spaced = Parse("gitGraph :\n  commit\n");
        var topToBottom = Parse("gitGraph TB:\n  commit\n");
        var bottomToTop = Parse("gitGraph BT :\n  commit\n");
        var sameLine = Parse("gitGraph commit\n");
        var noColon = Assert.Throws<MermaidSyntaxException>(() => Parse("gitGraph LR\n  commit\n"));

        // Assert
        Assert.Equal(["LR", "LR", "LR", "TB", "BT", "LR"], new[] { plain, colon, spaced, topToBottom, bottomToTop, sameLine }.Select(d => d.Direction));
        Assert.Equal(1, sameLine.NodeCount);
        Assert.Equal((1, 10), (noColon.Location.Line, noColon.Location.Column));
        Assert.Equal(["EOF"], noColon.Expected);
    }

    /// <summary>
    ///     Validates the keywords' longer alternative: a keyword followed by more name characters is a branch name
    ///     (<c>commits</c>, <c>cherry-picked</c>, <c>LRx</c>), while a keyword alone is not a name and needs quotes.
    /// </summary>
    [Fact]
    public void GitGraphParser_Parse_KeywordPrefixes_AreReferences()
    {
        // Act
        var diagram = Parse("gitGraph\n  commit\n  branch commits\n  branch cherry-picked\n  branch LRx\n  branch \"commit\"\n  branch release/1.0\n");
        var keyword = Assert.Throws<MermaidSyntaxException>(() => Parse("gitGraph\n  commit\n  branch commit\n"));
        var glued = Assert.Throws<MermaidSyntaxException>(() => Parse("gitGraphs\n  commit\n"));

        // Assert
        Assert.Equal(["main", "commits", "cherry-picked", "LRx", "commit", "release/1.0"], diagram.OpenDatabase.Branches.Select(branch => branch.Name));
        Assert.Equal((3, 10), (keyword.Location.Line, keyword.Location.Column));
        Assert.Equal(["REFERENCE", "STRING"], keyword.Expected);
        Assert.Equal((1, 1), (glued.Location.Line, glued.Location.Column));
        Assert.Equal(["gitGraph", "gitGraph:"], glued.Expected);
    }

    /// <summary>
    ///     Validates the grammar's located errors and the tokens chevrotain lists for them.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line of the error.</param>
    /// <param name="column">The column of the error.</param>
    /// <param name="expected">The expected tokens, separated by spaces, or null for a lexical error.</param>
    [Theory]
    [InlineData("gitGraph\n  commit id: foo\n", 2, 14, "STRING")]
    [InlineData("gitGraph\n  commit id: \"a\" foo\n", 2, 18, "NEWLINE EOF")]
    [InlineData("gitGraph\n  commit type: normal\n", 2, 16, "NORMAL REVERSE HIGHLIGHT")]
    [InlineData("gitGraph\n  branch\n", 2, 9, "REFERENCE STRING")]
    [InlineData("gitGraph\n  branch dev order: high\n", 2, 21, "INT")]
    [InlineData("gitGraph\n  commit\n  commit2\n", 3, 3, "EOF")]
    [InlineData("gitGraph\n  branch café\n", 2, 13, null)]
    [InlineData("gitGraph\n  branch a.\n", 2, 11, null)]
    public void GitGraphParser_Parse_GrammarErrors_AreLocatedWithTheExpectedTokens(string text, int line, int column, string? expected)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, line, column), (exception.Code, exception.Location.Line, exception.Location.Column));
        Assert.False(string.IsNullOrEmpty(exception.Hint));
        if (expected is null)
        {
            Assert.Contains("unrecognized text", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(expected.Split(' '), exception.Expected);
        }
    }

    /// <summary>
    ///     Validates the options of a commit: a repeated <c>id:</c>, message or <c>type:</c> keeps its last value, tags add
    ///     up, a message may be written without <c>msg:</c>, and strings are converted as Langium converts them.
    /// </summary>
    [Fact]
    public void GitGraphParser_Parse_CommitOptions_LastWinsAndTagsAddUp()
    {
        // Act
        var diagram = Parse("gitGraph\n  commit id: \"a\" id: 'b\\\"c' \"first\" msg: \"second\" tag: \"x\" type: REVERSE tag: \"y\" type: HIGHLIGHT\n");

        // Assert
        var commit = Assert.Single(diagram.OpenDatabase.Commits);
        Assert.Equal(("b\"c", "second", GitCommit.Highlight), (commit.Id, commit.Message, commit.Type));
        Assert.Equal(["x", "y"], commit.Tags);
    }

    /// <summary>
    ///     Validates the ids Mermaid generates for commits without one: the sequence number and seven digits of the fixed
    ///     sequence (restarted for every run), drawn only when an id is needed; a cherry-pick always gets one.
    /// </summary>
    [Fact]
    public void GitGraphParser_Parse_GeneratedIds_FollowTheFixedSequence()
    {
        // Act
        var first = Parse("gitGraph\n  commit\n  branch dev\n  commit id: \"x\"\n  commit\n  checkout main\n  cherry-pick id: \"x\"\n");
        var second = Parse("gitGraph\n  commit\n");

        // Assert
        Assert.Equal(["0-eb28011", "x", "2-cc99105", "3-43d14d3"], first.OpenDatabase.Commits.Select(commit => commit.Id));
        Assert.Equal("0-eb28011", Assert.Single(second.OpenDatabase.Commits).Id);
    }

    /// <summary>
    ///     Validates the operations Mermaid's database rejects: each is a located syntax error with Mermaid's message,
    ///     at its statement, with a hint.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="message">Mermaid's message.</param>
    /// <param name="line">The line of the statement.</param>
    [Theory]
    [InlineData("gitGraph\n  commit\n  branch dev\n  merge main\n", "Cannot merge branch 'main' into itself.", 4)]
    [InlineData("gitGraph\n  merge main\n", "Incorrect usage of \"merge\". Cannot merge a branch to itself", 2)]
    [InlineData("gitGraph\n  merge x\n", "Incorrect usage of \"merge\". Current branch (main)has no commits", 2)]
    [InlineData("gitGraph\n  commit\n  merge x\n", "Incorrect usage of \"merge\". Branch to be merged (x) does not exist", 3)]
    [InlineData("gitGraph\n  branch dev\n  checkout main\n  commit\n  merge dev\n", "Incorrect usage of \"merge\". Branch to be merged (dev) has no commits", 5)]
    [InlineData("gitGraph\n  commit\n  branch dev\n  checkout main\n  merge dev\n", "Incorrect usage of \"merge\". Both branches have same head", 5)]
    [InlineData("gitGraph\n  commit id: \"A\"\n  branch dev\n  commit\n  checkout main\n  merge dev id: \"A\"\n", "Incorrect usage of \"merge\". Commit with id:A already exists, use different custom id", 6)]
    [InlineData("gitGraph\n  branch main\n", "Trying to create an existing branch. (Help: Either use a new name if you want create a new branch or try using \"checkout main\")", 2)]
    [InlineData("gitGraph\n  checkout dev\n", "Trying to checkout branch which is not yet created. (Help try using \"branch dev\")", 2)]
    [InlineData("gitGraph\n  commit\n  cherry-pick id: \"x\"\n", "Incorrect usage of \"cherryPick\". Source commit id should exist and provided", 3)]
    [InlineData("gitGraph\n  commit id: \"A\"\n  branch dev\n  commit id: \"B\"\n  checkout main\n  cherry-pick id: \"B\" parent: \"Z\"\n", "Invalid operation: The specified parent commit is not an immediate parent of the cherry-picked commit.", 6)]
    [InlineData("gitGraph\n  commit id: \"A\"\n  branch dev\n  commit id: \"B\"\n  checkout main\n  merge dev id: \"M\"\n  branch rel\n  commit\n  cherry-pick id: \"M\"\n", "Incorrect usage of cherry-pick: If the source commit is a merge commit, an immediate parent commit must be specified.", 9)]
    [InlineData("gitGraph\n  commit id: \"A\"\n  cherry-pick id: \"A\"\n", "Incorrect usage of \"cherryPick\". Source commit is already on current branch", 3)]
    [InlineData("gitGraph\n  branch dev\n  commit id: \"B\"\n  checkout main\n  cherry-pick id: \"B\"\n", "Incorrect usage of \"cherry-pick\". Current branch (main)has no commits", 5)]
    public void GitGraphParser_Parse_RejectedOperations_FailWithMermaidsMessage(string text, string message, int line)
    {
        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, line, 3), (exception.Code, exception.Location.Line, exception.Location.Column));
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(exception.Hint));
    }

    /// <summary>
    ///     Validates what Mermaid does not reject: a commit whose id is used again replaces the earlier commit in its
    ///     place, a branch merged again without new commits gives another merge, and the same commit picked twice gives
    ///     two cherry-picks.
    /// </summary>
    [Fact]
    public void GitGraphParser_Parse_AcceptedRepeats_FollowMermaid()
    {
        // Act
        var reused = Parse("gitGraph\n  commit id: \"a\"\n  commit id: \"b\"\n  commit id: \"a\"\n");
        var mergedAgain = Parse("gitGraph\n  commit\n  branch dev\n  commit\n  checkout main\n  merge dev\n  merge dev\n");
        var picked = Parse("gitGraph\n  commit\n  branch dev\n  commit id: \"x\"\n  checkout main\n  cherry-pick id: \"x\"\n  cherry-pick id: \"x\"\n");

        // Assert
        Assert.Equal([("a", 2), ("b", 1)], reused.OpenDatabase.Commits.Select(commit => (commit.Id, commit.Seq)));
        Assert.Equal(["b"], reused.OpenDatabase.Commits[0].Parents);
        Assert.Equal(2, mergedAgain.OpenDatabase.Commits.Count(commit => commit.Type == GitCommit.Merge));
        Assert.Equal(["cherry-pick:x", "cherry-pick:x"], picked.OpenDatabase.Commits.Where(commit => commit.Type == GitCommit.CherryPick).Select(commit => commit.Tags[0]));
    }

    /// <summary>
    ///     Validates the texts Mermaid composes and the tags of cherry-picks: a merge's and a cherry-pick's message, the
    ///     default tag (with the parent of a picked merge), and given tags without the empty ones.
    /// </summary>
    [Fact]
    public void GitGraphParser_Parse_ComposedTexts_FollowMermaid()
    {
        // Act
        var diagram = Parse(
            "gitGraph\n  commit id: \"A\" msg: \"first\"\n  branch dev\n  commit id: \"B\"\n  checkout main\n  merge dev id: \"M\" type: REVERSE\n" +
            "  branch rel\n  commit\n  cherry-pick id: \"M\" parent: \"A\"\n  cherry-pick id: \"B\" tag: \"\" tag: \"kept\"\n");

        // Assert
        var commits = diagram.OpenDatabase.Commits;
        var merge = commits.Single(commit => commit.Id == "M");
        Assert.Equal(("merged branch dev into main", GitCommit.Reverse, true), (merge.Message, merge.CustomType, merge.CustomId));
        var picks = commits.Where(commit => commit.Type == GitCommit.CherryPick).ToList();
        Assert.Equal(["cherry-picked merged branch dev into main into rel", "cherry-picked  into rel"], picks.Select(pick => pick.Message));
        Assert.Equal(["cherry-pick:M|parent:A"], picks[0].Tags);
        Assert.Equal(["kept"], picks[1].Tags);
        Assert.Equal(["3-eb28011", "M"], picks[0].Parents);
    }

    /// <summary>
    ///     Validates the sanitizing of the gitGraph configuration: <c>&lt;</c>, <c>&gt;</c> and <c>=</c> escaped while
    ///     <c>&lt;br&gt;</c> tags are kept, ids, tags and branch names sanitized, and the tags of a merge kept as written.
    /// </summary>
    [Fact]
    public void GitGraphParser_Parse_Sanitizing_EscapesAsMermaid()
    {
        // Act
        var diagram = Parse("gitGraph\n  commit id: \"a<b>c=d&e\" tag: \"t<br>u\"\n  branch \"x<y\"\n  commit\n  checkout main\n  merge \"x<y\" tag: \"raw<b>\"\n");

        // Assert
        var commits = diagram.OpenDatabase.Commits;
        Assert.Equal("a&lt;b&gt;c&equals;d&e", commits[0].Id);
        Assert.Equal(["t<br>u"], commits[0].Tags);
        Assert.Equal("x&lt;y", commits[1].Branch);
        Assert.Equal(["raw<b>"], commits[2].Tags);
        Assert.Equal(string.Empty, GitGraphDatabase.Sanitize(string.Empty));
    }

    /// <summary>
    ///     Validates the drawing order of the branches: by their order, stably, a branch without one at 0, and the main
    ///     branch's name and order from the configuration.
    /// </summary>
    [Fact]
    public void GitGraphParser_Parse_BranchOrder_SortsStably()
    {
        // Arrange
        var config = Config("{\"gitGraph\": {\"mainBranchName\": \"trunk\", \"mainBranchOrder\": 0.5}}");

        // Act
        var plain = Parse("gitGraph\n  branch dev order: 2\n  branch b order: 1\n  branch c\n  branch d order: 0\n");
        var configured = Parse("gitGraph\n  branch a\n  branch b order: 1\n  checkout trunk\n", config: config);

        // Assert
        Assert.Equal(["main", "c", "d", "b", "dev"], plain.OpenDatabase.BranchOrder.Select(branch => branch.Name));
        Assert.Equal(["a", "trunk", "b"], configured.OpenDatabase.BranchOrder.Select(branch => branch.Name));
        Assert.Equal("trunk", configured.OpenDatabase.CurrentBranch);
    }

    /// <summary>
    ///     Validates the metadata: the commits as nodes, the parent links as edges, no subgraphs, the direction, and the
    ///     title statement, which is set (and replaces the frontmatter title) only when it has a value.
    /// </summary>
    [Fact]
    public void GitGraphParser_Parse_Metadata_CountsCommitsAndParentLinks()
    {
        // Act
        var diagram = Parse("---\ntitle: Front\n---\ngitGraph TB:\n  title History\n  accTitle: Acc\n  accDescr: Desc\n  commit\n  branch dev\n  commit\n  checkout main\n  merge dev\n");
        var empty = Parse("---\ntitle: Front\n---\ngitGraph\n  title\n  commit\n");

        // Assert
        Assert.Equal((3, 3, 0, "TB"), (diagram.NodeCount, diagram.EdgeCount, diagram.SubgraphCount, diagram.Direction));
        Assert.Equal(("History", true, "History", "Acc", "Desc"), (diagram.Title, diagram.IsTitleSet, diagram.DrawnTitle, diagram.AccTitle, diagram.AccessibleDescription));
        Assert.Equal((null, false, "Front"), (empty.Title, empty.IsTitleSet, empty.DrawnTitle));
    }

    /// <summary>
    ///     Validates the node limit: the main branch, each branch and each commit count, and the statement over the limit
    ///     is a located limit error.
    /// </summary>
    [Fact]
    public void GitGraphParser_Parse_NodeLimit_CountsBranchesAndCommits()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNodes = 4 };

        // Act
        var fits = Parse("gitGraph\n  commit\n  branch dev\n  commit\n", limits);
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("gitGraph\n  commit\n  branch dev\n  commit\n  merge main\n", limits));

        // Assert
        Assert.Equal(2, fits.NodeCount);
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, 5), (exception.Code, exception.Location.Line));
        Assert.Contains("MermaidLimits.MaxNodes", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the label limit on every text: a token, the escapes of sanitizing (which make a text longer), and the
    ///     messages Mermaid composes for merges and cherry-picks.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line of the error.</param>
    [Theory]
    [InlineData("gitGraph\n  commit id: \"0123456789012345678901234567890\"\n", 2)]
    [InlineData("gitGraph\n  commit id: \"==========\"\n", 2)]
    [InlineData("gitGraph\n  commit\n  branch abcdefghij\n  commit\n  checkout main\n  merge abcdefghij\n", 6)]
    [InlineData("gitGraph\n  commit\n  branch b\n  commit msg: \"abcdefghijklmnop\"\n  checkout main\n  cherry-pick id: \"1-cc99105\"\n", 6)]
    public void GitGraphParser_Parse_LabelLimit_CoversEveryText(string text, int line)
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 30 };

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text, limits));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, line), (exception.Code, exception.Location.Line));
        Assert.Contains("MermaidLimits.MaxLabelLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the database of a render's configuration: the text's own database when the main branch is the same,
    ///     else the statements run again, an operation Mermaid then rejects being a located render failure.
    /// </summary>
    [Fact]
    public void GitGraphDiagram_Database_OtherMainBranch_RunsTheStatementsAgain()
    {
        // Arrange
        var diagram = Parse("gitGraph\n  commit\n  branch dev\n  commit\n  checkout main\n  merge dev\n");

        // Act
        var same = diagram.Database(new GitGraphConfig());
        var reordered = diagram.Database(new GitGraphConfig { MainBranchOrder = 5 });
        var exception = Assert.Throws<MermaidRenderException>(() => diagram.Database(new GitGraphConfig { MainBranchName = "trunk" }));

        // Assert
        Assert.Same(diagram.OpenDatabase, same);
        Assert.Equal(["dev", "main"], reordered.BranchOrder.Select(branch => branch.Name));
        Assert.Equal(diagram.OpenDatabase.Commits.Select(commit => commit.Id), reordered.Commits.Select(commit => commit.Id));
        Assert.Equal((MermaidDiagnosticCodes.RenderFailed, 5), (exception.Code, exception.Location.Line));
        Assert.Contains("Trying to checkout branch which is not yet created. (Help try using \"branch main\")", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Resolves a site configuration for the gitGraph section.
    /// </summary>
    /// <param name="json">The site configuration.</param>
    /// <returns>The configuration.</returns>
    private static MermaidConfig Config(string json) =>
        MermaidConfigResolver.Resolve(Preprocessor.Run("gitGraph\n", null, MermaidLimits.Default), "gitGraph", new MermaidRenderOptions { Config = json }, null);

    /// <summary>
    ///     Parses a git graph with its own configuration (or a given one).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="limits">The limits, or null for the default.</param>
    /// <param name="config">The configuration, or null for the text's own.</param>
    /// <returns>The diagram.</returns>
    private static GitGraphDiagram Parse(string text, MermaidLimits? limits = null, MermaidConfig? config = null)
    {
        var effective = limits ?? MermaidLimits.Default;
        var source = Preprocessor.Run(text, null, effective);
        var textConfig = config ?? MermaidConfigResolver.Resolve(source, diagramId: null, options: null, sink: null);
        return GitGraphParser.Parse(source, DiagramDetector.Detect(source.Code, DetectorSettings.Default), effective, textConfig);
    }
}
