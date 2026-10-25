// Evaluation fixture: user modules, default parameters and children().

module pair(a = 1, b = a + 1) {
    echo("pair", a, b);
}
pair();
pair(5);
pair(b = 9);
pair(3, 4);

module frame(n) {
    echo("frame start", n);
    children();
    echo("frame end", n);
}
frame(1) {
    echo("first");
    frame(2) echo("nested");
}

module pick(i) {
    children(i);
}
pick(1) {
    echo("zero");
    echo("one");
    echo("two");
}

module range_of_children() {
    children([0 : 1]);
}
range_of_children() {
    echo("a0");
    echo("a1");
    echo("a2");
}

module count_children() {
    echo("children counted", $children);
}
count_children() {
    cube(1);
    sphere(1);
    cube(2);
}

module countdown(n) {
    if (n > 0) {
        echo("countdown", n);
        countdown(n - 1);
    }
}
countdown(3);

module cube(size) {
    echo("user cube", size);
}
cube(4);
