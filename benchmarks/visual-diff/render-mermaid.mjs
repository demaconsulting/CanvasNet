// render-mermaid.mjs - draws Mermaid diagrams with the reference implementation (the repository's pinned
// @mermaid-js/mermaid-cli and mermaid in headless Chrome through puppeteer) and writes, for each diagram, the
// picture (PNG) and its layout read back from the live SVG (a .layout.json file).
//
// Developer tooling of benchmarks/visual-diff; see the README there. Usage (from anywhere, after "npm ci" in the
// repository root):
//
//     node render-mermaid.mjs [--scale N] [--png-dir DIR] [--layout-dir DIR] <diagram.mmd>...
//
// For each <name>.mmd it writes <name>.png into --png-dir and <name>.layout.json into --layout-dir (either may be left
// out; at least one is needed). mermaid-cli's renderMermaid draws the SVG (what "mmdc -o x.svg" writes); the SVG is
// then shown at its natural size (times --scale) in a page with the same fonts, which gives the picture and the
// layout. The style sheet liberation.css (next to this script) is added to every page before Mermaid draws, so that
// the browser measures and draws text with the Liberation Sans faces that CanvasNet bundles.
//
// The layout file holds, in the coordinates of the SVG's view box (rounded to 0.001):
//   - viewBox: [x, y, width, height];
//   - nodes: id (the DOM id without the render id prefix, e.g. "flowchart-A-0"), shape, x and y (the node's
//     position: its center as laid out), box ([x, y, width, height] of the node's drawing);
//   - clusters: id (the subgraph id) and box (the subgraph's rectangle);
//   - edges: id (e.g. "L_A_B_0"), length, and points: the path sampled at 21 evenly spaced lengths (0%, 5%, ...,
//     100%) with getPointAtLength;
//   - edgeLabels: id (the edge id) and box of each label that has a size.
// Groups nested in other groups (an extracted subgraph is drawn in a translated group) are mapped with getScreenCTM.

import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..');
const svgId = 'my-svg';
const samples = 20;

// --- Arguments ---------------------------------------------------------------------------------------------------
const args = process.argv.slice(2);
const files = [];
let scale = 1;
let pngDir;
let layoutDir;
for (let i = 0; i < args.length; i++) {
  const arg = args[i];
  if (arg === '--scale') {
    scale = Number(args[++i]);
  } else if (arg === '--png-dir') {
    pngDir = args[++i];
  } else if (arg === '--layout-dir') {
    layoutDir = args[++i];
  } else if (arg.startsWith('--')) {
    usage(`unknown option ${arg}`);
  } else {
    files.push(arg);
  }
}

if (files.length === 0 || (!pngDir && !layoutDir) || !(scale > 0)) {
  usage('a diagram, a positive --scale and --png-dir or --layout-dir are needed');
}

function usage(message) {
  console.error(`render-mermaid.mjs: ${message}`);
  console.error('usage: node render-mermaid.mjs [--scale N] [--png-dir DIR] [--layout-dir DIR] <diagram.mmd>...');
  process.exit(2);
}

// --- The packages (from the repository's node_modules) ----------------------------------------------------------
const resolver = createRequire(join(repoRoot, 'noop.js'));
const load = async (name) => import(pathToFileURL(resolver.resolve(name)).href);
const puppeteer = (await load('puppeteer')).default;
const { renderMermaid } = await load('@mermaid-js/mermaid-cli');
const versionOf = (name) => JSON.parse(readFileSync(join(repoRoot, 'node_modules', name, 'package.json'), 'utf8')).version;
const mermaidVersion = versionOf('mermaid');
const cliVersion = versionOf('@mermaid-js/mermaid-cli');

// --- The style sheet: liberation.css with its font files inlined as data URLs -----------------------------------
const cssPath = join(here, 'liberation.css');
const css = readFileSync(cssPath, 'utf8').replace(/url\("([^"]+\.ttf)"\)/g, (_, file) =>
  `url(data:font/ttf;base64,${readFileSync(resolve(dirname(cssPath), file)).toString('base64')})`);

// mermaid-cli opens a new page for each diagram and adds its own style sheet only after Mermaid has drawn (and
// measured) the labels; the fonts must be there before, so every new page gets the style sheet first.
const addStyleSheet = (text) => {
  const add = () => {
    const style = document.createElement('style');
    style.textContent = text;
    document.head.appendChild(style);
  };
  if (document.head) {
    add();
  } else {
    document.addEventListener('DOMContentLoaded', add);
  }
};

const browser = await puppeteer.launch({ headless: true });
const host = {
  newPage: async () => {
    const page = await browser.newPage();
    await page.evaluateOnNewDocument(addStyleSheet, css);
    return page;
  },
};

// mermaid-cli's own PNG output narrows the page to the picture after the SVG (width 100%) has been laid out, which
// shrinks the picture by the page margin; the PNG is therefore taken from mermaid-cli's SVG shown at its natural size.
const options = { backgroundColor: 'white', svgId };

try {
  for (const file of files) {
    const name = basename(file).replace(/\.mmd$|\.mermaid$/i, '');
    const definition = readFileSync(file, 'utf8');
    const { data } = await renderMermaid(host, definition, 'svg', options);
    const { layout, png } = await readBack(new TextDecoder().decode(data), definition, !!pngDir);
    if (pngDir) {
      mkdirSync(pngDir, { recursive: true });
      writeFileSync(join(pngDir, `${name}.png`), png);
    }

    if (layoutDir) {
      const golden = { source: basename(file), mermaid: mermaidVersion, mermaidCli: cliVersion, ...layout };
      mkdirSync(layoutDir, { recursive: true });
      writeFileSync(join(layoutDir, `${name}.layout.json`), `${format(golden)}\n`);
    }

    console.log(`mermaid ${mermaidVersion} (mermaid-cli ${cliVersion}): ${file}`);
  }
} finally {
  await browser.close();
}

// JSON with two-space indentation, and each array of numbers on one line.
function format(value) {
  return JSON.stringify(value, null, 2).replace(/\[\s*(-?[\d.e+-]+(?:,\s*-?[\d.e+-]+)*)\s*\]/g, (_, inner) =>
    `[${inner.split(/,\s*/).join(', ')}]`);
}

// Places the SVG in a page with the same fonts (setContent replaces the document that evaluateOnNewDocument styled, so
// the style sheet is part of the content), reads its geometry back from the DOM and takes the picture; the node
// shapes (which the SVG does not name) come from the diagram's own data (mermaid.mermaidAPI.getDiagramFromText).
async function readBack(svgText, definition, picture) {
  const page = await host.newPage();
  try {
    await page.setViewport({ width: 4096, height: 1024, deviceScaleFactor: scale });
    await page.setContent(`<!DOCTYPE html><html><head><style>${css}</style></head><body style="margin: 0; background: white">${svgText}</body></html>`);
    await page.addScriptTag({ path: join(repoRoot, 'node_modules', 'mermaid', 'dist', 'mermaid.min.js') });
    await page.evaluate(async () => {
      await Promise.all(Array.from(document.fonts, (font) => font.load()));
      await document.fonts.ready;
    });
    const shapes = await page.evaluate(async (text) => {
      window.mermaid.initialize({ startOnLoad: false });
      const diagram = await window.mermaid.mermaidAPI.getDiagramFromText(text);
      const data = diagram.db.getData?.();
      return Object.fromEntries((data?.nodes ?? []).filter((n) => n.domId).map((n) => [n.domId, n.shape ?? null]));
    }, definition);
    const layout = await page.evaluate(extract, svgId, samples, shapes);
    let png;
    if (picture) {
      // The picture is the view box rounded up to whole pixels (the natural size CanvasNet draws at), times the scale
      const [width, height] = layout.viewBox.slice(2);
      await page.$eval(`#${svgId}`, (element, [w, h]) => {
        element.style.display = 'block';
        element.style.maxWidth = 'none';
        element.style.width = `${w}px`;
        element.style.height = `${h}px`;
      }, [width, height]);
      png = await page.screenshot({ clip: { x: 0, y: 0, width: Math.ceil(width - 1e-6), height: Math.ceil(height - 1e-6) }, captureBeyondViewport: true });
    }

    return { layout, png };
  } finally {
    await page.close();
  }
}

// Runs in the page.
function extract(id, count, shapes) {
  const svg = document.getElementById(id);
  const toSvg = svg.getScreenCTM().inverse();
  const round = (v) => Math.round(v * 1000) / 1000 + 0;
  const withoutPrefix = (text) => (text && text.startsWith(`${id}-`) ? text.slice(id.length + 1) : text);
  const matrix = (element) => toSvg.multiply(element.getScreenCTM());
  const box = (element) => {
    const b = element.getBBox();
    const m = matrix(element);
    const a = new DOMPoint(b.x, b.y).matrixTransform(m);
    const c = new DOMPoint(b.x + b.width, b.y + b.height).matrixTransform(m);
    return [Math.min(a.x, c.x), Math.min(a.y, c.y), Math.abs(c.x - a.x), Math.abs(c.y - a.y)].map(round);
  };

  const nodes = [];
  for (const node of svg.querySelectorAll('g.node')) {
    const m = matrix(node);
    const nodeId = withoutPrefix(node.id);
    nodes.push({ id: nodeId, shape: shapes[nodeId] ?? null, x: round(m.e), y: round(m.f), box: box(node) });
  }

  const clusters = [];
  for (const cluster of svg.querySelectorAll('g.cluster')) {
    const rect = cluster.querySelector(':scope > rect') ?? cluster.querySelector('rect, path');
    if (rect) {
      clusters.push({ id: withoutPrefix(cluster.id), box: box(rect) });
    }
  }

  const edges = [];
  for (const path of svg.querySelectorAll('g.edgePaths path')) {
    const edgeId = withoutPrefix(path.id || path.getAttribute('data-id'));
    if (!edgeId || !path.getAttribute('d')) {
      continue;
    }

    const m = matrix(path);
    const length = path.getTotalLength();
    const points = [];
    for (let i = 0; i <= count; i++) {
      const p = path.getPointAtLength((length * i) / count).matrixTransform(m);
      points.push([round(p.x), round(p.y)]);
    }

    edges.push({ id: edgeId, length: round(length), points });
  }

  const edgeLabels = [];
  for (const label of svg.querySelectorAll('g.edgeLabel .label')) {
    const b = box(label);
    if (b[2] > 0 && b[3] > 0) {
      edgeLabels.push({ id: label.getAttribute('data-id'), box: b });
    }
  }

  const viewBox = svg.getAttribute('viewBox').trim().split(/[\s,]+/).map(Number).map(round);
  return { viewBox, nodes, clusters, edges, edgeLabels };
}
