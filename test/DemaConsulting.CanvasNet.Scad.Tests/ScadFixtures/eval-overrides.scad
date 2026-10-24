// Evaluation fixture: variable overrides. Evaluated as written, and again with width, tag, $fn and extra
// supplied by the caller (see README.md); a supplied value replaces the assignment, whatever its position.

width = 10;
depth = width / 2;
tag = echo("computing the default tag") "default";
$fn = 12;

echo("size", width, depth);
echo("tag", tag);
echo("fragments", $fn);
echo("extra", is_undef(extra) ? "not supplied" : extra);
