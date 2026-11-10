// gen-diagram-vectors.mjs - records what the pinned mermaid's parser and database make of a list of diagram texts of
// one type (the database facts and the layout data of getData(), or the error), as the upstream vectors of the
// CanvasNet.Mermaid parser and model tests of that type.
//
// Developer tooling of benchmarks/visual-diff; see the README there. Usage:
//
//     node gen-diagram-vectors.mjs --type class|state|er [--modules <folder>] [--out <file>]
//     node gen-diagram-vectors.mjs --type class|state|er [--modules <folder>] --probe <file>
//
// <type> selects the cases (<type>-vector-cases.mjs next to this script) and the facts recorded: for class, the ClassDB
// classes, relations, notes and namespaces; for state, the StateDB statement tree (after the translation of start, end and
// concurrent regions), states, relations and style classes; for er, the ErDB entities, relationships, style classes and
// subgraphs; and for each, the reduced getData() nodes and edges. <modules> is a folder that holds node_modules/mermaid
// and node_modules/puppeteer (default: the repository root). <out> is the vector file (default:
// test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/<type>-parser.json). --probe runs the texts of a
// JSON file (an array of strings) and prints the results instead (a development aid).
//
// The cases run the real mermaid bundle in headless Chrome, so the expectations are mermaid's own results, not
// hand-written values. Each case runs in a fresh page: mermaid.initialize with the case's site configuration, then
// mermaidAPI.getDiagramFromText once. A fresh page matters because mermaid keeps some counters at module level (the
// class diagram's classCounter, which numbers the DOM ids); a page that draws one diagram, as mermaid-cli's does, starts
// them at 0. mermaid.parse is not called first, because it would parse the text a second time and move those counters;
// the cases therefore give configuration as a site configuration, not as frontmatter. The random part of an id that
// mermaid's generateId makes (the last region of a concurrent state) is written as "random" (id-random-<counter>).
// Nothing is downloaded.
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// The repository root is three folders above benchmarks/visual-diff/mermaid
const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '..', '..', '..');

// Read the arguments
const usage = 'usage: node gen-diagram-vectors.mjs --type class|state|er [--modules <folder>] [--out <file> | --probe <file>]';
const types = ['class', 'state', 'er'];
const args = process.argv.slice(2);
let type = null;
let modules = root;
let out = null;
let probe = null;
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--type' && i + 1 < args.length) {
    type = args[++i];
  } else if (args[i] === '--modules' && i + 1 < args.length) {
    modules = resolve(args[++i]);
  } else if (args[i] === '--out' && i + 1 < args.length) {
    out = resolve(args[++i]);
  } else if (args[i] === '--probe' && i + 1 < args.length) {
    probe = resolve(args[++i]);
  } else {
    console.error(usage);
    process.exit(2);
  }
}

if (!types.includes(type)) {
  console.error(usage);
  process.exit(2);
}

out ??= join(root, 'test', 'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'upstream-vectors', `${type}-parser.json`);
const loadCases = async () => import(pathToFileURL(join(here, `${type}-vector-cases.mjs`)).href);

const resolver = createRequire(join(modules, 'noop.js'));
const mermaidFolder = join(modules, 'node_modules', 'mermaid');
const mermaidVersion = JSON.parse(readFileSync(join(mermaidFolder, 'package.json'), 'utf8')).version;
const puppeteer = (await import(pathToFileURL(resolver.resolve('puppeteer')).href)).default;

// Runs in the page: the facts of a class diagram (the ClassDB state and getData())
function classFacts(diagram, pick) {
  const db = diagram.db;
  const member = (m) => ({
    ...pick(m, ['id', 'memberType', 'visibility', 'classifier', 'parameters', 'returnType', 'text']),
    display: m.getDisplayDetails()
  });
  const data = db.getData();
  return {
    type: diagram.type,
    direction: db.getDirection(),
    title: db.getDiagramTitle(),
    accTitle: db.getAccTitle(),
    accDescr: db.getAccDescription(),
    classes: [...db.getClasses().values()].map((c) => ({
      ...pick(c, ['id', 'type', 'label', 'text', 'shape', 'cssClasses', 'annotations', 'styles', 'domId', 'parent',
        'link', 'linkTarget', 'haveCallback', 'tooltip']),
      members: c.members.map(member),
      methods: c.methods.map(member)
    })),
    relations: db.getRelations().map((r) => pick(r, ['id1', 'id2', 'relationTitle1', 'relationTitle2', 'title',
      'relation', 'style'])),
    notes: [...db.getNotes().values()].map((n) => pick(n, ['id', 'class', 'text', 'index', 'parent'])),
    namespaces: [...db.getNamespaces().values()].map((n) => ({
      ...pick(n, ['id', 'label', 'domId', 'parent', 'explicit']),
      classes: [...n.classes.keys()],
      notes: [...n.notes.keys()],
      children: [...n.children.keys()]
    })),
    interfaces: (db.interfaces ?? []).map((i) => pick(i, ['id', 'label', 'classId'])),
    styleClasses: [...(db.styleClasses?.values() ?? [])].map((s) => pick(s, ['id', 'styles', 'textStyles'])),
    data: {
      direction: data.direction,
      nodes: data.nodes.map((n) => ({
        ...pick(n, ['id', 'label', 'text', 'shape', 'isGroup', 'parentId', 'padding', 'cssStyles', 'cssClasses',
          'cssCompiledStyles', 'styles', 'labelType', 'domId', 'look', 'annotations', 'link', 'linkTarget', 'tooltip',
          'haveCallback']),
        ...(n.members ? { members: n.members.map((m) => m.getDisplayDetails()) } : {}),
        ...(n.methods ? { methods: n.methods.map((m) => m.getDisplayDetails()) } : {})
      })),
      edges: data.edges.map((e) => pick(e, ['id', 'start', 'end', 'type', 'label', 'labelType', 'labelpos', 'thickness',
        'minlen', 'classes', 'arrowTypeStart', 'arrowTypeEnd', 'startLabelRight', 'endLabelLeft', 'arrowheadStyle',
        'labelStyle', 'style', 'pattern', 'look', 'curve']))
    }
  };
}

// Runs in the page: the facts of a state diagram (the StateDB state, its translated statement tree and getData())
function stateFacts(diagram, pick) {
  const db = diagram.db;
  const data = db.getData();
  // The statement tree after the translation (start and end ids, concurrent regions), without the parser's internals
  const statement = (s) => {
    const result = pick(s, ['stmt', 'id', 'type', 'description', 'descriptions', 'note', 'classes', 'styles',
      'textStyles', 'start', 'value', 'styleClass', 'url', 'tooltip']);
    if (s.stmt === 'relation') {
      result.state1 = statement(s.state1);
      result.state2 = statement(s.state2);
    }
    if (Array.isArray(s.doc)) {
      result.doc = s.doc.map(statement);
    }
    return result;
  };
  return {
    type: diagram.type,
    direction: db.getDirection(),
    title: db.getDiagramTitle(),
    accTitle: db.getAccTitle(),
    accDescr: db.getAccDescription(),
    rootDoc: (db.rootDoc ?? []).map(statement),
    states: [...db.getStates().values()].map((s) => pick(s, ['id', 'type', 'descriptions', 'note', 'classes', 'styles',
      'textStyles'])),
    relations: db.getRelations().map((r) => pick(r, ['id1', 'id2', 'relationTitle'])),
    styleClasses: [...db.getClasses().values()].map((c) => pick(c, ['id', 'styles', 'textStyles'])),
    links: [...(db.getLinks?.() ?? new Map()).entries()].map(([id, link]) => ({ id, ...pick(link, ['url', 'tooltip']) })),
    data: {
      direction: data.direction,
      nodes: data.nodes.map((n) => pick(n, ['id', 'label', 'description', 'shape', 'type', 'isGroup', 'parentId',
        'dir', 'domId', 'padding', 'rx', 'ry', 'centerLabel', 'cssClasses', 'cssStyles', 'cssCompiledStyles',
        'labelStyle', 'labelType', 'position', 'look'])),
      edges: data.edges.map((e) => pick(e, ['id', 'start', 'end', 'label', 'arrowhead', 'arrowTypeEnd', 'style',
        'labelStyle', 'arrowheadStyle', 'labelpos', 'labelType', 'thickness', 'classes', 'look']))
    }
  };
}

// Runs in the page: the facts of an ER diagram (the ErDB state and getData())
function erFacts(diagram, pick) {
  const db = diagram.db;
  const attribute = (a) => pick(a, ['type', 'name', 'keys', 'comment']);
  const entities = [...db.getEntities().entries()].map(([name, e]) => ({
    name,
    ...pick(e, ['id', 'label', 'alias', 'shape', 'cssClasses', 'cssStyles', 'labelType']),
    attributes: e.attributes.map(attribute)
  }));
  const data = db.getData();
  return {
    type: diagram.type,
    direction: db.getDirection(),
    title: db.getDiagramTitle(),
    accTitle: db.getAccTitle(),
    accDescr: db.getAccDescription(),
    entities,
    relationships: db.getRelationships().map((r) => pick(r, ['entityA', 'roleA', 'entityB', 'relSpec'])),
    styleClasses: [...db.getClasses().values()].map((c) => pick(c, ['id', 'styles', 'textStyles'])),
    subgraphs: db.getSubGraphs().map((s) => pick(s, ['id', 'title', 'nodes', 'classes', 'cssStyles', 'dir',
      'labelType'])),
    data: {
      direction: data.direction,
      nodes: data.nodes.map((n) => ({
        ...pick(n, ['id', 'label', 'alias', 'shape', 'isGroup', 'parentId', 'dir', 'padding', 'cssClasses',
          'cssStyles', 'cssCompiledStyles', 'colorIndex', 'labelStyle', 'labelType', 'look']),
        ...(n.attributes ? { attributes: n.attributes.map(attribute) } : {})
      })),
      edges: data.edges.map((e) => pick(e, ['id', 'start', 'end', 'type', 'curve', 'label', 'labelpos', 'labelType',
        'thickness', 'classes', 'arrowTypeStart', 'arrowTypeEnd', 'pattern', 'look']))
    }
  };
}

const factsOf = { class: classFacts, state: stateFacts, er: erFacts };
// The diagram ids (diagram.type) whose facts each type records: the legacy ids draw (class) or parse (state) the same
// way
const wantedOf = { class: ['classDiagram', 'class'], state: ['stateDiagram', 'state'], er: ['er'] };

// Runs one text in a fresh page: the facts, or the error
const runCase = async (browser, text, site) => {
  const page = await browser.newPage();
  try {
    await page.goto('about:blank');
    await page.addScriptTag({ path: join(mermaidFolder, 'dist', 'mermaid.min.js') });
    return await page.evaluate(
      async ({ text, site, facts, wanted }) => {
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

        // mermaid's generateId makes ids from Math.random ("id-" + random.toString(36).substr(2, 12) + "-" + counter;
        // a state diagram names the last region of a concurrent state so): each random token is recorded, and written
        // as "random" in the facts, so that the vectors do not change from run to run
        const tokens = new Set();
        const random = Math.random;
        Math.random = () => {
          const value = random();
          tokens.add(value.toString(36).substr(2, 12));
          return value;
        };
        const stable = (result) => {
          let json = JSON.stringify(result);
          for (const token of tokens) {
            json = json.split(`id-${token}-`).join('id-random-');
          }
          return JSON.parse(json);
        };

        window.mermaid.initialize({ startOnLoad: false, ...site });
        let diagram;
        try {
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

        if (!wanted.includes(diagram.type)) {
          return { type: diagram.type };
        }

        // The facts function is passed as text, because page.evaluate cannot pass a function argument
        return stable(new Function('diagram', 'pick', `return (${facts})(diagram, pick);`)(diagram, pick));
      },
      { text, site: site ?? {}, facts: factsOf[type].toString(), wanted: wantedOf[type] }
    );
  } finally {
    await page.close();
  }
};

const browser = await puppeteer.launch({ headless: true });
try {
  if (probe) {
    const texts = JSON.parse(readFileSync(probe, 'utf8'));
    for (const text of texts) {
      console.log(JSON.stringify({ text, result: await runCase(browser, text) }));
    }
  } else {
    const { cases, provenance } = await loadCases();
    const names = new Set();
    const results = [];
    for (const c of cases) {
      if (names.has(c.name)) {
        throw new Error(`duplicate case name ${c.name}`);
      }
      names.add(c.name);
      const result = await runCase(browser, c.text, c.site);
      results.push({ name: c.name, spec: c.spec, ...(c.site ? { site: c.site } : {}), text: c.text, expected: result });
    }

    // One case per line keeps the file reviewable and its diffs small
    const source = `Generated by benchmarks/visual-diff/mermaid/gen-diagram-vectors.mjs --type ${type} from mermaid ${mermaidVersion} (MIT, Knut Sveidqvist and contributors). ${provenance} See src/DemaConsulting.CanvasNet.Mermaid/third-party/README.md.`;
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
    console.log(`mermaid ${mermaidVersion}: wrote ${results.length} ${type} vectors (${errors} errors) to ${out}`);
  }
} finally {
  await browser.close();
}
