// cspell:ignore canvasnet scad openscad Minkowski minkowski polyhedron multmatrix rands Fcube
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the OpenSCAD source of the SourceLoader class: probing a model, the named views and their
///     overrides, the variables, the diagnostics, the limits, the include handling, and damaged models.
/// </summary>
public class SourceLoaderScadTests
{
    /// <summary>
    ///     A model with several kinds of statement, which the damage tests cut at every few characters.
    /// </summary>
    private const string SweepModel =
        "width = 12;\n"
        + "function half(x) = x / 2;\n"
        + "module post(h) { translate([half(width), 0, 0]) cylinder(h = h, r = 1, $fn = 8); }\n"
        + "difference() { union() { cube([width, 8, 4]); post(9); } translate([1, 1, -1]) cube([3, 3, 9]); }\n"
        + "for (i = [0 : 2]) translate([i * 4, 10, 0]) rotate([0, 0, i * 30]) cube(2);\n"
        + "echo(str(\"w=\", width));\n";

    /// <summary>
    ///     Loads a model at a small size.
    /// </summary>
    /// <param name="path">The path of the model.</param>
    /// <param name="scad">The view options, or <see langword="null"/> for the defaults.</param>
    /// <param name="size">The width and height in pixels.</param>
    /// <param name="strict"><see langword="true"/> to treat an unsupported feature as an error.</param>
    /// <returns>The loaded source; the caller disposes it.</returns>
    private static LoadedSource Load(string path, ScadViewOptions? scad = null, int size = 64, bool strict = false)
    {
        return SourceLoader.Load(path, new SourceOptions { Width = size, Height = size, Scad = scad, Strict = strict });
    }

    /// <summary>
    ///     Gets the pixels of a surface as one array of packed values, to compare pictures and to use them as keys.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The pixels in row order.</returns>
    private static uint[] Pixels(Surface surface)
    {
        var pixels = new List<uint>();
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                pixels.Add(((uint)pixel.A << 24) | ((uint)pixel.R << 16) | ((uint)pixel.G << 8) | pixel.B);
            }
        }

        return [.. pixels];
    }

    /// <summary>
    ///     Counts the pixels that are not the white background.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The number of covered pixels.</returns>
    private static int CountCovered(Surface surface)
    {
        return Pixels(surface).Count(p => p != 0xFFFFFFFF);
    }

    /// <summary>
    ///     Test that probing a model reads what it declares, including the include and use directives, without drawing it.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_ModelWithDirectives_ListsModulesFunctionsAndDirectives()
    {
        // Arrange: a model that includes one file and uses another; the files do not exist, which probing never notices
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "model.scad",
            "// a comment\ninclude <lib/part.scad>\nuse <lib/fn.scad>\nmodule one() {}\nmodule two() {}\nfunction f(x) = x;\nfunction f(x, y) = x;\none();\n");

        // Act
        var info = SourceLoader.Probe(path);

        // Assert: the names in source order (a repeated name is kept), the directives with their lines, and one page
        Assert.Equal(SourceKind.Scad, info.Format);
        Assert.Equal(["one", "two"], info.Modules);
        Assert.Equal(["f", "f"], info.Functions);
        Assert.Equal(
            [new SourceDirective("include", "lib/part.scad", 2), new SourceDirective("use", "lib/fn.scad", 3)],
            info.Directives);
        Assert.Equal(1, info.PageCount);
        Assert.Empty(info.Pages);
        Assert.Equal((800, 800), (info.Width, info.Height));
    }

    /// <summary>
    ///     Test that a syntax error is reported at probing, with the file, the line, and the column.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_SyntaxError_ThrowsScadErrorWithLocation()
    {
        // Arrange: a model with a syntax error on its second line
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "broken.scad", "cube(1);\ncube(;\n");

        // Act
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Probe(path));

        // Assert: a scad-error on one line that names the file, the line, and the column
        Assert.Equal(ErrorCodes.ScadError, exception.Code);
        Assert.StartsWith("SCAD syntax error: ", exception.Message);
        Assert.Contains($"{path}:2:6", exception.Message);
        Assert.DoesNotContain('\n', exception.Message);
        Assert.IsType<ScadSyntaxException>(exception.InnerException);
    }

    /// <summary>
    ///     Test that a model is drawn at 800 by 800 pixels from the isometric view, orthographic, over white by default.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_DefaultOptions_DrawsIsometricOrthographicAt800()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert: the size, a vector source, no notes, and the render operation that says what was drawn
        Assert.Equal((800, 800), (loaded.Surface.Width, loaded.Surface.Height));
        Assert.True(loaded.IsVector);
        Assert.Equal(1, loaded.Page);
        Assert.Empty(loaded.Diagnostics);
        Assert.Empty(loaded.Echo);
        var operation = Assert.Single(loaded.Operations);
        Assert.Equal("render", operation.Op);
        Assert.Equal(
            ["view", "azimuth", "elevation", "projection", "width", "height", "background"],
            operation.Fields.Select(f => f.Key));
        Assert.Equal("iso", operation.Fields[0].Value);
        Assert.Equal(45.0, operation.Fields[1].Value);
        Assert.Equal(Camera.DefaultElevationDegrees, operation.Fields[2].Value);
        Assert.Equal("orthographic", operation.Fields[3].Value);
        Assert.Equal("#FFFFFF", operation.Fields[6].Value);

        // The corner is the white background and the middle is the model
        Assert.Equal(new Rgba32(255, 255, 255, 255), loaded.Surface[0, 0]);
        Assert.NotEqual(new Rgba32(255, 255, 255, 255), loaded.Surface[400, 400]);
    }

    /// <summary>
    ///     Test that the default picture is the picture the library draws for an orthographic isometric view.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_DefaultOptions_MatchesTheLibraryRender()
    {
        // Arrange: the fixture model, and the library's own render with the same options
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "scad");
        using var expected = ScadDocument.Open(path).Render(
            64,
            64,
            new ScadRenderOptions { Projection = CameraProjection.Orthographic },
            TestContext.Current.CancellationToken);

        // Act
        using var loaded = Load(path);

        // Assert: the same pixels
        SurfaceAssert.Equal(expected, loaded.Surface);
    }

    /// <summary>
    ///     Test that every named view gives a picture of its own, and the same picture every time.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_EachNamedView_IsDistinctAndDeterministic()
    {
        // Arrange: a model that looks different from every side
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "asymmetric.scad", FixtureFactory.AsymmetricModel);
        var pictures = new Dictionary<string, uint[]>();

        foreach (var view in SourceLoader.ScadViews)
        {
            // Act: draw the view twice
            using var first = Load(path, new ScadViewOptions { View = view.Name });
            using var second = Load(path, new ScadViewOptions { View = view.Name });

            // Assert: not empty, not all covered, and the same both times
            Assert.InRange(CountCovered(first.Surface), 100, (64 * 64) - 100);
            Assert.Equal(Pixels(first.Surface), Pixels(second.Surface));
            pictures[view.Name] = Pixels(first.Surface);
        }

        // Assert: no two views produce the same picture
        Assert.Equal(SourceLoader.ScadViews.Count, pictures.Values.Select(p => string.Join(',', p)).Distinct().Count());
    }

    /// <summary>
    ///     Test that the views are the ones the plan names, in the camera convention: azimuth about the Z axis from the front, Z up.
    /// </summary>
    [Fact]
    public void SourceLoader_ScadViews_HaveTheDocumentedAngles()
    {
        // Arrange / Act: the table as (name, azimuth, elevation)
        var table = SourceLoader.ScadViews.Select(v => (v.Name, v.Azimuth, v.Elevation)).ToList();

        // Assert: the order of the help, and the angles
        Assert.Equal(
            [
                ("iso", 45.0, Camera.DefaultElevationDegrees),
                ("front", 0.0, 0.0),
                ("back", 180.0, 0.0),
                ("left", 270.0, 0.0),
                ("right", 90.0, 0.0),
                ("top", 0.0, 90.0),
                ("bottom", 0.0, -90.0)
            ],
            table);
        Assert.Equal(SourceLoader.DefaultScadView, table[0].Name);
    }

    /// <summary>
    ///     Test that the two opposite views are mirror images of each other (the silhouette of the back is the
    ///     silhouette of the front turned over).
    /// </summary>
    /// <param name="first">The first view.</param>
    /// <param name="second">The opposite view.</param>
    [Theory]
    [InlineData("front", "back")]
    [InlineData("left", "right")]
    public void SourceLoader_Load_OppositeViews_HaveMirroredSilhouettes(string first, string second)
    {
        // Arrange: the asymmetric model
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "asymmetric.scad", FixtureFactory.AsymmetricModel);

        // Act
        using var a = Load(path, new ScadViewOptions { View = first });
        using var b = Load(path, new ScadViewOptions { View = second });

        // Assert: covered pixels of one are, to within a pixel of antialiasing, the covered pixels of the other mirrored
        var covered = 0;
        var differing = 0;
        for (var y = 0; y < a.Surface.Height; y++)
        {
            for (var x = 0; x < a.Surface.Width; x++)
            {
                var white = new Rgba32(255, 255, 255, 255);
                var inA = a.Surface[x, y] != white;
                var inB = b.Surface[a.Surface.Width - 1 - x, y] != white;
                covered += inA ? 1 : 0;
                differing += inA != inB ? 1 : 0;
            }
        }

        Assert.True(covered > 100);
        Assert.True(differing * 20 < covered, $"{differing} of {covered} covered pixels differ");
    }

    /// <summary>
    ///     Test that an azimuth or an elevation replaces that of the named view.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_AzimuthAndElevationOverride_ReplaceTheViewAngles()
    {
        // Arrange: the asymmetric model
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "asymmetric.scad", FixtureFactory.AsymmetricModel);

        // Act: the front view turned to azimuth 90, the front view raised to 90 degrees, and the named views they must equal
        using var turned = Load(path, new ScadViewOptions { View = "front", Azimuth = 90 });
        using var right = Load(path, new ScadViewOptions { View = "right" });
        using var raised = Load(path, new ScadViewOptions { View = "front", Elevation = 90 });
        using var top = Load(path, new ScadViewOptions { View = "top" });
        using var isoAzimuth = Load(path, new ScadViewOptions { Azimuth = 0 });

        // Assert: an override gives the same picture as the view with those angles, and an override alone starts from iso
        SurfaceAssert.Equal(right.Surface, turned.Surface);
        SurfaceAssert.Equal(top.Surface, raised.Surface);
        var operation = isoAzimuth.Operations[0];
        Assert.Equal("iso", operation.Fields[0].Value);
        Assert.Equal(0.0, operation.Fields[1].Value);
        Assert.Equal(Camera.DefaultElevationDegrees, operation.Fields[2].Value);
    }

    /// <summary>
    ///     Test that the view name is case-insensitive and that an unknown name is a usage error that lists the views.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_ViewName_IsCaseInsensitiveAndValidated()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        using var upper = Load(path, new ScadViewOptions { View = "TOP" });
        using var lower = Load(path, new ScadViewOptions { View = "top" });
        var unknown = Assert.Throws<ToolException>(() => Load(path, new ScadViewOptions { View = "side" }));

        // Assert: the same picture, and an error that names the valid views
        SurfaceAssert.Equal(lower.Surface, upper.Surface);
        Assert.Equal(ErrorCodes.UsageError, unknown.Code);
        Assert.Contains("'side'", unknown.Message);
        Assert.Contains("iso, front, back, left, right, top, bottom", unknown.Hint);
    }

    /// <summary>
    ///     Test that a perspective view differs from the orthographic one of the same viewpoint, and says so.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_Perspective_DiffersFromOrthographic()
    {
        // Arrange: the asymmetric model
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "asymmetric.scad", FixtureFactory.AsymmetricModel);

        // Act
        using var ortho = Load(path);
        using var perspective = Load(path, new ScadViewOptions { Perspective = true });

        // Assert: different pictures, and the projection is named in the operation
        Assert.NotEqual(Pixels(ortho.Surface), Pixels(perspective.Surface));
        Assert.Equal("perspective", perspective.Operations[0].Fields.Single(f => f.Key == "projection").Value);
    }

    /// <summary>
    ///     Test that a definition replaces the assignment in the model, whether it is a number, a flag, or text.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_Defines_ChangeTheModel()
    {
        // Arrange: a model whose shape depends on a number, a flag, and a text
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "params.scad",
            "length = 10;\nhollow = false;\nkind = \"flat\";\n"
            + "cube([length, 10, kind == \"flat\" ? 2 : 10]);\n"
            + "if (hollow) translate([0, 0, 12]) cube(2);\n");

        // Act
        using var plain = Load(path, new ScadViewOptions { View = "front" });
        using var longer = Load(path, new ScadViewOptions { View = "front", Defines = new Dictionary<string, object?> { ["length"] = 40.0 } });
        using var tall = Load(path, new ScadViewOptions { View = "front", Defines = new Dictionary<string, object?> { ["kind"] = "tall" } });
        using var flagged = Load(path, new ScadViewOptions { View = "front", Defines = new Dictionary<string, object?> { ["hollow"] = true } });

        // Assert: each definition changes the picture
        var pictures = new[] { plain, longer, tall, flagged }.Select(l => string.Join(',', Pixels(l.Surface))).Distinct().Count();
        Assert.Equal(4, pictures);

        // The definitions are listed in the operation as name=value, text in quotes, in name order
        var defines = (IReadOnlyList<string>)flagged.Operations[0].Fields.Single(f => f.Key == "defines").Value;
        Assert.Equal(["hollow=true"], defines);
    }

    /// <summary>
    ///     Test that the definitions in an operation are sorted by name and formatted by type.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_Defines_AreDescribedByNameAndType()
    {
        // Arrange: a model and definitions of three types, given out of order
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "params.scad", "cube(1);\n");
        var defines = new Dictionary<string, object?> { ["zeta"] = "a b", ["alpha"] = 2.5, ["mid"] = true };

        // Act
        using var loaded = Load(path, new ScadViewOptions { Defines = defines });

        // Assert: sorted by name with the value formatted by its type
        var listed = (IReadOnlyList<string>)loaded.Operations[0].Fields.Single(f => f.Key == "defines").Value;
        Assert.Equal(["alpha=2.5", "mid=true", "zeta=\"a b\""], listed);
    }

    /// <summary>
    ///     Test that a definition whose name the library does not accept is a usage error, not an unexpected exception.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_DefineWithInvalidName_ThrowsUsageError()
    {
        // Arrange: a model, and a name with a dollar sign in the middle, which the command line grammar lets through
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        var exception = Assert.Throws<ToolException>(
            () => Load(path, new ScadViewOptions { Defines = new Dictionary<string, object?> { ["a$b"] = 1.0 } }));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("-D", exception.Message);
        Assert.DoesNotContain('\n', exception.Message);
    }

    /// <summary>
    ///     Test that the time sets the special variable $t.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_Time_SetsTheSpecialVariable()
    {
        // Arrange: a model whose length follows $t
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "animated.scad", "cube([1 + $t * 30, 10, 10]);\n");

        // Act
        using var start = Load(path, new ScadViewOptions { View = "front" });
        using var later = Load(path, new ScadViewOptions { View = "front", Time = 1 });

        // Assert: the picture changes with the time, and the time is named in the operation
        Assert.NotEqual(Pixels(start.Surface), Pixels(later.Surface));
        Assert.DoesNotContain(start.Operations[0].Fields, f => f.Key == "time");
        Assert.Equal(1.0, later.Operations[0].Fields.Single(f => f.Key == "time").Value);
    }

    /// <summary>
    ///     Test the size policy for a model: 800 square is its natural size, a scale multiplies it, one side keeps it square, two sides are exact.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SizeOptions_FollowTheNaturalSize()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act / Assert: scale, one side, two sides
        using (var scaled = SourceLoader.Load(path, new SourceOptions { Scale = 0.1 }))
        {
            Assert.Equal((80, 80), (scaled.Surface.Width, scaled.Surface.Height));
        }

        using (var width = SourceLoader.Load(path, new SourceOptions { Width = 50 }))
        {
            Assert.Equal((50, 50), (width.Surface.Width, width.Surface.Height));
        }

        using (var both = SourceLoader.Load(path, new SourceOptions { Width = 60, Height = 30 }))
        {
            Assert.Equal((60, 30), (both.Surface.Width, both.Surface.Height));
            Assert.Equal(new Rgba32(255, 255, 255, 255), both.Surface[0, 0]);
            Assert.NotEqual(new Rgba32(255, 255, 255, 255), both.Surface[30, 15]);
        }

        // A size over the limit is refused before anything is evaluated
        var tooBig = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions { Width = 9000 }));
        Assert.Equal(ErrorCodes.SizeLimit, tooBig.Code);
    }

    /// <summary>
    ///     Test that the background is white by default, can be another color, and can be transparent.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_Background_IsWhiteColoredOrTransparent()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        using var white = SourceLoader.Load(path, new SourceOptions { Width = 32, Height = 32 });
        using var blue = SourceLoader.Load(path, new SourceOptions { Width = 32, Height = 32, Background = new Rgba32(0, 0, 255, 255) });
        using var clear = SourceLoader.Load(path, new SourceOptions { Width = 32, Height = 32, Background = new Rgba32(0, 0, 0, 0) });

        // Assert: the corner shows the background; the model covers the middle in every case
        Assert.Equal(new Rgba32(255, 255, 255, 255), white.Surface[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), blue.Surface[0, 0]);
        Assert.Equal(0, clear.Surface[0, 0].A);
        Assert.Equal(255, clear.Surface[16, 16].A);
        Assert.Equal("#0000FF", blue.Operations[0].Fields.Single(f => f.Key == "background").Value);
        Assert.Equal("#00000000", clear.Operations[0].Fields.Single(f => f.Key == "background").Value);
    }

    /// <summary>
    ///     Test that the default color colors the surfaces the model does not color.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_DefaultColor_ColorsUncoloredSurfaces()
    {
        // Arrange: a plain cube and a cube the model colors itself
        using var directory = new TempDirectory();
        var plain = FixtureFactory.WriteText(directory.Path, "plain.scad", "cube(10, center = true);\n");
        var colored = FixtureFactory.WriteText(directory.Path, "colored.scad", "color(\"blue\") cube(10, center = true);\n");
        var red = new ScadViewOptions { View = "front", DefaultColor = new Rgba32(255, 0, 0, 255) };

        // Act
        using var gray = Load(plain, new ScadViewOptions { View = "front" });
        using var reddened = Load(plain, red);
        using var stillBlue = Load(colored, red);

        // Assert: gray without the option, red with it, and the model's own color wins
        var grayPixel = gray.Surface[32, 32];
        var redPixel = reddened.Surface[32, 32];
        var bluePixel = stillBlue.Surface[32, 32];
        Assert.InRange(Math.Abs(grayPixel.R - grayPixel.B), 0, 3);
        Assert.True(redPixel.R > redPixel.G + 60 && redPixel.R > redPixel.B + 60, $"{redPixel}");
        Assert.True(bluePixel.B > bluePixel.R + 60, $"{bluePixel}");
        Assert.Equal("#FF0000", reddened.Operations[0].Fields.Single(f => f.Key == "defaultColor").Value);
    }

    /// <summary>
    ///     Test that echo output and warnings are returned, and an unsupported feature is a note that does not stop the picture.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_EchoWarningAndUnsupported_AreReturned()
    {
        // Arrange: a model that echoes, calls a module that does not exist, and uses a module the library does not draw
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "noisy.scad", "echo(\"hello\", 3);\nmissing();\nsurface(\"A\");\ncube(1);\n");

        // Act
        using var loaded = Load(path);

        // Assert: the echo line as the library prints it, a warning, and an unsupported note with the feature as its code
        Assert.Equal(["ECHO: \"hello\", 3"], loaded.Echo);
        var warning = Assert.Single(loaded.Diagnostics, d => d.Severity == "warning");
        Assert.Equal(("scad", "scad-warning"), (warning.Source, warning.Code));
        Assert.Contains($"{path}:2:1", warning.Message);
        Assert.Contains("missing", warning.Message);
        var unsupported = Assert.Single(loaded.Diagnostics, d => d.Severity == "unsupported");
        Assert.Equal("scad-module-surface", unsupported.Code);
        Assert.Contains($"{path}:3:1", unsupported.Message);
        Assert.Null(unsupported.Page);
        Assert.NotEqual(0, CountCovered(loaded.Surface));
    }

    /// <summary>
    ///     Test that strict mode turns an unsupported feature into an error.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_UnsupportedFeatureInStrictMode_ThrowsUnsupportedFeature()
    {
        // Arrange: a model that uses a module the library does not draw
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "text.scad", "surface(\"A\");\ncube(1);\n");

        // Act
        var exception = Assert.Throws<ToolException>(() => Load(path, strict: true));

        // Assert: the error names the feature, the location, and the way out
        Assert.Equal(ErrorCodes.UnsupportedFeature, exception.Code);
        Assert.Contains("(scad-module-surface)", exception.Message);
        Assert.Contains($"{path}:1:1", exception.Message);
        Assert.Contains("--strict", exception.Hint);
        Assert.DoesNotContain('\n', exception.Message);
    }

    /// <summary>
    ///     Test that a model that exceeds the triangle limit is a budget error that names the option that raises it.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_TriangleLimitExceeded_ThrowsBudgetExceeded()
    {
        // Arrange: the fixture model and a limit of one triangle
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        var exception = Assert.Throws<ToolException>(() => Load(path, new ScadViewOptions { MaxTriangles = 1 }));

        // Assert: the code, the library's message with its location, and a hint that names the option
        Assert.Equal(ErrorCodes.BudgetExceeded, exception.Code);
        Assert.Contains("Triangle limit exceeded (limit 1)", exception.Message);
        Assert.Contains($"{path}:", exception.Message);
        Assert.Contains("--max-triangles", exception.Hint);
        Assert.DoesNotContain('\n', exception.Message);
    }

    /// <summary>
    ///     Test that a limit that is large enough lets the model through, and that the limit is named in the operation.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_TriangleLimitLargeEnough_Loads()
    {
        // Arrange: the fixture model
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        using var loaded = Load(path, new ScadViewOptions { MaxTriangles = 100_000 });

        // Assert
        Assert.Equal(100_000L, loaded.Operations[0].Fields.Single(f => f.Key == "maxTriangles").Value);
    }

    /// <summary>
    ///     Test that the other limits of the library (recursion, loop iterations) are budget errors too.
    /// </summary>
    /// <param name="source">The text of the model.</param>
    /// <param name="expected">The text the message contains.</param>
    [Theory]
    [InlineData("module m() { m(); }\nm();\n", "Recursion too deep (limit 5000)")]
    [InlineData("for (i = [0 : 1e9]) translate([i, 0, 0]) cube(1);\n", "Loop iteration limit exceeded")]
    public void SourceLoader_Load_EvaluationLimitExceeded_ThrowsBudgetExceeded(string source, string expected)
    {
        // Arrange: a model that runs away
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "runaway.scad", source);

        // Act
        var exception = Assert.Throws<ToolException>(() => Load(path));

        // Assert: the budget code, the message, and a hint that says the limit is fixed
        Assert.Equal(ErrorCodes.BudgetExceeded, exception.Code);
        Assert.Contains(expected, exception.Message);
        Assert.Contains("cannot be changed", exception.Hint);
    }

    /// <summary>
    ///     Test that a failed assertion is an error of the model.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_FailedAssert_ThrowsScadError()
    {
        // Arrange: a model that asserts something false
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "assert.scad", "assert(1 > 2, \"boom\");\ncube(1);\n");

        // Act
        var exception = Assert.Throws<ToolException>(() => Load(path));

        // Assert
        Assert.Equal(ErrorCodes.ScadError, exception.Code);
        Assert.StartsWith("SCAD evaluation error: ", exception.Message);
        Assert.Contains("boom", exception.Message);
        Assert.Contains($"{path}:1:1", exception.Message);
    }

    /// <summary>
    ///     Test that a model with no geometry is drawn as the background.
    /// </summary>
    /// <param name="source">The text of the model.</param>
    [Theory]
    [InlineData("")]
    [InlineData("// nothing but a comment\n")]
    [InlineData("x = 3;\nmodule unused() { cube(1); }\n")]
    public void SourceLoader_Load_ModelWithoutGeometry_IsTheBackground(string source)
    {
        // Arrange: a model that produces nothing
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "nothing.scad", source);

        // Act
        using var loaded = Load(path, size: 16);

        // Assert: every pixel is white
        Assert.Equal(0, CountCovered(loaded.Surface));
    }

    /// <summary>
    ///     Test that an include and a use are resolved from the folder of the model and from the folders below it.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_IncludeAndUse_AreReadFromTheModelFolderAndBelow()
    {
        // Arrange: a model that includes a module from a subfolder and uses a function from another file, and the same model in one file
        using var directory = new TempDirectory();
        var split = FixtureFactory.WriteText(
            directory.Path,
            "model.scad",
            "include <lib/part.scad>\nuse <lib/fn.scad>\nblock(f(3));\n");
        FixtureFactory.WriteText(directory.Path, "lib/part.scad", "module block(s) { cube([s, 4, 4]); }\n");
        FixtureFactory.WriteText(directory.Path, "lib/fn.scad", "function f(x) = x * 2;\n");
        var single = FixtureFactory.WriteText(directory.Path, "single.scad", "module block(s) { cube([s, 4, 4]); }\nfunction f(x) = x * 2;\nblock(f(3));\n");

        // Act
        using var fromParts = Load(split);
        using var fromOneFile = Load(single);

        // Assert: the same picture, which is not empty, and no notes
        SurfaceAssert.Equal(fromOneFile.Surface, fromParts.Surface);
        Assert.NotEqual(0, CountCovered(fromParts.Surface));
        Assert.Empty(fromParts.Diagnostics);
    }

    /// <summary>
    ///     Test that a path in an included file is relative to the file that names it.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_NestedInclude_IsRelativeToTheIncludingFile()
    {
        // Arrange: lib/outer.scad includes inner.scad, which sits next to it in lib, not next to the model
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "model.scad", "include <lib/outer.scad>\nblock();\n");
        FixtureFactory.WriteText(directory.Path, "lib/outer.scad", "include <inner.scad>\n");
        FixtureFactory.WriteText(directory.Path, "lib/inner.scad", "module block() { cube(5); }\n");

        // Act
        using var loaded = Load(path);

        // Assert: the module was found
        Assert.NotEqual(0, CountCovered(loaded.Surface));
        Assert.Empty(loaded.Diagnostics);
    }

    /// <summary>
    ///     Test that an include of a file that does not exist is an error, not a picture without what it asked for.
    /// </summary>
    /// <param name="directive">The directive.</param>
    [Theory]
    [InlineData("include <missing.scad>")]
    [InlineData("use <lib/missing.scad>")]
    public void SourceLoader_Load_MissingInclude_ThrowsScadError(string directive)
    {
        // Arrange: a model that includes or uses a file that does not exist
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "model.scad", directive + "\ncube(1);\n");

        // Act
        var exception = Assert.Throws<ToolException>(() => Load(path));

        // Assert: scad-error that names the include and the place, with a hint about where files are looked up
        Assert.Equal(ErrorCodes.ScadError, exception.Code);
        Assert.StartsWith("SCAD include not found: ", exception.Message);
        Assert.Contains("missing.scad", exception.Message);
        Assert.Contains($"{path}:1:1", exception.Message);
        Assert.Contains("only inside the folder of the model", exception.Hint);
    }

    /// <summary>
    ///     Test that a file outside the folder of the model is never read, even when it exists.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_IncludeOutsideTheModelFolder_ThrowsScadError()
    {
        // Arrange: a model in a subfolder that includes a file next to its folder, which exists
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "outside.scad", "module secret() { cube(1); }\n");
        var path = FixtureFactory.WriteText(directory.Path, "model/model.scad", "include <../outside.scad>\nsecret();\n");

        // Act
        var exception = Assert.Throws<ToolException>(() => Load(path));

        // Assert: refused with the reason
        Assert.Equal(ErrorCodes.ScadError, exception.Code);
        Assert.Contains("is outside the allowed directories", exception.Message);
    }

    /// <summary>
    ///     Test that an include cycle and a syntax error in an included file are errors that name the files.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_IncludeCycleAndBrokenInclude_ThrowScadError()
    {
        // Arrange: two files that include each other, and a model that includes a file with a syntax error
        using var directory = new TempDirectory();
        var cycle = FixtureFactory.WriteText(directory.Path, "a.scad", "include <b.scad>\ncube(1);\n");
        FixtureFactory.WriteText(directory.Path, "b.scad", "include <a.scad>\n");
        var broken = FixtureFactory.WriteText(directory.Path, "model.scad", "include <lib/bad.scad>\ncube(1);\n");
        FixtureFactory.WriteText(directory.Path, "lib/bad.scad", "cube(;\n");

        // Act
        var cycleError = Assert.Throws<ToolException>(() => Load(cycle));
        var brokenError = Assert.Throws<ToolException>(() => Load(broken));

        // Assert: both are scad errors; the first says it is a cycle, the second names the included file and the position
        Assert.Equal(ErrorCodes.ScadError, cycleError.Code);
        Assert.Contains("Include cycle", cycleError.Message);
        Assert.Equal(ErrorCodes.ScadError, brokenError.Code);
        Assert.Contains("SCAD syntax error", brokenError.Message);
        Assert.Contains("bad.scad:1:6", brokenError.Message);
    }

    /// <summary>
    ///     Test that a model longer than the library accepts is reported as corrupt input.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_SourceTooLong_ThrowsCorruptInput()
    {
        // Arrange: a model of 4,194,305 characters, one more than the library reads
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "long.scad", new string(' ', 4_194_305));

        // Act
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Probe(path));

        // Assert: the library's InvalidDataException is corrupt input, in one line
        Assert.Equal(ErrorCodes.CorruptInput, exception.Code);
        Assert.Contains("exceeds the maximum length", exception.Message);
        Assert.DoesNotContain('\n', exception.Message);
    }

    /// <summary>
    ///     Test the mapping of evaluation failures: a limit is a budget error, anything else a scad error.
    /// </summary>
    [Fact]
    public void SourceLoader_MapScadEvaluation_LimitAndOtherFailures_MapToTheirCodes()
    {
        // Arrange: a limit failure, a renderer budget failure, and a plain failure
        var limit = new ScadEvaluationException("Instance limit exceeded (limit 5)", new ScadSourceLocation("m.scad", 3, 1));
        var budget = new ScadEvaluationException("needs more fragments per render tile", new RenderBudgetExceededException(7));
        var plain = new ScadEvaluationException("Assertion failed", new ScadSourceLocation("m.scad", 1, 1));

        // Act / Assert
        var limitError = SourceLoader.MapScadEvaluation(limit);
        Assert.Equal(ErrorCodes.BudgetExceeded, limitError.Code);
        Assert.Contains("m.scad:3:1: Instance limit exceeded (limit 5)", limitError.Message);
        Assert.DoesNotContain("--max-triangles", limitError.Hint);
        Assert.Same(limit, limitError.InnerException);
        Assert.Equal(ErrorCodes.BudgetExceeded, SourceLoader.MapScadEvaluation(budget).Code);
        Assert.Equal(ErrorCodes.ScadError, SourceLoader.MapScadEvaluation(plain).Code);
    }

    /// <summary>
    ///     Test that the SCAD library exceptions are mapped by <c>Read</c> to their error codes.
    /// </summary>
    [Fact]
    public void SourceLoader_Read_ScadExceptions_AreMappedToErrorCodes()
    {
        // Arrange: the exceptions of the SCAD library and the code each maps to
        var location = new ScadSourceLocation("m.scad", 2, 4);
        (Exception Exception, string Code)[] cases =
        [
            (new ScadSyntaxException("Expected ';'", location), ErrorCodes.ScadError),
            (new ScadEvaluationException("Assertion failed", location), ErrorCodes.ScadError),
            (new ScadEvaluationException("Triangle limit exceeded (limit 3)", location), ErrorCodes.BudgetExceeded),
            (new ScadUnsupportedFeatureException("scad-module-surface", "not supported", location), ErrorCodes.UnsupportedFeature)
        ];

        foreach (var (exception, code) in cases)
        {
            // Act
            var mapped = Assert.Throws<ToolException>(() => SourceLoader.Read<int>("m.scad", SourceKind.Scad, () => throw exception));

            // Assert: the code, a single line that carries the location, and the cause kept
            Assert.Equal(code, mapped.Code);
            Assert.Contains("m.scad:2:4", mapped.Message);
            Assert.DoesNotContain('\n', mapped.Message);
            Assert.Same(exception, mapped.InnerException);
        }
    }

    /// <summary>
    ///     Test that cutting a model anywhere, and garbage of several sizes, only ever gives a picture or an expected error.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_TruncatedAndGarbageModels_GiveAPictureOrToolException()
    {
        // Arrange: the model cut after every seventh character, and garbage bytes
        using var directory = new TempDirectory();
        var inputs = new List<(string Name, byte[] Bytes)>();
        for (var length = 0; length <= SweepModel.Length; length += 7)
        {
            inputs.Add(($"cut-{length}", System.Text.Encoding.UTF8.GetBytes(SweepModel[..length])));
        }

        foreach (var length in new[] { 1, 10, 200, 5000 })
        {
            inputs.Add(($"garbage-{length}", FixtureFactory.CreateGarbage(length)));
        }

        foreach (var (name, bytes) in inputs)
        {
            var path = directory.Combine(name + ".scad");
            File.WriteAllBytes(path, bytes);

            // Act: probe and draw it; anything that is not a ToolException fails the test
            var outcome = Record.Exception(() =>
            {
                SourceLoader.Probe(path);
                Load(path, size: 16).Dispose();
            });

            // Assert
            if (outcome != null)
            {
                var exception = Assert.IsType<ToolException>(outcome);
                Assert.Contains(exception.Code, new[] { ErrorCodes.ScadError, ErrorCodes.BudgetExceeded, ErrorCodes.UnsupportedFeature });
            }
        }
    }

    /// <summary>
    ///     Test that models that stress the library (huge numbers, degenerate shapes, unsupported constructs, bad arguments)
    ///     give a picture or an expected error, and never an unexpected exception.
    /// </summary>
    /// <param name="source">The text of the model.</param>
    [Theory]
    [InlineData("cube(1e300);")]
    [InlineData("sphere(r = -1);")]
    [InlineData("cylinder(h = 0, r = 0);")]
    [InlineData("cube([0, 0, 0]);")]
    [InlineData("cube(\"a\");")]
    [InlineData("cube([1, 2]);")]
    [InlineData("translate(1) cube(1);")]
    [InlineData("circle(r = 1e300);")]
    [InlineData("linear_extrude(height = 1e9) circle(1);")]
    [InlineData("scale([1e200, 1e200, 1e200]) cube(1);")]
    [InlineData("rotate([1e308, 0, 0]) cube(1);")]
    [InlineData("rotate_extrude() translate([1, 0]) circle(1);")]
    [InlineData("rotate_extrude() circle(1);")]
    [InlineData("hull() { cube(1); translate([5, 5, 5]) cube(1); }")]
    [InlineData("hull() { }")]
    [InlineData("minkowski() { cube(1); sphere(1); }")]
    [InlineData("polyhedron(points = [], faces = []);")]
    [InlineData("polyhedron(points = [[0, 0, 0]], faces = [[0, 1, 2]]);")]
    [InlineData("polygon([[0, 0], [1, 1]]);")]
    [InlineData("import(\"x.stl\");")]
    [InlineData("surface(file = \"x.dat\");")]
    [InlineData("children();")]
    [InlineData("module a() { b(); } module b() { a(); } a();")]
    [InlineData("multmatrix([[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 0]]) cube(1);")]
    [InlineData("resize([0, 0, 0]) cube(1);")]
    [InlineData("color(\"nonsense\") cube(1);")]
    [InlineData("color([2, 2, 2, 2]) cube(1);")]
    [InlineData("$fn = 1e9; sphere(1);")]
    [InlineData("$fn = 1000000; sphere(1);")]
    [InlineData("echo(1 / 0, -1 / 0, 0 / 0);")]
    [InlineData("x = [for (i = [0 : 1e6]) i]; echo(len(x));")]
    [InlineData("echo(rands(0, 1, 1e7, 42));")]
    [InlineData("echo(norm([1, 2, 3]), cross([1, 0, 0], [0, 1, 0]), search(\"a\", \"abc\"), lookup(1, [[0, 0], [2, 2]]));")]
    [InlineData("assert(true); assert(false);")]
    [InlineData("include <>")]
    [InlineData("use <nothing>")]
    [InlineData("\u00e9\u4e2d = 3; cube(\u00e9\u4e2d);")]
    [InlineData("\uFEFFcube(1);")]
    [InlineData("cube(1)\0;")]
    [InlineData("difference() { cube(10); cube(10); }")]
    [InlineData("intersection() { cube(1); translate([9, 9, 9]) cube(1); }")]
    [InlineData("union() { }")]
    [InlineData("!cube(1); %cube(2); #cube(3); *cube(4);")]
    public void SourceLoader_Load_StressModels_GiveAPictureOrToolException(string source)
    {
        // Arrange: a model in a file
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "stress.scad", source);

        // Act: probe and draw it
        var outcome = Record.Exception(() =>
        {
            SourceLoader.Probe(path);
            Load(path, size: 16).Dispose();
        });

        // Assert: a picture, or an expected error
        if (outcome != null)
        {
            Assert.IsType<ToolException>(outcome);
        }
    }

    /// <summary>
    ///     Test that a model nested deeper than the parser allows is an expected error.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_DeeplyNestedExpression_ThrowsScadError()
    {
        // Arrange: parentheses nested 400 levels deep
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "deep.scad", "x = " + new string('(', 400) + "1" + new string(')', 400) + ";\n");

        // Act
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Probe(path));

        // Assert
        Assert.Equal(ErrorCodes.ScadError, exception.Code);
    }
}
