// gen-flowchart-vectors.mjs - records what the pinned mermaid's flowchart parser makes of a list of original flowchart
// texts (the flowDb facts and the LayoutData of getData, or the parse error), as the upstream vectors of the
// CanvasNet.Mermaid flowchart parser and model tests.
//
// Developer tooling of tools/visual-diff; see the README there. Usage:
//
//     node gen-flowchart-vectors.mjs [--modules <folder>] [--out <file>]
//     node gen-flowchart-vectors.mjs [--modules <folder>] --probe <file>
//
// <modules> is a folder that holds node_modules/mermaid and node_modules/puppeteer (default: the repository root). <out>
// is the vector file (default: test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/flowchart-parser.json).
// --probe runs the texts of a JSON file (an array of strings) and prints the results instead (a development aid). The
// cases run the real mermaid bundle in headless Chrome (mermaid.initialize for the site configuration, mermaid.parse to
// apply the frontmatter and directives, then mermaidAPI.getDiagramFromText for the diagram and its database), so the
// expectations are mermaid's own results, not hand-written values. Nothing is downloaded; the inputs are original.

import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { cases } from './flowchart-vector-cases.mjs';

// The repository root is three folders above tools/visual-diff/mermaid
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

// Read the optional arguments
const args = process.argv.slice(2);
let modules = root;
let out = join(root, 'test', 'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'upstream-vectors', 'flowchart-parser.json');
let probe = null;
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--modules' && i + 1 < args.length) {
    modules = resolve(args[++i]);
  } else if (args[i] === '--out' && i + 1 < args.length) {
    out = resolve(args[++i]);
  } else if (args[i] === '--probe' && i + 1 < args.length) {
    probe = resolve(args[++i]);
  } else {
    console.error('usage: node gen-flowchart-vectors.mjs [--modules <folder>] [--out <file> | --probe <file>]');
    process.exit(2);
  }
}

const resolver = createRequire(join(modules, 'noop.js'));
const mermaidFolder = join(modules, 'node_modules', 'mermaid');
const mermaidVersion = JSON.parse(readFileSync(join(mermaidFolder, 'package.json'), 'utf8')).version;
const puppeteer = (await import(pathToFileURL(resolver.resolve('puppeteer')).href)).default;

// Runs one text in the page: the database facts and the layout data, or the error
const runCase = async (page, text, site) =>
  page.evaluate(
    async ({ text, site }) => {
      // Keeps the listed keys of an object whose values are defined, as JSON.stringify would write them
      const pick = (obj, keys) => {
        const result = {};
        for (const key of keys) {
          if (obj[key] !== undefined) {
            result[key] = obj[key];
          }
        }
        return JSON.parse(JSON.stringify(result));
      };

      window.mermaid.initialize({ startOnLoad: false, ...site });
      let diagram;
      try {
        await window.mermaid.mermaidAPI.parse(text);
        diagram = await window.mermaid.mermaidAPI.getDiagramFromText(text);
      } catch (error) {
        const hash = error.hash;
        return {
          error: {
            // The position excerpt of a jison message (clipped source text) is left out
            message: String(error.message).split('\n').filter((line, i, all) => i === 0 || i === all.length - 1 || !hash).join('\n'),
            token: hash ? (hash.token === null || hash.token === undefined ? null : String(hash.token)) : undefined,
            line: hash && typeof hash.line === 'number' ? hash.line + 1 : undefined,
            expected: hash && Array.isArray(hash.expected) ? hash.expected.map((e) => e.replace(/^'|'$/g, '')) : undefined
          }
        };
      }

      if (!['flowchart-v2', 'flowchart', 'flowchart-elk'].includes(diagram.type)) {
        return { type: diagram.type };
      }

      const db = diagram.db;
      const vertexKeys = ['id', 'text', 'labelType', 'type', 'classes', 'styles', 'domId', 'props', 'dir', 'icon', 'form',
        'pos', 'img', 'constraint', 'assetWidth', 'assetHeight'];
      const edgeKeys = ['start', 'end', 'type', 'stroke', 'length', 'text', 'labelType', 'id', 'isUserDefinedId',
        'interpolate', 'style', 'classes', 'animate', 'animation'];
      const edges = db.getEdges();
      const data = db.getData();
      return {
        type: diagram.type,
        direction: db.getDirection() ?? null,
        accTitle: db.getAccTitle(),
        accDescr: db.getAccDescription(),
        vertices: [...db.getVertices().values()].map((v) => pick(v, vertexKeys)),
        edges: edges.map((e) => pick(e, edgeKeys)),
        defaultInterpolate: edges.defaultInterpolate ?? null,
        defaultStyle: edges.defaultStyle ?? null,
        subgraphs: db.getSubGraphs().map((s) => pick(s, ['id', 'nodes', 'title', 'classes', 'dir', 'labelType', 'metadata'])),
        classes: [...db.getClasses().values()].map((c) => pick(c, ['id', 'styles', 'textStyles'])),
        data: {
          nodes: data.nodes.map((n) => pick(n, ['id', 'label', 'labelType', 'labelStyle', 'parentId', 'padding', 'cssStyles',
            'cssCompiledStyles', 'cssClasses', 'dir', 'domId', 'look', 'isGroup', 'shape', 'icon', 'pos', 'img',
            'assetWidth', 'assetHeight', 'constraint'])),
          edges: data.edges.map((e) => pick(e, ['id', 'isUserDefinedId', 'start', 'end', 'type', 'label', 'labelType',
            'labelpos', 'thickness', 'minlen', 'classes', 'arrowTypeStart', 'arrowTypeEnd', 'arrowheadStyle',
            'cssCompiledStyles', 'labelStyle', 'style', 'pattern', 'look', 'animate', 'animation', 'curve']))
        }
      };
    },
    { text, site: site ?? {} }
  );

const browser = await puppeteer.launch({ headless: true });
try {
  const page = await browser.newPage();
  await page.goto('about:blank');
  await page.addScriptTag({ path: join(mermaidFolder, 'dist', 'mermaid.min.js') });

  if (probe) {
    const texts = JSON.parse(readFileSync(probe, 'utf8'));
    for (const text of texts) {
      console.log(JSON.stringify({ text, result: await runCase(page, text) }));
    }
  } else {
    const names = new Set();
    const results = [];
    for (const c of cases) {
      if (names.has(c.name)) {
        throw new Error(`duplicate case name ${c.name}`);
      }
      names.add(c.name);
      const result = await runCase(page, c.text, c.site);
      results.push({ name: c.name, ...(c.site ? { site: c.site } : {}), text: c.text, expected: result });
    }

    // One case per line keeps the file reviewable and its diffs small
    const source = `Generated by tools/visual-diff/mermaid/gen-flowchart-vectors.mjs from mermaid ${mermaidVersion} (MIT, Knut Sveidqvist and contributors); the inputs are original. See src/DemaConsulting.CanvasNet.Mermaid/third-party/README.md.`;
    const lines = [];
    lines.push('{');
    lines.push(`  "source": ${JSON.stringify(source)},`);
    lines.push(`  "mermaidVersion": ${JSON.stringify(mermaidVersion)},`);
    lines.push('  "cases": [');
    lines.push(results.map((c) => '    ' + JSON.stringify(c)).join(',\n'));
    lines.push('  ]');
    lines.push('}');
    mkdirSync(dirname(out), { recursive: true });
    writeFileSync(out, lines.join('\n') + '\n');
    const errors = results.filter((r) => r.expected.error).length;
    console.log(`mermaid ${mermaidVersion}: wrote ${results.length} flowchart vectors (${errors} errors) to ${out}`);
  }
} finally {
  await browser.close();
}
