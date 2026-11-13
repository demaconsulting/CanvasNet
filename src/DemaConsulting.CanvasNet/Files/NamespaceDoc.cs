namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Files"/> namespace provides access to the files that documents
///     reference. <see cref="FileReferenceResolver"/> reads a referenced file - relative, with <c>..</c>, or absolute,
///     as Pandoc does - after a <see cref="FileAccessPolicy"/> chosen by the host approved the file that was actually
///     opened; <see cref="FileReferenceOptions"/> configures it, <see cref="FileReference"/> is the result, and
///     <see cref="FileReferenceException"/> with its <see cref="FileReferenceFailureKind"/> reports a refusal.
///     <see cref="SandboxedFileResolver"/> is the folder sandbox of earlier versions, now an obsolete facade kept for one
///     release (with <see cref="SandboxOptions"/>, <see cref="SandboxedFile"/>, <see cref="SandboxException"/> and
///     <see cref="SandboxFailureKind"/>): it confines an untrusted relative path to caller-chosen directories without
///     following links, which <see cref="FileReferenceResolver"/> does with
///     <c>FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(folder), FileAccessPolicy.NoSymlinks)</c>. The namespace
///     has no dependency on any other CanvasNet namespace.
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
///         <see cref="FileReferenceResolver"/> is what <see cref="DemaConsulting.CanvasNet.ImageSources"/> uses for the
///         files a document links to, and what the OpenSCAD and Vega-Lite packages (<c>DemaConsulting.CanvasNet.Scad</c>,
///         <c>DemaConsulting.CanvasNet.VegaLite</c>) use for the files a model or a chart includes, each under the policy
///         its caller chose (every regular local file by default). No part of the library uses the sandbox any longer.
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
///     Read a referenced file like Pandoc, with the default policy (any regular local file):
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Files;
///
///     var resolver = new FileReferenceResolver(new FileReferenceOptions { BaseDirectory = "assets" });
///     var file = resolver.Resolve("../shared/logo.png");
///     Console.WriteLine(file is null ? "not found" : $"{file.Key}: {file.Content.Length} bytes");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
