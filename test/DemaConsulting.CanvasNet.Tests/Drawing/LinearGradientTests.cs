using System.Numerics;
using System.Reflection;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for <see cref="LinearGradient"/> and the shared <see cref="Gradient"/> base
///     class validation/normalization behavior (exercised through <see cref="LinearGradient"/>,
///     since <see cref="Gradient"/> itself has no public constructor).
/// </summary>
public class LinearGradientTests
{
    private static GradientStop[] OneStop() => [new GradientStop(0f, new Rgba32(1, 2, 3, 4))];

    /// <summary>
    ///     Proves that <see cref="Gradient"/>'s constructor is <see langword="private protected"/>
    ///     (family-and-assembly), not merely <see langword="protected"/> - a closed type
    ///     hierarchy where only in-assembly subtypes (<see cref="LinearGradient"/> and
    ///     <see cref="RadialGradient"/>) can derive from it, matching the exhaustive
    ///     <see cref="LinearGradient"/>/<see cref="RadialGradient"/> pattern match performed by
    ///     <c>GradientEvaluator.EvaluatePoint</c>.
    /// </summary>
    [Fact]
    public void Gradient_Constructor_IsPrivateProtected()
    {
        var constructor = typeof(Gradient).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single();

        // "Family and assembly" is the CLR accessibility term for C#'s "private protected".
        Assert.True(constructor.IsFamilyAndAssembly);
    }

    /// <summary>
    ///     Proves that the constructor rejects a null stops list.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_NullStops_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LinearGradient(Vector2.Zero, Vector2.One, null!));
    }

    /// <summary>
    ///     Proves that the constructor rejects an empty stops list.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_EmptyStops_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new LinearGradient(Vector2.Zero, Vector2.One, []));
    }

    /// <summary>
    ///     Proves that the constructor rejects an undefined GradientSpread value.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_UndefinedSpread_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LinearGradient(Vector2.Zero, Vector2.One, OneStop(), (GradientSpread)42));
    }

    /// <summary>
    ///     Proves that the constructor rejects a transform with a NaN component.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_TransformWithNaNComponent_ThrowsArgumentOutOfRangeException()
    {
        var transform = new Matrix3x2(float.NaN, 0, 0, 1, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LinearGradient(Vector2.Zero, Vector2.One, OneStop(), transform: transform));
    }

    /// <summary>
    ///     Proves that the constructor rejects a transform with an infinity component.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_TransformWithInfinityComponent_ThrowsArgumentOutOfRangeException()
    {
        var transform = new Matrix3x2(1, 0, 0, float.PositiveInfinity, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LinearGradient(Vector2.Zero, Vector2.One, OneStop(), transform: transform));
    }

    /// <summary>
    ///     Proves that a caller-omitted (default) transform is treated as the identity matrix,
    ///     rather than the singular all-zero matrix default(Matrix3x2) would otherwise produce.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_DefaultTransform_IsIdentity()
    {
        var gradient = new LinearGradient(Vector2.Zero, Vector2.One, OneStop());

        Assert.Equal(Matrix3x2.Identity, gradient.Transform);
    }

    /// <summary>
    ///     Proves that an explicitly-supplied all-zero transform (<see langword="default"/>(<see cref="Matrix3x2"/>))
    ///     is preserved exactly as given, rather than being silently replaced with the identity
    ///     matrix - distinguishing "the caller omitted the argument" from "the caller explicitly
    ///     passed the all-zero matrix", which a <c>transform == default</c> sentinel check cannot.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_ExplicitAllZeroTransform_IsPreservedNotReplacedWithIdentity()
    {
        var gradient = new LinearGradient(Vector2.Zero, Vector2.One, OneStop(), transform: default(Matrix3x2));

        Assert.Equal(default, gradient.Transform);
        Assert.NotEqual(Matrix3x2.Identity, gradient.Transform);
    }

    /// <summary>
    ///     Proves that every constructor argument round-trips through its corresponding property.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_ValidValues_PropertiesRoundTrip()
    {
        var start = new Vector2(1, 2);
        var end = new Vector2(3, 4);
        var stops = OneStop();
        var transform = Matrix3x2.CreateScale(2f);

        var gradient = new LinearGradient(start, end, stops, GradientSpread.Reflect, transform);

        Assert.Equal(start, gradient.Start);
        Assert.Equal(end, gradient.End);
        Assert.Equal(GradientSpread.Reflect, gradient.Spread);
        Assert.Equal(transform, gradient.Transform);
        Assert.Single(gradient.Stops);
    }

    /// <summary>
    ///     Proves that stops supplied out of order are sorted ascending by offset.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_UnsortedStops_AreSortedAscendingByOffset()
    {
        GradientStop[] stops =
        [
            new GradientStop(1f, new Rgba32(3, 3, 3, 3)),
            new GradientStop(0f, new Rgba32(1, 1, 1, 1)),
            new GradientStop(0.5f, new Rgba32(2, 2, 2, 2)),
        ];

        var gradient = new LinearGradient(Vector2.Zero, Vector2.One, stops);

        Assert.Equal(0f, gradient.Stops[0].Offset);
        Assert.Equal(0.5f, gradient.Stops[1].Offset);
        Assert.Equal(1f, gradient.Stops[2].Offset);
    }

    /// <summary>
    ///     Proves that two or more stops sharing the same offset preserve their caller-supplied
    ///     relative order (a stable sort), rather than being reordered arbitrarily.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_DuplicateOffsetStops_PreservesInputOrderAmongTies()
    {
        var first = new GradientStop(0.5f, new Rgba32(1, 0, 0, 255));
        var second = new GradientStop(0.5f, new Rgba32(0, 1, 0, 255));
        GradientStop[] stops = [first, second];

        var gradient = new LinearGradient(Vector2.Zero, Vector2.One, stops);

        Assert.Equal(first.Color, gradient.Stops[0].Color);
        Assert.Equal(second.Color, gradient.Stops[1].Color);
    }

    /// <summary>
    ///     Proves that Start equal to End (a zero-length gradient vector) is accepted at
    ///     construction, not rejected - it is a documented degenerate evaluation case, not a
    ///     constructor error.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_StartEqualsEnd_Succeeds()
    {
        var point = new Vector2(5, 5);

        var gradient = new LinearGradient(point, point, OneStop());

        Assert.Equal(point, gradient.Start);
        Assert.Equal(point, gradient.End);
    }

    /// <summary>
    ///     Proves that the constructor rejects a non-finite Start component.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_NonFiniteStart_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LinearGradient(new Vector2(float.NaN, 0), Vector2.One, OneStop()));
    }

    /// <summary>
    ///     Proves that the constructor rejects a non-finite End component.
    /// </summary>
    [Fact]
    public void LinearGradient_Constructor_NonFiniteEnd_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LinearGradient(Vector2.Zero, new Vector2(float.PositiveInfinity, 0), OneStop()));
    }
}
