// packet-vector-cases.mjs - the packet diagram texts of gen-diagram-vectors.mjs --type packet. Each case is
// { name, spec, text } (and an optional site configuration). The expectations are not written here: the generator
// records what the pinned mermaid makes of each text.
//
// Attribution: many texts are the inputs of the packet tests at the pinned version: mermaid's
// packages/mermaid/src/diagrams/packet/packet.spec.ts (MIT, Copyright (c) 2014 - 2022 Knut Sveidqvist, the notice of
// LICENSE-mermaid.txt) and @mermaid-js/parser's packages/parser/tests/packet.test.ts (MIT, Copyright (c) 2023
// Yokozuna59, the notice of LICENSE-mermaid-parser.txt). Their spec field names the upstream file and test. Only the
// texts are taken; the upstream assertions are replaced by the facts mermaid records. The parser tests call the Langium
// parser directly; here every text goes through mermaid's preprocessing and detection first, as a diagram does. The
// cases whose spec field is "original" cover the keywords, the INT terminal, the value converters, every database error,
// the row split for several bitsPerRow values (given as a site configuration, because getDiagramFromText does not apply
// frontmatter configuration), and the "fixture" cases are the packet fixtures of the tests.

import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const provenance = 'The inputs are those of mermaid\'s packet spec (diagrams/packet/packet.spec.ts; MIT) and of @mermaid-js/parser\'s packet tests (tests/packet.test.ts; MIT), named in each case\'s spec field, plus original texts and the packet fixtures.';

const lines = (...rows) => rows.join('\n');
const packetSpec = (test) => `diagrams/packet/packet.spec.ts: ${test}`;
const parserSpec = (test) => `@mermaid-js/parser tests/packet.test.ts: ${test}`;

// The packet fixtures of the tests (original), read from the fixture folder
const fixtureFolder = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'test',
  'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'packet');
const fixtures = readdirSync(fixtureFolder).filter((f) => f.endsWith('.mmd')).sort().map((f) => ({
  name: `fixture-${f.replace(/\.mmd$/, '')}`,
  spec: `fixture packet/${f}`,
  text: readFileSync(join(fixtureFolder, f), 'utf8')
}));

// The texts of an it.each of the parser tests, one case each
const each = (prefix, test, texts) => texts.map((text, i) => ({ name: `${prefix}-${i + 1}`, spec: parserSpec(test), text }));

// A site configuration with a packet section
const site = (packet) => ({ packet });

// A frame of three blocks that crosses several row ends for a small bitsPerRow
const frame = lines('packet', '0-5: "a"', '6: "b"', '7-20: "c"', '+3: "d"');

export const cases = [
  // ------------------------------------------------------------------------------- diagrams/packet/packet.spec.ts
  { name: 'spec-packet-beta', spec: packetSpec('should handle a packet-beta definition'), text: 'packet-beta' },
  { name: 'spec-packet', spec: packetSpec('should handle a packet definition'), text: 'packet' },
  { name: 'spec-data-title', spec: packetSpec('should handle diagram with data and title'), text: 'packet\n    title Packet diagram\n    accTitle: Packet accTitle\n    accDescr: Packet accDescription\n    0-10: "test"\n    ' },
  { name: 'spec-single-bits', spec: packetSpec('should handle single bits'), text: 'packet\n    0-10: "test"\n    11: "single"\n    ' },
  { name: 'spec-bit-counts', spec: packetSpec('should handle bit counts'), text: 'packet\n    +8: "byte"\n    +16: "word"\n    ' },
  { name: 'spec-bit-counts-bit-or-bits', spec: packetSpec('should handle bit counts with bit or bits'), text: 'packet\n    +8: "byte"\n    +16: "word"\n    ' },
  { name: 'spec-multiple-rows', spec: packetSpec('should split into multiple rows'), text: 'packet\n    0-10: "test"\n    11-90: "multiple"\n    ' },
  { name: 'spec-exact-length', spec: packetSpec('should split into multiple rows when cut at exact length'), text: 'packet\n    0-16: "test"\n    17-63: "multiple"\n    ' },
  { name: 'error-spec-not-continuous', spec: packetSpec('should throw error if numbers are not continuous'), text: 'packet\n    0-16: "test"\n    18-20: "error"\n    ' },
  { name: 'error-spec-not-continuous-bit-counts', spec: packetSpec('should throw error if numbers are not continuous with bit counts'), text: 'packet\n    +16: "test"\n    18-20: "error"\n    ' },
  { name: 'error-spec-not-continuous-single', spec: packetSpec('should throw error if numbers are not continuous for single packets'), text: 'packet\n    0-16: "test"\n    18: "error"\n    ' },
  { name: 'error-spec-not-continuous-single-bit-counts', spec: packetSpec('should throw error if numbers are not continuous for single packets with bit counts'), text: 'packet\n    +16: "test"\n    18: "error"\n    ' },
  { name: 'error-spec-not-continuous-single-2', spec: packetSpec('should throw error if numbers are not continuous for single packets - 2'), text: 'packet\n    0-16: "test"\n    17: "good"\n    19: "error"\n    ' },
  { name: 'error-spec-end-less-than-start', spec: packetSpec('should throw error if end is less than start'), text: 'packet\n    0-16: "test"\n    25-20: "error"\n    ' },
  { name: 'error-spec-zero-bits', spec: packetSpec('should throw error if bit count is 0'), text: 'packet\n    +0: "test"\n    ' },

  // ---------------------------------------------------------------------- @mermaid-js/parser tests/packet.test.ts
  ...each('parser-regular-packet', 'should handle regular packet', ['packet-beta', '  packet-beta  ', '\tpacket-beta\t', '\n    \tpacket-beta\n    ', 'packet', '  packet  ', '\tpacket\t', '\n    \tpacket\n    ']),

  // ------------------------------------------------------------------------------------------------- original
  // The header: the two keywords, the suffix of packet only, and what may follow them
  { name: 'header-packet-percent', spec: 'original', text: lines('packet%% comment', '0: "a"') },
  { name: 'header-packet-beta-percent', spec: 'original', text: lines('packet-beta%% comment', '0: "a"') },
  { name: 'header-packet-comment', spec: 'original', text: lines('packet %% comment', '0: "a"') },
  { name: 'header-leading-blank-lines', spec: 'original', text: lines('', '', 'packet', '0: "a"') },
  { name: 'header-same-line-title', spec: 'original', text: lines('packet title On the header line', '0-3: "a"') },
  { name: 'header-same-line-block', spec: 'original', text: lines('packet 0-3: "a"', '4: "b"') },
  { name: 'header-same-line-bits', spec: 'original', text: lines('packet-beta +4: "a"') },
  { name: 'error-header-glued', spec: 'original', text: lines('packets', '0: "a"') },
  { name: 'error-header-beta-glued', spec: 'original', text: lines('packet-betas', '0: "a"') },
  { name: 'error-header-word', spec: 'original', text: lines('packet.beta', '0: "a"') },
  { name: 'error-header-twice', spec: 'original', text: lines('packet', 'packet', '0: "a"') },
  { name: 'error-header-beta-then-packet', spec: 'original', text: lines('packet-beta packet', '0: "a"') },
  { name: 'error-header-word-after', spec: 'original', text: lines('packet diagram', '0: "a"') },

  // The INT terminal: a zero alone, no leading zero, backtracking before a dot, JavaScript parseInt
  { name: 'int-zero-single', spec: 'original', text: lines('packet', '0: "a"') },
  { name: 'int-zero-range', spec: 'original', text: lines('packet', '0-0: "a"', '1-1: "b"') },
  { name: 'int-spaces', spec: 'original', text: lines('packet', '0 - 3 : "a"', '+ 4 : "b"', '8   :   "c"') },
  { name: 'int-tabs', spec: 'original', text: 'packet\n\t0\t-\t3\t:\t"a"\n\t+\t4:\t"b"' },
  { name: 'int-no-spaces', spec: 'original', text: lines('packet', '0-3:"a"', '+4:"b"') },
  { name: 'error-int-double-zero', spec: 'original', text: lines('packet', '00: "a"') },
  { name: 'error-int-leading-zero', spec: 'original', text: lines('packet', '05: "a"') },
  { name: 'error-int-leading-zero-end', spec: 'original', text: lines('packet', '0-07: "a"') },
  { name: 'error-int-decimal', spec: 'original', text: lines('packet', '0-10.5: "a"') },
  { name: 'error-int-one-dot-zero', spec: 'original', text: lines('packet', '0-1.0: "a"') },
  { name: 'error-int-zero-dot', spec: 'original', text: lines('packet', '0.5: "a"') },
  { name: 'error-int-negative', spec: 'original', text: lines('packet', '-1: "a"') },
  { name: 'error-int-negative-bits', spec: 'original', text: lines('packet', '+-1: "a"') },
  { name: 'error-int-exponent', spec: 'original', text: lines('packet', '0-1e3: "a"') },
  { name: 'error-int-word', spec: 'original', text: lines('packet', 'zero: "a"') },
  { name: 'error-int-plus-range', spec: 'original', text: lines('packet', '+4-7: "a"') },
  { name: 'error-int-double-dash', spec: 'original', text: lines('packet', '0--3: "a"') },
  { name: 'int-21-digits', spec: 'original', text: lines('packet', '0: "a"', '+123456789012345678901: "b"'), site: site({ bitsPerRow: 1e30 }) },
  { name: 'error-int-25-digits', spec: 'original', text: lines('packet', '0: "a"', '1234567890123456789012345: "b"') },
  { name: 'error-int-25-digits-end', spec: 'original', text: lines('packet', '1234567890123456789012345-0: "a"') },
  { name: 'int-huge-bits', spec: 'original', text: lines('packet', '+1' + '0'.repeat(400) + ': "infinite"') },

  // The database: contiguity, order, zero bit fields, and the message of each
  { name: 'error-db-first-not-zero', spec: 'original', text: lines('packet', '1-3: "a"') },
  { name: 'error-db-first-single-not-zero', spec: 'original', text: lines('packet', '7: "a"') },
  { name: 'error-db-duplicate-single', spec: 'original', text: lines('packet', '0: "a"', '0: "b"') },
  { name: 'error-db-overlap-range', spec: 'original', text: lines('packet', '0-7: "a"', '4-9: "b"') },
  { name: 'error-db-gap-range', spec: 'original', text: lines('packet', '0-7: "a"', '9-12: "b"') },
  { name: 'error-db-gap-after-bits', spec: 'original', text: lines('packet', '+8: "a"', '+8: "b"', '17: "c"') },
  { name: 'error-db-end-less-first', spec: 'original', text: lines('packet', '5-3: "a"') },
  { name: 'error-db-end-less-before-contiguity', spec: 'original', text: lines('packet', '0-3: "a"', '9-6: "b"') },
  { name: 'error-db-end-less-contiguous', spec: 'original', text: lines('packet', '0-3: "a"', '4-2: "b"') },
  { name: 'error-db-zero-bits-later', spec: 'original', text: lines('packet', '0-7: "a"', '+0: "b"') },
  { name: 'error-db-zero-bits-after-error-free-blocks', spec: 'original', text: lines('packet', '+3: "a"', '+5: "b"', '+0: "c"', '+1: "d"') },
  { name: 'error-db-gap-after-cap', spec: 'original', text: lines('packet', '0-99999999: "big"', '5: "gap"') },
  { name: 'error-db-equal-start-end-gap', spec: 'original', text: lines('packet', '0-3: "a"', '5-5: "b"') },
  { name: 'db-equal-start-end', spec: 'original', text: lines('packet', '0-3: "a"', '4-4: "b"', '5: "c"') },
  { name: 'db-mixed-forms', spec: 'original', text: lines('packet', '+4: "a"', '4-7: "b"', '8: "c"', '+23: "d"', '32-35: "e"') },
  { name: 'db-many-small', spec: 'original', text: lines('packet', ...Array.from({ length: 40 }, (_, i) => `${i}: "b${i}"`)) },

  // The row split (getNextFittingBlock): the off-by-one bits of the parts, exact cuts and long ranges
  { name: 'split-one-row-exact', spec: 'original', text: lines('packet', '0-31: "row"') },
  { name: 'split-two-rows-exact', spec: 'original', text: lines('packet', '0-63: "two rows"') },
  { name: 'split-across-one-end', spec: 'original', text: lines('packet', '0-29: "a"', '30-35: "b"', '36: "c"') },
  { name: 'split-across-many-ends', spec: 'original', text: lines('packet', '0: "a"', '1-200: "long"') },
  { name: 'split-bits-across-ends', spec: 'original', text: lines('packet', '+30: "a"', '+70: "b"', '+1: "c"') },
  { name: 'split-last-bit-of-row', spec: 'original', text: lines('packet', '0-30: "a"', '31: "b"', '32: "c"') },
  { name: 'split-cap', spec: 'original', text: lines('packet', '0-99999999: "huge"') },
  { name: 'split-cap-bits', spec: 'original', text: lines('packet', '+99999999999999999999: "huge"') },
  { name: 'split-cap-then-more', spec: 'original', text: lines('packet', '0-99999999: "huge"', '100000000: "after"') },

  // The row split for other bitsPerRow values (site configuration)
  { name: 'bits-per-row-1', spec: 'original', text: frame, site: site({ bitsPerRow: 1 }) },
  { name: 'bits-per-row-4', spec: 'original', text: frame, site: site({ bitsPerRow: 4 }) },
  { name: 'bits-per-row-7', spec: 'original', text: frame, site: site({ bitsPerRow: 7 }) },
  { name: 'bits-per-row-8', spec: 'original', text: frame, site: site({ bitsPerRow: 8 }) },
  { name: 'bits-per-row-16', spec: 'original', text: frame, site: site({ bitsPerRow: 16 }) },
  { name: 'bits-per-row-64', spec: 'original', text: lines('packet', '0-100: "a"', '101-200: "b"'), site: site({ bitsPerRow: 64 }) },
  { name: 'error-bits-per-row-1-5', spec: 'original', text: lines('packet', '0: "a"', '1: "b"', '2-3: "c"'), site: site({ bitsPerRow: 1.5 }) },
  { name: 'bits-per-row-1-5', spec: 'original', text: frame, site: site({ bitsPerRow: 1.5 }) },
  { name: 'bits-per-row-0', spec: 'original', text: lines('packet', '0: "a"', '1: "b"'), site: site({ bitsPerRow: 0 }) },
  { name: 'bits-per-row-negative', spec: 'original', text: frame, site: site({ bitsPerRow: -3 }) },
  { name: 'bits-per-row-2-5', spec: 'original', text: lines('packet', '0-9: "a"'), site: site({ bitsPerRow: 2.5 }) },
  { name: 'error-bits-per-row-0-5', spec: 'original', text: lines('packet', '0: "a"', '1: "b"'), site: site({ bitsPerRow: 0.5 }) },
  { name: 'show-bits-false', spec: 'original', text: frame, site: site({ showBits: false }) },
  { name: 'padding-y-zero', spec: 'original', text: frame, site: site({ paddingY: 0 }) },
  { name: 'geometry-config', spec: 'original', text: frame, site: site({ rowHeight: 40, bitWidth: 20, paddingX: 2, paddingY: 8, showBits: true, useMaxWidth: false }) },

  // Titles: the value converter, the %% look-ahead, the empty alternative, the last one wins
  { name: 'title-spaces-collapsed', spec: 'original', text: lines('packet', '  title   A   B\t\tC  ', '0: "a"') },
  { name: 'title-comment', spec: 'original', text: lines('packet', 'title A %% not part of it', '0: "a"') },
  { name: 'title-empty', spec: 'original', text: lines('packet title', '0: "a"') },
  { name: 'title-only-comment', spec: 'original', text: lines('packet title %%x', '0: "a"') },
  { name: 'title-twice-last-wins', spec: 'original', text: lines('packet', 'title First', 'title Second', '0: "a"') },
  { name: 'title-after-blocks', spec: 'original', text: lines('packet', '0: "a"', 'title Late') },
  { name: 'title-html', spec: 'original', text: lines('packet title <b>Bold</b> & <script>x</script>', '0: "a"') },
  { name: 'title-tab', spec: 'original', text: 'packet\ntitle\tTabbed title\n0: "a"' },
  { name: 'title-packet-word', spec: 'original', text: lines('packet title packet-beta 0: "x"', '0: "a"') },
  { name: 'error-title-glued', spec: 'original', text: lines('packet', 'titles', '0: "a"') },

  // Accessibility texts
  { name: 'acc-title-spaces', spec: 'original', text: lines('packet', '  accTitle :   a   title  ', '0: "a"') },
  { name: 'acc-title-comment', spec: 'original', text: lines('packet accTitle: x %% y', '0: "a"') },
  { name: 'acc-title-empty', spec: 'original', text: lines('packet', 'accTitle:', '0: "a"') },
  { name: 'acc-descr-single-spaces', spec: 'original', text: lines('packet', 'accDescr:   one   two  ', '0: "a"') },
  { name: 'acc-descr-multiline-indented', spec: 'original', text: lines('packet', 'accDescr {', '      first   line', '', '', '      second line   ', '   }', '0: "a"') },
  { name: 'acc-descr-multiline-no-space', spec: 'original', text: lines('packet', 'accDescr{x}', '0: "a"') },
  { name: 'acc-descr-multiline-brace-next-line', spec: 'original', text: lines('packet', 'accDescr', '{', '  text', '}', '0: "a"') },
  { name: 'acc-all-three', spec: 'original', text: lines('packet title T', 'accTitle: A', 'accDescr: D', '0: "a"') },
  { name: 'acc-html', spec: 'original', text: lines('packet', 'accTitle: <i>x</i>', 'accDescr: a <b>b</b>', '0: "a"') },
  { name: 'error-acc-descr-trailing', spec: 'original', text: lines('packet', 'accDescr {', '  x', '  }   y') },
  { name: 'error-acc-descr-no-value', spec: 'original', text: lines('packet', 'accDescr', '0: "a"') },
  { name: 'error-acc-title-no-colon', spec: 'original', text: lines('packet', 'accTitle x', '0: "a"') },

  // Strings: quotes, escapes, empty labels, labels over lines, entities
  { name: 'string-single-quotes', spec: 'original', text: lines('packet', "0: 'single'", '1: "double"') },
  { name: 'string-escapes', spec: 'original', text: lines('packet', '0: "a\\nb\\tc\\q\\\\d\\"e\\b\\f\\v\\r\\0"') },
  { name: 'string-single-quote-escapes', spec: 'original', text: lines('packet', "0: 'it\\'s \"quoted\"'") },
  { name: 'string-empty', spec: 'original', text: lines('packet', '0: ""', "1: ''") },
  { name: 'string-over-two-lines', spec: 'original', text: lines('packet', '0: "first', 'second"') },
  { name: 'string-percent-signs', spec: 'original', text: lines('packet', '0: "50%% off"') },
  { name: 'string-entities', spec: 'original', text: lines('packet', '0: "A #amp; B #35; C"', '1: "<b>bold</b> &amp;"') },
  { name: 'string-unicode', spec: 'original', text: lines('packet', '0-7: "Café ☕ naïve"') },
  { name: 'string-spaces-kept', spec: 'original', text: lines('packet', '0: "  many   spaces  "') },
  { name: 'error-string-backslash-newline', spec: 'original', text: lines('packet', '0: "a\\', 'b"') },
  { name: 'error-string-unclosed', spec: 'original', text: lines('packet', '0: "open') },
  { name: 'error-string-unquoted', spec: 'original', text: lines('packet', '0: label') },

  // Statements: the colon, the label, the line ends
  { name: 'error-block-missing-colon', spec: 'original', text: lines('packet', '0 "a"') },
  { name: 'error-block-missing-label', spec: 'original', text: lines('packet', '0:') },
  { name: 'error-block-missing-label-next-line', spec: 'original', text: lines('packet', '0:', '"a"') },
  { name: 'error-block-two-on-a-line', spec: 'original', text: lines('packet', '0: "a" 1: "b"') },
  { name: 'error-block-semicolon', spec: 'original', text: lines('packet', '0: "a";') },
  { name: 'error-block-colon-first', spec: 'original', text: lines('packet', ': "a"') },
  { name: 'error-block-label-only', spec: 'original', text: lines('packet', '"a"') },
  { name: 'error-block-number-only', spec: 'original', text: lines('packet', '0') },
  { name: 'error-block-title-after', spec: 'original', text: lines('packet', '0: "a" title c') },
  { name: 'error-block-range-open', spec: 'original', text: lines('packet', '0-: "a"') },
  { name: 'block-blank-lines', spec: 'original', text: lines('packet', '', '0: "a"', '', '', '1: "b"', '') },
  { name: 'block-crlf', spec: 'original', text: 'packet\r\n0-3: "a"\r\n+4: "b"\r\n' },

  // Comments, directives and frontmatter that survive preprocessing
  { name: 'comment-trailing-block', spec: 'original', text: lines('packet', '0: "a" %% trailing', '1: "b"') },
  { name: 'comment-indented', spec: 'original', text: lines('packet', '   %% indented', '0: "a"') },
  { name: 'error-comment-after-number', spec: 'original', text: lines('packet', '0 %% c', ': "a"') },
  { name: 'frontmatter-title', spec: 'original', text: lines('---', 'title: From the frontmatter', '---', 'packet', '0: "a"') },
  { name: 'frontmatter-and-title', spec: 'original', text: lines('---', 'title: From the frontmatter', '---', 'packet title From the text', '0: "a"') },
  { name: 'directive-init', spec: 'original', text: lines('%%{init: {"packet": {"bitsPerRow": 8}}}%%', 'packet', '0-15: "a"') },

  // ---------------------------------------------------------------------------------------- the packet fixtures
  ...fixtures
];
