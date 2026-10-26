package CanvasNet {
    part def ArgumentReader {
        doc /* Command-line grammar for one command. Driven by the command's OptionSpec table, it reads the options and positional arguments of the command's argument list: an option is written --name value, --name=value, -a value, or with a one-letter alias and its value attached (-aValue, -Dname=value); a value may start with a dash; options may be repeated and every occurrence is kept, in command-line order, under its canonical name; a lone dash, a negative number, and every token after a bare -- are positional. An unknown option, a missing value, or a value given to a flag is a usage-error ToolException, with a suggestion of the closest declared option for an unknown one. It also provides the value grammars that commands share: integers, finite numbers, page numbers, and resolutions within a range; image sizes written WxH; rectangles written x,y,w,h or WxH+X+Y in pixels or percent; colors written #RGB, #RGBA, #RRGGBB, or #RRGGBBAA (CSS order, alpha last), as a name, or as transparent; angles in degrees, flip axes, border widths, and name=value definitions with number, boolean, and string values. Numbers always use the invariant culture. The supporting value types Dimension, GeometrySpec, PadSpec, FlipAxis, and ParsedOption are documented with this unit. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/ArgumentReader.cs, src/DemaConsulting.CanvasNet.Tool/ArgumentReader.Values.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/ArgumentReaderTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/argument-reader.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/argument-reader.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/argument-reader.yaml */
    }
}
