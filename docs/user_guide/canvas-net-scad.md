# CanvasNet Scad Package

## API Reference

### ScadDocument

`ScadDocument` (namespace `DemaConsulting.CanvasNet.Scad`) is distributed via the separate
`DemaConsulting.CanvasNet.Scad` NuGet package, which references the core `DemaConsulting.CanvasNet` and the
`DemaConsulting.CanvasNet.Render3D` packages - see the Installation section of the project README. It is the single
entry point for OpenSCAD-language models. A document is **immutable and thread-safe**: it is created from source
text, checked for syntax once, and can then be evaluated, built into a scene, or rendered any number of times (and
from several threads at once), each call giving the same result for the same options. The package is an
independent, clean-room implementation of the language, not affiliated with the OpenSCAD project; it follows its
own documented dialect (described below) and does not aim for pixel or tessellation parity with any other
implementation. It parses, evaluates, and renders `.scad` models - primitives, transforms, booleans, extrusions,
`hull`, `minkowski`, `projection`, `text` (with a deterministic bundled font), `color` with alpha, the `%` ghost
and `#` highlight modifiers, the viewport variables `$vpt`, `$vpr`, `$vpd`, and `$vpf`, and `-D`-style variable
overrides - straight to a `Surface`, and reports every unsupported
construct instead of silently ignoring it (see *Unsupported Features and Strict Mode*).

```csharp
public sealed partial class ScadDocument
{
    public static ScadDocument Parse(string source, ScadOpenOptions? options = null);
    public static ScadDocument Open(string path, ScadOpenOptions? options = null);
    public static ScadDocument Open(Stream stream, ScadOpenOptions? options = null);

    public string? SourceName { get; }
    public IReadOnlyList<ScadDirective> Directives { get; }
    public IReadOnlyList<string> ModuleNames { get; }
    public IReadOnlyList<string> FunctionNames { get; }

    public ScadEvaluationResult Evaluate(
        ScadEvaluationOptions? options = null,
        CancellationToken cancellationToken = default);

    public Scene BuildScene(
        ScadRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public Surface Render(
        int width,
        int height,
        ScadRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

- `Parse` and the two `Open` overloads read UTF-8 source (at most 4,194,304 characters) and throw a
  `ScadSyntaxException` with the file, line, and column of the first syntax error. `Open(string)` names the source
  by the path and uses the file's folder as the base directory for `include` and `use`; a string or stream document
  has no base directory unless `ScadOpenOptions` supplies one (or a `FileResolver`).
- `Evaluate` runs the program without generating geometry and returns the `echo` lines, the warnings, the
  unsupported-feature reports, and the number of module instances.
- `BuildScene` evaluates the program and returns a Render3D `Scene` in right-handed, Z-up model space, so you can
  measure it (`scene.ComputeBounds()`), combine it with other scenes, or render it with your own camera and
  `SceneRenderer`. Every call returns a new scene; a model without geometry gives a scene that renders as the
  background.
- `Render` evaluates, builds, and draws the model onto a new `Surface` of exactly `width` by `height` pixels. The
  caller owns, and must dispose, the returned surface. Without a camera the view is **fitted to the model**: an
  isometric perspective view (azimuth 45 degrees, elevation about 35.26 degrees) on a white background, with
  neutral gray surfaces unless the model uses `color()`. A model that is entirely 2D (a `projection()` drawing
  sheet, see *Drawing Sheets with projection()*) is drawn instead as a flat top-down orthographic picture, and a
  model can choose its own view with the viewport variables (see *Viewport Variables*); *Choosing the View* below
  gives the order of precedence. For the default view the result is the same as `SceneRenderer.RenderFitted` of
  the scene `BuildScene` returns, and rendering is deterministic: two renders of one document with the same
  options are byte-identical, for every degree of parallelism.

**Exceptions** (`BuildScene` and `Render`; `Evaluate` throws the same, except the render-only ones):

- `ArgumentOutOfRangeException`: Thrown by `Render`, before anything is evaluated, when `width` or `height` is
  outside 1 to `Surface.MaxDimension`.
- `ArgumentException`: Thrown by `Render`, before anything is evaluated, when `ScadRenderOptions.Camera` is invalid
  (for example eye equal to target).
- `ScadSyntaxException`: Thrown when an included or used file contains a syntax error (its location names that
  file).
- `ScadEvaluationException`: Thrown, with the source location in the message, when an `assert` fails, a limit is
  exceeded (including the triangle limit), an `include` or `use` violates the sandbox, or, without a location,
  when the renderer's `MaxFragmentsPerTile` budget is exceeded by a model with booleans.
- `ScadUnsupportedFeatureException`: Thrown at the first unsupported feature of any stage when
  `ScadEvaluationOptions.ThrowOnUnsupportedFeature` is set (see *Unsupported Features and Strict Mode*).
- `OperationCanceledException`: Thrown, never wrapped, when `cancellationToken` is canceled before or during the
  work; with an already canceled token nothing is evaluated.
- `InvalidDataException` and `IOException`: Thrown when an included or used file is longer than 4,194,304
  characters or cannot be read.

Nothing the model asks for is skipped silently: an invalid argument is a **warning** at the module's location and
yields nothing for that instance, a construct this package recognizes but cannot build is an **unsupported
feature** with a stable identifier, and an exhausted resource is an **error**. Warnings, unsupported features, and
`echo` output of all stages are delivered to the handlers of `ScadEvaluationOptions` once, in the order they
occurred, on the calling thread, before the method returns or throws.

### ScadRenderOptions

`ScadRenderOptions` is an immutable class; every property is init-only and validated when set, so one instance can
be shared between threads and calls.

<!-- markdownlint-disable MD013 -->
| Property | Default | Meaning |
| -------- | ------- | ------- |
| `Evaluation` | `null` (default `ScadEvaluationOptions`) | The options of the evaluation: `-D`-style `Variables`, `Time` (`$t`), `Limits`, the `EchoHandler`, `WarningHandler`, and `UnsupportedFeatureHandler`, and `ThrowOnUnsupportedFeature`. It is the single place that decides how problems are reported, for evaluation, geometry building, and rendering alike |
| `Camera` | `null` | A Render3D `Camera`; `null` fits the view to the model. An invalid camera is rejected by `Render` before evaluation. `BuildScene` ignores it, but the model still sees the viewport variables. An explicit camera also defines all four viewport variables for the model (see *Viewport Variables*) |
| `ViewAzimuthDegrees` | 45 | Azimuth of the fitted view (rotation about +Z); finite. Used only without a `Camera`. **Setting it, even to the default value, makes the view explicit** (see *Choosing the View*) |
| `ViewElevationDegrees` | about 35.26 | Elevation of the fitted view; finite. Used only without a `Camera`. Setting it, even to the default value, makes the view explicit |
| `Projection` | `Perspective`; `Orthographic` for a flat model | Projection of the fitted view (`CameraProjection.Perspective` or `Orthographic`). Used only without a `Camera`. Setting it changes only the projection: it does not make the view angles explicit |
| `ResolvedViewHandler` | `null` | An `Action<ScadResolvedView>` that `Render` calls once, on the calling thread, after the model was built and before it draws, to report the view that was used (see *Choosing the View*). `BuildScene` never calls it; an exception thrown by the handler propagates to the caller |
| `SceneOptions` | `null` (Render3D defaults) | The `SceneRenderOptions` of the renderer: background color (white by default), default surface color (neutral gray), lights, supersampling, parallelism, and fragment budget. Its `UnsupportedFeatureHandler` and `ThrowOnUnsupportedFeature` are **ignored**; reporting always goes through `Evaluation` |
| `FlatShapeThickness` | 0.1 | Thickness in model units of the slab that represents a 2D shape (`square`, `circle`, `polygon`) rendered without an extrusion; finite and greater than zero |
| `HighlightColor` | `(255, 64, 128, 110)`, a translucent pink | The color of the translucent copy that the `#` modifier draws over a highlighted solid (see *Transparency, Ghosts, and Highlights*). Any alpha is accepted: 255 draws an opaque copy and 0 an invisible one |
| `GhostColor` | `(128, 128, 128, 80)`, a translucent gray | The color of the translucent ghost that the `%` modifier draws instead of a solid. Any alpha is accepted |
| `FontResolution` | `null` (a default `ScadFontResolutionOptions`) | Where `text()` finds a font that the model names with `font = ...` (see *Text*): `UseSystemFonts` (default `true`) and `AdditionalDirectories` (default empty; copied and validated when set, a null, empty, or white-space directory is an `ArgumentException`). Text without a `font` argument always uses the bundled font and ignores these options |
<!-- markdownlint-enable MD013 -->

A `color()` in the model overrides `SceneOptions.DefaultColor` (and the alpha of a `color()` is rendered; the alpha of
`DefaultColor` is ignored). `ScadLimits` (in `Evaluation.Limits`) bounds every
resource of a call; besides the evaluation limits (`MaxRecursionDepth`, `MaxEvaluationSteps`,
`MaxLoopIterations`, `MaxListLength`, `MaxStringLength`, `MaxIncludeDepth`, `MaxIncludedFiles`, `MaxInstances`,
`MaxOutputLength`, `MaxDiagnostics`) it has `MaxTriangles` (a `long`, default 5,000,000, at least 1), the bound on
the number of triangles the model's geometry may need. The exact number of triangles of every sphere, cylinder,
polyhedron, slab, and extrusion (including the caps of a profile with holes) is computed **before** it is
allocated, so a hostile model such as `sphere(1, $fn = 100000)`
fails with a located `ScadEvaluationException` instead of exhausting memory.

**Variables (`-D`).** `ScadEvaluationOptions.Variables` replaces every top-level assignment of the named variable
in the document (and creates the variable when it is absent), including special variables such as `$fn`; values
may be `null`, booleans, numbers, strings, or sequences of these (a sequence becomes a vector). Names must be
ordinary identifiers (optionally starting with `$`).

#### Choosing the View

`Render` picks the view of a picture from the first of these sources that applies:

1. An explicit `Camera`, used as given (no fitting).
2. An explicit `ViewAzimuthDegrees` or `ViewElevationDegrees`. Setting either one (even to its default value) makes
   the view explicit, and the other angle keeps its default. An explicit view is used for every model, including one
   that is entirely 2D.
3. The model itself: a top-level assignment of `$vpr`, `$vpt`, `$vpd`, or `$vpf`, or a `-D` override of one of them
   (see *Viewport Variables*).
4. A flat top view, when the model is entirely 2D at the top level (see *Drawing Sheets with projection()*).
5. The isometric default.

An explicit `Projection` only changes the projection of the fitted view; it does not make the angles explicit, so a
flat model drawn with `Projection = CameraProjection.Perspective` is still looked at from above.

To find out which view a render used, set `ResolvedViewHandler`. It receives a `ScadResolvedView`, an immutable
record that `Render` reports once, after the model was built and before it is drawn (`BuildScene` chooses no camera
and never calls the handler):

```csharp
public sealed record ScadResolvedView(
    double AzimuthDegrees,
    double ElevationDegrees,
    CameraProjection Projection,
    bool IsFlatModel,
    ScadViewSource Source);

public enum ScadViewSource { Default, FlatDefault, Options, Document, Camera }
```

- `AzimuthDegrees` and `ElevationDegrees` give the direction from the target to the eye in the orbit convention of
  the renderer (the azimuth is a rotation about +Z that is counter-clockwise seen from above, 0 looks from -Y;
  the elevation is the angle above the XY plane), whichever way the camera was chosen. A flat model with no chosen
  view reports 0 and 90.
- `IsFlatModel` is `true` when the model is entirely 2D at the top level, not a mix of 2D and 3D.
- `Source` tells where the view came from: `Default` (the requested or isometric angles, fitted to the model),
  `FlatDefault` (the flat top view of a 2D-only model), `Options` (explicit angles), `Document` (the model, or a
  variable override, set a viewport variable), or `Camera` (the caller's `Camera`). Treat a value you do not know
  defensively, as the members may grow.

```csharp
var sheet = ScadDocument.Parse("projection() cube([30, 20, 15], center = true);");
ScadResolvedView? used = null;
using var image = sheet.Render(800, 600, new ScadRenderOptions { ResolvedViewHandler = view => used = view });
Console.WriteLine($"{used!.Source} {used.ElevationDegrees} {used.Projection}");
// Output: FlatDefault 90 Orthographic
```

### Supported OpenSCAD Subset

Everything below is built to a Render3D scene by `BuildScene` and `Render`. Arguments follow the OpenSCAD-language
forms (positional or named); an unknown, surplus, or repeated argument is a warning, and `$fn`, `$fa`, `$fs`
(see *Curve Resolution*) apply to the curved primitives. `convexity` arguments are accepted and ignored.

<!-- markdownlint-disable MD013 -->
| Module | Forms | Notes |
| ------ | ----- | ----- |
| `cube` | `cube(size, center)`; `size` is a number or `[x, y, z]` | Origin corner unless `center` |
| `sphere` | `sphere(r)` or `sphere(d = ...)` | `d` overrides `r`; the number of fragments follows `$fn`, `$fa`, and `$fs`, except that a radius of 1e-8 or less always gives 3 fragments, even with `$fn` set |
| `cylinder` | `cylinder(h, r, r1, r2, d, d1, d2, center)` | Cones and frusta with `r1`/`r2` (per-end values override `r`/`d`); `d1`/`d2` are strongest |
| `polyhedron` | `polyhedron(points, faces, convexity)`, or the legacy `triangles` when `faces` is absent | Faces list their points **clockwise seen from outside**; a solid supplied counter-clockwise is reversed with a warning. Concave planar faces are triangulated; an open shape is kept |
| `square`, `circle`, `polygon` | `square(size, center)`, `circle(r)` or `circle(d = ...)`, `polygon(points, paths)` | 2D shapes; `paths` gives holes and islands (nesting parity), in either winding. A 2D shape that is not extruded is drawn as a thin slab (`FlatShapeThickness`); a model that is entirely 2D is drawn flat from above (see *Drawing Sheets with projection()*) |
| `text` | `text(text, size, font, halign, valign, spacing, direction, language, script)` | A 2D shape of glyph outlines; see *Text* below for the size, font, alignment, and direction rules and the limits of the dialect |
| `translate`, `scale`, `mirror` | `translate(v)`, `scale(v)` with a number or vector, `mirror(v)` (reflection through the plane with normal `v`) | Work on solids and 2D shapes |
| `rotate` | `rotate([x, y, z])` (degrees; X first, then Y, then Z), `rotate(a)` (about Z), `rotate(a, v)` (about an axis) | |
| `multmatrix` | `multmatrix(m)` with a 4 x 4 (or 3 x 4) column-vector matrix | A bottom row other than `[0, 0, 0, 1]` is `scad-multmatrix-projective` |
| `resize` | `resize(newsize, auto)` | Scales about the origin so the children reach `newsize`; `auto` keeps proportions |
| `color` | `color(c, alpha)`; `c` is a CSS color name (case-insensitive, including `"transparent"`), `"#RGB"`, `"#RGBA"`, `"#RRGGBB"`, `"#RRGGBBAA"`, or `[r, g, b]`/`[r, g, b, a]` with components 0 to 1 | The innermost color wins, alpha included. An alpha below 1 (from `alpha`, which overrides the alpha inside `c`, from the fourth component, from the hex form, or from `transparent`) makes the surface **translucent** (see *Transparency, Ghosts, and Highlights*); an `alpha` without a `c` is a warning |
| `union`, `render`, `difference`, `intersection`, `intersection_for` | Booleans of solids and of 2D shapes | `difference` subtracts every later child from the first; an empty first child gives an empty result; mixed 2D and 3D children keep the dimension of the first operand and warn about the others |
| `linear_extrude` | `linear_extrude(height, center, convexity, twist, slices, scale)` | `height` defaults to 100; a positive `twist` turns **clockwise seen from +Z**; `scale` is a number or `[sx, sy]` (both zero makes an apex) |
| `hull` | `hull() { ... }` of 3D children or of 2D children | The convex hull of every vertex of the children, in their transformed positions (a union of children, whether groups, loops, or user modules, is one point set). 3D children give one closed convex solid that is a valid operand of later booleans; 2D children give a convex shape that `linear_extrude`, `rotate_extrude`, and the top-level slab consume. Colors inside are dropped with a warning (the hull takes the enclosing `color`). A `difference` or `intersection` among the children is `scad-hull-csg-operand`, mixed 2D and 3D children are `scad-hull-mixed-dimensions`, and points that span no volume (or area) are `scad-hull-degenerate`; the hull is omitted in each case, never approximated |
| `minkowski` | `minkowski(convexity) { ... }` of convex 3D children | The Minkowski sum of the children, folded from the left: `minkowski() { cube([10, 6, 4]); sphere(1); }` is the block with rounded edges and corners. A union among the children distributes over the sum. Every child must be **convex**: a block, a sphere, a cylinder, a `hull`, a convex `polyhedron`, or a union of those; a `difference`, an `intersection`, or a hollow, notched, or otherwise non-convex mesh is `scad-minkowski-nonconvex` (also for a single child), and the module is omitted. A sum of 2D children is deferred (`scad-minkowski-2d`), mixed 2D and 3D children are `scad-minkowski-mixed-dimensions`, and more than 4,096 convex parts or sums, or more than 2,097,152 pairwise vertex sums for two parts or in total for the whole module, is `scad-minkowski-too-complex`. `convexity` is ignored; colors inside are dropped with a warning (also for a lone child, so an enclosing `color` applies) |
| `rotate_extrude` | `rotate_extrude(angle, convexity)` | Profile X is the radius; the sweep starts at +X and runs counter-clockwise; `angle` defaults to 360, larger values are clamped, negative values sweep clockwise; a profile on the negative side of the axis is swept where it lies (a point at `(-4, 0)` with `angle = 90` sweeps from 180 to 270 degrees); a profile with vertices on both sides of the axis is `scad-rotate-extrude-axis-crossing` |
| `projection` | `projection(cut)` over 3D children; `cut` is `false` (default) or `true` | A flat 2D shape that every 2D module, extrusion, and 2D boolean accepts. `cut = false` is the **shadow** of the children on the XY plane (an exact silhouette of the tessellated meshes: a through hole stays a hole, a blind pocket leaves none, a tube gives an annulus); `cut = true` is their **cross-section at z = 0** (a face that lies exactly in that plane belongs to the section). A `cut` that is not a boolean is a warning and counts as `false`; `convexity` is ignored; 2D children are ignored with a warning, `%` children are skipped, and colors inside are not kept (a warning). A projection that needs more than 2^23 triangle visits (for example a very fine curved solid under a `difference`) is `scad-projection-too-complex`, and a mesh whose cross-section does not close (not a closed solid) is `scad-projection-open-mesh`; the projection is omitted in both cases. The footprints are checked against `MaxTriangles` before any is built. See *Drawing Sheets with projection()* |
| `!` (show only) | `!cube(5);` | The marked subtree becomes the new root of the design, **without** the transforms and colors of its ancestors: `translate([10, 0, 0]) { !cube(1); }` renders the cube at the origin |
| `*` (disable) | `*cube(5);` | The subtree is ignored |
| `#` (highlight) | `#cube(5);` | The subtree is rendered normally **and** as a translucent copy in `HighlightColor`; it still takes part in the booleans around it. On a 2D shape that is not extruded it is reported as `scad-modifier-highlight` (see the next section) |
| `%` (background) | `%cube(5);` | The subtree is drawn only as a translucent ghost in `GhostColor` that is not part of the model. On a 2D shape that is not extruded it is omitted and reported as `scad-modifier-background` (see the next section) |
<!-- markdownlint-enable MD013 -->

Language features (variables, functions, modules with `children()`, `for`, `if`, `let`, comprehensions, `echo`,
`assert`, `include` and `use`, and the built-in functions) are evaluated by the evaluator described in
*ScadDocument* above; the geometry builder never interprets them again.

#### Transparency, Ghosts, and Highlights

A `color()` with an alpha below 1, `%`, and `#` make surfaces translucent. The renderer composites the surfaces
along each viewing ray from front to back, deterministically (the same bytes for every degree of parallelism) and
over the background color of `SceneOptions` (a transparent background keeps a partial alpha). Because translucent
bodies and overlays are resolved like the operands of a boolean, **their meshes must be closed solids**; a
`polyhedron` that is not closed is reported through the `scad-csg-*` identifiers (when a handler or strict mode is
set), and the fragment budget `SceneOptions.MaxFragmentsPerTile` applies to them as to booleans.

- **A color with alpha.** `color("red", 0.5) cube(10);` is a half-transparent red cube: seen from above, the near and
  the far wall of the cube both show, each with the alpha of the color, over what lies behind. An alpha of 0 is
  invisible but still solid, so it still cuts and still counts for a `difference`. The innermost `color` wins,
  alpha included.
- **A union of translucent bodies shows the boundary of the union.** Overlapping members of one union merge, as in
  every constructive solid geometry result, so the faces of one member that lie inside another are not visible.
  To see through independent bodies, mark them with `%`.
- **`%` draws a ghost.** The subtree is drawn as a translucent body in `GhostColor` (its own colors are replaced)
  and is **not part of the model**: it adds nothing to a union, subtracts nothing from a `difference`, and
  intersects nothing (an `intersection` with a ghost is empty; a ghost as the **first** child of a `difference`
  leaves no ordinary geometry; the ghost is still drawn). It contributes no propagated bounds, so `resize`
  ignores it, and `hull` and `minkowski` ignore it too. It **is** part of `Scene.ComputeBounds()`, the bounds that
  fit the default camera, so a ghost cannot fall out of the frame (`ComputeBounds(includeOverlays: false)` leaves it
  out). This
  is a documented choice of this package, not a claim of parity with any other implementation.
- **`#` draws a highlight.** The subtree is rendered **normally** (its own colors, in the model) **and** as a
  translucent copy in `HighlightColor`. It keeps taking part in the booleans around it, so in
  `difference() { cube(10); #translate([2, 2, 2]) cube(6); }` the highlighted cutter still cuts, and its pink body is
  drawn inside the cut: the highlight is visible even where the subtree was subtracted. The triangles of the copy
  count a second time against `ScadLimits.MaxTriangles` (so a highlight on a very large subtree can reach the
  limit sooner). A `#` on a `hull` or `minkowski` child (at any depth below it) makes the child an ordinary part of
  the hull or sum: no copy is built or drawn, so it is not charged against the limit either; a `%` there contributes
  nothing, and a `minkowski` or `hull` of only a ghost builds nothing.
- **2D shapes.** A `#` or `%` on a 2D shape that is not extruded (a bare `square`, `circle`, or `polygon`, which is
  drawn as a slab) has no translucent form: `#` renders the slab normally and `%` omits it, and each is reported
  (`scad-modifier-highlight`, `scad-modifier-background`). The modifier on an **extrusion** of the shape, for example
  `%linear_extrude(2) circle(4);`, works fully.
- **The alpha of `SceneOptions.DefaultColor`, `BackFaceColor`, and lights is ignored.** Only the alpha of a model
  `color()` and of `HighlightColor` and `GhostColor` is honored.

Examples 25 and 27 show the reports and a complete transparent model.

#### Text

`text()` lays a string out with a TrueType font and builds a **flat 2D shape** of the glyph outlines (holes such as the
counter of an `O` are kept), so it works wherever a `square` does: extrude it with `linear_extrude` or
`rotate_extrude`, subtract it with a 2D `difference`, move it with `translate` and `rotate`, take its `hull`, or color
it. A text that is not extruded is drawn as a thin slab like any 2D shape. The arguments, in positional order:

<!-- markdownlint-disable MD013 -->
| Argument | Default | Meaning |
| -------- | ------- | ------- |
| `text` | required | The string. A number or another value is converted like `str()`. An empty string builds nothing and is not reported |
| `size` | 10 | The height in model units of the glyph `H` of the selected font, so a capital `H` is exactly `size` tall and stands on the baseline (`Y = 0`). A finite number greater than zero |
| `font` | the bundled Liberation Sans Regular | A family, `"Family"`, or a family with a style, `"Family:style=Bold Italic"` (the words `Bold` and `Italic`, or `Oblique`, in any case). See *Fonts* below |
| `halign` | `"left"` | `"left"`, `"center"`, or `"right"`: which point of the **ink** (the drawn outline, not the advance box) is at `X = 0`: the left edge, the middle, or the right edge |
| `valign` | `"baseline"` | `"baseline"` (the baseline is `Y = 0`), `"top"`, `"center"`, or `"bottom"`: the top edge, the middle, or the bottom edge of the ink at `Y = 0` |
| `spacing` | 1 | A finite number that is not negative that multiplies the advance of every glyph (not the kerning); `spacing = 0` overlaps the glyphs |
| `direction` | `"ltr"` | `"ltr"`; `"rtl"` (the characters in reverse order, laid out left to right); `"ttb"` and `"btt"` (upright characters stacked downwards or upwards by their advance, without kerning) |
| `language`, `script` | none | Accepted with any value and **ignored** |
<!-- markdownlint-enable MD013 -->

The curve resolution follows `$fn`, `$fa`, and `$fs` like a circle of radius `size / 2` does (see *Curve Resolution*):
the outlines are flattened so that they stay within the sagitta of such a circle. An invalid argument (for example a
`size` of 0, or a `halign` of `"middle"`) is a warning and the text is omitted.

```openscad
// A label cut into a plate: the text is a 2D shape, so it extrudes and subtracts like one
difference() {
    cube([60, 16, 4]);
    translate([30, 8, 2]) linear_extrude(height = 3) text("CanvasNet", size = 7, halign = "center", valign = "center");
}
```

**Fonts and determinism.** Without a `font` argument the text always uses the Liberation Sans Regular font that is
bundled with the core package, whatever fonts the machine has, so a model without a `font` renders identically
everywhere. A named font is resolved by `ScadRenderOptions.FontResolution`: first the family is searched in the
`AdditionalDirectories` (scanned recursively), then, when `UseSystemFonts` is `true` (the default), among the fonts
installed on the machine, then among the bundled `Liberation Sans`, `Liberation Serif`, and `Liberation Mono`
families (and the generic names `sans-serif`, `serif`, and `monospace`). The generic names (`sans-serif`, `sans`,
`serif`, `monospace`, `mono`, and the other CSS generic keywords) are the exception: they always resolve to the
bundled fonts first, so a font family that is literally named `sans` or `serif` in your font directory (or installed)
is never used - give such a font a different family name. Only an exact family name (case aside)
matches, and the closest bold and italic style of that family is used. A family that is found nowhere, or whose
font file cannot be loaded, gives one warning and the bundled sans font. **The result for a font that comes from
the installed fonts depends on the machine**, and the installed fonts are searched before the bundled families, so
even a request for a bundled family name (such as `Liberation Sans`) can be answered by a different font installed on
the machine. Only `UseSystemFonts = false` makes every named font reproducible.

```csharp
var options = new ScadRenderOptions
{
    FontResolution = new ScadFontResolutionOptions
    {
        UseSystemFonts = false,                       // do not look at the installed fonts
        AdditionalDirectories = ["./fonts"],          // but look here first
    },
};
```

**Limits of the dialect** (CanvasNetScad's own decisions; no parity with any other implementation is claimed): the
text is laid out one character at a time without shaping, ligatures, or combining marks, and `language` and
`script` change nothing, so right-to-left scripts are not shaped (`"rtl"` only reverses the characters); the kerning
is the one of the font's classic `kern` table; the size is the ink height of `H` and not a font-wide ascent;
vertical text stacks upright characters by their advance width (a tall letter can touch the next one; raise `spacing`
to separate them); a character the font does not map is taken from the bundled Noto Sans, Noto Sans Math, or Noto
Sans Symbols 2 (so `∝`, `≫`, arrows, and mathematical operators are drawn; only these bundled fonts are used, never an
installed one, so the result does not depend on the machine; the glyph is scaled so that `size` keeps its meaning, in the
regular style, with no kerning between glyphs of different fonts); a character that no bundled font covers (a
private-use or CJK character, for example) is drawn with the font's `.notdef` glyph, or skipped when that glyph has no
outline, and **one warning per `text()`** lists up to eight of those code points as `U+XXXX`;
control characters (such as line feeds) are skipped, so text does not wrap; the functions `textmetrics()` and
`fontmetrics()` are not supported (see *Unsupported Features and Strict Mode*). A glyph whose outline cannot be
represented (a damaged outline, or contours that cross each other) is reported as `scad-text-invalid-outline` or
`scad-text-overlapping-contours` and the whole text is omitted; none of the bundled fonts has such a glyph in Basic
Latin or Latin-1. Example 28 shows lettering, alignment, and the font options.

#### Curve Resolution

The number of segments of a circle of radius `r` follows a public, deterministic rule: a positive `$fn` gives
`max(3, $fn)`; otherwise it is `ceil(max(min(360 / $fa, 2 * pi * r / $fs), 5))`, never below 3. With the defaults
(`$fa = 12`, `$fs = 2`) a radius of 1 gives 5 segments and a radius of 10 gives 30. A sphere uses its own radius, a
cylinder the larger of its two radii, and `rotate_extrude` and a twisted `linear_extrude` the largest radius of
their profile. Increase `$fn` for smooth curves, for example `$fn = 64`; the triangle limit bounds the cost.

#### Drawing Sheets with projection()

`projection()` turns 3D children into a flat 2D shape on the XY plane, so a model can draw its own parts as a
technical sheet: outlines, sections, a frame, and lettering, all 2D, all in one `.scad` file. The behavior is aligned
with the documented semantics of OpenSCAD; this package's own tests are the reference, and no parity or
compatibility with any other implementation is claimed.

- `projection()` (that is, `cut = false`) is the **shadow** of the children seen from above: every point of the XY
  plane that some part of the children lies over. It is an exact silhouette of the tessellated meshes, so a curved
  solid projects to its polygon. A through hole in the solid is a hole in the shadow, a blind pocket leaves none, and
  a tube gives an annulus.
- `projection(cut = true)` is the **cross-section** of the children at `z = 0`. A face that lies exactly in the plane
  `z = 0` belongs to the section. Move the part with `translate([0, 0, -z])` to cut it at height `z`.
- The result is an ordinary 2D shape: `translate`, `rotate`, `scale`, `mirror`, `color`, 2D booleans, `hull`,
  `linear_extrude`, and `rotate_extrude` all accept it.
- A `cut` that is not a boolean is a warning and counts as `false`. `convexity` is ignored. 2D children are ignored
  with a warning, `%` children are skipped, and a `color()` inside is not kept (a warning): color the projection
  itself instead. Two problems are reported and omit the projection: `scad-projection-too-complex` and
  `scad-projection-open-mesh` (see *Unsupported Features and Strict Mode*). The footprints of the children are
  checked against `ScadLimits.MaxTriangles` before any of them is built.

A drawing sheet is a model that is entirely 2D: a frame, the views of a part, and a title block. This one shows the
part from above and from the front, inside a frame:

```openscad
union() {
    translate([0, 40]) projection() my_model();
    translate([0, 0]) projection() rotate([90, 0, 0]) my_model();
    difference() { square([120, 80]); translate([2, 2]) square([116, 76]); }
    translate([5, 5]) text("SCALE: 1:1", size = 3);
    translate([40, 5]) text("TITLE: PROJECTION SHEET", size = 4, font = "Liberation Sans:style=Bold");
}
module my_model() { cube([30, 20, 15], center = true); }
```

The same idea gives the classic three-view sheet: one `projection()` of the part for the plan, one of the part
turned about X for the front elevation, and one of the part turned a further quarter turn about Z for the side
elevation, each moved to its own place on the sheet. For the front view of a part that is not symmetric, turn it with
`rotate([-90, 0, 0])` (the part stays upright); `rotate([90, 0, 0])` shows it upside down, which the cube above
cannot tell.

A section and a shadow differ whenever the part is hollow inside:

```openscad
module block() difference() {
    cube([40, 30, 20], center = true);
    translate([0, 0, -3]) cylinder(r = 8, h = 20);  // a blind pocket from the top, down to z = -3
}

projection() block();                                // the shadow: a plain rectangle, the pocket is not visible
translate([50, 0]) projection(cut = true) block();   // the section at z = 0: a rectangle with a round hole
```

**Flat drawing default.** A model that is entirely 2D at the top level (no mix with 3D) is not a scene to look at from
a corner: with no explicit `Camera`, no explicit view angles, and no `$vpr`, `Render` draws it as a flat, top-down,
orthographic picture fitted to the shape with a 5% margin, lit by ambient light only, so that every shape shows
exactly its `color()` or the default neutral gray (a flat shape has no shading). Mixed 2D and 3D models, 3D models,
and explicit views behave as before. Lights chosen in `SceneOptions.Lights` are respected, and so is an explicit
`Projection` (for example `Perspective`). `ResolvedViewHandler` reports `FlatDefault` for such a picture.

For a stronger drawing, set `SceneOptions.DefaultColor` (for example black; its alpha is ignored), color the shape in
the model with `color("black")` outside the `projection()` (the colors inside `projection()` are dropped; a `color()`
wins over `DefaultColor` and its alpha is honored), and choose the paper with `SceneOptions.BackgroundColor` (a
transparent color keeps the alpha of the picture).

```csharp
using var sheet = ScadDocument.Open("sheet.scad").Render(1200, 900);
PngCodec.Save(sheet, "sheet.png");
```

#### Viewport Variables

The model sees the camera of its own picture in four special variables, so it can place geometry relative to it:

<!-- markdownlint-disable MD013 -->
| Variable | Meaning | Default |
| -------- | ------- | ------- |
| `$vpt` | The target: the point the camera looks at | `[0, 0, 0]` |
| `$vpr` | The rotation of the camera in degrees, as `[x, y, z]` angles composed like `rotate($vpr)` | From the requested view: about `[54.7356, 0, 45]` for the default isometric view |
| `$vpd` | The distance from the eye to the target | `140` |
| `$vpf` | The vertical field of view in degrees | `22.5` |
<!-- markdownlint-enable MD013 -->

A `ScadDocument.Evaluate()` call without render options has no picture, so it shows the literal defaults of a fresh
viewer instead, with `$vpr = [55, 0, 25]`.

**Mapping.** The orbit view that `ViewAzimuthDegrees` and `ViewElevationDegrees` describe (azimuth `az`, elevation
`el`) is `$vpr = [90 - el, 0, az]`, with every angle wrapped into 0 to 360. The view from straight above is
`[0, 0, 0]`, the front view is `[90, 0, 0]`, and the default isometric view is about `[54.7356, 0, 45]`.

**Precedence.** Each variable takes the first value that exists, highest first:

<!-- markdownlint-disable MD013 -->
| Source | Applies to |
| ------ | ---------- |
| An explicit render option | An explicit `Camera` supplies all four variables; explicit `ViewAzimuthDegrees` or `ViewElevationDegrees` sets `$vpr` only |
| A variable override (`-D`, `ScadEvaluationOptions.Variables`) | Any of the four; a vector needs a sequence in `Variables`, as `-D` cannot define vectors (a scalar such as `-D '$vpd=200'` works) |
| An assignment at the top level of the model file and its `include`s | Any of the four; not a `use`d library and not a nested scope |
| The default | See the table above |
<!-- markdownlint-enable MD013 -->

**What the variables do to the picture.** Assigning only `$vpr` sets the orientation of the view, while the framing
stays automatic (the view is still fitted to the model). Assigning any of `$vpt`, `$vpd`, or `$vpf` selects a
**manual camera**: the eye sits at `$vpd` from `$vpt` along the rotated Z axis, the vertical field of view is `$vpf`,
and nothing is fitted (an orthographic view is `2 * $vpd * tan($vpf / 2)` high). A manual camera on a model that is
entirely 2D and assigns no `$vpr` still looks straight down. A value that cannot be converted gives a warning, for
example `Unable to convert $vpr=... to a vec3 or vec2 of numbers; the viewport variable is ignored`, and the variable
keeps the value of the next source.

```openscad
$vpr = [60, 0, 20];   // look from 30 degrees above the horizon, azimuth 20; the framing stays automatic
$vpd = 200;           // a manual camera: 200 units from the target, no automatic fit
cube(20);
```

**Circularity.** The camera is chosen after the model is built, so the model only sees the defaults for the
variables it does not assign, and it cannot see that it turned out to be entirely 2D. In the other direction, the
automatic fit includes the geometry that the variables placed, so a label placed with `$vpt` is framed too.

**Facing the camera.** `$vpr` is the rotation that takes the camera's own axes (right is +X, up is +Y, toward the
viewer is +Z) into the model, composed like `rotate($vpr)`. An object that lies in the XY plane and faces +Z is
therefore turned to face the camera by `rotate($vpr)`, and it is placed at the target by `translate($vpt)`. This
labels the view with its own rotation:

```openscad
cube([20, 20, 20], center = true);
translate($vpt) rotate($vpr) linear_extrude(0.1) text(str("Cam View: ", $vpr), size = 2);
```

In the default isometric view the label reads about `Cam View: [54.7356, 0, 45]`. It sits at `$vpt`, which is the
origin and so inside the cube; move it toward the camera along the rotated Z axis (for example
`translate($vpt) rotate($vpr) translate([0, 0, 20])`) to see it in front of the cube.

A chain of negated single-axis rotations, `translate($vpt) rotate([0, 0, -$vpr[2]]) rotate([0, -$vpr[1], 0])
rotate([-$vpr[0], 0, 0]) ...`, is **not** the same rotation: it turns about X first, then Y, then Z (the same order as
`rotate($vpr)`), but by the opposite angles, which is neither `rotate($vpr)` nor its inverse (an inverse would also
reverse the order). It faces the camera only for a top view, and for a general view it evaluates without a warning
and places the text exactly as its rotations say, which is not toward the camera. Use `rotate($vpr)`.

The behavior of the viewport variables is aligned with the documented semantics of OpenSCAD; no parity or
compatibility with any other implementation is claimed.

### Unsupported Features and Strict Mode

A construct the package recognizes but cannot build is reported as a `ScadUnsupportedFeature` with a stable,
lower-case `Feature` identifier, a message, and the `ScadSourceLocation` of the module (the file is named for
included and used files), and the rest of the model is still built. Reports are de-duplicated per location, so a
module in a loop is reported once. The identifiers are part of the documented behavior of the package:

<!-- markdownlint-disable MD013 -->
| Identifier | Trigger | Effect |
| ---------- | ------- | ------ |
| `scad-module-offset`, `scad-module-surface`, `scad-module-import`, `scad-module-roof` | The module is recognized but not implemented | The module and its children are omitted |
| `scad-projection-too-complex` | A `projection` needs more work than the ceiling of 2^23 triangle visits, for example a very fine curved solid under a `difference` | The projection is omitted; simplify the solid, lower `$fn`, or project the parts separately |
| `scad-projection-open-mesh` | A `projection` meets a mesh whose cross-section does not close, that is, a mesh that is not a closed solid (for example an open `polyhedron`) | The projection is omitted; close the polyhedron |
| `scad-text-invalid-outline` | A glyph outline of a `text()` is damaged, or a contour of a glyph crosses itself or has no area | The whole text is omitted |
| `scad-text-overlapping-contours` | The contours of one glyph of a `text()` overlap or touch each other, other than as a hole, an island, or the overlapping solids of a composite glyph | The whole text is omitted |
| `scad-hull-csg-operand` | A `hull` has a `difference` or an `intersection` among its children (3D or 2D, at any depth) | The hull is omitted |
| `scad-hull-degenerate` | The points of a `hull` span no volume (3D: fewer than four, collinear, or coplanar points) or no area (2D) | The hull is omitted |
| `scad-hull-mixed-dimensions` | A `hull` has both 2D and 3D children | The hull is omitted |
| `scad-minkowski-nonconvex` | A `minkowski` has a child that is a boolean or a mesh that is not convex (also a lone child); the message names the child by its position and source location | The module is omitted |
| `scad-minkowski-2d` | A `minkowski` of 2D children (deferred) | The module is omitted |
| `scad-minkowski-mixed-dimensions` | A `minkowski` has both 2D and 3D children | The module is omitted |
| `scad-minkowski-too-complex` | A `minkowski` has a child of more than 4,096 convex parts, a step of more than 4,096 sums, or more than 2,097,152 pairwise vertex sums for two parts or in total for the module | The module is omitted |
| `scad-modifier-highlight` | The `#` modifier on a 2D shape that is not extruded (on a solid, `#` is rendered and not reported) | The shape is rendered normally, without highlighting |
| `scad-modifier-background` | The `%` modifier on a 2D shape that is not extruded (on a solid, `%` is rendered as a ghost and not reported) | The shape is omitted |
| `scad-resize-approximate` | `resize` over children that contain a difference or an intersection | Estimated bounds are used |
| `scad-multmatrix-projective` | `multmatrix` with a bottom row other than `[0, 0, 0, 1]` | The module and its children are omitted |
| `scad-polygon-invalid` | A `polygon` (or a 2D shape after transformation under an extrusion) that crosses itself or has no area | That shape is omitted |
| `scad-polyhedron-face-invalid` | A polyhedron face that cannot be triangulated | The polyhedron is omitted |
| `scad-linear-extrude-scale` | A negative `scale` component, or exactly one zero component | The extrusion is omitted |
| `scad-linear-extrude-apex-multiple-contours` | `scale = 0` over a profile with holes or islands | The extrusion is omitted |
| `scad-rotate-extrude-axis-crossing` | The profile lies on both sides of the axis | The extrusion is omitted |
| `scad-rotate-extrude-axis-contact` | The profile touches the axis at an isolated vertex | The extrusion is omitted |
| `scad-geometry-rejected` | Any other geometry the Render3D tessellators and extruders reject, or an instance whose coordinates are not finite or exceed 1e100 in magnitude (for example `translate([1.7e308, 0, 0])`) | The affected geometry is omitted; the message says why |
| `scad-csg-open-mesh`, `scad-csg-non-manifold-edge`, `scad-csg-inconsistent-winding`, `scad-csg-degenerate-triangle`, `scad-csg-inverted-mesh`, `scad-csg-singular-transform` | `Render` found a defect in an operand of a `difference` or `intersection` (reported only when a handler or strict mode is set) | Reported at the module that produced the mesh; the image is still drawn |
| `scad-function-<name>`, `scad-search-form` | An unimplemented built-in function (`parent_module`, `textmetrics`, `fontmetrics`) or `search` argument form | The call evaluates to `undef` |
<!-- markdownlint-enable MD013 -->

Pass a handler to receive the reports (and the warnings) as they are delivered, or set
`ThrowOnUnsupportedFeature` to refuse a partial result: the first unsupported feature of any stage, after the
earlier events were delivered, is thrown as a `ScadUnsupportedFeatureException` that carries the `Feature` and the
`Location`. See Examples 23 to 25. The former identifier `scad-color-alpha` no longer exists: a color with an alpha
below 1 is rendered as a translucent surface (see *Transparency, Ghosts, and Highlights*). The identifier
`scad-module-projection` no longer exists either: `projection` is implemented (see *Drawing Sheets with
projection()*).

### OpenSCAD Include Sandbox

`include` and `use` are resolved before evaluation and only through `ScadOpenOptions`: files under `BaseDirectory`
or a `LibraryPaths` entry, or, when a `FileResolver` callback is supplied, only that callback with no file-system
access at all. Absolute paths, drive letters, colons, control characters, and `..` above the root are rejected with a
`ScadEvaluationException`, and a symbolic link or junction below a root is never followed (a link that points nowhere
is refused as a link, not reported as a missing file). A file that does not exist is only a warning
(`Can't open include file`); a file that exists but cannot be read (a directory where a file is expected, a file
that cannot be opened, or a `FileResolver` callback that throws) is a `ScadEvaluationException` too. The checks are
those of the core library's public `SandboxedFileResolver`, which you can also use directly (see the core library user
guide).

**Security note.** The sandbox protects against path-based escapes in a *static* file tree. It is **not a
security boundary against a concurrent local attacker** who has write access to the included folders: the
link check and the file open are separate operations, so a component swapped for a link or junction between
them is not noticed, and a hard link (which cannot be detected) to a file outside the folders is read like any
other file. Only point `BaseDirectory` and `LibraryPaths` at folders that untrusted parties cannot write to, or
use a `FileResolver` callback to serve the files yourself.

Two further behaviors are worth knowing. Indexing is lenient: `v[i]` is `undef` for every negative or
non-finite index, and a fractional non-negative index is truncated (`[10, 20, 30][1.5]` is `20`). A very deeply
recursive program may need a dedicated 384 MiB-stack thread (address space, committed only as touched); where
the process cannot provide it, such as a 32-bit process, `Evaluate` throws a `ScadEvaluationException` (and
`Parse` a `ScadSyntaxException` for the corresponding 16 MiB parser thread) instead of a runtime
out-of-memory error.

## Examples

### Example 23: Rendering an OpenSCAD Model

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Scad;

// A small bracket: a plate with two through-holes and a rounded boss. The model is plain OpenSCAD-language
// source; ScadDocument.Open("bracket.scad") would read the same text from a file.
var document = ScadDocument.Parse("""
    $fn = 48;
    module boss(r, h) { cylinder(r = r, h = h); }

    difference() {
        union() {
            cube([40, 24, 4], center = true);
            translate([0, 0, 2]) boss(8, 6);
        }
        for (x = [-14, 14]) translate([x, 0, 0]) cylinder(r = 2.5, h = 10, center = true);
        cylinder(r = 4, h = 20, center = true);
    }
    """);

// Render a fitted isometric view; the caller owns the surface. Surfaces are neutral gray on white by default.
using var image = document.Render(800, 600);
PngCodec.Save(image, "bracket.png");

// Or build the Render3D scene to measure it, or to render it yourself with another camera. (The bounds of a
// scene include every operand of a boolean, so the cutters count towards the height; X and Y are the plate.)
var scene = document.BuildScene();
var bounds = scene.ComputeBounds();
Console.WriteLine($"{bounds.Max.X - bounds.Min.X} x {bounds.Max.Y - bounds.Min.Y}");
// Output: 40 x 24
```

### Example 24: Customizing the View, Colors, and Variables

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad;

var document = ScadDocument.Parse("""
    pegs = 3;      // can be overridden from outside, like a command-line -D pegs=5
    pitch = 10;
    $fn = 32;

    color("steelblue") cube([pegs * pitch, pitch, 4]);
    for (i = [0 : pegs - 1])
        color(i % 2 == 0 ? "tomato" : "gold")
            translate([pitch / 2 + i * pitch, pitch / 2, 4]) cylinder(r = 3, h = 6);
    """);

// 1. A fitted view from the front-right, in orthographic projection, on a dark background with smooth edges
var look = new ScadRenderOptions
{
    ViewAzimuthDegrees = 20,
    ViewElevationDegrees = 25,
    Projection = CameraProjection.Orthographic,
    SceneOptions = new SceneRenderOptions
    {
        BackgroundColor = new Rgba32(32, 34, 40, 255),
        Supersampling = 4,
    },
};
using var front = document.Render(800, 400, look);
PngCodec.Save(front, "pegs-front.png");

// 2. An explicit camera looking straight down at the model (a top view, model units)
var top = new ScadRenderOptions
{
    Camera = new Camera
    {
        Eye = new Vector3D(25, 5, 100),
        Target = new Vector3D(25, 5, 0),
        Up = Vector3D.UnitY,
        Projection = CameraProjection.Orthographic,
        OrthographicHeight = 20,
    },
};
using var plan = document.Render(800, 400, top);
PngCodec.Save(plan, "pegs-top.png");

// 3. Override variables from outside the source (the same as -D pegs=5 -D pitch=12), including special variables
var variant = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions
    {
        Variables = new Dictionary<string, object?> { ["pegs"] = 5, ["pitch"] = 12, ["$fn"] = 64 },
    },
};
using var longer = document.Render(800, 400, variant);
PngCodec.Save(longer, "pegs-five.png");

// 4. Bound the work for models you do not control: at most 200,000 triangles, and give up after 10 seconds
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var bounded = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions { Limits = new ScadLimits { MaxTriangles = 200_000 } },
};
using var safe = document.Render(400, 200, bounded, timeout.Token);
```

### Example 25: Handling Unsupported Features and Strict Mode

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Scad;

var document = ScadDocument.Parse("""
    cube(10);
    translate([20, 0, 0]) surface(file = "heights.dat");
    roof() { sphere(3); translate([8, 0, 0]) sphere(3); }
    cube(-1);
    offset(r = 1) square(2);
    """, new ScadOpenOptions { SourceName = "model.scad" });

// Lenient: everything that can be built is drawn, and every problem is reported once, in order, on this
// thread, before Render returns
var warnings = new List<ScadDiagnostic>();
var unsupported = new List<ScadUnsupportedFeature>();
var lenient = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions
    {
        WarningHandler = warnings.Add,
        UnsupportedFeatureHandler = unsupported.Add,
    },
};
using var image = document.Render(320, 240, lenient);

foreach (var warning in warnings)
{
    Console.WriteLine(warning); // Output: model.scad:4:1: cube: size must be positive; the instance is ignored
}

foreach (var feature in unsupported)
{
    Console.WriteLine($"{feature.Location} {feature.Feature}");
}
// Output:
// model.scad:2:23 scad-module-surface
// model.scad:3:1 scad-module-roof
// model.scad:5:1 scad-module-offset

// Strict: refuse a partial result. The first unsupported feature of any stage throws, after the events that
// preceded it were delivered to the handlers
var strict = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions { ThrowOnUnsupportedFeature = true },
};

try
{
    using var never = document.Render(320, 240, strict);
}
catch (ScadUnsupportedFeatureException ex)
{
    Console.WriteLine($"{ex.Location}: {ex.Feature}"); // Output: model.scad:2:23: scad-module-surface
}
```

### Example 26: Hull and Minkowski Sum

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Scad;

var document = ScadDocument.Parse("""
    $fn = 32;

    // A capsule: the convex hull of two spheres
    hull() { sphere(r = 3); translate([12, 0, 0]) sphere(r = 3); }

    // A rounded block: the Minkowski sum of a block and a ball
    translate([0, 14, 0]) minkowski() { cube([14, 8, 4]); sphere(r = 1.5); }

    // A hull as a cutter: a diagonal slot through a plate
    translate([0, 34, 0]) difference() {
        cube([24, 24, 4]);
        translate([4, 4, -1]) hull() { cube([2, 2, 6]); translate([12, 12, 0]) cube([2, 2, 6]); }
    }

    // A 2D hull, extruded
    translate([30, 0, 0]) linear_extrude(height = 3) hull() { circle(r = 3); translate([10, 6]) square(2); }
    """, new ScadOpenOptions { SourceName = "convex.scad" });

using var image = document.Render(800, 600);
PngCodec.Save(image, "convex.png");

// A hull of a difference, and a Minkowski sum with a hollow part, are reported, never approximated
var report = new List<ScadUnsupportedFeature>();
var options = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions { UnsupportedFeatureHandler = report.Add },
};
ScadDocument.Parse("""
    hull() difference() { cube(4); cube(3); }
    minkowski() { difference() { cube(4); translate([1, 1, 1]) cube(2); } sphere(1); }
    """, new ScadOpenOptions { SourceName = "bad.scad" }).BuildScene(options);

foreach (var feature in report)
{
    Console.WriteLine($"{feature.Location} {feature.Feature}");
}
// Output:
// bad.scad:1:1 scad-hull-csg-operand
// bad.scad:2:1 scad-minkowski-nonconvex
```

### Example 27: Transparency, Ghosts, and Highlights

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Scad;

var document = ScadDocument.Parse("""
    // A half-transparent red box: its near and far walls both show
    color("red", 0.5) cube(10);

    // A plate with a hole whose cutter is highlighted: the cutter still cuts, and its pink body is drawn in the hole
    translate([16, 0, 0]) difference() {
        cube([10, 10, 3]);
        #translate([5, 5, -1]) cylinder(h = 5, r = 3, $fn = 32);
    }

    // A ghost: drawn translucent gray, but not part of the model - it does not cut the block it passes through
    translate([32, 0, 0]) difference() {
        cube(10);
        %translate([2, 2, 2]) cube([6, 6, 12]);
    }

    // A ghost of an extruded shape (a % on a bare 2D shape is reported, not drawn)
    %translate([52, 5, 0]) linear_extrude(height = 4) circle(r = 4, $fn = 24);
    """, new ScadOpenOptions { SourceName = "see-through.scad" });

// The overlay colors are part of the render options; any alpha is accepted
var options = new ScadRenderOptions
{
    HighlightColor = new Rgba32(255, 64, 128, 110), // the default: a translucent pink
    GhostColor = new Rgba32(128, 128, 128, 80),     // the default: a translucent gray
};

using var image = document.Render(800, 300, options);
PngCodec.Save(image, "see-through.png");

// Nothing here is reported as unsupported: the alpha, the ghost, and the highlight are rendered. A scene that
// uses them is resolved like a boolean, so its meshes must be closed solids; the ghost is part of the scene bounds
// that fit the default camera but not of the model (it adds no propagated bounds, so resize ignores it)
var scene = document.BuildScene(options);
Console.WriteLine(scene.ComputeBounds().Max.X);                              // Output: 56
Console.WriteLine(scene.ComputeBounds(includeOverlays: false).Max.X);        // Output: 42
```

### Example 28: Text, Alignment, and Fonts

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Scad;

var document = ScadDocument.Parse("""
    // A nameplate with the name cut into it: text is a 2D shape, so it extrudes and subtracts like one
    difference() {
        cube([60, 16, 4]);
        translate([30, 8, 2]) linear_extrude(height = 3)
            text("CanvasNet", size = 7, halign = "center", valign = "center");
    }

    // Raised bold italic serif lettering on a second plate, spaced a little wider
    translate([0, 24, 0]) {
        cube([60, 16, 2]);
        translate([4, 4, 2]) linear_extrude(height = 1.5)
            text("Hello", size = 8, font = "Liberation Serif:style=Bold Italic", spacing = 1.1);
    }

    // Stacked letters, top to bottom, and a text whose characters are laid out right to left
    translate([70, 30, 0]) linear_extrude(height = 2) text("ABC", size = 5, direction = "ttb");
    translate([90, 0, 0]) linear_extrude(height = 2) text("12345", size = 5, direction = "rtl");
    """);

// Make the named font independent of this machine: only the bundled Liberation fonts and ./fonts are consulted
var options = new ScadRenderOptions
{
    FontResolution = new ScadFontResolutionOptions { UseSystemFonts = false, AdditionalDirectories = ["./fonts"] },
};

using var image = document.Render(800, 400, options);
PngCodec.Save(image, "nameplates.png");

// The size is the height of the capital H of the font, and the ink starts at X = 0 (halign = "left"): in the bundled
// Liberation Sans, text("HI", size = 14.09) is 16.91 wide and 14.09 high
var scene = ScadDocument.Parse("linear_extrude(height = 1) text(\"HI\", size = 14.09);").BuildScene();
var bounds = scene.ComputeBounds();
Console.WriteLine($"{bounds.Max.X:F2} x {bounds.Max.Y:F2}"); // Output: 16.91 x 14.09
```

### Example 38: Drawing an OpenSCAD Model in a Pandoc Document

The Pandoc filter `docs/filters/canvasnet.lua` draws a picture whose file is a `.scad` model, using the
`canvasnet` command-line tool (see *Using the Tool in Pandoc Documents* in the tool chapter). The attributes choose
the viewpoint and set the model's variables, and `width` stays on the picture. This guide draws
`examples/bracket.scad` with a taller upright leg than the model's own:

```markdown
![Bracket with a taller upright leg](examples/bracket.scad){view=iso define-wall_height=44 size=1200x900 width=60%}
```

![Bracket with a taller upright leg](examples/bracket.scad){view=iso define-wall_height=44 size=1200x900 width=60%}
