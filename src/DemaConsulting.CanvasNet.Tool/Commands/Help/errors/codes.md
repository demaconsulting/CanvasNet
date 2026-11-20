---
extends: errors/codes
see-also:
  - pandoc
---

## Command line

On the command line a usage-error also means that the command line is wrong: an
unknown command, option, or help subject, a missing or malformed value,
conflicting options, or a missing output. Read the hint and 'canvasnet <command>
--help'.

'canvasnet formats' lists the formats and 'canvasnet info' gives the page count;
--page chooses the page, --no-clobber keeps an existing output, --max-triangles
is the triangle limit of a model, --file-access and --allow-root choose the
file-access mode, compare takes --resize-to, and --strict turns on strict mode
(drop --strict if it is only a skipped picture). Quote an input with a fragment
in a shell so that & is not read as a command separator. With --strict an
unmatched --positions selector or a failed binding writes nothing.

The codes of the tool alone:

- `operation-failed`: A command could not complete for a reason that has no more
  specific code. Read the message.
- `threshold-failed`: compare measured a value on the wrong side of a
  --threshold limit; the result and the pictures were still written.
- `pandoc-filter-error`: The pandoc-filter command cannot draw the pictures of a
  Pandoc document: an attribute of a picture is not valid, a source is not in
  the resource path, a model, chart, diagram, or drawing has too many files or
  is nested too deep, CANVASNET_FILE_ACCESS, CANVASNET_ROOTS, or CANVASNET_JOBS
  is not valid, or a file of the picture cache cannot be written. The message
  names the picture; see [[pandoc]].
