package CanvasNet {
    part def PictureCache {
        doc /* The picture cache of the Pandoc filter: a stateless, thread-safe unit that computes the content-addressed key of a picture as the lowercase hexadecimal SHA-1 of the fields canvasnet.lua 17 (the key version), the kind, the tool identity, the file-access key, the sorted options, and the folder of a flattened drawing, followed by the parts of its dependencies, all separated by line feed, NUL, line feed, exactly as the Lua filter of version 16 composed it. It names the files of the cache folder as the Lua filter did (stem-key.png pictures, stem-key.svg flattened copies, key.unsupported and key.plain markers, and SHA-1 named .mmd sources of Mermaid code blocks and .puml sources of PlantUML code blocks), draws into key.digits.partial files, and commits them by a rename without overwriting: when another build wrote the same file first, the temporary file is removed and the identical copy kept, and when no file is there the commit fails with pandoc-filter-error. Text files are written atomically as UTF-8 and an existing one is kept. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Tool/PandocFilter/PictureCache.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/PictureCacheTests.cs */
        comment designRef /* Design: docs/design/canvas-net-tool/pandoc-filter/picture-cache.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-tool/pandoc-filter/picture-cache.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-tool/pandoc-filter/picture-cache.yaml */
    }
}
