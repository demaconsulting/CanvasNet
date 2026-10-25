// Evaluation fixture: scoping. The last assignment in a scope wins whatever its position, definitions are
// hoisted, blocks and modules open new scopes, and names are resolved lazily.

a = 1;
echo("a seen early", a);
a = 2;

echo("forward reference", later);
later = a * 10;

x = 5;
module show() {
    x = 7;
    echo("module x", x);
    {
        x = 8;
        echo("block x", x);
    }
    echo("module x again", x);
}
show();
echo("outer x", x);

echo("let sequence", let(p = 3, q = p + 1) p * q);
echo("hoisted function", thrice(4));
function thrice(v) = v * 3;

for (i = [1 : 2]) {
    s = i * 100;
    echo("loop scope", i, s);
}

p = q + 1;
q = p + 1;
echo("circular", p, q);
echo("unknown", missing);
