using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for the public diagnostic types of the SceneRenderer unit:
///     <see cref="RenderBudgetExceededException"/>, <see cref="SceneRenderUnsupportedFeature"/>
///     and <see cref="SceneRenderUnsupportedFeatureException"/>.
/// </summary>
public class SceneRenderDiagnosticsTests
{
    /// <summary>
    ///     Validates that the budget exception carries the exceeded budget and a tile-independent message.
    /// </summary>
    [Fact]
    public void RenderBudgetExceededException_Budget_CarriesBudgetAndStandardMessage()
    {
        // Arrange / Act
        var exception = new RenderBudgetExceededException(1234L);

        // Assert
        Assert.IsAssignableFrom<InvalidOperationException>(exception);
        Assert.Equal(1234L, exception.MaxFragmentsPerTile);
        Assert.Contains("1234", exception.Message);
        Assert.Contains("MaxFragmentsPerTile", exception.Message);
        Assert.Equal(exception.Message, new RenderBudgetExceededException(1234L).Message);
    }

    /// <summary>
    ///     Validates the standard exception constructors of the budget exception, which report the default budget.
    /// </summary>
    [Fact]
    public void RenderBudgetExceededException_StandardConstructors_UseDefaultBudget()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var parameterless = new RenderBudgetExceededException();
        var messageOnly = new RenderBudgetExceededException("custom");
        var withInner = new RenderBudgetExceededException("custom", inner);
        var withMessage = new RenderBudgetExceededException(7L, "seven");

        // Assert
        Assert.Equal(4_194_304L, parameterless.MaxFragmentsPerTile);
        Assert.Contains("4194304", parameterless.Message);
        Assert.Equal("custom", messageOnly.Message);
        Assert.Equal(4_194_304L, messageOnly.MaxFragmentsPerTile);
        Assert.Same(inner, withInner.InnerException);
        Assert.Equal(4_194_304L, withInner.MaxFragmentsPerTile);
        Assert.Equal(7L, withMessage.MaxFragmentsPerTile);
        Assert.Equal("seven", withMessage.Message);
    }

    /// <summary>
    ///     Validates that the report record exposes its values, defaults the node to null and has value equality.
    /// </summary>
    [Fact]
    public void SceneRenderUnsupportedFeature_Record_ExposesValues()
    {
        // Arrange
        var node = new MeshNode(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One));

        // Act
        var withoutNode = new SceneRenderUnsupportedFeature("render3d-csg-open-mesh", "open");
        var withNode = new SceneRenderUnsupportedFeature("render3d-csg-open-mesh", "open", node);

        // Assert
        Assert.Equal("render3d-csg-open-mesh", withoutNode.Feature);
        Assert.Equal("open", withoutNode.Message);
        Assert.Null(withoutNode.Node);
        Assert.Same(node, withNode.Node);
        Assert.Equal(withoutNode, new SceneRenderUnsupportedFeature("render3d-csg-open-mesh", "open"));
        Assert.NotEqual(withoutNode, withNode);
    }

    /// <summary>
    ///     Validates that the unsupported-feature exception carries its feature token and node.
    /// </summary>
    [Fact]
    public void SceneRenderUnsupportedFeatureException_FeatureConstructor_CarriesFeatureAndNode()
    {
        // Arrange
        var node = new MeshNode(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One));

        // Act
        var exception = new SceneRenderUnsupportedFeatureException("render3d-csg-open-mesh", "open mesh", node);
        var withoutNode = new SceneRenderUnsupportedFeatureException("render3d-csg-open-mesh", "open mesh");

        // Assert
        Assert.IsAssignableFrom<InvalidOperationException>(exception);
        Assert.Equal("render3d-csg-open-mesh", exception.Feature);
        Assert.Equal("open mesh", exception.Message);
        Assert.Same(node, exception.Node);
        Assert.Null(withoutNode.Node);
    }

    /// <summary>
    ///     Validates that the standard constructors leave the feature empty and the node null.
    /// </summary>
    [Fact]
    public void SceneRenderUnsupportedFeatureException_StandardConstructors_HaveEmptyFeature()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var parameterless = new SceneRenderUnsupportedFeatureException();
        var messageOnly = new SceneRenderUnsupportedFeatureException("custom");
        var withInner = new SceneRenderUnsupportedFeatureException("custom", inner);

        // Assert
        Assert.Equal(string.Empty, parameterless.Feature);
        Assert.NotEmpty(parameterless.Message);
        Assert.Equal(string.Empty, messageOnly.Feature);
        Assert.Equal("custom", messageOnly.Message);
        Assert.Equal(string.Empty, withInner.Feature);
        Assert.Same(inner, withInner.InnerException);
        Assert.Null(parameterless.Node);
        Assert.Null(messageOnly.Node);
        Assert.Null(withInner.Node);
    }
}
