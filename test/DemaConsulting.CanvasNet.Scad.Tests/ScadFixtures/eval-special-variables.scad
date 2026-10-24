// Evaluation fixture: special variables. They are dynamically scoped: a binding made in a module is visible to
// everything that module calls, but not to its caller.

echo("defaults", $fn, $fa, $fs, $t, $preview);

module ring(r) {
    echo("ring", r, $fn);
}

module outer() {
    $fn = 24;
    ring(3);
}

function segments() = $fn;

module via_function() {
    $fn = 6;
    echo("function sees", segments());
}

outer();
ring(1);
ring(2, $fn = 9);
via_function();
echo("after", $fn);

module spread() {
    echo("children", $children);
    children();
}

module inherit() {
    $fn = 40;
    spread() {
        echo("child sees", $fn);
    }
}
inherit();
