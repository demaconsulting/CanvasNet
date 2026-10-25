using System.Reflection;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad.Geometry;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of <see cref="ScadSceneOptions"/>: the copy of <see cref="SceneRenderOptions"/> with a substituted
///     unsupported-feature handler, and the mapping of Render3D feature identifiers.
/// </summary>
public class ScadSceneOptionsTests
{
    /// <summary>
    ///     The non-default value this test sets for each public property of <see cref="SceneRenderOptions"/>; a
    ///     property missing here fails the reflection test, so a property added to Render3D cannot be forgotten by
    ///     the copy.
    /// </summary>
    private static readonly Dictionary<string, object?> NonDefaults = new()
    {
        [nameof(SceneRenderOptions.BackgroundColor)] = new Rgba32(1, 2, 3, 4),
        [nameof(SceneRenderOptions.DefaultColor)] = new Rgba32(5, 6, 7, 255),
        [nameof(SceneRenderOptions.BackFaceColor)] = new Rgba32(8, 9, 10, 255),
        [nameof(SceneRenderOptions.Lights)] = new Light[] { new AmbientLight { Intensity = 0.25f } },
        [nameof(SceneRenderOptions.SpecularStrength)] = 0.5f,
        [nameof(SceneRenderOptions.SpecularPower)] = 7.0f,
        [nameof(SceneRenderOptions.Supersampling)] = 2,
        [nameof(SceneRenderOptions.MaxDegreeOfParallelism)] = 3,
        [nameof(SceneRenderOptions.MaxFragmentsPerTile)] = 12345L,
        [nameof(SceneRenderOptions.UnsupportedFeatureHandler)] = new Action<SceneRenderUnsupportedFeature>(_ => { }),
        [nameof(SceneRenderOptions.ThrowOnUnsupportedFeature)] = true,
    };

    /// <summary>The properties whose copy is deliberately replaced by <see cref="ScadSceneOptions.Create"/>.</summary>
    private static readonly string[] Replaced =
        [nameof(SceneRenderOptions.UnsupportedFeatureHandler), nameof(SceneRenderOptions.ThrowOnUnsupportedFeature)];

    /// <summary>
    ///     Creates scene options with every property set to its non-default value.
    /// </summary>
    /// <returns>The options.</returns>
    private static SceneRenderOptions CreateNonDefault() => new()
    {
        BackgroundColor = (Rgba32)NonDefaults[nameof(SceneRenderOptions.BackgroundColor)]!,
        DefaultColor = (Rgba32)NonDefaults[nameof(SceneRenderOptions.DefaultColor)]!,
        BackFaceColor = (Rgba32)NonDefaults[nameof(SceneRenderOptions.BackFaceColor)]!,
        Lights = (Light[])NonDefaults[nameof(SceneRenderOptions.Lights)]!,
        SpecularStrength = (float)NonDefaults[nameof(SceneRenderOptions.SpecularStrength)]!,
        SpecularPower = (float)NonDefaults[nameof(SceneRenderOptions.SpecularPower)]!,
        Supersampling = (int)NonDefaults[nameof(SceneRenderOptions.Supersampling)]!,
        MaxDegreeOfParallelism = (int)NonDefaults[nameof(SceneRenderOptions.MaxDegreeOfParallelism)]!,
        MaxFragmentsPerTile = (long)NonDefaults[nameof(SceneRenderOptions.MaxFragmentsPerTile)]!,
        UnsupportedFeatureHandler = (Action<SceneRenderUnsupportedFeature>)NonDefaults[nameof(SceneRenderOptions.UnsupportedFeatureHandler)]!,
        ThrowOnUnsupportedFeature = (bool)NonDefaults[nameof(SceneRenderOptions.ThrowOnUnsupportedFeature)]!,
    };

    /// <summary>
    ///     Validates by reflection that every public property of <see cref="SceneRenderOptions"/> is known to this
    ///     test and is copied, except the two that are replaced.
    /// </summary>
    [Fact]
    public void ScadSceneOptions_Create_EveryPublicProperty_IsCopiedExceptReplacedOnes()
    {
        // Arrange
        var properties = typeof(SceneRenderOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var source = CreateNonDefault();

        // Act
        var copy = ScadSceneOptions.Create(source, handler: null);

        // Assert: the test knows every property (a new Render3D property must be added to the copy and to this test)
        Assert.Equal(NonDefaults.Keys.Order(StringComparer.Ordinal), properties.Select(p => p.Name).Order(StringComparer.Ordinal));
        foreach (var property in properties.Where(p => !Replaced.Contains(p.Name)))
        {
            var expected = property.GetValue(source);
            var actual = property.GetValue(copy);
            if (expected is IEnumerable<Light> expectedLights)
            {
                Assert.Equal(expectedLights, (IEnumerable<Light>)actual!);
            }
            else
            {
                Assert.True(Equals(expected, actual), $"{property.Name} was not copied.");
            }

            // A non-default test value proves the copy rather than a coincidental default
            Assert.False(Equals(property.GetValue(new SceneRenderOptions()), expected), $"{property.Name} needs a non-default test value.");
        }
    }

    /// <summary>
    ///     Validates that the handler is substituted and strict mode is forced off, whatever the source says.
    /// </summary>
    [Fact]
    public void ScadSceneOptions_Create_ReplacesHandlerAndForcesStrictOff()
    {
        // Arrange
        var source = CreateNonDefault();
        Action<SceneRenderUnsupportedFeature> handler = _ => { };

        // Act
        var withHandler = ScadSceneOptions.Create(source, handler);
        var withoutHandler = ScadSceneOptions.Create(source, null);

        // Assert
        Assert.Same(handler, withHandler.UnsupportedFeatureHandler);
        Assert.False(withHandler.ThrowOnUnsupportedFeature);
        Assert.Null(withoutHandler.UnsupportedFeatureHandler);
        Assert.False(withoutHandler.ThrowOnUnsupportedFeature);
        Assert.True(source.ThrowOnUnsupportedFeature);
    }

    /// <summary>
    ///     Validates that a null source yields the Render3D defaults and that the source is left untouched.
    /// </summary>
    [Fact]
    public void ScadSceneOptions_Create_NullSource_UsesRenderDefaults()
    {
        // Arrange / Act
        var copy = ScadSceneOptions.Create(null, null);

        // Assert
        Assert.NotSame(SceneRenderOptions.Default, copy);
        Assert.Equal(SceneRenderOptions.Default.BackgroundColor, copy.BackgroundColor);
        Assert.Equal(SceneRenderOptions.Default.DefaultColor, copy.DefaultColor);
        Assert.Equal(SceneRenderOptions.Default.Supersampling, copy.Supersampling);
        Assert.Equal(SceneRenderOptions.Default.MaxFragmentsPerTile, copy.MaxFragmentsPerTile);
        Assert.Null(copy.Lights);
    }

    /// <summary>
    ///     Validates the mapping of Render3D feature identifiers to this package's identifiers.
    /// </summary>
    /// <param name="render">The Render3D identifier.</param>
    /// <param name="expected">The identifier reported by this package.</param>
    [Theory]
    [InlineData("render3d-csg-open-mesh", "scad-csg-open-mesh")]
    [InlineData("render3d-csg-non-manifold-edge", "scad-csg-non-manifold-edge")]
    [InlineData("render3d-csg-singular-transform", "scad-csg-singular-transform")]
    [InlineData("something-else", "scad-something-else")]
    public void ScadSceneOptions_MapFeature_RewritesPrefix(string render, string expected)
    {
        // Arrange / Act / Assert
        Assert.Equal(expected, ScadSceneOptions.MapFeature(render));
    }
}
