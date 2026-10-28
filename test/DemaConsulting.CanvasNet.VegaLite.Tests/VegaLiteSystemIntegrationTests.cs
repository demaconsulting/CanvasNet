// cspell:ignore consolas menlo neue vegalite verdana
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the CanvasNetVegaLite system: opening, schema handling, options, rendering
///     of the empty view, limits, cancellation, determinism and fonts, exercised through the public API only.
/// </summary>
public class VegaLiteSystemIntegrationTests
{
    /// <summary>Gets the cancellation token of the running test, so a canceled test run stops rendering promptly.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>An original specification that uses most of the metadata the document reports.</summary>
    private const string SampleSpec = """
        {
          "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
          "title": "Widgets per day",
          "params": [ { "name": "threshold", "value": 3 } ],
          "data": { "values": [ { "day": "a", "count": 2 }, { "day": "b", "count": 5 } ] },
          "layer": [
            {
              "mark": "bar",
              "encoding": {
                "x": { "field": "day", "type": "nominal" },
                "y": { "field": "count", "type": "quantitative" }
              }
            },
            { "mark": { "type": "rule" }, "encoding": { "y": { "datum": 3 } } }
          ]
        }
        """;

    /// <summary>
    ///     Validates that parsing a string, opening a stream and opening a path give the same document.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Open_ParseStringOpenStreamAndOpenPathAgree()
    {
        // Arrange: the same text as a string, as a UTF-8 stream with a byte-order mark, and as a file
        var folder = Path.Combine(Path.GetTempPath(), "vegalite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "sample.vl.json");
        try
        {
            File.WriteAllText(path, SampleSpec, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            using var stream = new MemoryStream([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(SampleSpec)]);

            // Act
            var parsed = VegaLiteDocument.Parse(SampleSpec);
            var streamed = VegaLiteDocument.Open(stream);
            var opened = VegaLiteDocument.Open(path);

            // Assert: every public description agrees (data locations differ only by file name)
            foreach (var document in new[] { streamed, opened })
            {
                Assert.Equal(parsed.SchemaUrl, document.SchemaUrl);
                Assert.Equal(parsed.SchemaMajorVersion, document.SchemaMajorVersion);
                Assert.Equal(parsed.Title, document.Title);
                Assert.Equal(parsed.MarkTypes, document.MarkTypes);
                Assert.Equal(parsed.ParameterNames, document.ParameterNames);
                Assert.Equal(
                    parsed.DataReferences.Select(VegaLiteTestHelpers.Anonymous),
                    document.DataReferences.Select(VegaLiteTestHelpers.Anonymous));
            }

            Assert.Equal("https://vega.github.io/schema/vega-lite/v5.json", parsed.SchemaUrl);
            Assert.Equal(5, parsed.SchemaMajorVersion);
            Assert.Equal("Widgets per day", parsed.Title);
            Assert.Equal(["bar", "rule"], parsed.MarkTypes);
            Assert.Equal(["threshold"], parsed.ParameterNames);
            var reference = Assert.Single(parsed.DataReferences);
            Assert.Equal(VegaLiteDataKind.Inline, reference.Kind);
            Assert.Equal(2, reference.RowCount);

            // Assert: the path names the source and its folder is the data base directory; the others are unnamed
            Assert.Null(parsed.SourceName);
            Assert.Null(streamed.SourceName);
            Assert.Equal(path, opened.SourceName);
            Assert.Equal(path, opened.DataReferences[0].Location.File);
            Assert.Equal(folder, opened.Options.BaseDirectory);

            // Assert: the stream is left open and fully consumed, and explicit options win over the defaults
            Assert.True(stream.CanRead);
            Assert.Equal(stream.Length, stream.Position);
            var named = VegaLiteDocument.Open(path, new VegaLiteOpenOptions { SourceName = "chosen", BaseDirectory = "elsewhere" });
            Assert.Equal("chosen", named.SourceName);
            Assert.Equal("elsewhere", named.Options.BaseDirectory);

            // Assert: the same text renders the same pixels whichever way it was opened
            using var a = parsed.Render(cancellationToken: Ct);
            using var b = opened.Render(cancellationToken: Ct);
            Assert.Equal(VegaLiteTestHelpers.Snapshot(a), VegaLiteTestHelpers.Snapshot(b));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that schema versions 5 and 6 are both interpreted with the version 6 defaults without warnings,
    ///     other versions are reported, and invalid schema values are specification errors.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Schema_V5AndV6UseV6Defaults()
    {
        // Arrange
        var v5 = VegaLiteDocument.Parse("{\"$schema\":\"https://vega.github.io/schema/vega-lite/v5.json\"}");
        var v6 = VegaLiteDocument.Parse("{\"$schema\":\"https://vega.github.io/schema/vega-lite/v6.json\"}");
        var v4 = VegaLiteDocument.Parse("{\"$schema\":\"https://vega.github.io/schema/vega-lite/v4.json\"}");
        var none = VegaLiteDocument.Parse("{}");
        var warnings = new List<VegaLiteDiagnostic>();
        var options = new VegaLiteRenderOptions { WarningHandler = warnings.Add };

        // Act
        using var fromV5 = v5.Render(options, Ct);
        using var fromV6 = v6.Render(options, Ct);
        var warningsForV5AndV6 = warnings.Count;
        using var fromV4 = v4.Render(options, Ct);
        using var fromNone = none.Render(options, Ct);

        // Assert: both versions are accepted silently and render identically with the version 6 defaults
        Assert.Equal(5, v5.SchemaMajorVersion);
        Assert.Equal(6, v6.SchemaMajorVersion);
        Assert.Equal(0, warningsForV5AndV6);
        Assert.Equal(VegaLiteTestHelpers.Snapshot(fromV5), VegaLiteTestHelpers.Snapshot(fromV6));
        Assert.Equal(new VegaLiteSize(310, 310), v5.Measure(cancellationToken: Ct));

        // Assert: another major version is interpreted the same way but reported with a stable identifier
        var warning = Assert.Single(warnings);
        Assert.StartsWith("vl-schema-version:", warning.Message, StringComparison.Ordinal);
        Assert.Equal("/$schema", warning.Location.Pointer);
        Assert.Equal(VegaLiteTestHelpers.Snapshot(fromV5), VegaLiteTestHelpers.Snapshot(fromV4));
        Assert.Equal(VegaLiteTestHelpers.Snapshot(fromV5), VegaLiteTestHelpers.Snapshot(fromNone));

        // Assert: malformed schema values and plain Vega are specification errors that point at the value
        foreach (var bad in new[] { "{\"$schema\": 6}", "{\"$schema\": \"\"}", "{\"$schema\": \"https://vega.github.io/schema/vega/v5.json\"}" })
        {
            var ex = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(bad));
            Assert.Equal("/$schema", ex.Location.Pointer);
        }
    }

    /// <summary>
    ///     Validates that options validate at initialization and are immutable afterwards.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Options_AreValidatedAndImmutable()
    {
        // Arrange: every option type of the public API
        var optionTypes = new[] { typeof(VegaLiteOpenOptions), typeof(VegaLiteRenderOptions), typeof(VegaLiteLimits), typeof(VegaLiteFontOptions) };

        // Act / Assert: no public property can be set after initialization
        foreach (var type in optionTypes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var setter = property.SetMethod;
                if (setter is not null)
                {
                    Assert.Contains(typeof(IsExternalInit), setter.ReturnParameter.GetRequiredCustomModifiers());
                }
            }
        }

        // Assert: collections are read-only views over private copies
        var source = new List<string> { "data" };
        var open = new VegaLiteOpenOptions { DataPaths = source };
        source.Add("changed");
        Assert.Equal(["data"], open.DataPaths);
        var parameters = new Dictionary<string, object?> { ["p"] = 1 };
        var render = new VegaLiteRenderOptions { Parameters = parameters };
        parameters["p"] = 2;
        Assert.Equal(1, render.Parameters!["p"]);

        // Assert: invalid values fail at initialization, naming the property, rather than later during rendering
        Assert.Throws<ArgumentException>(() => new VegaLiteOpenOptions { BaseDirectory = " " });
        Assert.Throws<ArgumentOutOfRangeException>(() => new VegaLiteRenderOptions { Scale = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new VegaLiteRenderOptions { Scale = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => new VegaLiteRenderOptions { ContainerWidth = 0 });
        Assert.Throws<ArgumentException>(() => new VegaLiteRenderOptions { Config = "[1]" });
        Assert.Throws<ArgumentNullException>(() => new VegaLiteRenderOptions { Limits = null! });
        Assert.Throws<ArgumentOutOfRangeException>(() => new VegaLiteLimits { MaxJsonDepth = 0 });
        Assert.Throws<ArgumentException>(() => new VegaLiteFontOptions { AdditionalDirectories = [""] });

        // Assert: a parameter override that the document does not declare is rejected when it is used
        var document = VegaLiteDocument.Parse(SampleSpec);
        using var ok = document.Render(new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["threshold"] = 4 } }, Ct);
        var ex = Assert.Throws<ArgumentException>(() => document.Render(new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["missing"] = 4 } }, Ct));
        Assert.Contains("'missing'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the documented natural size and the colors of the empty view.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Render_EmptyTitleViewHasDocumentedSizeAndColors()
    {
        // Arrange: a view without axes has the default 300 by 300 data rectangle (Vega-Lite 6) plus 5 pixels of padding per side
        var empty = VegaLiteDocument.Parse("{}");
        var red = new Rgba32(200, 10, 20, 255);
        var border = new Rgba32(221, 221, 221, 255);

        // Act
        using var surface = empty.Render(cancellationToken: Ct);
        using var custom = empty.Render(new VegaLiteRenderOptions { Background = red }, Ct);
        var measured = empty.Measure(cancellationToken: Ct);

        // Assert: 310 by 310 on an opaque white background, and Measure reports what Render produces
        Assert.Equal(310, surface.Width);
        Assert.Equal(310, surface.Height);
        Assert.Equal(new VegaLiteSize(310, 310), measured);
        Assert.Equal(VegaLiteTestHelpers.White, surface[0, 0]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[150, 150]);

        // Assert: the one-pixel #ddd border of the plot area covers exactly the pixel column and row at its edge
        Assert.Equal(border, surface[5, 150]);
        Assert.Equal(border, surface[305, 150]);
        Assert.Equal(border, surface[150, 5]);
        Assert.Equal(border, surface[150, 305]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[4, 150]);
        Assert.Equal(VegaLiteTestHelpers.White, surface[6, 150]);

        // Assert: the caller's background replaces white everywhere outside the border (the plot area has no fill)
        Assert.Equal(310, custom.Width);
        Assert.Equal(red, custom[0, 0]);
        Assert.Equal(red, custom[150, 150]);
        Assert.Equal(border, custom[5, 150]);

        // Assert: a title grows the canvas above the plot area (13 pixel title + 4 pixel offset) and does not stop rendering
        var titled = VegaLiteDocument.Parse("{\"title\":\"Quarterly report\"}");
        using var withTitle = titled.Render(cancellationToken: Ct);
        Assert.Equal("Quarterly report", titled.Title);
        Assert.Equal((310, 327), (withTitle.Width, withTitle.Height));
    }

    /// <summary>
    ///     Validates the scale, the uniform fit to an explicit size and the rejection of invalid sizes.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Render_ScaleAndExplicitSizeFit()
    {
        // Arrange
        var document = VegaLiteDocument.Parse("{}");
        var red = new Rgba32(200, 10, 20, 255);
        var calls = 0;
        var invalid = new VegaLiteRenderOptions { WarningHandler = _ => calls++, UnsupportedFeatureHandler = _ => calls++ };
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        using var doubled = document.Render(new VegaLiteRenderOptions { Scale = 2 }, Ct);
        using var fractional = document.Render(new VegaLiteRenderOptions { Scale = 1.5 }, Ct);
        using var wide = document.Render(300, 100, cancellationToken: Ct);
        using var tall = document.Render(100, 400, new VegaLiteRenderOptions { Background = red, Scale = 3 }, Ct);
        var measuredDoubled = document.Measure(new VegaLiteRenderOptions { Scale = 2 }, Ct);

        // Assert: the scale multiplies the natural size (rounded to whole pixels) and Measure agrees
        Assert.Equal((620, 620), (doubled.Width, doubled.Height));
        Assert.Equal((465, 465), (fractional.Width, fractional.Height));
        Assert.Equal(new VegaLiteSize(620, 620), measuredDoubled);
        Assert.Equal(VegaLiteTestHelpers.White, doubled[0, 0]);
        Assert.Equal(new Rgba32(221, 221, 221, 255), doubled[10, 300]);
        Assert.Equal(new Rgba32(221, 221, 221, 255), doubled[11, 300]);
        Assert.Equal(VegaLiteTestHelpers.White, doubled[9, 300]);
        Assert.Equal(VegaLiteTestHelpers.White, doubled[12, 300]);

        // Assert: an explicit size is honored exactly, the chart is fitted and centered (here 100 by 100 pixels in the
        // middle of the wide surface), the margins use the background, and the scale does not influence an explicit size
        Assert.Equal((300, 100), (wide.Width, wide.Height));
        Assert.True(VegaLiteTestHelpers.RegionIs(wide, 0, 0, 100, 100, VegaLiteTestHelpers.White));
        Assert.True(VegaLiteTestHelpers.RegionIs(wide, 200, 0, 300, 100, VegaLiteTestHelpers.White));
        Assert.False(VegaLiteTestHelpers.RegionIs(wide, 100, 0, 200, 100, VegaLiteTestHelpers.White));
        Assert.Equal((100, 400), (tall.Width, tall.Height));
        Assert.True(VegaLiteTestHelpers.RegionIs(tall, 0, 0, 100, 150, red));
        Assert.True(VegaLiteTestHelpers.RegionIs(tall, 0, 250, 100, 400, red));
        Assert.False(VegaLiteTestHelpers.RegionIs(tall, 0, 150, 100, 250, red));

        // Assert: invalid sizes are rejected before any other work (no events, cancellation not consulted)
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 10, invalid, canceled.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(10, Surface.MaxDimension + 1, invalid, canceled.Token));
        var tooBig = new VegaLiteRenderOptions { Scale = 40, WarningHandler = _ => calls++ };
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(tooBig, canceled.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Measure(tooBig, canceled.Token));
        var tooSmall = new VegaLiteRenderOptions { Scale = 0.001 };
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(tooSmall, Ct));
        Assert.Equal(0, calls);
    }

    /// <summary>
    ///     Validates that oversized source and excessive nesting fail closed with the documented exceptions.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Limits_OversizedSourceAndDepthFailClosed()
    {
        // Arrange
        var small = new VegaLiteOpenOptions { Limits = new VegaLiteLimits { MaxSourceLength = 50 } };
        var padded = "{\"title\":\"" + new string('x', 100) + "\"}";
        var shallow = new VegaLiteOpenOptions { Limits = new VegaLiteLimits { MaxJsonDepth = 3 } };
        using var endless = new EndlessStream();
        var folder = Path.Combine(Path.GetTempPath(), "vegalite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "big.json");
        try
        {
            File.WriteAllText(path, padded);

            // Act / Assert: source over the limit is an InvalidDataException from a string, a file and a stream
            Assert.Throws<InvalidDataException>(() => VegaLiteDocument.Parse(padded, small));
            Assert.Throws<InvalidDataException>(() => VegaLiteDocument.Open(path, small));
            using var memory = new MemoryStream(Encoding.UTF8.GetBytes(padded));
            Assert.Throws<InvalidDataException>(() => VegaLiteDocument.Open(memory, small));
            Assert.Throws<InvalidDataException>(() => VegaLiteDocument.Parse(new string(' ', 4_194_305)));

            // Assert: a stream that never ends is cut off shortly after the limit instead of being read forever
            Assert.Throws<InvalidDataException>(() => VegaLiteDocument.Open(endless, small));
            Assert.True(endless.BytesRead < 100_000, $"read {endless.BytesRead} bytes");

            // Assert: source exactly at the limit is accepted
            var exact = VegaLiteDocument.Parse("{}", new VegaLiteOpenOptions { Limits = new VegaLiteLimits { MaxSourceLength = 2 } });
            Assert.Empty(exact.MarkTypes);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }

        // Assert: nesting up to the limit is accepted, one level more is a specification error with a location
        _ = VegaLiteDocument.Parse("{\"a\":[[1]]}", shallow);
        var tooDeep = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse("{\"a\":[[[1]]]}", shallow));
        Assert.Equal(1, tooDeep.Location.Line);
        Assert.True(tooDeep.Location.Column >= 1);
        Assert.Contains("limit of 3", tooDeep.Message, StringComparison.Ordinal);

        // Assert: hostile nesting under the default limits fails closed without overflowing the stack
        var hundred = "{\"a\":" + new string('[', 100) + new string(']', 100) + "}";
        Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(hundred));
        var huge = "{\"layer\":[" + string.Concat(Enumerable.Repeat("{\"layer\":[", 20_000)) + "{}" + string.Concat(Enumerable.Repeat("]}", 20_000)) + "]}";
        Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(huge));
    }

    /// <summary>
    ///     Validates that a canceled token stops measuring and rendering before anything is reported.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Cancellation_Honored()
    {
        // Arrange: a document that would report events if the work ran
        var document = VegaLiteDocument.Parse(SampleSpec);
        var events = 0;
        var options = new VegaLiteRenderOptions { WarningHandler = _ => events++, UnsupportedFeatureHandler = _ => events++ };
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act / Assert: all three entry points throw OperationCanceledException and deliver nothing
        Assert.Throws<OperationCanceledException>(() => document.Measure(options, canceled.Token));
        Assert.Throws<OperationCanceledException>(() => document.Render(options, canceled.Token));
        Assert.Throws<OperationCanceledException>(() => document.Render(100, 100, options, canceled.Token));
        Assert.Equal(0, events);

        // Assert: the same calls work with a token that is not canceled
        using var live = new CancellationTokenSource();
        using var surface = document.Render(options, live.Token);
        Assert.False(live.IsCancellationRequested);
        Assert.Equal(310, surface.Width);
        Assert.True(events > 0);
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders are byte-identical.
    /// </summary>
    /// <returns>A task that completes when the concurrent renders are done.</returns>
    [Fact]
    public async Task CanvasNetVegaLite_SystemIntegration_Determinism_RepeatedAndConcurrentRendersAreByteIdentical()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(SampleSpec);
        var options = new VegaLiteRenderOptions { Scale = 1.5, Background = new Rgba32(10, 20, 30, 255) };
        using var first = document.Render(options, Ct);
        var baseline = VegaLiteTestHelpers.Snapshot(first);

        // Act: render again on this thread, then from many threads at once through the same immutable document
        using var second = document.Render(options, Ct);
        var token = Ct;
        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(
            () =>
            {
                using var surface = document.Render(options, token);
                return VegaLiteTestHelpers.Snapshot(surface);
            },
            token)).ToArray();
        var concurrent = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(baseline, VegaLiteTestHelpers.Snapshot(second));
        foreach (var snapshot in concurrent)
        {
            Assert.Equal(baseline, snapshot);
        }

        // Assert: parsing the same text twice gives the same description, and events repeat identically
        var again = VegaLiteDocument.Parse(SampleSpec);
        Assert.Equal(document.MarkTypes, again.MarkTypes);
        Assert.Equal(document.DataReferences, again.DataReferences);
        var firstEvents = new List<string>();
        var secondEvents = new List<string>();
        using var a = document.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => firstEvents.Add(u.ToString()) }, Ct);
        using var b = again.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => secondEvents.Add(u.ToString()) }, Ct);
        Assert.Equal(firstEvents, secondEvents);
    }

    /// <summary>
    ///     Validates the font defaults: bundled fonts only, independent of the installed fonts.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Fonts_DefaultUsesBundledLiberation()
    {
        // Arrange: no font options at all, and explicit defaults
        var options = new VegaLiteRenderOptions();
        var explicitDefaults = new VegaLiteFontOptions();

        // Act
        var effective = options.EffectiveFonts;

        // Assert: by default no system font is consulted and no extra directory is scanned, so text can only use
        // the Liberation families bundled with the core package (proved below by rendering)
        Assert.Null(options.Fonts);
        Assert.False(effective.UseSystemFonts);
        Assert.Empty(effective.AdditionalDirectories);
        Assert.False(explicitDefaults.UseSystemFonts);
        Assert.Empty(explicitDefaults.AdditionalDirectories);

        // Assert: opting in to system fonts or extra directories does not change a chart that has no text
        var document = VegaLiteDocument.Parse("{}");
        using var bundled = document.Render(options, Ct);
        using var opted = document.Render(new VegaLiteRenderOptions { Fonts = new VegaLiteFontOptions { UseSystemFonts = true, AdditionalDirectories = ["no-such-directory"] } }, Ct);
        Assert.Equal(VegaLiteTestHelpers.Snapshot(bundled), VegaLiteTestHelpers.Snapshot(opted));

        // Arrange: titles that name fonts. Generic and Arial-like families map to the bundled fonts, so every spelling of
        // sans-serif renders identical bytes, whether or not system fonts are allowed; the same holds for serif and monospace
        var folder = Path.Combine(Path.GetTempPath(), "vegalite-fonts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var system = new VegaLiteRenderOptions { Fonts = new VegaLiteFontOptions { UseSystemFonts = true } };
            var familyOf = new Func<string, VegaLiteDocument>(font => VegaLiteDocument.Parse("{\"title\":{\"text\":\"Fonts and Sizes\",\"font\":\"" + font + "\"}}"));
            byte[] RenderWith(string font, VegaLiteRenderOptions? o = null)
            {
                using var surface = familyOf(font).Render(o, Ct);
                return VegaLiteTestHelpers.Snapshot(surface);
            }

            var sans = RenderWith("sans-serif");
            var serif = RenderWith("serif");
            var mono = RenderWith("monospace");

            // Assert: the three bundled families look different from each other
            Assert.NotEqual(sans, serif);
            Assert.NotEqual(sans, mono);
            Assert.NotEqual(serif, mono);

            // Assert: generic and Arial-like names are the bundled font, with and without system fonts
            foreach (var name in new[] { "Arial", "Helvetica", "Helvetica Neue", "Verdana", "system-ui", "Arial, Helvetica, sans-serif" })
            {
                Assert.Equal(sans, RenderWith(name));
                Assert.Equal(sans, RenderWith(name, system));
            }

            foreach (var name in new[] { "Times", "Times New Roman", "Georgia" })
            {
                Assert.Equal(serif, RenderWith(name));
                Assert.Equal(serif, RenderWith(name, system));
            }

            foreach (var name in new[] { "Courier", "Courier New", "Menlo", "Consolas" })
            {
                Assert.Equal(mono, RenderWith(name));
                Assert.Equal(mono, RenderWith(name, system));
            }

            // Assert: a named family that is not generic is ignored unless allowed: the bundled sans-serif is drawn, even
            // when that family is available (here: copied into a directory that is not searched)
            using (var resource = typeof(Surface).Assembly.GetManifestResourceStream("DemaConsulting.CanvasNet.Fonts.BundledFonts.NotoSans-Regular.ttf"))
            {
                Assert.NotNull(resource);
                using var file = File.Create(Path.Combine(folder, "NotoSans-Regular.ttf"));
                resource.CopyTo(file);
            }

            Assert.Equal(sans, RenderWith("Noto Sans"));
            Assert.Equal(sans, RenderWith("Noto Sans", new VegaLiteRenderOptions { Fonts = new VegaLiteFontOptions { UseSystemFonts = false } }));

            // Assert: the same family is used once its directory is allowed, and then the text differs from the bundled one
            var allowed = new VegaLiteRenderOptions { Fonts = new VegaLiteFontOptions { AdditionalDirectories = [folder] } };
            Assert.NotEqual(sans, RenderWith("Noto Sans", allowed));
            Assert.Equal(RenderWith("Noto Sans", allowed), RenderWith("Noto Sans", allowed));

            // Assert: a family list falls through unknown names to the next family
            Assert.Equal(serif, RenderWith("No Such Family, serif"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that malformed JSON is reported with its file, line and column.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Spec_MalformedJsonReportsLineAndColumn()
    {
        // Arrange: a missing value on the third line
        const string text = "{\n  \"mark\": \"bar\",\n  \"data\": ,\n}";

        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(text, new VegaLiteOpenOptions { SourceName = "broken.vl.json" }));

        // Assert
        Assert.Equal("broken.vl.json", ex.Location.File);
        Assert.Equal(3, ex.Location.Line);
        Assert.Equal(11, ex.Location.Column);
        Assert.StartsWith("broken.vl.json:3:11: ", ex.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<FormatException>(ex);
    }

    /// <summary>
    ///     Validates that constructs that cannot be drawn yet are reported with stable identifiers and pointers,
    ///     in order, once, on the calling thread.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Unsupported_MarkIsReportedWithPointer()
    {
        // Arrange: schema version 4 gives a warning first; a layer of two views follows, the second with an unsupported mark
        // (bar is drawn, so only the arc mark and the composition itself are reported)
        var document = VegaLiteDocument.Parse(
            "{\"$schema\":\"https://vega.github.io/schema/vega-lite/v4.json\",\"layer\":[{\"mark\":\"bar\"},{\"mark\":{\"type\":\"arc\"}}]}");
        var log = new List<string>();
        var threads = new HashSet<int>();
        var options = new VegaLiteRenderOptions
        {
            WarningHandler = w =>
            {
                threads.Add(Environment.CurrentManagedThreadId);
                log.Add("W " + w.Message[..17]);
            },
            UnsupportedFeatureHandler = u =>
            {
                threads.Add(Environment.CurrentManagedThreadId);
                log.Add($"U {u.Feature} {u.Location.Pointer}");
            },
        };

        // Act
        using var surface = document.Render(options, Ct);

        // Assert
        Assert.Equal(["W vl-schema-version", "U vl-composition-layer /layer", "U vl-mark-arc /layer/1/mark"], log);
        Assert.Equal([Environment.CurrentManagedThreadId], threads);
    }

    /// <summary>
    ///     Validates that strict mode stops at the first unsupported feature, after delivering it.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Strict_ThrowsAtFirstUnsupportedFeature()
    {
        // Arrange: an unsupported mark first and an unsupported channel second, in a view that is not a composition
        var document = VegaLiteDocument.Parse("{\"mark\":\"arc\",\"encoding\":{\"shape\":{\"field\":\"a\",\"type\":\"nominal\"}}}");
        var reported = new List<string>();
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, UnsupportedFeatureHandler = u => reported.Add(u.Feature) };

        // Act
        var ex = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(options, Ct));

        // Assert: the first feature only, with its location, and the handler saw exactly that report
        Assert.Equal("vl-mark-arc", ex.Feature);
        Assert.Equal("/mark", ex.Location.Pointer);
        Assert.Equal(["vl-mark-arc"], reported);

        // Assert: a handler that fails while the failure is being reported does not hide it
        var hostile = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, UnsupportedFeatureHandler = _ => throw new InvalidOperationException("handler") };
        Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(hostile, Ct));

        // Assert: after success a failing handler propagates
        var failing = new VegaLiteRenderOptions { UnsupportedFeatureHandler = _ => throw new InvalidOperationException("handler") };
        Assert.Throws<InvalidOperationException>(() => document.Render(failing, Ct));
    }

    /// <summary>
    ///     Validates that remote data is reported and never fetched or resolved, and that empty inline data warns.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_RemoteUrlIsReportedNotFetched()
    {
        // Arrange: every scheme-bearing form, plus empty inline data; the resolver must never be asked
        var resolved = new List<string>();
        var open = new VegaLiteOpenOptions { FileResolver = name => { resolved.Add(name); return null; } };
        var document = VegaLiteDocument.Parse(
            "{\"layer\":[{\"data\":{\"url\":\"https://example.org/a.csv\"}},{\"data\":{\"url\":\"file:///etc/hosts\"}},{\"data\":{\"values\":[]}}]}",
            open);
        var features = new List<VegaLiteUnsupportedFeature>();
        var warnings = new List<VegaLiteDiagnostic>();

        // Act
        using var surface = document.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = features.Add, WarningHandler = warnings.Add }, Ct);

        // Assert: every scheme-bearing url is reported (and none reaches the resolver); the composition itself is reported too
        var remote = features.Where(f => f.Feature == "vl-data-remote-url").ToList();
        Assert.Equal(["vl-composition-layer", "vl-data-remote-url", "vl-data-remote-url"], features.Select(f => f.Feature));
        Assert.Equal(["/layer/0/data", "/layer/1/data"], remote.Select(f => f.Location.Pointer));
        var warning = Assert.Single(warnings);
        Assert.StartsWith("vl-data-empty:", warning.Message, StringComparison.Ordinal);
        Assert.Equal("/layer/2/data", warning.Location.Pointer);
        Assert.Empty(resolved);
    }

    /// <summary>
    ///     A readable stream of spaces that never ends and counts the bytes read from it.
    /// </summary>
    private sealed class EndlessStream : Stream
    {
        /// <summary>Gets the number of bytes that were read.</summary>
        public long BytesRead { get; private set; }

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)' ', offset, count);
            BytesRead += count;
            return count;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
