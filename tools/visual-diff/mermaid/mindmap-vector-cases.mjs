// mindmap-vector-cases.mjs - the mindmap texts of gen-diagram-vectors.mjs --type mindmap. Each case is { name, spec,
// text } (and an optional site configuration). The expectations are not written here: the generator records what the
// pinned mermaid makes of each text.
//
// Attribution: many texts are the inputs of the mindmap tests at the pinned version: mermaid's
// packages/mermaid/src/diagrams/mindmap/mindmap.spec.ts and packages/mermaid/src/diagrams/mindmap/mindmapDb.getData.test.ts
// (MIT, Copyright (c) 2014 - 2022 Knut Sveidqvist, the notice of LICENSE-mermaid.txt). Their spec field names the
// upstream file and test. Only the inputs are taken; the database tests call addNode directly, so their calls are
// written here as the mindmap text that makes the same calls (the indentation is the level). The upstream assertions are
// replaced by the facts mermaid records. The cases whose spec field is "original" cover the lexer's rule order and
// quirks, every node bracket and end, strings, decorations, the indentation rules, sections, comments, entities and the
// errors, and the "fixture" cases are the mindmap fixtures of the tests (their frontmatter is removed by mermaid's
// preprocessing; its configuration is not applied).

import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const provenance = 'The inputs are those of mermaid\'s mindmap specs (diagrams/mindmap/mindmap.spec.ts and diagrams/mindmap/mindmapDb.getData.test.ts; MIT), named in each case\'s spec field, plus original texts and the mindmap fixtures.';

const lines = (...rows) => rows.join('\n');
const parserSpec = (test) => `diagrams/mindmap/mindmap.spec.ts: ${test}`;
const dbSpec = (test) => `diagrams/mindmap/mindmapDb.getData.test.ts: ${test}`;

// The mindmap fixtures of the tests (original), read from the fixture folder when it exists
const fixtureFolder = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'test',
  'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'mindmap');
const fixtures = existsSync(fixtureFolder)
  ? readdirSync(fixtureFolder).filter((f) => f.endsWith('.mmd')).sort().map((f) => ({
    name: `fixture-${f.replace(/\.mmd$/, '')}`,
    spec: `fixture mindmap/${f}`,
    text: readFileSync(join(fixtureFolder, f), 'utf8')
  }))
  : [];

// A mindmap of rows, each row indented by its level (two spaces a level), as the database tests' addNode calls
const tree = (...rows) => lines('mindmap', ...rows.map(([level, text]) => ' '.repeat(2 * level) + text));

// The node brackets: every start with every end the NODE condition knows
const starts = ['[', '(', '((', ')', '))', '{{', '(-', '-)'];
const ends = [']', ')', '))', '(', '((', '}}', '(-', '-)'];
const brackets = [];
for (const [i, start] of starts.entries()) {
  for (const [j, end] of ends.entries()) {
    brackets.push({ name: `bracket-${i}-${j}`, spec: 'original', text: lines('mindmap', `  id${start}text${end}`) });
  }
}

// The children of the root, more than the 11 sections
const manyChildren = (count) => lines('mindmap', 'root', ...Array.from({ length: count }, (_, i) => `  c${i}`));

export const cases = [
  // ----------------------------------------------------------------------- diagrams/mindmap/mindmap.spec.ts
  { name: 'spec-mmp-1', spec: parserSpec('MMP-1 should handle a simple root definition abc122'), text: 'mindmap\n    root' },
  { name: 'spec-mmp-2', spec: parserSpec('MMP-2 should handle a hierarchical mindmap definition'), text: 'mindmap\n    root\n      child1\n      child2\n ' },
  { name: 'spec-3', spec: parserSpec('3 should handle a simple root definition with a shape and without an id abc123'), text: 'mindmap\n    (root)' },
  { name: 'spec-mmp-4', spec: parserSpec('MMP-4 should handle a deeper hierarchical mindmap definition'), text: 'mindmap\n    root\n      child1\n        leaf1\n      child2' },
  { name: 'spec-5', spec: parserSpec('5 Multiple roots are illegal'), text: 'mindmap\n    root\n    fakeRoot' },
  { name: 'spec-mmp-6', spec: parserSpec('MMP-6 real root in wrong place'), text: 'mindmap\n          root\n        fakeRoot\n    realRootWrongPlace' },
  { name: 'spec-mmp-7', spec: parserSpec('MMP-7 should handle an id and type for a node definition'), text: 'mindmap\n    root[The root]\n      ' },
  { name: 'spec-mmp-8', spec: parserSpec('MMP-8 should handle an id and type for a node definition'), text: 'mindmap\n    root\n      theId(child1)' },
  { name: 'spec-mmp-9', spec: parserSpec('MMP-9 should handle an id and type for a node definition'), text: 'mindmap\nroot\n      theId(child1)' },
  { name: 'spec-mmp-10', spec: parserSpec('MMP-10 multiple types (circle)'), text: 'mindmap\n root((the root))\n ' },
  { name: 'spec-mmp-11', spec: parserSpec('MMP-11 multiple types (cloud)'), text: 'mindmap\n root)the root(\n' },
  { name: 'spec-mmp-12', spec: parserSpec('MMP-12 multiple types (bang)'), text: 'mindmap\n root))the root((\n' },
  { name: 'spec-mmp-12-a', spec: parserSpec('MMP-12-a multiple types (hexagon)'), text: 'mindmap\n root{{the root}}\n' },
  { name: 'spec-mmp-13', spec: parserSpec('MMP-13 should be possible to set an icon for the node'), text: 'mindmap\n    root[The root]\n    ::icon(bomb)\n    ' },
  { name: 'spec-mmp-14', spec: parserSpec('MMP-14 should be possible to set classes for the node'), text: 'mindmap\n    root[The root]\n    :::m-4 p-8\n    ' },
  { name: 'spec-mmp-15', spec: parserSpec('MMP-15 should be possible to set both classes and icon for the node'), text: 'mindmap\n    root[The root]\n    :::m-4 p-8\n    ::icon(bomb)\n    ' },
  { name: 'spec-mmp-16', spec: parserSpec('MMP-16 should be possible to set both classes and icon for the node'), text: 'mindmap\n    root[The root]\n    ::icon(bomb)\n    :::m-4 p-8\n    ' },
  { name: 'spec-mmp-17', spec: parserSpec('MMP-17 should be possible to use node syntax in the descriptions'), text: 'mindmap\n    root["String containing []"]\n' },
  { name: 'spec-mmp-18', spec: parserSpec('MMP-18 should be possible to use node syntax in the descriptions in children'), text: 'mindmap\n    root["String containing []"]\n      child1["String containing ()"]\n' },
  { name: 'spec-mmp-19', spec: parserSpec('MMP-19 should be possible to have a child after a class assignment'), text: 'mindmap\n  root(Root)\n    Child(Child)\n    :::hot\n      a(a)\n      b[New Stuff]' },
  { name: 'spec-mmp-20', spec: parserSpec('MMP-20 should be possible to have meaningless empty rows in a mindmap abc124'), text: 'mindmap\n  root(Root)\n    Child(Child)\n      a(a)\n\n      b[New Stuff]' },
  { name: 'spec-mmp-21', spec: parserSpec('MMP-21 should be possible to have comments in a mindmap'), text: 'mindmap\n  root(Root)\n    Child(Child)\n      a(a)\n\n      %% This is a comment\n      b[New Stuff]' },
  { name: 'spec-mmp-22', spec: parserSpec('MMP-22 should be possible to have comments at the end of a line'), text: 'mindmap\n  root(Root)\n    Child(Child)\n      a(a) %% This is a comment\n      b[New Stuff]' },
  { name: 'spec-mmp-23', spec: parserSpec('MMP-23 Rows with only spaces should not interfere'), text: 'mindmap\nroot\n A\n \n\n B' },
  { name: 'spec-mmp-24', spec: parserSpec('MMP-24 Handle rows above the mindmap declarations'), text: '\n \nmindmap\nroot\n A\n \n\n B' },
  { name: 'spec-mmp-25', spec: parserSpec('MMP-25 Handle rows above the mindmap declarations, no space'), text: '\n\n\nmindmap\nroot\n A\n \n\n B' },

  // ------------------------------------------------------------ diagrams/mindmap/mindmapDb.getData.test.ts
  { name: 'db-simple', spec: dbSpec('should return structured data for simple mindmap'), text: tree([0, 'root[Root Node]'], [1, 'child1[Child 1]'], [1, 'child2[Child 2]']) },
  { name: 'db-hierarchical', spec: dbSpec('should return structured data for hierarchical mindmap'), text: tree([0, 'root'], [1, 'child1'], [2, 'grandchild1'], [2, 'grandchild2'], [1, 'child2']) },
  { name: 'db-rectangle', spec: dbSpec('should preserve node properties in processed data'), text: tree([0, 'root[Root Node]']) },
  { name: 'db-unique-edge-ids', spec: dbSpec('should generate unique edge IDs'), text: tree([0, 'root'], [1, 'child1'], [1, 'child2'], [1, 'child3']) },
  { name: 'db-missing-optional', spec: dbSpec('should handle nodes with missing optional properties'), text: tree([0, 'root']) },
  { name: 'db-sections', spec: dbSpec('should assign correct section classes based on sibling position'), text: tree([0, 'A'], [1, 'a0'], [2, 'aa0'], [1, 'a1'], [2, 'aaa'], [1, 'a2']) },
  { name: 'db-custom-classes', spec: dbSpec('should preserve custom classes while adding section classes'), text: tree([0, 'root[Root Node]'], [0, ':::custom-root'], [1, 'child[Child Node]'], [1, ':::custom-child']) },
  { name: 'db-no-fake-root', spec: dbSpec('should not create any fake root nodes'), text: tree([0, 'A'], [1, 'a0'], [1, 'a1']) },
  { name: 'db-edge-sections', spec: dbSpec('should assign correct section classes to edges'), text: tree([0, 'A'], [1, 'a0'], [2, 'aa0'], [1, 'a1'], [2, 'aaa'], [1, 'a2']) },
  { name: 'db-wrap-sections', spec: dbSpec('should wrap section numbers when there are more than 11 level 2 nodes'), text: lines('mindmap', 'root[Example]', ...Array.from({ length: 15 }, (_, i) => `  child${i + 1}[${i + 1}]`)) },

  // -------------------------------------------------------------------------------------- the keyword
  { name: 'keyword-alone', spec: 'original', text: 'mindmap' },
  { name: 'keyword-line-feed', spec: 'original', text: 'mindmap\n' },
  { name: 'keyword-blank-lines', spec: 'original', text: 'mindmap\n\n  \n\n' },
  { name: 'keyword-spaces-after', spec: 'original', text: 'mindmap   \nroot' },
  { name: 'keyword-root-same-line', spec: 'original', text: 'mindmap root\n  child' },
  { name: 'keyword-glued', spec: 'original', text: 'mindmap2\n  root' },
  { name: 'keyword-dash', spec: 'original', text: 'mindmap-x\n  root' },
  { name: 'keyword-upper', spec: 'original', text: 'MINDMAP\n  root' },
  { name: 'keyword-upper-in-document', spec: 'original', text: 'mindmap\n  root\n  MINDMAP' },
  { name: 'keyword-mixed-in-document', spec: 'original', text: 'mindmap\nroot\n  MindMap' },
  { name: 'keyword-leading-spaces', spec: 'original', text: '   mindmap\n  root' },
  { name: 'keyword-blank-then-root', spec: 'original', text: 'mindmap\n\nroot' },
  { name: 'keyword-two-blank-then-root', spec: 'original', text: 'mindmap\n\n\nroot\n  a' },
  { name: 'keyword-spaces-line-then-root', spec: 'original', text: 'mindmap\n   \nroot' },

  // ------------------------------------------------------------------------------------------- nodes
  { name: 'node-id-only', spec: 'original', text: lines('mindmap', 'root') },
  { name: 'node-id-spaces', spec: 'original', text: lines('mindmap', 'root node with  spaces  ') },
  { name: 'node-id-punctuation', spec: 'original', text: lines('mindmap', 'a:b;c,d.e!f?g&h*i+j=k|l~m^n$o#p@q\'r"s<t>u/v\\w_x-y') },
  { name: 'node-id-closing-bracket', spec: 'original', text: lines('mindmap', 'a]b') },
  { name: 'node-id-and-descr', spec: 'original', text: lines('mindmap', 'root[The root]', '  child(The child)') },
  { name: 'node-without-id', spec: 'original', text: lines('mindmap', '[Square]', '  (Rounded)', '  ((Circle))', '  ))Bang((', '  )Cloud(', '  {{Hexagon}}') },
  { name: 'node-with-id-all-shapes', spec: 'original', text: lines('mindmap', 'r[Square]', '  a(Rounded)', '  b((Circle))', '  c))Bang((', '  d)Cloud(', '  e{{Hexagon}}', '  f') },
  { name: 'node-id-space-before-bracket', spec: 'original', text: lines('mindmap', 'root [The root]') },
  { name: 'node-descr-spaces', spec: 'original', text: lines('mindmap', 'root[  padded text  ]') },
  { name: 'node-descr-multiline', spec: 'original', text: lines('mindmap', 'root[first', 'second]', '  child') },
  { name: 'node-descr-brace', spec: 'original', text: lines('mindmap', 'root[a}b]') },
  { name: 'node-descr-brace-end', spec: 'original', text: lines('mindmap', 'root{{a}b}}') },
  { name: 'node-descr-open-paren', spec: 'original', text: lines('mindmap', 'root[a(b]') },
  { name: 'node-descr-empty', spec: 'original', text: lines('mindmap', 'root[]') },
  { name: 'node-unterminated', spec: 'original', text: lines('mindmap', 'root[text') },
  { name: 'node-unterminated-next-line', spec: 'original', text: lines('mindmap', 'root[text', '  child') },
  { name: 'node-start-alone', spec: 'original', text: lines('mindmap', 'root[') },
  { name: 'node-two-on-a-line', spec: 'original', text: lines('mindmap', 'root[a] b') },
  { name: 'node-two-brackets', spec: 'original', text: lines('mindmap', 'root[a][b]') },
  { name: 'node-text-after-bracket', spec: 'original', text: lines('mindmap', 'root[a]b') },
  { name: 'node-open-brace', spec: 'original', text: lines('mindmap', 'root', '  a{b') },
  { name: 'node-close-brace', spec: 'original', text: lines('mindmap', 'root', '  a}b') },
  { name: 'node-single-brace-start', spec: 'original', text: lines('mindmap', 'root', '  {b}') },
  { name: 'node-dash-paren-id', spec: 'original', text: lines('mindmap', 'root', '  a-)b(-') },
  { name: 'node-paren-dash', spec: 'original', text: lines('mindmap', 'root', '  a(-b-)') },
  { name: 'node-bang-in-text', spec: 'original', text: lines('mindmap', 'root))a ((b((') },
  { name: 'node-circle-text-paren', spec: 'original', text: lines('mindmap', 'root((a) b))') },
  ...brackets,

  // ---------------------------------------------------------------------------------------- strings
  { name: 'string-quoted', spec: 'original', text: lines('mindmap', 'root["quoted text"]') },
  { name: 'string-quoted-brackets', spec: 'original', text: lines('mindmap', 'root["a [b] (c) {d}"]') },
  { name: 'string-quoted-empty', spec: 'original', text: lines('mindmap', 'root[""]') },
  { name: 'string-quoted-unterminated', spec: 'original', text: lines('mindmap', 'root["open]', '  child') },
  { name: 'string-quoted-then-text', spec: 'original', text: lines('mindmap', 'root["a" b]') },
  { name: 'string-quoted-inner-quote', spec: 'original', text: lines('mindmap', 'root[say "hi"]') },
  { name: 'string-quoted-multiline', spec: 'original', text: lines('mindmap', 'root["line one', 'line two"]') },
  { name: 'string-markdown', spec: 'original', text: lines('mindmap', 'root["`**Bold** and *italic*`"]') },
  { name: 'string-markdown-multiline', spec: 'original', text: lines('mindmap', 'root["`first line', 'second line`"]') },
  { name: 'string-markdown-quote-inside', spec: 'original', text: lines('mindmap', 'root["`a " b`"]') },
  { name: 'string-markdown-backtick-inside', spec: 'original', text: lines('mindmap', 'root["`a ` b`"]') },
  { name: 'string-markdown-empty', spec: 'original', text: lines('mindmap', 'root["``"]') },
  { name: 'string-markdown-circle', spec: 'original', text: lines('mindmap', 'root(("`**round**`"))') },
  { name: 'string-backtick-unquoted', spec: 'original', text: lines('mindmap', 'root[`not markdown`]') },
  { name: 'string-markdown-without-id', spec: 'original', text: lines('mindmap', '["`**no id**`"]') },
  { name: 'string-quoted-without-id', spec: 'original', text: lines('mindmap', '("only text")') },

  // ------------------------------------------------------------------------------------ decorations
  { name: 'icon-simple', spec: 'original', text: lines('mindmap', 'root', '  ::icon(fa fa-book)') },
  { name: 'icon-spaced', spec: 'original', text: lines('mindmap', 'root', '  ::icon( spaced icon )') },
  { name: 'icon-empty', spec: 'original', text: lines('mindmap', 'root', '  ::icon()', '  child') },
  { name: 'icon-upper', spec: 'original', text: lines('mindmap', 'root', '::ICON(mdi mdi-skull)') },
  { name: 'icon-unterminated', spec: 'original', text: lines('mindmap', 'root', '  ::icon(fa fa-book', '  child') },
  { name: 'icon-multiline', spec: 'original', text: lines('mindmap', 'root', '  ::icon(fa', 'fa-book)') },
  { name: 'icon-same-line', spec: 'original', text: lines('mindmap', 'root[a]::icon(fa fa-book)') },
  { name: 'icon-after-id', spec: 'original', text: lines('mindmap', 'root::icon(fa fa-book)') },
  { name: 'icon-text-after', spec: 'original', text: lines('mindmap', 'root', '  ::icon(fa fa-book) more') },
  { name: 'icon-last-node', spec: 'original', text: lines('mindmap', 'root', '  a', '    b', '  ::icon(fa fa-star)') },
  { name: 'icon-twice', spec: 'original', text: lines('mindmap', 'root', '::icon(one)', '::icon(two)') },
  { name: 'icon-before-node', spec: 'original', text: lines('mindmap', '::icon(fa fa-book)', 'root') },
  { name: 'icon-markup', spec: 'original', text: lines('mindmap', 'root', '  ::icon(<b>x</b>)') },
  { name: 'icon-script', spec: 'original', text: lines('mindmap', 'root', '  ::icon(<script>x</script>)') },
  { name: 'class-simple', spec: 'original', text: lines('mindmap', 'root', '  :::urgent') },
  { name: 'class-several', spec: 'original', text: lines('mindmap', 'root', '  :::urgent large  ') },
  { name: 'class-empty', spec: 'original', text: lines('mindmap', 'root', '  :::', '  child') },
  { name: 'class-before-node', spec: 'original', text: lines('mindmap', ':::urgent', 'root') },
  { name: 'class-same-line', spec: 'original', text: lines('mindmap', 'root[a]:::urgent') },
  { name: 'class-after-id', spec: 'original', text: lines('mindmap', 'root:::urgent') },
  { name: 'class-four-colons', spec: 'original', text: lines('mindmap', 'root', '  ::::urgent') },
  { name: 'class-with-comment', spec: 'original', text: lines('mindmap', 'root', '  :::urgent %% not a comment') },
  { name: 'class-twice', spec: 'original', text: lines('mindmap', 'root', '  :::one', '  :::two') },
  { name: 'class-and-icon', spec: 'original', text: lines('mindmap', 'root', '  a', '  :::c1 c2', '  ::icon(fa fa-a)', '    b', '    ::icon(fa fa-b)') },
  { name: 'class-at-end', spec: 'original', text: 'mindmap\nroot\n  :::' },
  { name: 'class-markup', spec: 'original', text: lines('mindmap', 'root', '  :::<i>c</i>') },
  { name: 'decoration-at-root-level', spec: 'original', text: lines('mindmap', '  root', '  ::icon(fa fa-a)', '  :::c', '    child') },
  { name: 'decoration-double-colon-other', spec: 'original', text: lines('mindmap', 'root', '  ::other(x)') },

  // ------------------------------------------------------------------------------------- indentation
  { name: 'indent-odd', spec: 'original', text: lines('mindmap', 'root', ' a', '   b', '     c', '  d') },
  { name: 'indent-tabs', spec: 'original', text: lines('mindmap', 'root', '\ta', '\t\tb', '\tc') },
  { name: 'indent-mixed', spec: 'original', text: lines('mindmap', 'root', '\t a', '  b', '\t\tc') },
  { name: 'indent-base-level', spec: 'original', text: lines('mindmap', '      root', '        a', '         b') },
  { name: 'indent-below-root', spec: 'original', text: lines('mindmap', '    root', '      a', '  b') },
  { name: 'indent-sibling-of-root', spec: 'original', text: lines('mindmap', '  root', '    a', '  b') },
  { name: 'indent-back-to-middle', spec: 'original', text: lines('mindmap', 'root', '    a', '        b', '  c', '      d', '   e') },
  { name: 'indent-jump', spec: 'original', text: lines('mindmap', 'root', '          deep', '  shallow', '    middle') },
  { name: 'indent-no-break-space', spec: 'original', text: lines('mindmap', 'root', '\u00a0\u00a0a', '\u00a0\u00a0\u00a0\u00a0b') },
  { name: 'indent-trailing-spaces', spec: 'original', text: lines('mindmap', 'root   ', '  a   ', '    b   ') },
  { name: 'indent-whitespace-lines', spec: 'original', text: lines('mindmap', 'root', '   ', '  a', '\t', '    b', '      ', '') },
  { name: 'indent-crlf', spec: 'original', text: 'mindmap\r\nroot\r\n  a\r\n    b\r\n  c\r\n' },
  { name: 'indent-cr-only', spec: 'original', text: 'mindmap\rroot\r  a' },
  { name: 'indent-deep-chain', spec: 'original', text: lines('mindmap', ...Array.from({ length: 40 }, (_, i) => `${' '.repeat(i)}n${i}`)) },
  { name: 'indent-wide', spec: 'original', text: lines('mindmap', 'root', ...Array.from({ length: 30 }, (_, i) => `  leaf${i}`)) },
  { name: 'indent-spaces-alone', spec: 'original', text: lines('mindmap', 'root', '   ', '  a') },

  // ---------------------------------------------------------------------------------------- sections
  { name: 'sections-eleven', spec: 'original', text: manyChildren(11) },
  { name: 'sections-twelve', spec: 'original', text: manyChildren(12) },
  { name: 'sections-twenty-three', spec: 'original', text: manyChildren(23) },
  { name: 'sections-inherit', spec: 'original', text: lines('mindmap', 'root', '  a', '    a1', '      a11', '  b', '    b1', '  c') },
  { name: 'sections-depth-classes', spec: 'original', text: lines('mindmap', 'root', '    a', '        b', '          c') },
  { name: 'sections-single-node', spec: 'original', text: lines('mindmap', 'only') },

  // ---------------------------------------------------------------------------------------- comments
  { name: 'comment-own-line', spec: 'original', text: lines('mindmap', '%% before', 'root', '  %% between', '  a') },
  { name: 'comment-indented-line', spec: 'original', text: lines('mindmap', 'root', '      %% indented', '  a') },
  { name: 'comment-end-of-id', spec: 'original', text: lines('mindmap', 'root %% kept in the id', '  a') },
  { name: 'comment-in-brackets', spec: 'original', text: lines('mindmap', 'root[text %% kept]') },
  { name: 'comment-after-bracket', spec: 'original', text: lines('mindmap', 'root[text] %% after', '  a') },
  { name: 'comment-after-keyword', spec: 'original', text: lines('mindmap %% keyword', 'root') },
  { name: 'comment-only', spec: 'original', text: lines('mindmap', '%% nothing else') },
  { name: 'comment-empty-percent', spec: 'original', text: lines('mindmap', 'root', '  %%', '  a') },
  { name: 'comment-directive-like', spec: 'original', text: lines('%%{init: {"theme": "forest"}}%%', 'mindmap', 'root', '  a') },
  { name: 'comment-frontmatter', spec: 'original', text: lines('---', 'title: A mindmap', 'config:', '  mindmap:', '    padding: 4', '---', 'mindmap', 'root', '  a') },

  // ------------------------------------------------------------------------------------ entities, markup
  { name: 'entity-named', spec: 'original', text: lines('mindmap', 'root[Fish #amp; chips]', '  a[#lt;tag#gt;]') },
  { name: 'entity-numeric', spec: 'original', text: lines('mindmap', 'root[#9829; love]') },
  { name: 'entity-in-id', spec: 'original', text: lines('mindmap', 'A#amp;B') },
  { name: 'markup-br', spec: 'original', text: lines('mindmap', 'root[line one<br/>line two]', '  a[one<br>two]') },
  { name: 'markup-bold', spec: 'original', text: lines('mindmap', 'root[<b>bold</b> text]') },
  { name: 'markup-script', spec: 'original', text: lines('mindmap', 'root[<script>alert(1)</script>safe]') },
  { name: 'markup-script-id', spec: 'original', text: lines('mindmap', '<script>x</script>y') },
  { name: 'markup-angle-in-id', spec: 'original', text: lines('mindmap', 'a < b', '  c > d') },
  { name: 'markup-fa', spec: 'original', text: lines('mindmap', 'root[fa:fa-book Read]') },
  { name: 'text-unicode', spec: 'original', text: lines('mindmap', 'naïve café 根', '  子[日本語]', '  emoji 🚀') },
  { name: 'text-long-id', spec: 'original', text: lines('mindmap', 'x'.repeat(300)) },
  { name: 'text-acc-title', spec: 'original', text: lines('mindmap', 'accTitle: An accessible title', '  accDescr: A description') },
  { name: 'text-title', spec: 'original', text: lines('mindmap', 'title A title', '  a') },

  // -------------------------------------------------------------------------------------- site configuration
  { name: 'site-padding-width', spec: 'original', site: { mindmap: { padding: 5, maxNodeWidth: 120 } }, text: lines('mindmap', 'root', '  a[Square]', '  b(Rounded)', '  c{{Hexagon}}', '  d((Circle))', '  e))Bang((', '  f)Cloud(') },
  { name: 'site-look-hand-drawn', spec: 'original', site: { look: 'handDrawn' }, text: lines('mindmap', 'root', '  a') },

  // ------------------------------------------------------------------------------------------- the fixtures
  ...fixtures
];
