package CanvasNetMathMl_MathMlAttributeValues {
    private import SoftwareItemMetadata::*;

    part def MathMlAttributeValues {
        doc /* The internal attribute value parsers of the MathML Syntax subsystem: the static AttributeValues (AttributeValues.cs) with the MathLength value and its MathLengthUnit (CSS pixels after converting the absolute units, em, ex, and percent) that parses CSS lengths and percentages, the legacy named spaces of MathML 3 in eighteenths of an em, booleans, unsigned integers, the three forms of scriptlevel, and CSS colors (through the core CssColor), and resolves a length against a font size, an x-height, and a percentage base, clamped to a million pixels; and the StyleDeclarations record (StyleDeclarations.cs) with MathDepthValue, MathDepthKind, and StyleIssue that reads the supported subset of the inline style attribute (color, background-color, font-size, math-style, math-depth, font-style, and display: none) with the last declaration of a property winning, and returns every other declaration and every invalid value as an issue. Every parser is a pure function that refuses what it does not accept; the caller reports. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.MathMl/Syntax/AttributeValues.cs",
                "src/DemaConsulting.CanvasNet.MathMl/Syntax/StyleDeclarations.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Syntax/AttributeValuesTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/syntax/attribute-values.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/syntax/attribute-values.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/syntax/attribute-values.yaml");
        }

        // Colors are parsed by the shared CSS color parser of the core
        dependency usesCanvas from MathMlAttributeValues to CanvasNet_CanvasSubsystem::Canvas;
    }
}
