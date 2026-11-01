package CanvasNet {
    part def FontDirectoryScanner {
        doc /* Scans directory trees for font files (.ttf, .otf, .ttc, .otc) and reports the identity, name and style metadata of every well-formed face, reading only font headers through the internal SfntHeaderReader.                 Deterministic (depth first; each directory's font files, alphabetical ignoring case, before its subdirectories), bounded by a FontScanLimits record (directories, entries, font files, faces), tolerant of missing, unreadable and unparseable inputs, cancellable, and never follows a subdirectory that is a link (a root or font file that is a link is skipped unless the caller trusts linked files). The single directory-scan implementation behind SystemFontCatalog.Fonts. Thread-safe: all state is local to a call. */        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Fonts/FontDirectoryScanner.cs, src/DemaConsulting.CanvasNet/Fonts/FontScanLimits.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Fonts/FontDirectoryScannerTests.cs */
        comment designRef /* Design: docs/design/canvas-net/fonts/font-directory-scanner.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/fonts/font-directory-scanner.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/fonts/font-directory-scanner.yaml */
    }
}
