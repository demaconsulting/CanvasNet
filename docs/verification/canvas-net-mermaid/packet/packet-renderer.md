### PacketRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `PacketRenderer` unit in the
`DemaConsulting.CanvasNet.Mermaid.Packet` namespace: the internal sealed `PacketRenderer`, which draws a
`PacketDiagram` as Mermaid's packet renderer draws it.

#### Verification Approach

`PacketRendererTests` verifies the shown text with `DisplayText` directly, and the drawing by laying out whole texts
through the document (`SceneQuery.Layout`, the test assembly's helper over the internal layout) and asserting the view
box, the rows, the rectangles of the blocks, the content, position, and anchor of every text, the paint and font sizes,
and the failures of the work limit and of cancellation. The expected coordinates are those of Mermaid's `draw` and
`drawWord` worked out by hand for each text and confirmed against the pinned Mermaid release (the rectangle of the first
block of a row at (1, 15) with a width of 507 and a height of 32 is the one `mmdc` writes). The positions and boxes of
every text of the 13 fixtures against the goldens drawn by Mermaid are verified at the system level by the reference
layout tests.

No mocking or stubbing is used; the preprocessor, the detector, the configuration resolver, the packet style sheet, the
`PacketParser` unit, the text metrics, and the bundled fonts are real. The internal types are reached through the test
assembly's `InternalsVisibleTo` access.

Unit tests reside in `PacketRendererTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Packet` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled fonts
- **Isolation**: Each test lays out its own text

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any coordinate, text, anchor, color, font size, or failure that differs from the asserted Mermaid value
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Packet-PacketRenderer-Blocks: Rectangles And Labels Where Mermaid Puts Them

**Tests**: `PacketRendererTests.PacketRenderer_Draw_Default_PlacesBlocksAndBitNumbersAsMermaid`,
`PacketRendererTests.PacketRenderer_Draw_Configuration_ChangesTheGeometry`,
`PacketRendererTests.PacketRenderer_Draw_NarrowBlock_HasTextsButNoRectangle`

Asserts that the blocks `0-15`, `16`, and `17-40` draw two rows with the rectangles (1, 15, 507, 32), (513, 15, 27, 32),
(545, 15, 475, 32), and, for the part of `17-40` on the second row, (1, 62, 283, 32), with their labels centered at the
x of 254.5, 526.5, 782.5, and 142.5; that with the bit numbers turned off the first row starts at a y of 5 instead of 15;
that a row height of 40, a bit width of 20, 16 bits per row, and paddings of 2 and 8 give the rectangles
(1, 18, 78, 40), (81, 18, 238, 40), and (1, 76, 98, 40); and that with a `paddingX` of 40 the block of one bit draws
its label without a rectangle, while the block of two bits keeps its rectangle.

##### CanvasNetMermaid-Packet-PacketRenderer-BitNumbers: The First And Last Bits Above Each Part

**Tests**: `PacketRendererTests.PacketRenderer_Draw_Default_PlacesBlocksAndBitNumbersAsMermaid`,
`PacketRendererTests.PacketRenderer_Draw_Configuration_ChangesTheGeometry`

Asserts that the first bit numbers `0`, `17`, and `32` are anchored at the start of their parts (x 1, 545, and 1) and
the last bit numbers `15`, `31`, and `40` at their ends (x 508, 1020, and 284), that the single bit `16` has one number
centered over it (x 526.5), that the baseline of the bit number `32` of the second row is 2 pixels above its rectangle
(y 60), and that with the bit numbers turned off no bit number is drawn and the view box is 42 pixels high.

##### CanvasNetMermaid-Packet-PacketRenderer-Title: A Title Row Below The Blocks

**Tests**: `PacketRendererTests.PacketRenderer_Draw_Title_IsCenteredInAnExtraRow`

Asserts that the title `Header` of one row of blocks makes the view box 94 pixels high (two rows of 47) and is centered
at the x of 513 with its baseline in the middle of the extra row (between 70.5 and 75), and that the frontmatter title
`From front` is drawn when the text has no title.

##### CanvasNetMermaid-Packet-PacketRenderer-ViewBox: Mermaid's View Box

**Tests**: `PacketRendererTests.PacketRenderer_Draw_Default_PlacesBlocksAndBitNumbersAsMermaid`,
`PacketRendererTests.PacketRenderer_Draw_Title_IsCenteredInAnExtraRow`,
`PacketRendererTests.PacketRenderer_Draw_Empty_IsThePaddingOnly`

Asserts that two rows without a title give the view box (0, 0, 1026, 109), one row with a title (0, 0, 1026, 94), and
the keyword alone (0, 0, 1026, 15), the padding of one row, with an empty group.

##### CanvasNetMermaid-Packet-PacketRenderer-Paint: The Paint Of The Packet Style Sheet

**Tests**: `PacketRendererTests.PacketRenderer_Draw_DarkTheme_PaintsWithThePacketColors`,
`PacketRendererTests.PacketRenderer_Draw_Default_PlacesBlocksAndBitNumbersAsMermaid`

Asserts that in the dark theme a block is filled `#333` and stroked `#e0dfdf` at a width of 1, its label is `#e0dfdf`
at 12 pixels, and its bit number `#e0dfdf` at 10 pixels; the paint of every theme and of the theme variables is
verified with the style sheet (see the ThemeResolver unit verification design).

##### CanvasNetMermaid-Packet-PacketRenderer-Texts: Texts As The Browser Shows Them

**Tests**: `PacketRendererTests.PacketRenderer_DisplayText_DecodesOnlyThePlaceholders`,
`PacketRendererTests.PacketRenderer_Draw_Default_PlacesBlocksAndBitNumbersAsMermaid`

Asserts that the text `A`, the placeholder of `amp`, `B &amp;`, a line feed, a no-break space, the placeholder of
`#35`, and two spaces is shown as `A & B &amp;`, a space, a no-break space, and `#` (the placeholders decoded, the
reference the author wrote kept, the white space collapsed, the no-break space kept); and that the labels and bit
numbers of the default scenario have Mermaid's anchors (`middle` for a label and a single bit, `start` and `end` for
the first and last bits).

##### CanvasNetMermaid-Packet-PacketRenderer-Cost: Bounded And Cancelable

**Tests**: `PacketRendererTests.PacketRenderer_Draw_WorkLimitAndCancellation_StopTheRender`

Asserts that a `MaxLayoutWork` of 20, below the work of two blocks and their labels and bit numbers, fails the render
with `mermaid-limit-exceeded` at a known location, and that a canceled token stops the render with an
`OperationCanceledException`.
