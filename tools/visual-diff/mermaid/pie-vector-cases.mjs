// pie-vector-cases.mjs - the pie chart texts of gen-diagram-vectors.mjs --type pie. Each case is { name, spec, text }
// (and an optional site configuration). The expectations are not written here: the generator records what the pinned
// mermaid makes of each text.
//
// Attribution: many texts are the inputs of the pie tests at the pinned version: mermaid's
// packages/mermaid/src/diagrams/pie/pie.spec.ts (MIT, Copyright (c) 2014 - 2022 Knut Sveidqvist, the notice of
// LICENSE-mermaid.txt) and @mermaid-js/parser's packages/parser/tests/pie.test.ts (MIT, Copyright (c) 2023 Yokozuna59,
// the notice of LICENSE-mermaid-parser.txt). Their spec field names the upstream file and test. Only the texts are taken
// (each text that a test gives to the parser, once); the upstream assertions are replaced by the facts mermaid records.
// The parser tests call the Langium parser directly; here every text goes through mermaid's preprocessing and
// detection first, as a diagram does. The cases whose spec field is "original" cover the lexer's first-match order, the
// value converters, numbers, escapes, comments, duplicates and errors, and the "fixture" cases are the pie fixtures of
// the tests (their frontmatter is removed by mermaid's preprocessing; its configuration is not applied).

import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const provenance = 'The inputs are those of mermaid\'s pie spec (diagrams/pie/pie.spec.ts; MIT) and of @mermaid-js/parser\'s pie tests (tests/pie.test.ts; MIT), named in each case\'s spec field, plus original texts and the pie fixtures.';

const lines = (...rows) => rows.join('\n');
const pieSpec = (test) => `diagrams/pie/pie.spec.ts: ${test}`;
const parserSpec = (test) => `@mermaid-js/parser tests/pie.test.ts: ${test}`;

// The pie fixtures of the tests (original), read from the fixture folder
const fixtureFolder = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'test',
  'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'pie');
const fixtures = readdirSync(fixtureFolder).filter((f) => f.endsWith('.mmd')).sort().map((f) => ({
  name: `fixture-${f.replace(/\.mmd$/, '')}`,
  spec: `fixture pie/${f}`,
  text: readFileSync(join(fixtureFolder, f), 'utf8')
}));

// The texts of an it.each of the parser tests, one case each
const each = (prefix, test, texts) => texts.map((text, i) => ({ name: `${prefix}-${i + 1}`, spec: parserSpec(test), text }));

export const cases = [
  // ------------------------------------------------------------------------------------- diagrams/pie/pie.spec.ts
  { name: 'spec-very-simple', spec: pieSpec('should handle very simple pie'), text: 'pie\n      "ash": 100\n      ' },
  { name: 'spec-simple', spec: pieSpec('should handle simple pie'), text: 'pie\n      "ash" : 60\n      "bat" : 40\n      ' },
  { name: 'spec-show-data', spec: pieSpec('should handle simple pie with showData'), text: 'pie showData\n      "ash" : 60\n      "bat" : 40\n      ' },
  { name: 'spec-comments', spec: pieSpec('should handle simple pie with comments'), text: 'pie\n      %% comments\n      "ash" : 60\n      "bat" : 40\n      ' },
  { name: 'spec-title', spec: pieSpec('should handle simple pie with a title'), text: 'pie title a 60/40 pie\n      "ash" : 60\n      "bat" : 40\n      ' },
  { name: 'spec-acc-title', spec: pieSpec('should handle simple pie with an acc title (accTitle)'), text: 'pie title a neat chart\n      accTitle: a neat acc title\n      "ash" : 60\n      "bat" : 40\n      ' },
  { name: 'spec-acc-descr', spec: pieSpec('should handle simple pie with an acc description (accDescr)'), text: 'pie title a neat chart\n      accDescr: a neat description\n      "ash" : 60\n      "bat" : 40\n      ' },
  { name: 'spec-acc-descr-multiline', spec: pieSpec('should handle simple pie with a multiline acc description (accDescr)'), text: 'pie title a neat chart\n      accDescr {\n        a neat description\n        on multiple lines\n      }\n      "ash" : 60\n      "bat" : 40\n    ' },
  { name: 'spec-positive-decimal', spec: pieSpec('should handle simple pie with positive decimal'), text: 'pie\n      "ash" : 60.67\n      "bat" : 40\n      ' },
  { name: 'error-spec-negative-decimal', spec: pieSpec('should handle simple pie with negative decimal'), text: 'pie\n        "ash" : -60.67\n        "bat" : 40.12\n        ' },
  { name: 'spec-zero-slice', spec: pieSpec('should handle simple pie with zero slice value'), text: 'pie title Default text position: Animal adoption\n        accTitle: simple pie char demo\n        accDescr: pie chart with 3 sections: dogs, cats, rats. Most are dogs.\n         "dogs" : 0\n        "rats" : 40.12\n      ' },
  { name: 'error-spec-negative-slice', spec: pieSpec('should handle simple pie with negative slice value'), text: 'pie title Default text position: Animal adoption\n        accTitle: simple pie char demo\n        accDescr: pie chart with 3 sections: dogs, cats, rats. Most are dogs.\n         "dogs" : -60.67\n        "rats" : 40.12\n    ' },
  { name: 'spec-unsafe-properties', spec: pieSpec('should handle unsafe properties'), text: 'pie title Unsafe props test\n        "__proto__" : 386\n        "constructor" : 85\n        "prototype" : 15' },

  // -------------------------------------------------------------------------- @mermaid-js/parser tests/pie.test.ts
  ...each('parser-regular-pie', 'should handle regular pie', ['pie', '  pie  ', '\tpie\t', '\n    \tpie\n    ']),
  ...each('parser-regular-show-data', 'should handle regular showData', ['pie showData', '  pie  showData  ', '\tpie\tshowData\t', '\n    pie\tshowData\n    ']),
  ...each('parser-title-same-line', 'should handle regular pie + title in same line', ['pie title sample title', '  pie  title sample title  ', '\tpie\ttitle sample title\t', 'pie\n        \ttitle sample title\n        ']),
  ...each('parser-title-different-line', 'should handle regular pie + title in different line', ['pie\n        title sample title', 'pie\n        title sample title\n        ']),
  ...each('parser-show-data-title', 'should handle regular pie + showData + title', ['pie showData title sample title', 'pie showData title sample title\n        ']),
  ...each('parser-show-data-title-different-line', 'should handle regular showData + title in different line', ['pie showData\n        title sample title', 'pie showData\n        title sample title\n        ']),
  ...each('parser-sections', 'should handle regular sections', ['pie\n        "GitHub":100\n        "GitLab":50', 'pie\n        "GitHub"   :   100\n        "GitLab"   :   50', 'pie\n        "GitHub"\t:\t100\n        "GitLab"\t:\t50', 'pie\n        \t"GitHub" \t : \t 100\n        \t"GitLab" \t : \t  50\n        ']),
  { name: 'parser-sections-show-data', spec: parserSpec('should handle sections with showData'), text: 'pie showData\n        "GitHub": 100\n        "GitLab": 50' },
  { name: 'parser-sections-title', spec: parserSpec('should handle sections with title'), text: 'pie title sample wow\n        "GitHub": 100\n        "GitLab": 50' },
  { name: 'parser-positive-decimal', spec: parserSpec('should handle value with positive decimal'), text: 'pie\n        "ash": 60.67\n        "bat": 40' },
  { name: 'parser-acc-title', spec: parserSpec('should handle sections with accTitle'), text: 'pie accTitle: sample wow\n        "GitHub": 100\n        "GitLab": 50' },
  { name: 'parser-acc-descr', spec: parserSpec('should handle sections with single line accDescr'), text: 'pie accDescr: sample wow\n        "GitHub": 100\n        "GitLab": 50' },
  { name: 'parser-acc-descr-multiline', spec: parserSpec('should handle sections with multi line accDescr'), text: 'pie accDescr {\n            sample wow\n        }\n        "GitHub": 100\n        "GitLab": 50' },

  // ------------------------------------------------------------------------------------------------- original
  // Strings: quotes, escapes, empty labels, labels over lines, entities
  { name: 'string-single-quotes', spec: 'original', text: lines('pie', "'single' : 1", '"double" : 2') },
  { name: 'string-escapes', spec: 'original', text: lines('pie', '"a\\nb\\tc\\q\\\\d\\"e\\b\\f\\v\\r\\0" : 2') },
  { name: 'string-single-quote-escapes', spec: 'original', text: lines('pie', "'it\\'s \"quoted\"' : 2") },
  { name: 'string-empty', spec: 'original', text: lines('pie', '"" : 1', "'' : 2") },
  { name: 'string-over-two-lines', spec: 'original', text: lines('pie', '"first', 'second" : 1') },
  { name: 'string-percent-signs', spec: 'original', text: lines('pie', '"50%% off" : 1') },
  { name: 'string-entities', spec: 'original', text: lines('pie', '"A #amp; B #35; C" : 1', '"<b>bold</b> &amp;" : 2') },
  { name: 'string-unicode', spec: 'original', text: lines('pie', '"Café ☕ naïve" : 3') },
  { name: 'error-string-backslash-newline', spec: 'original', text: lines('pie', '"a\\', 'b" : 1') },
  { name: 'error-string-unclosed', spec: 'original', text: lines('pie', '"open : 1') },
  { name: 'error-string-unquoted', spec: 'original', text: lines('pie', 'label : 1') },

  // Numbers: the forms NUMBER_PIE lexes, and Number()
  { name: 'number-zero', spec: 'original', text: lines('pie', '"a" : 0', '"b" : 0.0', '"c" : 3') },
  { name: 'number-negative-zero', spec: 'original', text: lines('pie', '"a" : -0', '"b" : -0.0', '"c" : 3') },
  { name: 'number-large', spec: 'original', text: lines('pie', '"a" : 100000000000000000000000', '"b" : 123456789012345678901234567890') },
  { name: 'number-huge', spec: 'original', text: lines('pie', '"a" : 1' + '0'.repeat(400), '"b" : 1') },
  { name: 'number-tiny-decimal', spec: 'original', text: lines('pie', '"a" : 0.000000000000000000001', '"b" : 0.1') },
  { name: 'number-many-decimals', spec: 'original', text: lines('pie', '"a" : 1.23456789012345678901', '"b" : 2.5') },
  { name: 'number-trailing-zeros', spec: 'original', text: lines('pie', '"a" : 10.500', '"b" : 7') },
  { name: 'error-number-leading-zeros', spec: 'original', text: lines('pie', '"a" : 1', '"b" : 007') },
  { name: 'error-number-leading-zero', spec: 'original', text: lines('pie', '"a" : 01') },
  { name: 'error-number-exponent', spec: 'original', text: lines('pie', '"a" : 1e3') },
  { name: 'error-number-leading-dot', spec: 'original', text: lines('pie', '"a" : .5') },
  { name: 'error-number-trailing-dot', spec: 'original', text: lines('pie', '"a" : 1.') },
  { name: 'error-number-two-dots', spec: 'original', text: lines('pie', '"a" : 1.5.') },
  { name: 'error-number-ten-dot', spec: 'original', text: lines('pie', '"a" : 10.') },
  { name: 'error-number-plus', spec: 'original', text: lines('pie', '"a" : +1') },
  { name: 'error-number-double-minus', spec: 'original', text: lines('pie', '"a" : --1') },
  { name: 'error-number-negative', spec: 'original', text: lines('pie', '"a" : 5', '"b" : -1') },
  { name: 'error-number-negative-decimal-label', spec: 'original', text: lines('pie', '"x \\"y\\"" : -0.5') },
  { name: 'error-number-missing', spec: 'original', text: lines('pie', '"a" :') },
  { name: 'error-number-word', spec: 'original', text: lines('pie', '"a" : ten') },
  { name: 'error-number-comma', spec: 'original', text: lines('pie', '"a" : 1,000') },

  // Sections: the colon, duplicates, sums, statement ends
  { name: 'section-no-spaces', spec: 'original', text: lines('pie', '"a":1', '"b":2') },
  { name: 'section-duplicate-first-wins', spec: 'original', text: lines('pie', '"x" : 5', '"x" : 7', '"y" : 1') },
  { name: 'error-section-duplicate-negative-later', spec: 'original', text: lines('pie', '"x" : 5', '"x" : -7') },
  { name: 'section-all-zero', spec: 'original', text: lines('pie', '"a" : 0', '"b" : 0') },
  { name: 'section-blank-lines', spec: 'original', text: lines('pie', '', '"a" : 1', '', '', '"b" : 2', '') },
  { name: 'section-crlf', spec: 'original', text: 'pie\r\n"a" : 1\r\n"b" : 2\r\n' },
  { name: 'error-section-missing-colon', spec: 'original', text: lines('pie', '"a" 1') },
  { name: 'error-section-two-on-a-line', spec: 'original', text: lines('pie', '"a" : 1 "b" : 2') },
  { name: 'error-section-semicolon', spec: 'original', text: lines('pie', '"a" : 1;') },
  { name: 'error-section-colon-first', spec: 'original', text: lines('pie', ': 5') },
  { name: 'error-section-number-only', spec: 'original', text: lines('pie', '5') },
  { name: 'error-section-label-only', spec: 'original', text: lines('pie', '"a"', '"b" : 1') },
  { name: 'error-section-title-after', spec: 'original', text: lines('pie', '"b" : 1 title c') },

  // The header: pie, showData and what may follow them
  { name: 'header-show-data-percent', spec: 'original', text: lines('pie showData%% comment', '"a" : 1') },
  { name: 'header-pie-percent', spec: 'original', text: lines('pie%% comment', '"a" : 1') },
  { name: 'header-pie-comment', spec: 'original', text: lines('pie %% comment', '"a" : 1') },
  { name: 'error-header-show-data-on-next-line', spec: 'original', text: lines('pie', 'showData', '"a" : 1') },
  { name: 'error-header-pieces', spec: 'original', text: lines('pieces', '"a" : 1') },
  { name: 'error-header-show-data-twice', spec: 'original', text: 'pie showData showData' },
  { name: 'error-header-show-data-glued', spec: 'original', text: lines('pie showDataX', '"a" : 1') },
  { name: 'error-header-show-data-lower', spec: 'original', text: lines('pie showdata', '"a" : 1') },
  { name: 'error-header-pie-twice', spec: 'original', text: lines('pie', 'pie', '"a" : 1') },
  { name: 'error-header-word', spec: 'original', text: lines('pie chart', '"a" : 1') },

  // Titles: the value converter, the %% look-ahead, the empty alternative
  { name: 'title-spaces-collapsed', spec: 'original', text: lines('pie', '  title   A   B\t\tC  ', '"a" : 2') },
  { name: 'title-comment', spec: 'original', text: lines('pie', 'title A %% not part of it', '"a" : 2') },
  { name: 'title-empty', spec: 'original', text: lines('pie title', '"a" : 2') },
  { name: 'title-only-comment', spec: 'original', text: lines('pie title %%x', '"a" : 2') },
  { name: 'title-twice-last-wins', spec: 'original', text: lines('pie', 'title First', 'title Second', '"a" : 2') },
  { name: 'title-after-sections', spec: 'original', text: lines('pie', '"a" : 2', 'title Late') },
  { name: 'title-html', spec: 'original', text: lines('pie title <b>Bold</b> & <script>x</script>', '"a" : 2') },
  { name: 'title-tab', spec: 'original', text: 'pie\ntitle\tTabbed title\n"a" : 2' },
  { name: 'title-pie-word', spec: 'original', text: lines('pie title pie showData', '"a" : 2') },
  { name: 'error-title-glued', spec: 'original', text: lines('pie', 'titles', '"a" : 2') },

  // Accessibility texts
  { name: 'acc-title-spaces', spec: 'original', text: lines('pie', '  accTitle :   a   title  ', '"a" : 2') },
  { name: 'acc-title-comment', spec: 'original', text: lines('pie accTitle: x %% y', '"a" : 2') },
  { name: 'acc-title-empty', spec: 'original', text: lines('pie', 'accTitle:', '"a" : 2') },
  { name: 'acc-descr-single-spaces', spec: 'original', text: lines('pie', 'accDescr:   one   two  ', '"a" : 2') },
  { name: 'acc-descr-multiline-indented', spec: 'original', text: lines('pie', 'accDescr {', '      first   line', '', '', '      second line   ', '   }', '"a" : 2') },
  { name: 'acc-descr-multiline-no-space', spec: 'original', text: lines('pie', 'accDescr{x}', '"a" : 2') },
  { name: 'acc-descr-multiline-brace-next-line', spec: 'original', text: lines('pie', 'accDescr', '{', '  text', '}', '"a" : 2') },
  { name: 'acc-descr-multiline-same-line', spec: 'original', text: lines('pie', 'accDescr { one line }', '"a" : 2') },
  { name: 'acc-all-three', spec: 'original', text: lines('pie title T', 'accTitle: A', 'accDescr: D', '"a" : 2') },
  { name: 'acc-html', spec: 'original', text: lines('pie', 'accTitle: <i>x</i>', 'accDescr: a <b>b</b>', '"a" : 2') },
  { name: 'error-acc-descr-trailing', spec: 'original', text: lines('pie', 'accDescr {', '  x', '  }   y') },
  { name: 'error-acc-descr-no-value', spec: 'original', text: lines('pie', 'accDescr', '"a" : 2') },
  { name: 'error-acc-title-no-colon', spec: 'original', text: lines('pie', 'accTitle x', '"a" : 2') },

  // Comments, directives and frontmatter that survive preprocessing
  { name: 'comment-trailing-section', spec: 'original', text: lines('pie', '"a" : 1 %% trailing', '"b" : 2') },
  { name: 'comment-indented', spec: 'original', text: lines('pie', '   %% indented', '"a" : 1') },
  { name: 'error-comment-after-label', spec: 'original', text: lines('pie', '"a" %% c', ': 1') },
  { name: 'frontmatter-title', spec: 'original', text: lines('---', 'title: From the frontmatter', '---', 'pie', '"a" : 1') },
  { name: 'frontmatter-and-title', spec: 'original', text: lines('---', 'title: From the frontmatter', '---', 'pie title From the text', '"a" : 1') },
  { name: 'directive-init', spec: 'original', text: lines('%%{init: {"pie": {"textPosition": 0.5}}}%%', 'pie', '"a" : 1') },

  // ------------------------------------------------------------------------------------------- the pie fixtures
  ...fixtures
];
