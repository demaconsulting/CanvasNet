package CanvasNet {
    part def ContactSheet {
        doc /* Composes a contact sheet: a grid of images, each fitted into a cell of the same size. CalculateLayout derives the geometry from the number of images and the options (columns, or the ceiling of the square root of the count; rows; a sheet width and height from the cell size, the spacing between cells, the padding around the sheet, and a 22-pixel label strip per row when labels are on), refuses a sheet larger than 8192 pixels on a side with the size-limit error before anything is allocated, and gives the origin of each cell in row-major order. A ContactSheet allocates the sheet cleared to an opaque background and composes one image at a time: Add fits the image into its cell (contained, scaled to the largest size that fits and centered on the background, or stretched to the cell), flattens transparency, copies it onto the sheet, and draws its label centered in the strip below it with the Rendering text renderer in 14-pixel text of the bundled Liberation Sans font (never a system font), white on a dark background and black on a light one, cutting a label that is too wide with the ASCII text ...; Detach hands the finished sheet to the caller. The unit works on surfaces only and is deterministic. It includes the SheetOptions and SheetLayout supporting types. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/Imaging/ContactSheet.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/Imaging/ContactSheetTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/imaging/contact-sheet.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/imaging/contact-sheet.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/imaging/contact-sheet.yaml */
    }
}
