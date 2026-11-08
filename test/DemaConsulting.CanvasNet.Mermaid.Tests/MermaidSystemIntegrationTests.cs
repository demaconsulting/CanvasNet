namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of the Mermaid package through its public API: determinism, cancellation, concurrency and the
///     third-party notices that ship with it.
/// </summary>
public class MermaidSystemIntegrationTests
{
    /// <summary>
    ///     Validates that the license notices of the ported projects ship with the package and that the provenance names
    ///     each of them.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ThirdPartyNotices_ShipWithPackage()
    {
        // Arrange
        var folder = Path.Combine(AppContext.BaseDirectory, "Notices");

        // Act
        var mermaid = File.ReadAllText(Path.Combine(folder, "LICENSE-mermaid.txt"));
        var yaml = File.ReadAllText(Path.Combine(folder, "LICENSE-js-yaml.txt"));
        var khroma = File.ReadAllText(Path.Combine(folder, "LICENSE-khroma.txt"));
        var dagre = File.ReadAllText(Path.Combine(folder, "LICENSE-dagre.txt"));
        var lodash = File.ReadAllText(Path.Combine(folder, "LICENSE-lodash.txt"));
        var marked = File.ReadAllText(Path.Combine(folder, "LICENSE-marked.txt"));
        var provenance = File.ReadAllText(Path.Combine(folder, "third-party-provenance.txt"));

        // Assert
        Assert.Contains("Copyright (c) 2014 - 2022 Knut Sveidqvist", mermaid, StringComparison.Ordinal);
        Assert.Contains("Copyright (C) 2011-2015 by Vitaly Puzrin", yaml, StringComparison.Ordinal);
        Assert.Contains("Copyright (c) 2019-present Fabio Spampinato, Andrew Maney", khroma, StringComparison.Ordinal);
        Assert.Contains("Copyright (c) 2012-2014 Chris Pettitt", dagre, StringComparison.Ordinal);
        Assert.Contains("dagre-es contributors", dagre, StringComparison.Ordinal);
        Assert.Contains("Copyright OpenJS Foundation and other contributors", lodash, StringComparison.Ordinal);
        Assert.Contains("Christopher Jeffrey", marked, StringComparison.Ordinal);
        Assert.Contains("not affiliated", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-mermaid.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-js-yaml.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-khroma.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-dagre.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-lodash.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-marked.txt", provenance, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled token stops measuring and rendering.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Render_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(MermaidTestHelpers.SequenceText);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Measure(cancellationToken: source.Token));
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
        Assert.Throws<OperationCanceledException>(() => document.Render(10, 10, cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that a document can be rendered from several threads at once with identical results and reports.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Render_Concurrent_IsDeterministic()
    {
        // Arrange
        var document = MermaidDocument.Parse(MermaidTestHelpers.SequenceText);

        // Act
        var results = Enumerable.Range(0, 8).AsParallel().Select(_ =>
        {
            var (options, _, unsupported) = MermaidTestHelpers.Collecting();
            using var surface = document.Render(options);
            return (surface.Width, surface.Height, Report: unsupported.Single().ToString());
        }).ToList();

        // Assert
        Assert.All(results, r => Assert.Equal(results[0], r));
    }
}
