// gen-dagre-goldens.mjs - records the layouts that the pinned dagre-d3-es computes on generated graphs, as the stage
// goldens of the CanvasNet.Mermaid dagre port.
//
// Developer tooling of tools/visual-diff; see the README there. Usage:
//
//     node gen-dagre-goldens.mjs [--modules <folder>] [--out <file>]
//
// <modules> is a folder that holds node_modules/dagre-d3-es and node_modules/lodash-es (default: the repository root).
// <out> is the golden file (default:
// test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/dagre-layouts.json). dagre-d3-es is pure
// JavaScript, so it runs directly in Node. Every graph comes from a seeded generator (seeded random DAGs, cyclic
// graphs, compound graphs with nested clusters, labeled edges, self loops, parallel named edges, numeric node names,
// every rank direction, ranker and alignment), so the inputs are original and the file is reproducible. For each graph
// the file records the calls that build it and dagre's results: every node's rank and order (read from dagre's
// internal layout graph), its center and size, every edge's route and label center, and the drawing size; or the
// error dagre throws. Nothing is downloaded.

import { mkdirSync, readFileSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// The repository root is three folders above tools/visual-diff/mermaid
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

// Read the two optional arguments
const args = process.argv.slice(2);
let modules = root;
let out = join(root, 'test', 'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'upstream-vectors', 'dagre-layouts.json');
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--modules' && i + 1 < args.length) {
    modules = resolve(args[++i]);
  } else if (args[i] === '--out' && i + 1 < args.length) {
    out = resolve(args[++i]);
  } else {
    console.error('usage: node gen-dagre-goldens.mjs [--modules <folder>] [--out <file>]');
    process.exit(2);
  }
}

const dagreRoot = join(modules, 'node_modules', 'dagre-d3-es');
const dagreVersion = JSON.parse(readFileSync(join(dagreRoot, 'package.json'), 'utf8')).version;
const dagreDir = join(dagreRoot, 'src', 'dagre');
const lodashUrl = pathToFileURL(join(modules, 'node_modules', 'lodash-es', 'lodash.js')).href;

// ---------------------------------------------------------------------------------------------------------------------
// A copy of layout.js that also exposes dagre's internal layout graph (for the ranks and orders), importing the
// unchanged stage modules from node_modules; the copy lives in a temporary folder and is removed at the end
// ---------------------------------------------------------------------------------------------------------------------
const hookLine = "time('  updateInputGraph', () => updateInputGraph(g, layoutGraph));";
let layoutSource = readFileSync(join(dagreDir, 'layout.js'), 'utf8');
if (!layoutSource.includes(hookLine)) {
  throw new Error('layout.js has changed: the updateInputGraph call was not found');
}
layoutSource = layoutSource
  .replace(hookLine, `${hookLine}\n    globalThis.__dagreLayoutGraph = layoutGraph;`)
  .replace("from 'lodash-es'", `from '${lodashUrl}'`)
  .replace(/from '\.\.\/graphlib\/index\.js'/, `from '${pathToFileURL(join(dagreRoot, 'src', 'graphlib', 'index.js')).href}'`)
  .replace(/from '\.\/([^']+)'/g, (_, path) => `from '${pathToFileURL(join(dagreDir, path)).href}'`);
const work = mkdtempSync(join(tmpdir(), 'dagre-goldens-'));
const hooked = join(work, 'layout.mjs');
writeFileSync(hooked, layoutSource);
const { layout } = await import(pathToFileURL(hooked).href);
const { Graph } = await import(pathToFileURL(join(dagreRoot, 'src', 'graphlib', 'index.js')).href);
rmSync(work, { recursive: true, force: true });

// ---------------------------------------------------------------------------------------------------------------------
// Seeded generator (mulberry32)
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

const rankdirs = ['TB', 'BT', 'LR', 'RL', 'tb', 'lr'];
const rankers = [undefined, undefined, 'network-simplex', 'tight-tree', 'longest-path'];
const aligns = [undefined, undefined, undefined, undefined, 'UL', 'UR', 'DL', 'DR'];
const labelposes = ['c', 'c', 'l', 'r', undefined];

function pick(next, items) {
  return items[Math.floor(next() * items.length)];
}

function size(next, min, max) {
  // Mostly whole numbers, sometimes halves and thirds as text measurement gives
  const value = min + next() * (max - min);
  const kind = next();
  return kind < 0.6 ? Math.round(value) : kind < 0.8 ? Math.round(value * 2) / 2 : value;
}

// Node names: letters, numeric names (array indexes, which JavaScript orders first) or Mermaid-like ids
function names(next, n) {
  const style = next();
  const result = [];
  for (let i = 0; i < n; i++) {
    if (style < 0.3) {
      result.push(String(Math.floor(next() * 3) === 0 ? i * 7 + 1 : n - i));
    } else if (style < 0.5) {
      result.push(i % 3 === 0 ? String(i + 10) : 'n' + i);
    } else if (style < 0.75) {
      result.push(String.fromCharCode(65 + (i % 26)) + (i >= 26 ? String(i) : ''));
    } else {
      result.push('flowchart-N' + i + '-' + (100 + i));
    }
  }
  return [...new Set(result)];
}

function graphLabel(next, family) {
  const label = { rankdir: pick(next, rankdirs) };
  if (next() < 0.5) label.nodesep = size(next, 10, 80);
  if (next() < 0.5) label.ranksep = size(next, 10, 80);
  if (next() < 0.3) label.edgesep = size(next, 5, 40);
  if (next() < 0.6) {
    label.marginx = 8;
    label.marginy = 8;
  }
  const ranker = pick(next, rankers);
  if (ranker !== undefined) label.ranker = ranker;
  const align = pick(next, aligns);
  if (align !== undefined) label.align = align;
  if (family === 'cyclic' && next() < 0.5) label.acyclicer = 'greedy';
  return label;
}

function edgeLabel(next, labeled) {
  const label = {};
  if (labeled) {
    label.width = size(next, 10, 90);
    label.height = size(next, 10, 30);
    const labelpos = pick(next, labelposes);
    if (labelpos !== undefined) label.labelpos = labelpos;
    if (next() < 0.3) label.labeloffset = size(next, 2, 15);
  }
  if (next() < 0.2) label.minlen = 1 + Math.floor(next() * 3);
  if (next() < 0.2) label.weight = 1 + Math.floor(next() * 3);
  return label;
}

// One case: the calls that build the graph
function makeCase(name, seed, family) {
  const next = rng(seed);
  const multigraph = family === 'simple' ? false : true;
  const compound = family === 'simple' ? false : true;
  const n = family === 'tiny' ? Math.floor(next() * 3) : family === 'big' ? 40 + Math.floor(next() * 30) : 3 + Math.floor(next() * (family === 'cluster' ? 16 : family === 'nested' ? 30 : 22));
  const ids = names(next, n);
  const nodes = ids.map((id) => ({ id, width: size(next, 10, 120), height: size(next, 10, 60) }));

  // Clusters: a random tree of up to four clusters, each holding some of the nodes
  if (family === 'cluster' || family === 'nested' || family === 'big') {
    const clusterCount = family === 'cluster' ? 1 + Math.floor(next() * 4) : 3 + Math.floor(next() * 6);
    const clusters = [];
    for (let c = 0; c < clusterCount; c++) {
      const id = next() < 0.3 ? String(900 + c) : 'subGraph' + c;
      const parent = c > 0 && next() < (family === 'cluster' ? 0.5 : 0.8) ? clusters[Math.floor(next() * clusters.length)].id : undefined;
      clusters.push({ id, parent });
    }
    for (const node of nodes) {
      if (next() < 0.6) node.parent = clusters[Math.floor(next() * clusters.length)].id;
    }
    // Every cluster holds at least one node, so that it is a cluster and not a leaf
    for (const cluster of clusters) {
      const members = nodes.filter((node) => node.parent === cluster.id).length +
        clusters.filter((other) => other.parent === cluster.id).length;
      if (members === 0) nodes.push({ id: cluster.id + '-only', width: size(next, 10, 60), height: size(next, 10, 40), parent: cluster.id });
    }
    for (const cluster of clusters) {
      const entry = { id: cluster.id, width: 0, height: 0 };
      if (cluster.parent !== undefined) entry.parent = cluster.parent;
      nodes.splice(Math.floor(next() * (nodes.length + 1)), 0, entry);
    }
    nodes.clusterIds = new Set(clusters.map((c) => c.id));
  }

  // Edges between leaves
  const leaves = nodes.filter((node) => !nodes.clusterIds || !nodes.clusterIds.has(node.id)).map((node) => node.id);
  const edges = [];
  const density = family === 'tiny' ? 1 : 0.8 + next() * 1.6;
  const edgeCount = leaves.length < 2 ? 0 : Math.floor(leaves.length * density);
  let named = 0;
  for (let k = 0; k < edgeCount; k++) {
    let i = Math.floor(next() * leaves.length);
    let j = Math.floor(next() * leaves.length);
    if (i === j) j = (j + 1) % leaves.length;
    if (family !== 'cyclic' && i > j) [i, j] = [j, i];
    const edge = { v: leaves[i], w: leaves[j], label: edgeLabel(next, next() < 0.35) };
    if (multigraph && next() < 0.7) edge.name = 'L-' + edge.v + '-' + edge.w + '-' + named++;
    edges.push(edge);
  }
  if (family === 'selfloop' || (family !== 'simple' && next() < 0.15)) {
    for (let k = 0; k < 1 + Math.floor(next() * 3) && leaves.length > 0; k++) {
      const v = leaves[Math.floor(next() * leaves.length)];
      const edge = { v, w: v, label: edgeLabel(next, next() < 0.5) };
      if (multigraph) edge.name = 'self-' + v + '-' + named++;
      edges.push(edge);
    }
  }
  // A parallel edge with another name
  if (multigraph && edges.length > 0 && next() < 0.3) {
    const first = edges[0];
    if (first.v !== first.w) edges.push({ v: first.v, w: first.w, name: 'parallel-' + named++, label: edgeLabel(next, next() < 0.5) });
  }
  // Without names in a simple graph, a repeated pair replaces the label: keep the cases free of repeats there
  const kept = multigraph ? edges : edges.filter((e, index) => edges.findIndex((o) => o.v === e.v && o.w === e.w) === index);

  return {
    name,
    multigraph,
    compound,
    graph: graphLabel(next, family),
    nodes: nodes.map(({ id, width, height, parent }) => (parent === undefined ? { id, width, height } : { id, width, height, parent })),
    edges: kept
  };
}

// ---------------------------------------------------------------------------------------------------------------------
// Running one case
// ---------------------------------------------------------------------------------------------------------------------
const stats = { halfRankLabels: 0 };
function run(c) {
  const g = new Graph({ multigraph: c.multigraph, compound: c.compound });
  g.setGraph({ ...c.graph });
  g.setDefaultEdgeLabel(() => ({}));
  for (const node of c.nodes) g.setNode(node.id, { width: node.width, height: node.height });
  if (c.compound) {
    for (const node of c.nodes) if (node.parent !== undefined) g.setParent(node.id, node.parent);
  }
  for (const e of c.edges) {
    if (e.name !== undefined) g.setEdge(e.v, e.w, { ...e.label }, e.name);
    else g.setEdge(e.v, e.w, { ...e.label });
  }
  try {
    globalThis.__dagreLayoutGraph = undefined;
    layout(g);
  } catch (error) {
    return { error: String(error.message) };
  }
  const lg = globalThis.__dagreLayoutGraph;
  const nodes = {};
  for (const v of g.nodes()) {
    const label = g.node(v);
    const internal = lg.node(v);
    nodes[v] = {
      x: label.x,
      y: label.y,
      width: label.width,
      height: label.height,
      rank: internal.rank ?? null,
      order: internal.order ?? null
    };
  }
  const edges = g.edges().map((e) => {
    const label = g.edge(e);
    const result = { v: e.v, w: e.w };
    if (e.name !== undefined) result.name = e.name;
    result.points = (label.points ?? []).map((p) => [p.x, p.y]);
    if (Object.prototype.hasOwnProperty.call(label, 'x')) {
      result.x = label.x;
      result.y = label.y;
    }
    return result;
  });
  for (const e of lg.edges()) {
    const labelRank = lg.edge(e).labelRank;
    if (labelRank !== undefined && !Number.isInteger(labelRank)) stats.halfRankLabels++;
  }
  return { nodes, edges, width: g.graph().width, height: g.graph().height };
}

// ---------------------------------------------------------------------------------------------------------------------
// The cases
// ---------------------------------------------------------------------------------------------------------------------
const families = [
  ['dag', 50],
  ['cyclic', 35],
  ['cluster', 50],
  ['nested', 30],
  ['big', 8],
  ['selfloop', 20],
  ['simple', 15],
  ['tiny', 10]
];
const cases = [];
let seed = 20261108;
for (const [family, count] of families) {
  for (let k = 0; k < count; k++) {
    cases.push(makeCase(`${family}-${String(k + 1).padStart(3, '0')}`, seed++, family));
  }
}
// Hand-made cases: the empty graph, a chain with a long edge, and a labeled edge in every direction
cases.push({ name: 'empty', multigraph: true, compound: true, graph: { rankdir: 'TB' }, nodes: [], edges: [] });
cases.push({
  name: 'chain-long-edge', multigraph: true, compound: true, graph: { rankdir: 'TB', nodesep: 50, ranksep: 50 },
  nodes: [{ id: 'a', width: 50, height: 20 }, { id: 'b', width: 60, height: 30 }, { id: 'c', width: 40, height: 20 }],
  edges: [{ v: 'a', w: 'b', label: {} }, { v: 'b', w: 'c', label: {} }, { v: 'a', w: 'c', label: { width: 30, height: 10, labelpos: 'c' } }]
});
for (const rankdir of ['TB', 'BT', 'LR', 'RL']) {
  cases.push({
    name: `labeled-${rankdir}`, multigraph: true, compound: true, graph: { rankdir, marginx: 8, marginy: 8 },
    nodes: [{ id: '1', width: 50, height: 20 }, { id: '2', width: 60, height: 30 }],
    edges: [{ v: '1', w: '2', name: 'e', label: { width: 30, height: 10, labelpos: 'l' } }]
  });
}

const vectors = cases.map((c) => ({ ...c, expected: run(c) }));
const errors = vectors.filter((v) => v.expected.error !== undefined).length;

// One case per line keeps the file reviewable and its diffs small
const lines = [];
lines.push('{');
lines.push(`  "source": ${JSON.stringify(`Generated by tools/visual-diff/mermaid/gen-dagre-goldens.mjs from dagre-d3-es ${dagreVersion} (MIT, Chris Pettitt and the dagre-es contributors); the inputs are original. See src/DemaConsulting.CanvasNet.Mermaid/third-party/README.md.`)},`);
lines.push(`  "dagreVersion": ${JSON.stringify(dagreVersion)},`);
lines.push('  "layouts": [');
lines.push(vectors.map((v) => '    ' + JSON.stringify(v)).join(',\n'));
lines.push('  ]');
lines.push('}');
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, lines.join('\n') + '\n');
console.log(`dagre-d3-es ${dagreVersion}: wrote ${vectors.length} layouts (${errors} errors, ${stats.halfRankLabels} labels at half ranks) to ${out}`);
