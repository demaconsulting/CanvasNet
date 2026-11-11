// gen-diagram-vectors.mjs - records what the pinned mermaid's parser and database make of a list of diagram texts of
// one type (the database facts and the layout data of getData(), or the error), as the upstream vectors of the
// CanvasNet.Mermaid parser and model tests of that type.
//
// Developer tooling of tools/visual-diff; see the README there. Usage:
//
//     node gen-diagram-vectors.mjs --type class|state|er|pie|gantt|mindmap [--modules <folder>] [--out <file>]
//     node gen-diagram-vectors.mjs --type class|state|er|pie|gantt|mindmap [--modules <folder>] --probe <file>
//
// <type> selects the cases (<type>-vector-cases.mjs next to this script) and the facts recorded: for class, the ClassDB
// classes, relations, notes and namespaces; for state, the StateDB statement tree (after the translation of start, end and
// concurrent regions), states, relations and style classes; for er, the ErDB entities, relationships, style classes and
// subgraphs; and for each of those, the reduced getData() nodes and edges; for pie, the PieDB sections (each value as
// String() writes it), showData, title and accessibility texts; for gantt, the GanttDB statements (date and axis formats, tick
// interval, today marker, includes, excludes, weekday, inclusive end dates, top axis, display mode, sections, links,
// title and accessibility texts) and the tasks of getTasks() (their dates in milliseconds), or the error getTasks throws; for mindmap, the MindmapDB nodes in the order they were added (with the sections getData assigns) and the getData() nodes and edges. <modules> is a folder that holds node_modules/mermaid
// and node_modules/puppeteer (default: the repository root). <out> is the vector file (default:
// test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/<type>-parser.json). --probe runs the texts of a
// JSON file (an array of strings) and prints the results instead (a development aid).
//
// The cases run the real mermaid bundle in headless Chrome, so the expectations are mermaid's own results, not
// hand-written values. Each case runs in a fresh page: mermaid.initialize with the case's site configuration, then
// mermaidAPI.getDiagramFromText once. A fresh page matters because mermaid keeps some counters at module level (the
// class diagram's classCounter, which numbers the DOM ids); a page that draws one diagram, as mermaid-cli's does, starts
// them at 0 (and the gantt database never clears its map of task ids). Every page runs in the UTC time zone with its
// clock fixed at 2024-06-12T10:30:00Z (fixedNow), because a gantt chart reads the clock for today. mermaid.parse is not
// called first, because it would parse the text a second time and move those counters;
// the cases therefore give configuration as a site configuration, not as frontmatter. The random part of an id that
// mermaid's generateId makes (the last region of a concurrent state) is written as "random" (id-random-<counter>).
// Nothing is downloaded.
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// The repository root is three folders above tools/visual-diff/mermaid
const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '..', '..', '..');

// Read the arguments
const usage = 'usage: node gen-diagram-vectors.mjs --type class|state|er|pie|gantt|mindmap [--modules <folder>] [--out <file> | --probe <file>]';
const types = ['class', 'state', 'er', 'pie', 'gantt', 'mindmap'];
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

// Runs in the page: the facts of a pie chart (the PieDB state); each value is written as JavaScript's String() writes
// it (the legend of showData shows it so, and JSON has neither Infinity nor -0)
function pieFacts(diagram) {
  const db = diagram.db;
  return {
    type: diagram.type,
    title: db.getDiagramTitle(),
    accTitle: db.getAccTitle(),
    accDescr: db.getAccDescription(),
    showData: db.getShowData(),
    sections: [...db.getSections().entries()].map(([label, value]) => ({ label, value: String(value) }))
  };
}

// Runs in the page: the facts of a gantt chart (the GanttDB statements, then the tasks of getTasks(), which compiles the
// dates as the renderer does, or the error it throws); a date is written as its time in milliseconds (null for an
// invalid date), and a value that is not a date as its String()
function ganttFacts(diagram, pick) {
  const db = diagram.db;
  const time = (d) => (d instanceof Date ? (Number.isNaN(d.getTime()) ? null : d.getTime()) : d === undefined || d === null ? d : String(d));
  const facts = {
    type: diagram.type,
    title: db.getDiagramTitle(),
    accTitle: db.getAccTitle(),
    accDescr: db.getAccDescription(),
    dateFormat: db.getDateFormat(),
    axisFormat: db.getAxisFormat(),
    tickInterval: db.getTickInterval() ?? null,
    todayMarker: db.getTodayMarker(),
    includes: db.getIncludes(),
    excludes: db.getExcludes(),
    weekday: db.getWeekday(),
    inclusiveEndDates: db.endDatesAreInclusive(),
    topAxis: db.topAxisEnabled(),
    displayMode: db.getDisplayMode(),
    sections: db.getSections(),
    links: [...db.getLinks().entries()].map(([id, url]) => ({ id, url }))
  };
  try {
    facts.tasks = db.getTasks().map((t) => ({
      ...pick(t, ['id', 'task', 'section', 'type', 'processed', 'manualEndTime', 'classes', 'active', 'done', 'crit',
        'milestone', 'vert', 'order']),
      startTime: time(t.startTime),
      endTime: time(t.endTime),
      renderEndTime: time(t.renderEndTime)
    }));
  } catch (error) {
    facts.compileError = String(error.message);
  }
  return facts;
}

// Runs in the page: the facts of a mindmap (the MindmapDB nodes in the order addNode added them, after getData() has
// assigned their sections, and the getData() nodes and edges); the diagram id of getData() holds a random uuid and is
// left out
function mindmapFacts(diagram, pick) {
  const db = diagram.db;
  const data = db.getData();
  return {
    type: diagram.type,
    nodes: db.nodes.map((n) => ({
      ...pick(n, ['id', 'nodeId', 'level', 'descr', 'type', 'icon', 'class', 'section', 'isRoot', 'padding', 'width']),
      children: n.children.map((c) => c.id)
    })),
    data: {
      nodes: data.nodes.map((n) => pick(n, ['id', 'domId', 'label', 'labelType', 'shape', 'width', 'height', 'padding',
        'cssClasses', 'icon', 'level', 'nodeId', 'type', 'section', 'look'])),
      edges: data.edges.map((e) => pick(e, ['id', 'start', 'end', 'type', 'curve', 'thickness', 'classes', 'depth',
        'section', 'look']))
    }
  };
}

const factsOf = { class: classFacts, state: stateFacts, er: erFacts, pie: pieFacts, gantt: ganttFacts, mindmap: mindmapFacts };
// The diagram ids (diagram.type) whose facts each type records: the legacy ids draw (class) or parse (state) the same
// way
const wantedOf = { class: ['classDiagram', 'class'], state: ['stateDiagram', 'state'], er: ['er'], pie: ['pie'], gantt: ['gantt'], mindmap: ['mindmap'] };

// The fixed clock of every page (mermaid's gantt chart reads the time for "today"): a Date whose constructor without
// arguments and now() give this instant; the pages also run in the UTC time zone, the zone of the CanvasNet port
export const fixedNow = '2024-06-12T10:30:00Z';
function fixClock(now) {
  const fixed = Date.parse(now);
  const RealDate = Date;
  class FixedDate extends RealDate {
    constructor(...args) {
      super(...(args.length === 0 ? [fixed] : args));
    }

    static now() {
      return fixed;
    }
  }
  window.Date = FixedDate;
  if (new Date().getTimezoneOffset() !== 0 || Date.now() !== fixed) {
    throw new Error('the page clock is not fixed to UTC');
  }
}

// Runs one text in a fresh page: the facts, or the error
const runCase = async (browser, text, site) => {
  const page = await browser.newPage();
  try {
    await page.emulateTimezone('UTC');
    await page.goto('about:blank');
    await page.evaluate(fixClock, fixedNow);
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
    const source = `Generated by tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type ${type} from mermaid ${mermaidVersion} (MIT, Knut Sveidqvist and contributors). ${provenance} See src/DemaConsulting.CanvasNet.Mermaid/third-party/README.md.`;
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
