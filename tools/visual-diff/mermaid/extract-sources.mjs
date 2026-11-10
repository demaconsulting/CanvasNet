// extract-sources.mjs - writes the original sources of the pinned mermaid package (the TypeScript, JavaScript, jison
// and YAML files that the published bundle was built from) to a folder, so a developer can read the code that the
// CanvasNet.Mermaid port follows.
//
// Developer tooling of tools/visual-diff; see the README there. Usage:
//
//     node extract-sources.mjs [--modules <folder>] [--out <folder>]
//
// <modules> is a folder that holds node_modules/mermaid (default: the repository root). <out> is the folder the
// sources are written to (default: artifacts/mermaid-src under the repository root; the artifacts folder is ignored by
// git, so the sources are never committed). The sources come from the sourcesContent of dist/mermaid.js.map, which
// lists every original file of the release; nothing is downloaded.

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, normalize, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

// The repository root is three folders above tools/visual-diff/mermaid
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

// Read the two optional arguments
const args = process.argv.slice(2);
let modules = root;
let out = join(root, 'artifacts', 'mermaid-src');
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--modules' && i + 1 < args.length) {
    modules = resolve(args[++i]);
  } else if (args[i] === '--out' && i + 1 < args.length) {
    out = resolve(args[++i]);
  } else {
    console.error('usage: node extract-sources.mjs [--modules <folder>] [--out <folder>]');
    process.exit(2);
  }
}

// The source map lists every original file of the bundle together with its text
const packageFolder = join(modules, 'node_modules', 'mermaid');
const version = JSON.parse(readFileSync(join(packageFolder, 'package.json'), 'utf8')).version;
const map = JSON.parse(readFileSync(join(packageFolder, 'dist', 'mermaid.js.map'), 'utf8'));

// Each source is written under its path with the leading ../ steps removed; a path that would leave the output folder
// after normalization is skipped, so a malformed map cannot write elsewhere
let written = 0;
for (let i = 0; i < map.sources.length; i++) {
  const text = map.sourcesContent?.[i];
  if (typeof text !== 'string') {
    continue;
  }

  const relative = normalize(map.sources[i].replace(/^webpack:\/\//, '').replace(/^(\.\.\/)+/, ''));
  const target = resolve(out, relative);
  if (!target.startsWith(resolve(out) + sep)) {
    continue;
  }

  mkdirSync(dirname(target), { recursive: true });
  writeFileSync(target, text);
  written++;
}

console.log(`mermaid ${version}: wrote ${written} of ${map.sources.length} sources to ${out}`);
