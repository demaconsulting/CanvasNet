# PDF Test Fixtures

Every PDF file in this folder is a small, hand-authored document created specifically for this
repository to exercise `PdfDocument`'s Phase 1 parsing internals (tokenizer, object model,
cross-reference resolution in all three forms, the linear-scan fallback, page-tree traversal with
inheritance, and `/Encrypt` detection) end to end via real files on disk. There is no third-party
source corpus behind any of them (unlike, for example, `PngSuite` in the core test project): each
was constructed byte-by-byte from scratch for CanvasNet and is licensed under the same MIT license
as the rest of this repository.

| File | Exercises |
| ------ | ----------- |
| `classic-xref-single-page.pdf` | Classic `xref` table + `trailer` dictionary, one page |
| `xref-stream-single-page.pdf` | A `/Type /XRef` cross-reference stream, no classic table |
| `object-stream.pdf` | A `/Type /ObjStm` compressed-object stream holding the page dictionary |
| `hybrid-xref.pdf` | Classic trailer + `/XRefStm` hybrid link to a supplementary xref stream |
| `multi-page-mixed-mediabox-rotate.pdf` | Three pages, differing/inherited `/MediaBox` and `/Rotate` |
| `malformed-startxref.pdf` | No `startxref`/`xref`/`trailer` at all - exercises the linear-scan fallback |
| `encrypted-trailer.pdf` | A trailer containing an `/Encrypt` key - exercises `/Encrypt` detection |
| `cyclic-page-tree.pdf` | A `/Kids` entry referencing an ancestor - exercises page-tree cycle rejection |
