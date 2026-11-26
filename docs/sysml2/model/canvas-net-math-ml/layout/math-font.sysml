package CanvasNetMathMl_MathMlMathFont {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlOperatorDictionary::*;

    part def MathMlMathFont {
        doc /* The internal MathFont of the MathML Layout subsystem with the MathConstant enum (the 52 MATH constants that the layout reads: the scale-down percentages, the axis height, the minimum heights of display operators and delimited sub-formulas, and the constants of the fraction, stack, radical, script, limit, stretch stack, overbar, underbar, and accent layouts) and the construction records StretchVariant, StretchPart, and StretchConstruction. The process-wide Default is the bundled Noto Sans Math, loaded once through the shared cache of the core (SystemFontCatalog.LoadBundledFallbackCore) with its OpenType MATH table (TrueTypeFont.TryGetMathTable, internal friend access); a font without a MATH table is refused with an InvalidDataException. It gives the raw and scaled constants, resolves the font and glyph of a character with the deterministic bundled fallback of the core (GlyphFallback: Noto Sans Math, then Noto Sans, then Noto Sans Symbols 2, else the missing-glyph box), and gives the advance, the ink bounds of the outline (cached per font and glyph, thread-safe), the italic correction, the top accent attachment, the x-height, MinConnectorOverlap, and the vertical or horizontal construction of a glyph (its size variants and the parts of its glyph assembly) in CSS pixels at a font size. It is immutable apart from its caches and safe for concurrent use. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/Layout/MathFont.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/MathFontTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/math-font.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/math-font.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/math-font.yaml");
        }

        // The bundled font, its MATH table, its outlines, and the bundled fallback come from the core
        dependency usesFonts from MathMlMathFont to CanvasNet_FontsSubsystem::Fonts;
        dependency usesGeometry from MathMlMathFont to CanvasNet_GeometrySubsystem::Geometry;

        // A construction is read for a stretch axis
        dependency usesOperatorDictionary from MathMlMathFont to MathMlOperatorDictionary;
    }
}
