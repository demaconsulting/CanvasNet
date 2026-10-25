using System.Reflection;
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
        };

        // Assert
        Assert.Same(evaluation, options.Evaluation);
        Assert.Same(camera, options.Camera);
        Assert.Equal(-90, options.ViewAzimuthDegrees);
        Assert.Equal(600, options.ViewElevationDegrees);
        Assert.Equal(CameraProjection.Orthographic, options.Projection);
        Assert.Same(scene, options.SceneOptions);
        Assert.Equal(1e-9, options.FlatShapeThickness);
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
