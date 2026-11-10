### SvgFlattener

#### Verification Approach

`SvgFlattener` is verified with unit tests in `SvgFlattenerTests.cs` (the operation) and `SvgFlattenerLinkTests.cs`
(the link scanner and the rebasing) in the Viewer test project. The tests write small SVG drawings to a temporary
directory next to copies of the Mermaid diagram and the OpenSCAD model of the fixtures, the PDF of the fixture factory,
and solid PNG files, call `SvgFlattener.Execute` on the probed drawing, and examine the copy as text: the data URIs are
decoded and their PNG headers read for the sizes, and putting the original link back over the data URI must give the
input byte for byte. Nothing is mocked; the drawing of the pictures is the resolver of render, verified with
`SourceLoader` and the core resolver. The tests assert outcomes only (bytes, sizes, codes), never time.

#### Test Environment

N/A - standard test environment; the tests write only to temporary directories that each test deletes.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Exactly the links to files only CanvasNet draws (and nested SVG files that link one) are replaced, at their placement
  size times the scale, and every other byte of the drawing is kept.
- Nothing is written when nothing is replaced; a link the drawing never shows is the warning
  `svg-flatten-image-not-drawn`.
- The sandbox and limits of render apply, a copy over the limit is `size-limit`, and every refused input or output has
  its documented code.

#### Test Scenarios

**SvgFlattener_Execute_MermaidImage_ReplacedWithPngDataUri**: a 60 by 40 placement of a diagram becomes one PNG of 60 by
40 pixels, the link is gone, and the result reports the size of the copy.

**SvgFlattener_Execute_ReplacedLink_OnlyAttributeValueChanges** and **SvgFlattener_Execute_BomAndLineEndings_Preserved**:
a comment that contains an `image` tag, an unknown element, character data, odd spacing around `=`, single quotes, a byte
order mark, and CRLF and lone CR line breaks are kept; the copy is the input with only the value changed.

**SvgFlattener_Execute_ViewBoxTransformAndScale_RenderAtPlacementSize**,
**SvgFlattener_Execute_RotatedPlacement_UsesEdgeLengths**, and
**SvgFlattener_Execute_LargeDrawing_PictureKeepsFullPlacementSize**: a placement scaled by a group and by the scale, a
rotated placement (its edge lengths), and a placement in a drawing larger than the detection canvas get their exact
sizes.

**SvgFlattener_Execute_PlainRasterAndPlainSvg_NothingWritten** and
**SvgFlattener_Execute_DataUriAndFragmentLinks_LeftUntouched**: a raster, an SVG file that links only a raster, a data
URI, and a `#` link are kept, and with nothing to replace nothing is written.

**SvgFlattener_Execute_NestedSvgLinkingDiagram_ReplacedAsWhole** and
**SvgFlattener_Execute_DeeplyNestedComposite_ReplacedAtTopLevel**: an SVG file that links a diagram, directly or through
another SVG file, is replaced as a whole.

**SvgFlattener_Execute_XlinkHrefWithFragment_FragmentApplied** and
**SvgFlattener_Execute_HrefAndXlinkHref_OnlyEffectiveAttributeRewritten**: `xlink:href="doc.pdf#page=1"` is replaced and
reported as written; when an element has both attributes only `href` is rewritten.

**SvgFlattener_Execute_SameHrefTwice_BothReplacedAtLargestSize**: one link in two placements gives one entry with two
elements and the larger size in both.

**SvgFlattener_Execute_HiddenDiagram_ReportsNotDrawnAndWritesNothing**: a diagram in an unused definition is the warning
`svg-flatten-image-not-drawn` with the file and line, and nothing is written.

**SvgFlattener_Execute_MissingLinkedFile_ReportsUnresolved**,
**SvgFlattener_Execute_MissingLinkedFileStrict_ThrowsUnsupportedFeature**,
**SvgFlattener_Execute_LinkOutsideFolder_RefusedBySandbox**, **SvgFlattener_Execute_SvgCycle_StopsWithoutReplacingTheCycle**,
and **SvgFlattener_Execute_MoreThan64Images_ExtraLinksStayAndAreReported**: a missing file, a path that leaves the
folder, a cycle, and the 65th picture are reported as `svg-image-unresolved` and stay links; with strict a missing file
is
`unsupported-feature`.

**SvgFlattener_Execute_OutputOverLimit_ThrowsSizeLimitAndWritesNothing** and
**SvgFlattener_Execute_OutputInOtherFolder_RebasesRemainingRelativeLinks**: a copy over a 1000-byte limit is
`size-limit` with no file; a copy in a sub folder rewrites `my logo.png` to `../my%20logo.png`.

**SvgFlattener_Execute_NotSvg_ThrowsUnsupportedFormat**, **SvgFlattener_Execute_Doctype_ThrowsCorruptInput**,
**SvgFlattener_Execute_InvalidUtf8_ThrowsCorruptInput**, **SvgFlattener_Execute_NonUtf8Declaration_ThrowsUnsupportedFeature**,
**SvgFlattener_Execute_OutputIsInput_ThrowsSameInputOutput**, **SvgFlattener_Execute_NoClobberExisting_ThrowsOutputExists**,
**SvgFlattener_Execute_BrokenLinkedModel_ThrowsScadErrorNamingBothFiles**, and
**SvgFlattener_Execute_ScaleOutOfRange_ThrowsUsageError**: each refused input, output, or option has its code, and an
existing output is left as it was.

**SvgFlattener_Execute_SameInputTwice_WritesIdenticalBytes**: two flattens of one drawing write the same bytes.

**SvgFlattener_FindImageLinks_\*** (`SvgFlattenerLinkTests.cs`): the value is found exactly with tabs, line breaks, and
spaces around `=`, characters outside the basic plane before it, lone CR line breaks, and a byte order mark; the span
is the raw value with its entities while the link is decoded; markup in comments and character data is not found; and a
prefixed `s:image` with `x:href` is found.

**SvgFlattener_RebaseLink_\*** (`SvgFlattenerLinkTests.cs`): sibling and parent folders give relative paths with `/`,
the query and the fragment are kept and escaped for the quote, a space is percent-encoded, and a data URI, a network URL,
a `#` link, a rooted path, and an empty link are left alone.
