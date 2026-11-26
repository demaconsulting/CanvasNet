package CanvasNetMathMl_MathMlTableLayout {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlMathTree::*;
    private import CanvasNetMathMl_MathMlStyleResolver::*;
    private import CanvasNetMathMl_MathMlAttributeValues::*;

    part def MathMlTableLayout {
        doc /* The internal TableLayout of the MathML Layout subsystem. Layout builds the box of mtable from the boxes of the contents of its cells (TableInput, TableRowInput, and TableCellInput, with null elements for the anonymous rows and cells around stray children) as MathML Core 3.5 does: the cells are placed by the HTML algorithm for processing rows (columnspan 1-1,000, rowspan 0-65,534, 0 and a span past the last row ending at the last row, read with the HTML rules for parsing non-negative integers as browsers read them, characters after the digits ignored but reported, invalid spans reported as mathml-attribute-invalid), the slot count is checked against MathMlLimits.MaxTableCells as the columns grow, before the grid is allocated, and every slot is charged to the work budget through the engine's charge delegate, which also checks cancellation; every cell is padded by 0.4em and 0.5ex at its font size, columns take their widest padded cell, rows the largest padded ascent and descent of their baseline-aligned cells, and spanning cells spread their excess in proportion to the sizes they span, as Chrome does; the table is centered on the math axis. The box of the table holds the boxes of its rows, which hold the boxes of their cells (whole cell rectangles), which hold the contents, as browsers report them. The MathML 3 attributes are applied as Firefox draws them: columnalign and rowalign align the contents, columnspacing, rowspacing, and framespacing switch to the spacing mode (gaps between the cells, the outer padding of MathML Core kept, the frame spacing inside a frame), and columnlines, rowlines, and frame draw one-pixel solid or dashed rules in the middle of their gaps (interrupted by spanning cells) and around the table; invalid values are reported and ignored. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/Layout/TableLayout.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/TableLayoutTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/table-layout.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/table-layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/table-layout.yaml");
        }

        // The table places the content boxes of its cells and paints its lines with the axis and x-height of the math font
        dependency usesMathBox from MathMlTableLayout to MathMlMathBox;
        dependency usesMathFont from MathMlTableLayout to MathMlMathFont;

        // The spans, alignments, spacing, and lines are read from the elements; the styles give the font sizes and colors
        dependency usesMathTree from MathMlTableLayout to MathMlMathTree;
        dependency usesStyleResolver from MathMlTableLayout to MathMlStyleResolver;
        dependency usesAttributeValues from MathMlTableLayout to MathMlAttributeValues;
        dependency usesCanvas from MathMlTableLayout to CanvasNet_CanvasSubsystem::Canvas;
    }
}