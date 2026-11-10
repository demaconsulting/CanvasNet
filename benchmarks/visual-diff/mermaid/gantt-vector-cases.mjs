// gantt-vector-cases.mjs - the gantt chart texts of gen-diagram-vectors.mjs --type gantt. Each case is { name, spec, text }
// (and an optional site configuration). The expectations are not written here: the generator records what the pinned
// mermaid makes of each text (in a page whose time zone is UTC and whose clock is fixed, see gen-diagram-vectors.mjs).
//
// Attribution: many texts are the inputs of the gantt tests at the pinned version: mermaid's
// packages/mermaid/src/diagrams/gantt/parser/gantt.spec.js and packages/mermaid/src/diagrams/gantt/ganttDb.spec.ts (MIT,
// Copyright (c) 2014 - 2022 Knut Sveidqvist, the notice of LICENSE-mermaid.txt). Their spec field names the upstream file
// and test. Only the inputs are taken; the database tests call the database directly, so their calls are written here
// as the gantt text that makes the same calls. The upstream assertions are replaced by the facts mermaid records. The
// cases whose spec field is "original" cover the lexer's rule order and quirks, the grammar, dates, durations, the
// dayjs formats, after and until, excludes, links, the limits of JavaScript objects and the errors, and the "fixture"
// cases are the gantt fixtures of the tests (their frontmatter is removed by mermaid's preprocessing).

import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const provenance = 'The inputs are those of mermaid\'s gantt specs (diagrams/gantt/parser/gantt.spec.js and diagrams/gantt/ganttDb.spec.ts; MIT), named in each case\'s spec field, plus original texts and the gantt fixtures.';

const lines = (...rows) => rows.join('\n');
const parserSpec = (test) => `diagrams/gantt/parser/gantt.spec.js: ${test}`;
const dbSpec = (test) => `diagrams/gantt/ganttDb.spec.ts: ${test}`;

// The gantt fixtures of the tests (original), read from the fixture folder
const fixtureFolder = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'test',
  'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'gantt');
const fixtures = existsSync(fixtureFolder)
  ? readdirSync(fixtureFolder).filter((f) => f.endsWith('.mmd')).sort().map((f) => ({
    name: `fixture-${f.replace(/\.mmd$/, '')}`,
    spec: `fixture gantt/${f}`,
    text: readFileSync(join(fixtureFolder, f), 'utf8')
  }))
  : [];

// A chart with a date format, a section and task lines
const chart = (format, ...rows) => lines('gantt', `dateFormat ${format}`, 'section S', ...rows);
const iso = (...rows) => chart('YYYY-MM-DD', ...rows);

// The tasks of ganttDb.spec.ts "should maintain the order in which tasks are created"
const orderTasks = [
  'section section A section',
  'Completed task :done,    des1, 2014-01-06,2014-01-08',
  'Active task :active,  des2, 2014-01-09, 3d',
  'Future task :des3, after des2, 5d',
  'Future task2 :des4, after des3, 5d',
  'section section Critical tasks',
  'Completed task in the critical line :crit, done, 2014-01-06,24h',
  'Implement parser and jison :crit, done, after des1, 2d',
  'Create tests for parser :crit, active, 3d',
  'Future task in critical line :crit, 5d',
  'Create tests for renderer :2d',
  'Add to mermaid :1d',
  'section section Documentation',
  'Describe gantt syntax :active, a1, after des1, 3d',
  'Add gantt diagram to demo page :after a1  , 20h',
  'Add another diagram to demo page :doc1, after a1  , 48h',
  'section section Last section',
  'Describe gantt syntax :after doc1, 3d',
  'Add gantt diagram to demo page :20h',
  'Add another diagram to demo page :48h'
];

export const cases = [
  // --------------------------------------------------------------------------- diagrams/gantt/parser/gantt.spec.js
  { name: 'spec-date-format', spec: parserSpec('should handle a dateFormat definition'), text: 'gantt\ndateFormat yyyy-mm-dd' },
  { name: 'spec-inclusive', spec: parserSpec('should handle a inclusive end date definition'), text: 'gantt\ndateFormat yyyy-mm-dd\ninclusiveEndDates' },
  { name: 'spec-title', spec: parserSpec('should handle a title definition'), text: 'gantt\ndateFormat yyyy-mm-dd\ntitle Adding gantt diagram functionality to mermaid' },
  { name: 'spec-title-semicolon', spec: parserSpec('should handle a title definition'), text: 'gantt\ndateFormat yyyy-mm-dd\ntitle ;Gantt diagram titles support semicolons' },
  { name: 'spec-title-hash', spec: parserSpec('should handle a title definition'), text: 'gantt\ndateFormat yyyy-mm-dd\ntitle #Gantt diagram titles support hashtags' },
  { name: 'spec-excludes', spec: parserSpec('should handle an excludes definition'), text: 'gantt\ndateFormat yyyy-mm-dd\ntitle Adding gantt diagram functionality to mermaid\nexcludes weekdays 2019-02-01' },
  {
    name: 'spec-excludes-lines', spec: parserSpec('should concatenate tokens across multiple excludes lines (issue #6270)'),
    text: lines('gantt', 'dateFormat DD-MM-YYYY', 'excludes weekends', '%% week 7 is winter break', 'excludes 10-02-2025 11-02-2025 12-02-2025', '%% workers holiday 1 maj', 'excludes 01-05-2025')
  },
  { name: 'spec-today-marker', spec: parserSpec('should handle a todayMarker definition'), text: 'gantt\ndateFormat yyyy-mm-dd\ntitle Adding gantt diagram functionality to mermaid\nexcludes weekdays 2019-02-01\ntodayMarker off' },
  { name: 'spec-section', spec: parserSpec('should handle a section definition'), text: 'gantt\ndateFormat yyyy-mm-dd\ntitle Adding gantt diagram functionality to mermaid\nexcludes weekdays 2019-02-01\nsection Documentation' },
  { name: 'spec-section-semicolon', spec: parserSpec('should handle a section definition'), text: 'gantt\ndateFormat yyyy-mm-dd\ntitle Adding gantt diagram functionality to mermaid\nexcludes weekdays 2019-02-01\nsection ;Documentation' },
  { name: 'spec-section-hash', spec: parserSpec('should handle a section definition'), text: 'gantt\ndateFormat yyyy-mm-dd\ntitle Adding gantt diagram functionality to mermaid\nexcludes weekdays 2019-02-01\nsection #Documentation' },
  { name: 'spec-section-breaks', spec: parserSpec('should handle multiline section titles with different line breaks'), text: 'gantt\ndateFormat yyyy-mm-dd\ntitle Adding gantt diagram functionality to mermaid\nexcludes weekdays 2019-02-01\nsection Line1<br>Line2<br/>Line3</br />Line4<br\t/>Line5' },
  { name: 'spec-task', spec: parserSpec('should handle a task definition'), text: 'gantt\ndateFormat YYYY-MM-DD\ntitle Adding gantt diagram functionality to mermaid\nsection Documentation\nDesign jison grammar:des1, 2014-01-01, 2014-01-04' },
  { name: 'spec-task-semicolon', spec: parserSpec('should handle a task definition'), text: 'gantt\ndateFormat YYYY-MM-DD\ntitle Adding gantt diagram functionality to mermaid\nsection Documentation\n;Design jison grammar:des1, 2014-01-01, 2014-01-04' },
  { name: 'spec-task-hash', spec: parserSpec('should handle a task definition'), text: 'gantt\ndateFormat YYYY-MM-DD\ntitle Adding gantt diagram functionality to mermaid\nsection Documentation\n#Design jison grammar:des1, 2014-01-01, 2014-01-04' },
  {
    name: 'spec-relative', spec: parserSpec('should handle a task with start/end time relative to other tasks'),
    text: 'gantt\ndateFormat YYYY-MM-DD\ntitle Adding gantt diagram functionality to mermaid\nsection Documentation\ntask A: a, 2024-01-27, 2024-01-28\ntask B: b, after a, 2024-01-30\ntask C: c, 2024-01-20, until a\ntask D: d, after c, until b'
  },
  ...['milestone', 'done', 'crit', 'active', 'crit,milestone,done'].map((tags, i) => ({
    name: `spec-tags-${i + 1}`, spec: parserSpec('should handle a task with tags $tags'),
    text: `gantt\ndateFormat YYYY-MM-DD\ntitle Adding gantt diagram functionality to mermaid\nsection Documentation\ntest task:${tags}, 2014-01-01, 2014-01-04`
  })),
  {
    name: 'spec-callback-no-args', spec: parserSpec('should parse callback specifier with no args'),
    text: 'gantt\ndateFormat  YYYY-MM-DD\nsection Clickable\nVisit mermaidjs           :active, cl1, 2014-01-07, 3d\nCalling a callback        :cl2, after cl1, 3d\n\nclick cl1 href "https://mermaidjs.github.io/"\nclick cl2 call ganttTestClick()\n'
  },
  {
    name: 'spec-callback-args', spec: parserSpec('should parse callback specifier with arbitrary number of args'),
    text: 'gantt\ndateFormat  YYYY-MM-DD\nsection Clickable\nVisit mermaidjs           :active, cl1, 2014-01-07, 3d\nCalling a callback        :cl2, after cl1, 3d\n\nclick cl1 href "https://mermaidjs.github.io/"\nclick cl2 call ganttTestClick("test0", test1, test2)\n'
  },
  {
    name: 'spec-acc', spec: parserSpec('should allow for a accessibility title and description (accDescr)'),
    text: 'gantt\n       accTitle: Gantt Diagram\n       accDescr: Tasks for Q4\n       dateFormat  YYYY-MM-DD\n       section Section\n       A task :a1, 2014-01-01, 30d\n'
  },
  {
    name: 'spec-acc-multiline', spec: parserSpec('should allow for a accessibility title and multiline description (accDescr)'),
    text: 'gantt\n       accTitle: Gantt Diagram\n       accDescr {\n         Tasks for Q4 row1\nrow2\n       }\n       dateFormat  YYYY-MM-DD\n       section Section\n       A task :a1, 2014-01-01, 30d\n'
  },
  ...['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'].map((day) => ({
    name: `spec-weekday-${day}`, spec: parserSpec('should allow for setting the starting weekday to %s for tick interval'), text: `gantt\nweekday ${day}`
  })),
  ...['__proto__', 'constructor'].map((prop) => ({
    name: `spec-link-${prop.replace(/_/g, '')}`, spec: parserSpec('should allow for a link to %s id'),
    text: `gantt\n    dateFormat YYYY-MM-DD\n    section Section\n    A task :${prop}, 2024-10-01, 3d\n    click ${prop} href "https://mermaid.js.org/"\n    `
  })),

  // ------------------------------------------------------------------------- diagrams/gantt/ganttDb.spec.ts as text
  ...[
    ['fixed-dates', 'should handle fixed dates', 'id1,2013-01-01,2013-01-12'],
    ['duration-days', 'should handle duration (days) instead of fixed date to determine end date', 'id1,2013-01-01,2d'],
    ['duration-hours', 'should handle duration (hours) instead of fixed date to determine end date', 'id1,2013-01-01,2h'],
    ['duration-minutes', 'should handle duration (minutes) instead of fixed date to determine end date', 'id1,2013-01-01,2m'],
    ['duration-seconds', 'should handle duration (seconds) instead of fixed date to determine end date', 'id1,2013-01-01,2s'],
    ['duration-weeks', 'should handle duration (weeks) instead of fixed date to determine end date', 'id1,2013-01-01,2w'],
    ['fixed-no-id', 'should handle fixed dates without id', '2013-01-01,2013-01-12'],
    ['duration-no-id', 'should handle duration instead of a fixed date to determine end date without id', '2013-01-01,4d']
  ].map(([name, test, data]) => ({ name: `db-${name}`, spec: dbSpec(test), text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section testa1', `test1 :${data}`) })),
  ...['id2,after id1,1d', 'id2,after id3,1d', 'after id1,1d', '2013-01-26', '2d'].map((data, i) => ({
    name: `db-second-task-${i + 1}`, spec: dbSpec('$testName (the second task)'),
    text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section testa1', 'test1 :id1,2013-01-01,2w', `test2 :${data}`)
  })),
  {
    name: 'db-milliseconds', spec: dbSpec('should handle milliseconds'),
    text: lines('gantt', 'dateFormat x', 'section testa1', 'test1 :id1,0,20ms', 'test2 :id2,after id1,5ms', 'section testa2', 'test3 :id3,20,10ms', 'test4 :id4,after id3,0.005s')
  },
  {
    name: 'db-relative-start-sections', spec: dbSpec('should handle relative start date based on id regardless of sections'),
    text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section sec1', 'test1 :id1,2013-01-01,2w', 'test2 :id2,after id3,1d', 'section sec2', 'test3 :id3,after id1,2d')
  },
  {
    name: 'db-relative-end-sections', spec: dbSpec('should handle relative end date based on id regardless of sections'),
    text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section sec1', 'task1 :id1,2013-01-01,until id3', 'section sec2', 'task2 :id2,2013-01-10,until id3', 'task3 :id3,2013-02-01,2d')
  },
  {
    name: 'db-relative-start-multiple', spec: dbSpec('should handle relative start date based on multiple id'),
    text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section sec1', 'task1 :id1,after id2 id3 id4,1d', 'task2 :id2,2013-01-01,1d', 'task3 :id3,2013-02-01,3d', 'task4 :id4,2013-02-01,2d')
  },
  {
    name: 'db-relative-end-multiple', spec: dbSpec('should handle relative end date based on multiple id'),
    text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section sec1', 'task1 :id1,2013-01-01,until id2 id3 id4', 'task2 :id2,2013-01-11,1d', 'task3 :id3,2013-02-10,1d', 'task4 :id4,2013-02-12,1d')
  },
  {
    name: 'db-weekends', spec: dbSpec('should ignore weekends'),
    text: lines('gantt', 'dateFormat YYYY-MM-DD', 'excludes weekends 2019-02-06,friday', 'section weekends skip test', 'test1 :id1,2019-02-01,1d',
      'test2 :id2,after id1,2d', 'test3 :id3,after id2,7d', 'test4 :id4,2019-02-01,2019-02-20', 'test5 :id5,after id4,1d',
      'section full ending task on last day', 'test6 :id6,2019-02-13,2d', 'test7 :id7,after id6,1d')
  },
  { name: 'db-weekend-friday', spec: dbSpec('should ignore weekends starting on friday'), text: lines('gantt', 'dateFormat YYYY-MM-DD', 'excludes weekends', 'weekend friday', 'section friday-saturday weekends skip test', 'test1 :id1,2024-02-28, 3d') },
  { name: 'db-exclude-everything', spec: dbSpec('should not infinite loop when excluding everything'), text: lines('gantt', 'dateFormat YYYY-MM-DD', 'excludes weekends,monday,tuesday,wednesday,thursday,friday', 'weekend saturday', 'section weekends skip test', 'test1 :id1,2019-02-01,7d') },
  { name: 'db-exclude-all-but-friday', spec: dbSpec('should not infinite loop when excluding everything'), text: lines('gantt', 'dateFormat YYYY-MM-DD', 'excludes weekends,monday,tuesday,wednesday,thursday', 'weekend saturday', 'section weekends skip test', 'test1 :id1,2019-02-01,7d') },
  { name: 'db-excludes-merge', spec: dbSpec('should merge tokens across multiple setExcludes calls (issue #6270)'), text: lines('gantt', 'excludes weekends', 'excludes 2019-02-06', 'excludes friday, monday') },
  { name: 'db-excludes-dedupe', spec: dbSpec('should dedupe tokens across multiple setExcludes calls (issue #6270)'), text: lines('gantt', 'excludes weekends,2019-02-06', 'excludes weekends 2019-02-07') },
  { name: 'db-includes-merge', spec: dbSpec('should merge tokens across multiple setIncludes calls (issue #6270)'), text: lines('gantt', 'includes 2019-02-06', 'includes 2019-02-07,2019-02-08') },
  { name: 'db-order', spec: dbSpec('should maintain the order in which tasks are created'), text: lines('gantt', 'accTitle: Project Execution', 'dateFormat YYYY-MM-DD', ...orderTasks) },
  { name: 'db-end-31st', spec: dbSpec('should work when end date is the 31st'), text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section Task endTime is on the 31st day of the month', 'test1 :id1,2019-09-30,11d', 'test2 :id2,after id1,20d') },
  { name: 'db-inclusive', spec: dbSpec('should automatically add one day to all end dates'), text: lines('gantt', 'dateFormat YYYY-MM-DD', 'inclusiveEndDates', 'test1 :id1,2019-02-01,1d', 'test2 :id2,2019-02-01,2019-02-03') },
  { name: 'db-today-marker-off', spec: dbSpec('should ${type} today marker'), text: lines('gantt', 'todayMarker off') },
  { name: 'db-today-marker-style', spec: dbSpec('should ${type} today marker'), text: lines('gantt', 'todayMarker stoke:stroke-width:5px,stroke:#00f,opacity:0.5') },
  { name: 'db-ridiculous-year', spec: dbSpec('should reject dates with ridiculous years'), text: lines('gantt', 'dateFormat YYYYMMDD', 'test1 :id1,202304,1d') },
  { name: 'db-seconds-only', spec: dbSpec('should handle seconds-only format with valid numeric values (issue #5496)'), text: lines('gantt', 'dateFormat ss', 'section Network Request', 'RTT :rtt, 0, 20') },
  { name: 'db-year-typo', spec: dbSpec('should handle dates with year typo like 202 instead of 2024 (issue #5496)'), text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section Vacation', 'London Trip 1 :2024-12-01, 7d', 'London Trip 2 :202-12-01, 7d') },

  // ------------------------------------------------------------------------------------------------- the lexer
  { name: 'lexer-case-insensitive', spec: 'original', text: lines('GANTT', 'DATEFORMAT YYYY-MM-DD', 'TITLE Upper', 'AXISFORMAT %d', 'Section Mixed', 'Task :a, 2024-01-01, 2d', 'CLICK a HREF "https://example.com"') },
  { name: 'lexer-gantt-glued', spec: 'original', text: lines('ganttx', 'A :2024-01-01, 1d') },
  { name: 'lexer-gantt-trailing-text', spec: 'original', text: lines('gantt title On the keyword line', 'A :2024-01-01, 1d') },
  { name: 'lexer-comment-line', spec: 'original', text: lines('gantt', '%% a comment', 'dateFormat YYYY-MM-DD', '  %% indented', 'A :a, 2024-01-01, 1d') },
  { name: 'lexer-comment-single-percent', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', '% single percent comment', 'A :a, 2024-01-01, 1d') },
  { name: 'lexer-comment-quirk-char-percent', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'A :a, 2024-01-01, 1d', 'x% skipped line', 'B :b, 2024-01-02, 1d') },
  { name: 'lexer-comment-quirk-brace', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'A :a, 2024-01-01, 1d', '}% kept', 'B :b, 2024-01-02, 1d') },
  { name: 'lexer-comment-after-task', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'A :a, 2024-01-01, 1d %% trailing', 'B :b, after a, 2d') },
  { name: 'lexer-percent-in-task', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'Half 50% done :a, 2024-01-01, 1d') },
  { name: 'lexer-hash-in-data', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'A :a, 2024-01-01, 1d # note', 'B :b, after a, 2d') },
  { name: 'lexer-semicolon-in-data', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'A :a, 2024-01-01, 1d; B :b, after a, 2d') },
  { name: 'lexer-date-token', spec: 'original', text: lines('gantt', '2024-01-01') },
  { name: 'lexer-acc-description-token', spec: 'original', text: lines('gantt', 'accDescription A description') },
  { name: 'lexer-top-axis-statement', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'topAxis', 'A :a, 2024-01-01, 1d') },
  { name: 'lexer-colon-only', spec: 'original', text: lines('gantt', ':#') },
  { name: 'lexer-task-without-data', spec: 'original', text: lines('gantt', 'A task without data') },
  { name: 'lexer-title-newline', spec: 'original', text: 'gantt\ntitle\nThe next line\nA :2024-01-01, 1d' },
  { name: 'lexer-section-newline', spec: 'original', text: 'gantt\ndateFormat YYYY-MM-DD\nsection\nNext line\nA :a, 2024-01-01, 1d' },
  { name: 'lexer-section-empty', spec: 'original', text: lines('gantt', 'section ', 'A :2024-01-01, 1d') },
  { name: 'lexer-title-spaces', spec: 'original', text: lines('gantt', 'title   Spaced   title  ', 'dateFormat   YYYY-MM-DD  ', 'A :a, 2024-01-01, 1d') },
  { name: 'lexer-crlf', spec: 'original', text: 'gantt\r\ndateFormat YYYY-MM-DD\r\ntitle CRLF\r\nsection S1\r\nA :a, 2024-01-01, 2d\r\nB :after a, 1d\r\n' },
  { name: 'lexer-tabs', spec: 'original', text: 'gantt\n\tdateFormat\tYYYY-MM-DD\n\tsection\tTabbed\n\tA\t:a,\t2024-01-01,\t2d' },
  { name: 'lexer-weekday-spaces', spec: 'original', text: lines('gantt', 'weekday    friday', 'weekend   friday') },
  { name: 'lexer-weekday-glued', spec: 'original', text: lines('gantt', 'weekday mondays') },
  { name: 'lexer-inclusive-glued', spec: 'original', text: lines('gantt', 'inclusiveEndDatesX') },
  { name: 'lexer-entities', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section R&amp;D #35;1', 'Fish &amp; chips :a, 2024-01-01, 1d') },
  { name: 'lexer-html', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'title <b>Bold</b> <script>x</script>', 'A <i>x</i> :a, 2024-01-01, 1d') },
  { name: 'lexer-today-marker-semicolon', spec: 'original', text: lines('gantt', 'todayMarker stroke:#f00;opacity:0.5') },
  { name: 'lexer-today-marker-hash', spec: 'original', text: lines('gantt', 'todayMarker stroke:#0f0,stroke-width:4px') },
  { name: 'lexer-statements-one-line', spec: 'original', text: 'gantt\ndateFormat YYYY-MM-DD;axisFormat %d;A :a, 2024-01-01, 1d' },
  { name: 'lexer-statements-hash', spec: 'original', text: 'gantt\naxisFormat %Y#x\ntickInterval 1day#x\nincludes 2024-01-01#x\nexcludes monday#x' },
  { name: 'lexer-open-directive', spec: 'original', text: lines('gantt', '%%{ }%%', 'A :2024-01-01, 1d') },
  { name: 'lexer-empty', spec: 'original', text: 'gantt' },
  { name: 'lexer-empty-newlines', spec: 'original', text: 'gantt\n\n\n' },

  // ------------------------------------------------------------------------------------------- click statements
  { name: 'click-href', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'click a href "https://example.com/a"') },
  { name: 'click-href-javascript', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'click a href "javascript:alert(1)"') },
  { name: 'click-call-href', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'click a call cb("x") href "https://example.com"') },
  { name: 'click-href-call', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'click a href "https://example.com" call cb()') },
  { name: 'click-href-call-args', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'click a href "https://example.com" call cb(1, "two")') },
  { name: 'click-call-only', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'click a call cb') },
  { name: 'click-many-ids', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'B :b, after a, 1d', 'click a,b,c href "https://example.com"') },
  { name: 'click-unknown-id', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'click zz href "https://example.com"') },
  { name: 'click-twice', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'click a href "https://one.example"', 'click a href "https://two.example"') },
  { name: 'click-before-task', spec: 'original', text: iso('click a href "https://example.com"', 'A :a, 2024-01-01, 1d') },
  { name: 'click-at-end', spec: 'original', text: 'gantt\nA :a, 2024-01-01, 1d\nclick ' },
  { name: 'click-no-command', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'click a', 'B :b, 2024-01-02, 1d') },
  { name: 'click-task-named-click', spec: 'original', text: iso('click me :a, 2024-01-01, 1d') },
  { name: 'click-task-named-call', spec: 'original', text: iso('call mom :a, 2024-01-01, 1d') },
  { name: 'click-task-named-clickable', spec: 'original', text: iso('clickable task :a, 2024-01-01, 1d') },
  { name: 'click-href-empty', spec: 'original', text: iso('A :a, 2024-01-01, 1d', 'click a href ""') },

  // --------------------------------------------------------------------------------------------- task data
  { name: 'data-one-field-first', spec: 'original', text: iso('A :3d') },
  { name: 'data-one-field-chain', spec: 'original', text: iso('A :2024-01-01, 2d', 'B :3d', 'C :2024-01-20') },
  { name: 'data-four-fields', spec: 'original', text: iso('A :a, b, 2024-01-01, 2d') },
  { name: 'data-tags-only', spec: 'original', text: iso('A :done') },
  { name: 'data-tag-vert-only', spec: 'original', text: iso('A :vert') },
  { name: 'data-tags-all', spec: 'original', text: iso('A :active, done, crit, milestone, vert, a, 2024-01-01, 1d') },
  { name: 'data-tags-repeated', spec: 'original', text: iso('A :crit, crit, a, 2024-01-01, 1d') },
  { name: 'data-tags-after-id', spec: 'original', text: iso('A :a, done, 2024-01-01') },
  { name: 'data-tags-spacing', spec: 'original', text: iso('A :  milestone  ,  a , 2024-01-01 ,  0d  ') },
  { name: 'data-tags-case', spec: 'original', text: iso('A :Done, a, 2024-01-01, 1d') },
  { name: 'data-empty-id', spec: 'original', text: iso('A : , 2024-01-01, 1d', 'B :after , 1d') },
  { name: 'data-id-reused', spec: 'original', text: iso('A :x, 2024-01-01, 1d', 'B :x, 2024-02-01, 1d', 'C :after x, 1d') },
  { name: 'data-vert', spec: 'original', text: iso('A :a, 2024-01-01, 5d', 'Release :vert, v1, 2024-01-03, 0d', 'B :b, after a, 2d') },
  { name: 'data-milestone', spec: 'original', text: iso('A :a, 2024-01-01, 5d', 'Done :milestone, m1, after a, 0d') },
  { name: 'data-no-section', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'A :a, 2024-01-01, 1d', 'section Later', 'B :after a, 1d') },
  { name: 'data-proto-section', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section __proto__', 'A :a, 2024-01-01, 1d', 'B :b, after a, 1d') },
  { name: 'data-numeric-sections', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'section 2', 'A :a, 2024-01-01, 1d', 'section 1', 'B :b, after a, 1d', 'section x', 'C :c, after b, 1d') },
  { name: 'data-proto-ids', spec: 'original', text: iso('A :toString, 2024-01-01, 1d', 'B :after toString, 1d', 'C :hasOwnProperty, after constructor, 1d') },
  { name: 'data-after-proto', spec: 'original', text: iso('A :__proto__, 2024-01-01, 1d', 'B :b, after __proto__, 1d', 'C :3d') },
  { name: 'data-undefined-id', spec: 'original', text: iso('Z :undefined, 2024-01-01, 1d', 'A :2d') },
  { name: 'data-colon-in-name', spec: 'original', text: iso('Time 10:30 meeting :a, 2024-01-01, 1d') },

  // ------------------------------------------------------------------------------------------------ durations
  ...['500ms', '90s', '45m', '36h', '3d', '2w', '1M', '1y', '1.5d', '0.5w', '2.5h', '1.5M', '1.5y', '0.4d', '0.5d', '0d',
    '1e3d', '-1d', '1D', '1 d', '1dd', 'd', '100000y', '1000000000d', '01d', '2.d', '.5d'
  ].map((duration) => ({
    name: `duration-${duration.replace(/\./g, 'p').replace(/ /g, 'sp').replace(/-/g, 'minus')}`,
    spec: 'original',
    text: iso(`A :a, 2024-01-31, ${duration}`)
  })),
  { name: 'duration-month-end', spec: 'original', text: iso('A :a, 2024-01-31, 1M', 'B :b, 2024-02-29, 1y', 'C :c, 2023-03-31, 1M') },

  // ------------------------------------------------------------------- dates without a dateFormat (new Date())
  ...['2024-01-05', '2024-01', '2024', '2024-01-05T10:00', '2024-01-05T10:00:30', '2024-01-05T10:00:30.250', '2024-01-05T10:00Z',
    '2024-01-05T10:00:00+02:00', '2024-01-05T10:00:00.5Z', '+002024-01-05', '-000001-01-01', '2024-02-30', '2024-13-01',
    '2024-01-32', '2024-01-05T24:00', '2024-01-05T25:00', '2024-1-5', 'Jan 5 2024', '20240105', '2024/01/05', '2024-01-05 10:00',
    '1e3', 'not a date', '9999-12-31', '+010001-01-01', '2024-01-05t10:00', '2024-01-05T10'
  ].map((date, i) => ({ name: `iso-${i + 1}`, spec: `original: new Date('${date}')`, text: lines('gantt', 'section S', `A :a, ${date}, 1d`) })),
  { name: 'iso-end-date', spec: 'original', text: lines('gantt', 'A :a, 2024-01-05, 2024-01-09', 'B :b, 2024-01-05, 2024-01-09T12:00') },

  // ------------------------------------------------------------------------------------ dayjs strict formats
  ...[
    ['DD-MM-YYYY', '05-01-2024', '08-01-2024'],
    ['YYYYMMDD', '20240105', '20240108'],
    ['YY-MM-DD', '24-01-05', '69-01-08'],
    ['YY-MM-DD', '70-01-05', '3d'],
    ['MM/DD/YYYY', '01/05/2024', '1/8/2024'],
    ['M/D/YYYY', '1/5/2024', '01/08/2024'],
    ['DD.MM.YYYY HH:mm', '05.01.2024 10:30', '05.01.2024 18:00'],
    ['HH:mm', '10:30', '12:00'],
    ['H:m', '9:5', '2h'],
    ['x', '1704412800000', '1704499200000'],
    ['X', '1704412800', '1704499200'],
    ['D', '5', '9'],
    ['Do MMMM YYYY', '5th January 2024', '8th January 2024'],
    ['MMMM D, YYYY', 'January 5, 2024', 'March 1, 2024'],
    ['MMM D, YYYY', 'Jan 5, 2024', 'Mar 1, 2024'],
    ['YYYY-MM-DDTHH:mm', '2024-01-05T10:30', '2024-01-0510:30'],
    ['YYYY-MM-DD HH:mm:ss.SSS', '2024-01-05 10:30:15.250', '2024-01-05 10:30:16.5'],
    ['hh:mm a', '09:30 am', '04:30 pm'],
    ['h:mm A', '9:30 AM', '12:00 PM'],
    ['Q YYYY', '2 2024', '3 2024'],
    ['YYYY-MM-DD ZZ', '2024-01-05 +0200', '2024-01-06 +0000'],
    ['YYYY-MM-DD Z', '2024-01-05 +02:00', '2024-01-06 +00:00'],
    ['YYYY w', '2024 5', '1d'],
    ['YYYY-MM-DD kk', '2024-01-05 24', '1d'],
    ['dddd YYYY-MM-DD', 'Friday 2024-01-05', '1d'],
    ['YYYY-MM-DD [at] HH', '2024-01-05 at 10', '2024-01-05 at 18'],
    ['yyyy-mm-dd', '2024-01-05', '2024-01-08'],
    ['L', '01/05/2024', '1d'],
    ['YYYY-MM-DD', '2024-1-5', '2024-01-08'],
    ['YYYY-MM-DD', '2024-02-30', '2024-03-08'],
    ['YYYY-MM-DD', ' 2024-01-05 ', ' 2024-01-08 '],
    ['  YYYY-MM-DD  ', '2024-01-05', '2024-01-08'],
    ['SSS', '250', '500'],
    ['ss', '0', '20'],
    ['mm:ss', '01:30', '02:00'],
    ['YYYY', '2024', '2025'],
    ['MM-YYYY', '03-2024', '05-2024'],
    ['GGGG', '2024', '1d'],
    ['YYYY-MM-DD W', '2024-01-05 1', '1d']
  ].map(([format, start, end], i) => ({ name: `format-${i + 1}`, spec: `original: dateFormat ${format}`, text: chart(format, `A :a, ${start}, ${end}`) })),
  { name: 'format-timestamp-x-after', spec: 'original', text: chart('x', 'A :a, 1000, 2000', 'B :b, after a, 500', 'C :c, 3000, 1500ms') },
  { name: 'format-timestamp-X-start', spec: 'original', text: chart('X', 'A :a, 1700000000, 1700000600') },

  // ------------------------------------------------------------------------------------------ after and until
  { name: 'after-forward', spec: 'original', text: iso('B :b, after a, 2d', 'A :a, 2024-01-01, 3d') },
  { name: 'after-unknown', spec: 'original', text: iso('A :a, after nothing, 2d') },
  { name: 'after-mixed-known-unknown', spec: 'original', text: iso('A :a, 2024-01-01, 3d', 'B :b, after nothing a, 2d') },
  { name: 'after-latest', spec: 'original', text: iso('A :a, 2024-01-01, 3d', 'B :b, 2024-01-02, 5d', 'C :c, after a b, 1d', 'D :d, after b a, 1d') },
  { name: 'after-order-unprocessed', spec: 'original', text: iso('A :a, 2024-01-01, 3d', 'C :c, after z a, 1d', 'D :d, after a z, 1d', 'Z :z, after y, 1d', 'Y :y, 2024-02-01, 1d') },
  { name: 'after-circular', spec: 'original', text: iso('A :a, after b, 1d', 'B :b, after a, 1d') },
  { name: 'after-self', spec: 'original', text: iso('A :a, after a, 1d') },
  { name: 'after-spaces', spec: 'original', text: iso('A :a, 2024-01-01, 3d', 'B :b, after   a, 2d', 'C :c, after a  b, 2d') },
  { name: 'after-dashes', spec: 'original', text: iso('A :task-1, 2024-01-01, 3d', 'B :b, after task-1, 2d') },
  { name: 'after-not-word', spec: 'original', text: iso('A :a.1, 2024-01-01, 3d', 'B :b, after a.1, 2d') },
  { name: 'after-uppercase', spec: 'original', text: iso('A :a, 2024-01-01, 3d', 'B :b, AFTER a, 2d') },
  { name: 'after-chain-long', spec: 'original', text: iso('E :e, after d, 1d', 'D :d, after c, 1d', 'C :c, after b, 1d', 'B :b, after a, 1d', 'A :a, 2024-01-01, 1d') },
  {
    name: 'after-chain-eleven', spec: 'original',
    text: iso(...Array.from({ length: 12 }, (_, i) => `T${12 - i} :t${12 - i}, after t${11 - i}, 1d`).slice(0, 11), 'T1 :t1, 2024-01-01, 1d')
  },
  { name: 'until-unknown', spec: 'original', text: iso('A :a, 2024-01-01, until nothing') },
  { name: 'until-earliest', spec: 'original', text: iso('A :a, 2024-01-01, until b c', 'B :b, 2024-01-10, 1d', 'C :c, 2024-01-05, 1d') },
  { name: 'until-forward-unprocessed', spec: 'original', text: iso('A :a, 2024-01-01, until b', 'B :b, after c, 1d', 'C :c, 2024-01-10, 1d') },
  { name: 'until-before-start', spec: 'original', text: iso('A :a, 2024-01-10, until b', 'B :b, 2024-01-05, 1d') },
  { name: 'first-task-no-start', spec: 'original', text: iso('A :2d', 'B :b, 2024-01-01, 1d') },
  { name: 'end-before-start', spec: 'original', text: iso('A :a, 2024-01-10, 2024-01-05') },

  // -------------------------------------------------------------------------------- excludes and includes
  { name: 'excludes-weekends-saturday', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'excludes weekends', 'A :a, 2024-01-05, 3d', 'B :b, 2024-01-06, 2d') },
  { name: 'excludes-day-names', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'excludes Monday, WEDNESDAY', 'A :a, 2024-01-01, 5d') },
  { name: 'excludes-dates-format', spec: 'original', text: lines('gantt', 'dateFormat DD/MM/YYYY', 'excludes 02/01/2024 2024-01-03', 'A :a, 01/01/2024, 4d') },
  { name: 'excludes-includes', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'excludes weekends', 'includes 2024-01-06', 'A :a, 2024-01-05, 3d') },
  { name: 'excludes-manual-end', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'excludes weekends', 'A :a, 2024-01-05, 2024-01-10') },
  { name: 'excludes-manual-end-other-format', spec: 'original', text: lines('gantt', 'dateFormat DD-MM-YYYY', 'excludes weekends', 'A :a, 05-01-2024, 10-01-2024') },
  { name: 'excludes-hours', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD HH:mm', 'excludes weekends', 'A :a, 2024-01-05 10:00, 36h') },
  { name: 'excludes-weekend-friday-sunday', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'excludes weekends', 'weekend friday', 'A :a, 2024-01-04, 4d') },
  { name: 'excludes-week-format-throws', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD w', 'excludes monday', 'A :a, 2024-01-05 1, 3d') },
  { name: 'excludes-long', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'excludes weekends', 'A :a, 2024-01-01, 300d') },
  { name: 'excludes-inclusive', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'inclusiveEndDates', 'excludes weekends', 'A :a, 2024-01-05, 2d', 'B :b, 2024-01-05, 2024-01-08') },
  { name: 'excludes-empty-tokens', spec: 'original', text: lines('gantt', 'excludes ,, weekends ,,', 'includes   ') },
  { name: 'excludes-case-dates', spec: 'original', text: lines('gantt', 'dateFormat MMM D YYYY', 'excludes Jan 3 2024', 'A :a, Jan 1 2024, 4d') },

  // --------------------------------------------------------------------------------------------- statements
  { name: 'statements-all', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'axisFormat %m/%d', 'tickInterval 1week', 'weekday monday', 'todayMarker stroke-width:5px', 'title A title', 'accTitle: Acc', 'accDescr: Descr', 'excludes weekends', 'includes 2024-01-06', 'inclusiveEndDates', 'section S', 'A :a, 2024-01-01, 3d') },
  { name: 'statements-last-wins', spec: 'original', text: lines('gantt', 'dateFormat YYYY-MM-DD', 'dateFormat DD-MM-YYYY', 'axisFormat %d', 'axisFormat %m', 'title One', 'title Two', 'A :a, 05-01-2024, 1d') },
  { name: 'statements-raw-values', spec: 'original', text: lines('gantt', 'dateFormat  YYYY-MM-DD  ', 'axisFormat  %d  ', 'tickInterval  2day ', 'todayMarker  off ') },
  { name: 'statements-acc-title-spaces', spec: 'original', text: lines('gantt', 'accTitle   :   spaced acc   ', 'accDescr :  d  ') },
  { name: 'statements-acc-descr-multiline', spec: 'original', text: lines('gantt', 'accDescr {', '   line one', '      line two', '}', 'A :2024-01-01, 1d') },
  { name: 'statements-title-empty-ish', spec: 'original', text: lines('gantt', 'title  ', 'A :2024-01-01, 1d') },
  { name: 'statements-weekend-invalid', spec: 'original', text: lines('gantt', 'weekend sunday') },
  { name: 'statements-display-mode-frontmatter', spec: 'original', text: lines('---', 'displayMode: compact', '---', 'gantt', 'dateFormat YYYY-MM-DD', 'A :a, 2024-01-01, 1d') },
  { name: 'statements-frontmatter-title', spec: 'original', text: lines('---', 'title: From the frontmatter', '---', 'gantt', 'A :a, 2024-01-01, 1d') },
  { name: 'statements-directive', spec: 'original', text: lines('%%{init: {"gantt": {"barHeight": 30}}}%%', 'gantt', 'A :a, 2024-01-01, 1d') },

  // ------------------------------------------------------------------------------------------- the fixtures
  ...fixtures
];
