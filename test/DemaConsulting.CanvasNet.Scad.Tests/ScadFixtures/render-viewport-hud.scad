// CanvasNet test fixture: a head-up display. A cube at the origin and a line of text placed relative to the camera with
// the viewport variables, written as the requester of the feature wrote it (three single-axis rotations, each with the
// negated component of $vpr). The tests pin what that chain does under the documented meaning of $vpr, next to the
// rotate($vpr) form that faces the camera.
cube([20, 20, 20], center = true);
translate($vpt) rotate([0, 0, -$vpr[2]]) rotate([0, -$vpr[1], 0]) rotate([-$vpr[0], 0, 0])
translate([-40, 30, -$vpd/2]) linear_extrude(0.1) text(str("Cam View: ", $vpr), size = 2);
