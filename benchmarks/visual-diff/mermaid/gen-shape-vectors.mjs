// gen-shape-vectors.mjs - records what the pinned mermaid's unified renderer makes of single flowchart nodes: the size
// of every node shape (the bounding box dagre is given), where the label goes, how big the label is, the geometry and
// computed paint of every drawn element, and the intersection points of the shape's intersect function, as the upstream
// vectors of the CanvasNet.Mermaid label builder and shape library tests.
//
// Developer tooling of benchmarks/visual-diff; see the README there. Usage:
//
//     node gen-shape-vectors.mjs [--modules <folder>] [--out <file>]
//     node gen-shape-vectors.mjs [--modules <folder>] --probe <file>
//
// <modules> is a folder that holds node_modules/mermaid and node_modules/puppeteer (default: the repository root). <out>
// is the vector file (default: test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/shapes.json).
// --probe runs the texts of a JSON file (an array of strings) and prints the results instead (a development aid).
//
// The cases run the real mermaid bundle in headless Chrome. The text is measured with the bundled Liberation Sans faces
// (src/DemaConsulting.CanvasNet/Fonts/BundledFonts), registered with @font-face under the names of Mermaid's default
// font stack, so the vectors do not depend on the fonts of the machine. A layout algorithm named "probe" is registered
// with mermaid.registerLayoutLoaders: mermaid hands it the LayoutData of the flowchart, and it inserts every node with
// mermaid's own insertNode (the step insertMeasuredNode of the dagre layout does), then records the facts. Nothing is
// downloaded; the inputs are original.

import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { deflateSync } from 'node:zlib';

// The repository root is three folders above benchmarks/visual-diff/mermaid
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

// Read the optional arguments
const args = process.argv.slice(2);
let modules = root;
let out = join(root, 'test', 'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'upstream-vectors', 'shapes.json');
let probe = null;
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--modules' && i + 1 < args.length) {
    modules = resolve(args[++i]);
  } else if (args[i] === '--out' && i + 1 < args.length) {
    out = resolve(args[++i]);
  } else if (args[i] === '--probe' && i + 1 < args.length) {
    probe = resolve(args[++i]);
  } else {
    console.error('usage: node gen-shape-vectors.mjs [--modules <folder>] [--out <file> | --probe <file>]');
    process.exit(2);
  }
}

const resolver = createRequire(join(modules, 'noop.js'));
const mermaidFolder = join(modules, 'node_modules', 'mermaid');
const mermaidVersion = JSON.parse(readFileSync(join(mermaidFolder, 'package.json'), 'utf8')).version;
const puppeteer = (await import(pathToFileURL(resolver.resolve('puppeteer')).href)).default;

// The bundled faces, registered under the family names of mermaid's default stack
const fonts = join(root, 'src', 'DemaConsulting.CanvasNet', 'Fonts', 'BundledFonts');
const faces = [
  ['LiberationSans-Regular.ttf', 'normal', 'normal'],
  ['LiberationSans-Bold.ttf', 'bold', 'normal'],
  ['LiberationSans-Italic.ttf', 'normal', 'italic'],
  ['LiberationSans-BoldItalic.ttf', 'bold', 'italic']
];
const fontCss = ['trebuchet ms', 'verdana', 'arial', 'Liberation Sans']
  .flatMap((family) =>
    faces.map(
      ([file, weight, style]) =>
        `@font-face { font-family: '${family}'; src: url(data:font/ttf;base64,${readFileSync(join(fonts, file)).toString('base64')}) format('truetype'); font-weight: ${weight}; font-style: ${style}; }`
    )
  )
  .join('\n');

// A small PNG of a given size (one color), so image nodes have a known natural size
const crcTable = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) {
    c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  }
  return c >>> 0;
});
const crc32 = (buffer) => {
  let c = 0xffffffff;
  for (const byte of buffer) {
    c = crcTable[(c ^ byte) & 0xff] ^ (c >>> 8);
  }
  return (c ^ 0xffffffff) >>> 0;
};
const chunk = (type, data) => {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body));
  return Buffer.concat([length, body, crc]);
};
const png = (width, height) => {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0);
  header.writeUInt32BE(height, 4);
  header[8] = 8;
  header[9] = 2;
  const rows = Buffer.alloc((width * 3 + 1) * height);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      rows.set([200, 80, 40], y * (width * 3 + 1) + 1 + x * 3);
    }
  }
  const data = Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(rows)),
    chunk('IEND', Buffer.alloc(0))
  ]);
  return 'data:image/png;base64,' + data.toString('base64');
};

// The images of the image cases: the href the vectors name and the data the browser loads
const images = { 'img-40x30.png': png(40, 30), 'img-120x60.png': png(120, 60) };

// ------------------------------------------------------------------------------------------------------------ cases

const shapeNames = [
  'rect', 'rounded', 'stadium', 'fr-rect', 'cyl', 'datastore', 'folder', 'bucket', 'console', 'browser', 'person',
  'circle', 'bang', 'cloud', 'diam', 'hex', 'lean-r', 'lean-l', 'trap-b', 'trap-t', 'dbl-circ', 'text', 'notch-rect',
  'lin-rect', 'sm-circ', 'fr-circ', 'fork', 'hourglass', 'brace', 'brace-r', 'braces', 'bolt', 'doc', 'delay', 'h-cyl',
  'lin-cyl', 'curv-trap', 'div-rect', 'tri', 'win-pane', 'f-circ', 'notch-pent', 'flip-tri', 'sl-rect', 'docs',
  'st-rect', 'bow-rect', 'cross-circ', 'tag-doc', 'tag-rect', 'flag', 'odd', 'lin-doc',
  // undocumented names the flowchart accepts
  'state', 'choice', 'note', 'composite', 'anchor'
];
const short = 'Shape';
const long = 'A much longer label that has to wrap onto lines';
const html = { htmlLabels: true };
const svg = { htmlLabels: false };

const node = (shape, label) => `flowchart TD\n  n0@{ shape: ${shape}, label: ${JSON.stringify(label)} }`;
const cases = [];

// Every shape with a short and a wrapping label, as HTML labels; and with a short label as SVG labels
for (const shape of shapeNames) {
  cases.push({ name: `shape-${shape}-short`, config: html, text: node(shape, short) });
  cases.push({ name: `shape-${shape}-long`, config: html, text: node(shape, long) });
  cases.push({ name: `shape-${shape}-svg`, config: svg, text: node(shape, short) });
}

// The bracket shapes of the flowchart grammar
const brackets = [
  ['square', 'n0[Shape]'], ['round', 'n0(Shape)'], ['stadium', 'n0([Shape])'], ['subroutine', 'n0[[Shape]]'],
  ['cylinder', 'n0[(Shape)]'], ['circle', 'n0((Shape))'], ['odd', 'n0>Shape]'], ['diamond', 'n0{Shape}'],
  ['hexagon', 'n0{{Shape}}'], ['lean-right', 'n0[/Shape/]'], ['lean-left', 'n0[\\Shape\\]'],
  ['trapezoid', 'n0[/Shape\\]'], ['inv-trapezoid', 'n0[\\Shape/]'], ['double-circle', 'n0(((Shape)))']
];
for (const [name, text] of brackets) {
  cases.push({ name: `bracket-${name}`, config: html, text: `flowchart TD\n  ${text}` });
}

// Labels: the HTML-label and SVG-label rules on a plain rectangle
const labels = [
  ['plain', 'n0["Hello World"]'],
  ['empty', 'n0[" "]'],
  ['single', 'n0[A]'],
  ['digits', 'n0[0123456789]'],
  ['kerning', 'n0["AVAWAY To Ty Yo LT"]'],
  ['spaces', 'n0["a   b    c"]'],
  ['bold', 'n0["<b>bold</b> text"]'],
  ['strong-em', 'n0["<strong>strong</strong> and <em>em</em> and <i>i</i>"]'],
  ['break', 'n0["one<br>two<br/>three"]'],
  ['break-long', 'n0["first line<br>a second line that is much longer than the wrapping width of mermaid"]'],
  ['newline', 'n0["one\\ntwo"]'],
  ['entities', 'n0["a &amp; b &lt; c #quot; d #35; e"]'],
  ['entity-named', 'n0["caf&eacute; &copy; &rarr;"]'],
  ['unicode', 'n0["Größe ÄÖÜ ß éè ñ"]'],
  ['span-style', 'n0["<span style=\'color:red\'>red</span> plain"]'],
  ['ignored-tag', 'n0["<u>under</u> <sup>sup</sup> <code>code</code>"]'],
  ['icon', 'n0["fa:fa-car Car"]'],
  ['wrap', 'n0["The quick brown fox jumps over the lazy dog and keeps on running far away"]'],
  ['wrap-exact', 'n0["aaaa bbbb cccc dddd eeee ffff gggg hhhh iiii jjjj kkkk"]'],
  ['long-word', 'n0["Supercalifragilisticexpialidocious-and-even-longer-word-here"]'],
  ['long-word-mixed', 'n0["short Supercalifragilisticexpialidocious-and-even-longer-word-here end"]'],
  ['markdown', 'n0["`**Bold** and *italic* text`"]'],
  ['markdown-lines', 'n0["`Line one\nLine **two**`"]'],
  ['markdown-wrap', 'n0["`This **markdown** label is long enough that it must wrap at the wrapping width`"]'],
  ['markdown-escape', 'n0["`a \\*not em\\* b`"]'],
  ['markdown-html', 'n0["`a <b>b</b> c`"]'],
  ['markdown-list', 'n0["`- item one`"]'],
  ['markdown-heading', 'n0["`# Heading`"]'],
  ['markdown-underscore', 'n0["`__bold__ _em_`"]'],
  ['markdown-nested', 'n0["`***both*** **a *b* c**`"]']
];
for (const [name, text] of labels) {
  cases.push({ name: `label-${name}-html`, config: html, text: `flowchart TD\n  ${text}` });
  cases.push({ name: `label-${name}-svg`, config: svg, text: `flowchart TD\n  ${text}` });
}

// Font sizes and families of the configuration and of classDef styles
cases.push({ name: 'label-font-size-24', config: { ...html, fontSize: 24, themeVariables: { fontSize: '24px' } }, text: 'flowchart TD\n  n0["Hello World"]' });
cases.push({ name: 'label-class-font', config: html, text: 'flowchart TD\n  n0["Hello World"]:::big\n  classDef big font-size:20px,font-weight:bold,color:#f00' });
cases.push({ name: 'label-class-font-svg', config: svg, text: 'flowchart TD\n  n0["Hello World"]:::big\n  classDef big font-size:20px,font-weight:bold,color:#f00' });
cases.push({ name: 'label-wrapping-width', config: { ...html, flowchart: { wrappingWidth: 100 } }, text: 'flowchart TD\n  n0["The quick brown fox jumps over"]' });
cases.push({ name: 'label-padding', config: { ...html, flowchart: { padding: 4 } }, text: 'flowchart TD\n  n0["Padding"]' });

// Node styles: the paint of every element under classDef and style
cases.push({ name: 'style-rect', config: html, text: 'flowchart TD\n  n0[Styled]\n  style n0 fill:#f9f,stroke:#333,stroke-width:4px,stroke-dasharray:5 5' });
cases.push({ name: 'style-class-circle', config: html, text: 'flowchart TD\n  n0((Styled)):::c\n  classDef c fill:#9f9,stroke:#060' });
for (const shape of ['stadium', 'cyl', 'dbl-circ', 'f-circ', 'fork', 'fr-circ', 'brace', 'tag-rect', 'browser', 'note', 'lin-cyl']) {
  cases.push({ name: `style-${shape}`, config: html, text: `flowchart TD\n  n0@{ shape: ${shape}, label: "S" }\n  style n0 fill:#f9f,stroke:#333,stroke-width:3px` });
}

// Directions that change shapes
cases.push({ name: 'fork-lr', config: html, text: 'flowchart LR\n  n0@{ shape: fork }' });

// Images: natural sizes, given sizes, the aspect constraint and the label position
const img = (attrs) => `flowchart TD\n  n0@{ ${attrs} }`;
cases.push({ name: 'image-natural', config: html, text: img('img: "img-40x30.png", label: "Pic"') });
cases.push({ name: 'image-no-label', config: html, text: img('img: "img-40x30.png", label: ""') });
cases.push({ name: 'image-top', config: html, text: img('img: "img-120x60.png", label: "Top label", pos: "t"') });
cases.push({ name: 'image-size', config: html, text: img('img: "img-120x60.png", label: "Sized", w: 240, h: 50') });
cases.push({ name: 'image-constraint', config: html, text: img('img: "img-120x60.png", label: "Kept", h: 90, constraint: "on"') });
cases.push({ name: 'image-constraint-width', config: html, text: img('img: "img-40x30.png", label: "", w: 300, constraint: "on"') });
cases.push({ name: 'image-long-label', config: html, text: img(`img: "img-40x30.png", label: "${long} and more words still"`) });
cases.push({ name: 'image-svg', config: svg, text: img('img: "img-40x30.png", label: "Pic"') });

// Icons: the shapes keep their geometry (the icon itself is not registered)
for (const form of ['square', 'circle', 'rounded', '']) {
  const formAttr = form ? `, form: ${form}` : '';
  cases.push({ name: `icon-${form || 'none'}`, config: html, text: img(`icon: "fa:car", label: "Car"${formAttr}`) });
  cases.push({ name: `icon-${form || 'none'}-top`, config: html, text: img(`icon: "fa:car", label: "Car", pos: t${formAttr}`) });
}
cases.push({ name: 'icon-size', config: html, text: img('icon: "fa:car", label: "", form: square, w: 64, h: 32') });

// Labels with images
cases.push({ name: 'label-img-only', config: html, text: 'flowchart TD\n  n0["<img src=\'img-40x30.png\'>"]' });
cases.push({ name: 'label-img-size', config: html, text: 'flowchart TD\n  n0["<img src=\'img-40x30.png\' width=\'60\' height=\'20\'>"]' });

// ------------------------------------------------------------------------------------------------------------ probe

// The page-side recorder: registered once, it records the nodes of the next render into window.__probe
const installProbe = async (page) =>
  page.evaluate(() => {
    const round = (v) => (typeof v === 'number' ? v : v);
    const rect = (r) => ({ x: round(r.x), y: round(r.y), width: round(r.width), height: round(r.height) });
    const relativeBox = (el, group) => {
      // The bbox of an element in the coordinates of the node group
      const box = el.getBBox();
      const m = group.getCTM().inverse().multiply(el.getCTM());
      const corners = [
        [box.x, box.y], [box.x + box.width, box.y], [box.x, box.y + box.height], [box.x + box.width, box.y + box.height]
      ].map(([x, y]) => ({ x: m.a * x + m.c * y + m.e, y: m.b * x + m.d * y + m.f }));
      const xs = corners.map((p) => p.x);
      const ys = corners.map((p) => p.y);
      return { x: Math.min(...xs), y: Math.min(...ys), width: Math.max(...xs) - Math.min(...xs), height: Math.max(...ys) - Math.min(...ys) };
    };
    const offsetOf = (el, group) => {
      const m = group.getCTM().inverse().multiply(el.getCTM());
      return { x: m.e, y: m.f };
    };
    window.mermaid.registerLayoutLoaders([
      {
        name: 'probe',
        loader: async () => ({
          render: async (data, svg, helpers) => {
            const results = [];
            const group = svg.insert('g').attr('class', 'nodes');
            for (const node of data.nodes) {
              if (node.isGroup) {
                continue;
              }
              const el = await helpers.insertNode(group, node, { config: helpers.getConfig(), dir: node.dir });
              const g = el.node();
              const bbox = g.getBBox();
              node.width = bbox.width;
              node.height = bbox.height;
              node.x = 0;
              node.y = 0;

              // The label: its translation and its measured box
              const labelEl = g.querySelector(':scope > g.label') ?? g.querySelector('g.label');
              let label = null;
              if (labelEl) {
                const fo = labelEl.querySelector('foreignObject');
                const offset = offsetOf(labelEl, g);
                if (fo) {
                  const div = fo.querySelector('div');
                  const divRect = div.getBoundingClientRect();
                  const lines = [];
                  const walker = document.createTreeWalker(div, NodeFilter.SHOW_TEXT);
                  for (let t = walker.nextNode(); t; t = walker.nextNode()) {
                    const range = document.createRange();
                    range.selectNodeContents(t);
                    for (const r of range.getClientRects()) {
                      lines.push({ text: t.textContent, x: r.left - divRect.left, y: r.top - divRect.top, width: r.width, height: r.height });
                    }
                  }
                  const imgs = [...div.querySelectorAll('img')].map((i) => {
                    const r = i.getBoundingClientRect();
                    return { x: r.left - divRect.left, y: r.top - divRect.top, width: r.width, height: r.height };
                  });
                  label = {
                    html: true,
                    offset,
                    box: { x: 0, y: 0, width: Number(fo.getAttribute('width')), height: Number(fo.getAttribute('height')) },
                    inner: div.innerHTML,
                    lines,
                    images: imgs
                  };
                } else {
                  const text = labelEl.querySelector('text');
                  const content = labelEl.querySelector(':scope > g') ?? text;
                  label = {
                    html: false,
                    offset,
                    box: content ? rect(content.getBBox()) : { x: 0, y: 0, width: 0, height: 0 },
                    lines: text
                      ? [...text.querySelectorAll('tspan.text-outer-tspan')].map((t) => ({
                          text: t.textContent,
                          y: t.getAttribute('y'),
                          width: t.getComputedTextLength(),
                          box: rect(t.getBBox())
                        }))
                      : []
                  };
                }
              }

              // The drawn elements with their computed paint
              const parts = [...g.querySelectorAll('path, rect, circle, ellipse, polygon, line, image, text')]
                .filter((p) => !p.closest('foreignObject') && !(p.closest('g.label') && p.tagName !== 'image'))
                .map((p) => {
                  const cs = getComputedStyle(p);
                  return {
                    tag: p.tagName,
                    cls: p.getAttribute('class'),
                    box: relativeBox(p, g),
                    offset: offsetOf(p, g),
                    d: p.getAttribute('d'),
                    points: p.getAttribute('points'),
                    fill: cs.fill,
                    stroke: cs.stroke,
                    strokeWidth: cs.strokeWidth,
                    dash: cs.strokeDasharray,
                    opacity: cs.opacity,
                    fillOpacity: cs.fillOpacity,
                    strokeOpacity: cs.strokeOpacity,
                    href: p.getAttribute('href')
                  };
                });

              // The intersections, from far and from near points around the center
              const intersects = [];
              for (const radius of [400, Math.max(bbox.width, bbox.height) * 0.6]) {
                for (let a = 0; a < 360; a += 15) {
                  const p = { x: radius * Math.cos((a * Math.PI) / 180), y: radius * Math.sin((a * Math.PI) / 180) };
                  const r = node.intersect(p);
                  intersects.push({ p, r: { x: r.x, y: r.y } });
                }
              }

              results.push({ id: node.id, shape: node.shape, width: bbox.width, height: bbox.height, bbox: rect(bbox), label, parts, intersects });
            }
            window.__probe = results;
          }
        })
      }
    ]);
  });

// Runs one text in the page and returns the recorded nodes, or the error
const runCase = async (page, text, config) =>
  page.evaluate(
    async ({ text, config, images }) => {
      // The images are loaded by href: the vectors name them, the browser gets their data
      let source = text;
      for (const [name, data] of Object.entries(images)) {
        source = source.split(name).join(data);
      }
      const front = `---\nconfig: ${JSON.stringify({ ...config, layout: 'probe' })}\n---\n`;
      window.__probe = null;
      try {
        window.mermaid.initialize({ startOnLoad: false });
        await window.mermaid.render('probe' + Math.floor(Math.random() * 1e9), front + source);
      } catch (error) {
        if (!window.__probe) {
          return { error: String(error.message ?? error) };
        }
      }
      // The data URIs go back to the names
      const json = JSON.stringify(window.__probe);
      let named = json;
      for (const [name, data] of Object.entries(images)) {
        named = named.split(data).join(name);
      }
      return JSON.parse(named);
    },
    { text, config: config ?? {}, images }
  );

const browser = await puppeteer.launch({ headless: true });
try {
  const page = await browser.newPage();
  await page.goto('about:blank');
  await page.addStyleTag({ content: fontCss });
  await page.addScriptTag({ path: join(mermaidFolder, 'dist', 'mermaid.min.js') });
  await page.evaluate(async () => {
    for (const style of ['normal', 'italic']) {
      for (const weight of ['normal', 'bold']) {
        await document.fonts.load(`${style} ${weight} 16px "trebuchet ms"`);
        await document.fonts.load(`${style} ${weight} 16px "Liberation Sans"`);
      }
    }
  });
  await installProbe(page);

  if (probe) {
    const texts = JSON.parse(readFileSync(probe, 'utf8'));
    for (const entry of texts) {
      const { text, config } = typeof entry === 'string' ? { text: entry, config: html } : entry;
      console.log(JSON.stringify({ text, result: await runCase(page, text, config) }, null, 1));
    }
  } else {
    const names = new Set();
    const results = [];
    for (const c of cases) {
      if (names.has(c.name)) {
        throw new Error(`duplicate case name ${c.name}`);
      }
      names.add(c.name);
      const result = await runCase(page, c.text, c.config);
      results.push({ name: c.name, config: c.config, text: c.text, expected: result });
    }

    // One case per line keeps the file reviewable and its diffs small
    const source = `Generated by benchmarks/visual-diff/mermaid/gen-shape-vectors.mjs from mermaid ${mermaidVersion} (MIT, Knut Sveidqvist and contributors) in headless Chrome with the bundled Liberation Sans; the inputs are original. See src/DemaConsulting.CanvasNet.Mermaid/third-party/README.md.`;
    const lines = [];
    lines.push('{');
    lines.push(`  "source": ${JSON.stringify(source)},`);
    lines.push(`  "mermaidVersion": ${JSON.stringify(mermaidVersion)},`);
    lines.push(`  "images": ${JSON.stringify({ 'img-40x30.png': [40, 30], 'img-120x60.png': [120, 60] })},`);
    lines.push('  "cases": [');
    lines.push(results.map((c) => '    ' + JSON.stringify(c)).join(',\n'));
    lines.push('  ]');
    lines.push('}');
    mkdirSync(dirname(out), { recursive: true });
    writeFileSync(out, lines.join('\n') + '\n');
    const errors = results.filter((r) => r.expected.error).length;
    console.log(`mermaid ${mermaidVersion}: wrote ${results.length} shape vectors (${errors} errors) to ${out}`);
  }
} finally {
  await browser.close();
}
