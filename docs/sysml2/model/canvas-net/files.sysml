package CanvasNet {
    part def Files {
        doc /* File-access subsystem for the files that documents reference. FileReferenceResolver (public) opens a referenced file, asks a FileAccessPolicy (public: AllowAll by default, UnderRoots, NoSymlinks, Combine, FromDelegate) about the real path of the open handle, and only then reads it from the same handle, so the decision cannot be raced; it refuses network, device and alternate-data-stream paths, control characters, and non-regular files under every policy and keeps the byte and file limits. RealPath (internal) is the only unit that calls the operating system (Windows GetFinalPathNameByHandleW, Linux /proc/self/fd and fstat, macOS proc_pidfdinfo) to tell the real path and type of an open handle, and opens files without blocking on Unix. SandboxedFileResolver (public) confines untrusted relative paths to caller-chosen folders, refuses symbolic links and junctions, and verifies the open handle the same way. Files depends on no other CanvasNet subsystem; it is used by ImageSources and by the CanvasNetVegaLite and CanvasNetScad systems. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Files/FilesTests.cs */
        comment designRef /* Design: docs/design/canvas-net/files.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/files.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/files.yaml */

        part fileReferenceResolver : FileReferenceResolver;
        part fileAccessPolicy : FileAccessPolicy;
        part realPath : RealPath;
        part sandboxedFileResolver : SandboxedFileResolver;
    }
}
