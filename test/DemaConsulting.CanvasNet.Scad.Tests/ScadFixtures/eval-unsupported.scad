// Evaluation fixture: unsupported features. Recognized but unimplemented functions are reported once per
// location and evaluate to undef; compared with eval-unsupported.unsupported.txt.

echo("before");
echo(parent_module(1));
for (i = [1 : 3]) echo(textmetrics("x"));
x = fontmetrics();
echo("after", x);
