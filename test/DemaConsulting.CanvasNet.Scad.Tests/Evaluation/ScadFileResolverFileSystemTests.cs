using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadResolverTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="ScadFileResolver"/> against a real temporary folder.
/// </summary>
public class ScadFileResolverFileSystemTests
{
    /// <summary>
    ///     Loads a file from disk through <see cref="ScadDocument.Open(string, ScadOpenOptions?)"/> and the resolver.
    /// </summary>
    /// <param name="path">The path of the main file.</param>
    /// <param name="options">Optional extra options (library paths).</param>
    /// <returns>The expanded main unit.</returns>
    private static ScadSourceUnit LoadFile(string path, ScadOpenOptions? options = null)
    {
        var document = ScadDocument.Open(path, options);
        var resolver = new ScadFileResolver(document.Options, ScadLimits.Default, new ScadEvaluationSink(ScadLimits.Default), CancellationToken.None);
        return resolver.Load(document.Program, document.SourceName);
    }

    /// <summary>
    ///     Validates that a file opened from disk resolves relative includes against its own folder, and names included files relative to it.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_OpenedFile_ResolvesRelativeIncludes()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        var main = directory.Write("main.scad", "include <parts/a.scad>\nm = 0;\n");
        directory.Write("parts/a.scad", "a = 1;\ninclude <b.scad>\n");
        directory.Write("parts/b.scad", "b = 2;\ninclude <../c.scad>\n");
        directory.Write("c.scad", "c = 3;\n");

        // Act
        var unit = LoadFile(main);

        // Assert
        Assert.Equal(
            ["a@parts/a.scad:1", "b@parts/b.scad:1", "c@c.scad:1", "m@" + main + ":2"],
            Assignments(unit));
    }

    /// <summary>
    ///     Validates that a library path outside the base directory is searched and its files are named by full path.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_LibraryPath_IsSearchedAfterIncludingDirectory()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        var main = directory.Write("project/main.scad", "include <shared.scad>\nuse <tool.scad>\n");
        var shared = directory.Write("lib/shared.scad", "s = 1;\n");
        directory.Write("lib/tool.scad", "t = 1;\n");

        // Act
        var unit = LoadFile(main, new ScadOpenOptions { LibraryPaths = [directory.Combine("lib")] });

        // Assert
        Assert.Equal(["s@" + Path.GetFullPath(shared) + ":1"], Assignments(unit));
        Assert.Equal(Path.GetFullPath(directory.Combine("lib/tool.scad")), Assert.Single(unit.Uses).Name);
    }

    /// <summary>
    ///     Validates that an explicit base directory overrides the folder of the opened file.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_ExplicitBaseDirectory_OverridesFileFolder()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        var main = directory.Write("src/main.scad", "include <x.scad>\n");
        directory.Write("src/x.scad", "wrong = 1;\n");
        directory.Write("other/x.scad", "right = 1;\n");

        // Act
        var unit = LoadFile(main, new ScadOpenOptions { BaseDirectory = directory.Combine("other") });

        // Assert
        Assert.Equal(["right"], AssignmentNames(unit));
    }

    /// <summary>
    ///     Validates that a string document needs a base directory (or library path) to use the file system.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_StringDocument_UsesBaseDirectoryOption()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("inc.scad", "i = 1;\n");

        // Act
        var withBase = Load("include <inc.scad>\n", new ScadOpenOptions { BaseDirectory = directory.Path });
        var withoutBase = Assert.Throws<ScadEvaluationException>(() => Load("include <inc.scad>\n", new ScadOpenOptions()));

        // Assert
        Assert.Equal(["i"], AssignmentNames(withBase));
        Assert.Contains("no base directory", withoutBase.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the main file including itself is detected as a cycle.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_MainFileIncludingItself_IsACycle()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        var main = directory.Write("main.scad", "include <main.scad>\n");

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadFile(main));

        // Assert
        Assert.Contains("Include cycle", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a cycle through the main file from an included file is detected.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_IncludeBackToMain_IsACycle()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        var main = directory.Write("main.scad", "include <a.scad>\n");
        directory.Write("a.scad", "include <main.scad>\n");

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadFile(main));

        // Assert
        Assert.Contains("Include cycle", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a missing file on disk is a warning, not an exception.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_MissingFile_WarnsOnDisk()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        var main = directory.Write("main.scad", "include <nothing.scad>\nx = 1;\n");
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var document = ScadDocument.Open(main);

        // Act
        var unit = new ScadFileResolver(document.Options, ScadLimits.Default, sink, CancellationToken.None).Load(document.Program, document.SourceName);

        // Assert
        Assert.Single(unit.Statements);
        Assert.Equal("Can't open include file 'nothing.scad'", Assert.Single(sink.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates that a directory with the requested name is treated as a missing file.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_DirectoryInsteadOfFile_IsMissing()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        Directory.CreateDirectory(directory.Combine("folder.scad"));
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var options = new ScadOpenOptions { BaseDirectory = directory.Path };

        // Act
        var unit = Load("include <folder.scad>\n", options, sink: sink);

        // Assert
        Assert.Empty(unit.Statements);
        Assert.Single(sink.Diagnostics);
    }

    /// <summary>
    ///     Validates that a UTF-8 byte-order mark in an included file is accepted.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_Utf8BomFile_IsAccepted()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        File.WriteAllText(directory.Combine("bom.scad"), "b = 1;\n", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        // Act
        var unit = Load("include <bom.scad>\n", new ScadOpenOptions { BaseDirectory = directory.Path });

        // Assert
        Assert.Equal(["b"], AssignmentNames(unit));
    }

    /// <summary>
    ///     Validates that file names are matched with the case rules of the platform: case-insensitive on Windows,
    ///     case-sensitive on Linux (macOS depends on the volume and is not asserted).
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_FileNameCase_FollowsPlatform()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("lower.scad", "l = 1;\n");
        var options = new ScadOpenOptions { BaseDirectory = directory.Path };
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var unit = Load("include <LOWER.scad>\n", options, sink: sink);

        // Assert
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(["l"], AssignmentNames(unit));
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Empty(unit.Statements);
            Assert.Single(sink.Diagnostics);
        }
        else
        {
            Assert.True(unit.Statements.Count <= 1);
        }
    }

    /// <summary>
    ///     Validates that the same file reached by two spellings is one file on disk (cache by normalized path).
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_TwoSpellingsOfOnePath_AreOneFile()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("sub/x.scad", "x = 1;\n");
        var options = new ScadOpenOptions { BaseDirectory = directory.Path };

        // Act
        var unit = Load("use <sub/x.scad>\nuse <sub/../sub/./x.scad>\n", options);

        // Assert
        Assert.Single(unit.Uses);
    }
}
