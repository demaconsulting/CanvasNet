// gen-cose-goldens.mjs - records what the cose-bilkent layout that the pinned mermaid bundles computes for generated
// trees, and what Chrome's Math.sin, Math.cos, Math.log and Math.pow give, as the upstream vectors of the
// CanvasNet.Mermaid CoseLayout tests (the layout of mindmaps).
//
// Developer tooling of tools/visual-diff; see the README there. Usage:
//
//     node gen-cose-goldens.mjs [--modules <folder>] [--layouts <file>] [--math <file>]
//
// <modules> is a folder that holds node_modules/mermaid and node_modules/puppeteer (default: the repository root).
// <layouts> is the layout golden file (default:
// test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/cose-layouts.json) and <math> the math vector
// file (default: .../upstream-vectors/js-math.json).
//
// CHROME ONLY. The layout is chaotic, so its results depend on the last bit of every sine, cosine, logarithm and power,
// and Node's Math functions differ from Chrome's (which the reference renderer runs). Every value is therefore computed
// in headless Chrome through puppeteer; the script has no Node fallback, and the page code refuses to run outside a
// browser. The four libraries are not taken from node_modules but extracted from mermaid's own bundle (the sources of
// dist/mermaid.js.map): cytoscape 3.34.0, layout-base 1.0.2, cose-base 1.0.3 and cytoscape-cose-bilkent 4.1.0, at the
// paths the script asserts. The page runs Mermaid's createCytoscapeInstance (cose-bilkent/cytoscape-setup.ts) as written:
// the nodes at (0, 0) with their sizes, the edges, layout dimensions from the data, and the layout { name:
// 'cose-bilkent', quality: 'proof', styleEnabled: false, animate: false }. It records every node position and the start,
// middle and end point of every edge (rscratch), the number of iterations, and every Math call the layout made. It
// fails when Math.random is called during a layout or when a layout does not take the radial (flat forest) path, the two
// facts that make the port free of random numbers.
//
// The trees come from a seeded generator (sizes on Chrome's 1/64-pixel text grid, whole numbers and arbitrary values;
// paths, stars, balanced and random trees up to 600 nodes, zero sizes, sizes beyond the world boundary, and sizes that
// make the layout fail), plus three trees whose sizes are read from mindmaps that the pinned mermaid draws in the same
// browser; for those the script also checks that its replay gives the positions and edge points Mermaid drew. The math
// file holds, as the hexadecimal bits of each double, the sine and cosine of 20,000 seeded angles in [0, 2π), of
// 2,000 in [-8, 8] and of every angle the layouts used; the logarithm of 5,000 seeded positive values and of every
// argument the layouts used; and the power of every pair the cooling schedule makes for 1 to 1,016 nodes. Nothing is
// downloaded; the inputs are original.

import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// The repository root is three folders above tools/visual-diff/mermaid
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const vectors = join(root, 'test', 'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'upstream-vectors');

// Read the optional arguments
const args = process.argv.slice(2);
let modules = root;
let layoutsOut = join(vectors, 'cose-layouts.json');
let mathOut = join(vectors, 'js-math.json');
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--modules' && i + 1 < args.length) {
    modules = resolve(args[++i]);
  } else if (args[i] === '--layouts' && i + 1 < args.length) {
    layoutsOut = resolve(args[++i]);
  } else if (args[i] === '--math' && i + 1 < args.length) {
    mathOut = resolve(args[++i]);
  } else {
    console.error('usage: node gen-cose-goldens.mjs [--modules <folder>] [--layouts <file>] [--math <file>]');
    process.exit(2);
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// The libraries, extracted from mermaid's bundle
// ---------------------------------------------------------------------------------------------------------------------
const mermaidFolder = join(modules, 'node_modules', 'mermaid');
const mermaidVersion = JSON.parse(readFileSync(join(mermaidFolder, 'package.json'), 'utf8')).version;
if (mermaidVersion !== '11.17.2') {
  console.error(`mermaid ${mermaidVersion} is installed; the goldens are recorded from mermaid 11.17.2`);
  process.exit(1);
}

const map = JSON.parse(readFileSync(join(mermaidFolder, 'dist', 'mermaid.js.map'), 'utf8'));
const bundled = (path) => {
  const index = map.sources.indexOf(path);
  if (index < 0 || typeof map.sourcesContent[index] !== 'string') {
    console.error(`mermaid's source map has no source ${path}`);
    process.exit(1);
  }
  return map.sourcesContent[index];
};
const pnpm = '../../../node_modules/.pnpm/';
const layoutBase = bundled(`${pnpm}layout-base@1.0.2/node_modules/layout-base/layout-base.js`);
const coseBase = bundled(`${pnpm}cose-base@1.0.3/node_modules/cose-base/cose-base.js`);
const coseBilkent = bundled(
  `${pnpm}cytoscape-cose-bilkent@4.1.0_cytoscape@3.34.0/node_modules/cytoscape-cose-bilkent/cytoscape-cose-bilkent.js`
);
const cytoscapeEsm = bundled(`${pnpm}cytoscape@3.34.0/node_modules/cytoscape/dist/cytoscape.esm.mjs`);
const exportLine = 'export { cytoscape as default };';
if (!cytoscapeEsm.includes(exportLine)) {
  console.error('cytoscape.esm.mjs has changed: its default export was not found');
  process.exit(1);
}
const cytoscapeScript = cytoscapeEsm.replace(exportLine, 'window.cytoscape = cytoscape;');

const resolver = createRequire(join(modules, 'noop.js'));
const puppeteer = (await import(pathToFileURL(resolver.resolve('puppeteer')).href)).default;

// ---------------------------------------------------------------------------------------------------------------------
// Seeded generator (mulberry32) and the trees
// ---------------------------------------------------------------------------------------------------------------------
function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// A node size: on the 1/64 grid of Chrome's text widths (with whole paddings), whole, or arbitrary
function size(next, kind) {
  const k = kind ?? (next() < 0.6 ? 'grid' : next() < 0.5 ? 'whole' : 'odd');
  if (k === 'grid') {
    return [Math.floor(next() * 140 * 64) / 64 + 20, [24, 44, 68, 92][Math.floor(next() * 4)] + Math.floor(next() * 3) * 10];
  }
  if (k === 'whole') {
    return [Math.floor(20 + next() * 180), Math.floor(20 + next() * 80)];
  }
  return [20 + next() * 180, 20 + next() * 80];
}

// The edges of a tree whose nodes 1..n-1 each hang from an earlier node, as Mermaid orders a mindmap's (by child)
const tree = (n, parentOf) => Array.from({ length: n - 1 }, (_, i) => [parentOf(i + 1), i + 1]);

const cases = [];
const add = (name, nodes, edges) => cases.push({ name, nodes, edges });

add('single node', [[60, 30]], []);
add('two nodes', [[80.5, 44], [120.25, 44]], [[0, 1]]);
add('two zero-size nodes', [[0, 0], [0, 0]], [[0, 1]]);
{
  const next = rng(1);
  add('path of 40', Array.from({ length: 40 }, () => size(next)), tree(40, (i) => i - 1));
}
{
  const next = rng(2);
  add('star of 60', Array.from({ length: 60 }, () => size(next)), tree(60, () => 0));
}
{
  const next = rng(3);
  add('binary tree of 63', Array.from({ length: 63 }, () => size(next, 'grid')), tree(63, (i) => Math.floor((i - 1) / 2)));
}
{
  const next = rng(4);
  add('ternary tree of 40', Array.from({ length: 40 }, () => size(next, 'whole')), tree(40, (i) => Math.floor((i - 1) / 3)));
}
{
  const next = rng(5);
  add('chain of depth 32', Array.from({ length: 33 }, () => size(next, 'grid')), tree(33, (i) => i - 1));
}
for (const [n, seed] of [[3, 10], [5, 11], [8, 12], [10, 13], [12, 14], [20, 15], [30, 16], [45, 17], [60, 18], [80, 19], [120, 20], [200, 21], [300, 22], [400, 23]]) {
  const next = rng(seed);
  add(`random tree of ${n}`, Array.from({ length: n }, () => size(next)), tree(n, (i) => Math.floor(next() * i)));
}
for (const [n, seed] of [[15, 30], [40, 31], [100, 32]]) {
  // Mindmap-like: shallow and bushy, nodes in pre-order (each node hangs from one of the last few nodes)
  const next = rng(seed);
  add(`bushy tree of ${n}`, Array.from({ length: n }, () => size(next, 'grid')), tree(n, (i) => Math.max(0, i - 1 - Math.floor(next() * Math.min(i, 4)))));
}
{
  // Reversed and shuffled edges: the layout's forces depend on the edge directions and order
  const next = rng(40);
  const edges = tree(50, (i) => Math.floor(next() * i)).map(([s, t]) => (next() < 0.5 ? [t, s] : [s, t]));
  for (let i = edges.length - 1; i > 0; i--) {
    const j = Math.floor(next() * (i + 1));
    [edges[i], edges[j]] = [edges[j], edges[i]];
  }
  add('random tree of 50 with shuffled edges', Array.from({ length: 50 }, () => size(next)), edges);
}
{
  const next = rng(41);
  add('random tree of 600', Array.from({ length: 600 }, () => size(next, 'grid')), tree(600, (i) => Math.floor(next() * i)));
}
{
  const next = rng(42);
  add('zero widths', Array.from({ length: 12 }, () => [0, Math.floor(next() * 3) * 20]), tree(12, (i) => Math.floor(next() * i)));
}
{
  const next = rng(43);
  add('identical sizes', Array.from({ length: 25 }, () => [100, 44]), tree(25, (i) => Math.floor(next() * i)));
}
add('sizes beyond the world boundary', [[2.2e6, 30], [40, 30], [60, 40], [2.5e6, 20]], [[0, 1], [0, 2], [2, 3]]);
add('huge sizes', [[1e300, 40], [50, 40], [80, 40]], [[0, 1], [0, 2]]);
add('sizes past the plain number strings', [[1e24, 40], [50, 40]], [[0, 1]]);
add('infinite size', [[Infinity, 40], [50, 40]], [[0, 1]]);

// ---------------------------------------------------------------------------------------------------------------------
// The page code: the libraries, Mermaid's createCytoscapeInstance, the recorders and the guards
// ---------------------------------------------------------------------------------------------------------------------
function pageSetup() {
  if (typeof window === 'undefined' || typeof document === 'undefined' || !/Chrome/.test(navigator.userAgent)) {
    throw new Error('the cose goldens must be computed in Chrome');
  }

  window.cytoscape.use(window.cytoscapeCoseBilkent);

  // The bits of a double, as 16 hexadecimal digits
  const view = new DataView(new ArrayBuffer(8));
  window.hex = (v) => {
    view.setFloat64(0, v);
    return view.getBigUint64(0).toString(16).padStart(16, '0');
  };

  // Recorders of the Math functions the layout calls, and the Math.random guard
  const original = { sin: Math.sin, cos: Math.cos, log: Math.log, pow: Math.pow, random: Math.random };
  window.original = original;
  window.recorded = { trig: new Set(), log: new Set(), pow: new Set() };
  window.randomCalls = 0;
  Math.sin = (x) => {
    window.recorded.trig.add(x);
    return original.sin(x);
  };
  Math.cos = (x) => {
    window.recorded.trig.add(x);
    return original.cos(x);
  };
  Math.log = (x) => {
    window.recorded.log.add(x);
    return original.log(x);
  };
  Math.pow = (x, y) => {
    window.recorded.pow.add(`${window.hex(x)},${window.hex(y)}`);
    return original.pow(x, y);
  };
  Math.random = () => {
    window.randomCalls++;
    return original.random();
  };

  // The iteration count and the path the layout takes
  const CoSELayout = window.coseBase.CoSELayout;
  const tick = CoSELayout.prototype.tick;
  CoSELayout.prototype.tick = function () {
    window.ticks++;
    return tick.call(this);
  };
  const radially = CoSELayout.prototype.positionNodesRadially;
  CoSELayout.prototype.positionNodesRadially = function (forest) {
    window.radial++;
    return radially.call(this, forest);
  };

  // Mermaid's createCytoscapeInstance (rendering-util/layout-algorithms/cose-bilkent/cytoscape-setup.ts) with its
  // addNodes and addEdges, as written; d3's select('body').append('div') is written with the DOM
  window.createCytoscapeInstance = (data) => {
    const renderEl = document.body.appendChild(document.createElement('div'));
    renderEl.setAttribute('id', 'cy');
    renderEl.setAttribute('style', 'display:none');
    const cy = window.cytoscape({
      container: document.getElementById('cy'),
      style: [{ selector: 'edge', style: { 'curve-style': 'bezier' } }]
    });
    renderEl.remove();
    data.nodes.forEach((node) => {
      cy.add({
        group: 'nodes',
        data: { id: node.id, labelText: node.label, height: node.height, width: node.width, padding: node.padding ?? 0 },
        position: { x: node.x ?? 0, y: node.y ?? 0 }
      });
    });
    data.edges.forEach((edge) => {
      cy.add({ group: 'edges', data: { id: edge.id, source: edge.start, target: edge.end } });
    });
    cy.nodes().forEach(function (n) {
      n.layoutDimensions = () => {
        const nodeData = n.data();
        return { w: nodeData.width, h: nodeData.height };
      };
    });
    cy.layout({ name: 'cose-bilkent', quality: 'proof', styleEnabled: false, animate: false }).run();
    return cy;
  };

  // One layout: the positions and edge points (extractPositionedNodes and extractPositionedEdges), or the error; the
  // sizes come as strings, because the JSON that carries them to the page has no infinity
  window.runCase = (nodes, edges) => {
    const data = {
      nodes: nodes.map(([width, height], i) => ({ id: String(i), label: '', width: Number(width), height: Number(height) })),
      edges: edges.map(([s, t], i) => ({ id: `e${i}`, start: String(s), end: String(t) }))
    };
    window.ticks = 0;
    window.radial = 0;
    window.randomCalls = 0;
    const num = (v) => (Number.isNaN(v) ? null : v);
    let result;
    try {
      const cy = window.createCytoscapeInstance(data);
      result = {
        centers: cy.nodes().map((n) => [num(n.position().x), num(n.position().y)]),
        points: cy.edges().map((e) => {
          const r = e._private.rscratch;
          return [r.startX, r.startY, r.midX, r.midY, r.endX, r.endY].map(num);
        }),
        iterations: window.ticks
      };
      if (window.radial !== 1) {
        throw new Error(`the layout took the radial path ${window.radial} times`);
      }
    } catch (e) {
      result = { error: `${e.name}: ${e.message}` };
    }
    if (window.randomCalls !== 0) {
      throw new Error(`Math.random was called ${window.randomCalls} times`);
    }
    return result;
  };
}

// The math vectors: seeded arguments, the recorded ones and the cooling pairs, computed with the original functions
function pageMath() {
  const { sin, cos, log, pow } = window.original;
  const hex = window.hex;
  let a = 7;
  const next = () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };

  // Angles: 20,000 in [0, 2π), 2,000 in [-8, 8], and those of the layouts
  const angles = [];
  for (let i = 0; i < 20000; i++) angles.push(next() * 2 * Math.PI);
  for (let i = 0; i < 2000; i++) angles.push((next() * 2 - 1) * 8);
  const layoutAngles = [...window.recorded.trig].sort((x, y) => x - y);
  const trig = [...angles, ...layoutAngles].map((x) => [hex(x), hex(sin(x)), hex(cos(x))]);

  // Logarithms: 5,000 positive values over the double range and near 1, and those of the layouts
  const logs = [];
  for (let i = 0; i < 5000; i++) {
    const kind = i % 4;
    const v =
      kind === 0
        ? next() * 1000
        : kind === 1
          ? 1 + (next() * 2 - 1) * 2 ** -Math.floor(next() * 52)
          : kind === 2
            ? 2 ** Math.floor(next() * 2098 - 1074) * (1 + next())
            : Math.floor(next() * 5000) + 1;
    if (v > 0 && Number.isFinite(v)) logs.push(v);
  }
  const layoutLogs = [...window.recorded.log].sort((x, y) => x - y);
  const logVectors = [...logs, ...layoutLogs].map((x) => [hex(x), hex(log(x))]);

  // The cooling pairs of cose-base's tick for 1 to 1,016 nodes: Math.pow(coolingCycle, Math.log(100 * (initialCoolingFactor
  // - finalTemperature)) / Math.log(maxCoolingCycle)) with maxCoolingCycle 25, finalTemperature 0.04, the initial
  // cooling factor of initSpringEmbedder and the cycles before the iteration limit max(5n, 2500)
  const pairs = new Map();
  for (let n = 1; n <= 1016; n++) {
    const initial = n > 1000 ? Math.max(0.33, 1.0 - ((n - 1000) / (5000 - 1000)) * (1 - 0.33)) : 1.0;
    const exponent = log(100 * (initial - 100 / 2500)) / log(2500 / 100);
    const maxIterations = Math.max(n * 5, 2500);
    for (let cycle = 1; cycle * 100 < maxIterations; cycle++) {
      pairs.set(`${hex(cycle)},${hex(exponent)}`, [cycle, exponent]);
    }
  }
  for (const key of window.recorded.pow) {
    const [x, y] = key.split(',');
    const view = new DataView(new ArrayBuffer(8));
    const value = (h) => {
      view.setBigUint64(0, BigInt('0x' + h));
      return view.getFloat64(0);
    };
    pairs.set(key, [value(x), value(y)]);
  }
  const powVectors = [...pairs.values()].map(([x, y]) => [hex(x), hex(y), hex(pow(x, y))]);
  return { trig, log: logVectors, pow: powVectors };
}

// A mindmap drawn by mermaid: each node's size (getBBox of its group) and center, and each edge's points
async function drawMindmap(page, text) {
  return page.evaluate(async (text) => {
    const host = document.createElement('pre');
    host.className = 'mermaid';
    host.textContent = text;
    document.body.appendChild(host);
    await window.mermaid.run({ nodes: [host] });
    const svg = host.querySelector('svg');
    const nodes = [...svg.querySelectorAll('g.node')].map((g) => {
      const id = Number(g.id.replace(/^.*node_/, ''));
      const box = g.getBBox();
      const m = /translate\(([^,]+),\s*([^)]+)\)/.exec(g.getAttribute('transform'));
      return { id, size: [box.width, box.height], center: [Number(m[1]), Number(m[2])] };
    });
    const edges = [...svg.querySelectorAll('path[data-id^="edge_"]')].map((p) => {
      const [, s, t] = /^edge_(\d+)_(\d+)$/.exec(p.getAttribute('data-id'));
      const points = JSON.parse(atob(p.getAttribute('data-points')));
      return { s: Number(s), t: Number(t), points: points.flatMap((q) => [q.x, q.y]) };
    });
    host.remove();
    return { nodes, edges };
  }, text);
}

const mindmaps = [
  `mindmap
  root((Planning))
    Goals
      Short term
      Long term
        Five years
    Resources
      People
      [Budget]
      Tools
        Editors
        Compilers
    Risks
      ))Late((
      {{Scope}}`,
  `mindmap
  Center
    A
      A1
      A2
      A3
    B
      B1
        B11
        B12
    C
    D
      D1
    E
      E1
      E2`,
  `mindmap
  root(The **project** plan)
    Design
      Architecture
      Interfaces
      Data model
    Build
      Code
      Tests
        Unit
        System
      Reviews
    Ship
      Package
      Release notes
      Announce`
];

// ---------------------------------------------------------------------------------------------------------------------
// Run everything in one browser
// ---------------------------------------------------------------------------------------------------------------------
const browser = await puppeteer.launch({ headless: true });
const chrome = await browser.version();
let layouts;
let math;
try {
  // The mindmaps, drawn by mermaid in its own page
  const mermaidPage = await browser.newPage();
  await mermaidPage.goto('about:blank');
  await mermaidPage.addScriptTag({ path: join(mermaidFolder, 'dist', 'mermaid.min.js') });
  await mermaidPage.evaluate(() => window.mermaid.initialize({ startOnLoad: false }));
  const drawn = [];
  for (const text of mindmaps) {
    drawn.push(await drawMindmap(mermaidPage, text));
  }
  await mermaidPage.close();
  drawn.forEach((d, k) => {
    const nodes = [...d.nodes].sort((x, y) => x.id - y.id);
    const edges = [...d.edges].sort((x, y) => x.t - y.t);
    add(`mindmap ${k + 1}`, nodes.map((n) => n.size), edges.map((e) => [e.s, e.t]));
    cases[cases.length - 1].drawn = { centers: nodes.map((n) => n.center), points: edges.map((e) => e.points) };
  });

  // The layouts and the math, in a page with the extracted libraries
  const page = await browser.newPage();
  await page.goto('about:blank');
  await page.addScriptTag({ content: layoutBase });
  await page.addScriptTag({ content: coseBase });
  await page.addScriptTag({ content: coseBilkent });
  await page.addScriptTag({ type: 'module', content: cytoscapeScript });
  await page.waitForFunction(() => typeof window.cytoscape === 'function');
  await page.evaluate(pageSetup);
  layouts = [];
  for (const c of cases) {
    const result = await page.evaluate(
      (nodes, edges) => window.runCase(nodes, edges),
      c.nodes.map(([w, h]) => [String(w), String(h)]),
      c.edges
    );
    if (c.drawn) {
      // The replay must give what Mermaid drew
      const same = JSON.stringify(result.centers) === JSON.stringify(c.drawn.centers) &&
        JSON.stringify(result.points) === JSON.stringify(c.drawn.points);
      if (!same) {
        throw new Error(`the replay of ${c.name} differs from the mindmap mermaid drew`);
      }
    }
    layouts.push({ name: c.name, nodes: c.nodes, edges: c.edges, ...result });
    console.log(`${c.name}: ${result.error ?? `${result.iterations} iterations`}`);
  }
  math = await page.evaluate(pageMath);
} finally {
  await browser.close();
}

// ---------------------------------------------------------------------------------------------------------------------
// Write the files: one case (or vector) per line keeps them reviewable and their diffs small
// ---------------------------------------------------------------------------------------------------------------------
const source =
  `Generated by tools/visual-diff/mermaid/gen-cose-goldens.mjs from mermaid ${mermaidVersion} (MIT, Knut Sveidqvist and ` +
  'contributors) with the cytoscape 3.34.0, layout-base 1.0.2, cose-base 1.0.3 and cytoscape-cose-bilkent 4.1.0 it ' +
  'bundles (MIT; extracted from dist/mermaid.js.map), in headless Chrome; the inputs are original. See ' +
  'src/DemaConsulting.CanvasNet.Mermaid/third-party/README.md.';
const write = (out, note, name, items) => {
  const lines = [];
  lines.push('{');
  lines.push(`  "source": ${JSON.stringify(source)},`);
  lines.push(`  "mermaidVersion": ${JSON.stringify(mermaidVersion)},`);
  lines.push(`  "chrome": ${JSON.stringify(chrome)},`);
  lines.push(`  "note": ${JSON.stringify(note)},`);
  for (let k = 0; k < name.length; k++) {
    lines.push(`  "${name[k]}": [`);
    lines.push(items[k].map((item) => '    ' + JSON.stringify(item)).join(',\n'));
    lines.push(k + 1 < name.length ? '  ],' : '  ]');
  }
  lines.push('}');
  mkdirSync(dirname(out), { recursive: true });
  writeFileSync(out, lines.join('\n') + '\n');
};
const num = (v) => (Number.isFinite(v) ? v : v === Infinity ? 'Infinity' : v === -Infinity ? '-Infinity' : null);
write(
  layoutsOut,
  'Node sizes and the results are JSON numbers (exact doubles); NaN is null and an infinite size is a string.',
  ['layouts'],
  [layouts.map((l) => ({ ...l, nodes: l.nodes.map(([w, h]) => [num(w), num(h)]) }))]
);
write(
  mathOut,
  'Each value is the 16 hexadecimal digits of a double. trig: [x, sin x, cos x]; log: [x, log x]; pow: [x, y, pow(x, y)].',
  ['trig', 'log', 'pow'],
  [math.trig, math.log, math.pow]
);
console.log(`wrote ${layouts.length} layouts to ${layoutsOut}`);
console.log(`wrote ${math.trig.length} angles, ${math.log.length} logarithms and ${math.pow.length} powers to ${mathOut}`);
