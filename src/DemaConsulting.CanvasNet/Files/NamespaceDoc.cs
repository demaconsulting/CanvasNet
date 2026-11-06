namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Files"/> namespace provides sandboxed file access: the
///     <see cref="SandboxedFileResolver"/> turns an untrusted relative path into the bytes of a file that is
///     guaranteed to lie inside caller-chosen directories (or inside a caller-supplied virtual file source), without
///     following symbolic links or junctions. <see cref="SandboxOptions"/> configures it,
///     <see cref="SandboxedFile"/> is the result, and <see cref="SandboxException"/> with its
///     <see cref="SandboxFailureKind"/> reports a refusal. The namespace has no dependency on any other CanvasNet
///     namespace.
/// </summary>
/// <remarks>
///     See <see cref="SandboxedFileResolver"/> for the resolver, <see cref="SandboxOptions"/> for the folders and limits,
///     and <see cref="SandboxException"/> for a refusal. It is what <see cref="DemaConsulting.CanvasNet.ImageSources"/>
///     uses to confine the files a document links to, and what the OpenSCAD and Vega-Lite packages
///     (<c>DemaConsulting.CanvasNet.Scad</c>, <c>DemaConsulting.CanvasNet.VegaLite</c>) use for the files a model or a
///     chart includes.
/// </remarks>
/// <example>
///     Read an untrusted relative path from a folder, and handle a refusal:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Files;
///
///     var resolver = new SandboxedFileResolver(new SandboxOptions { BaseDirectory = "assets" });
///     try
///     {
///         var file = resolver.Resolve("images/logo.png");
///         Console.WriteLine(file is null ? "not found" : $"{file.Key}: {file.Content.Length} bytes");
///     }
///     catch (SandboxException ex)
///     {
///         Console.WriteLine($"refused ({ex.Kind}): {ex.Path}");
///     }
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
