// cspell:ignore vsdx

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Smoke tests confirming the <see cref="VsdxDocument"/> project-scaffolding skeleton
///     compiles and is referenceable from the test project. No behavior exists yet - this will
///     be superseded by real unit tests as each implementation milestone lands.
/// </summary>
public class VsdxDocumentTests
{
    /// <summary>
    ///     Confirms the <see cref="VsdxDocument"/> type exists and can be referenced from the
    ///     test project.
    /// </summary>
    [Fact]
    public void VsdxDocument_TypeExists()
    {
        Assert.NotNull(typeof(VsdxDocument));
    }
}
