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
///     See <see cref="OpcPackage"/> to open a package and <see cref="SafeXml"/> to load untrusted XML. The format packages
///     <c>DemaConsulting.CanvasNet.Pptx</c> and <c>DemaConsulting.CanvasNet.Vsdx</c> are built on it, and
///     <see cref="DemaConsulting.CanvasNet.Files"/> offers policy-checked access to plain files.
/// </remarks>
/// <example>
///     Open a PowerPoint file as a package and read its main document part:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Packaging;
///
///     using var stream = File.OpenRead("deck.pptx");
///     using var package = OpcPackage.Open(stream);
///     var presentationPart = package.ResolveRelationshipByType(string.Empty, "/officeDocument");
///     var root = package.LoadPartXmlRoot(presentationPart);
///     Console.WriteLine($"{presentationPart}: {root.Name.LocalName}");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
