## Syntax

![Syntax Structure](PlantUmlSyntaxView.svg)

### Overview

The `Syntax` subsystem (namespace `DemaConsulting.CanvasNet.PlantUml.Syntax`, folder
`src/DemaConsulting.CanvasNet.PlantUml/Syntax/`) is the first stage of the pipeline: it turns a PlantUML text into the
statements that the diagram parsers read, with every position mapped back to the text the author wrote, and detects
the diagram type. It is **internal**: its types are visible only to the other parts of the system and, through
`InternalsVisibleTo`, to the test assembly. It contains two units:

- **Preprocessor** - the internal static `Preprocessor` with the records `DiagramBlock` and `PreprocessedSource`
  (`Preprocessor.cs`), and the location map `SourceLines` and `SourceStatement` (`Preprocessor.MappedText.cs`): the
  framing of the first diagram block, the comments, the continued lines, the supported directives, the reports of the
  others, and the size and nesting limits; see *Preprocessor Unit Design*
- **DiagramDetector** - the internal static `DiagramDetector` with the record `DetectedDiagram`: the diagram type from
  the tag or from the statements, the recognized types that are not drawn, and the errors; see *DiagramDetector Unit
  Design*

The subsystem's namespace documentation is in `Syntax/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetPlantUml-Syntax-*` requirements in `docs/reqstream/canvas-net-plant-uml/syntax.yaml`. The SysML2 model names
the subsystem `PlantUmlSyntax` (see *CanvasNetPlantUml System Design*).

### Interfaces

**Exposed** (internal only): `Preprocessor.Run(string, string?, PlantUmlLimits)` with its result
`PreprocessedSource(Lines, Block, Reports)` and `DiagramBlock(Tag, Location, Statements)`,
`Preprocessor.SourceLengthFactor` and `Preprocessor.TagOf`; `SourceLines` (`Locate`, `LineText`, `StartOf`, `Count`)
and `SourceStatement` (`Text`, `Location`, `Locate`, `OriginOf`, `Slice`, and its `Builder`);
`DiagramDetector.Detect(PreprocessedSource)`, `DiagramDetector.UnsupportedHint`, `DiagramDetector.SupportedTypes`,
`DiagramDetector.TagTypes`, `DiagramDetector.OuterStatements`, `DiagramDetector.BlockCloser`,
`DiagramDetector.IsCloser`, and `DiagramDetector.EditDistance`.

**Consumed**: the public `PlantUmlLimits` (`MaxTextSize`, `MaxNestingDepth`), `PlantUmlLocation`,
`PlantUmlSyntaxException`, `PlantUmlDiagnosticCodes`, and the internal `PlantUmlPendingReport` of the root namespace,
and the .NET base class library. The subsystem uses no other subsystem of the system and no part of the core
`CanvasNet` library.

**Consumers**: `PlantUmlDocument` preprocesses and detects every text it opens and reads the unsupported-type hint;
the diagram parsers read the statements and locate their errors with `SourceStatement`, and use the multi-line block
rules of the detector.

### Design

The document calls `Preprocessor.Run`, which frames the first block, strips the comments, joins continued lines, and
applies the directives, producing statements that keep the original offset of every character. `DiagramDetector.Detect`
then reads the tag of the block, or classifies the statements outside multi-line text blocks by their leading
keywords and arrows, and names the type. Both units are stateless functions of the text and the limits; the
preprocessor keeps its per-call state (variables, macros, open blocks) in a private object that lives for one call.
