// render-mathml.mjs - lays MathML equations out with a reference implementation (MathML Core in headless Chrome, or
// headless Firefox for the MathML 3 table attributes that Chrome ignores, through the puppeteer that the repository's
// pinned @mermaid-js/mermaid-cli brings) and writes, for each equation, the boxes of its elements (a .layout.json file)
// and optionally its picture (PNG).
//
// Developer tooling of tools/visual-diff; see the README there. Usage (from anywhere, after "npm install" in the
// repository root; Firefox also needs the one-time "npx puppeteer browsers install firefox"):
//
//     node render-mathml.mjs [--browser chrome|firefox] [--font-size N] [--png-dir DIR] [--layout-dir DIR] <equation.mml>...
//
// For each <name>.mml it writes <name>.layout.json into --layout-dir and <name>.png into --png-dir (either may be left
// out; at least one is needed). The page uses the Noto Sans Math font that CanvasNet bundles (an @font-face of the file
// in src/DemaConsulting.CanvasNet/Fonts/BundledFonts, inlined as a data URL) for the whole equation, at --font-size CSS
// pixels (100 by default, to dilute the integer rounding of the browser). The equation is shown inline (display: inline
// math; a block equation keeps its math-style: normal), so that a zero-size inline-block probe after it gives the
// baseline.
//
// The layout file holds, rounded to 0.001 CSS pixels:
//   - chrome (or firefox): the browser version; fontSize: the font size;
//   - math: width, height and baseline (from the top) of the math element;
//   - elements: in document order, every element inside math with its path (the indices of the element children from
//     math, joined with "/"), its tag, and its box [x, y, width, height] from getBoundingClientRect, relative to the
//     top left corner of the math element.

import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..');
const fontPath = join(repoRoot, 'src', 'DemaConsulting.CanvasNet', 'Fonts', 'BundledFonts', 'NotoSansMath-Regular.ttf');

// --- Arguments ---------------------------------------------------------------------------------------------------
const args = process.argv.slice(2);
const files = [];
let fontSize = 100;
let pngDir;
let layoutDir;
let browserName = 'chrome';
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--browser') {
    browserName = args[++i];
  } else if (args[i] === '--font-size') {
    fontSize = Number(args[++i]);
  } else if (args[i] === '--png-dir') {
    pngDir = args[++i];
  } else if (args[i] === '--layout-dir') {
    layoutDir = args[++i];
  } else if (args[i].startsWith('--')) {
    usage(`unknown option ${args[i]}`);
  } else {
    files.push(args[i]);
  }
}

if (files.length === 0 || (!pngDir && !layoutDir) || !(fontSize > 0)) {
  usage('give at least one equation and --png-dir or --layout-dir');
}

if (browserName !== 'chrome' && browserName !== 'firefox') {
  usage(`unknown browser ${browserName} (chrome or firefox)`);
}

function usage(message) {
  console.error(`render-mathml.mjs: ${message}`);
  console.error('usage: node render-mathml.mjs [--browser chrome|firefox] [--font-size N] [--png-dir DIR] [--layout-dir DIR] <equation.mml>...');
  process.exit(2);
}

// --- The browser (the puppeteer of the repository's node_modules) -------------------------------------------------
const resolver = createRequire(join(repoRoot, 'noop.js'));
const puppeteer = (await import(pathToFileURL(resolver.resolve('puppeteer')).href)).default;
const font = readFileSync(fontPath).toString('base64');
const browser = await puppeteer.launch({ browser: browserName, headless: true });
const version = await browser.version();

const round = (value) => Math.round(value * 1000) / 1000;

try {
  for (const file of files) {
    const name = basename(file).replace(/\.mml$/, '');
    const markup = readFileSync(file, 'utf8').replace(/^\uFEFF/, '').replace(/<\?xml[^>]*\?>/, '');

    // The page: the bundled math font for the whole equation, shown inline with a baseline probe after it
    const page = await browser.newPage();
    await page.setViewport({ width: 4000, height: 2000 });
    await page.setContent(`<!DOCTYPE html><html><head><meta charset="utf-8"><style>
      @font-face { font-family: CanvasNetMath; src: url(data:font/ttf;base64,${font}); }
      html, body { margin: 0; padding: 0; }
      body { padding: 50px; }
      math { font-family: CanvasNetMath; font-size: ${fontSize}px; display: inline math; }
      #probe { display: inline-block; width: 0; height: 0; vertical-align: baseline; }
      </style></head><body><div id="line">${markup}<span id="probe"></span></div></body></html>`);
    await page.evaluate(() => document.fonts.ready);

    // The boxes of every element, relative to the math element
    const layout = await page.evaluate(() => {
      const math = document.querySelector('math');
      const origin = math.getBoundingClientRect();
      const probe = document.getElementById('probe').getBoundingClientRect();
      const elements = [];
      const walk = (element, path) => {
        Array.from(element.children).forEach((child, index) => {
          const childPath = path === '' ? `${index}` : `${path}/${index}`;
          const box = child.getBoundingClientRect();
          elements.push({ path: childPath, tag: child.localName, box: [box.x - origin.x, box.y - origin.y, box.width, box.height] });
          walk(child, childPath);
        });
      };
      walk(math, '');
      return { math: { width: origin.width, height: origin.height, baseline: probe.top - origin.top }, elements };
    });

    if (layoutDir) {
      mkdirSync(layoutDir, { recursive: true });
      const result = {
        [browserName]: version,
        fontSize,
        math: { width: round(layout.math.width), height: round(layout.math.height), baseline: round(layout.math.baseline) },
        elements: layout.elements.map((element) => ({ path: element.path, tag: element.tag, box: element.box.map(round) })),
      };
      writeFileSync(join(layoutDir, `${name}.layout.json`), JSON.stringify(result, null, 2) + '\n');
    }

    if (pngDir) {
      mkdirSync(pngDir, { recursive: true });
      const math = await page.$('math');
      // WebDriver BiDi (Firefox) cannot omit the background, so the Firefox picture is on white
      await math.screenshot({ path: join(pngDir, `${name}.png`), omitBackground: browserName === 'chrome' });
    }

    await page.close();
    console.log(`${name}: ${version}`);
  }
} finally {
  await browser.close();
}
