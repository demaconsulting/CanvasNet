// gen-theme-vectors.mjs - records the theme variables and the effective configuration that the pinned mermaid computes,
// and the colors that its khroma computes, as the upstream vectors of the CanvasNet.Mermaid configuration tests.
//
// Developer tooling of tools/visual-diff; see the README there. Usage:
//
//     node gen-theme-vectors.mjs [--modules <folder>] [--out <file>]
//
// <modules> is a folder that holds node_modules/mermaid, node_modules/khroma and node_modules/puppeteer (default: the
// repository root). <out> is the vector file (default:
// test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/themes.json). The configuration cases run the
// real mermaid bundle in headless Chrome (mermaid.initialize for the site configuration, then mermaid.parse for the
// frontmatter and the directives of a text, then mermaidAPI.getConfig), so the expectations are mermaid's own results,
// not hand-written values. The color cases run khroma in Node. Nothing is downloaded; the inputs are original.

import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// The repository root is three folders above tools/visual-diff/mermaid
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

// Read the two optional arguments
const args = process.argv.slice(2);
let modules = root;
let out = join(root, 'test', 'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'upstream-vectors', 'themes.json');
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--modules' && i + 1 < args.length) {
    modules = resolve(args[++i]);
  } else if (args[i] === '--out' && i + 1 < args.length) {
    out = resolve(args[++i]);
  } else {
    console.error('usage: node gen-theme-vectors.mjs [--modules <folder>] [--out <file>]');
    process.exit(2);
  }
}

const resolver = createRequire(join(modules, 'noop.js'));
const mermaidFolder = join(modules, 'node_modules', 'mermaid');
const mermaidVersion = JSON.parse(readFileSync(join(mermaidFolder, 'package.json'), 'utf8')).version;
const khromaVersion = JSON.parse(readFileSync(join(modules, 'node_modules', 'khroma', 'package.json'), 'utf8')).version;
const khroma = await import(pathToFileURL(resolver.resolve('khroma')).href);
const puppeteer = (await import(pathToFileURL(resolver.resolve('puppeteer')).href)).default;

// ---------------------------------------------------------------------------------------------------------------------
// Color cases: each khroma function the themes and the flowchart styles use, on inputs that exercise every parser
// (hexadecimal, keyword, rgb, hsl) and every output form (hexadecimal, rgb, rgba, hsl, unchanged input)
// ---------------------------------------------------------------------------------------------------------------------
const colorInputs = [
  '#ECECFF', '#ffffde', '#1f2020', '#333', '#cde498', '#fff4dd', '#eee', '#707070', '#EAE8D9', '#81B1DB',
  'white', 'black', 'lightgrey', 'green', 'Navy', 'grey', 'red',
  'rgb(10, 20, 30)', 'rgba(232,232,232, 0.8)', 'rgb(50%, 25%, 0%)', 'rgba(255, 0, 0, 50%)',
  'hsl(240, 100%, 96.2745098039%)', 'hsl(80, 100%, 96.2745098039%)', 'hsla(-30, 40%, 60%, 0.5)', 'hsl(1.5turn, 50%, 50%)',
  '#abcd', '#11223344', '#ff000080'
];
const colors = [];
const record = (fn, input, call) => {
  try {
    colors.push({ fn, args: input, result: call() });
  } catch (error) {
    colors.push({ fn, args: input, error: String(error.message) });
  }
};
for (const c of colorInputs) {
  record('invert', [c], () => khroma.invert(c));
  record('darken', [c, 10], () => khroma.darken(c, 10));
  record('darken', [c, 25], () => khroma.darken(c, 25));
  record('lighten', [c, 16], () => khroma.lighten(c, 16));
  record('lighten', [c, 75], () => khroma.lighten(c, 75));
  record('lighten', [c], () => khroma.lighten(c));
  record('adjust', [c, { h: 120 }], () => khroma.adjust(c, { h: 120 }));
  record('adjust', [c, { h: -160 }], () => khroma.adjust(c, { h: -160 }));
  record('adjust', [c, { s: -40, l: -10 }], () => khroma.adjust(c, { s: -40, l: -10 }));
  record('adjust', [c, { s: -40, l: 10 }], () => khroma.adjust(c, { s: -40, l: 10 }));
  record('adjust', [c, { h: 180, s: -15, l: -5 }], () => khroma.adjust(c, { h: 180, s: -15, l: -5 }));
  record('adjust', [c, { r: 5, g: 5, b: 5 }], () => khroma.adjust(c, { r: 5, g: 5, b: 5 }));
  record('adjust', [c, { r: -15, g: -15, b: -15 }], () => khroma.adjust(c, { r: -15, g: -15, b: -15 }));
  record('adjust', [c, { h: 0 }], () => khroma.adjust(c, { h: 0 }));
  record('rgba', [c, 0.5], () => khroma.rgba(c, 0.5));
  record('isDark', [c], () => khroma.isDark(c));
  record('luminance', [c], () => khroma.luminance(c));
  record('channel', [c, 'r'], () => khroma.channel(c, 'r'));
  record('channel', [c, 'g'], () => khroma.channel(c, 'g'));
  record('channel', [c, 'b'], () => khroma.channel(c, 'b'));
  record('channel', [c, 'h'], () => khroma.channel(c, 'h'));
  record('channel', [c, 'l'], () => khroma.channel(c, 'l'));
}
record('rgba', [102, 102, 255, 0.49], () => khroma.rgba(102, 102, 255, 0.49));
record('rgba', [255, 255, 255, 70], () => khroma.rgba(255, 255, 255, 70));
record('rgba', [255, 255, 255, 0.25], () => khroma.rgba(255, 255, 255, 0.25));
record('rgba', [12.5, 300, -4, 1], () => khroma.rgba(12.5, 300, -4, 1));
record('invert', ['not-a-color'], () => khroma.invert('not-a-color'));
record('adjust', ['#123', { h: 30, r: 5 }], () => khroma.adjust('#123', { h: 30, r: 5 }));

// ---------------------------------------------------------------------------------------------------------------------
// Configuration cases: a site configuration (mermaid.initialize, as mmdc -t and -c give it) and a text (frontmatter and
// directives); the result is mermaid's current configuration after parsing the text
// ---------------------------------------------------------------------------------------------------------------------
const flow = 'flowchart LR\n  A --> B\n';
const themes = ['default', 'base', 'dark', 'forest', 'neutral'];
const overrides = { primaryColor: '#ff6600', lineColor: '#0000ff', fontFamily: 'arial', nodeBorder: '#123456' };
const configCases = [];
for (const theme of themes) {
  configCases.push({ name: `site-${theme}`, site: { theme }, text: flow, full: true });
  configCases.push({ name: `site-${theme}-themeVariables`, site: { theme, themeVariables: overrides }, text: flow, full: true });
  configCases.push({ name: `site-${theme}-darkMode`, site: { theme, themeVariables: { darkMode: true } }, text: flow, full: true });
  configCases.push({
    name: `directive-${theme}-themeVariables`,
    site: { theme: 'default' },
    text: `%%{init: {"theme": "${theme}", "themeVariables": {"primaryColor": "#00aa55", "edgeLabelBackground": "#ffffff"}}}%%\n${flow}`,
    full: true
  });
}

// Precedence, secure keys, sanitizing and the config key move; only the scalar keys are recorded (full: false)
const precedence = [
  { name: 'defaults', site: {}, text: flow },
  { name: 'site-config', site: { theme: 'forest', flowchart: { nodeSpacing: 70, curve: 'linear' }, fontFamily: 'courier' }, text: flow },
  { name: 'frontmatter-over-site', site: { theme: 'forest', flowchart: { nodeSpacing: 70 } }, text: `---\nconfig:\n  theme: dark\n  flowchart:\n    rankSpacing: 20\n---\n${flow}` },
  { name: 'directive-over-frontmatter', site: { theme: 'forest' }, text: `---\nconfig:\n  theme: dark\n  look: handDrawn\n---\n%%{init: {'theme': 'neutral', 'flowchart': {'curve': 'step'}}}%%\n${flow}` },
  { name: 'two-directives', site: {}, text: `%%{init: {"theme": "dark"}}%%\n%%{initialize: {"theme": "base", "flowchart": {"padding": 3}}}%%\n${flow}` },
  { name: 'config-key-move', site: {}, text: `%%{init: {"config": {"nodeSpacing": 11}}}%%\n${flow}` },
  { name: 'config-key-move-graph', site: {}, text: `%%{init: {"config": {"rankSpacing": 12}}}%%\ngraph TD\n  A --> B\n` },
  { name: 'secure-keys', site: {}, text: `%%{init: {"securityLevel": "loose", "maxTextSize": 10, "maxEdges": 3, "startOnLoad": false, "secure": ["theme"], "theme": "dark"}}%%\n${flow}` },
  { name: 'secure-keys-frontmatter', site: {}, text: `---\nconfig:\n  securityLevel: loose\n  maxEdges: 2\n  theme: forest\n---\n${flow}` },
  { name: 'site-secure-list', site: { secure: ['theme'] }, text: `%%{init: {"theme": "dark", "look": "handDrawn"}}%%\n${flow}` },
  { name: 'unknown-keys', site: {}, text: `%%{init: {"notAKey": 1, "flowchart": {"alsoNot": 2, "padding": 4}, "__proto__x": 3}}%%\n${flow}` },
  { name: 'unsafe-strings', site: {}, text: `%%{init: {"fontFamily": "<b>x</b>", "themeVariables": {"primaryColor": "#f00;}{", "lineColor": "url(x)"}}}%%\n${flow}` },
  { name: 'bad-json', site: {}, text: `%%{init: {"theme": "dark",}}%%\n${flow}` },
  { name: 'bad-json-second', site: {}, text: `%%{init: {"theme": "dark"}}%%\n%%{init: {bad}}%%\n${flow}` },
  { name: 'wrap', site: {}, text: `%%{wrap}%%\n${flow}` },
  { name: 'font-family-to-theme', site: {}, text: `%%{init: {"fontFamily": "monospace"}}%%\n${flow}` },
  { name: 'site-font-family', site: { fontFamily: 'Georgia' }, text: flow },
  { name: 'themeVariables-without-theme', site: { theme: 'dark' }, text: `%%{init: {"themeVariables": {"primaryColor": "#ff0000"}}}%%\n${flow}`, full: true },
  { name: 'theme-without-themeVariables', site: { theme: 'dark', themeVariables: { primaryColor: '#ff0000' } }, text: `%%{init: {"theme": "forest"}}%%\n${flow}`, full: true },
  { name: 'unknown-theme', site: { theme: 'nope' }, text: flow, full: true },
  { name: 'unknown-theme-directive', site: { theme: 'dark' }, text: `%%{init: {"theme": "nope"}}%%\n${flow}` },
  { name: 'html-labels', site: {}, text: `---\nconfig:\n  htmlLabels: false\n  flowchart:\n    htmlLabels: true\n---\n${flow}` },
  { name: 'theme-css', site: {}, text: `%%{init: {"themeCSS": ".node rect { fill: red; }"}}%%\n${flow}` },
  { name: 'theme-css-unbalanced', site: {}, text: `%%{init: {"themeCSS": ".node rect { fill: red; "}}%%\n${flow}` },
  { name: 'display-mode', site: {}, text: `---\ndisplayMode: compact\n---\n${flow}` },
  { name: 'null-cancels', site: {}, text: `---\nconfig:\n  flowchart:\n    curve: linear\n---\n%%{init: {"flowchart": {"curve": null}}}%%\n${flow}` },
  { name: 'renderer-elk', site: {}, text: `---\nconfig:\n  flowchart:\n    defaultRenderer: elk\n---\n${flow}` },
  { name: 'flowchart-elk-keyword', site: {}, text: 'flowchart-elk LR\n  A --> B\n' },
  { name: 'layout-user-over-elk', site: { layout: 'dagre' }, text: 'flowchart-elk LR\n  A --> B\n' },
  { name: 'config-key-move-two-directives', site: {}, text: `%%{init: {"config": {"nodeSpacing": 11}}}%%\n%%{init: {"theme": "dark"}}%%\n${flow}` },
  {
    name: 'site-and-directive-themeVariables',
    site: { theme: 'forest', themeVariables: { primaryColor: '#ff0000' } },
    text: `%%{init: {"theme": "dark", "themeVariables": {"lineColor": "#00ff00"}}}%%\n${flow}`,
    full: true
  },
  {
    name: 'frontmatter-themeVariables-and-directive-theme',
    site: {},
    text: `---\nconfig:\n  themeVariables:\n    primaryColor: "#123456"\n---\n%%{init: {"theme": "base"}}%%\n${flow}`,
    full: true
  },
  { name: 'directive-font-family-and-theme', site: {}, text: `%%{init: {"theme": "neutral", "fontFamily": "Georgia"}}%%\n${flow}`, full: true }
];
configCases.push(...precedence);

const browser = await puppeteer.launch({ headless: true });
try {
  const page = await browser.newPage();
  await page.goto('about:blank');
  await page.addScriptTag({ path: join(mermaidFolder, 'dist', 'mermaid.min.js') });

  // The configuration keys mermaid's sanitizeDirective accepts (defaultConfig.ts keyify) and the flowchart and global
  // defaults
  const reference = await page.evaluate(() => {
    const config = window.mermaid.mermaidAPI.defaultConfig;
    const keyify = (obj, prefix = '') =>
      Object.keys(obj).reduce((res, el) => {
        if (Array.isArray(obj[el])) {
          return res;
        } else if (typeof obj[el] === 'object' && obj[el] !== null) {
          return [...res, prefix + el, ...keyify(obj[el], '')];
        }
        return [...res, prefix + el];
      }, []);
    const keys = [...new Set(keyify(config, ''))];
    const global = {};
    for (const [key, value] of Object.entries(config)) {
      if (typeof value !== 'object' || value === null || Array.isArray(value)) {
        global[key] = value === undefined ? null : value;
      }
    }
    return { keys, global, flowchart: config.flowchart };
  });

  const results = [];
  for (const c of configCases) {
    const result = await page.evaluate(async ({ site, text, full }) => {
      window.mermaid.initialize({ startOnLoad: false, ...site });
      await window.mermaid.parse(text);
      const config = window.mermaid.mermaidAPI.getConfig();
      const scalars = {};
      for (const [key, value] of Object.entries(config)) {
        if (value !== undefined && (typeof value !== 'object' || value === null || Array.isArray(value))) {
          scalars[key] = value;
        }
      }
      return {
        config: scalars,
        flowchart: config.flowchart,
        gantt: { displayMode: config.gantt?.displayMode },
        themeVariables: full ? JSON.parse(JSON.stringify(config.themeVariables)) : undefined
      };
    }, c);
    results.push({ name: c.name, site: c.site, text: c.text, expected: result });
  }

  const vectors = {
    source: `Generated by tools/visual-diff/mermaid/gen-theme-vectors.mjs from mermaid ${mermaidVersion} (MIT, Knut Sveidqvist and contributors) and khroma ${khromaVersion} (MIT, Fabio Spampinato); the inputs are original. See src/DemaConsulting.CanvasNet.Mermaid/third-party/README.md.`,
    mermaidVersion,
    khromaVersion,
    configKeys: reference.keys,
    defaults: { global: reference.global, flowchart: reference.flowchart },
    colors,
    configs: results
  };

  // One case per line keeps the file reviewable and its diffs small
  const lines = [];
  lines.push('{');
  lines.push(`  "source": ${JSON.stringify(vectors.source)},`);
  lines.push(`  "mermaidVersion": ${JSON.stringify(mermaidVersion)},`);
  lines.push(`  "khromaVersion": ${JSON.stringify(khromaVersion)},`);
  lines.push(`  "configKeys": ${JSON.stringify(vectors.configKeys)},`);
  lines.push(`  "defaults": ${JSON.stringify(vectors.defaults)},`);
  lines.push('  "colors": [');
  lines.push(colors.map((c) => '    ' + JSON.stringify(c)).join(',\n'));
  lines.push('  ],');
  lines.push('  "configs": [');
  lines.push(results.map((c) => '    ' + JSON.stringify(c)).join(',\n'));
  lines.push('  ]');
  lines.push('}');
  mkdirSync(dirname(out), { recursive: true });
  writeFileSync(out, lines.join('\n') + '\n');
  console.log(`mermaid ${mermaidVersion}, khroma ${khromaVersion}: wrote ${colors.length} color and ${results.length} configuration vectors to ${out}`);
} finally {
  await browser.close();
}
