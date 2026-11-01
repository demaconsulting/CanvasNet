package CanvasNet {
    part def Files {
        doc /* Sandboxed file-access subsystem exposing the SandboxedFileResolver unit (a public resolver that turns an untrusted relative path into the bytes of a file inside caller-chosen directories or a caller-supplied virtual file source, together with its SandboxOptions, SandboxedFile, SandboxException, and SandboxFailureKind types): it validates the path text, confines every candidate to its root, refuses symbolic links and junctions, reads with a byte bound and shared access, supports a resolver callback instead of the file system, and caches per instance. Files depends on no other CanvasNet subsystem; it is used by the CanvasNetVegaLite and CanvasNetScad systems for every local file. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Files/SandboxedFileResolverTests.cs, test/DemaConsulting.CanvasNet.Tests/Files/SandboxedFileResolverLinkTests.cs, test/DemaConsulting.CanvasNet.Tests/Files/SandboxExceptionTests.cs */
        comment designRef /* Design: docs/design/canvas-net/files.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/files.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/files.yaml */

        part sandboxedFileResolver : SandboxedFileResolver;
    }
}
