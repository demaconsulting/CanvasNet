package CanvasNet_RealPath {
    private import SoftwareItemMetadata::*;

    part def RealPath {
        doc /* The internal RealPath unit (RealPath, NativeMethods, FileOpenHooks): the only place in CanvasNet that calls the operating system about files. It opens a file for reading (read-only with read, write and delete sharing on Windows; O_NONBLOCK and O_CLOEXEC on Linux and macOS so a named pipe cannot hang the open), reports whether an open handle is a regular file and its real path (Windows GetFileType and GetFinalPathNameByHandleW, whose final path keeps its \\?\Volume{GUID}\ form on a volume without a drive letter; Linux fstat and the /proc/self/fd link; macOS proc_pidfdinfo with PROC_PIDFDVNODEPATHINFO), the real location of a folder, the long spelling of short (8.3) names, the exact (letter-case) comparison of real paths, the on-disk letter case of a path (a name in another letter case takes the stored spelling only when both spellings open the same directory entry, so a case-sensitive folder keeps it as written), and the confirmation that this spelling names the open file (volume serial and 128-bit file identifier on Windows, falling back to the 32-bit serial and 64-bit file index for both handles where a volume does not report it; device and inode on Linux and macOS); only paths as written are compared under the platform rule. Unknown facts are null and callers fail closed. FileOpenHooks are the test seams around the open. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Files/RealPath.cs",
                "src/DemaConsulting.CanvasNet/Files/RealPath.Windows.cs",
                "src/DemaConsulting.CanvasNet/Files/RealPath.Unix.cs",
                "src/DemaConsulting.CanvasNet/Files/NativeMethods.cs",
                "src/DemaConsulting.CanvasNet/Files/FileOpenHooks.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Files/RealPathTests.cs");
            designFiles = ("docs/design/canvas-net/files/real-path.md");
            verificationFiles = ("docs/verification/canvas-net/files/real-path.md");
            requirementFiles = ("docs/reqstream/canvas-net/files/real-path.yaml");
        }
    }
}
