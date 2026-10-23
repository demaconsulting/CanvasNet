using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Groups the look-and-feel and performance settings accepted by <see cref="SceneRenderer"/>.
/// </summary>
/// <remarks>
///     <para>
///         This type is a single, extensible options parameter (rather than a growing argument list)
///         so future settings can be added as new <see langword="init"/>-only properties without
///         breaking the <see cref="SceneRenderer"/> signatures; it is a sealed class with
///         <see langword="init"/>-only properties so object-initializer call sites such as
///         <c>new SceneRenderOptions { Supersampling = 4 }</c> keep compiling when options are
///         added, mirroring the sibling render-options types.
///     </para>
///     <para>
///         Numeric properties are validated when they are set and throw
///         <see cref="ArgumentOutOfRangeException"/> immediately, so an options instance is always
///         valid. Instances are immutable and thread-safe and may be shared between renders.
///     </para>
/// </remarks>
public sealed class SceneRenderOptions
{
    /// <summary>Smallest supported supersampling factor (no antialiasing).</summary>
    private const int MinSupersampling = 1;

    /// <summary>Largest supported supersampling factor (16 samples per pixel).</summary>
    private const int MaxSupersampling = 4;

    /// <summary>Backing field for <see cref="Lights"/>: a private, read-only copy or <see langword="null"/>.</summary>
    private readonly IReadOnlyList<Light>? _lights;

    /// <summary>Backing field for <see cref="SpecularStrength"/>.</summary>
    private readonly float _specularStrength = 0.10f;

    /// <summary>Backing field for <see cref="SpecularPower"/>.</summary>
    private readonly float _specularPower = 32.0f;

    /// <summary>Backing field for <see cref="Supersampling"/>.</summary>
    private readonly int _supersampling = 3;

    /// <summary>Backing field for <see cref="MaxDegreeOfParallelism"/>.</summary>
    private readonly int _maxDegreeOfParallelism = -1;

    /// <summary>
    ///     Gets the default options instance, used by <see cref="SceneRenderer"/> whenever the caller
    ///     passes <see langword="null"/>: opaque white background, neutral gray surfaces, the default
    ///     light rig, 3x3 supersampling and scheduler-chosen parallelism.
    /// </summary>
    public static SceneRenderOptions Default { get; } = new();

    /// <summary>
    ///     Gets the color of pixels no surface covers. Defaults to opaque white (the sibling renderers'
    ///     convention). The alpha channel is honoured: a transparent background yields a transparent
    ///     result with correctly premultiplied antialiased edges (fully transparent pixels are stored as
    ///     <c>(0, 0, 0, 0)</c>).
    /// </summary>
    public Rgba32 BackgroundColor { get; init; } = new(255, 255, 255, 255);

    /// <summary>
    ///     Gets the color of meshes that have no <see cref="SceneModel.NodeAppearance"/>: a neutral
    ///     CAD gray, <c>(180, 180, 180)</c> (alpha ignored).
    /// </summary>
    public Rgba32 DefaultColor { get; init; } = new(180, 180, 180, 255);

    /// <summary>
    ///     Gets the color used where the viewer sees the back of a surface (an inside-out mesh, an
    ///     open mesh or a camera inside a solid): a muted red, <c>(200, 70, 70)</c>, so modelling
    ///     errors are visible (alpha ignored).
    /// </summary>
    public Rgba32 BackFaceColor { get; init; } = new(200, 70, 70, 255);

    /// <summary>
    ///     Gets the lights. <see langword="null"/> (default) selects <see cref="Light.DefaultRig"/>.
    ///     An <b>empty</b> list means no light at all and renders black; use a single
    ///     <see cref="AmbientLight"/> with intensity 1 for flat, unshaded colors. The list is copied
    ///     when set, so later changes to the caller's collection have no effect.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown (from the <see langword="init"/> accessor) when the list contains a <see langword="null"/> light.</exception>
    public IReadOnlyList<Light>? Lights
    {
        get => _lights;
        init
        {
            // Copy and validate once so the renderer can use the list without further checks
            if (value is null)
            {
                _lights = null;
                return;
            }

            var copy = value.ToArray();
            if (Array.Exists(copy, light => light is null))
            {
                throw new ArgumentException("A light must not be null.", nameof(value));
            }

            _lights = Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    ///     Gets the strength of the Blinn-Phong specular highlight in the range 0 to 1; 0 disables
    ///     highlights. Defaults to 0.10 (a weak, CAD-style sheen).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown (from the <see langword="init"/> accessor) when the value is not finite or outside [0, 1].</exception>
    public float SpecularStrength
    {
        get => _specularStrength;
        init
        {
            if (!float.IsFinite(value) || value < 0.0f || value > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The specular strength must be between 0 and 1.");
            }

            _specularStrength = value;
        }
    }

    /// <summary>
    ///     Gets the specular exponent (higher is tighter); finite and greater than zero. Defaults to 32.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown (from the <see langword="init"/> accessor) when the value is not finite and greater than zero.</exception>
    public float SpecularPower
    {
        get => _specularPower;
        init
        {
            if (!float.IsFinite(value) || value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The specular power must be finite and greater than zero.");
            }

            _specularPower = value;
        }
    }

    /// <summary>
    ///     Gets the supersampling factor <c>k</c> (1 to 4): each pixel is shaded at a regular <c>k x k</c>
    ///     grid of samples and resolved with a box filter. 1 disables antialiasing; the cost grows with
    ///     <c>k</c> squared. Defaults to 3.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown (from the <see langword="init"/> accessor) when the value is outside 1 to 4.</exception>
    public int Supersampling
    {
        get => _supersampling;
        init
        {
            if (value < MinSupersampling || value > MaxSupersampling)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The supersampling factor must be between 1 and 4.");
            }

            _supersampling = value;
        }
    }

    /// <summary>
    ///     Gets the maximum number of tiles rendered concurrently: -1 (default) lets the scheduler
    ///     decide, 1 renders sequentially on the calling thread, larger values cap the parallelism.
    ///     The output is byte-identical for every setting.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown (from the <see langword="init"/> accessor) when the value is 0 or less than -1.</exception>
    public int MaxDegreeOfParallelism
    {
        get => _maxDegreeOfParallelism;
        init
        {
            if (value == 0 || value < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The degree of parallelism must be -1 or at least 1.");
            }

            _maxDegreeOfParallelism = value;
        }
    }

    /// <summary>Gets the lights actually used for shading: <see cref="Lights"/> or, when that is <see langword="null"/>, <see cref="Light.DefaultRig"/>.</summary>
    internal IReadOnlyList<Light> EffectiveLights => _lights ?? Light.DefaultRig;
}
