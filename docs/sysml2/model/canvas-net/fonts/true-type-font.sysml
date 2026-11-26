package CanvasNet_TrueTypeFont {
    private import SoftwareItemMetadata::*;

    part def TrueTypeFont {
        doc /* Public TrueType/CFF/Type 1 font loader/query unit fronting SFNT container parsing (including ttcf collection face selection), cmap lookup, glyf/loca outline decoding, CFF/Type 2 charstring outline decoding, standalone Type 1/PostScript font parsing (PFA/PFB container reading, eexec/charstring decryption, and Type 1 charstring outline decoding with standard glyph name resolution), hhea/hmtx metrics, tolerant format-0 kern lookup, name/style metadata (family/subfamily/full/PostScript name, bold/italic/fixed-pitch), and a lazily parsed, fail-closed internal OpenType MATH table reader (constants, glyph information, size variants and glyph assemblies, through OpenType coverage tables) for the MathML package, with a per-font decoded-outline cache bounded by both entry count and total retained geometry cost. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Fonts/TrueTypeFont.cs",
                "src/DemaConsulting.CanvasNet/Fonts/SfntContainer.cs",
                "src/DemaConsulting.CanvasNet/Fonts/CmapTable.cs",
                "src/DemaConsulting.CanvasNet/Fonts/GlyfLocaReader.cs",
                "src/DemaConsulting.CanvasNet/Fonts/IGlyphOutlineSource.cs",
                "src/DemaConsulting.CanvasNet/Fonts/CffTable.cs",
                "src/DemaConsulting.CanvasNet/Fonts/CffCharstringInterpreter.cs",
                "src/DemaConsulting.CanvasNet/Fonts/CffStandardEncoding.cs",
                "src/DemaConsulting.CanvasNet/Fonts/Type1Table.cs",
                "src/DemaConsulting.CanvasNet/Fonts/Type1CharstringInterpreter.cs",
                "src/DemaConsulting.CanvasNet/Fonts/Type1CharstringDecryption.cs",
                "src/DemaConsulting.CanvasNet/Fonts/Type1PfbReader.cs",
                "src/DemaConsulting.CanvasNet/Fonts/Type1PfaReader.cs",
                "src/DemaConsulting.CanvasNet/Fonts/Type1StandardGlyphNames.cs",
                "src/DemaConsulting.CanvasNet/Fonts/HmtxHheaReader.cs",
                "src/DemaConsulting.CanvasNet/Fonts/KernTable.cs",
                "src/DemaConsulting.CanvasNet/Fonts/NameTable.cs",
                "src/DemaConsulting.CanvasNet/Fonts/StyleTable.cs",
                "src/DemaConsulting.CanvasNet/Fonts/MathTable.cs",
                "src/DemaConsulting.CanvasNet/Fonts/OpenTypeCoverage.cs",
                "src/DemaConsulting.CanvasNet/Fonts/NamespaceDoc.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/Fonts/SfntContainerTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/CmapTableTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/GlyfLocaReaderTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/CffTableTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/CffCharstringInterpreterTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/CffStandardEncodingTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/Type1TableTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/Type1CharstringInterpreterTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/Type1PfbReaderTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/Type1PfaReaderTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/HmtxHheaReaderTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/KernTableTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/NameTableTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/StyleTableTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/MathTableTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/OpenTypeCoverageTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/TrueTypeFontTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Fonts/TrueTypeFontRealFontIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/TestSupport/SyntheticFontBuilder.cs"
            );
            designFiles = ("docs/design/canvas-net/fonts/true-type-font.md");
            verificationFiles = ("docs/verification/canvas-net/fonts/true-type-font.md");
            requirementFiles = ("docs/reqstream/canvas-net/fonts/true-type-font.yaml");
        }
    }
}
