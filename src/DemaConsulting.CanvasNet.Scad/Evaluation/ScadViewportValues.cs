using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The values of the four viewport special variables: <c>$vpt</c> (the point the camera looks at), <c>$vpr</c>
///     (the camera rotation in degrees), <c>$vpd</c> (the distance to the target) and <c>$vpf</c> (the vertical field
///     of view in degrees).
/// </summary>
/// <remarks>
///     The values are the defaults a document sees in its special variables (see
///     <see cref="ScadEvaluationSettings.Viewport"/>); the names and their meaning follow the documented viewport
///     variables of the OpenSCAD language.
/// </remarks>
/// <param name="Target">The value of <c>$vpt</c>.</param>
/// <param name="Rotation">The value of <c>$vpr</c> in degrees (rotation about X, Y and Z, applied in that order like <c>rotate()</c>).</param>
/// <param name="Distance">The value of <c>$vpd</c>.</param>
/// <param name="FieldOfView">The value of <c>$vpf</c> in degrees.</param>
internal readonly record struct ScadViewportValues(Vector3D Target, Vector3D Rotation, double Distance, double FieldOfView)
{
    /// <summary>The name of the special variable that holds the target point.</summary>
    public const string TargetName = "$vpt";

    /// <summary>The name of the special variable that holds the rotation.</summary>
    public const string RotationName = "$vpr";

    /// <summary>The name of the special variable that holds the distance.</summary>
    public const string DistanceName = "$vpd";

    /// <summary>The name of the special variable that holds the field of view.</summary>
    public const string FieldOfViewName = "$vpf";

    /// <summary>The default distance (<c>$vpd</c>) in model units.</summary>
    public const double DefaultDistance = 140.0;

    /// <summary>The default vertical field of view (<c>$vpf</c>) in degrees.</summary>
    public const double DefaultFieldOfView = 22.5;

    /// <summary>
    ///     Gets the documented defaults of a fresh viewer: target at the origin, rotation <c>[55, 0, 25]</c>, distance
    ///     140 and a field of view of 22.5 degrees.
    /// </summary>
    public static ScadViewportValues OpenScadDefaults { get; } = new(Vector3D.Zero, new Vector3D(55.0, 0.0, 25.0), DefaultDistance, DefaultFieldOfView);

    /// <summary>
    ///     Gets the value of <c>$vpt</c> as a vector value.
    /// </summary>
    /// <returns>The value.</returns>
    public ScadValue TargetValue() => ToVector(Target);

    /// <summary>
    ///     Gets the value of <c>$vpr</c> as a vector value.
    /// </summary>
    /// <returns>The value.</returns>
    public ScadValue RotationValue() => ToVector(Rotation);

    /// <summary>
    ///     Converts a 3-component vector into a SCAD list of three numbers.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The value.</returns>
    public static ScadValue ToVector(Vector3D vector) =>
        ScadValue.FromVector([ScadValue.FromNumber(vector.X), ScadValue.FromNumber(vector.Y), ScadValue.FromNumber(vector.Z)]);
}

/// <summary>
///     The viewport variables that the document itself (or an override from outside) defined at the top level, as
///     read after the main file was evaluated; a member is <see langword="null"/> when that variable was not defined
///     or its value was not usable.
/// </summary>
/// <param name="Target">The valid value of <c>$vpt</c>, or <see langword="null"/>.</param>
/// <param name="Rotation">The valid value of <c>$vpr</c>, or <see langword="null"/>.</param>
/// <param name="Distance">The valid value of <c>$vpd</c>, or <see langword="null"/>.</param>
/// <param name="FieldOfView">The valid value of <c>$vpf</c>, or <see langword="null"/>.</param>
internal sealed record ScadDocumentViewport(Vector3D? Target, Vector3D? Rotation, double? Distance, double? FieldOfView)
{
    /// <summary>Gets the result for a document that defines none of the variables.</summary>
    public static ScadDocumentViewport None { get; } = new(null, null, null, null);

    /// <summary>
    ///     Gets a value indicating whether the target, the distance or the field of view was defined, which selects a
    ///     manual camera instead of the automatic framing.
    /// </summary>
    public bool IsManual => Target is not null || Distance is not null || FieldOfView is not null;
}
