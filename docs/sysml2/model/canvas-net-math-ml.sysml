package CanvasNetMathMl {
    private import SoftwareItemMetadata::*;
    public import CanvasNetMathMl_MathMlDocument::*;
    public import CanvasNetMathMl_MathMlPainter::*;
    public import CanvasNetMathMl_MathMlSyntaxSubsystem::*;
    public import CanvasNetMathMl_MathMlLayoutSubsystem::*;

    part def CanvasNetMathMlSystem {
        doc /* CanvasNetMathMl is a static, offline, deterministic renderer of MathML Core equations, delivered as the separate DemaConsulting.CanvasNet.MathMl NuGet package. The public MathMlDocument unit parses one math element from a string, a stream, or a file path (strict UTF-8, the byte-order mark dropped; a located, coded, hinted MathMlSyntaxException for text that is not well-formed MathML, declares a document type, uses an unknown named reference, or declares an encoding other than UTF-8; an InvalidDataException for oversized text), decoding the full W3C HTML+MathML set of named character references without a document type; describes the equation without drawing it (display mode, element count, alternative text, annotations); measures it (width, height, and the baseline row); and renders it onto a CanvasNet Surface at its natural size times a scale or fitted inside an exact size, at a font size, in a color, over a transparent or colored background, with the bundled Noto Sans Math font and its OpenType MATH table only. Every element of MathML Core is drawn: the token elements (mi with the automatic italic, mn, mo with the interim operator spacing, mtext, ms, mspace) with every mathvariant value, mrow and the inferred rows, and the grouping elements mstyle, mpadded, mphantom, merror, semantics, and maction, stretchy and large operators, fractions (mfrac with linethickness, the legacy bevelled, numalign, and denomalign reported), radicals (msqrt, mroot), scripts (msub, msup, msubsup, mmultiscripts with mprescripts and none), under- and overscripts (munder, mover, munderover with accents, limits, movablelimits, and the horizontal stretching of their operators), and tables (mtable, mtr, mtd with rowspan and columnspan, centered on the math axis, and the MathML 3 columnalign, rowalign, columnspacing, rowspacing, framespacing, columnlines, rowlines, and frame drawn as Firefox draws them), with the style attributes (displaystyle, scriptlevel, mathsize, mathcolor, mathbackground), the user agent script levels of the children of fractions, roots, and scripted elements, and a subset of the inline style attribute. A fraction, a root, or a scripted element without the in-flow children MathML Core requires is laid out as a row of its children, stray table children are wrapped in anonymous rows and cells, and an mtr or mtd outside a table is a row; each is reported as a mathml-child-count warning. Every element outside MathML Core is laid out as a row of its children and reported as mathml-element-unsupported; every unsupported attribute, style declaration, and link is reported with a stable mathml-* code and location, and strict mode throws. Untrusted input is bounded by MathMlLimits (the grid slots of a table among them); the library never reads another file and never uses the network. MathMlImageSource draws .mml files that other documents link to, with a #display=block or #display=inline fragment. The internal subsystems are Syntax (the reader, the named character references with the location map, the equation tree, and the attribute values) and Layout (the layout engine, the style resolver, the math font, the box model, the operator dictionary, the stretchy glyphs, and the token, row, fraction, radical, script, under- and overscript, and table layouts); the MathMlPainter unit draws the box tree. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlSystemIntegrationTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlReferenceLayoutTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlHardeningTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlGalleryTests.cs"
            );
            designFiles = ("docs/design/canvas-net-math-ml.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml.yaml");
        }

        comment /* Third-Party Provenance and Attribution: the code of CanvasNetMathMl is original and implements MathML Core from the specification text. Five generated data tables are derived with attribution: the named character references from the W3C XML Entity Definitions for Characters (htmlmathml-f.ent), the automatic italic table from MathML Core Appendix C.1, the operator dictionary from MathML Core Appendix B.1, the glyph assemblies from MathML Core Appendix B.4, and the mathvariant tables from the Unicode Character Database (UnicodeData.txt). The license texts and the provenance table ship in src/DemaConsulting.CanvasNet.MathMl/third-party/, and every generated file carries a Portions derived from header. The package is not affiliated with the W3C or Unicode, Inc. */
        comment /* CanvasNetMathMl references the sibling CanvasNet system: its Canvas subsystem (Surface, Rgba32, CssColor), Geometry (paths), Drawing (path filling), Fonts (the bundled Noto Sans Math, its MATH table through internal friend access, the glyph outlines, and the bundled glyph fallback), Packaging (the hardened XML reader), and ImageSources (the image-source contract of the composition hook). It has no OTS runtime dependency and references no other format package. */

        part mathMlDocument : MathMlDocument;
        part mathMlPainter : MathMlPainter;
        part mathMlSyntax : MathMlSyntax;
        part mathMlLayout : MathMlLayout;

        dependency usesCanvas from CanvasNetMathMlSystem to CanvasNet_CanvasSubsystem::Canvas;
        dependency usesGeometry from CanvasNetMathMlSystem to CanvasNet_GeometrySubsystem::Geometry;
        dependency usesDrawing from CanvasNetMathMlSystem to CanvasNet_DrawingSubsystem::Drawing;
        dependency usesFonts from CanvasNetMathMlSystem to CanvasNet_FontsSubsystem::Fonts;
        dependency usesPackaging from CanvasNetMathMlSystem to CanvasNet_PackagingSubsystem::Packaging;
        dependency usesImageSources from CanvasNetMathMlSystem to CanvasNet_ImageSourcesSubsystem::ImageSources;
    }
}
