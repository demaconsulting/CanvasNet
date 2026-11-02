namespace DemaConsulting.CanvasNet.Packaging;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Packaging"/> namespace provides the hardened building
///     blocks for reading untrusted Open Packaging Conventions (OPC, ECMA-376 Part 2) packages such as
///     <c>.pptx</c> and <c>.vsdx</c> files, and untrusted XML in general.
/// </summary>
/// <remarks>
///     The subsystem's units are the <see cref="OpcPackage"/> class (a read-only, bounded view of an OPC
///     ZIP package with content-type and relationship resolution), the <see cref="OpcFormatException"/>
///     class (the exception it throws for a malformed package), and the <see cref="SafeXml"/> static
///     class (XML loading with no DTD processing, no external resolution, and a character bound). It
///     depends only on the .NET base class library; no new NuGet dependencies are introduced.
/// </remarks>
internal static class NamespaceDoc
{
}
