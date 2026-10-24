// Original CanvasNet test fixture: comments, strings, escapes, numbers and special variables.
/* block comment
   spanning lines // still inside the comment */
a = 1; /* inline */ b = 2; // trailing

text_simple = "plain text";
text_escapes = "tab\there \"quoted\" back\\slash newline\nend";
text_hex = "\x41\x42 \u00e9 \U01F600";
text_slashes = "not // a comment and /* neither */";

n1 = .5; n2 = 5.; n3 = 1e3; n4 = 2.5E-2; n5 = 3e+2; n6 = 0.125; n7 = 007;

$fn = 12;
$fa = 6;
$fs = 0.5;
$custom_1 = 1;

_under = 1;
Mixed_Case9 = 2;
