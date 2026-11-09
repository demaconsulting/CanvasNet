# CanvasNet Mermaid Package

## API Reference

### MermaidDocument

`MermaidDocument` (namespace `DemaConsulting.CanvasNet.Mermaid`) is distributed via the separate
`DemaConsulting.CanvasNet.Mermaid` NuGet package, whose only dependency is the core `DemaConsulting.CanvasNet`
package - see the Installation section of the project README. It reads diagrams written in the Mermaid v11 text
language and draws them as **static pictures** with Mermaid's own layout: a flowchart is laid out by a port of the
dagre layout that Mermaid uses, and a sequence diagram by a port of Mermaid's sequence renderer, so the nodes, edges,
participants, messages, and labels land where Mermaid 11 puts them. This version draws **flowcharts** (the `flowchart`
and `graph` keywords) and **sequence diagrams** (the `sequenceDiagram` keyword). Every other diagram type that Mermaid
knows is recognized and reported, never drawn wrongly: class, state, entity relationship, pie, gantt, and mindmap
diagrams are planned later. The package is derived with attribution from the Mermaid project and the libraries it uses,
and it is **not affiliated with or endorsed by** the Mermaid project (see *Mermaid Third-Party Attribution*). A document
is **immutable and thread-safe**: it is read and validated once and can then be measured and rendered any number of
times, from several threads at once, each call giving the same result for the same options.

```csharp
public sealed partial class MermaidDocument
{
    public static MermaidDocument Parse(string text, MermaidOpenOptions? options = null);
    public static MermaidDocument Open(string path, MermaidOpenOptions? options = null);
    public static MermaidDocument Open(Stream stream, MermaidOpenOptions? options = null);

    public string? SourceName { get; }
    public string DiagramType { get; }      // "flowchart" (also for "graph"), "sequence", or "class", "pie", ...
    public bool IsSupported { get; }        // true when this version draws the type
    public string? Title { get; }           // the title statement, else the frontmatter title
    public string? AccTitle { get; }        // accTitle, metadata only
    public string? Direction { get; }       // TB, TD, BT, LR, or RL; null for a sequence diagram
    public int NodeCount { get; }           // the participants of a sequence diagram
    public int EdgeCount { get; }           // the messages of a sequence diagram
    public int SubgraphCount { get; }       // the boxes of a sequence diagram

    public MermaidSize Measure(
        MermaidRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public Surface Render(
        MermaidRenderOptions? options = null,
        CancellationToken cancellationToken = default);

    public Surface Render(
        int width,
        int height,
        MermaidRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

- `Parse` and the two `Open` overloads read the text as Mermaid reads it: the YAML frontmatter, the `%%{init: ...}%%`
  directives, and the `%%` comments are taken out, the diagram type is detected with Mermaid's own detectors, and a
  supported diagram is parsed with a port of its grammar. A file or stream is read as strict UTF-8 (a byte order mark
  is removed, invalid UTF-8 is rejected). Reading fails fast with a located `MermaidSyntaxException` and never opens
  another file or uses the network. `Open(Stream)` reads from the current position and leaves the stream open.
- The descriptive properties come from the text only. For a sequence diagram, `NodeCount` is the number of
  participants, `EdgeCount` the number of messages, and `SubgraphCount` the number of boxes. For a type that is not
  drawn, `IsSupported` is `false`, the counts are zero, and `Direction` and `AccTitle` are `null`.
- `Measure` lays the diagram out and returns the size that `Render` produces for the same options, without drawing. It
  delivers the same reports as `Render`.
- `Render()` draws the diagram onto a new `Surface` at its **natural size** (the view box of Mermaid's SVG, rounded up
  to whole pixels as `mmdc` does) multiplied by `MermaidRenderOptions.Scale`. The diagram is drawn sharply at the final
  size, not enlarged afterwards. The caller owns, and must dispose, the returned surface.
- `Render(width, height)` draws onto a surface of exactly that size: the diagram is scaled uniformly to fit and
  centered, and the margins take the background. `Scale` is not used.
- A known type that this version does not draw (`classDiagram`, `stateDiagram`, `pie`, ...) opens without error.
  Measuring or rendering it reports the unsupported feature `mermaid-diagram-unsupported` and gives an empty,
  transparent picture of 16 by 16 pixels (twice Mermaid's diagram padding), or throws in strict mode.

**Exceptions** (`Measure` and both `Render` overloads):

- `ArgumentOutOfRangeException`: the explicit size or the scaled natural size is outside 1 to `Surface.MaxDimension`
  (8192) on a side. A diagram larger than that needs a smaller `Scale` or the sized overload.
- `MermaidRenderException`: a limit of `MermaidLimits` was met while the diagram was laid out or painted
  (`mermaid-limit-exceeded`), or the diagram cannot be laid out (`mermaid-render-failed`, for example a subgraph that
  contains itself or an empty sequence block). An unexpected internal error of the layout is reported the same way
  (`mermaid-render-failed` at the diagram keyword, with the original exception as the inner exception); an exception
  of your image resolver reaches you unchanged.
- `MermaidUnsupportedFeatureException`: the first unsupported feature when `ThrowOnUnsupportedFeature` is set.
- `OperationCanceledException`: the token was canceled before or during the work.

`Parse` and `Open` throw `MermaidSyntaxException` (a `FormatException`) for text that is not a Mermaid diagram, invalid
frontmatter or shape data, a grammar error, or a structural limit (`MaxEdges`, `MaxNodes`, `MaxNestingDepth`,
`MaxLabelLength`, `MaxYamlDepth`), and `InvalidDataException` for text longer than `MaxTextSize`.

Every exception and report carries a stable **code** (the constants of `MermaidDiagnosticCodes`), a **location**, and,
where one helps, a **hint**. A `MermaidLocation` is the `File` (the source name), and the one-based `Line` and `Column`
in the original text, frontmatter and directive lines included, so an editor can go straight to the character.
`ToString()` gives `file:line:column`. A syntax error also lists the `Expected` tokens, as Mermaid's parser does:

```text
bad.mmd:2:9: mermaid-syntax-error: expected 'SQE', 'DOUBLECIRCLEEND', 'PE', ... or 'TAGSTART', found 'SQS' ('[')
Hint: close the label with the bracket that matches its opening bracket, or quote text that contains brackets:
A["a [b] c"]
```

### MermaidOpenOptions and MermaidRenderOptions

Both classes are immutable; every property is init-only and validated when it is set, so one instance can be shared
between threads and calls.

| `MermaidOpenOptions` property | Default | Meaning |
| ----------------------------- | ------- | ------- |
| `SourceName` | `null` (the path for `Open(string)`) | The name shown in locations and messages |
| `Limits` | `null` (`MermaidLimits.Default`) | The limits checked while the text is read |

<!-- markdownlint-disable MD013 -->
| `MermaidRenderOptions` property | Default | Meaning |
| ------------------------------- | ------- | ------- |
| `Scale` | 1 | The factor by which the natural size is multiplied, text included; finite, greater than zero |
| `Background` | `null` | The color under the diagram; `null` is transparent, as Mermaid's SVG is |
| `Config` | `null` | A Mermaid site configuration as JSON object text, like the argument of `mermaid.initialize` or the file of `mmdc -c` |
| `Theme` | `null` (`default`) | The site theme, one of `MermaidRenderOptions.ThemeNames` (`default`, `base`, `dark`, `forest`, `neutral`), like `mmdc -t` |
| `ImageResolver` | `null` | A core `IImageResolver` that loads the pictures of the nodes (see *Pictures in Nodes*); `null` draws none |
| `Limits` | `MermaidLimits.Default` | The resource limits of the call |
| `WarningHandler` | `null` | Receives each `MermaidDiagnostic` (a warning); `null` drops them |
| `UnsupportedFeatureHandler` | `null` | Receives each `MermaidUnsupportedFeature`; `null` drops them |
| `ThrowOnUnsupportedFeature` | `false` | Turns the first unsupported feature into a `MermaidUnsupportedFeatureException` (strict mode) |
<!-- markdownlint-enable MD013 -->

Reports are delivered once, in the order they were found, on the calling thread, after the work and before the method
returns or throws. A warning that is reported several times at the same place is delivered once.

### MermaidLimits

Mermaid text is untrusted input, so every resource a call can use is limited, and exceeding a limit fails closed with a
message that names the property. The limits bound the work, not the clock: the cancellation token is the only
wall-clock bound.

<!-- markdownlint-disable MD013 -->
| Property | Default | What it bounds | When it is exceeded |
| -------- | ------- | -------------- | ------------------- |
| `MaxTextSize` | 50,000 | The diagram text without frontmatter, directives, and comments (Mermaid's `maxTextSize`); the whole text may be four times as long | `InvalidDataException` |
| `MaxEdges` | 500 | The links of a flowchart (Mermaid's `maxEdges`, which Mermaid does not check for a sequence diagram) | `MermaidSyntaxException` |
| `MaxNodes` | 2,000 | The nodes, subgraphs included, or the participants of a sequence diagram | `MermaidSyntaxException` |
| `MaxLabelLength` | 4,096 | One label, in UTF-16 characters | `MermaidSyntaxException` |
| `MaxNestingDepth` | 32 | Subgraphs inside subgraphs, or the blocks and boxes of a sequence diagram (at most 256) | `MermaidSyntaxException` |
| `MaxYamlDepth` | 32 | Nested YAML collections of the frontmatter and the shape data (at most 256) | `MermaidSyntaxException` |
| `MaxLayoutWork` | 50,000,000 | The metered steps of the layout (ranking, ordering, positioning; the text measurement and placement of a sequence diagram) | `MermaidRenderException` |
| `MaxImages` | 256 | The pictures one render loads through the resolver | `MermaidRenderException` |
| `MaxPaintedPixels` | 250,000,000 | The device pixels one render paints (after `Scale`) | `MermaidRenderException` |
| `MaxDiagnostics` | 1,000 | The reports kept per call; the rest become one `mermaid-diagnostics-suppressed` warning | (rendering continues) |
<!-- markdownlint-enable MD013 -->

`MaxTextSize` and `MaxEdges` are Mermaid's own limits with Mermaid's defaults. The others are CanvasNet additions, so
a very large but valid diagram that Mermaid draws can fail here; raise the limit when you trust the text. The open
limits come from `MermaidOpenOptions.Limits` and the render limits from `MermaidRenderOptions.Limits`.

### Configuration and Themes

The effective configuration is built as Mermaid builds it, from lowest to highest precedence:

1. the built-in defaults of Mermaid 11;
2. the site configuration: `MermaidRenderOptions.Theme`, then `MermaidRenderOptions.Config` (a `theme` in `Config`
   wins over `Theme`);
3. the `config:` of the YAML frontmatter;
4. the `%%{init: ...}%%` directives of the text.

So a theme that the diagram sets itself always wins over the caller's theme, exactly as with `mmdc -t`. The text can
never change a **secure** key (`securityLevel`, `maxTextSize`, `maxEdges`, and so on): such a change is ignored with
the warning `mermaid-config-secure-key`. A key that Mermaid does not define is the warning `mermaid-config-unknown-key`
(its hint names the nearest key when one is close), and a value of the wrong type or range is `mermaid-config-invalid`,
which keeps the value of the lower layer.

The five built-in themes are `default`, `base`, `dark`, `forest`, and `neutral`. Each is derived as Mermaid derives
it, so `themeVariables` (such as `primaryColor`, `primaryTextColor`, `primaryBorderColor`, `lineColor`,
`secondaryColor`, `tertiaryColor`, `fontFamily`, and `fontSize`) change the colors that depend on them too. Use the
`base` theme to start from your own colors. The newer `neo`, `neo-dark`, and `redux` themes are not available; they are
reported as `mermaid-config-invalid` and the theme stays as it was.

```text
---
title: Release
config:
  theme: forest
  flowchart:
    curve: linear
    nodeSpacing: 40
---
flowchart LR
    build --> test --> ship
```

The flowchart settings follow Mermaid: `nodeSpacing`, `rankSpacing`, `curve` (`basis` by default; `linear`,
`natural`, `step`, `stepBefore`, `stepAfter`, `monotoneX`, `monotoneY`, `cardinal`, `catmullRom`, `bumpX`, `bumpY`, and
`rounded`), `padding`, `diagramPadding`, `wrappingWidth`, `htmlLabels`, `titleTopMargin`, `subGraphTitleMargin`, and
`inheritDir`, with the global `fontFamily`, `fontSize`, and `markdownAutoWrap`. The sequence settings are listed under
*Supported Sequence Syntax*.

### Supported Flowchart Syntax

<!-- markdownlint-disable MD013 -->
| Area | Supported |
| ---- | --------- |
| Header | `flowchart` and `graph` with the directions `TB`, `TD`, `BT`, `LR`, and `RL` |
| Node shapes | Every bracket form (`[ ]`, `( )`, `([ ])`, `[[ ]]`, `[( )]`, `(( ))`, `((( )))`, `> ]`, `{ }`, `{{ }}`, `[/ /]`, `[\ \]`, `[/ \]`, `[\ /]`) and the `@{ shape: ... }` syntax with all of Mermaid's named shapes (`rect`, `rounded`, `stadium`, `cyl`, `diam`, `hex`, `doc`, `docs`, `fork`, `delay`, and so on) |
| Links | Arrows, open links, dotted (`-.->`) and thick (`==>`) links, circle (`--o`) and cross (`--x`) heads, two-headed links, longer links (`--->`), labels (`-- text -->` and `-->\|text\|`), chains and `&` groups, invisible links (`~~~`), self-loops, and edge ids (`e1@-->`) with `curve` and `animate` shape data |
| Subgraphs | Titles, nesting, their own `direction`, and links to and from a subgraph |
| Labels | Quoted text, Markdown strings (`` "`**bold** and *italic*`" ``), `<br>` and the common inline tags (`b`, `strong`, `i`, `em`, `span` with a style, `img`), entity codes (`#quot;`, `#35;`), and wrapping at `wrappingWidth` |
| Styles | `style`, `classDef` (including `classDef default`), `class`, `:::`, and `linkStyle` (including `default` and `interpolate`), with `fill`, `stroke`, `stroke-width`, `stroke-dasharray`, `color`, `font-weight`, `font-style`, `font-size`, `font-family`, and `opacity` |
| Configuration | YAML frontmatter (`title`, `config`), `%%{init: ...}%%` directives, the five themes (`default`, `base`, `dark`, `forest`, `neutral`), `themeVariables`, and the flowchart settings (`nodeSpacing`, `rankSpacing`, `curve`, `padding`, `wrappingWidth`, and so on), with Mermaid's precedence |
| Pictures | `@{ img: "part.png", label, pos, w, h, constraint }` nodes and `<img>` in labels, through the caller's resolver |
| Titles | The frontmatter `title` is drawn above the diagram; `accTitle` and `accDescr` are read as metadata |
<!-- markdownlint-enable MD013 -->

### Supported Sequence Syntax

A sequence diagram starts with `sequenceDiagram`. It is laid out by a port of Mermaid's sequence renderer: the
participants stand in a row in the order they appear, and every message, note, activation, and block is placed below
the one before it, where Mermaid 11 puts it.

<!-- markdownlint-disable MD013 -->
| Area | Supported |
| ---- | --------- |
| Header | `sequenceDiagram` |
| Participants | `participant` and `actor`, aliases (`participant Bank as Payment provider`), and participants named only by a message |
| Participant types | The eight types of `@{ type: ... }`: `participant`, `actor`, `boundary`, `control`, `entity`, `database`, `collections`, and `queue`, with the `alias` key |
| Boxes | `box` with a color and a title (`box LightBlue Front end` ... `end`), with a color only, or with a title only |
| Create and destroy | `create participant` and `create actor` just before the first message of the participant, and `destroy` just before its last message |
| Messages | The arrows `->`, `-->`, `->>`, `-->>`, `<<->>`, `<<-->>`, `-x`, `--x`, `-)`, and `--)`, the half-arrow heads, self messages, and central connections (`A->>()B`, `A()->>B`, `A()->>()B`) |
| Activations | `activate` and `deactivate`, and the `+` and `-` shorthands on a message, nested |
| Notes | `Note left of`, `right of`, and `over` one or two participants |
| Blocks | `loop`, `alt` and `else`, `opt`, `par` and `and`, `par_over`, `critical` and `option`, and `break`, nested; `rect` backgrounds with a CSS color |
| Numbers | `autonumber`, with a start and a step, and `autonumber off`; the `showSequenceNumbers` setting |
| Text | `<br>` line breaks, the `wrap:` and `nowrap:` prefixes, wrapping when `wrap` is on (a word wider than the line is hyphenated), and entity codes |
| Menus and properties | `link`, `links`, and `properties`; a menu is drawn only with `forceMenus`, and a `class` in `properties` changes the paint of the participant |
| Titles | The `title` statement, else the frontmatter `title`, is drawn above the diagram; `accTitle` and `accDescr` are read as metadata |
| Configuration | YAML frontmatter, `%%{init: ...}%%` directives, the five themes, `themeVariables`, and the sequence settings (`actorMargin`, `mirrorActors`, `rightAngles`, `messageAlign`, `noteAlign`, `wrap`, and so on), with Mermaid's precedence |
<!-- markdownlint-enable MD013 -->

| Arrow | Line | End |
| ----- | ---- | --- |
| `->` | Solid | None |
| `-->` | Dotted | None |
| `->>` | Solid | Arrowhead |
| `-->>` | Dotted | Arrowhead |
| `<<->>` | Solid | Arrowheads at both ends |
| `<<-->>` | Dotted | Arrowheads at both ends |
| `-x` | Solid | Cross |
| `--x` | Dotted | Cross |
| `-)` | Solid | Open arrowhead (asynchronous) |
| `--)` | Dotted | Open arrowhead (asynchronous) |

The half-arrow heads draw one side of a head: `-|\` and `-|/` draw a solid half head at the top or at the bottom, and
`-\\` and `-//` a stick half head; with `--` the line is dotted (`--|\`, `--//`, and so on). Written before the line,
they point back at the sender: `/|-`, `\|-`, `//-`, and `\\-`, and the dotted `/|--`, `\|--`, `//--`, and `\\--`.

The sequence settings follow Mermaid (`config.sequence` in the frontmatter, a directive, or
`MermaidRenderOptions.Config`):

<!-- markdownlint-disable MD013 -->
| Key | Default | Meaning |
| --- | ------- | ------- |
| `mirrorActors` | `true` | Draws the participants again below the diagram |
| `actorMargin` | 50 | The least space between two participants |
| `width`, `height` | 150, 65 | The size of a participant box |
| `boxMargin`, `boxTextMargin` | 10, 5 | The margins around blocks and boxes, and around their text |
| `noteMargin` | 10 | The margin around a note |
| `activationWidth` | 10 | The width of an activation |
| `diagramMarginX`, `diagramMarginY` | 50, 10 | The margins around the diagram |
| `bottomMarginAdj` | 1 | The extra space below the diagram |
| `messageAlign` | `center` | The alignment of message text: `left`, `center`, or `right` (also `start`, `middle`, and `end`) |
| `noteAlign` | `center` | The alignment of note text, with the same values |
| `rightAngles` | `false` | Draws a self message with right angles |
| `showSequenceNumbers` | `false` | Numbers the messages without `autonumber` |
| `hideUnusedParticipants` | `false` | Leaves out the participants that no message, note, or activation names |
| `forceMenus` | `false` | Draws the menus of `link` and `links` |
| `wrap` | `false` | Wraps every text without a `nowrap:` prefix; a top-level `wrap: true` does the same |
| `wrapPadding` | 10 | The padding of wrapped text |
| `labelBoxWidth`, `labelBoxHeight` | 50, 20 | The size of the label box of a block |
| `textPlacement` | (none) | `old` draws the participant labels with Mermaid's older text placement; `fo` is reported and drawn with the default placement |
| `actorFontWeight`, `noteFontWeight`, `messageFontWeight` | 400 | The weight of the participant, note, and message text |
| `actorFontSize`, `noteFontSize`, `messageFontSize`, and the three font families | 14, 14, 16 | Replaced by the top-level `fontSize` and `fontFamily`, as in Mermaid |
| `useMaxWidth`, `arrowMarkerAbsolute` | | No meaning for a picture |
<!-- markdownlint-enable MD013 -->

As in Mermaid, the top-level `fontFamily` and `fontSize` replace the font families and sizes of the sequence settings,
so every sequence text is 16 pixels in the theme's font by default; set the top-level `fontSize` to change it. A
top-level `fontWeight` replaces the three weights in the same way. Other behaviors that follow Mermaid:

- A hidden menu still takes its room: the diagram is as large as if the menu panel below the participant (20 pixels
  plus 30 per link, and as wide as its widest label) were drawn.
- The lines of a menu link label are drawn on one baseline when the participant font size has a unit, such as an
  `actorFontSize` of `18px` with the top-level `fontSize` set to 0, because Mermaid spaces them by the size as a number.
- A participant with a `class` in `properties` is filled `#EDF2AE` and stroked `#666`, unless the class names a rule of
  the style sheet, such as `note`.
- The color of a `rect` or a `box` can be a CSS color, a color keyword, or a system color. A box gets the theme's
  `nodeBorder` stroke and `dropShadow`; a `rect` background gets neither.
- The `boundary`, `entity`, and `database` participants are `labelBoxHeight` taller than their figure, and `control`
  twice that.
- A participant type that Mermaid does not define draws nothing, and is reported as `mermaid-participant-type-unknown`.
- Note text is drawn in normal weight whatever `noteFontWeight` says, while the note is sized for that weight.

### Mermaid Unsupported Features and Strict Mode

A construct that Mermaid defines but this package does not draw is reported as a `MermaidUnsupportedFeature` with a
stable `Feature` code, a message, a location, and a hint, and the rest of the diagram is still drawn.
`ThrowOnUnsupportedFeature` turns the first one into a `MermaidUnsupportedFeatureException`. A **warning**
(`MermaidDiagnostic`) is a questionable construct that still renders as Mermaid would render it; strict mode does not
fail on a warning.

<!-- markdownlint-disable MD013 -->
| Code | Kind | Reported when |
| ---- | ---- | ------------- |
| `mermaid-diagram-unsupported` | Unsupported | The diagram type is not drawn by this version (every type but `flowchart`, `graph`, and `sequenceDiagram`); the picture is empty |
| `mermaid-icon-unsupported` | Unsupported | An icon (`fa:fa-car` in a label, an `@{ icon: ... }` node, an `icon` in the `properties` of a sequence participant); the node or participant keeps its shape and label without the icon |
| `mermaid-math-unsupported` | Unsupported | Math (`$$...$$`, drawn by KaTeX in Mermaid) in a sequence diagram; the text is drawn as written |
| `mermaid-look-unsupported` | Unsupported | A `look` other than `classic` (`handDrawn`, `neo`); the diagram is drawn classic |
| `mermaid-layout-unsupported` | Unsupported | A `layout` other than `dagre` (`elk`, also the `flowchart-elk` keyword); the diagram is laid out with dagre |
| `mermaid-theme-css-unsupported` | Unsupported | A `themeCSS` rule whose selector is not one of the diagram's own (such as `.node rect` in a flowchart), an at-rule, or a nested rule; the rule is not applied |
| `mermaid-config-unknown-key` | Warning | A configuration key Mermaid does not define; it is ignored |
| `mermaid-config-invalid` | Warning | A configuration value of the wrong type or range, or a theme that is not available; it is ignored (`sequence.textPlacement: fo` is drawn with the default placement) |
| `mermaid-participant-type-unknown` | Warning | A sequence participant has a type Mermaid does not define; it is not drawn, as in Mermaid, and the hint names the nearest type |
| `mermaid-config-secure-key` | Warning | The text tried to change a secure key (`securityLevel`, `maxTextSize`, `maxEdges`, and so on); the change is ignored |
| `mermaid-style-ignored` | Warning | A style declaration outside the supported subset; it is ignored |
| `mermaid-html-tag-ignored` | Warning | An HTML tag outside the supported label subset; the tag is dropped and its text is kept |
| `mermaid-image-unresolved` | Warning | A picture the resolver did not give; the node keeps its frame |
| `mermaid-text-missing-glyph` | Warning | A character that no bundled font has (for example CJK); it is drawn as the missing-glyph box |
| `mermaid-diagnostics-suppressed` | Warning | More reports than `MaxDiagnostics`; the rest are dropped |
<!-- markdownlint-enable MD013 -->

The codes of the exceptions are `mermaid-syntax-error` (the grammar), `mermaid-unknown-diagram` (no diagram keyword;
the hint names the nearest one), `mermaid-shape-unknown` (a shape name Mermaid does not define; the hint names the
nearest one), `mermaid-yaml-invalid` and `mermaid-yaml-unsupported` (frontmatter or shape data; YAML anchors, aliases,
and tags are refused), `mermaid-limit-exceeded`, and `mermaid-render-failed`. Interaction (`click`, `href`, `call`, and
tooltips) is read and ignored without a report, because a picture is static.

### Pictures in Nodes

A node can show a picture: `@{ img: "part.png", label: "Part", pos: "t", w: 120, h: 120, constraint: "on" }`, or an
`<img src="part.png">` in a label. The library never opens a file or the network for a picture: the link is handed, as
text, to `MermaidRenderOptions.ImageResolver`, and the resolver decides what it may refer to. Without a resolver no
picture is drawn and the warning `mermaid-image-unresolved` is reported. `CanvasNetViewer.CreateImageResolver(folder)`
of the `DemaConsulting.CanvasNet.Viewer` package confines the links to one folder and draws every CanvasNet format, so a
node can show a raster, an SVG file, an OpenSCAD model, a Vega-Lite chart, a page of a PDF, PPTX, or VSDX document, or
another diagram. `MaxImages` bounds the pictures of one render.

The size of a picture node follows Mermaid: `w` and `h` set the picture size, and with `constraint: "on"` the picture
keeps its aspect ratio. Give a picture node its `w` and `h` so that the layout does not depend on the picture: the
`canvasnet info` command and the Viewer's probe lay a diagram out without loading its pictures. A Viewer load at the
natural size (`CanvasNetViewer.Render` without `Width` and `Height`; a document that embeds the diagram always gives it
a size) reports the size of the diagram it drew, with its pictures, and charges `MaxDecodedPixels` for it.

### Fonts, Determinism, and Known Differences

Rendering is **deterministic**: two renders of one document with the same options give identical pixels, for every
degree of parallelism, on every machine, and in every culture. The package reads no clock, environment variable,
locale, or installed font, runs no script, and never uses the network. Text is measured and drawn with the bundled
fonts only: Mermaid's default `"trebuchet ms", verdana, arial, sans-serif` resolves to Liberation Sans, a family that
is not bundled falls back to it, and the bundled Noto fonts draw the characters it lacks. Compare the pixels of two
surfaces, not the bytes of two PNG files, because the PNG encoder of .NET 9 and later writes a different stream for the
same pixels.

The layout is checked against Mermaid itself: a set of reference diagrams is drawn by Mermaid 11.17.2 (with the same
Liberation Sans font) and every node, edge, and subgraph of the CanvasNet layout matches within a hundredth of a pixel,
as does every participant, message, note, activation, block, and box of a sequence diagram (text boxes within two
hundredths). The known differences from Mermaid are:

- **Fonts.** Mermaid uses the fonts of the browser, so its pictures differ from these where Trebuchet MS is installed.
  Against Mermaid drawn with Liberation Sans the text widths agree to a fraction of a pixel.
- **Labels.** HTML labels are emulated with Mermaid's rules (a line box of 1.5 em, wrapping at `wrappingWidth`). Only
  the tags `b`, `strong`, `i`, `em`, `br`, `img`, and `span` with a style are drawn; other tags are dropped with their
  text kept (`mermaid-html-tag-ignored`). Markdown labels support paragraphs, strong, and emphasis; links, tables, and
  strikethrough are plain text.
- **Icons, looks, and layouts.** Icons are not drawn, the `handDrawn` and `neo` looks are drawn classic, and `elk` is
  laid out with dagre (each is reported, see the table above).
- **Interaction and animation.** `click`, `href`, `call`, and tooltips are ignored; an animated edge is drawn as its
  first frame.
- **CSS.** `classDef`, `style`, `linkStyle`, and `themeCSS` honor only the declarations listed under *Styles*; there are
  no CSS animations. A `font-size` that text cannot be drawn at (zero, or more than 32768 pixels) is ignored with a
  `mermaid-style-ignored` warning and the inherited size is kept, and a `stroke-dasharray` with a dash longer than
  32768 pixels is drawn solid.
- **Security.** `securityLevel` changes nothing (no script ever runs). YAML anchors, aliases, and tags are refused,
  although Mermaid's YAML reader accepts them.
- **Errors.** For invalid text Mermaid draws an error picture; this package throws a located `MermaidSyntaxException`.
  A few graphs on which Mermaid itself fails (a subgraph that contains itself, a link to a subgraph that has no node, a
  subgraph also used as a node of another subgraph, a self-loop on a subgraph next to links into the subgraphs around
  it) are a located `mermaid-render-failed`. The legacy `A(-text-)` shape, which Mermaid 11 cannot draw, is drawn as an
  ellipse.
- **Shapes.** The circles of the newer shapes are true circles where Mermaid draws rough.js curves (a few hundredths
  of a pixel).
- **Sequence diagrams.** Mermaid draws a broken sequence diagram or fails for an empty block (a block with no message
  or note inside), for `link` or `links` on an `actor`, `boundary`, `control`, or `entity` participant, and for a
  `properties` icon that is not text; these are a located `mermaid-render-failed` instead. `$$` math is drawn and
  measured as written (`mermaid-math-unsupported`), and `textPlacement: fo` (HTML labels) is drawn with the default
  placement. The color keywords and system colors of a `box` or a `rect` background take the values of headless Chrome
  in the light scheme (system colors depend on the platform in a browser). The width of a text is the advance of its
  glyphs grown to the pixels its first and last glyphs reach, as Chrome measures it; for fonts or sizes other than those
  of the reference diagrams, a width can differ by a pixel, which can move the margins by up to a pixel.
- **Pictures.** A picture that cannot be loaded leaves the node's frame (Mermaid fails the whole render).
- **Background and size.** The background is transparent by default (as the SVG is), and `useMaxWidth` has no meaning
  for a picture of a fixed size.
- **Themes.** `neo`, `neo-dark`, and `redux` are not available.
- **Limits.** `MaxNodes`, `MaxNestingDepth`, `MaxLabelLength`, `MaxLayoutWork`, `MaxImages`, and `MaxPaintedPixels`
  have no Mermaid equivalent.
- **Version.** The target is Mermaid 11.17.2; later Mermaid versions are not followed automatically.

### Mermaid Third-Party Attribution

`DemaConsulting.CanvasNet.Mermaid` is **derived with attribution**: to draw diagrams the way Mermaid draws them, its
preprocessing, diagram detection, flowchart and sequence parsers and models, configuration defaults, themes, shapes,
edges, labels, and layouts are ports of these projects (all MIT unless stated):

- **mermaid** (Copyright (c) 2014 - 2022 Knut Sveidqvist): the preprocessing, detectors, flowchart grammar and
  database, configuration and the five themes, the shapes, clusters, edges, markers, and label rules of its renderer,
  the sequence grammar, database, renderer, drawing functions, and style sheet, and the text utilities they use;
- **dagre-d3-es**, derived from **dagre** and **graphlib** (Copyright (c) 2012-2014 Chris Pettitt and the dagre-es
  contributors): the graph and the layered layout;
- **js-yaml** (Copyright (C) 2011-2015 by Vitaly Puzrin): the YAML reader of the frontmatter and shape data;
- **khroma** (Copyright (c) 2019-present Fabio Spampinato, Andrew Maney): the color functions of the themes;
- **lodash** (Copyright OpenJS Foundation and other contributors): the sort and extremum rules the layout depends on;
- **marked** (Copyright (c) 2018+, MarkedJS; Copyright (c) 2011-2018, Christopher Jeffrey): the Markdown of labels;
- **d3-shape** and **d3-path** (ISC, Copyright Mike Bostock): the edge curves.

The license texts ship in the NuGet packages under `licenses/` (also for the `canvasnet` tool, which bundles the
assembly), and `src/DemaConsulting.CanvasNet.Mermaid/third-party/README.md` records what was ported, from which
version, and by which unit. The package is **not affiliated with, endorsed by, or promoted by** the Mermaid project or
the authors of these libraries; the names are used only to describe the language that is implemented.

### Using As An Embedded Image

`MermaidImageSource` is the image source of this package for `.mmd` and `.mermaid` files. With it a document that links
to a diagram (an SVG `<image href="flow.mmd">`, a picture of a Markdown document) draws the diagram through a
`FolderImageResolver` of the core package, or `CanvasNetViewer.CreateImageResolver` of the Viewer package, which
includes it. The diagram is laid out afresh at the size of its placement, on a transparent background, and its own
pictures load through the resolver of the diagram's own file, so they share the budgets and the folder rule of the
whole render. Warnings are returned as notes of the source `mermaid` with the code `mermaid-warning` (the specific
`mermaid-*` code is in `Detail`), and unsupported features as notes whose code is the feature.

The source declares one fragment parameter, `theme`: `flow.mmd#theme=dark` sets the site theme like
`MermaidRenderOptions.Theme` (a theme the diagram sets itself still wins). A value that is not a theme refuses the
link with `image-fragment-invalid`, and any other parameter is the warning `image-fragment-unknown-parameter`.

```csharp
// An SVG document that links to diagrams, drawn through the resolver
var resolver = new FolderImageResolver("assets", [new MermaidImageSource()]);
var options = new SvgLoadOptions { ImageResolver = resolver };
using var surface = SvgCodec.Load("assets/diagram.svg", 800, 600, fonts: null, options);
```

## Examples

### Example 52: Rendering a Mermaid Flowchart

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Mermaid;

// MermaidDocument.Open("order.mmd") would read the same text from a file
var diagram = MermaidDocument.Parse("""
    ---
    title: Order flow
    ---
    flowchart LR
        start([New order]) --> stock{In stock?}
        stock -- yes --> pack[Pack the order]
        stock -- no --> reorder[/Reorder/]
        reorder --> stock
        subgraph ship [Shipping]
            pack --> send((Send))
        end
    """);

// The document describes the text
Console.WriteLine($"{diagram.DiagramType} {diagram.Direction}");     // Output: flowchart LR
Console.WriteLine(diagram.Title);                                     // Output: Order flow
Console.WriteLine($"{diagram.NodeCount} nodes, {diagram.EdgeCount} edges, {diagram.SubgraphCount} subgraph");
                                                                      // Output: 5 nodes, 5 edges, 1 subgraph

// Measure lays the diagram out without drawing; Render draws it sharply at twice the size over white
MermaidSize natural = diagram.Measure();
Console.WriteLine($"{natural.Width}x{natural.Height}");               // Output: 674x286
var white = new Rgba32(255, 255, 255, 255);
using var picture = diagram.Render(new MermaidRenderOptions { Scale = 2, Background = white });
PngCodec.Save(picture, "order-flow.png");                             // 1348x572 pixels

// Fit the diagram into a fixed box: it is scaled uniformly and centered
using var thumbnail = diagram.Render(320, 160, new MermaidRenderOptions { Background = white });
PngCodec.Save(thumbnail, "order-flow-thumbnail.png");
```

### Example 53: Mermaid Errors, Unsupported Features, and Strict Mode

```csharp
using DemaConsulting.CanvasNet.Mermaid;

// A grammar error is located in the original text and carries a hint
try
{
    MermaidDocument.Parse("flowchart LR\n    A[a [b] c] --> B\n", new MermaidOpenOptions { SourceName = "bad.mmd" });
}
catch (MermaidSyntaxException ex)
{
    Console.WriteLine($"{ex.Code} at {ex.Location}");  // Output: mermaid-syntax-error at bad.mmd:2:9
    Console.WriteLine(ex.Hint);                        // Output: close the label with the bracket that matches ...
}

// A known type that this version does not draw opens, is reported, and gives an empty picture
var classes = MermaidDocument.Parse("classDiagram\n    Animal <|-- Duck\n");
Console.WriteLine($"{classes.DiagramType} {classes.IsSupported}");    // Output: class False
var reports = new List<MermaidUnsupportedFeature>();
using var empty = classes.Render(new MermaidRenderOptions { UnsupportedFeatureHandler = reports.Add });
Console.WriteLine($"{reports[0].Feature} {empty.Width}x{empty.Height}");
                                                                      // Output: mermaid-diagram-unsupported 16x16

// An icon is left out and reported; strict mode makes it an error
var icons = MermaidDocument.Parse("flowchart LR\n    car[fa:fa-car Car] --> road[Road]\n");
try
{
    using var strict = icons.Render(new MermaidRenderOptions { ThrowOnUnsupportedFeature = true });
}
catch (MermaidUnsupportedFeatureException ex)
{
    Console.WriteLine(ex.Feature);                                    // Output: mermaid-icon-unsupported
}
```

### Example 54: Themes, Configuration, and Limits for a Mermaid Diagram

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Mermaid;

var diagram = MermaidDocument.Open("pipeline.mmd");
var options = new MermaidRenderOptions
{
    Theme = "dark",                                                    // like mmdc -t dark
    Config = """{ "flowchart": { "curve": "linear", "nodeSpacing": 30 } }""",   // like mmdc -c
    Background = new Rgba32(0x33, 0x33, 0x33, 255),
    Limits = new MermaidLimits { MaxNodes = 200, MaxLayoutWork = 5_000_000 },
    WarningHandler = warning => Console.Error.WriteLine(warning),     // file:line:column: code: message
};
using var picture = diagram.Render(options);
PngCodec.Save(picture, "pipeline-dark.png");
```

A key that Mermaid does not define is a warning (its `Hint` names the nearest key when one is close), and the rest of
the configuration still applies:

```text
flow.mmd:4:5: mermaid-config-unknown-key: 'flowchart.nodeSpace' in the frontmatter is not a configuration key Mermaid
accepts there; it is ignored
```

### Example 55: A Mermaid Diagram with Pictures

A diagram whose nodes show an OpenSCAD model, an SVG picture, and a Vega-Lite chart that lie next to it
(`examples/mermaid-pictures.mmd`, `bracket.scad`, `swatch.svg`, and `stall-sales.vl.json`). The library asks the
resolver for each link; the tool, the Viewer, and the Pandoc filter wire the same resolver rooted at the folder of the
diagram.

```csharp
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.Viewer;

var diagram = MermaidDocument.Open("examples/mermaid-pictures.mmd");
var options = new MermaidRenderOptions
{
    ImageResolver = CanvasNetViewer.CreateImageResolver("examples"),  // links stay inside this folder
    WarningHandler = warning => Console.WriteLine(warning),          // a picture that was not drawn is a warning
};
using var picture = diagram.Render(options);
PngCodec.Save(picture, "pictures.png");
```

```markdown
![Pictures in nodes](examples/mermaid-pictures.mmd){scale=2 width=80%}
```

![Pictures in nodes](examples/mermaid-pictures.mmd){scale=2 width=80%}

### Example 56: Drawing a Mermaid Diagram in a Pandoc Document

The Pandoc filter `docs/filters/canvasnet.lua` draws a picture whose file is a `.mmd` or `.mermaid` diagram, and a
fenced code block of class `mermaid` written in the document itself. The block reads the attributes of a `.mmd` picture
(`size`, `scale`, `background`, and `theme`); `caption` makes it a figure, and every other attribute stays on the
picture:

````markdown
```{.mermaid caption="A deployment, drawn from a code block" theme=forest scale=2 width=60%}
flowchart LR
    commit[Commit] --> ci{CI green?}
    ci -- yes --> deploy([Deploy])
    ci -- no --> fix[Fix] --> commit
```
````

```{.mermaid caption="A deployment, drawn from a code block" theme=forest scale=2 width=60%}
flowchart LR
    commit[Commit] --> ci{CI green?}
    ci -- yes --> deploy([Deploy])
    ci -- no --> fix[Fix] --> commit
```

A plain ` ```mermaid ` block works the same way, without a caption. The text of a block is written to the filter's
cache folder and drawn from there, so a diagram that shows pictures must be a `.mmd` file of its own (as in Example
55). A block or a `.mmd` picture of a type that this version does not draw (`classDiagram`, `pie`, ...) is
left as written, with the tool's message as a warning, so that another filter, such as `mermaid-filter`, can draw it.

### Example 57: The Mermaid Gallery

The gallery is a set of small flowcharts and sequence diagrams that together use the major features of the package.
Every diagram is **original to this repository**, lies in `docs/user_guide/examples` as `mermaid-*.mmd`, and is drawn
below by the Pandoc filter, so the pictures are always those of the version of the library that built this guide. A
test (`MermaidGalleryTests`) renders every file with `ThrowOnUnsupportedFeature = true` and fails on any warning, so a
diagram of the gallery cannot use a feature that is only reported. The same files can be drawn with the tool, for
example `canvasnet render docs/user_guide/examples/mermaid-shapes.mmd --strict --scale 2 -o shapes.png`.

*Shapes.* The bracket shapes and two named shapes, with a frontmatter title and labeled links:

![Shapes of a release](examples/mermaid-shapes.mmd){scale=2 width=100%}

*Links.* Open, dotted, and thick links, circle and cross heads, two heads, a longer link, a self-loop, and an invisible
link that keeps two nodes in one rank:

![Kinds of links](examples/mermaid-links.mmd){scale=2 width=80%}

*Subgraphs.* Nested subgraphs, one with its own direction, and links to and from a subgraph:

![Nested subgraphs](examples/mermaid-subgraphs.mmd){scale=2 width=60%}

*Styles.* `classDef`, `class`, `:::`, `style`, and `linkStyle` with a color, a width, and a dash pattern:

![Styled nodes and links](examples/mermaid-styles.mmd){scale=2 width=70%}

*Labels.* A Markdown label, a long label wrapped at `wrappingWidth`, a two-line edge label, and entity codes:

![Labels](examples/mermaid-labels.mmd){scale=2 width=40%}

*A theme of your own.* The `base` theme with `themeVariables` and a `stepAfter` curve, all from the frontmatter:

![A custom theme](examples/mermaid-theme.mmd){scale=2 width=90%}

*The same diagram in another theme.* The `theme` attribute of the filter (the `--theme` option of the tool) applies
the `dark` theme to the subgraphs diagram, over a dark background:

![The dark theme](examples/mermaid-subgraphs.mmd){theme=dark background=#333333 scale=2 width=60%}

*Sequence blocks.* A frontmatter title, an activation, `loop`, `alt` and `else`, `opt`, `par` and `and`, a `rect`
background inside a loop, an asynchronous message, and a note over two participants:

![Sequence blocks](examples/mermaid-sequence-blocks.mmd){scale=2 width=80%}

*Participant types.* The eight participant types, two boxes with a color and a title, a self message, and a
participant that is created and destroyed:

![Participant types](examples/mermaid-sequence-participants.mmd){scale=2 width=100%}

*Sequence arrows.* `autonumber`, solid and dotted arrows, open, cross, and two-headed arrows, a half arrowhead, central
connections, and a note that wraps, in the `forest` theme without the mirrored participants:

![Sequence arrows](examples/mermaid-sequence-arrows.mmd){scale=2 width=70%}

### Example 58: Rendering a Mermaid Sequence Diagram

A sequence diagram is opened, measured, and drawn exactly like a flowchart. Its counts are the participants, the
messages, and the boxes, and it has no direction:

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Mermaid;

var diagram = MermaidDocument.Parse("""
    ---
    title: Checkout
    ---
    sequenceDiagram
        autonumber
        actor Buyer
        participant Shop
        participant Bank as Payment provider
        Buyer->>+Shop: Place the order
        Shop->>Bank: Charge the card
        alt Card accepted
            Bank-->>Shop: Approved
            Shop-->>Buyer: Order confirmed
        else Card declined
            Bank-->>Shop: Declined
            Shop-->>Buyer: Try another card
        end
        deactivate Shop
        Note over Buyer,Shop: The receipt is sent by email
    """);

Console.WriteLine($"{diagram.DiagramType} {diagram.Direction ?? "-"}");   // Output: sequence -
Console.WriteLine(diagram.Title);                                         // Output: Checkout
Console.WriteLine($"{diagram.NodeCount} participants, {diagram.EdgeCount} messages, {diagram.SubgraphCount} boxes");
                                                                          // Output: 3 participants, 6 messages, 0 boxes

MermaidSize natural = diagram.Measure();
Console.WriteLine($"{natural.Width}x{natural.Height}");                   // Output: 650x622
var white = new Rgba32(255, 255, 255, 255);
using var picture = diagram.Render(new MermaidRenderOptions { Theme = "forest", Scale = 2, Background = white });
PngCodec.Save(picture, "checkout.png");                                   // 1300x1244 pixels
```

The same text in a Pandoc document, drawn by the filter in the `forest` theme:

```{.mermaid caption="A checkout, drawn from a code block" theme=forest scale=2 width=70%}
---
title: Checkout
---
sequenceDiagram
    autonumber
    actor Buyer
    participant Shop
    participant Bank as Payment provider
    Buyer->>+Shop: Place the order
    Shop->>Bank: Charge the card
    alt Card accepted
        Bank-->>Shop: Approved
        Shop-->>Buyer: Order confirmed
    else Card declined
        Bank-->>Shop: Declined
        Shop-->>Buyer: Try another card
    end
    deactivate Shop
    Note over Buyer,Shop: The receipt is sent by email
```
