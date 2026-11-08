# Third-Party Provenance and Attribution

`DemaConsulting.CanvasNet.Mermaid` implements the Mermaid text language. The package is **derived with
attribution**: to draw diagrams the way Mermaid draws them, its preprocessing, diagram detection, parsers,
configuration defaults, themes and layout are ports of the projects listed below. Every ported item is acknowledged
here, the license texts of the upstream projects ship with the package (in its `licenses\` folder), and every source
file that contains ported logic starts with the header comment
`// Portions derived from <project> (MIT); see third-party/README.md`. Ported code is local source code, maintained
and tested in this repository; no upstream package is a dependency.

This project is **not affiliated with, and is not endorsed or promoted by**, the Mermaid project, its authors or the
authors of js-yaml. The names are used only to describe the language that is implemented.

## Provenance table

| Upstream project | Version studied | License | Copyright notice | License file | CanvasNet unit that ports it | What is ported |
| ---------------- | --------------- | ------- | ---------------- | ------------ | ---------------------------- | -------------- |
| mermaid (`mermaid`) | 11.17.2 (the version pinned in the repository's `package-lock.json`; read from the original sources in `dist/mermaid.js.map`) | MIT | Copyright (c) 2014 - 2022 Knut Sveidqvist | `LICENSE-mermaid.txt` | `Preprocessor` (`Syntax/`) | The preprocessing of `preprocess.ts` (`cleanupText`, `processFrontmatter`, `processDirectives`), `diagram-api/frontmatter.ts` (`extractFrontMatter`: which frontmatter keys are kept and how), `diagram-api/regexes.ts` (the frontmatter, directive and comment expressions, reimplemented as scanners with the same matches), `diagram-api/comments.ts` (`cleanupComments`), `utils.ts` (`detectDirective`, `removeDirectives`, `encodeEntities` and its private entity markers) and the `maxTextSize` default and check of `mermaidAPI.ts` |
| mermaid (`mermaid`) | 11.17.2 | MIT | Copyright (c) 2014 - 2022 Knut Sveidqvist | `LICENSE-mermaid.txt` | `DiagramDetector` (`Syntax/`) | `diagram-api/detectType.ts` (the clean-up before detection and the first-match rule), the registration order of `diagram-api/diagram-orchestration.ts` (`addDiagrams`, with the large features), every `*Detector.ts` / `detector.ts` expression of the diagram folders, the error message of the `---` pseudo-diagram, and the `defaultRenderer` defaults of `config.schema.yaml` that the flowchart, class and state detectors read |
| mermaid (`mermaid`) | 11.17.2 | MIT | Copyright (c) 2014 - 2022 Knut Sveidqvist | `LICENSE-mermaid.txt` | `SourceScanner` (`Syntax/`) | The lexer semantics of the jison 0.4.18 generated parsers that Mermaid ships (first matching rule in rule order without the `flex` option, start-condition stack, `less`, the end-of-input rule run once, the "Expecting ..., got ..." parse error), as read from the generated `flow.jison` |
| mermaid (`mermaid`) | 11.17.2 | MIT | Copyright (c) 2014 - 2022 Knut Sveidqvist | `LICENSE-mermaid.txt` | `MermaidDocument`, `MermaidLimits` (package root) | The default `maxTextSize` (50,000) and `maxEdges` (500), the default `diagramPadding` (8) behind the empty picture of a type that is not drawn, and the rounding of the picture size of `@mermaid-js/mermaid-cli` (the bounding box rounded up to whole pixels) |
| js-yaml (`js-yaml`, as bundled by mermaid) | 4.3.0 (the version in mermaid's source map) | MIT | Copyright (C) 2011-2015 by Vitaly Puzrin | `LICENSE-js-yaml.txt` | `YamlSubsetParser` (`Syntax/`) | The loader of `lib/loader.js` (`loadDocuments`, `readDocument`, `composeNode`, `readBlockSequence`, `readBlockMapping`, `readFlowCollection`, `readBlockScalar`, `readPlainScalar`, `readSingleQuotedScalar`, `readDoubleQuotedScalar`, `skipSeparationSpace`, `storeMappingPair` and their error messages) without tags, anchors, aliases, explicit keys and directives, and the implicit types of `JSON_SCHEMA` (`lib/type/null.js`, `bool.js`, `int.js`, `float.js`) |

## Original work

The public API, the location map, the scene model and painter (`Scenegraph/`), the resource limits other than
`maxTextSize` and `maxEdges`, the diagnostics, hints and codes, and the tests are original work of this repository.
The test fixtures are original diagrams.
