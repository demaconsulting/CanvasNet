namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Files"/> namespace provides access to the files that documents
///     reference. <see cref="FileReferenceResolver"/> reads a referenced file - relative, with <c>..</c>, or absolute,
///     as Pandoc does - after a <see cref="FileAccessPolicy"/> chosen by the host approved the file that was actually
///     opened; <see cref="FileReferenceOptions"/> configures it, <see cref="FileReference"/> is the result, and
///     <see cref="FileReferenceException"/> with its <see cref="FileReferenceFailureKind"/> reports a refusal.
///     <see cref="SandboxedFileResolver"/> is the folder sandbox: it turns an untrusted relative path into the bytes of a
///     file that is guaranteed to lie inside caller-chosen directories (or inside a caller-supplied virtual file
///     source), without following symbolic links or junctions; <see cref="SandboxOptions"/>, <see cref="SandboxedFile"/>,
///     <see cref="SandboxException"/> and <see cref="SandboxFailureKind"/> go with it. The namespace has no dependency on
///     any other CanvasNet namespace.
/// </summary>
/// <remarks>
///     <para>
///         <b>Policy model.</b> A <see cref="FileAccessPolicy"/> is asked after the file was opened and before any byte
///         is read, about the real path of the open handle (<see cref="FileAccessRequest.RealPath"/>), so a link swapped
///         in or out by another process cannot change what is read after the decision. The built-in policies are
///         <see cref="FileAccessPolicy.AllowAll"/> (the default), <see cref="FileAccessPolicy.UnderRoots(string[])"/>,
///         <see cref="FileAccessPolicy.NoSymlinks"/>, <see cref="FileAccessPolicy.Combine"/> and
///         <see cref="FileAccessPolicy.FromDelegate"/>. Network and device paths (on Windows also legacy device names
///         such as <c>NUL</c>), alternate data streams, control characters, files that are not regular files and, on
///         Windows, a link from a local path to a network share are refused under every policy, and every size and
///         count limit stays.
///     </para>
///     <para>
///         The sandbox is what <see cref="DemaConsulting.CanvasNet.ImageSources"/> uses to confine the files a document
///         links to, and what the OpenSCAD and Vega-Lite packages (<c>DemaConsulting.CanvasNet.Scad</c>,
///         <c>DemaConsulting.CanvasNet.VegaLite</c>) use for the files a model or a chart includes; it verifies its open
///         file the same way.
///     </para>
/// </remarks>
/// <example>
///     Read a referenced file, confined to a folder and refusing links, and handle a refusal:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Files;
///
///     var resolver = new FileReferenceResolver(new FileReferenceOptions
///     {
///         BaseDirectory = "docs",
///         FileAccess = FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots("docs"), FileAccessPolicy.NoSymlinks),
///     });
///     try
///     {
///         var file = resolver.Resolve("../images/logo.png");
///         Console.WriteLine(file is null ? "not found" : $"{file.RealPath}: {file.Content.Length} bytes");
///     }
///     catch (FileReferenceException ex)
///     {
///         Console.WriteLine($"refused ({ex.Kind}, policy {ex.PolicyName}): {ex.Message}");
///     }
///     </code>
///     Read an untrusted relative path from a folder with the sandbox:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Files;
///
///     var sandbox = new SandboxedFileResolver(new SandboxOptions { BaseDirectory = "assets" });
///     try
///     {
///         var file = sandbox.Resolve("images/logo.png");
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
