// cspell:ignore minkowski
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests of the modules the geometry builder recognizes but does not support: <c>text</c>, <c>offset</c>,
///     <c>projection</c>, <c>surface</c>, <c>import</c> and <c>roof</c> (<c>hull</c> and <c>minkowski</c> are
///     built; see the hull and minkowski tests). Each is
///     reported as <c>scad-module-{name}</c> at its source location, omitted together with its children, and honors
///     strict mode, the handlers and the modifiers.
/// </summary>
public class ScadUnsupportedModuleTests
{
    /// <summary>Gets the names of the unsupported modules as theory data.</summary>
    public static TheoryData<string> Modules =>
        ["text", "offset", "projection", "surface", "import", "roof"];

    /// <summary>
    ///     Validates that each module is reported with its identifier, location and a message naming it, and that
    ///     nothing inside it is built or reported (its children are not visited).
    /// </summary>
    /// <param name="name">The module name.</param>
    [Theory]
    [MemberData(nameof(Modules))]
    public void ScadUnsupportedModule_Module_IsReportedAndOmittedWithItsChildren(string name)
    {
        // Arrange / Act: the child is an invalid cube that would warn if it were visited
        var result = Build($"\n    {name}() cube(0);");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Empty(result.Warnings);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal($"scad-module-{name}", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 5), report.Location);
        Assert.Contains($"'{name}'", report.Message, StringComparison.Ordinal);
        Assert.Contains("not supported", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that arguments do not matter: a module with realistic arguments is reported the same way.
    /// </summary>
    [Fact]
    public void ScadUnsupportedModule_RealisticArguments_AreReportedOnce()
    {
        // Arrange / Act
        var result = Build("text(\"hi\", size = 5, font = \"x\");\noffset(r = 2) square(4);\nroof() { cube(1); sphere(2); }");

        // Assert
        Assert.Equal(["scad-module-text", "scad-module-offset", "scad-module-roof"], result.Features);
        Assert.Equal(
            [new ScadSourceLocation(null, 1, 1), new ScadSourceLocation(null, 2, 1), new ScadSourceLocation(null, 3, 1)],
            result.Unsupported.Select(u => u.Location).ToArray());
    }

    /// <summary>
    ///     Validates that the supported siblings of an unsupported module still build.
    /// </summary>
    [Fact]
    public void ScadUnsupportedModule_Siblings_StillBuild()
    {
        // Arrange / Act
        var result = Build("cube(1);\nroof() cube(2);\ntranslate([5, 0, 0]) cube(3);");

        // Assert
        Assert.Equal(2, result.Meshes.Count);
        Assert.Equal(["scad-module-roof"], result.Features);
    }

    /// <summary>
    ///     Validates the whole table: of every recognized built-in module, exactly the six unsupported ones are
    ///     reported as unsupported features, so a module added to the evaluator's list cannot be silently dropped.
    /// </summary>
    [Fact]
    public void ScadUnsupportedModule_Table_CoversExactlyTheDocumentedModules()
    {
        // Arrange
        var reported = new SortedSet<string>(StringComparer.Ordinal);

        // Act
        foreach (var name in ScadBuiltinModules.Names)
        {
            foreach (var feature in Build($"{name}();").Features)
            {
                Assert.Equal($"scad-module-{name}", feature);
                reported.Add(name);
            }
        }

        // Assert
        Assert.Equal(
            new[] { "import", "offset", "projection", "roof", "surface", "text" },
            reported.ToArray());
    }

    /// <summary>
    ///     Validates that an unsupported module under transforms and booleans yields nothing there: a transform of
    ///     nothing is nothing, and a difference whose first operand is unsupported is empty.
    /// </summary>
    [Fact]
    public void ScadUnsupportedModule_UnderTransformsAndBooleans_YieldsNothing()
    {
        // Arrange / Act
        var transformed = Build("translate([1, 0, 0]) rotate(5) roof() cube(1);");
        var asMinuend = Build("difference() { offset(1) square(2); square(1); }");
        var asSubtrahend = Build("difference() { cube(2); roof() cube(1); }");

        // Assert
        Assert.Empty(transformed.Meshes);
        Assert.Equal(["scad-module-roof"], transformed.Features);
        Assert.Empty(asMinuend.Meshes);
        Assert.IsType<MeshNode>(asSubtrahend.Scene.Root);
        Assert.Equal(["scad-module-roof"], asSubtrahend.Features);
    }

    /// <summary>
    ///     Validates that a user module of the same name shadows the built-in, so nothing is reported and the
    ///     user's geometry is built.
    /// </summary>
    /// <param name="name">The module name.</param>
    [Theory]
    [MemberData(nameof(Modules))]
    public void ScadUnsupportedModule_UserModuleOfTheSameName_ShadowsTheBuiltin(string name)
    {
        // Arrange / Act
        var result = Build($"module {name}() cube(1);\n{name}();");

        // Assert
        Assert.Equal(1.0, result.SingleMesh.SignedVolume, 1e-12);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates the modifiers on an unsupported module: <c>#</c> adds the highlight report to the module report,
    ///     <c>%</c> omits the module before it is looked at, and <c>!</c> makes it the model (so it is reported and
    ///     the rest is dropped).
    /// </summary>
    [Fact]
    public void ScadUnsupportedModule_Modifiers_BehaveAsForAnyModule()
    {
        // Arrange / Act
        var highlighted = Build("#roof() cube(1);");
        var background = Build("%roof() cube(1);");
        var showOnly = Build("cube(1);\n!offset(1) square(1);");

        // Assert
        Assert.Equal(["scad-modifier-highlight", "scad-module-roof"], highlighted.Features);
        Assert.Equal(["scad-modifier-background"], background.Features);
        Assert.Equal(["scad-module-offset"], showOnly.Features);
        Assert.Empty(showOnly.Meshes);
    }

    /// <summary>
    ///     Validates that the location of a report in a used file carries the file.
    /// </summary>
    [Fact]
    public void ScadUnsupportedModule_InUsedFile_CarriesTheFile()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "\nmodule m() { roof() cube(1); }\n"));

        // Act
        var result = Build("use <lib.scad>\nm();", files: files);

        // Assert
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-module-roof", report.Feature);
        Assert.Equal(new ScadSourceLocation("lib.scad", 2, 14), report.Location);
    }

    /// <summary>
    ///     Validates that strict mode throws for the first unsupported module with its identifier and location,
    ///     leaving the earlier (supported) work built and the report in the sink.
    /// </summary>
    [Fact]
    public void ScadUnsupportedModule_StrictMode_ThrowsAtTheFirst()
    {
        // Arrange
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(
            () => Build("cube(1);\n  roof() square(1);\nsurface(\"f.dat\");", Settings(strict: true), sink: sink));

        // Assert
        Assert.Equal("scad-module-roof", ex.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), ex.Location);
        Assert.Equal(["scad-module-roof"], sink.UnsupportedFeatures.Select(u => u.Feature).ToArray());
    }

    /// <summary>
    ///     Validates the public path: every unsupported module reaches the unsupported-feature handler in document
    ///     order, and strict mode through the public options throws the first.
    /// </summary>
    [Fact]
    public void ScadUnsupportedModule_PublicHandlerAndStrictMode_SeeEveryModule()
    {
        // Arrange
        var features = new List<string>();
        var options = new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { UnsupportedFeatureHandler = u => features.Add($"{u.Feature}@{u.Location}") } };
        var document = ScadDocument.Parse("text(\"a\");\nsurface(\"f.dat\");\nimport(\"f.stl\");\nprojection() cube(1);");

        // Act
        document.BuildScene(options, TestContext.Current.CancellationToken);
        var strict = Assert.Throws<ScadUnsupportedFeatureException>(
            () => document.BuildScene(new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { ThrowOnUnsupportedFeature = true } }, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["scad-module-text@1:1", "scad-module-surface@2:1", "scad-module-import@3:1", "scad-module-projection@4:1"], features);
        Assert.Equal("scad-module-text", strict.Feature);
    }
}
