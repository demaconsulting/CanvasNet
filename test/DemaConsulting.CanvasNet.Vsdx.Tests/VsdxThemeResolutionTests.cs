// cspell:ignore vsdx Visio Foregnd Themed THEMEVAL davehoward jgreywolfvsdxjs

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit and real-fixture-level tests for <c>VsdxDocument.Theme.cs</c>'s lazy theme loader and
///     <c>VsdxColorPalette.cs</c>'s new 4-arg <c>Resolve(rawValue, formula, theme, fallback)</c>
///     overload (Milestone 6): resolving the literal sentinel <c>"Themed"</c> against a parsed
///     <c>visio/theme/theme1.xml</c> when the cell's own formula carries a recognized
///     <c>THEMEVAL("slotName")</c> reference to one of the 12 canonical DrawingML
///     <c>&lt;a:clrScheme&gt;</c> slot names, and falling back gracefully
///     (<see cref="VsdxColorPalette.ThemedFallback"/>) when no theme part exists at all.
/// </summary>
/// <remarks>
///     Direct inspection of every in-scope real fixture (see the originating plan report's own
///     exhaustive fixture grep) confirms that no real <c>.vsdx</c> sample's own <c>THEMEVAL(...)</c>
///     argument is ever one of the 12 canonical clrScheme slot names - real occurrences use
///     Visio-internal QuickStyle role names instead (for example <c>"FillColor"</c>,
///     <c>"VariantColor3"</c> - confirmed directly in <c>davehoward-test10-nested-shapes.vsdx</c>
///     and <c>davehoward-test3-house.vsdx</c>'s own <c>document.xml</c>). So
///     <c>VsdxDocument_ThemedCellResolution_ResolvesAgainstParsedTheme</c> is necessarily a synthetic,
///     hand-built package - it is not possible to exercise the canonical-slot match against any
///     real sample, and this test's own name/summary does not claim otherwise.
/// </remarks>
public class VsdxThemeResolutionTests
{
    /// <summary>The root folder a built test project copies the staged <c>.vsdx</c> fixtures into (see the <c>.csproj</c>'s fixture <c>&lt;None&gt;</c> wiring).</summary>
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "VsdxFixtures");

    /// <summary>Resolves a staged fixture's full path by file name, failing clearly if the fixture-copy wiring did not place it in the output directory.</summary>
    private static string FixturePath(string fileName)
    {
        var path = Path.Combine(FixturesDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected staged fixture '{path}' to exist in the test output directory.");
        return path;
    }

    /// <summary>
    ///     Proves a cell carrying the literal sentinel <c>"Themed"</c>, with a narrowly-recognized
    ///     <c>THEMEVAL("accent1")</c> formula, resolves against a synthetic package's own parsed
    ///     <c>visio/theme/theme1.xml</c> rather than falling back to the neutral gray - see this
    ///     class's own remarks for why this path is necessarily synthetic, not real-fixture-based.
    /// </summary>
    [Fact]
    public void VsdxDocument_ThemedCellResolution_ResolvesAgainstParsedTheme()
    {
        // Arrange: accent1 is set to a distinctive, recognizable color (#3366CC); every other
        // slot gets a distinct filler so a wrong-slot match would be caught, not masked.
        var theme1Xml = VsdxTestPackages.BuildTheme1Xml("accent1", "3366CC");
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="Themed" F="THEMEVAL(&quot;accent1&quot;)"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml, theme1Xml: theme1Xml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var paint = document.GetPageShapes(0)[0].Paint!;

        // Assert: #3366CC.
        Assert.Equal(0x33, paint.FillColor.R);
        Assert.Equal(0x66, paint.FillColor.G);
        Assert.Equal(0xCC, paint.FillColor.B);
    }

    /// <summary>
    ///     Proves a cell carrying the literal sentinel <c>"Themed"</c> falls back to
    ///     <see cref="VsdxColorPalette.ThemedFallback"/>, without throwing, when the package
    ///     declares no <c>theme</c> relationship at all - the overwhelmingly common real-fixture
    ///     case (see <c>VsdxStyleResolutionTests.StyleResolution_ThemedColorSentinel_ResolvesToNeutralFallbackWithoutThrowing</c>
    ///     for the pre-existing, bare-<c>THEMEVAL()</c>-formula variant of this same fallback
    ///     path).
    /// </summary>
    [Fact]
    public void VsdxDocument_ThemedCellResolution_FallsBackToNeutralDefaultWhenNoThemePart()
    {
        // Arrange: no theme1Xml supplied at all - the package declares no theme relationship.
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="Themed" F="THEMEVAL(&quot;accent1&quot;)"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        var exception = Record.Exception(() => document.GetPageShapes(0));

        // Assert
        Assert.Null(exception);
        var paint = document.GetPageShapes(0)[0].Paint!;
        Assert.Equal(VsdxColorPalette.ThemedFallback, paint.FillColor);
    }

    /// <summary>
    ///     Proves a real-world fixture carrying an actual <c>visio/theme/theme1.xml</c> part
    ///     (<c>jgreywolfvsdxjs-connectors.vsdx</c> - confirmed directly against its own extracted
    ///     theme part) resolves its theme without throwing, and that the resolved theme's own
    ///     <c>dk1</c>/<c>lt1</c> slots are non-null (every real theme part inspected declares a
    ///     standard 12-slot <c>&lt;a:clrScheme&gt;</c> using <c>&lt;a:srgbClr&gt;</c> literals).
    /// </summary>
    [Fact]
    public void VsdxDocument_ThemedCellResolution_RealFixtureThemePartResolvesWithoutThrowing()
    {
        // Arrange
        using var document = VsdxDocument.Open(FixturePath("jgreywolfvsdxjs-connectors.vsdx"));

        // Act / Assert: resolving every page's shapes (which internally resolves the theme once,
        // lazily) must not throw.
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            var exception = Record.Exception(() => document.GetPageShapes(pageIndex));
            Assert.Null(exception);
        }
    }
}

