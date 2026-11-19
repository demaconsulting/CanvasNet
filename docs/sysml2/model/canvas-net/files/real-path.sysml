package CanvasNet {
    part def RealPath {
        doc /* The internal RealPath unit (RealPath, NativeMethods, FileOpenHooks): the only place in CanvasNet that calls the operating system about files. It opens a file for reading (read-only with read, write and delete sharing on Windows; O_NONBLOCK and O_CLOEXEC on Linux and macOS so a named pipe cannot hang the open), reports whether an open handle is a regular file and its real path (Windows GetFileType and GetFinalPathNameByHandleW; Linux fstat and the /proc/self/fd link; macOS proc_pidfdinfo with PROC_PIDFDVNODEPATHINFO), the real location of a folder, the long spelling of short (8.3) names, the exact (letter-case) comparison of real paths, the on-disk letter case of a path, and the confirmation that this spelling names the open file (volume serial and file index on Windows; device and inode on Linux and macOS); only paths as written are compared under the platform rule. Unknown facts are null and callers fail closed. FileOpenHooks are the test seams around the open. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Files/RealPath.cs, src/DemaConsulting.CanvasNet/Files/RealPath.Windows.cs, src/DemaConsulting.CanvasNet/Files/RealPath.Unix.cs, src/DemaConsulting.CanvasNet/Files/NativeMethods.cs, src/DemaConsulting.CanvasNet/Files/FileOpenHooks.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Files/RealPathTests.cs */
        comment designRef /* Design: docs/design/canvas-net/files/real-path.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/files/real-path.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/files/real-path.yaml */
    }
}
