// Original CanvasNet test fixture: polyhedra whose faces are listed clockwise when seen from outside.
// A square pyramid (x 0..10), an octahedron (centered at x = 25) and an L-shaped prism (x 40..52), which has
// a concave top and bottom face.

// Pyramid: 10 x 10 base on z = 0, apex at (5, 5, 8)
polyhedron(
  points = [[0, 0, 0], [10, 0, 0], [10, 10, 0], [0, 10, 0], [5, 5, 8]],
  faces = [[1, 0, 4], [2, 1, 4], [3, 2, 4], [0, 3, 4], [0, 1, 2, 3]]
);

// Octahedron: vertices at distance 6 from the center (25, 0, 0) along each axis
translate([25, 0, 0]) polyhedron(
  points = [[6, 0, 0], [-6, 0, 0], [0, 6, 0], [0, -6, 0], [0, 0, 6], [0, 0, -6]],
  faces = [[0, 4, 2], [1, 2, 4], [0, 3, 4], [1, 4, 3], [0, 2, 5], [1, 5, 2], [0, 5, 3], [1, 3, 5]]
);

// L-shaped prism: the L is 12 x 12 with arms 4 wide, the prism is 5 tall
translate([40, 0, 0]) polyhedron(
  points = [
    [0, 0, 0], [12, 0, 0], [12, 4, 0], [4, 4, 0], [4, 12, 0], [0, 12, 0],
    [0, 0, 5], [12, 0, 5], [12, 4, 5], [4, 4, 5], [4, 12, 5], [0, 12, 5]
  ],
  faces = [
    [0, 1, 2, 3, 4, 5],
    [11, 10, 9, 8, 7, 6],
    [0, 6, 7, 1], [1, 7, 8, 2], [2, 8, 9, 3], [3, 9, 10, 4], [4, 10, 11, 5], [5, 11, 6, 0]
  ]
);
