// gen-time-vectors.mjs - records what the d3 time functions that the pinned mermaid bundles (d3-time, d3-time-format,
// d3-scale and d3-array) compute for a matrix of dates, intervals, domains and format specifiers, as the upstream vectors
// of the CanvasNet.Mermaid TimeAxis tests (the time arithmetic of gantt charts).
//
// Developer tooling of tools/visual-diff; see the README there. Usage:
//
//     node gen-time-vectors.mjs [--modules <folder>] [--out <file>]
//
// <modules> is a folder that holds node_modules/mermaid and the d3 packages (default: the repository root). <out> is the
// vector file (default: test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/time-axis.json).
//
// The script runs the installed d3 packages directly in Node (no browser is needed) with the local time zone set to UTC,
// the zone the CanvasNet port uses; it refuses to run when the zone is not UTC or when an installed d3 package is not the
// version that mermaid's bundle contains (read from dist/mermaid.js.map). The dates, domains and specifiers are original;
// the expected values are d3's results. Invalid dates, NaN and infinities are written as null. Nothing is downloaded.
process.env.TZ = 'UTC';

import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

// The repository root is three folders above tools/visual-diff/mermaid
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

// Read the optional arguments
const args = process.argv.slice(2);
let modules = root;
let out = join(root, 'test', 'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'upstream-vectors', 'time-axis.json');
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--modules' && i + 1 < args.length) {
    modules = resolve(args[++i]);
  } else if (args[i] === '--out' && i + 1 < args.length) {
    out = resolve(args[++i]);
  } else {
    console.error('usage: node gen-time-vectors.mjs [--modules <folder>] [--out <file>]');
    process.exit(2);
  }
}

// The local zone must be UTC, the zone of the port
if (new Date(2024, 5, 1).getTimezoneOffset() !== 0 || new Date(1900, 0, 1).getTimezoneOffset() !== 0) {
  console.error('the local time zone is not UTC; run with TZ=UTC');
  process.exit(1);
}

// Each d3 package must be the version that mermaid's bundle contains
const packages = ['d3-time', 'd3-time-format', 'd3-scale', 'd3-array', 'd3-interpolate'];
const map = readFileSync(join(modules, 'node_modules', 'mermaid', 'dist', 'mermaid.js.map'), 'utf8');
const mermaidVersion = JSON.parse(readFileSync(join(modules, 'node_modules', 'mermaid', 'package.json'), 'utf8')).version;
const versions = {};
for (const name of packages) {
  const installed = JSON.parse(readFileSync(join(modules, 'node_modules', name, 'package.json'), 'utf8')).version;
  const bundled = [...new Set([...map.matchAll(new RegExp(`node_modules/\\.pnpm/${name}@([0-9.]+)/`, 'g'))].map((m) => m[1]))];
  if (!bundled.includes(installed)) {
    console.error(`${name} ${installed} is installed, but mermaid ${mermaidVersion} bundles ${bundled.join(', ') || 'none'}`);
    process.exit(1);
  }

  versions[name] = installed;
}

const resolver = createRequire(join(modules, 'noop.js'));
const load = async (name) => import(pathToFileURL(resolver.resolve(name)).href);
const time = await load('d3-time');
const format = await load('d3-time-format');
const scale = await load('d3-scale');
const array = await load('d3-array');

// A number or an invalid date as JSON (NaN is null)
const num = (value) => (Number.isNaN(+value) ? null : +value);
const utc = (text) => Date.parse(text);

// The dates of the matrix: ordinary dates, week and year edges, leap days, the epoch, two-digit, negative and
// six-digit years, the ends of the range and an invalid date
const dates = [
  utc('2024-01-01T00:00:00Z'),
  utc('2024-06-12T10:30:45.123Z'),
  utc('2024-12-31T23:59:59.999Z'),
  utc('2023-01-01T00:00:00Z'),
  utc('2021-01-03T12:00:00Z'),
  utc('2020-12-31T06:07:08.009Z'),
  utc('2020-02-29T13:14:15.016Z'),
  utc('2019-12-30T00:00:00Z'),
  0,
  -43200000,
  utc('1999-07-04T22:00:00Z'),
  new Date(Date.UTC(99, 2, 15, 1, 2, 3)).setUTCFullYear(99),
  new Date(Date.UTC(0, 0, 1)).setUTCFullYear(0),
  new Date(Date.UTC(0, 5, 30, 18)).setUTCFullYear(-1),
  utc('+010000-01-01T00:00:00Z'),
  utc('+202024-06-01T09:00:00Z'),
  -8.64e15,
  8.64e15,
  NaN
];

const cases = [];

// The getters of every date
for (const t of dates) {
  const d = new Date(t);
  cases.push({
    kind: 'date',
    time: num(t),
    expected: [d.getFullYear(), d.getMonth(), d.getDate(), d.getDay(), d.getHours(), d.getMinutes(), d.getSeconds(),
      d.getMilliseconds()].map(num)
  });
}

// The Date constructor from components: two-digit years, carries, negative components, fractions and huge values
const componentArgs = [
  [2024, 0, 1], [2024, 11, 31, 23, 59, 59, 999], [2024, 12, 1], [2024, -1, 1], [2024, 1, 30], [2023, 1, 29],
  [2024, 0, 0], [2024, 0, -1], [2024, 0, 1, 24], [2024, 0, 1, -1], [2024, 0, 1, 0, 90], [2024, 0, 1, 0, 0, 3600],
  [2024, 0, 1, 0, 0, 0, 86400000], [0, 0, 1], [99, 5, 15], [100, 0, 1], [-1, 0, 1], [1.9, 0.5, 2.7, 3.2],
  [-0.5, 0, 1], [275760, 8, 13], [275760, 8, 14], [-271821, 3, 20], [-271821, 3, 19], [1e6, 0, 1], [2024, 1e7, 1],
  [2024, 0, 1e9], [NaN, 0, 1], [2024, NaN, 1], [Infinity, 0, 1], [202024, 5, 1], [1970, 0, 1, 0, 0, 0, 0.9]
];
for (const a of componentArgs) {
  cases.push({ kind: 'components', args: a.map(num), expected: num(new Date(...a)) });
}

// The setters d3 and dayjs use, on valid and invalid dates
const setterCalls = [
  ['setFullYear', [2025]], ['setFullYear', [2025, 1]], ['setFullYear', [2025, 1, 29]], ['setFullYear', [2024, 13, 40]],
  ['setFullYear', [NaN]], ['setFullYear', [300000]], ['setMonth', [0]], ['setMonth', [1]], ['setMonth', [14, 3]],
  ['setMonth', [-13]], ['setMonth', [0, 1]], ['setDate', [1]], ['setDate', [0]], ['setDate', [31]], ['setDate', [400]],
  ['setDate', [-400]], ['setHours', [0, 0, 0, 0]], ['setHours', [25]], ['setHours', [-1, 30]], ['setHours', [12, 0]],
  ['setMinutes', [61, 0]], ['setSeconds', [0, 0]], ['setMilliseconds', [1500]], ['setTime', [1.7]]
];
for (const t of [utc('2024-01-31T10:20:30.400Z'), utc('2023-03-31T23:00:00Z'), -1, 8.64e15, NaN]) {
  for (const [method, a] of setterCalls) {
    const d = new Date(t);
    d[method](...a);
    cases.push({ kind: 'set', time: num(t), method, args: a.map(num), expected: num(d) });
  }
}

// The intervals and their every variants: floor, ceil, offset and count around every date
const intervals = {
  millisecond: [time.timeMillisecond, [1, 10, 250]],
  second: [time.timeSecond, [1, 5, 15, 30]],
  minute: [time.timeMinute, [1, 5, 15, 30]],
  hour: [time.timeHour, [1, 3, 6, 12]],
  day: [time.timeDay, [1, 2, 7]],
  sunday: [time.timeSunday, [1, 2]],
  monday: [time.timeMonday, [1, 2]],
  tuesday: [time.timeTuesday, [1]],
  wednesday: [time.timeWednesday, [1]],
  thursday: [time.timeThursday, [1, 3]],
  friday: [time.timeFriday, [1]],
  saturday: [time.timeSaturday, [1]],
  month: [time.timeMonth, [1, 3, 6]],
  year: [time.timeYear, [1, 2, 10, 100]]
};
const unitLength = {
  millisecond: 1, second: 1e3, minute: 6e4, hour: 3.6e6, day: 864e5, sunday: 6048e5, monday: 6048e5, tuesday: 6048e5,
  wednesday: 6048e5, thursday: 6048e5, friday: 6048e5, saturday: 6048e5, month: 2592e6, year: 31536e6
};
const offsetSteps = [-3, -1, 0, 1, 2.7, 5];
const intervalDates = dates.filter((t) => Number.isNaN(t) || Math.abs(t) < 8e15);
for (const [name, [base, everySteps]] of Object.entries(intervals)) {
  for (const every of everySteps) {
    const interval = base.every(every);
    for (const t of intervalDates) {
      const c = {
        kind: 'interval',
        interval: name,
        every,
        time: num(t),
        floor: num(interval.floor(new Date(t))),
        ceil: num(interval.ceil(new Date(t))),
        offsets: offsetSteps.map((s) => [s, num(interval.offset(new Date(t), s))])
      };
      if (typeof interval.count === 'function') {
        c.counts = dates.slice(0, 6).map((u) => [u, num(interval.count(new Date(t), new Date(u)))]);
      }

      cases.push(c);
    }

    // Ranges of about 25 units from a few starts, with a step of one and of three
    for (const start of [utc('2024-02-27T21:13:14.567Z'), utc('2019-12-30T00:00:00Z'), -5e10]) {
      const stop = start + unitLength[name] * every * 25 + 1234;
      for (const step of [1, 3]) {
        cases.push({
          kind: 'range',
          interval: name,
          every,
          start,
          stop,
          step,
          expected: interval.range(new Date(start), new Date(stop), step).map(num)
        });
      }
    }
  }
}

// The default ticks of a time scale (count 10, and other counts) over domains from milliseconds to centuries,
// descending and empty domains included
const domains = [
  [utc('2024-01-01T00:00:00Z'), utc('2024-01-01T00:00:00.040Z')],
  [utc('2024-01-01T00:00:00Z'), utc('2024-01-01T00:00:03Z')],
  [utc('2024-01-01T10:00:00Z'), utc('2024-01-01T10:20:00Z')],
  [utc('2024-01-01T10:00:00Z'), utc('2024-01-01T18:30:00Z')],
  [utc('2024-01-01T00:00:00Z'), utc('2024-01-04T00:00:00Z')],
  [utc('2024-01-01T00:00:00Z'), utc('2024-01-20T00:00:00Z')],
  [utc('2024-01-06T00:00:00Z'), utc('2024-03-15T00:00:00Z')],
  [utc('2024-01-01T00:00:00Z'), utc('2024-07-01T00:00:00Z')],
  [utc('2023-11-15T00:00:00Z'), utc('2026-02-01T00:00:00Z')],
  [utc('2014-01-01T00:00:00Z'), utc('2034-06-01T00:00:00Z')],
  [utc('1800-01-01T00:00:00Z'), utc('2200-01-01T00:00:00Z')],
  [utc('2024-03-01T00:00:00Z'), utc('2024-01-01T00:00:00Z')],
  [utc('2024-03-01T00:00:00Z'), utc('2024-03-01T00:00:00Z')],
  [-5e10, 5e10],
  [utc('+202024-01-01T00:00:00Z'), utc('+202030-01-01T00:00:00Z')],
  [NaN, NaN]
];
for (const domain of domains) {
  for (const count of [10, 1, 5, 20, 0]) {
    const s = scale.scaleTime().domain(domain.map((t) => new Date(t)));
    cases.push({ kind: 'ticks', domain: domain.map(num), count, expected: s.ticks(count).map(num) });
  }
}

// Ticks at the boundaries of an interval (Mermaid's tickInterval), as d3-axis asks a time scale for them
const tickIntervals = [
  ['millisecond', 10, 0], ['second', 15, 1], ['minute', 5, 2], ['hour', 3, 3], ['day', 1, 5], ['day', 2, 6],
  ['monday', 1, 6], ['sunday', 2, 7], ['month', 1, 8], ['month', 3, 9], ['month', 1, 11], ['day', 1, 12]
];
for (const [name, every, d] of tickIntervals) {
  const s = scale.scaleTime().domain(domains[d].map((t) => new Date(t)));
  cases.push({
    kind: 'ticks',
    domain: domains[d].map(num),
    interval: name,
    every,
    expected: s.ticks(intervals[name][0].every(every)).map(num)
  });
}

// d3-array's tickStep
const tickSteps = [
  [0, 1, 10], [0, 10, 10], [0, 100, 10], [0, 7, 10], [0, 0.35, 10], [1, 0, 10], [-3.2, 7.9, 5], [0, 1e-9, 3],
  [0, 54.5, 10], [1.38, 2.92, 10], [0, 10, 0.75], [0, 10, 0], [5, 5, 10], [NaN, 1, 10], [53.26, 54.37, 10],
  [0, 3e15, 10], [-27000, 27000, 10]
];
for (const [start, stop, count] of tickSteps) {
  cases.push({ kind: 'tickStep', start: num(start), stop: num(stop), count, expected: num(array.tickStep(start, stop, count)) });
}

// The time scale with rangeRound: ascending, descending, empty and invalid domains, and halves that round
const scales = [
  [[utc('2024-01-01T00:00:00Z'), utc('2024-01-31T00:00:00Z')], [0, 634]],
  [[utc('2024-01-31T00:00:00Z'), utc('2024-01-01T00:00:00Z')], [0, 634]],
  [[utc('2024-01-01T00:00:00Z'), utc('2024-01-01T00:00:00Z')], [0, 634]],
  [[0, 3], [0, 1]],
  [[0, 4], [10, 0]],
  [[NaN, NaN], [0, 634]]
];
for (const [domain, range] of scales) {
  const s = scale.scaleTime().domain(domain.map((t) => new Date(t))).rangeRound(range);
  const inputs = [domain[0], domain[1], (domain[0] + domain[1]) / 2, domain[0] - 864e5, domain[1] + 1e7, 1.5, 0.5, NaN]
    .map((v) => (Number.isNaN(v) ? NaN : Math.trunc(v)));
  cases.push({
    kind: 'scale',
    domain: domain.map(num),
    range,
    inputs: inputs.map(num),
    expected: inputs.map((v) => num(s(new Date(v))))
  });
}

// timeFormat: every directive with every pad modifier, on every date, plus composite and malformed specifiers
const directives = 'a A b B c d e f g G H I j L m M p q Q s S u U V w W x X y Y Z %'.split(' ');
const pads = ['', '-', '_', '0'];
const extra = ['%Y-%m-%d', '%d', '%b %d', '%H:%M', '%I %p', 'Week %W of %Y', '%k', '%', '%-', '100%%', '%%%Y', '%e/%_m', 'plain'];
for (const t of dates) {
  for (const pad of pads) {
    const specifiers = directives.map((c) => `%${pad}${c}`);
    if (pad === '') {
      specifiers.push(...extra);
    }

    cases.push({
      kind: 'format',
      time: num(t),
      specifiers,
      expected: specifiers.map((s) => format.timeFormat(s)(new Date(t)))
    });
  }
}

// One case per line keeps the file reviewable and its diffs small
const source = `Generated by tools/visual-diff/mermaid/gen-time-vectors.mjs (TZ=UTC) from ${packages.map((p) => `${p} ${versions[p]}`).join(', ')} (ISC, Mike Bostock), the versions mermaid ${mermaidVersion} bundles; the inputs are original. See src/DemaConsulting.CanvasNet.Mermaid/third-party/README.md.`;
const lines = [];
lines.push('{');
lines.push(`  "source": ${JSON.stringify(source)},`);
lines.push(`  "versions": ${JSON.stringify(versions)},`);
lines.push('  "cases": [');
lines.push(cases.map((c) => '    ' + JSON.stringify(c)).join(',\n'));
lines.push('  ]');
lines.push('}');
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, lines.join('\n') + '\n');
console.log(`mermaid ${mermaidVersion}: wrote ${cases.length} time vectors to ${out}`);
