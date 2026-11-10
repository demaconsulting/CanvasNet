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
// A sequence diagram (aria-roledescription="sequence") gets the sequence layout instead (extractSequence): the actors
// (with their top and bottom drawings and lifelines), messages (with their texts, markers, sequence numbers and
// central-connection circles), notes, activations, frames, background rects, boxes, menus, the title, and the paint
// order; see Fixtures/reference/README.md for the format.
// Any other diagram of the unified renderer (a class diagram, for example) gets the flowchart layout extended with: the
// diagram (its aria-roledescription), the title, each node's class and drawn parts (shapes and labels, with the group
// that holds them), each cluster's class, inner rectangle and label, each edge's class and marker ids, and the
// terminal labels (cardinalities) with their edge. A flowchart keeps the plain layout. The state and ER diagrams also
// get each cluster's shape (state clusters are g.statediagram-cluster and g.note-cluster groups, which are clusters
// here too) and the pieces of a label drawn in several parts; the random id of the last region of a concurrent state is
// written as id-random-<counter>.
// A pie chart (aria-roledescription="pie") gets the pie layout instead (extractPie): the outer circle, each slice (its
// path data as written, in the coordinates of the pie group, and its box), each percentage label, the title and each
// legend entry, with the computed paint and fonts; see Fixtures/reference/README.md for the format.
// A gantt chart (aria-roledescription="gantt") gets the gantt layout (extractGantt): the excluded days, each axis with its
// ticks, the section rows, the tasks and their texts, the section titles, the today line and the title, with the computed
// paint and fonts. Every page runs in the UTC time zone with the clock fixed at FIXED_NOW (2024-06-12T10:30:00Z).
// Groups nested in other groups (an extracted subgraph is drawn in a translated group) are mapped with getScreenCTM.

import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..');
const svgId = 'my-svg';
const samples = 20;
// mermaid's generateId ("id-" + Math.random().toString(36).substr(2, 12) + "-" + a page-wide counter), which a state
// diagram uses for the last region of a concurrent state; the layout of a state diagram writes it as id-random-<counter>
// so that the goldens do not change from run to run
const randomIdPattern = '(?<![0-9A-Za-z_])id-[0-9a-z]{4,12}-(\\d+)(?![0-9])';

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

// A gantt chart reads the clock (its today marker, and today for an unknown after or until id): every page runs in the
// UTC time zone, the zone of the CanvasNet port, with a Date whose constructor without arguments and now() give the fixed
// instant FIXED_NOW (the MermaidRenderOptions.Now that MermaidChartReferenceLayoutTests passes)
export const FIXED_NOW = '2024-06-12T10:30:00Z';
const fixClock = (now) => {
  const RealDate = Date;
  class FixedDate extends RealDate {
    constructor(...args) {
      super(...(args.length === 0 ? [now] : args));
    }

    static now() {
      return now;
    }
  }
  window.Date = FixedDate;
};

const browser = await puppeteer.launch({ headless: true });
const host = {
  newPage: async () => {
    const page = await browser.newPage();
    await page.emulateTimezone('UTC');
    await page.evaluateOnNewDocument(fixClock, Date.parse(FIXED_NOW));
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
      const json = layout.diagram ? pretty(golden) : format(golden);
      writeFileSync(join(layoutDir, `${name}.layout.json`), `${json}\n`);
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

// JSON with two-space indentation, where each array or object that fits in 120 columns is written on one line (the
// sequence goldens, whose many small objects would otherwise take a line per value).
function pretty(value, indent = '', prefix = 0) {
  const inline = (v) => {
    if (Array.isArray(v)) {
      return `[${v.map(inline).join(', ')}]`;
    }
    if (v && typeof v === 'object') {
      const entries = Object.entries(v).map(([k, x]) => `${JSON.stringify(k)}: ${inline(x)}`);
      return entries.length ? `{ ${entries.join(', ')} }` : '{}';
    }
    return JSON.stringify(v);
  };
  const one = inline(value);
  if (value === null || typeof value !== 'object' || indent.length + prefix + one.length <= 120) {
    return one;
  }
  const next = `${indent}  `;
  if (Array.isArray(value)) {
    return `[\n${value.map((x) => next + pretty(x, next)).join(',\n')}\n${indent}]`;
  }
  const entries = Object.entries(value).map(([k, x]) => {
    const key = `${JSON.stringify(k)}: `;
    return next + key + pretty(x, next, key.length);
  });
  return `{\n${entries.join(',\n')}\n${indent}}`;
}

// Places the SVG in a page with the same fonts (setContent replaces the document that evaluateOnNewDocument styled, so
// the style sheet is part of the content), reads its geometry back from the DOM and takes the picture; the node
// shapes (which the SVG does not name) and the sequence actor list come from the diagram's own data
// (mermaid.mermaidAPI.getDiagramFromText).
async function readBack(svgText, definition, picture) {
  const page = await host.newPage();
  try {
    await page.setViewport({ width: 4096, height: 1024, deviceScaleFactor: scale });
    await page.setContent(`<!DOCTYPE html><html><head><style>${css}</style></head><body style="margin: 0; background: white">${svgText}</body></html>`);
    // setContent keeps the page's document, which the new-document scripts never saw: the clock is fixed here too
    await page.evaluate(fixClock, Date.parse(FIXED_NOW));
    await page.addScriptTag({ path: join(repoRoot, 'node_modules', 'mermaid', 'dist', 'mermaid.min.js') });
    const clock = await page.evaluate(() => [new Date().getTimezoneOffset(), Date.now(), new Date().getTime()]);
    if (clock[0] !== 0 || clock[1] !== Date.parse(FIXED_NOW) || clock[2] !== Date.parse(FIXED_NOW)) {
      throw new Error(`the page clock is not fixed to ${FIXED_NOW} in UTC: ${clock}`);
    }

    await page.evaluate(async () => {
      await Promise.all(Array.from(document.fonts, (font) => font.load()));
      await document.fonts.ready;
    });
    const { shapes, groupShapes, actors, terminalTexts } = await page.evaluate(async (text, randomId) => {
      window.mermaid.initialize({ startOnLoad: false });
      const diagram = await window.mermaid.mermaidAPI.getDiagramFromText(text);
      const data = diagram.db.getData?.();
      // A state diagram names the last region of a concurrent state with a random id (see randomIdPattern)
      const key = (id) => (/^state/.test(diagram.type) ? id.replace(new RegExp(randomId, 'g'), 'id-random-$1') : id);
      return {
        // Keyed by the DOM id, or by the id for nodes without one (class notes and lollipop interfaces)
        shapes: Object.fromEntries((data?.nodes ?? []).filter((n) => !n.isGroup || n.domId).map((n) => [key(n.domId ?? n.id), n.shape ?? null])),
        // The shapes of the groups (clusters), keyed the same way
        groupShapes: Object.fromEntries((data?.nodes ?? []).filter((n) => n.isGroup).map((n) => [key(n.domId ?? n.id), n.shape ?? null])),
        actors: diagram.type === 'sequence'
          ? [...diagram.db.getActors().values()].map((a) => ({ name: a.name, type: a.type, description: a.description }))
          : null,
        // The terminal label texts of each edge that has one: [start, end]
        terminalTexts: Object.fromEntries((data?.edges ?? []).filter((e) => e.startLabelRight || e.endLabelLeft)
          .map((e) => [e.id, [e.startLabelRight || '', e.endLabelLeft || '']])),
      };
    }, definition, randomIdPattern);

    // A diagram of the unified renderer other than a flowchart is read back at a scale of exactly 1: shown at its
    // max-width (the view box width rounded to 0.001 px), Chrome lays its SVG texts out with a font scaled by about
    // 0.99995, which narrows their boxes by up to 0.1 px. The view box is grown to whole pixels from the same origin,
    // so nothing moves; extract reports the original view box, which is restored for the picture.
    const viewBoxText = actors ? null : await page.$eval(`#${svgId}`, (element) => {
      const text = element.getAttribute('viewBox');
      if (/^flowchart/.test(element.getAttribute('aria-roledescription') ?? '') || !text) {
        return null;
      }
      const [x, y, w, h] = text.trim().split(/[\s,]+/).map(Number);
      element.dataset.layoutViewBox = text;
      element.setAttribute('viewBox', `${x} ${y} ${Math.ceil(w)} ${Math.ceil(h)}`);
      element.style.maxWidth = 'none';
      element.style.width = `${Math.ceil(w)}px`;
      element.style.height = `${Math.ceil(h)}px`;
      return text;
    });
    const role = await page.$eval(`#${svgId}`, (element) => element.getAttribute('aria-roledescription') ?? '');
    let layout;
    if (actors) {
      layout = await page.evaluate(extractSequence, svgId, samples, actors);
    } else if (role === 'pie') {
      layout = await page.evaluate(extractPie, svgId);
    } else if (role === 'gantt') {
      layout = await page.evaluate(extractGantt, svgId);
    } else {
      layout = await page.evaluate(extract, svgId, samples, shapes, terminalTexts, groupShapes, randomIdPattern);
    }
    if (viewBoxText) {
      await page.$eval(`#${svgId}`, (element, text) => {
        element.setAttribute('viewBox', text);
        delete element.dataset.layoutViewBox;
      }, viewBoxText);
    }
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
function extract(id, count, shapes, terminalTexts, groupShapes, randomId) {
  const svg = document.getElementById(id);
  const toSvg = svg.getScreenCTM().inverse();
  const round = (v) => Math.round(v * 1000) / 1000 + 0;
  const role = svg.getAttribute('aria-roledescription') ?? '';
  // A state diagram's random ids (see randomIdPattern) are written as id-random-<counter>
  const stable = /^state/.test(role)
    ? (text) => (text ? text.replace(new RegExp(randomId, 'g'), 'id-random-$1') : text)
    : (text) => text;
  const withoutPrefix = (text) => stable(text && text.startsWith(`${id}-`) ? text.slice(id.length + 1) : text);
  const matrix = (element) => toSvg.multiply(element.getScreenCTM());
  const box = (element) => {
    const b = element.getBBox();
    const m = matrix(element);
    const a = new DOMPoint(b.x, b.y).matrixTransform(m);
    const c = new DOMPoint(b.x + b.width, b.y + b.height).matrixTransform(m);
    return [Math.min(a.x, c.x), Math.min(a.y, c.y), Math.abs(c.x - a.x), Math.abs(c.y - a.y)].map(round);
  };

  // A diagram of the unified renderer other than a flowchart (class, for example) gets the extended layout: the
  // drawn parts of each node, the labels and inner rectangles of the clusters, the edge markers, the terminal labels
  // and the title. A flowchart keeps the plain layout, so its goldens do not change. The diagrams after the class
  // diagram (state and ER) also get each cluster's shape and the lines of a label drawn in several pieces (a state
  // with descriptions); the class goldens keep their format.
  const extended = !/^flowchart/.test(role);
  const rich = extended && role !== 'classDiagram';
  // A marker id without the render id prefix (my-svg_classDiagram-extensionStart becomes classDiagram-extensionStart)
  const marker = (element, name) => {
    const match = /url\(#(.+)\)/.exec(element.getAttribute(name) ?? '');
    return match ? (match[1].startsWith(`${id}_`) ? match[1].slice(id.length + 1) : withoutPrefix(match[1])) : null;
  };
  const textOf = (element) => element.textContent.replace(/\s+/g, ' ').trim();
  // In the rich layout, the lines and pieces of a text (HTML lines split by <br>, SVG tspans, the title and description
  // texts of a state) are joined with a space
  const labelText = (element) => {
    if (!rich) {
      return textOf(element);
    }
    const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
    const parts = [];
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      parts.push(node.data);
    }
    return parts.join(' ').replace(/\s+/g, ' ').trim();
  };

  // The drawn parts of a node in document order: each shape (rect, path, line, circle, ellipse, polygon) and each
  // label (g.label, or a text outside a label) with the class of the node's child group that holds it. In the rich
  // layout, a label drawn in several pieces (the title and description texts of a state with descriptions) also lists
  // each piece (a foreignObject or text) as an item.
  const partsOf = (node) => {
    const result = [];
    const walk = (element, group) => {
      for (const child of element.children) {
        if (child.matches('g.label, text')) {
          const style = child.getAttribute('style');
          const pieces = rich ? Array.from(child.querySelectorAll(':scope > foreignObject, :scope > text, :scope > g > text')) : [];
          result.push({ tag: child.tagName, class: child.getAttribute('class'), group, box: box(child),
            text: labelText(child), ...(style ? { style } : {}),
            ...(pieces.length > 1 ? { items: pieces.map((p) => ({ tag: p.tagName, box: box(p), text: labelText(p) })) } : {}) });
        } else if (child.matches('rect, path, line, circle, ellipse, polygon')) {
          result.push({ tag: child.tagName, class: child.getAttribute('class'), group, box: box(child) });
        } else if (child.tagName === 'g') {
          walk(child, group ?? child.getAttribute('class'));
        }
      }
    };
    walk(node, null);
    return result;
  };

  const nodes = [];
  for (const node of svg.querySelectorAll('g.node')) {
    const m = matrix(node);
    const nodeId = withoutPrefix(node.id);
    const entry = { id: nodeId, shape: shapes[nodeId] ?? null, x: round(m.e), y: round(m.f), box: box(node) };
    if (extended) {
      entry.class = node.getAttribute('class');
      entry.parts = partsOf(node);
    }
    nodes.push(entry);
  }

  // Clusters: flowchart subgraphs, class namespaces and ER subgraphs (g.cluster), composite states and the regions of a
  // concurrent state (g.statediagram-cluster: the outer or divider rectangle) and the invisible groups that hold a
  // state with its note (g.note-cluster)
  const clusters = [];
  for (const cluster of svg.querySelectorAll('g.cluster, g.statediagram-cluster, g.note-cluster')) {
    const rect = cluster.matches('.statediagram-cluster')
      ? cluster.querySelector('rect.outer, rect.divider')
      : cluster.querySelector(':scope > rect') ?? cluster.querySelector('rect, path');
    if (rect) {
      const clusterId = withoutPrefix(cluster.id);
      const entry = rich
        ? { id: clusterId, shape: groupShapes[clusterId] ?? null, box: box(rect) }
        : { id: clusterId, box: box(rect) };
      if (extended) {
        const inner = cluster.querySelector(':scope > rect.inner');
        const label = cluster.querySelector('.cluster-label');
        entry.class = cluster.getAttribute('class');
        entry.inner = inner ? box(inner) : null;
        entry.label = label ? { text: labelText(label), box: box(label) } : null;
      }
      clusters.push(entry);
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

    edges.push(extended
      ? { id: edgeId, class: path.getAttribute('class'), markerStart: marker(path, 'marker-start'),
        markerEnd: marker(path, 'marker-end'), length: round(length), points }
      : { id: edgeId, length: round(length), points });
  }

  const edgeLabels = [];
  for (const label of svg.querySelectorAll('g.edgeLabel .label')) {
    const b = box(label);
    if (b[2] > 0 && b[3] > 0) {
      edgeLabels.push({ id: label.getAttribute('data-id'), box: b });
    }
  }

  const viewBox = (svg.dataset.layoutViewBox ?? svg.getAttribute('viewBox')).trim().split(/[\s,]+/).map(Number).map(round);
  if (!extended) {
    return { viewBox, nodes, clusters, edges, edgeLabels };
  }

  // Terminal labels (cardinalities), in document order, with their position (the terminal group's translation). Mermaid
  // inserts them concurrently, so their order does not name their edge: each is matched to the nearest unmatched
  // route end whose terminal text (from the diagram's data) is the same, and records that edge and end.
  const terminals = Array.from(svg.querySelectorAll('g.edgeTerminals'), (element) => {
    const m = matrix(element);
    return { edge: null, end: null, text: textOf(element), x: round(m.e), y: round(m.f), box: box(element) };
  });
  const candidates = [];
  for (const edge of edges) {
    const texts = terminalTexts?.[edge.id];
    if (!texts) {
      continue;
    }
    [[edge.points[0], 'start', texts[0]], [edge.points[edge.points.length - 1], 'end', texts[1]]].forEach(([p, end, t]) => {
      terminals.forEach((terminal, i) => {
        if (t && terminal.text === t) {
          candidates.push({ i, edge: edge.id, end, distance: Math.hypot(terminal.x - p[0], terminal.y - p[1]) });
        }
      });
    });
  }
  candidates.sort((a, b) => a.distance - b.distance);
  const taken = new Set();
  for (const c of candidates) {
    if (terminals[c.i].edge === null && !taken.has(`${c.edge}:${c.end}`)) {
      terminals[c.i].edge = c.edge;
      terminals[c.i].end = c.end;
      taken.add(`${c.edge}:${c.end}`);
    }
  }

  const titleElement = svg.querySelector('text[class$="TitleText"]');
  const title = titleElement ? { class: titleElement.getAttribute('class'), text: textOf(titleElement), box: box(titleElement) } : null;
  return { diagram: role, viewBox, title, nodes, clusters, edges, edgeLabels, terminals };
}

// Runs in the page: the layout of a sequence diagram. actors is the database's actor list (name, type, description)
// in declaration order; each actor gets its top and bottom drawing (null when not drawn) and its lifeline.
function extractSequence(id, count, actors) {
  const svg = document.getElementById(id);
  const toSvg = svg.getScreenCTM().inverse();
  const round = (v) => Math.round(v * 1000) / 1000 + 0;
  const withoutPrefix = (text) => (text && text.startsWith(`${id}-`) ? text.slice(id.length + 1) : text);
  const matrix = (element) => toSvg.multiply(element.getScreenCTM());
  const attr = (element, name) => Number(element.getAttribute(name) ?? 0);
  const point = (element, x, y) => {
    const p = new DOMPoint(x, y).matrixTransform(matrix(element));
    return [round(p.x), round(p.y)];
  };
  const box = (element) => {
    const b = element.getBBox();
    const m = matrix(element);
    const a = new DOMPoint(b.x, b.y).matrixTransform(m);
    const c = new DOMPoint(b.x + b.width, b.y + b.height).matrixTransform(m);
    return [Math.min(a.x, c.x), Math.min(a.y, c.y), Math.abs(c.x - a.x), Math.abs(c.y - a.y)].map(round);
  };
  const rendered = (element) => !element.closest('[display="none"]') && getComputedStyle(element).display !== 'none';
  const marker = (element, name) => {
    const match = /url\(#(.+)\)/.exec(element.getAttribute(name) ?? '');
    return match ? withoutPrefix(match[1]) : null;
  };

  // The computed paint (CSS beats the presentation attributes): fill (not for a line), stroke, stroke width, and the
  // dash pattern and filter when set.
  const paint = (element) => {
    const s = getComputedStyle(element);
    const result = element.tagName === 'line' ? {} : { fill: s.fill };
    result.stroke = s.stroke;
    result.strokeWidth = round(parseFloat(s.strokeWidth));
    if (s.strokeDasharray && s.strokeDasharray !== 'none') {
      result.dash = s.strokeDasharray;
    }
    if (s.filter && s.filter !== 'none') {
      result.filter = s.filter;
    }
    return result;
  };
  const line = (element) => [...point(element, attr(element, 'x1'), attr(element, 'y1')),
    ...point(element, attr(element, 'x2'), attr(element, 'y2'))];
  // A text: its lines (one per tspan), its x and y, and the computed style of its first tspan (where the theme's text
  // color is set) or of the text itself
  const text = (element) => {
    const spans = element.querySelectorAll('tspan');
    const s = getComputedStyle(spans[0] ?? element);
    const result = {
      class: element.getAttribute('class'),
      lines: spans.length ? Array.from(spans, (t) => t.textContent) : [element.textContent],
      at: point(element, attr(element, 'x'), attr(element, 'y')),
      anchor: s.textAnchor,
      baseline: element.getAttribute('dominant-baseline'),
      fontSize: round(parseFloat(s.fontSize)),
      fontWeight: s.fontWeight,
      fill: s.fill,
    };
    if (rendered(element)) {
      result.box = box(element);
    }
    return result;
  };
  const texts = (element) => Array.from(element.querySelectorAll('text'), text);

  // A shape part of a drawing (not a text, not a marker definition)
  const part = (element) => {
    const result = { tag: element.tagName, box: box(element), paint: paint(element) };
    for (const name of ['marker-start', 'marker-end']) {
      const value = marker(element, name);
      if (value) {
        result[name === 'marker-start' ? 'markerStart' : 'markerEnd'] = value;
      }
    }
    return result;
  };
  const parts = (group) => Array.from(group.querySelectorAll('rect, path, circle, ellipse, line, polygon'))
    .filter((e) => !e.closest('defs, marker'))
    .map(part);
  const drawing = (group) => ({ box: box(group), parts: parts(group), texts: texts(group) });

  // The paint order: each recognized item with its element, sorted by document position at the end
  const order = [];

  // Lifelines, then the top drawings (data-et participant), then the bottom drawings (class actor-bottom: the
  // drawing's group, named by its name attribute or by its label's x, which is the lifeline's x)
  const lifelines = new Map();
  for (const element of svg.querySelectorAll('line[data-et="life-line"]')) {
    const name = element.getAttribute('data-id');
    lifelines.set(name, element);
    order.push([`lifeline:${name}`, element]);
  }
  const tops = new Map();
  for (const element of svg.querySelectorAll('[data-et="participant"]')) {
    const name = element.getAttribute('data-id');
    tops.set(name, element);
    order.push([`actor-top:${name}`, element]);
  }
  const bottoms = new Map();
  for (const element of svg.querySelectorAll('.actor-bottom')) {
    const group = element.tagName === 'g' && element.querySelector('text') ? element : element.parentElement;
    if ([...bottoms.values()].includes(group)) {
      continue;
    }
    let name = element.getAttribute('name') ?? group.getAttribute('name');
    if (!name) {
      const label = group.querySelector('text');
      const x = label ? point(label, attr(label, 'x'), 0)[0] : NaN;
      name = [...lifelines].find(([, l]) => Math.abs(line(l)[0] - x) < 0.01)?.[0] ?? null;
    }
    bottoms.set(name, group);
    order.push([`actor-bottom:${name}`, group]);
  }
  const actorList = actors.map((a) => ({
    name: a.name,
    type: a.type,
    description: a.description,
    top: tops.has(a.name) ? drawing(tops.get(a.name)) : null,
    bottom: bottoms.has(a.name) ? drawing(bottoms.get(a.name)) : null,
    lifeline: lifelines.has(a.name)
      ? { line: line(lifelines.get(a.name)), paint: paint(lifelines.get(a.name)) }
      : null,
  }));

  // Messages (data-et message) with the texts drawn just before them, and the sequence number and the
  // central-connection circles drawn just after them; all are children of the svg element
  const messages = [];
  let pending = [];
  let current = null;
  for (const child of svg.children) {
    if (child.matches('text.messageText')) {
      pending.push(text(child));
    } else if (child.matches('[data-et="message"]')) {
      const dataId = child.getAttribute('data-id');
      current = {
        id: dataId,
        from: child.getAttribute('data-from'),
        to: child.getAttribute('data-to'),
        class: child.getAttribute('class'),
        tag: child.tagName,
        markerStart: marker(child, 'marker-start'),
        markerEnd: marker(child, 'marker-end'),
        paint: paint(child),
        texts: pending,
        number: null,
        circles: [],
      };
      if (child.tagName === 'line') {
        current.line = line(child);
      } else {
        const m = matrix(child);
        const length = child.getTotalLength();
        current.d = child.getAttribute('d');
        current.length = round(length);
        current.points = [];
        for (let i = 0; i <= count; i++) {
          const p = child.getPointAtLength((length * i) / count).matrixTransform(m);
          current.points.push([round(p.x), round(p.y)]);
        }
      }
      pending = [];
      messages.push(current);
      order.push([`message:${dataId}`, child]);
    } else if (current && child.matches('line') && marker(child, 'marker-start') === 'sequencenumber') {
      current.number = { line: line(child), paint: paint(child), text: null };
      order.push([`number:${current.id}`, child]);
    } else if (current && current.number && child.matches('text.sequenceNumber')) {
      current.number.text = text(child);
    } else if (current && child.matches('g') && !child.hasAttribute('class') && !child.hasAttribute('data-et') &&
      child.children.length > 0 && Array.from(child.children).every((c) => c.tagName === 'circle')) {
      for (const circle of child.children) {
        current.circles.push({ center: point(circle, attr(circle, 'cx'), attr(circle, 'cy')), r: attr(circle, 'r'),
          paint: paint(circle) });
      }
      order.push([`circles:${current.id}`, child]);
    }
  }

  const notes = Array.from(svg.querySelectorAll('g[data-et="note"]'), (g) => {
    const rect = g.querySelector('rect');
    order.push([`note:${g.getAttribute('data-id')}`, g]);
    return { id: g.getAttribute('data-id'), box: box(rect), paint: paint(rect), texts: texts(g) };
  });

  const activations = Array.from(svg.querySelectorAll('rect[class^="activation"]'), (rect, i) => {
    order.push([`activation:${i}`, rect]);
    return { class: rect.getAttribute('class'), box: box(rect), paint: paint(rect) };
  });

  const frames = Array.from(svg.querySelectorAll('g[data-et="control-structure"]'), (g) => {
    order.push([`frame:${g.getAttribute('data-id')}`, g]);
    const polygon = g.querySelector('polygon');
    const points = polygon
      ? Array.from(polygon.points, (p) => point(polygon, p.x, p.y))
      : null;
    return {
      id: g.getAttribute('data-id'),
      lines: Array.from(g.querySelectorAll('line'), (l) => ({ line: line(l), paint: paint(l) })),
      labelBox: polygon ? { points, paint: paint(polygon) } : null,
      texts: texts(g),
    };
  });

  // Background rects of rect blocks are children of the svg element; boxes are rects in a group with their title
  const rects = Array.from(svg.querySelectorAll(':scope > rect.rect'), (rect, i) => {
    order.push([`rect:${i}`, rect]);
    return { box: box(rect), paint: paint(rect) };
  });
  const boxes = Array.from(svg.querySelectorAll(':scope > g > rect.rect'), (rect, i) => {
    order.push([`box:${i}`, rect.parentElement]);
    return { box: box(rect), paint: paint(rect), texts: texts(rect.parentElement) };
  });

  // Popup menus are drawn hidden (display none) unless forceMenus is set: the panel is given by its attributes
  const menus = Array.from(svg.querySelectorAll('g.actorPopupMenu'), (g) => {
    order.push([`menu:${g.id}`, g]);
    const panel = g.querySelector('rect');
    const displayed = rendered(g);
    return {
      id: g.id,
      displayed,
      panel: panel
        ? [...point(panel, attr(panel, 'x'), attr(panel, 'y')), attr(panel, 'width'), attr(panel, 'height')].map(round)
        : null,
      ...(panel && displayed ? { paint: paint(panel) } : {}),
      texts: texts(g),
    };
  });

  const titleElement = Array.from(svg.querySelectorAll(':scope > text')).find((t) => !t.hasAttribute('class'));
  if (titleElement) {
    order.push(['title', titleElement]);
  }

  order.sort(([, a], [, b]) => (a === b ? 0 : a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING ? -1 : 1));
  const viewBox = svg.getAttribute('viewBox').trim().split(/[\s,]+/).map(Number).map(round);
  return {
    diagram: 'sequence',
    viewBox,
    title: titleElement ? text(titleElement) : null,
    actors: actorList,
    messages,
    notes,
    activations,
    frames,
    rects,
    boxes,
    menus,
    order: order.map(([key]) => key),
  };
}

// Runs in the page: the layout of a gantt chart (ganttRenderer.js). The svg holds, in order, an empty group, the group
// of the excluded days, the bottom axis (and the top axis), the group of the section rows, the group of the tasks and
// their texts, the group of the section titles, the today line and the title.
function extractGantt(id) {
  const svg = document.getElementById(id);
  const toSvg = svg.getScreenCTM().inverse();
  const round = (v) => Math.round(v * 1000) / 1000 + 0;
  const matrix = (element) => toSvg.multiply(element.getScreenCTM());
  const attr = (element, name) => Number(element.getAttribute(name) ?? 0);
  const point = (element, x, y) => {
    const p = new DOMPoint(x, y).matrixTransform(matrix(element));
    return [round(p.x), round(p.y)];
  };
  const box = (element) => {
    const b = element.getBBox();
    const m = matrix(element);
    const corners = [[b.x, b.y], [b.x + b.width, b.y], [b.x, b.y + b.height], [b.x + b.width, b.y + b.height]]
      .map(([x, y]) => new DOMPoint(x, y).matrixTransform(m));
    const xs = corners.map((c) => c.x);
    const ys = corners.map((c) => c.y);
    return [Math.min(...xs), Math.min(...ys), Math.max(...xs) - Math.min(...xs), Math.max(...ys) - Math.min(...ys)].map(round);
  };
  // The computed paint (CSS beats the presentation attributes), with the element opacity
  const paint = (element) => {
    const s = getComputedStyle(element);
    return { fill: s.fill, stroke: s.stroke, strokeWidth: round(parseFloat(s.strokeWidth)), opacity: round(parseFloat(s.opacity)) };
  };
  // A text: its content, the point of its anchor, its computed anchor, font and fill, and its box
  const text = (element) => {
    const s = getComputedStyle(element);
    return {
      text: element.textContent,
      at: point(element, attr(element, 'x'), attr(element, 'y')),
      dy: element.getAttribute('dy'),
      anchor: s.textAnchor,
      fontSize: round(parseFloat(s.fontSize)),
      fontWeight: s.fontWeight,
      fontStyle: s.fontStyle,
      fill: s.fill,
      box: element.textContent.length > 0 ? box(element) : null
    };
  };
  // The class of an element without the measured width that the task texts carry (width-<number>)
  const classOf = (element) => (element.getAttribute('class') ?? '').split(/\s+/).filter((c) => c && !c.startsWith('width-')).join(' ');
  // The corners of a rectangle after its transforms (a milestone is rotated and scaled by CSS)
  const corners = (rect) => {
    const [x, y, w, h] = ['x', 'y', 'width', 'height'].map((n) => attr(rect, n));
    return [[x, y], [x + w, y], [x + w, y + h], [x, y + h]].map(([px, py]) => point(rect, px, py));
  };

  const groups = Array.from(svg.children).filter((c) => c.tagName === 'g');
  const axes = groups.filter((g) => g.classList.contains('grid'));
  const excludeGroup = groups.find((g) => g.querySelector(':scope > rect.exclude-range'));
  const sectionGroup = groups.find((g) => g.querySelector(':scope > rect.section'));
  const taskGroup = groups.find((g) => g.querySelector(':scope > rect.task'));
  const titleGroup = groups.find((g) => g.querySelector(':scope > text.sectionTitle'));
  const todayLine = svg.querySelector(':scope > g.today > line');
  const title = svg.querySelector(':scope > text.titleText');
  const viewBox = svg.getAttribute('data-layout-view-box') ?? svg.dataset.layoutViewBox ?? svg.getAttribute('viewBox');
  return {
    diagram: 'gantt',
    viewBox: viewBox.trim().split(/[\s,]+/).map(Number).map(round),
    excludes: Array.from(excludeGroup?.querySelectorAll(':scope > rect') ?? [], (rect) => ({
      id: rect.id.replace(`${id}-`, ''),
      corners: corners(rect),
      paint: paint(rect)
    })),
    axes: axes.map((g) => ({
      origin: point(g, 0, 0),
      ticks: Array.from(g.querySelectorAll(':scope > g.tick'), (tick) => {
        const line = tick.querySelector('line');
        return {
          at: point(tick, 0, 0),
          end: point(line, 0, attr(line, 'y2')),
          tick: { opacity: round(parseFloat(getComputedStyle(tick).opacity)) },
          line: paint(line),
          text: text(tick.querySelector('text'))
        };
      })
    })),
    sections: Array.from(sectionGroup?.querySelectorAll(':scope > rect') ?? [], (rect) => ({
      class: classOf(rect),
      corners: corners(rect),
      paint: paint(rect)
    })),
    tasks: Array.from(taskGroup?.querySelectorAll(':scope > rect') ?? [], (rect) => ({
      id: rect.id.replace(`${id}-`, ''),
      class: classOf(rect),
      rx: attr(rect, 'rx'),
      corners: corners(rect),
      paint: paint(rect)
    })),
    texts: Array.from(taskGroup?.querySelectorAll(':scope > text') ?? [], (element) => ({
      id: element.id.replace(`${id}-`, ''),
      class: classOf(element),
      ...text(element)
    })),
    sectionTitles: Array.from(titleGroup?.querySelectorAll(':scope > text') ?? [], (element) => ({
      class: classOf(element),
      fill: getComputedStyle(element).fill,
      fontSize: round(parseFloat(getComputedStyle(element).fontSize)),
      lines: Array.from(element.querySelectorAll('tspan'), (tspan) => ({
        text: tspan.textContent,
        box: tspan.textContent.length > 0 ? box(tspan) : null
      }))
    })),
    today: todayLine
      ? { start: point(todayLine, attr(todayLine, 'x1'), attr(todayLine, 'y1')), end: point(todayLine, attr(todayLine, 'x2'), attr(todayLine, 'y2')), paint: paint(todayLine) }
      : null,
    title: title ? text(title) : null
  };
}

// Runs in the page: the layout of a pie chart (pieRenderer.ts). The svg holds a group at the pie's center, which holds
// the pie group (the outer circle, the slices and their percentage labels), the title and the legend groups.
function extractPie(id) {
  const svg = document.getElementById(id);
  const toSvg = svg.getScreenCTM().inverse();
  const round = (v) => Math.round(v * 1000) / 1000 + 0;
  const matrix = (element) => toSvg.multiply(element.getScreenCTM());
  const attr = (element, name) => Number(element.getAttribute(name) ?? 0);
  const point = (element, x, y) => {
    const p = new DOMPoint(x, y).matrixTransform(matrix(element));
    return [round(p.x), round(p.y)];
  };
  const box = (element) => {
    const b = element.getBBox();
    const m = matrix(element);
    const a = new DOMPoint(b.x, b.y).matrixTransform(m);
    const c = new DOMPoint(b.x + b.width, b.y + b.height).matrixTransform(m);
    return [Math.min(a.x, c.x), Math.min(a.y, c.y), Math.abs(c.x - a.x), Math.abs(c.y - a.y)].map(round);
  };
  // The computed paint (CSS beats the presentation attributes), with the element opacity and the CSS scale
  const paint = (element) => {
    const s = getComputedStyle(element);
    return {
      fill: s.fill,
      stroke: s.stroke,
      strokeWidth: round(parseFloat(s.strokeWidth)),
      opacity: round(parseFloat(s.opacity)),
      scale: s.scale
    };
  };
  // A text: its content, the point of its anchor, its computed anchor, font and fill, and its box
  const text = (element) => {
    const s = getComputedStyle(element);
    return {
      class: element.getAttribute('class'),
      text: element.textContent,
      at: point(element, attr(element, 'x'), attr(element, 'y')),
      anchor: s.textAnchor,
      fontSize: round(parseFloat(s.fontSize)),
      fontWeight: s.fontWeight,
      fill: s.fill,
      box: box(element)
    };
  };

  // mermaid's render leaves an empty group before the chart's group
  const center = svg.querySelector(':scope > g[transform]');
  const pie = center.querySelector(':scope > g:not(.legend)');
  const circle = pie.querySelector(':scope > circle');
  const title = center.querySelector(':scope > text');
  const viewBox = svg.getAttribute('data-layout-view-box') ?? svg.dataset.layoutViewBox ?? svg.getAttribute('viewBox');
  return {
    diagram: 'pie',
    viewBox: viewBox.trim().split(/[\s,]+/).map(Number).map(round),
    origin: point(pie, 0, 0),
    outerCircle: {
      center: point(circle, attr(circle, 'cx'), attr(circle, 'cy')),
      r: round(attr(circle, 'r')),
      paint: paint(circle)
    },
    slices: Array.from(pie.querySelectorAll(':scope > path'), (path) => ({
      class: path.getAttribute('class'),
      d: path.getAttribute('d'),
      box: box(path),
      paint: paint(path)
    })),
    labels: Array.from(pie.querySelectorAll(':scope > text'), text),
    title: title ? text(title) : null,
    legend: Array.from(center.querySelectorAll(':scope > g.legend'), (g) => {
      const rect = g.querySelector('rect');
      return {
        at: point(g, 0, 0),
        rect: { box: box(rect), paint: paint(rect) },
        text: text(g.querySelector('text'))
      };
    })
  };
}