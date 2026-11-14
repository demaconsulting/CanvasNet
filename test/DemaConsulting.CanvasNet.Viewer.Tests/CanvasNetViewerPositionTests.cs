using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     Tests of the positions of <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/>: they follow the fit
///     of a diagram into a box, and the strict option refuses a selector that matches nothing.
/// </summary>
public class CanvasNetViewerPositionTests
{
    /// <summary>A flowchart with two nodes.</summary>
    private const string Flowchart = "flowchart LR\n    A[Start] --> B[End]\n";

    /// <summary>
    ///     Test that the positions of a diagram rendered into a box are in the pixels of the returned picture.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PositionsIntoABox_FollowTheFit()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = directory.Combine("flow.mmd");
        File.WriteAllText(path, Flowchart);
        var selection = PositionSelection.Parse(["node"]);

        // Act: the natural size, and a box twice as large
        using var natural = CanvasNetViewer.Render(path, new ViewerRenderOptions { Positions = selection });
        using var boxed = CanvasNetViewer.Render(path, new ViewerRenderOptions { Positions = selection, Width = natural.Surface.Width * 2, Height = natural.Surface.Height * 2 });

        // Assert
        Assert.Equal(["A", "B"], boxed.Positions.Select(p => p.Id));
        Assert.False(boxed.PositionsTruncated);
        for (var index = 0; index < 2; index++)
        {
            Assert.InRange(boxed.Positions[index].Pixel!.Value.X - (natural.Positions[index].Pixel!.Value.X * 2), -1, 1);
            Assert.InRange(boxed.Positions[index].Pixel!.Value.Y - (natural.Positions[index].Pixel!.Value.Y * 2), -1, 1);
        }
    }

    /// <summary>
    ///     Test that a selector that matches nothing is a note, and with the strict option a failure with the code
    ///     <c>positions-unmatched</c>.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_UnmatchedSelector_NoteOrStrictFailure()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = directory.Combine("flow.mmd");
        File.WriteAllText(path, Flowchart);
        var selection = PositionSelection.Parse(["node:Zed"]);

        // Act
        using var image = CanvasNetViewer.Render(path, new ViewerRenderOptions { Positions = selection });
        var error = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { Positions = selection, ThrowOnUnsupportedFeature = true }));

        // Assert
        Assert.Empty(image.Positions);
        Assert.Contains(image.Diagnostics, d => d.Code == ErrorCodes.PositionsUnmatched);
        Assert.Equal(ErrorCodes.PositionsUnmatched, error.Code);
    }
}
