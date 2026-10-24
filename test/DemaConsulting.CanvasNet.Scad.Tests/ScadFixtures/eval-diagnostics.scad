// Evaluation fixture: warnings. Every problem here is survivable: evaluation continues with undef and the
// warning (message and location) is compared with eval-diagnostics.diagnostics.txt.

count = 1;
count = 2;
echo("reassigned", count);
echo("unknown variable", nothing_here);
echo("unknown function", no_such_function(1));
no_such_module();
echo("calling a number", count(3));

function one(a) = a;
echo("extra argument", one(1, 2));
echo("unknown argument", one(1, z = 3));

module m(a) {
    echo("module body", a);
}
m(1, 2);
m(1, nope = 3);

for (i = [0 : 0 : 3]) echo("zero step never runs");
echo("iterate undef");
for (i = undef) echo("never");

children();
module bad_child() { children(5); }
bad_child() { echo("only child"); }

echo("bad operands", 1 + "a", -"x", [1, 2] + [1], undef * 2);
echo("bad math", sqrt("x"), len(5));
echo("repeated");
for (i = [1 : 3]) echo(missing_in_loop);
