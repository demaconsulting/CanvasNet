// cspell:ignore surfacescale specularconstant diffuseconstant specularexponent azimuth elevation
// cspell:ignore pointsat limitingconeangle lightingcolor lambertian blinn phong sobel
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    /// <summary>
    ///     Identifies which of the three SVG light-source element kinds a <c>feDiffuseLighting</c>/
    ///     <c>feSpecularLighting</c> primitive's single light-source child resolved to.
    /// </summary>
    private enum LightKind
    {
        /// <summary>A <c>feDistantLight</c>: a constant, position-independent direction.</summary>
        Distant,

        /// <summary>A <c>fePointLight</c>: an omnidirectional point source at a fixed position.</summary>
        Point,

        /// <summary>A <c>feSpotLight</c>: a directional, cone-limited point source.</summary>
        Spot
    }

    /// <summary>
    ///     Holds a <c>feDistantLight</c>/<c>fePointLight</c>/<c>feSpotLight</c> child element's
    ///     attributes, already resolved into pixel space (via the same <c>transform *
    ///     Matrix3x2.CreateTranslation(-regionPixelX, -regionPixelY)</c> mapping
    ///     <see cref="ApplyFeImage"/> uses) so the per-pixel lighting loop never has to touch
    ///     local-space coordinates or the document transform again.
    /// </summary>
    /// <remarks>
    ///     Immutable once constructed by <see cref="ResolveLightSource"/>; safe to reuse across
    ///     every pixel of a single primitive evaluation.
    /// </remarks>
    private sealed class ResolvedLight
    {
        /// <summary>Gets which light-source kind this instance represents.</summary>
        public required LightKind Kind { get; init; }

        /// <summary>
        ///     Gets the constant, already-unit-length light vector for <see cref="LightKind.Distant"/>;
        ///     unused for the other two kinds.
        /// </summary>
        public Vector3 DistantDirection { get; init; }

        /// <summary>
        ///     Gets the light's own position in pixel space (X/Y via the accumulated transform,
        ///     Z via the local-to-pixel <c>scale</c> factor) for <see cref="LightKind.Point"/> and
        ///     <see cref="LightKind.Spot"/>; unused for <see cref="LightKind.Distant"/>.
        /// </summary>
        public Vector3 PositionPixel { get; init; }

        /// <summary>
        ///     Gets the unit vector from the light's position toward its <c>pointsAt</c> target, in
        ///     pixel space, for <see cref="LightKind.Spot"/>; unused for the other two kinds.
        /// </summary>
        public Vector3 SpotDirectionUnit { get; init; }

        /// <summary>
        ///     Gets the <c>feSpotLight</c> element's own <c>specularExponent</c> attribute (default
        ///     <c>1</c>), which controls the cone's edge falloff sharpness; unused for the other two
        ///     kinds.
        /// </summary>
        public float SpotFalloffExponent { get; init; }

        /// <summary>
        ///     Gets the cosine of the <c>feSpotLight</c> element's <c>limitingConeAngle</c> attribute
        ///     (converted from degrees), or <see langword="null"/> when the attribute is absent - in
        ///     which case the cone cutoff is not enforced. Unused for the other two kinds.
        /// </summary>
        public float? CosLimitingConeAngle { get; init; }

        /// <summary>
        ///     Gets the resolved <c>lighting-color</c> as 0-1 RGB components (its alpha channel is
        ///     not part of the SVG lighting-color model and is intentionally never read).
        /// </summary>
        public required Vector3 Color { get; init; }
    }

    /// <summary>
    ///     Evaluates one <c>feDiffuseLighting</c> primitive against <paramref name="input"/>'s
    ///     straight-alpha channel, treated as a Sobel-estimated bump map, using the SVG
    ///     specification's Lambertian (<c>N.L</c>) diffuse lighting formula.
    /// </summary>
    /// <param name="element">The <c>feDiffuseLighting</c> element.</param>
    /// <param name="input">The already-resolved input buffer, whose alpha channel is the bump map.</param>
    /// <param name="scale">The local-space-to-pixel-space scale factor (see <see cref="EstimateUniformScale"/>).</param>
    /// <param name="transform">The referencing element's accumulated transform.</param>
    /// <param name="regionPixelX">The enclosing filter region's absolute pixel-space X origin.</param>
    /// <param name="regionPixelY">The enclosing filter region's absolute pixel-space Y origin.</param>
    /// <returns>
    ///     A new, independent, fully opaque output buffer (per spec, <c>feDiffuseLighting</c>'s
    ///     alpha is always <c>1.0</c>).
    /// </returns>
    /// <remarks>
    ///     <c>N.L</c> is clamped to <c>&gt;= 0</c> before use: a surface patch facing away from the
    ///     light contributes no light, and a negative dot product would otherwise produce a
    ///     negative, meaningless color contribution. When the primitive has no recognized
    ///     <c>feDistantLight</c>/<c>fePointLight</c>/<c>feSpotLight</c> child, this is a tolerant
    ///     "no light" fallback: every pixel is fully opaque black.
    /// </remarks>
    private static Surface ApplyFeDiffuseLighting(XElement element, Surface input, float scale, Matrix3x2 transform, int regionPixelX, int regionPixelY)
    {
        var surfaceScalePixel = (ParseFirstNumberToken((string?)element.Attribute("surfaceScale")) ?? 1f) * scale;
        var diffuseConstant = ParseFirstNumberToken((string?)element.Attribute("diffuseConstant")) ?? 1f;
        var lightingColor = ResolveLightingColor(element);
        var light = ResolveLightSource(element, transform, regionPixelX, regionPixelY, scale, lightingColor);

        var width = input.Width;
        var height = input.Height;
        var output = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            var outputRow = output.GetRowSpan(y);
            for (var x = 0; x < width; x++)
            {
                var normal = ComputeSurfaceNormal(input, x, y, surfaceScalePixel);
                var i = ReadStraightAlpha(input, x, y);
                var (l, lightColor) = ComputeLightAt(light, x, y, surfaceScalePixel * i);

                var nDotL = Math.Max(Vector3.Dot(normal, l), 0f);
                var diffuse = diffuseConstant * nDotL * lightColor;
                outputRow[x] = new Rgba32(
                    ToByte(diffuse.X * 255f),
                    ToByte(diffuse.Y * 255f),
                    ToByte(diffuse.Z * 255f),
                    255);
            }
        }

        return output;
    }

    /// <summary>
    ///     Evaluates one <c>feSpecularLighting</c> primitive against <paramref name="input"/>'s
    ///     straight-alpha channel, treated as a Sobel-estimated bump map, using the SVG
    ///     specification's Blinn-Phong (<c>N.H</c>) specular lighting formula.
    /// </summary>
    /// <param name="element">The <c>feSpecularLighting</c> element.</param>
    /// <param name="input">The already-resolved input buffer, whose alpha channel is the bump map.</param>
    /// <param name="scale">The local-space-to-pixel-space scale factor (see <see cref="EstimateUniformScale"/>).</param>
    /// <param name="transform">The referencing element's accumulated transform.</param>
    /// <param name="regionPixelX">The enclosing filter region's absolute pixel-space X origin.</param>
    /// <param name="regionPixelY">The enclosing filter region's absolute pixel-space Y origin.</param>
    /// <returns>A new, independent output buffer.</returns>
    /// <remarks>
    ///     Per spec, the output alpha is <c>max(R, G, B)</c> of the computed specular color - it is
    ///     deliberately <b>not</b> forced to <c>1.0</c> (unlike <see cref="ApplyFeDiffuseLighting"/>),
    ///     which is what lets a specular highlight be composited additively over its input without
    ///     obscuring it. <c>N.H</c> is clamped to <c>&gt;= 0</c> before <c>pow</c>, both because a
    ///     surface facing away from the halfway vector contributes no highlight and because
    ///     <see cref="MathF.Pow(float, float)"/> of a negative base with a non-integer exponent is
    ///     undefined/<c>NaN</c> - the clamp is a correctness necessity, not only a lighting-realism
    ///     choice. When the primitive has no recognized
    ///     <c>feDistantLight</c>/<c>fePointLight</c>/<c>feSpotLight</c> child, this is a tolerant
    ///     "no light" fallback: every pixel is fully transparent black.
    /// </remarks>
    private static Surface ApplyFeSpecularLighting(XElement element, Surface input, float scale, Matrix3x2 transform, int regionPixelX, int regionPixelY)
    {
        var surfaceScalePixel = (ParseFirstNumberToken((string?)element.Attribute("surfaceScale")) ?? 1f) * scale;
        var specularConstant = ParseFirstNumberToken((string?)element.Attribute("specularConstant")) ?? 1f;
        var specularExponent = ParseFirstNumberToken((string?)element.Attribute("specularExponent")) ?? 1f;
        var lightingColor = ResolveLightingColor(element);
        var light = ResolveLightSource(element, transform, regionPixelX, regionPixelY, scale, lightingColor);

        const float viewerZ = 1f;
        var eye = new Vector3(0f, 0f, viewerZ);

        var width = input.Width;
        var height = input.Height;
        var output = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            var outputRow = output.GetRowSpan(y);
            for (var x = 0; x < width; x++)
            {
                var normal = ComputeSurfaceNormal(input, x, y, surfaceScalePixel);
                var i = ReadStraightAlpha(input, x, y);
                var (l, lightColor) = ComputeLightAt(light, x, y, surfaceScalePixel * i);

                var lPlusE = l + eye;
                var halfway = lPlusE == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(lPlusE);
                var nDotH = Math.Max(Vector3.Dot(normal, halfway), 0f);
                var factor = specularConstant * MathF.Pow(nDotH, specularExponent);
                var specular = factor * lightColor;

                var r = specular.X * 255f;
                var g = specular.Y * 255f;
                var b = specular.Z * 255f;
                var a = MathF.Max(r, MathF.Max(g, b));
                outputRow[x] = new Rgba32(ToByte(r), ToByte(g), ToByte(b), ToByte(a));
            }
        }

        return output;
    }

    /// <summary>
    ///     Reads the straight-alpha value of one pixel as a bump-map height in <c>[0, 1]</c>.
    /// </summary>
    /// <param name="surface">The surface to sample.</param>
    /// <param name="x">The pixel X coordinate (always within bounds for every caller here).</param>
    /// <param name="y">The pixel Y coordinate (always within bounds for every caller here).</param>
    /// <returns>The pixel's alpha channel divided by <c>255</c>.</returns>
    private static float ReadStraightAlpha(Surface surface, int x, int y) => surface[x, y].A / 255f;

    /// <summary>
    ///     Reads one pixel's straight-alpha bump-map height, or <c>0</c> for any coordinate outside
    ///     <paramref name="surface"/>'s bounds.
    /// </summary>
    /// <param name="surface">The surface to sample.</param>
    /// <param name="x">The requested pixel X coordinate.</param>
    /// <param name="y">The requested pixel Y coordinate.</param>
    /// <returns>The sampled height, or <c>0</c> when out of bounds.</returns>
    /// <remarks>
    ///     Every one of the 9 Sobel kernel variants selected by <see cref="ComputeSurfaceNormal"/>
    ///     pairs an out-of-bounds neighbor position with a zero coefficient (see that method's
    ///     remarks), so the <c>0</c> returned here for an out-of-bounds sample is always multiplied
    ///     by zero and never actually contributes - this helper exists only so the kernel
    ///     application loop can read a uniform 3x3 neighborhood unconditionally, rather than
    ///     branching per tap.
    /// </remarks>
    private static float ReadStraightAlphaOrZero(Surface surface, int x, int y) =>
        x < 0 || x >= surface.Width || y < 0 || y >= surface.Height ? 0f : ReadStraightAlpha(surface, x, y);

    /// <summary>
    ///     Estimates the surface normal at one bump-map pixel using the SVG specification's 9-variant
    ///     Sobel kernel table (SVG 1.1 &#167;15.14), selecting the interior/edge/corner variant that
    ///     matches <paramref name="x"/>/<paramref name="y"/>'s position.
    /// </summary>
    /// <param name="alphaMap">The buffer whose straight-alpha channel is the bump map.</param>
    /// <param name="x">The pixel X coordinate.</param>
    /// <param name="y">The pixel Y coordinate.</param>
    /// <param name="surfaceScalePixel">The already pixel-scaled <c>surfaceScale</c> height factor.</param>
    /// <returns>The estimated, unit-length surface normal.</returns>
    /// <remarks>
    ///     Uses <c>dx = dy = 1</c> pixel, the same "no <c>kernelUnitLength</c> support" convention
    ///     this codec already applies to <c>feConvolveMatrix</c>: the spec's general
    ///     <c>FACTORx</c>/<c>FACTORy</c> formulas (e.g. <c>2/(3*dx)</c>) are pre-substituted with
    ///     <c>dx = dy = 1</c> in the 9 tables below. Every table's zero row/column exactly matches
    ///     the neighbor direction that does not exist for that variant (e.g. the top row's tables
    ///     have an all-zero top row, since no <c>y - 1</c> neighbor exists there) - so reading a
    ///     nonexistent neighbor as <c>0</c> (see <see cref="ReadStraightAlphaOrZero"/>) never changes
    ///     the result.
    /// </remarks>
    private static Vector3 ComputeSurfaceNormal(Surface alphaMap, int x, int y, float surfaceScalePixel)
    {
        var isLeft = x == 0;
        var isRight = x == alphaMap.Width - 1;
        var isTop = y == 0;
        var isBottom = y == alphaMap.Height - 1;

        var (factorX, kx, factorY, ky) = (isTop, isBottom, isLeft, isRight) switch
        {
            (true, false, true, false) => (2f / 3f, KxTopLeft, 2f / 3f, KyTopLeft),
            (true, false, false, false) => (1f / 3f, KxTopRow, 1f / 2f, KyTopRow),
            (true, false, false, true) => (2f / 3f, KxTopRight, 2f / 3f, KyTopRight),
            (false, false, true, false) => (1f / 2f, KxLeftCol, 1f / 3f, KyLeftCol),
            (false, false, false, true) => (1f / 2f, KxRightCol, 1f / 3f, KyRightCol),
            (false, true, true, false) => (2f / 3f, KxBottomLeft, 2f / 3f, KyBottomLeft),
            (false, true, false, false) => (1f / 3f, KxBottomRow, 1f / 2f, KyBottomRow),
            (false, true, false, true) => (2f / 3f, KxBottomRight, 2f / 3f, KyBottomRight),
            _ => (1f / 4f, KxInterior, 1f / 4f, KyInterior)
        };

        float kxSum = 0f, kySum = 0f;
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var sample = ReadStraightAlphaOrZero(alphaMap, x + col - 1, y + row - 1);
                kxSum += kx[row, col] * sample;
                kySum += ky[row, col] * sample;
            }
        }

        var nx = -surfaceScalePixel * factorX * kxSum;
        var ny = -surfaceScalePixel * factorY * kySum;
        const float nz = 1f;

        var normal = new Vector3(nx, ny, nz);
        return normal == Vector3.Zero ? new Vector3(0f, 0f, 1f) : Vector3.Normalize(normal);
    }

    // The following 18 tables (9 Kx/Ky pairs) are verbatim transcriptions of the SVG 1.1 Filter
    // Effects specification's feDiffuseLighting/feSpecularLighting surface-normal kernel table
    // (row-major, [row, col], row 0 = y-1, row 2 = y+1, col 0 = x-1, col 2 = x+1). Do not
    // "simplify" or re-derive these - they intentionally are not perfectly symmetric, and even a
    // seemingly-equivalent rewrite risks silently mistranscribing the spec.
    private static readonly float[,] KxTopLeft = { { 0, 0, 0 }, { 0, -2, 2 }, { 0, -1, 1 } };
    private static readonly float[,] KyTopLeft = { { 0, 0, 0 }, { 0, -2, -1 }, { 0, 2, 1 } };
    private static readonly float[,] KxTopRow = { { 0, 0, 0 }, { -2, 0, 2 }, { -1, 0, 1 } };
    private static readonly float[,] KyTopRow = { { 0, 0, 0 }, { -1, -2, -1 }, { 1, 2, 1 } };
    private static readonly float[,] KxTopRight = { { 0, 0, 0 }, { -2, 2, 0 }, { -1, 1, 0 } };
    private static readonly float[,] KyTopRight = { { 0, 0, 0 }, { -1, -2, 0 }, { 1, 2, 0 } };
    private static readonly float[,] KxLeftCol = { { 0, -1, 1 }, { 0, -2, 2 }, { 0, -1, 1 } };
    private static readonly float[,] KyLeftCol = { { 0, -2, -1 }, { 0, 0, 0 }, { 0, 2, 1 } };
    private static readonly float[,] KxInterior = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
    private static readonly float[,] KyInterior = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };
    private static readonly float[,] KxRightCol = { { -1, 1, 0 }, { -2, 2, 0 }, { -1, 1, 0 } };
    private static readonly float[,] KyRightCol = { { -1, -2, 0 }, { 0, 0, 0 }, { 1, 2, 0 } };
    private static readonly float[,] KxBottomLeft = { { 0, -1, 1 }, { 0, -2, 2 }, { 0, 0, 0 } };
    private static readonly float[,] KyBottomLeft = { { 0, -2, -1 }, { 0, 2, 1 }, { 0, 0, 0 } };
    private static readonly float[,] KxBottomRow = { { -1, 0, 1 }, { -2, 0, 2 }, { 0, 0, 0 } };
    private static readonly float[,] KyBottomRow = { { -1, -2, -1 }, { 1, 2, 1 }, { 0, 0, 0 } };
    private static readonly float[,] KxBottomRight = { { -1, 1, 0 }, { -2, 2, 0 }, { 0, 0, 0 } };
    private static readonly float[,] KyBottomRight = { { -1, -2, 0 }, { 1, 2, 0 }, { 0, 0, 0 } };

    /// <summary>
    ///     Resolves a <c>feDiffuseLighting</c>/<c>feSpecularLighting</c> element's own
    ///     <c>lighting-color</c> presentation attribute.
    /// </summary>
    /// <param name="element">The <c>feDiffuseLighting</c> or <c>feSpecularLighting</c> element.</param>
    /// <returns>The resolved color as 0-1 RGB components, defaulting to opaque white per spec.</returns>
    private static Vector3 ResolveLightingColor(XElement element)
    {
        var raw = (string?)element.Attribute("lighting-color");
        var color = (raw != null ? ParseColor(raw.Trim()) : null) ?? new Rgba32(255, 255, 255, 255);
        return new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
    }

    /// <summary>
    ///     Resolves a lighting primitive's single <c>feDistantLight</c>/<c>fePointLight</c>/
    ///     <c>feSpotLight</c> child into a <see cref="ResolvedLight"/> whose position-dependent
    ///     attributes are already mapped into pixel space.
    /// </summary>
    /// <param name="element">The <c>feDiffuseLighting</c> or <c>feSpecularLighting</c> element.</param>
    /// <param name="transform">The referencing element's accumulated transform.</param>
    /// <param name="regionPixelX">The enclosing filter region's absolute pixel-space X origin.</param>
    /// <param name="regionPixelY">The enclosing filter region's absolute pixel-space Y origin.</param>
    /// <param name="scale">The local-space-to-pixel-space scale factor.</param>
    /// <param name="color">The already-resolved <c>lighting-color</c>.</param>
    /// <returns>
    ///     The resolved light, or <see langword="null"/> when no recognized light-source child is
    ///     present (a tolerant "no light" fallback - see <see cref="ApplyFeDiffuseLighting"/>/
    ///     <see cref="ApplyFeSpecularLighting"/>'s remarks).
    /// </returns>
    private static ResolvedLight? ResolveLightSource(XElement element, Matrix3x2 transform, int regionPixelX, int regionPixelY, float scale, Vector3 color)
    {
        var lightElement = element.Elements().FirstOrDefault(e =>
            e.Name.LocalName is "feDistantLight" or "fePointLight" or "feSpotLight");
        if (lightElement == null)
        {
            return null;
        }

        if (lightElement.Name.LocalName == "feDistantLight")
        {
            var azimuthRadians = (ParseFirstNumberToken((string?)lightElement.Attribute("azimuth")) ?? 0f) * MathF.PI / 180f;
            var elevationRadians = (ParseFirstNumberToken((string?)lightElement.Attribute("elevation")) ?? 0f) * MathF.PI / 180f;
            var direction = new Vector3(
                MathF.Cos(azimuthRadians) * MathF.Cos(elevationRadians),
                MathF.Sin(azimuthRadians) * MathF.Cos(elevationRadians),
                MathF.Sin(elevationRadians));
            return new ResolvedLight { Kind = LightKind.Distant, DistantDirection = direction, Color = color };
        }

        var localToPixel = transform * Matrix3x2.CreateTranslation(-regionPixelX, -regionPixelY);
        var positionPixel = ResolveLightPositionPixel(lightElement, localToPixel, scale);

        if (lightElement.Name.LocalName == "fePointLight")
        {
            return new ResolvedLight { Kind = LightKind.Point, PositionPixel = positionPixel, Color = color };
        }

        // feSpotLight
        var pointsAtLocal = new Vector2(
            ParseFirstNumberToken((string?)lightElement.Attribute("pointsAtX")) ?? 0f,
            ParseFirstNumberToken((string?)lightElement.Attribute("pointsAtY")) ?? 0f);
        var pointsAtZ = (ParseFirstNumberToken((string?)lightElement.Attribute("pointsAtZ")) ?? 0f) * scale;
        var pointsAtPixel2D = Vector2.Transform(pointsAtLocal, localToPixel);
        var pointsAtPixel = new Vector3(pointsAtPixel2D.X, pointsAtPixel2D.Y, pointsAtZ);

        var spotVector = pointsAtPixel - positionPixel;
        var spotDirectionUnit = spotVector == Vector3.Zero ? new Vector3(0f, 0f, 1f) : Vector3.Normalize(spotVector);

        var falloffExponent = ParseFirstNumberToken((string?)lightElement.Attribute("specularExponent")) ?? 1f;
        var rawLimitingConeAngle = (string?)lightElement.Attribute("limitingConeAngle");
        float? cosLimitingConeAngle = rawLimitingConeAngle == null
            ? null
            : MathF.Cos((ParseFirstNumberToken(rawLimitingConeAngle) ?? 0f) * MathF.PI / 180f);

        return new ResolvedLight
        {
            Kind = LightKind.Spot,
            PositionPixel = positionPixel,
            SpotDirectionUnit = spotDirectionUnit,
            SpotFalloffExponent = falloffExponent,
            CosLimitingConeAngle = cosLimitingConeAngle,
            Color = color
        };
    }

    /// <summary>
    ///     Resolves a <c>fePointLight</c>/<c>feSpotLight</c> element's own <c>x</c>/<c>y</c>/<c>z</c>
    ///     position into pixel space.
    /// </summary>
    /// <param name="lightElement">The <c>fePointLight</c> or <c>feSpotLight</c> element.</param>
    /// <param name="localToPixel">The local-space-to-pixel-space transform (see <see cref="ApplyFeImage"/>'s remarks for the identical mapping).</param>
    /// <param name="scale">The local-space-to-pixel-space scale factor, applied to <c>z</c> alone (not part of the 2D affine <paramref name="localToPixel"/> transform).</param>
    /// <returns>The light's position in pixel space.</returns>
    private static Vector3 ResolveLightPositionPixel(XElement lightElement, Matrix3x2 localToPixel, float scale)
    {
        var localXy = new Vector2(
            ParseFirstNumberToken((string?)lightElement.Attribute("x")) ?? 0f,
            ParseFirstNumberToken((string?)lightElement.Attribute("y")) ?? 0f);
        var z = (ParseFirstNumberToken((string?)lightElement.Attribute("z")) ?? 0f) * scale;
        var pixelXy = Vector2.Transform(localXy, localToPixel);
        return new Vector3(pixelXy.X, pixelXy.Y, z);
    }

    /// <summary>
    ///     Computes the per-pixel light vector <c>L</c> and (already lighting-color-multiplied)
    ///     light color at one surface point, per SVG 1.1 &#167;15.14's per-light-kind formulas.
    /// </summary>
    /// <param name="light">The resolved light, or <see langword="null"/> for the tolerant "no light" fallback.</param>
    /// <param name="pixelX">The surface point's pixel-space X coordinate.</param>
    /// <param name="pixelY">The surface point's pixel-space Y coordinate.</param>
    /// <param name="pixelZ">The surface point's pixel-space Z coordinate (<c>surfaceScale * I(x,y)</c>).</param>
    /// <returns>The light vector <c>L</c> and the effective light color (0-1 RGB, zero when unlit).</returns>
    private static (Vector3 L, Vector3 Color) ComputeLightAt(ResolvedLight? light, int pixelX, int pixelY, float pixelZ)
    {
        if (light == null)
        {
            return (new Vector3(0f, 0f, 1f), Vector3.Zero);
        }

        if (light.Kind == LightKind.Distant)
        {
            return (light.DistantDirection, light.Color);
        }

        var surfacePoint = new Vector3(pixelX, pixelY, pixelZ);
        var toLight = light.PositionPixel - surfacePoint;
        var l = toLight == Vector3.Zero ? new Vector3(0f, 0f, 1f) : Vector3.Normalize(toLight);

        if (light.Kind == LightKind.Point)
        {
            return (l, light.Color);
        }

        // feSpotLight: attenuate by pow(-L.S, specularExponent), zeroed outside the light cone.
        var negativeLDotS = -Vector3.Dot(l, light.SpotDirectionUnit);
        if (negativeLDotS <= 0f || (light.CosLimitingConeAngle is { } cosCone && negativeLDotS < cosCone))
        {
            return (l, Vector3.Zero);
        }

        var attenuation = MathF.Pow(negativeLDotS, light.SpotFalloffExponent);
        return (l, light.Color * attenuation);
    }
}
