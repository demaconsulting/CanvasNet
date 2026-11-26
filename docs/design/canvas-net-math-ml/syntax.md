## Syntax

![Syntax Structure](MathMlSyntaxView.svg)

### Overview

The `Syntax` subsystem (namespace `DemaConsulting.CanvasNet.MathMl.Syntax`, folder
`src/DemaConsulting.CanvasNet.MathMl/Syntax/`) is the first stage of the pipeline: it turns MathML text into the
equation tree, with every element, attribute, text, report, and error located in the text the author wrote, and parses
the values of the attributes that the layout reads. It is **internal**: its types are visible only to the other parts of
the system and, through `InternalsVisibleTo`, to the test assembly. It contains four units:

- **CharacterReferences** - the internal static `CharacterReferences` with the records `PrePassResult` and the class
  `LocationMap` (`CharacterReferences.cs`) and the generated table of the 2125 W3C names
  (`CharacterReferences.Table.cs`): the rewriting of named references into numeric references, the refusals of unknown
  names, document types, and foreign encodings, and the map of the rewritten positions; see *CharacterReferences Unit
  Design*
- **MathMlReader** - the internal static `MathMlReader` with the record `MathReadResult` (`MathMlReader.cs`): the root,
  the foreign content, the whitespace, the annotations, the links, the locations, and the reading limits; see
  *MathMlReader Unit Design*
- **MathTree** - the internal records `MathNode`, `MathElement`, `MathText`, and `MathAttribute` (`MathTree.cs`): the
  single internal equation model; see *MathTree Unit Design*
- **AttributeValues** - the internal static `AttributeValues` with `MathLength` and `MathLengthUnit`
  (`AttributeValues.cs`) and the record `StyleDeclarations` with `MathDepthValue`, `MathDepthKind`, and `StyleIssue`
  (`StyleDeclarations.cs`): the parsers of attribute values and of the inline style subset; see *AttributeValues Unit
  Design*

The subsystem's namespace documentation is in `Syntax/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMathMl-Syntax-*` requirements in `docs/reqstream/canvas-net-math-ml/syntax.yaml`. The SysML2 model names the
subsystem `MathMlSyntax` (see *CanvasNetMathMl System Design*).

### Interfaces

**Exposed** (internal only): `MathMlReader.Read(string, string?, MathMlLimits)` with its result
`MathReadResult(Root, ElementCount, Reports, Annotations)`, `MathMlReader.CollapseWhitespace`, and
`MathMlReader.MathMlNamespace`; `CharacterReferences.Rewrite`, `TryGetValue`, `Count`, and `PositionOf`, with
`PrePassResult` and `LocationMap.MapBack`; the `MathNode`, `MathElement` (`ElementChildren`, `TextContent`,
`GetAttribute`, `IsTextTokenName`), `MathText`, and `MathAttribute` records; `AttributeValues.TryParseLength`,
`TryParseBoolean`, `TryParseUnsignedInteger`, `TryParseScriptLevel`, and `TryParseColor`, `MathLength.Resolve`, and
`StyleDeclarations.Parse`.

**Consumed**: the public `MathMlLimits` (`MaxElements`, `MaxNestingDepth`, `MaxTokenLength`, `MaxDiagnostics`),
`MathMlLocation`, `MathMlAnnotation`, `MathMlSyntaxException`, `MathMlDiagnosticCodes`, and the internal
`MathMlPendingReport` of the root namespace; the core `Packaging` subsystem (`SafeXml.CreateReader`: document types
prohibited, no resolver, a character cap) and `Canvas` subsystem (`CssColor`, `Rgba32`); and the .NET base class
library. The subsystem uses no other subsystem of the system.

**Consumers**: `MathMlDocument` reads every text with `MathMlReader.Read` and keeps the tree; the Layout subsystem reads
the tree and parses its attribute values and inline styles with `AttributeValues` and `StyleDeclarations`; the painter
reads the locations of the elements.

### Design

`MathMlReader.Read` first has `CharacterReferences.Rewrite` turn every named reference into numeric references, which
keeps the lines of the text and records the column shifts of each changed line in a `LocationMap`. It encodes the
rewritten text as strict UTF-8 into a memory stream and reads it with the hardened reader of the core, which decodes the
numeric references; every position the reader reports (elements, attributes, errors) is mapped back to the original text
through the map, also the positions that the messages of the reader cite. An XML declaration of any encoding other than
UTF-8 is refused by the pre-pass, so the reader always decodes the text as the UTF-8 it was encoded as, and literal
characters of every plane are read as written. The reader walks the nodes iteratively with an explicit stack of open
elements and builds the immutable `MathTree`, keeping what it finds to report as pending reports in text order (at most
`MaxDiagnostics` of them, then one suppression notice). The `AttributeValues` parsers are pure
functions used later by the Layout subsystem; they refuse what they do not accept and leave the reporting to their
caller. All units are stateless functions of their input (the scanner and the read state live for one call), so the
subsystem is safe for concurrent use.
