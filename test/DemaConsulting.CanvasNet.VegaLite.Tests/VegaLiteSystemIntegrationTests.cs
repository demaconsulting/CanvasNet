using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;

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

        // Assert: strict mode on a specification file stops at the first construct in the text (the regression transform, line 12)
        var fixture = VegaLiteDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "unsupported.vl.json"));
        var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true };
        var first = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => fixture.Measure(strict, Ct));
        Assert.Equal(("vl-transform-regression", "/transform/0", 12), (first.Feature, first.Location.Pointer, first.Location.Line));
    }

    /// <summary>
    ///     Validates that strict mode delivers, once and in order, the warnings found before the first unsupported feature and that
    ///     feature itself (even when the diagnostics cap is already reached) to the handlers before it throws, for measuring and
    ///     for both render overloads.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Strict_DeliversTheReportsBeforeThrowing()
    {
        // Arrange: a version warning, then an unsupported mark and a second unsupported channel; the cap of one is already full
        var document = VegaLiteDocument.Parse(
            "{\"$schema\":\"https://vega.github.io/schema/vega-lite/v4.json\",\"mark\":\"arc\",\"encoding\":{\"shape\":{\"field\":\"a\",\"type\":\"nominal\"}}}");
        var log = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            ThrowOnUnsupportedFeature = true,
            Limits = new VegaLiteLimits { MaxDiagnostics = 1 },
            WarningHandler = w => log.Add("W " + w.Message[..17]),
            UnsupportedFeatureHandler = u => log.Add($"U {u.Feature} {u.Location.Pointer}"),
        };

        // Act / Assert: every entry point throws for the first feature after delivering exactly [warning, feature]
        string[] expected = ["W vl-schema-version", "U vl-mark-arc /mark"];
        Assert.Equal("vl-mark-arc", Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(options, Ct)).Feature);
        Assert.Equal(expected, log);
        log.Clear();
        Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(64, 64, options, Ct));
        Assert.Equal(expected, log);
        log.Clear();
        Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Measure(options, Ct));
        Assert.Equal(expected, log);
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

        // Assert: strict mode turns the first remote url into an exception, still without any request
        var strict = Assert.Throws<VegaLiteUnsupportedFeatureException>(
            () => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
        Assert.Equal("vl-composition-layer", strict.Feature);
        var single = VegaLiteDocument.Parse("{\"data\":{\"url\":\"https://example.org/a.csv\"},\"mark\":\"point\"}", open);
        var remoteStrict = Assert.Throws<VegaLiteUnsupportedFeatureException>(
            () => single.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
        Assert.Equal("vl-data-remote-url", remoteStrict.Feature);
        Assert.Equal("/data", remoteStrict.Location.Pointer);
        Assert.Empty(resolved);
    }

    /// <summary>The specification of a bar chart whose rows come from a relative file.</summary>
    private const string FileChartSpec = """
        { "data": { "url": "data-files.csv" }, "mark": "bar",
          "encoding": { "x": { "field": "stall", "type": "nominal" }, "y": { "field": "crates", "type": "quantitative" } } }
        """;

    /// <summary>The same chart with its rows written inline, to compare pixels with the file-backed one.</summary>
    private const string InlineChartSpec = """
        { "data": { "values": [ { "stall": "North", "crates": 28 }, { "stall": "South", "crates": 55 },
                                { "stall": "East", "crates": 43 }, { "stall": "West", "crates": 91 } ] },
          "mark": "bar",
          "encoding": { "x": { "field": "stall", "type": "nominal" }, "y": { "field": "crates", "type": "quantitative" } } }
        """;

    /// <summary>Gets the folder that holds the fixture specifications and their data files.</summary>
    private static string FixturesFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>
    ///     Renders a document and returns the pixels.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The render options, or null.</param>
    /// <returns>The size and the pixel bytes.</returns>
    private static (int Width, int Height, byte[] Pixels) Pixels(VegaLiteDocument document, VegaLiteRenderOptions? options = null)
    {
        using var surface = document.Render(options, Ct);
        return (surface.Width, surface.Height, VegaLiteTestHelpers.Snapshot(surface));
    }

    /// <summary>
    ///     Validates that a specification opened from a file reads its relative data file from its own folder, that the
    ///     base directory, a data path and a resolver callback find the same file for a specification that has no folder, and
    ///     that every way gives the pixels of the inline chart.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_LocalFileLoadsFromOpenedSpecFolder()
    {
        // Arrange: the inline charts are the references; the fixture and a copy in a new folder are opened from their paths
        var inlineFixture = Pixels(VegaLiteDocument.Open(Path.Combine(FixturesFolder, "bar-inline.vl.json")));
        var inlineChart = Pixels(VegaLiteDocument.Parse(InlineChartSpec));
        using var directory = new DataTempDirectory();
        File.Copy(Path.Combine(FixturesFolder, "data-files.vl.json"), directory.Combine("chart.vl.json"));
        File.Copy(Path.Combine(FixturesFolder, "data-files.csv"), directory.Combine("data-files.csv"));
        var csv = File.ReadAllBytes(Path.Combine(FixturesFolder, "data-files.csv"));
        var requests = new List<string>();

        // Act
        var fromFixture = Pixels(VegaLiteDocument.Open(Path.Combine(FixturesFolder, "data-files.vl.json")));
        var fromCopy = Pixels(VegaLiteDocument.Open(directory.Combine("chart.vl.json")));
        var viaBase = Pixels(VegaLiteDocument.Parse(FileChartSpec, new VegaLiteOpenOptions { BaseDirectory = FixturesFolder }));
        var viaDataPath = Pixels(VegaLiteDocument.Parse(FileChartSpec, new VegaLiteOpenOptions { DataPaths = [FixturesFolder] }));
        var viaCallback = Pixels(VegaLiteDocument.Parse(
            FileChartSpec,
            new VegaLiteOpenOptions { FileResolver = name => { requests.Add(name); return name == "data-files.csv" ? new MemoryStream(csv) : null; } }));

        // Assert: the chart of a file is the chart of the same rows written inline, whichever way the file was found
        foreach (var actual in new[] { fromFixture, fromCopy })
        {
            Assert.Equal((inlineFixture.Width, inlineFixture.Height), (actual.Width, actual.Height));
            Assert.True(inlineFixture.Pixels.AsSpan().SequenceEqual(actual.Pixels));
        }

        foreach (var actual in new[] { viaBase, viaDataPath, viaCallback })
        {
            Assert.Equal((inlineChart.Width, inlineChart.Height), (actual.Width, actual.Height));
            Assert.True(inlineChart.Pixels.AsSpan().SequenceEqual(actual.Pixels));
        }

        // Assert: the callback was asked for the logical path only
        Assert.Equal(["data-files.csv"], requests);
    }

    /// <summary>
    ///     Validates that a document without a folder (parsed from a string or opened from a stream) fails to render a
    ///     local data file unless a base directory, a data path or a resolver is given, in lenient and strict mode.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_StreamNeedsResolverForLocalFile()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(FileChartSpec));
        var document = VegaLiteDocument.Open(stream);
        var parsed = VegaLiteDocument.Parse(FileChartSpec);

        // Act / Assert: rendering and measuring fail with the location of the url, never with an empty chart
        foreach (var candidate in new[] { document, parsed })
        {
            var render = Assert.Throws<VegaLiteRenderException>(() => candidate.Render(cancellationToken: Ct));
            var measure = Assert.Throws<VegaLiteRenderException>(() => candidate.Measure(cancellationToken: Ct));
            var strict = Assert.Throws<VegaLiteRenderException>(() => candidate.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
            Assert.Contains("no base directory, data path or file resolver", render.Message, StringComparison.Ordinal);
            Assert.Equal("/data/url", render.Location.Pointer);
            Assert.Equal("/data/url", measure.Location.Pointer);
            Assert.Equal("/data/url", strict.Location.Pointer);
        }

        // Assert: opening itself never needs the file (the references describe the text only)
        var reference = Assert.Single(document.DataReferences);
        Assert.Equal((VegaLiteDataKind.Url, "data-files.csv"), (reference.Kind, reference.Url));
    }

    /// <summary>
    ///     Validates that a local file that is missing, outside the sandbox, a directory link, or not valid UTF-8 fails the
    ///     render with a located <see cref="VegaLiteRenderException"/> in lenient and strict mode, while an absolute path is
    ///     remote data and is never opened.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_MissingOrBlockedFileThrowsRenderException()
    {
        // Arrange: a folder with a spec-visible root, a file beside it, a file of invalid UTF-8, and a spec for each case
        using var directory = new DataTempDirectory();
        directory.Write("root/ok.csv", "a\n1\n");
        directory.Write("root/bad.csv", new byte[] { 0x61, 0x0A, 0xFF, 0x0A });
        directory.Write("outside.csv", "a\n1\n");
        var open = new VegaLiteOpenOptions { BaseDirectory = directory.Combine("root") };
        string Spec(string url) => "{\"data\":{\"url\":" + System.Text.Json.JsonSerializer.Serialize(url) + "},\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"}}}";

        // Act / Assert: each failure is located at the url, whatever the mode
        foreach (var (url, expected) in new[]
        {
            ("missing.csv", "was not found"),
            ("../outside.csv", "outside the allowed directories"),
            ("bad.csv", "is not valid UTF-8"),
            ("dir/../../outside.csv", "outside the allowed directories"),
            ("dir/a:b.csv", "is not allowed"),
        })
        {
            var document = VegaLiteDocument.Parse(Spec(url), open);
            foreach (var strict in new[] { false, true })
            {
                var ex = Assert.Throws<VegaLiteRenderException>(
                    () => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = strict }, Ct));
                Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
                Assert.Equal("/data/url", ex.Location.Pointer);
            }
        }

        // Assert: the file that is fine renders, and an absolute path is remote data, never opened (empty chart in lenient mode)
        using var fine = VegaLiteDocument.Parse(Spec("ok.csv"), open).Render(cancellationToken: Ct);
        Assert.True(fine.Width > 0);

        var absolute = VegaLiteDocument.Parse(Spec(directory.Combine("outside.csv")), open);
        var reports = new List<VegaLiteUnsupportedFeature>();
        using var emptyChart = absolute.Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = reports.Add }, Ct);
        Assert.True(emptyChart.Width > 0);
        Assert.Equal(["vl-data-remote-url"], reports.Select(r => r.Feature));
        Assert.Throws<VegaLiteUnsupportedFeatureException>(
            () => absolute.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
    }

    /// <summary>
    ///     Validates that sequences, named datasets given by the caller and named datasets of the specification render
    ///     without any report (strict mode accepts them) and give the pixels of the same rows written inline.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_SequenceAndNamedDatasetsRender()
    {
        // Arrange: three ways to say "x from 0 to 3" and "the four stalls"
        const string encoding = "\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"x\",\"type\":\"quantitative\"}}";
        var inline = Pixels(VegaLiteDocument.Parse("{\"data\":{\"values\":[{\"x\":0},{\"x\":1},{\"x\":2}]}," + encoding + "}"));
        var sequence = VegaLiteDocument.Parse("{\"data\":{\"sequence\":{\"start\":0,\"stop\":3,\"as\":\"x\"}}," + encoding + "}");
        var named = VegaLiteDocument.Parse("{\"data\":{\"name\":\"rows\"}," + encoding + "}");
        var specDatasets = VegaLiteDocument.Parse("{\"datasets\":{\"rows\":[{\"x\":0},{\"x\":1},{\"x\":2}]},\"data\":{\"name\":\"rows\"}," + encoding + "}");
        var callerRows = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
        {
            ["rows"] = new[] { 0, 1, 2 }.Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["x"] = i }).ToList(),
        };
        var features = new List<VegaLiteUnsupportedFeature>();
        var strict = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, UnsupportedFeatureHandler = features.Add };

        // Act
        var fromSequence = Pixels(sequence, strict);
        var fromCaller = Pixels(named, new VegaLiteRenderOptions { Datasets = callerRows, ThrowOnUnsupportedFeature = true });
        var fromSpec = Pixels(specDatasets, strict);

        // Assert: no report, and the same pixels as the inline rows
        Assert.Empty(features);
        foreach (var actual in new[] { fromSequence, fromCaller, fromSpec })
        {
            Assert.Equal((inline.Width, inline.Height), (actual.Width, actual.Height));
            Assert.True(inline.Pixels.AsSpan().SequenceEqual(actual.Pixels));
        }

        // Assert: the caller's dataset replaces the one of the specification
        var override3 = Pixels(
            specDatasets,
            new VegaLiteRenderOptions
            {
                Datasets = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
                {
                    ["rows"] = new List<IReadOnlyDictionary<string, object?>> { new Dictionary<string, object?> { ["x"] = 100 } },
                },
            });
        Assert.False(inline.Pixels.AsSpan().SequenceEqual(override3.Pixels));
    }

    /// <summary>
    ///     Validates that values that an explicit parse cannot read are delivered as the warning
    ///     <c>vl-data-parse-failed</c> once per field, after the reports of the specification and with the location of the data.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_ParseFailuresAreDeliveredAsWarnings()
    {
        // Arrange: a schema version warning first, then two cells of one field that are not numbers
        const string spec = """
            { "$schema": "https://vega.github.io/schema/vega-lite/v4.json",
              "data": { "url": "d.csv", "format": { "parse": { "n": "number" } } }, "mark": "point",
              "encoding": { "x": { "field": "n", "type": "quantitative" } } }
            """;
        var files = new VirtualDataFiles().With("d.csv", "n\n1\nbad\nworse\n4\n");
        var document = VegaLiteDocument.Parse(spec, new VegaLiteOpenOptions { FileResolver = files.Resolve });
        var log = new List<string>();
        var messages = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            WarningHandler = w =>
            {
                messages.Add(w.Message);
                log.Add(w.Message[..w.Message.IndexOf(':', StringComparison.Ordinal)] + "|" + w.Location.Pointer);
            },
        };

        // Act
        using var surface = document.Render(options, Ct);

        // Assert: the specification's warning first, the data stage's after it, once, with the counts
        Assert.Equal(["vl-schema-version|/$schema", "vl-data-parse-failed|/data"], log);
        Assert.Contains("2 value(s) of the field 'n'", messages[1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the warnings of the data stage are delivered, once and in order after the reports of the
    ///     specification, even when a later stage (here the scene-item limit) fails the call, and before the exception.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_ParseWarningsAreDeliveredWhenALaterStageFails()
    {
        // Arrange: a schema version warning, a parse failure in the data, and a scene limit the marks cannot fit in
        const string spec = """
            { "$schema": "https://vega.github.io/schema/vega-lite/v4.json",
              "data": { "url": "d.csv", "format": { "parse": { "n": "number" } } }, "mark": "point",
              "encoding": { "x": { "field": "n", "type": "quantitative" } } }
            """;
        var files = new VirtualDataFiles().With("d.csv", "n\n1\nbad\nworse\n4\n");
        var document = VegaLiteDocument.Parse(spec, new VegaLiteOpenOptions { FileResolver = files.Resolve });
        var log = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            Limits = new VegaLiteLimits { MaxSceneItems = 20 },
            WarningHandler = w => log.Add(w.Message[..w.Message.IndexOf(':', StringComparison.Ordinal)] + "|" + w.Location.Pointer),
        };

        // Act / Assert: every entry point fails on the limit after delivering both warnings, exactly once each
        var ex = Assert.Throws<VegaLiteRenderException>(() => document.Render(options, Ct));
        Assert.Contains("MaxSceneItems", ex.Message, StringComparison.Ordinal);
        string[] expected = ["vl-schema-version|/$schema", "vl-data-parse-failed|/data"];
        Assert.Equal(expected, log);
        log.Clear();
        Assert.Throws<VegaLiteRenderException>(() => document.Render(64, 64, options, Ct));
        Assert.Equal(expected, log);
        log.Clear();
        Assert.Throws<VegaLiteRenderException>(() => document.Measure(options, Ct));
        Assert.Equal(expected, log);
    }

    /// <summary>
    ///     Validates that every measure and render call reads its data again (nothing is cached between calls) and that
    ///     changing a file between calls changes the result.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_EachCallReadsTheFileAgain()
    {
        // Arrange
        using var directory = new DataTempDirectory();
        directory.Write("d.csv", "a\n1\n2\n");
        var document = VegaLiteDocument.Parse(
            "{\"data\":{\"url\":\"d.csv\"},\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"}}}",
            new VegaLiteOpenOptions { BaseDirectory = directory.Path });

        // Act
        var first = Pixels(document);
        directory.Write("d.csv", "a\n1\n50\n");
        var second = Pixels(document);
        var measured = document.Measure(cancellationToken: Ct);

        // Assert: the second call saw the new file (the first would have drawn a different chart otherwise)
        Assert.False(first.Pixels.AsSpan().SequenceEqual(second.Pixels));
        Assert.Equal(new VegaLiteSize(second.Width, second.Height), measured);
    }

    /// <summary>
    ///     Validates that the data limits are enforced before any scene item is created: a file over the row, file or byte
    ///     limit fails with that limit named and located at the url even when the scene limit would also fail, while a chart
    ///     whose data fits and whose scene does not fails with the scene limit.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_LimitsFailBeforeSceneBudget()
    {
        // Arrange: a three-row file; the scene limit of one would fail every chart, so only the order tells the failures apart
        var files = new VirtualDataFiles().With("rows.csv", "a\n1\n2\n3\n");
        var open = new VegaLiteOpenOptions { FileResolver = files.Resolve };
        var document = VegaLiteDocument.Parse(
            "{\"data\":{\"url\":\"rows.csv\"},\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"}}}", open);

        // Act
        var rows = Assert.Throws<VegaLiteRenderException>(
            () => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxDataRows = 2, MaxSceneItems = 1 } }, Ct));
        var bytes = Assert.Throws<VegaLiteRenderException>(
            () => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxDataBytes = 5, MaxSceneItems = 1 } }, Ct));
        var scene = Assert.Throws<VegaLiteRenderException>(
            () => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = 1 } }, Ct));

        // Assert: the data limits are named and located at the url; the scene limit only fails when the data was fine
        Assert.Contains("MaxDataRows", rows.Message, StringComparison.Ordinal);
        Assert.Equal("/data/url", rows.Location.Pointer);
        Assert.Contains("MaxDataBytes", bytes.Message, StringComparison.Ordinal);
        Assert.Equal("/data/url", bytes.Location.Pointer);
        Assert.Contains("MaxSceneItems", scene.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the file-backed fixture chart does not depend on the line endings of the checkout: the same
    ///     rows written with LF and with CRLF line breaks (and without a final line break) give the same pixels.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_FixtureLineEndingsDoNotChangeTheChart()
    {
        // Arrange: the fixture text normalized to LF, then written back with each line ending
        var text = File.ReadAllText(Path.Combine(FixturesFolder, "data-files.csv")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lf = new VirtualDataFiles().With("data-files.csv", text);
        var crlf = new VirtualDataFiles().With("data-files.csv", text.Replace("\n", "\r\n", StringComparison.Ordinal));
        var noFinalBreak = new VirtualDataFiles().With("data-files.csv", text.TrimEnd('\n'));

        // Act
        var actual = new[] { lf, crlf, noFinalBreak }
            .Select(files => Pixels(VegaLiteDocument.Parse(FileChartSpec, new VegaLiteOpenOptions { FileResolver = files.Resolve })))
            .ToList();
        var inline = Pixels(VegaLiteDocument.Parse(InlineChartSpec));

        // Assert: every spelling draws the chart of the inline rows
        foreach (var chart in actual)
        {
            Assert.Equal((inline.Width, inline.Height), (chart.Width, chart.Height));
            Assert.True(inline.Pixels.AsSpan().SequenceEqual(chart.Pixels));
        }
    }

    /// <summary>
    ///     Validates that delimited text without <c>parse</c> keeps its cells as text (as Vega does), so nominal text with
    ///     leading zeros is not turned into numbers and no report or warning appears, while the quantitative x, y and size
    ///     encodings still coerce the numeric text when they draw: the chart has the pixels of the same rows written
    ///     inline with real numbers.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Data_DefaultDelimitedTextPreservesTextAndQuantitativeRenderCoerces()
    {
        // Arrange: a CSV whose nominal column has leading zeros and whose other columns are numeric text; the inline
        // chart has the same values with real numbers for the quantitative fields
        const string encoding = """
            "mark": "point",
            "encoding": {
              "x": { "field": "x", "type": "quantitative" },
              "y": { "field": "y", "type": "quantitative" },
              "size": { "field": "w", "type": "quantitative" },
              "color": { "field": "zip", "type": "nominal" } }
            """;
        var files = new VirtualDataFiles().With("p.csv", "zip,x,y,w\n007,1,10,5\n008,2,20,15\n010,3,15,30\n");
        var fromCsv = VegaLiteDocument.Parse(
            "{ \"data\": { \"url\": \"p.csv\" }, " + encoding + " }",
            new VegaLiteOpenOptions { FileResolver = files.Resolve });
        var inline = VegaLiteDocument.Parse(
            "{ \"data\": { \"values\": [ { \"zip\": \"007\", \"x\": 1, \"y\": 10, \"w\": 5 }, { \"zip\": \"008\", \"x\": 2, \"y\": 20, \"w\": 15 },"
            + " { \"zip\": \"010\", \"x\": 3, \"y\": 15, \"w\": 30 } ] }, " + encoding + " }");
        var warnings = new List<VegaLiteDiagnostic>();
        var features = new List<VegaLiteUnsupportedFeature>();
        var options = new VegaLiteRenderOptions { WarningHandler = warnings.Add, UnsupportedFeatureHandler = features.Add };

        // Act
        var actual = Pixels(fromCsv, options);
        var expected = Pixels(inline);

        // Assert: no data report or warning appears (the text was kept, not mis-converted; the legend warnings of the size
        // and color encodings are unrelated) and the quantitative channels read the text as numbers, giving the same chart
        // as real numbers
        Assert.DoesNotContain(warnings, w => w.Message.StartsWith("vl-data", StringComparison.Ordinal));
        Assert.DoesNotContain(features, f => f.Feature.StartsWith("vl-data", StringComparison.Ordinal));
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        Assert.True(expected.Pixels.AsSpan().SequenceEqual(actual.Pixels));
    }

    /// <summary>A chart whose rows go through every Phase 5 transform and a parameter-driven filter.</summary>
    private const string TransformChartSpec = """
        {
          "width": 120, "height": 80,
          "params": [ { "name": "minimum", "value": 0 }, { "name": "scale", "expr": "minimum + 2" } ],
          "data": { "values": [ { "g": "a", "v": [1, 2, 3] }, { "g": "b", "v": [4, 5] }, { "g": "c", "v": [] } ] },
          "transform": [
            { "flatten": ["v"] },
            { "calculate": "datum.v * scale", "as": "w" },
            { "filter": "datum.w > minimum" },
            { "lookup": "g", "from": { "data": { "values": [ { "k": "a", "m": 1 }, { "k": "b", "m": 3 } ] }, "key": "k", "fields": ["m"] } },
            { "sample": 4 }
          ],
          "mark": "point",
          "encoding": { "x": { "field": "w", "type": "quantitative" }, "y": { "field": "m", "type": "quantitative" } }
        }
        """;

    /// <summary>
    ///     Validates that a chart through every kind of transform measures the size it renders, renders the same bytes on every
    ///     call and from many threads, and differs from the chart without the sample (the sample is not a no-op).
    /// </summary>
    /// <returns>A task that completes when the concurrent renders are done.</returns>
    [Fact]
    public async Task CanvasNetVegaLite_SystemIntegration_Transforms_MeasureMatchesRenderAndRendersAreByteIdentical()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(TransformChartSpec);
        var unsampled = VegaLiteDocument.Parse(TransformChartSpec.Replace("{ \"sample\": 4 }", "{ \"sample\": 99 }", StringComparison.Ordinal));
        using var first = document.Render(cancellationToken: Ct);
        var baseline = VegaLiteTestHelpers.Snapshot(first);

        // Act
        var measured = document.Measure(cancellationToken: Ct);
        using var other = unsampled.Render(cancellationToken: Ct);
        var token = Ct;
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(
            () =>
            {
                using var surface = document.Render(cancellationToken: token);
                return VegaLiteTestHelpers.Snapshot(surface);
            },
            token)));

        // Assert
        Assert.Equal(new VegaLiteSize(first.Width, first.Height), measured);
        Assert.All(concurrent, snapshot => Assert.Equal(baseline, snapshot));
        Assert.False(baseline.AsSpan().SequenceEqual(VegaLiteTestHelpers.Snapshot(other)));
    }

    /// <summary>
    ///     Validates that an expression with a syntax error, one over the length or depth limit and a reference to an unknown
    ///     parameter fail when the document is opened, with the location of the expression text.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Expressions_ErrorsAndLimitsFailWhenTheDocumentIsOpened()
    {
        // Arrange
        static string Spec(string expression) => "{\"data\":{\"values\":[{\"a\":1}]},\"transform\":[{\"calculate\":\"" + expression + "\",\"as\":\"b\"}],\"mark\":\"point\"}";
        var tight = new VegaLiteOpenOptions { Limits = new VegaLiteLimits { MaxExpressionLength = 12, MaxExpressionDepth = 3 } };

        // Act
        var syntax = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(Spec("datum.a +")));
        var assignment = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(Spec("datum.a = 1")));
        var unknown = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(Spec("datum.a + other")));
        var tooLong = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(Spec("datum.a + datum.a"), tight));
        var tooDeep = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse(Spec("((((1))))"), tight));

        // Assert: every failure is located at the expression text and names the offset inside it
        foreach (var ex in new[] { syntax, assignment, unknown, tooLong, tooDeep })
        {
            Assert.Equal("/transform/0/calculate", ex.Location.Pointer);
            Assert.Equal(1, ex.Location.Line);
            Assert.Contains("expression offset", ex.Message, StringComparison.Ordinal);
        }

        // Assert: a document within the limits opens
        Assert.Equal(["point"], VegaLiteDocument.Parse(Spec("datum.a"), tight).MarkTypes);
    }

    /// <summary>
    ///     Validates that hostile evaluation fails closed with the location of the expression: a step budget that is too small
    ///     is a located render failure delivered after the reports found before it, and a catastrophic regular expression
    ///     finishes.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Expressions_HostileEvaluationFailsClosed()
    {
        // Arrange: fifty rows against a budget of fifty steps, after a selection parameter that is reported first
        var rows = "[" + string.Join(",", Enumerable.Range(0, 50).Select(i => "{\"a\":" + i + "}")) + "]";
        var spec = "{\"params\":[{\"name\":\"s\",\"select\":\"interval\"}],\"data\":{\"values\":" + rows + "},"
            + "\"transform\":[{\"calculate\":\"datum.a * 2 + 1\",\"as\":\"b\"}],\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"b\",\"type\":\"quantitative\"}}}";
        var delivered = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            Limits = new VegaLiteLimits { MaxEvaluationSteps = 50 },
            UnsupportedFeatureHandler = u => delivered.Add(u.Feature),
        };

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => VegaLiteDocument.Parse(spec).Render(options, Ct));

        // Assert: located at the expression, with the offset, after the report that came first
        Assert.Equal("/transform/0/calculate", ex.Location.Pointer);
        Assert.Contains("expression offset", ex.Message, StringComparison.Ordinal);
        Assert.Equal(["vl-param-selection"], delivered);

        // Arrange: a pattern that makes a backtracking engine take exponential time on this text (forty a and a b)
        var text = new string('a', 40) + "b";
        var catastrophic = "{ \"data\": { \"values\": [ { \"s\": \"" + text + "\" } ] },"
            + " \"transform\": [ { \"calculate\": \"test(regexp('(a+)+$'), datum.s)\", \"as\": \"m\" } ],"
            + " \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"m\", \"type\": \"quantitative\" } } }";

        // Act / Assert: the linear-time engine finishes and the chart renders
        using var surface = VegaLiteDocument.Parse(catastrophic).Render(cancellationToken: Ct);
        Assert.True(surface.Width > 0);
    }

    /// <summary>
    ///     Validates that the derived-row limit, the sandbox and the cancellation work through the public API: a fold over the
    ///     row limit and a lookup that escapes the folder of the specification fail with a location, and a canceled token
    ///     delivers nothing.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Transforms_LimitsSandboxAndCancellationFailClosed()
    {
        // Arrange: a fold of 3 rows by 2 fields needs 6 rows beside the 3 loaded ones; a lookup in a file outside the sandbox
        using var directory = new DataTempDirectory();
        directory.Write("site/chart.vl.json", """
            { "data": { "values": [ { "k": 1, "a": 1, "b": 2 }, { "k": 2, "a": 3, "b": 4 }, { "k": 3, "a": 5, "b": 6 } ] },
              "transform": [ { "lookup": "k", "from": { "data": { "url": "../outside.csv" }, "key": "k", "fields": ["n"] } } ],
              "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }
            """);
        directory.Write("outside.csv", "k,n\n1,leak\n");
        var fold = VegaLiteDocument.Parse("{\"data\":{\"values\":[{\"a\":1,\"b\":2},{\"a\":3,\"b\":4},{\"a\":5,\"b\":6}]},\"transform\":[{\"fold\":[\"a\",\"b\"]}],\"mark\":\"point\"}");
        var lookup = VegaLiteDocument.Open(directory.Combine("site/chart.vl.json"));
        var events = 0;
        var counting = new VegaLiteRenderOptions { WarningHandler = _ => events++, UnsupportedFeatureHandler = _ => events++ };
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        var rows = Assert.Throws<VegaLiteRenderException>(() => fold.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxDataRows = 8 } }, Ct));
        var escape = Assert.Throws<VegaLiteRenderException>(() => lookup.Render(cancellationToken: Ct));
        using var fits = fold.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxDataRows = 9 } }, Ct);

        // Assert: the fold names its limit and its transform; the lookup is refused by the sandbox at the url of its source
        Assert.Contains("MaxDataRows", rows.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0", rows.Location.Pointer);
        Assert.Contains("outside the allowed directories", escape.Message, StringComparison.Ordinal);
        Assert.Equal("/transform/0/from/data/url", escape.Location.Pointer);
        Assert.True(fits.Width > 0);

        // Act / Assert: cancellation throws and delivers nothing, for a document with transforms too
        var document = VegaLiteDocument.Parse(TransformChartSpec);
        Assert.Throws<OperationCanceledException>(() => document.Measure(counting, canceled.Token));
        Assert.Throws<OperationCanceledException>(() => document.Render(counting, canceled.Token));
        Assert.Equal(0, events);
    }

    /// <summary>
    ///     Validates that parameter overrides reach the transforms through the options (a value replaces a default and the
    ///     name of a selection parameter is accepted), and that a name the document does not declare is still refused.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Parameters_OverridesReachTheTransforms()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(TransformChartSpec.Replace("\"params\": [", "\"params\": [ { \"name\": \"brush\", \"select\": \"interval\" },", StringComparison.Ordinal));
        using var baseline = document.Render(cancellationToken: Ct);

        // Act
        using var overridden = document.Render(new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["minimum"] = 3 } }, Ct);
        using var selection = document.Render(new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["brush"] = 1 } }, Ct);
        var unknown = Assert.Throws<ArgumentException>(() => document.Render(new VegaLiteRenderOptions { Parameters = new Dictionary<string, object?> { ["nothing"] = 1 } }, Ct));

        // Assert: minimum = 3 changes the scale and the filter, so the chart differs; naming the selection changes nothing
        Assert.Equal(["brush", "minimum", "scale"], document.ParameterNames);
        Assert.False(VegaLiteTestHelpers.Snapshot(baseline).AsSpan().SequenceEqual(VegaLiteTestHelpers.Snapshot(overridden)));
        Assert.True(VegaLiteTestHelpers.Snapshot(baseline).AsSpan().SequenceEqual(VegaLiteTestHelpers.Snapshot(selection)));
        Assert.Contains("'nothing'", unknown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the specification errors of parameters when a document is opened: a cycle, an unknown reference and a
    ///     repeated name are located at the offending value.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Parameters_InvalidDefinitionsFailWhenTheDocumentIsOpened()
    {
        // Arrange / Act
        var cycle = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse("{\"params\":[{\"name\":\"a\",\"expr\":\"b + 1\"},{\"name\":\"b\",\"expr\":\"a + 1\"}]}"));
        var unknown = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse("{\"params\":[{\"name\":\"a\",\"expr\":\"c\"}]}"));
        var repeated = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse("{\"params\":[{\"name\":\"a\"},{\"name\":\"a\"}]}"));

        // Assert
        Assert.Equal("/params/0/expr", cycle.Location.Pointer);
        Assert.Equal("/params/0/expr", unknown.Location.Pointer);
        Assert.Equal("/params/1/name", repeated.Location.Pointer);
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
