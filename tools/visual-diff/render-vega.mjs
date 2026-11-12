// render-vega.mjs - draws a Vega-Lite specification to SVG with the reference implementation (headless: no browser
// and no native canvas module, vega's own SVG renderer through view.toSVG()).
//
// Developer tooling of tools/visual-diff; see the README there. Usage:
//
//     node render-vega.mjs <spec.vl.json> <out.svg> <modules-folder>
//
// <modules-folder> is a folder that holds node_modules/vega and node_modules/vega-lite (compare-with-vega.ps1
// installs them). Local data files of the specification are read relative to the folder of the specification; a
// remote URL is not fetched (the harness is for specifications with inline or local data).

import { createRequire } from 'node:module';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const [specPath, outPath, modulesFolder] = process.argv.slice(2);
if (!specPath || !outPath || !modulesFolder) {
  console.error('usage: node render-vega.mjs <spec.vl.json> <out.svg> <modules-folder>');
  process.exit(2);
}

// An ES module does not honor NODE_PATH, so the packages are resolved from the modules folder explicitly, and
// loaded with import() (vega has a top-level await, which require() cannot load).
const resolver = createRequire(join(resolve(modulesFolder), 'noop.js'));
const vega = await import(pathToFileURL(resolver.resolve('vega')).href);
const vegaLite = await import(pathToFileURL(resolver.resolve('vega-lite')).href);

const spec = JSON.parse(readFileSync(specPath, 'utf8'));
const compiled = vegaLite.compile(spec, { logger: { level: () => {}, warn: () => {}, info: () => {}, debug: () => {}, error: console.error } }).spec;

// Relative file names are read from the folder of the specification (the way the canvasnet tool reads them).
const loader = vega.loader({ baseURL: dirname(resolve(specPath)) + '/', mode: 'file' });
const view = new vega.View(vega.parse(compiled), { renderer: 'none', loader });
await view.runAsync();
const svg = await view.toSVG();

// canvasnet's SVG reader takes plain numbers for lengths: vega writes font-size="10px", so the unit is dropped (user units
// are pixels in a standalone SVG, so the picture does not change).
writeFileSync(outPath, svg.replace(/font-size="([0-9.]+)px"/g, 'font-size="$1"'));
console.log(`vega ${vega.version}, vega-lite ${vegaLite.version}: wrote ${outPath}`);
