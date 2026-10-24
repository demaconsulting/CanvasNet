// Evaluation fixture: control flow in statements.

for (i = [0 : 2]) echo("range", i);
for (i = [5 : -2 : 0]) echo("descending", i);
for (i = [0 : 0.5 : 1.5]) echo("fractional", i);
for (a = [1, 2], b = ["x", "y"]) echo("pairs", a, b);
for (v = [[1, 2], [3, 4]]) echo("rows", v);
for (i = [3 : 1]) echo("never printed");
for (n = 7) echo("scalar iterates once", n);

for (i = [1 : 3])
    if (i == 2)
        echo("two");
    else
        echo("not two", i);

if (1 > 2) echo("wrong");
else if (2 > 1) echo("else-if branch");
else echo("wrong");

let (n = 3, m = n * 2) for (i = [1 : n]) echo("let and for", i, m);

module twice(n) {
    for (i = [1 : n]) {
        echo("body", i);
        if (i == n) echo("last");
    }
}
twice(2);

intersection_for (i = [1 : 3]) {
    echo("intersection_for body", i);
    cube(i);
}

echo("assert statement follows");
assert(true);
