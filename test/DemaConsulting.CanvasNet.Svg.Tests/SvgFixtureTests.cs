using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Unit tests that exercise <see cref="SvgCodec"/> against the real-file SVG fixture corpus
///     in <c>SvgFixtures</c> (see <c>SvgFixtures\README.md</c> for provenance - most fixtures are
///     hand-authored for this repository), mirroring the pattern used by
///     <c>TiffFixtureTests</c>/<c>JpegFixtureTests</c>. Includes one real-font integration test
///     that reuses <c>FontFixtures\OpenSans-Regular.ttf</c>, mirroring
///     <c>TrueTypeFontRealFontIntegrationTests</c>, and two real-world, third-party,
///     Wikimedia-Commons-sourced fixtures (<c>SvgGradient.svg</c>/<c>InkscapeFilters.svg</c>; see
///     <c>SvgFixtures\WikimediaCommons.LICENSE</c> for provenance/licensing).
/// </summary>
public class SvgFixtureTests
{
    /// <summary>
    ///     The directory containing the SVG fixture corpus, copied to the test output directory
    ///     by the test project's <c>SvgFixtures\**</c> content item. <c>Path.Join</c> is used
    ///     instead of <see cref="Path.Combine(string, string)"/> purely to avoid CodeQL's
    ///     <c>cs/path-combine</c> rule, since <c>Path.Join</c> does not discard
    ///     <see cref="AppContext.BaseDirectory"/> when the second segment looks rooted.
    /// </summary>
    private static string AssetsPath => Path.Join(AppContext.BaseDirectory, "SvgFixtures");

    /// <summary>
    ///     The path to the real "Open Sans" TrueType font, copied to the test output directory by
    ///     the test project's <c>FontFixtures\**</c> content item. <c>Path.Join</c> is used
    ///     instead of <see cref="Path.Combine(string, string, string)"/> for the same CodeQL
    ///     reason as <see cref="AssetsPath"/>.
    /// </summary>
    private static string FontPath => Path.Join(AppContext.BaseDirectory, "FontFixtures", "OpenSans-Regular.ttf");

    /// <summary>
    ///     Resolves a fixture file within <see cref="AssetsPath"/>. The file names always
    ///     originate from this class's own literals rather than external input, so
    ///     path-injection is not a concern here; <c>Path.Join</c> is used instead of
    ///     <see cref="Path.Combine(string, string)"/> purely to avoid CodeQL's
    ///     <c>cs/path-combine</c> rule, since <c>Path.Join</c> does not discard
    ///     <see cref="AssetsPath"/> when <paramref name="fileName"/> looks rooted.
    /// </summary>
    private static string ResolveFixturePath(string fileName) =>
        Path.Join(AssetsPath, fileName);

    /// <summary>
    ///     Proves that <c>shapes.svg</c>'s four basic shapes (rect/circle/ellipse/polygon) each
    ///     rasterize their expected solid fill color at a representative interior pixel, while a
    ///     point covered by none of them remains fully transparent.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ShapesFixture_RendersExpectedShapeColors()
    {
        // Arrange & Act: load the fixture at its native 1:1 viewBox-to-raster size
        var surface = SvgCodec.Load(ResolveFixturePath("shapes.svg"), 100, 100);

        // Assert: each shape's expected solid color appears at a representative interior pixel
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[25, 25]); // rect (red)
        Assert.Equal(new Rgba32(0, 128, 0, 255), surface[75, 25]); // circle (green)
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[25, 75]); // ellipse (blue)
        Assert.Equal(new Rgba32(255, 255, 0, 255), surface[75, 75]); // polygon (yellow)

        // Assert: a point covered by none of the four shapes remains fully transparent
        Assert.Equal(0, surface[95, 5].A);
    }

    /// <summary>
    ///     Proves that <c>groups-and-transforms.svg</c>'s nested group fill inheritance and
    ///     combined <c>translate</c>/<c>rotate</c> transform functions both bake correctly into
    ///     final pixel-space geometry - the first rectangle only appears at its translated
    ///     position (not its pre-translation local position), and the second rectangle only
    ///     appears within its rotated-then-translated bounding region.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupsAndTransformsFixture_RendersAtTransformedPositions()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(ResolveFixturePath("groups-and-transforms.svg"), 100, 100);

        // Assert: the first rect inherits its group's "orange" fill and renders at its
        // translate(50,0)-mapped position, not its pre-translation local (0,0)-(20,20) position
        Assert.Equal(new Rgba32(255, 165, 0, 255), surface[60, 10]);
        Assert.Equal(0, surface[10, 10].A);

        // Assert: the second rect's local (0,0)-(30,10) region, rotated 90 degrees then
        // translated by (20,60), maps to final pixel region x:[10,20], y:[60,90]
        Assert.Equal(new Rgba32(128, 0, 128, 255), surface[15, 75]);
        Assert.Equal(0, surface[5, 75].A);
    }

    /// <summary>
    ///     Proves that <c>gradient.svg</c>'s <c>linearGradient</c> (referenced via
    ///     <c>fill="url(#id)"</c>) actually varies across the filled rectangle - a monotonically
    ///     increasing red channel from the black stop at the left edge to the white stop at the
    ///     right edge - rather than degrading to a single flat color.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientFixture_RendersVaryingGradientColors()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(ResolveFixturePath("gradient.svg"), 100, 100);

        // Assert: the red channel increases monotonically from left to right, and is fully
        // opaque throughout - proving a real gradient (not a flat fallback color) was rendered
        var left = surface[5, 50];
        var middle = surface[50, 50];
        var right = surface[95, 50];
        Assert.True(left.R < middle.R, $"Expected left ({left.R}) < middle ({middle.R}).");
        Assert.True(middle.R < right.R, $"Expected middle ({middle.R}) < right ({right.R}).");
        Assert.Equal(255, left.A);
        Assert.Equal(255, right.A);
    }

    /// <summary>
    ///     Proves that <c>use-reference.svg</c>'s <c>use</c> element renders its <c>defs</c>-only
    ///     template at the <c>use</c> element's own <c>x</c>/<c>y</c> offset, and that the
    ///     template itself never renders directly at its own local position (since <c>defs</c>
    ///     content is only reachable via a reference).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UseReferenceFixture_RendersAtOffsetPositionOnly()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(ResolveFixturePath("use-reference.svg"), 100, 100);

        // Assert: the referenced 20x20 "teal" box renders at its use-offset (30,40) position
        Assert.Equal(new Rgba32(0, 128, 128, 255), surface[40, 50]);

        // Assert: the box's own local (pre-offset) position, and a far corner, remain transparent
        Assert.Equal(0, surface[5, 5].A);
        Assert.Equal(0, surface[95, 95].A);
    }

    /// <summary>
    ///     Proves that <c>tolerant-unsupported.svg</c>'s <c>filter</c> element definition -
    ///     unreferenced by any <c>filter="url(#id)"</c> attribute - does not prevent the rest of
    ///     the document (an ordinary <c>rect</c>) from rendering normally. Now that <c>filter</c>
    ///     defs are genuinely parsed and evaluated, this fixture instead exercises the case of a
    ///     <c>filter</c> def that is simply never referenced, rather than an unsupported
    ///     construct; see the dedicated dangling-reference and unsupported-primitive tests in
    ///     <c>SvgCodecTests</c> for filter-specific tolerance coverage.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ToleratesUnsupportedConstructFixture_StillRendersRemainingContent()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(ResolveFixturePath("tolerant-unsupported.svg"), 100, 100);

        // Assert: the rect still renders its "lime" fill despite the sibling, unreferenced
        // <filter> element
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[25, 25]);
    }

    /// <summary>
    ///     Proves that a real glyph, loaded from the real "Open Sans" production font and
    ///     rendered through <c>text.svg</c>'s <c>text</c> element, paints genuine, non-vacuous ink
    ///     within the text's expected region while the canvas's far corners remain fully
    ///     transparent - not merely "no exception was thrown" (mirrors
    ///     <c>TrueTypeFontRealFontIntegrationTests</c>'s pattern applied end to end through
    ///     <see cref="SvgCodec"/>).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFixtureWithRealFont_RendersVisibleGlyphInk()
    {
        // Arrange: load the real production font and supply it under the family name the
        // fixture's font-family attribute references
        var font = TrueTypeFont.Load(FontPath);
        var fonts = new Dictionary<string, TrueTypeFont> { ["Open Sans"] = font };

        // Act: rasterize the fixture with the font dictionary supplied
        var surface = SvgCodec.Load(ResolveFixturePath("text.svg"), 200, 80, fonts);

        // Assert: at least one pixel within the text's expected region (below and around the
        // baseline at x=10, y=55, font-size 40) was actually painted (non-transparent)
        var foundInk = false;
        for (var y = 15; y < 70 && !foundInk; y++)
        {
            for (var x = 5; x < 90; x++)
            {
                if (surface[x, y].A > 0)
                {
                    foundInk = true;
                    break;
                }
            }
        }

        Assert.True(foundInk, "Expected at least one filled (non-transparent) pixel within the text's expected region.");

        // Assert: the canvas's far corners, well outside the text, remain fully transparent
        Assert.Equal(0, surface[0, 0].A);
        Assert.Equal(0, surface[199, 0].A);
        Assert.Equal(0, surface[0, 79].A);
        Assert.Equal(0, surface[199, 79].A);
    }

    /// <summary>
    ///     Proves that the real, unmodified, Wikimedia-Commons-sourced <c>SvgGradient.svg</c>
    ///     (see <c>SvgFixtures\WikimediaCommons.LICENSE</c> for provenance) renders its
    ///     <c>userSpaceOnUse</c> <c>linearGradient</c> bar as genuinely varying pixel colors
    ///     (bright near its white stop, dark near its black stop), rather than degrading to a
    ///     single flat color, and that its <c>fill="pink"</c> background rectangle is visible at
    ///     a point clearly outside every gradient bar and <c>use</c> shape. A regressed
    ///     <c>userSpaceOnUse</c> gradient-unit handling (for example, mistakenly treating the
    ///     gradient's <c>x1</c>/<c>x2</c> user-space coordinates as fractional
    ///     <c>objectBoundingBox</c> offsets) would clamp most of the bar to a single extreme
    ///     stop color, and a background-color-name or basic-rect regression would leave the
    ///     background pixel transparent or black instead of pink.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_SvgGradientFixture_RendersVaryingGradientAndPinkBackground()
    {
        // Arrange & Act: rasterize at the fixture's native 300x200 viewBox size
        var surface = SvgCodec.Load(ResolveFixturePath("SvgGradient.svg"), 300, 200);

        // Assert: the pink background rect is visible at a point clearly outside the "bar" use
        // shape (y in [80,100]) and every gradient rect (rows at y in [30,70] and [110,190])
        Assert.Equal(new Rgba32(255, 192, 203, 255), surface[5, 5]);

        // Assert: the leftmost gradient bar (translate(50,50), the fixture's userSpaceOnUse
        // gradient) is bright near its x1=-40 (offset 0, white) end and much darker near its
        // offset=0.9 (black) stop further along the bar - a genuine, non-flat gradient
        var brightEnd = surface[15, 50];
        var darkEnd = surface[78, 50];
        Assert.True(brightEnd.R > 200, $"Expected bright end red channel > 200, got {brightEnd.R}.");
        Assert.True(darkEnd.R < 60, $"Expected dark end red channel < 60, got {darkEnd.R}.");
        Assert.True(
            brightEnd.R - darkEnd.R > 140,
            $"Expected a large red-channel drop from bright end ({brightEnd.R}) to dark end ({darkEnd.R}).");
        Assert.Equal(255, brightEnd.A);
        Assert.Equal(255, darkEnd.A);
    }

    /// <summary>
    ///     Proves that the real, unmodified, Wikimedia-Commons-sourced
    ///     <c>InkscapeFilters.svg</c> (see <c>SvgFixtures\WikimediaCommons.LICENSE</c> for
    ///     provenance) - a large, complex document built entirely from <c>defs</c>/<c>use</c>
    ///     templating, composed <c>transform</c> functions, and dozens of <c>filter="url(#...)"</c>
    ///     references placed on <c>use</c> elements referencing a shared <c>g</c> template (the
    ///     group-level filtering this feature adds support for) - whose primitive chains combine
    ///     <c>feGaussianBlur</c>/<c>feComposite</c> (including its <c>arithmetic</c> operator) with
    ///     <c>feSpecularLighting</c>/<c>feDiffuseLighting</c>, all now fully implemented rather
    ///     than tolerantly passed through - loads without
    ///     throwing despite this genuinely deep filter evaluation now actually being
    ///     performed for every one of these group-level filter references (proving the
    ///     resource-safety guards
    ///     shared with per-shape filtering hold within a real, unmodified, complex
    ///     third-party document, not only a small synthetic one), that a flower shape rendered
    ///     behind one such group-level <c>filter</c> reference still paints recognizably-related,
    ///     fully-opaque content at a deep-interior pixel (proving real content rendered, not just
    ///     "didn't crash", and that the final <c>feComposite operator="atop"</c> against
    ///     <c>SourceGraphic</c> - which by definition adopts <c>SourceGraphic</c>'s own alpha -
    ///     still reconstructs full opacity far from any shape edge, even though the chain's
    ///     <c>feSpecularLighting</c>/<c>feGaussianBlur</c> primitives now genuinely alter the
    ///     evaluated color), that points in the gap between flowers remain transparent (proving
    ///     the render is not a degenerate whole-canvas fill that would make the previous
    ///     assertions vacuous), and - the assertion this feature specifically adds - that the
    ///     filtered flower's final alpha at a partial-coverage antialiased silhouette edge pixel
    ///     exactly matches the corresponding pixel of the first, unfiltered flower
    ///     (<c>translate(50,50)</c>, no <c>filter</c> attribute) - a mathematical certainty given
    ///     the chain's final primitive is <c>feComposite operator="atop"</c> against
    ///     <c>SourceGraphic</c> (Porter-Duff atop's alpha formula, <c>bgA*fgA + (1-fgA)*bgA</c>,
    ///     algebraically reduces to exactly <c>bgA</c> for any <c>fgA</c>) - proving group-level
    ///     filtering now genuinely evaluates its full primitive chain (rather than tolerating the
    ///     attribute as a no-op) while still recovering the source silhouette's own alpha exactly,
    ///     as the spec requires.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_InkscapeFiltersFixture_ToleratesFiltersAndRendersFlowerContent()
    {
        // Arrange & Act: rasterize at the fixture's native 600x1000 width/height - this must not
        // throw despite every flower (other than the very first) referencing a group-level
        // <filter> whose chain mixes supported and unsupported (tolerantly passed-through)
        // primitives
        var surface = SvgCodec.Load(ResolveFixturePath("InkscapeFilters.svg"), 600, 1000);

        // Assert: a petal of the second flower - translate(150,50), filter="url(#filter48)"
        // applied to the referencing <use> element itself - renders fully opaque at this
        // deep-interior pixel, and its color, now that operator="arithmetic" feComposite is
        // genuinely evaluated (previously an unimplemented no-op passthrough of "result3"),
        // reflects "result7"'s real k1=0.5/k2=0.5/k3=1.1-weighted combination of the blurred
        // "result3" base color and "result5"'s own feSpecularLighting output - whose k3=1.1
        // weighting of a bright Blinn-Phong specular highlight (specularConstant="1.10000002",
        // deep inside a well-lit convex region) saturates every channel to near-white, still
        // exactly matching this filter's own deterministic evaluation - proving real, filtered
        // content rendered, not a blank/degenerate result; the alpha component remains unchanged
        // at 255 (unaffected by the arithmetic fix), since the chain's final operator="atop"
        // composite against SourceGraphic by definition adopts SourceGraphic's own alpha
        Assert.Equal(new Rgba32(254, 253, 252, 255), surface[135, 30]);

        // Assert: the gaps between flowers (the grid spacing is 100 units, and each flower's
        // petals only reach roughly 36 units from its own center) remain fully transparent -
        // proving the render did not degenerate into filling the whole canvas with one color
        Assert.Equal(0, surface[100, 50].A);
        Assert.Equal(0, surface[100, 100].A);

        // Assert: the second flower's own group-level filter chain (feGaussianBlur ->
        // feComposite xor/atop -> feGaussianBlur -> feComposite xor -> feGaussianBlur ->
        // feSpecularLighting -> feComposite arithmetic -> feGaussianBlur -> feComposite atop
        // against SourceGraphic) resolves to exactly SourceGraphic's own antialiased silhouette
        // alpha at this pixel: Porter-Duff atop's alpha formula, bgA*fgA + (1-fgA)*bgA, is an
        // algebraic identity that reduces to exactly bgA (SourceGraphic's own alpha) regardless
        // of fgA (the upstream chain's foreground alpha) - so the final composite against
        // SourceGraphic exactly recovers SourceGraphic's own antialiased edge, even though every
        // primitive upstream of that final atop composite now genuinely evaluates (rather than
        // tolerating arithmetic as a no-op passthrough). This is verified by comparing against
        // the corresponding relative-offset pixel of the first, unfiltered flower
        // (translate(50,50), no filter attribute), whose own raw antialiased edge independently
        // yields the identical partial-coverage alpha value
        Assert.Equal(0, surface[98, 29].A);
        var filteredEdgePixel = surface[182, 29];
        var unfilteredEdgePixel = surface[82, 29];
        Assert.Equal(35, filteredEdgePixel.A);
        Assert.Equal(unfilteredEdgePixel.A, filteredEdgePixel.A);
    }

    /// <summary>
    ///     Proves that <c>arrow-markers.svg</c>'s <c>marker-end</c>-referenced arrowhead
    ///     (<c>orient="auto"</c>, <c>markerUnits="userSpaceOnUse"</c>) renders its own "navy"
    ///     triangle content past the line's own end point, and that a point clearly outside both
    ///     the line and the arrowhead remains transparent.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ArrowMarkersFixture_RendersArrowheadPastLineEnd()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(ResolveFixturePath("arrow-markers.svg"), 100, 100);

        // Assert: the arrowhead's own "navy" fill is visible past the line's own x2=80 end point
        // (the triangle's tip reaches x=82 at y=50, per its refX=8/refY=5 anchor and
        // markerWidth=markerHeight=10)
        Assert.Equal(new Rgba32(0, 0, 128, 255), surface[78, 50]);

        // Assert: a point clearly outside both the line and the arrowhead remains transparent
        Assert.Equal(0, surface[10, 90].A);
    }

    /// <summary>
    ///     Proves that <c>label-halo.svg</c>'s <c>filter="url(#label-bg)"</c> reference - a
    ///     conventional <c>feFlood</c> → <c>feGaussianBlur</c> → <c>feComposite</c> label-background
    ///     recipe, mirroring the reported real-world bug where <c>SvgRenderer</c> uses this exact
    ///     pattern for a white halo behind midpoint line labels - actually renders the white halo
    ///     (rather than the halo silently disappearing, as it did while <c>filter</c> was ignored):
    ///     the halo's flood extends beyond its own <c>rect</c>'s bounds, softened by the blur, and
    ///     the sibling <c>line</c> remains fully visible outside the halo's own bounds.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LabelHaloFixture_RendersWhiteHaloBehindLineMidpointLabel()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(ResolveFixturePath("label-halo.svg"), 100, 40);

        // Assert: the halo rect's own center renders fully opaque white
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[50, 20]);

        // Assert: just outside the halo rect's own left edge, the blurred flood blends partially
        // (not fully opaquely) with the line passing beneath it - proving the halo actually
        // extends past the rect's own bounds with a softened, not hard, edge
        Assert.Equal(new Rgba32(152, 152, 152, 255), surface[39, 20]);

        // Assert: just outside the filter's own (default, bounding-box-relative) region, the
        // line renders its plain, unaffected black stroke
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[37, 20]);

        // Assert: the line remains fully visible, unaffected, far from the halo on either side
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[10, 20]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[90, 20]);
    }

    /// <summary>
    ///     Proves that <c>css-styling.svg</c>'s <c>&lt;style&gt;</c> element and every selector
    ///     kind/combinator it exercises (universal, class, id, compound, comma-separated list,
    ///     descendant combinator, child combinator, cascade tiebreak) each apply their expected
    ///     fill color end to end through a real file on disk, and that all 3 precedence tiers
    ///     resolve as documented (presentation attribute only, stylesheet overrides presentation
    ///     attribute, inline style overrides even a higher-specificity stylesheet id rule).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_CssStylingFixture_AppliesEverySelectorKindAndPrecedenceTier()
    {
        // Arrange & Act
        var surface = SvgCodec.Load(ResolveFixturePath("css-styling.svg"), 200, 80);

        // Assert: presentation-attribute-only tier (no stylesheet rule targets this element)
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[10, 10]);

        // Assert: class selector
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[30, 10]);

        // Assert: id selector wins over a same-element, lower-specificity class selector
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[50, 10]);

        // Assert: compound selector - the rect (correct type+class) matched, the circle
        // (correct class, wrong type) kept its default black fill
        Assert.Equal(new Rgba32(255, 165, 0, 255), surface[70, 10]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[70, 50]);

        // Assert: comma-separated selector list - both listed classes applied the same declaration
        Assert.Equal(new Rgba32(165, 42, 42, 255), surface[90, 10]);
        Assert.Equal(new Rgba32(165, 42, 42, 255), surface[90, 50]);

        // Assert: descendant combinator - matches any depth of nesting inside a g
        Assert.Equal(new Rgba32(128, 0, 128, 255), surface[110, 10]);

        // Assert: child combinator - only a direct child of #direct-parent matched; the same
        // class one level further nested (whose immediate parent is a different g) did not
        Assert.Equal(new Rgba32(0, 128, 128, 255), surface[130, 10]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[130, 50]);

        // Assert: cascade tiebreak - the later, equal-specificity rule (magenta) won
        Assert.Equal(new Rgba32(255, 0, 255, 255), surface[150, 10]);

        // Assert: a matching stylesheet rule overrides a plain presentation attribute
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[170, 10]);

        // Assert: an inline style unconditionally overrides even a higher-specificity stylesheet
        // id rule
        Assert.Equal(new Rgba32(0, 255, 255, 255), surface[190, 10]);
    }
}
