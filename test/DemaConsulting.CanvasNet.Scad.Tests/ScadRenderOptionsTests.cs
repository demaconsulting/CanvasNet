using System.Reflection;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Unit tests for <see cref="ScadRenderOptions"/>: defaults, validation, immutability and composition.
/// </summary>
public class ScadRenderOptionsTests
{
    /// <summary>
    ///     Validates the documented defaults.
    /// </summary>
    [Fact]
    public void ScadRenderOptions_New_HasDocumentedDefaults()
    {
        // Arrange / Act
        var options = new ScadRenderOptions();

        // Assert
        Assert.Null(options.Evaluation);
        Assert.Null(options.Camera);
        Assert.Equal(Camera.DefaultAzimuthDegrees, options.ViewAzimuthDegrees);
        Assert.Equal(Camera.DefaultElevationDegrees, options.ViewElevationDegrees);
        Assert.Equal(CameraProjection.Perspective, options.Projection);
        Assert.Null(options.SceneOptions);
        Assert.Equal(0.1, options.FlatShapeThickness);
        Assert.Equal(new Rgba32(255, 64, 128, 110), options.HighlightColor);
        Assert.Equal(new Rgba32(128, 128, 128, 80), options.GhostColor);
        Assert.Null(options.FontResolution);
    }

    /// <summary>
    ///     Validates the defaults of the font resolution options, that the directory list is copied when it is set
    ///     (a later change of the caller's list has no effect and the exposed list cannot be used to change it), and
    ///     that the options reach the geometry: a font directory supplied through the render options is searched.
    /// </summary>
    [Fact]
    public void ScadRenderOptions_FontResolution_DefaultsAndCopiesDirectories()
    {
        // Arrange: a directory holding only a bold serif face, and a caller's list that changes afterwards
        var defaults = new ScadFontResolutionOptions();
        var directory = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "canvasnet-test-scad-options-" + Guid.NewGuid())).FullName;
        try
        {
            using (var source = typeof(SystemFontCatalog).Assembly.GetManifestResourceStream("DemaConsulting.CanvasNet.Fonts.BundledFonts.LiberationSerif-Bold.ttf")!)
            using (var target = File.Create(Path.Join(directory, "SerifBold.ttf")))
            {
                source.CopyTo(target);
            }

            var callers = new List<string> { directory };
            var fonts = new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = callers };
            callers.Clear();
            var document = ScadDocument.Parse("text(\"H\", font = \"Liberation Serif\");");

            // Act: the same text with and without the directory (without it the bundled regular serif is used)
            var withDirectory = document.BuildScene(new ScadRenderOptions { FontResolution = fonts }, TestContext.Current.CancellationToken).ComputeBounds();
            var bundled = document.BuildScene(new ScadRenderOptions { FontResolution = new ScadFontResolutionOptions { UseSystemFonts = false } }, TestContext.Current.CancellationToken).ComputeBounds();

            // Assert
            Assert.True(defaults.UseSystemFonts);
            Assert.Empty(defaults.AdditionalDirectories);
            Assert.Equal([directory], fonts.AdditionalDirectories);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)fonts.AdditionalDirectories).Add("x"));
            Assert.NotEqual(bundled.Max.X, withDirectory.Max.X, 3);
            Assert.Equal(10.0, withDirectory.Max.Y, 1e-4);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that a null list, and a null, empty or white-space directory are rejected when the options are
    ///     initialized.
    /// </summary>
    [Fact]
    public void ScadRenderOptions_FontResolution_InvalidDirectoriesAreRejected()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new ScadFontResolutionOptions { AdditionalDirectories = null! });
        Assert.Throws<ArgumentException>(() => new ScadFontResolutionOptions { AdditionalDirectories = [null!] });
        Assert.Throws<ArgumentException>(() => new ScadFontResolutionOptions { AdditionalDirectories = ["fonts", string.Empty] });
        Assert.Throws<ArgumentException>(() => new ScadFontResolutionOptions { AdditionalDirectories = ["   "] });
        Assert.Contains(
            typeof(ScadFontResolutionOptions).GetProperties(),
            p => p.Name == nameof(ScadFontResolutionOptions.UseSystemFonts) && p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit"));
    }

    /// <summary>
    ///     Validates that every property accepts a valid value and keeps it.
    /// </summary>
    [Fact]
    public void ScadRenderOptions_Initialization_AcceptsValidValues()
    {
        // Arrange
        var evaluation = new ScadEvaluationOptions { Time = 0.5 };
        var camera = Camera.LookAt(new Vector3D(10, 10, 10), Vector3D.Zero, Vector3D.UnitZ);
        var scene = new SceneRenderOptions { Supersampling = 2 };

        // Act
        var options = new ScadRenderOptions
        {
            Evaluation = evaluation,
            Camera = camera,
            ViewAzimuthDegrees = -90,
            ViewElevationDegrees = 600,
            Projection = CameraProjection.Orthographic,
            SceneOptions = scene,
            FlatShapeThickness = 1e-9,
            HighlightColor = new Rgba32(1, 2, 3, 0),
            GhostColor = new Rgba32(4, 5, 6, 255),
        };

        // Assert
        Assert.Same(evaluation, options.Evaluation);
        Assert.Same(camera, options.Camera);
        Assert.Equal(-90, options.ViewAzimuthDegrees);
        Assert.Equal(600, options.ViewElevationDegrees);
        Assert.Equal(CameraProjection.Orthographic, options.Projection);
        Assert.Same(scene, options.SceneOptions);
        Assert.Equal(1e-9, options.FlatShapeThickness);
        Assert.Equal(new Rgba32(1, 2, 3, 0), options.HighlightColor);
        Assert.Equal(new Rgba32(4, 5, 6, 255), options.GhostColor);
    }

    /// <summary>
    ///     Validates that the default highlight and ghost colors are translucent, and that overriding them changes the
    ///     picture: an opaque ghost and an opaque highlight are drawn in exactly their colors (flat lighting, top view).
    /// </summary>
    [Fact]
    public void ScadRenderOptions_HighlightColorAndGhostColor_DefaultsAndOverrideChangeTheLook()
    {
        // Arrange
        var flat = new SceneRenderOptions { Lights = [new AmbientLight { Intensity = 1.0f }], Supersampling = 1 };
        var camera = new Camera
        {
            Eye = new Vector3D(15, 5, 100),
            Target = new Vector3D(15, 5, 0),
            Up = Vector3D.UnitY,
            Projection = CameraProjection.Orthographic,
            OrthographicHeight = 20,
        };
        var document = ScadDocument.Parse("#cube(10);\n%translate([20, 0, 0]) cube(10);");
        var green = new Rgba32(0, 255, 0, 255);
        var magenta = new Rgba32(255, 0, 255, 255);

        // Act: 60 by 20 pixels cover x -15..45 and y -5..15; the highlighted cube is at columns 15..25, the ghost at 35..45
        using var wide = document.Render(60, 20, new ScadRenderOptions { Camera = camera, SceneOptions = flat }, TestContext.Current.CancellationToken);
        using var custom = document.Render(60, 20, new ScadRenderOptions { Camera = camera, SceneOptions = flat, HighlightColor = magenta, GhostColor = green }, TestContext.Current.CancellationToken);

        // Assert: the defaults blend (neither the exact option color nor the background); the overrides are exact
        Assert.NotEqual(magenta, wide[20, 10]);
        Assert.NotEqual(green, wide[40, 10]);
        Assert.NotEqual(new Rgba32(255, 255, 255, 255), wide[40, 10]);
        Assert.Equal(magenta, custom[20, 10]);
        Assert.Equal(green, custom[40, 10]);
    }

    /// <summary>
    ///     Validates that a non-finite view angle is rejected with the property name.
    /// </summary>
    /// <param name="value">The angle.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ScadRenderOptions_ViewAngles_NonFinite_Throw(double value)
    {
        // Arrange / Act
        var azimuth = Assert.Throws<ArgumentOutOfRangeException>(() => new ScadRenderOptions { ViewAzimuthDegrees = value });
        var elevation = Assert.Throws<ArgumentOutOfRangeException>(() => new ScadRenderOptions { ViewElevationDegrees = value });

        // Assert
        Assert.Equal(nameof(ScadRenderOptions.ViewAzimuthDegrees), azimuth.ParamName);
        Assert.Equal(nameof(ScadRenderOptions.ViewElevationDegrees), elevation.ParamName);
    }

    /// <summary>
    ///     Validates that an undefined projection is rejected.
    /// </summary>
    [Fact]
    public void ScadRenderOptions_Projection_Undefined_Throws()
    {
        // Arrange / Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ScadRenderOptions { Projection = (CameraProjection)99 });

        // Assert
        Assert.Equal(nameof(ScadRenderOptions.Projection), ex.ParamName);
    }

    /// <summary>
    ///     Validates that the slab thickness must be finite and positive.
    /// </summary>
    /// <param name="value">The thickness.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData(0.0, false)]
    [InlineData(-1.0, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.Epsilon, true)]
    [InlineData(5.0, true)]
    public void ScadRenderOptions_FlatShapeThickness_MustBeFiniteAndPositive(double value, bool valid)
    {
        // Arrange / Act
        var act = () => new ScadRenderOptions { FlatShapeThickness = value };

        // Assert
        if (valid)
        {
            Assert.Equal(value, act().FlatShapeThickness);
        }
        else
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(act);
            Assert.Equal(nameof(ScadRenderOptions.FlatShapeThickness), ex.ParamName);
        }
    }

    /// <summary>
    ///     Validates that the options are immutable: every public property is init-only.
    /// </summary>
    [Fact]
    public void ScadRenderOptions_Properties_AreInitOnly()
    {
        // Arrange
        var properties = typeof(ScadRenderOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Act / Assert
        Assert.NotEmpty(properties);
        foreach (var property in properties)
        {
            var setter = property.SetMethod;
            Assert.NotNull(setter);
            Assert.Contains(setter.ReturnParameter.GetRequiredCustomModifiers(), m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
        }
    }

    /// <summary>
    ///     Validates that the evaluation options are composed, not copied: the instance is kept and may be omitted,
    ///     and the options object can be shared between calls.
    /// </summary>
    [Fact]
    public void ScadRenderOptions_Evaluation_IsComposedAndOptional()
    {
        // Arrange
        var evaluation = new ScadEvaluationOptions { Limits = new ScadLimits { MaxTriangles = 11 } };
        var document = ScadDocument.Parse("cube(1);");

        // Act
        var shared = new ScadRenderOptions { Evaluation = evaluation };
        var first = Assert.Throws<ScadEvaluationException>(() => document.BuildScene(shared, TestContext.Current.CancellationToken));
        var second = Assert.Throws<ScadEvaluationException>(() => document.BuildScene(shared, TestContext.Current.CancellationToken));
        var defaults = document.BuildScene(new ScadRenderOptions { Evaluation = null }, TestContext.Current.CancellationToken);

        // Assert: a cube needs 12 triangles, so the limit that stopped it came from the composed options
        Assert.Contains("limit 11", first.Message, StringComparison.Ordinal);
        Assert.Equal(first.Message, second.Message);
        Assert.Single(Geometry.ScadGeometryTestSupport.MeshNodes(defaults));
    }
}
