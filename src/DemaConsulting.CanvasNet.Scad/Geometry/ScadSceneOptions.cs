using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Builds the renderer options for a render: a copy of the caller's <see cref="SceneRenderOptions"/> whose
///     unsupported-feature reporting is replaced by the document's own channel.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="SceneRenderOptions"/> has no clone or <c>with</c> support, so every public property is copied
///         explicitly here; a test reflects over the type so that a property added to Render3D later cannot be
///         forgotten. The copy always has strict mode off and the supplied handler (or none): reporting and strict
///         mode are decided by <see cref="ScadEvaluationOptions"/>, never by the nested scene options.
///     </para>
/// </remarks>
internal static class ScadSceneOptions
{
    /// <summary>The prefix of the identifiers Render3D uses for its operand reports.</summary>
    private const string RenderPrefix = "render3d-";

    /// <summary>The prefix of the identifiers this package reports for them.</summary>
    private const string ScadPrefix = "scad-";

    /// <summary>
    ///     Copies scene options, substituting the unsupported-feature handler and switching strict mode off.
    /// </summary>
    /// <param name="source">The caller's options, or <see langword="null"/> for the Render3D defaults.</param>
    /// <param name="handler">The handler for operand reports, or <see langword="null"/> to request none.</param>
    /// <returns>The copy.</returns>
    public static SceneRenderOptions Create(SceneRenderOptions? source, Action<SceneRenderUnsupportedFeature>? handler)
    {
        var options = source ?? SceneRenderOptions.Default;
        return new SceneRenderOptions
        {
            BackgroundColor = options.BackgroundColor,
            DefaultColor = options.DefaultColor,
            BackFaceColor = options.BackFaceColor,
            Lights = options.Lights,
            SpecularStrength = options.SpecularStrength,
            SpecularPower = options.SpecularPower,
            Supersampling = options.Supersampling,
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            MaxFragmentsPerTile = options.MaxFragmentsPerTile,
            UnsupportedFeatureHandler = handler,
            ThrowOnUnsupportedFeature = false,
        };
    }

    /// <summary>
    ///     Maps a Render3D feature identifier to this package's identifier (<c>render3d-x</c> becomes <c>scad-x</c>).
    /// </summary>
    /// <param name="feature">The Render3D identifier.</param>
    /// <returns>The identifier to report.</returns>
    public static string MapFeature(string feature) =>
        feature.StartsWith(RenderPrefix, StringComparison.Ordinal)
            ? ScadPrefix + feature[RenderPrefix.Length..]
            : ScadPrefix + feature;
}
