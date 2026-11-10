// er-vector-cases.mjs - the ER diagram texts of gen-diagram-vectors.mjs --type er. Each case is { name, spec, text }
// (and an optional site configuration). The expectations are not written here: the generator records what the pinned
// mermaid makes of each text.
//
// Attribution: most texts are the inputs of mermaid's own ER parser tests at the pinned version (mermaid 11.17.2,
// packages/mermaid/src/diagrams/er/: parser/erDiagram.spec.js and parser/subgraph.spec.js; MIT, Copyright (c) 2014 -
// 2022 Knut Sveidqvist, the notice of LICENSE-mermaid.txt). Their spec field names the upstream file and test. Only the
// texts are taken (each text that a test gives to the parser, once); the upstream assertions are replaced by the facts
// mermaid records. The upstream entity name test runs about 90 characters in five forms; a sample of them is kept. The
// texts of erDb.spec.js, which calls the database directly, are written as diagram lines in the "original" cases that
// model them. The cases whose spec field is "original" cover features and quirks the upstream specs do not (the
// cardinality words on both sides, names that start like a keyword, aliases, styles, subgraphs and errors), and the
// "fixture" cases are the ER fixtures of the tests (their frontmatter is removed by mermaid's preprocessing; its
// configuration and title are not applied).

import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const provenance = 'The inputs are those of mermaid\'s ER parser specs (parser/erDiagram.spec.js, parser/subgraph.spec.js; MIT), named in each case\'s spec field, plus original texts and the ER fixtures.';

const lines = (...rows) => rows.join('\n');
const erSpec = (test) => `parser/erDiagram.spec.js: ${test}`;
const subgraphSpec = (test) => `parser/subgraph.spec.js: ${test}`;
const db = (test) => `original, modeled on erDb.spec.js: ${test}`;

// The ER fixtures of the tests (original), read from the fixture folder
const fixtureFolder = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'test',
  'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'er');
const fixtures = readdirSync(fixtureFolder).filter((f) => f.endsWith('.mmd')).sort().map((f) => ({
  name: `fixture-${f.replace(/\.mmd$/, '')}`,
  spec: `fixture er/${f}`,
  text: readFileSync(join(fixtureFolder, f), 'utf8')
}));

// Every cardinality word or symbol, as the left and the right side of a relationship
const leftCards = ['|o', '||', '}o', '}|', 'one or zero', 'zero or one', 'one or more', 'one or many', '1+', 'zero or more',
  'zero or many', '0+', 'many(0)', 'many(1)', 'many', 'only one', 'one', '1'];
const rightCards = ['o|', '||', 'o{', '|{', 'one or zero', 'zero or one', 'one or more', 'one or many', '1+', 'zero or more',
  'zero or many', '0+', 'many(0)', 'many(1)', 'many', 'only one', 'one', '1'];
const symbols = { '|o': 'bar-o', '||': 'bars', '}o': 'crow-o', '}|': 'crow-bar', 'o|': 'o-bar', 'o{': 'o-crow', '|{': 'bar-crow' };
const slug = (card) => symbols[card] ?? card.replace('+', '-plus').replace(/[()]/g, '').replace(/ /g, '-');

export const cases = [  // ---------------------------------------------------------------------------------------- parser/erDiagram.spec.js
  { name: "er-should-allow-stand-alone-entities-with-no-relationships", spec: erSpec("should allow stand-alone entities with no relationships"), text: `erDiagram
ISLAND
MAINLAND` },
  { name: "er-cannot-be-empty-quotes", spec: erSpec("entity name > cannot be empty quotes \"\""), text: `erDiagram
 ""
` },
  { name: "er-char-21-fails-if-not-surrounded-by-quotes", spec: erSpec("entity name > has non A-Za-z0-9_- chars > Blo!rf fails if not surrounded by quotes"), text: `erDiagram
 Blo!rf
` },
  { name: "er-char-23-single-occurrence", spec: erSpec("entity name > has non A-Za-z0-9_- chars > \"Blo#rf\" single occurrence"), text: `erDiagram
 "Blo#rf"
` },
  { name: "er-char-2a-repeated-occurrence", spec: erSpec("entity name > has non A-Za-z0-9_- chars > \"Blo**rf\" repeated occurrence"), text: `erDiagram
 "Blo**rf"
` },
  { name: "er-char-2d-single-occurrence", spec: erSpec("entity name > has non A-Za-z0-9_- chars > \"Blo-rf\" single occurrence"), text: `erDiagram
 "Blo-rf"
` },
  { name: "er-char-2d-cannot-start-with-the-character", spec: erSpec("entity name > has non A-Za-z0-9_- chars > \"-Blorf\" cannot start with the character"), text: `erDiagram
 "Blo--rf"
` },
  { name: "er-char-7c-single-occurrence", spec: erSpec("entity name > has non A-Za-z0-9_- chars > \"Blo|rf\" single occurrence"), text: `erDiagram
 "Blo|rf"
` },
  { name: "er-char-3a-fails-if-not-surrounded-by-quotes", spec: erSpec("entity name > has non A-Za-z0-9_- chars > Blo:rf fails if not surrounded by quotes"), text: `erDiagram
 Blo:rf
` },
  { name: "er-char-3a-single-occurrence", spec: erSpec("entity name > has non A-Za-z0-9_- chars > \"Blo:rf\" single occurrence"), text: `erDiagram
 "Blo:rf"
` },
  { name: "er-char-2e-single-occurrence", spec: erSpec("entity name > has non A-Za-z0-9_- chars > \"Blo.rf\" single occurrence"), text: `erDiagram
 "Blo.rf"
` },
  { name: "er-char-2e-ends-with", spec: erSpec("entity name > has non A-Za-z0-9_- chars > \"Blo.rf\" ends with"), text: `erDiagram
 "Blorf."
` },
  { name: "er-char-20ac-single-occurrence", spec: erSpec("entity name > has non A-Za-z0-9_- chars > \"Blo€rf\" single occurrence"), text: `erDiagram
 "Blo€rf"
` },
  { name: "er-cannot-contain-because-it-interferes-with-parsing", spec: erSpec("entity name > cannot contain % because it interferes with parsing comments"), text: `erDiagram
 "Blo%rf"
` },
  { name: "er-cannot-contain-because-it-could-start-and-escape-code", spec: erSpec("entity name > cannot contain \\ because it could start and escape code"), text: "erDiagram\n \"Blo\\rf\"\n" },
  { name: "er-cannot-newline-backspace-or-vertical-characters", spec: erSpec("entity name > cannot newline, backspace, or vertical characters"), text: `erDiagram
 "Blo
rf"
` },
  { name: "er-cannot-newline-backspace-or-vertical-characters-2", spec: erSpec("entity name > cannot newline, backspace, or vertical characters"), text: "erDiagram\n \"Blo\rrf\"\n" },
  { name: "er-cannot-newline-backspace-or-vertical-characters-3", spec: erSpec("entity name > cannot newline, backspace, or vertical characters"), text: "erDiagram\n \"Blo\brf\"\n" },
  { name: "er-cannot-newline-backspace-or-vertical-characters-4", spec: erSpec("entity name > cannot newline, backspace, or vertical characters"), text: "erDiagram\n \"Blo\u000brf\"\n" },
  { name: "er-can-contain-without-needing", spec: erSpec("entity name > can contain - _ without needing \"\""), text: `erDiagram
DUCK-BILLED_PLATYPUS
` },
  { name: "er-can-have-an-alias", spec: erSpec("entity name > can have an alias"), text: `erDiagram
foo["bar"]
` },
  { name: "er-can-have-an-alias-even-if-the-relationship-is-defined", spec: erSpec("entity name > can have an alias even if the relationship is defined before buzz"), text: `erDiagram
foo ||--o| bar : rel
buzz foo["batman"]
` },
  { name: "er-can-have-an-alias-even-if-the-relationship-is-defined-2", spec: erSpec("entity name > can have an alias even if the relationship is defined after buzz"), text: `erDiagram
buzz foo["batman"]
foo ||--o| bar : rel
` },
  { name: "er-can-start-with-an-underscore", spec: erSpec("entity name > can start with an underscore"), text: `erDiagram
_foo
` },
  { name: "er-should-allow-alphanumeric-characters-dashes-underscores", spec: erSpec("attribute name > should allow alphanumeric characters, dashes, underscores and brackets (not leading chars)"), text: `erDiagram
BOOK {
string myBookTitle
string MYBOOKSUBTITLE_1
string author-ref[name](1)
}` },
  { name: "er-should-allow-asterisk-at-the-start-of-attribute-name", spec: erSpec("attribute name > should allow asterisk at the start of attribute name"), text: `erDiagram
BOOK{
string *title}` },
  { name: "er-should-allow-asterisks-at-the-start-of-attribute", spec: erSpec("attribute name > should allow asterisks at the start of attribute declared with type and name"), text: `erDiagram
BOOK {
id *the_Primary_Key}` },
  { name: "er-should-allow-commas-within-an-attribute-type", spec: erSpec("attribute name > should allow commas within an attribute type"), text: `erDiagram
BUILDING {
public.geometry(point,4326) location}` },
  { name: "er-should-not-allow-leading-numbers-dashes-or-brackets", spec: erSpec("attribute name > should not allow leading numbers, dashes or brackets"), text: `erDiagram
BOOK {
string 0author
}` },
  { name: "er-should-not-allow-leading-numbers-dashes-or-brackets-2", spec: erSpec("attribute name > should not allow leading numbers, dashes or brackets"), text: `erDiagram
BOOK {
string -author
}` },
  { name: "er-should-not-allow-leading-numbers-dashes-or-brackets-3", spec: erSpec("attribute name > should not allow leading numbers, dashes or brackets"), text: `erDiagram
BOOK {
string [author
}` },
  { name: "er-should-not-allow-leading-numbers-dashes-or-brackets-4", spec: erSpec("attribute name > should not allow leading numbers, dashes or brackets"), text: `erDiagram
BOOK {
string ]author
}` },
  { name: "er-should-not-allow-leading-numbers-dashes-or-brackets-5", spec: erSpec("attribute name > should not allow leading numbers, dashes or brackets"), text: `erDiagram
BOOK {
string (author
}` },
  { name: "er-should-not-allow-leading-numbers-dashes-or-brackets-6", spec: erSpec("attribute name > should not allow leading numbers, dashes or brackets"), text: `erDiagram
BOOK {
string )author
}` },
  { name: "er-should-allow-special-characters-in-a-backtick-escaped", spec: erSpec("attribute name > should allow special characters in a backtick-escaped attribute name"), text: "erDiagram\nHOTEL {\nstring `geo.accuracy`\n}" },
  { name: "er-should-allow-a-backtick-escaped-attribute-name-together", spec: erSpec("attribute name > should allow a backtick-escaped attribute name together with a key and comment"), text: "erDiagram\nHOTEL {\nstring `geo.accuracy` PK \"the comment\"\n}" },
  { name: "er-should-allow-a-backtick-escaped-attribute-type", spec: erSpec("attribute name > should allow a backtick-escaped attribute type"), text: "erDiagram\nHOTEL {\n`my.type` address\n}" },
  { name: "er-should-allow-a-question-mark-suffix-on-an-attribute-type", spec: erSpec("attribute name > should allow a question mark suffix on an attribute type"), text: `erDiagram
PERSON {
string? alias
}` },
  { name: "er-should-allow-a-question-mark-suffix-on-an-attribute-type-2", spec: erSpec("attribute name > should allow a question mark suffix on an attribute type with a key and comment"), text: `erDiagram
PERSON {
string? alias UK "optional display name"
}` },
  { name: "er-should-allow-an-entity-with-a-single-attribute-to-be", spec: erSpec("should allow an entity with a single attribute to be defined"), text: `erDiagram
BOOK {
string title
}` },
  { name: "er-should-allow-an-entity-with-a-single-attribute-to-be-2", spec: erSpec("should allow an entity with a single attribute to be defined with a key"), text: `erDiagram
BOOK {
string title PK
}` },
  { name: "er-should-allow-an-entity-with-a-single-attribute-to-be-3", spec: erSpec("should allow an entity with a single attribute to be defined with a comment"), text: `erDiagram
BOOK {
string title "comment"
}` },
  { name: "er-should-allow-an-entity-with-a-single-attribute-to-be-4", spec: erSpec("should allow an entity with a single attribute to be defined with a key and a comment"), text: `erDiagram
BOOK {
string title PK "comment"
}` },
  { name: "er-should-allow-an-entity-with-attribute-starting-with-fk", spec: erSpec("should allow an entity with attribute starting with fk, pk or uk and a comment"), text: "erDiagram\nBOOK {\nint fk_title FK \n\nstring pk_author PK\nstring uk_address UK\nfloat pk_price PK \"comment\"\n}" },
  { name: "er-should-allow-an-entity-with-attributes-that-have-many", spec: erSpec("should allow an entity with attributes that have many constraints and comments"), text: `erDiagram
CUSTOMER {
int customer_number PK, FK "comment1"
datetime customer_status_start_datetime PK,UK, FK
datetime customer_status_end_datetime PK , UK "comment3"
string customer_firstname
string customer_lastname "comment5"
}` },
  { name: "er-should-allow-an-entity-with-attribute-that-has-a-generic", spec: erSpec("should allow an entity with attribute that has a generic type"), text: `erDiagram
BOOK {
type~T~ type
option~T~ readable "comment"
string id PK
}` },
  { name: "er-should-allow-an-entity-with-attribute-that-is-an-array", spec: erSpec("should allow an entity with attribute that is an array"), text: `erDiagram
BOOK {
string[] readers FK "comment"
string[] authors FK
}` },
  { name: "er-should-allow-an-entity-with-attribute-that-is-a-limited", spec: erSpec("should allow an entity with attribute that is a limited length string"), text: `erDiagram
BOOK {
character(10) isbn FK
varchar(5) postal_code "Five digits"
}` },
  { name: "er-should-allow-an-entity-with-multiple-attributes-to-be", spec: erSpec("should allow an entity with multiple attributes to be defined"), text: `erDiagram
BOOK {
string title
string author
float price
}` },
  { name: "er-should-allow-attribute-definitions-to-be-split-into", spec: erSpec("should allow attribute definitions to be split into multiple blocks"), text: `erDiagram
BOOK {
string title
}
BOOK {
string author
float price
}` },
  { name: "er-should-allow-an-empty-attribute-block", spec: erSpec("should allow an empty attribute block"), text: `erDiagram
BOOK {}` },
  { name: "er-should-allow-an-attribute-block-to-start-immediately", spec: erSpec("should allow an attribute block to start immediately after the entity name"), text: `erDiagram
BOOK{}` },
  { name: "er-should-allow-an-attribute-block-to-be-separated-from-the", spec: erSpec("should allow an attribute block to be separated from the entity name by spaces"), text: `erDiagram
BOOK         {}` },
  { name: "er-should-allow-whitespace-before-and-after-attribute", spec: erSpec("should allow whitespace before and after attribute definitions"), text: "erDiagram\nBOOK {\n  \n\n  string title\n\n  \n}" },
  { name: "er-should-allow-no-whitespace-before-and-after-attribute", spec: erSpec("should allow no whitespace before and after attribute definitions"), text: `erDiagram
BOOK{string title}` },
  { name: "er-should-associate-two-entities-correctly", spec: erSpec("should associate two entities correctly"), text: `erDiagram
CAR ||--o{ DRIVER : "insured for"` },
  { name: "er-should-not-create-duplicate-entities", spec: erSpec("should not create duplicate entities"), text: `erDiagram
CAR ||--o{ DRIVER : "insured for"
DRIVER ||--|| LICENSE : has` },
  { name: "er-should-create-the-role-specified", spec: erSpec("should create the role specified"), text: `erDiagram
TEACHER }o--o{ STUDENT : "is teacher of"` },
  { name: "er-should-allow-recursive-relationships", spec: erSpec("should allow recursive relationships"), text: `erDiagram
NODE ||--o{ NODE : "leads to"` },
  { name: "er-should-allow-for-a-accessibility-title-and-description", spec: erSpec("accessible title and description > should allow for a accessibility title and description (accDescr)"), text: `erDiagram
      accTitle: graph title
      accDescr: this graph is about stuff
      TEACHER }o--o{ STUDENT : "is teacher of"` },
  { name: "er-parses-a-multi-line-description-acc-descr", spec: erSpec("accessible title and description > parses a multi line description (accDescr)"), text: `erDiagram
      accTitle: graph title
      accDescr { this graph is
        about
        stuff
        }

      TEACHER }o--o{ STUDENT : "is teacher of"` },
  { name: "er-should-allow-more-than-one-relationship-between-the-same", spec: erSpec("should allow more than one relationship between the same two entities"), text: `erDiagram
CAR ||--o{ PERSON : "insured for"
CAR }o--|| PERSON : "owned by"` },
  { name: "er-should-handle-only-one-to-one-or-more-relationships", spec: erSpec("should handle only-one-to-one-or-more relationships"), text: `erDiagram
A ||--|{ B : has` },
  { name: "er-should-handle-only-one-to-zero-or-more-relationships", spec: erSpec("should handle only-one-to-zero-or-more relationships"), text: `erDiagram
A ||..o{ B : has` },
  { name: "er-should-handle-zero-or-one-to-zero-or-more-relationships", spec: erSpec("should handle zero-or-one-to-zero-or-more relationships"), text: `erDiagram
A |o..o{ B : has` },
  { name: "er-should-handle-zero-or-one-to-one-or-more-relationships", spec: erSpec("should handle zero-or-one-to-one-or-more relationships"), text: `erDiagram
A |o--|{ B : has` },
  { name: "er-should-handle-one-or-more-to-only-one-relationships", spec: erSpec("should handle one-or-more-to-only-one relationships"), text: `erDiagram
A }|--|| B : has` },
  { name: "er-should-handle-zero-or-more-to-only-one-relationships", spec: erSpec("should handle zero-or-more-to-only-one relationships"), text: `erDiagram
A }o--|| B : has` },
  { name: "er-should-handle-zero-or-more-to-zero-or-one-relationships", spec: erSpec("should handle zero-or-more-to-zero-or-one relationships"), text: `erDiagram
A }o..o| B : has` },
  { name: "er-should-handle-one-or-more-to-zero-or-one-relationships", spec: erSpec("should handle one-or-more-to-zero-or-one relationships"), text: `erDiagram
A }|..o| B : has` },
  { name: "er-should-handle-zero-or-one-to-only-one-relationships", spec: erSpec("should handle zero-or-one-to-only-one relationships"), text: `erDiagram
A |o..|| B : has` },
  { name: "er-should-handle-only-one-to-only-one-relationships", spec: erSpec("should handle only-one-to-only-one relationships"), text: `erDiagram
A ||..|| B : has` },
  { name: "er-should-handle-only-one-to-zero-or-one-relationships", spec: erSpec("should handle only-one-to-zero-or-one relationships"), text: `erDiagram
A ||--o| B : has` },
  { name: "er-should-handle-zero-or-one-to-zero-or-one-relationships", spec: erSpec("should handle zero-or-one-to-zero-or-one relationships"), text: `erDiagram
A |o..o| B : has` },
  { name: "er-should-handle-zero-or-more-to-zero-or-more-relationships", spec: erSpec("should handle zero-or-more-to-zero-or-more relationships"), text: `erDiagram
A }o--o{ B : has` },
  { name: "er-should-handle-one-or-more-to-one-or-more-relationships", spec: erSpec("should handle one-or-more-to-one-or-more relationships"), text: `erDiagram
A }|..|{ B : has` },
  { name: "er-should-handle-zero-or-more-to-one-or-more-relationships", spec: erSpec("should handle zero-or-more-to-one-or-more relationships"), text: `erDiagram
A }o--|{ B : has` },
  { name: "er-should-handle-one-or-more-to-zero-or-more-relationships", spec: erSpec("should handle one-or-more-to-zero-or-more relationships"), text: `erDiagram
A }|..o{ B : has` },
  { name: "er-should-handle-zero-or-one-to-zero-or-more-relationships-2", spec: erSpec("should handle zero-or-one-to-zero-or-more relationships (aliases \"one or zero\" and \"zero or many\")"), text: `erDiagram
A one or zero to many B : has` },
  { name: "er-should-handle-one-or-more-to-zero-or-one-relationships-2", spec: erSpec("should handle one-or-more-to-zero-or-one relationships (aliases \"one or many\" and \"zero or one\")"), text: `erDiagram
A one or many optionally to zero or one B : has` },
  { name: "er-should-handle-zero-or-more-to-zero-or-more-relationships-2", spec: erSpec("should handle zero-or-more-to-zero-or-more relationships (aliases \"zero or more\" and \"zero or many\")"), text: `erDiagram
A zero or more to zero or many B : has` },
  { name: "er-should-handle-zero-or-more-to-one-or-more-relationships-2", spec: erSpec("should handle zero-or-more-to-one-or-more relationships (aliases \"many(0)\" and \"many(1)\")"), text: `erDiagram
A many(0) to many(1) B : has` },
  { name: "er-should-handle-zero-or-more-to-only-one-relationships-2", spec: erSpec("should handle zero-or-more-to-only-one relationships (aliases \"many(0)\" and \"many(1)\")"), text: `erDiagram
A many optionally to one B : has` },
  { name: "er-should-handle-only-one-to-only-one-relationships-aliases", spec: erSpec("should handle only-one-to-only-one relationships (aliases \"only one\" and  \"1+\")"), text: `erDiagram
A only one optionally to 1+ B : has` },
  { name: "er-should-handle-zero-or-more-to-only-one-relationships-3", spec: erSpec("should handle zero-or-more-to-only-one relationships (aliases \"0+\" and  \"1\")"), text: `erDiagram
A 0+ optionally to 1 B : has` },
  { name: "er-should-handle-1-alias-directly-followed-by-identifying", spec: erSpec("should handle \"1\" alias directly followed by identifying relationship operator \"--\""), text: `erDiagram
CUSTOMER 1--one or more DELIVERY-ADDRESS : has` },
  { name: "er-should-handle-1-alias-directly-followed-by-non", spec: erSpec("should handle \"1\" alias directly followed by non-identifying relationship operator \"..\""), text: `erDiagram
CUSTOMER 1..one or more DELIVERY-ADDRESS : has` },
  { name: "er-should-handle-1-alias-directly-followed-by-mixed", spec: erSpec("should handle \"1\" alias directly followed by mixed relationship operator \".-\""), text: `erDiagram
CUSTOMER 1.-one or more DELIVERY-ADDRESS : has` },
  { name: "er-should-handle-1-alias-directly-followed-by-mixed-2", spec: erSpec("should handle \"1\" alias directly followed by mixed relationship operator \"-.\""), text: `erDiagram
CUSTOMER 1-.one or more DELIVERY-ADDRESS : has` },
  { name: "er-should-allow-numeric-entity-identifier-1-on-right-side", spec: erSpec("should allow numeric entity identifier \"1\" on right side with cardinality 1 (issue #7472)"), text: `erDiagram
a many to 1 1: label` },
  { name: "er-should-allow-numeric-entity-identifier-12-on-right-side", spec: erSpec("should allow numeric entity identifier \"12\" on right side with cardinality 1 (issue #7472 boundary test)"), text: `erDiagram
a many to 1 12: label` },
  { name: "er-should-represent-identifying-relationships-properly", spec: erSpec("should represent identifying relationships properly"), text: `erDiagram
HOUSE ||--|{ ROOM : contains` },
  { name: "er-should-represent-identifying-relationships-properly-2", spec: erSpec("should represent identifying relationships properly (alias \"to\")"), text: `erDiagram
HOUSE one to one ROOM : contains` },
  { name: "er-should-represent-non-identifying-relationships-properly", spec: erSpec("should represent non-identifying relationships properly"), text: `erDiagram
 PERSON ||..o{ POSSESSION : owns` },
  { name: "er-should-represent-non-identifying-relationships-properly-2", spec: erSpec("should represent non-identifying relationships properly (alias \"optionally to\")"), text: `erDiagram
 PERSON many optionally to many POSSESSION : owns` },
  { name: "er-should-not-accept-a-syntax-error", spec: erSpec("should not accept a syntax error"), text: `erDiagram
A xxx B : has` },
  { name: "er-should-be-possible-to-apply-a-style-to-an-entity", spec: erSpec("should be possible to apply a style to an entity"), text: "erDiagram\n        CUSTOMER\n        style CUSTOMER color:red\n      " },
  { name: "er-should-be-possible-to-apply-multiple-styles-to-an-entity", spec: erSpec("should be possible to apply multiple styles to an entity at the same time"), text: "erDiagram\n        CUSTOMER\n        style CUSTOMER color:red,stroke:blue,fill:#f9f\n      " },
  { name: "er-should-be-possible-to-apply-multiple-separately-defined", spec: erSpec("should be possible to apply multiple separately defined styles"), text: "erDiagram\n        CUSTOMER\n        style CUSTOMER color:red\n        style CUSTOMER fill:#f9f\n      " },
  { name: "er-should-be-possible-to-assign-a-class-to-an-entity", spec: erSpec("should be possible to assign a class to an entity"), text: `erDiagram
CUSTOMER
class CUSTOMER myClass` },
  { name: "er-should-be-possible-to-assign-multiple-classes-to-an", spec: erSpec("should be possible to assign multiple classes to an entity at the same time"), text: `erDiagram
CUSTOMER
class CUSTOMER firstClass, secondClass, thirdClass` },
  { name: "er-should-be-possible-to-assign-multiple-separately-defined", spec: erSpec("should be possible to assign multiple separately defined classes to an entity"), text: `erDiagram
CUSTOMER
class CUSTOMER firstClass
class CUSTOMER secondClass` },
  { name: "er-should-be-possible-to-configure-the-default-class-and", spec: erSpec("should be possible to configure the default class and have it apply to each entity"), text: "erDiagram\n        ENTITY1\n        ENTITY2\n        classDef default fill:#f9f\n      " },
  { name: "er-should-be-possible-to-define-a-class-with-styles", spec: erSpec("should be possible to define a class with styles"), text: "erDiagram\n        classDef myClass fill:#f9f, stroke: red, color: pink\n      " },
  { name: "er-should-be-possible-to-define-multiple-class-with-styles", spec: erSpec("should be possible to define multiple class with styles at the same time"), text: "erDiagram\n        classDef firstClass,secondClass fill:#f9f, stroke: red, color: pink\n       " },
  { name: "er-should-be-possible-to-assign-a-class-using-the-shorthand", spec: erSpec("should be possible to assign a class using the shorthand syntax just by itself"), text: `erDiagram
CUSTOMER:::myClass` },
  { name: "er-should-be-possible-to-assign-a-class-using-the-shorthand-2", spec: erSpec("should be possible to assign a class using the shorthand syntax with empty block"), text: `erDiagram
CUSTOMER:::myClass {}` },
  { name: "er-should-be-possible-to-assign-a-class-using-the-shorthand-3", spec: erSpec("should be possible to assign a class using the shorthand syntax with block of attributes"), text: `erDiagram
CUSTOMER:::myClass {
string name
}` },
  { name: "er-should-be-possible-to-assign-multiple-classes-using-the", spec: erSpec("should be possible to assign multiple classes using the shorthand syntax"), text: `erDiagram
CUSTOMER:::firstClass,secondClass` },
  { name: "er-should-be-possible-to-assign-classes-using-the-shorthand", spec: erSpec("should be possible to assign classes using the shorthand syntax after defining an alias"), text: `erDiagram
c[CUSTOMER]:::myClass` },
  { name: "er-should-be-possible-to-assign-classes-using-the-shorthand-2", spec: erSpec("should be possible to assign classes using the shorthand syntax while defining a relationship"), text: `erDiagram
CUSTOMER:::myClass ||--o{ PERSON:::myClass : allows` },
  { name: "er-should-allow-an-empty-quoted-label", spec: erSpec("relationship labels > should allow an empty quoted label"), text: `erDiagram
CUSTOMER ||--|{ ORDER : ""` },
  { name: "er-should-allow-a-non-empty-quoted-label", spec: erSpec("relationship labels > should allow a non-empty quoted label"), text: `erDiagram
CUSTOMER ||--|{ ORDER : "places"` },
  { name: "er-should-allow-a-non-empty-unquoted-label", spec: erSpec("relationship labels > should allow a non-empty unquoted label"), text: `erDiagram
CUSTOMER ||--|{ ORDER : places` },
  { name: "er-should-represent-parent-child-relationship-correctly", spec: erSpec("relationship labels > should represent parent-child relationship correctly"), text: `erDiagram
PROJECT u--o{ TEAM_MEMBER : "parent"` },
  { name: "er-should-work-with-a-proto-property", spec: erSpec("prototype properties > should work with a __proto__ property"), text: `erDiagram
__proto__ ||--|{ ORDER : place` },
  { name: "er-should-work-with-a-constructor-property", spec: erSpec("prototype properties > should work with a constructor property"), text: `erDiagram
constructor ||--|{ ORDER : place` },
  { name: "er-should-work-with-a-prototype-property", spec: erSpec("prototype properties > should work with a prototype property"), text: `erDiagram
prototype ||--|{ ORDER : place` },
  { name: "er-should-allow-number-1-as-standalone-entity", spec: erSpec("syntax fixes for special characters and numbers > standalone entity names > should allow number \"1\" as standalone entity"), text: `erDiagram
CUSTOMER }|..|{ DELIVERY-ADDRESS : has
1` },
  { name: "er-should-allow-character-u-as-standalone-entity", spec: erSpec("syntax fixes for special characters and numbers > standalone entity names > should allow character \"u\" as standalone entity"), text: `erDiagram
CUSTOMER }|..|{ DELIVERY-ADDRESS : has
u` },
  { name: "er-should-allow-decimal-numbers-as-standalone-entities", spec: erSpec("syntax fixes for special characters and numbers > standalone entity names > should allow decimal numbers as standalone entities"), text: `erDiagram
CUSTOMER }|..|{ DELIVERY-ADDRESS : has
2.5` },
  { name: "er-should-allow-decimal-numbers-as-standalone-entities-2", spec: erSpec("syntax fixes for special characters and numbers > standalone entity names > should allow decimal numbers as standalone entities"), text: `erDiagram
CUSTOMER }|..|{ DELIVERY-ADDRESS : has
1.5` },
  { name: "er-should-allow-decimal-numbers-as-standalone-entities-3", spec: erSpec("syntax fixes for special characters and numbers > standalone entity names > should allow decimal numbers as standalone entities"), text: `erDiagram
CUSTOMER }|..|{ DELIVERY-ADDRESS : has
0.1` },
  { name: "er-should-allow-decimal-numbers-as-standalone-entities-4", spec: erSpec("syntax fixes for special characters and numbers > standalone entity names > should allow decimal numbers as standalone entities"), text: `erDiagram
CUSTOMER }|..|{ DELIVERY-ADDRESS : has
99.99` },
  { name: "er-should-allow-u-as-entity-name-with-attributes", spec: erSpec("syntax fixes for special characters and numbers > entity names with attributes > should allow \"u\" as entity name with attributes"), text: `erDiagram
u {
string name
int id
}` },
  { name: "er-should-allow-number-1-as-entity-name-with-attributes", spec: erSpec("syntax fixes for special characters and numbers > entity names with attributes > should allow number \"1\" as entity name with attributes"), text: `erDiagram
1 {
string name
int id
}` },
  { name: "er-should-allow-decimal-numbers-as-entity-names-with", spec: erSpec("syntax fixes for special characters and numbers > entity names with attributes > should allow decimal numbers as entity names with attributes"), text: `erDiagram
2.5 {
string name
int id
}` },
  { name: "er-should-allow-decimal-numbers-as-entity-names-with-2", spec: erSpec("syntax fixes for special characters and numbers > entity names with attributes > should allow decimal numbers as entity names with attributes"), text: `erDiagram
1.5 {
string value
}` },
  { name: "er-should-allow-u-in-relationships", spec: erSpec("syntax fixes for special characters and numbers > entity names in relationships > should allow \"u\" in relationships"), text: `erDiagram
CUSTOMER ||--|| u : has` },
  { name: "er-should-allow-u-in-relationships-2", spec: erSpec("syntax fixes for special characters and numbers > entity names in relationships > should allow \"u\" in relationships"), text: `erDiagram
u ||--|| ORDER : places` },
  { name: "er-should-allow-u-in-relationships-3", spec: erSpec("syntax fixes for special characters and numbers > entity names in relationships > should allow \"u\" in relationships"), text: `erDiagram
u ||--|| v : connects` },
  { name: "er-should-allow-numbers-in-relationships", spec: erSpec("syntax fixes for special characters and numbers > entity names in relationships > should allow numbers in relationships"), text: `erDiagram
CUSTOMER ||--|| 1 : has` },
  { name: "er-should-allow-numbers-in-relationships-2", spec: erSpec("syntax fixes for special characters and numbers > entity names in relationships > should allow numbers in relationships"), text: `erDiagram
1 ||--|| ORDER : places` },
  { name: "er-should-allow-numbers-in-relationships-3", spec: erSpec("syntax fixes for special characters and numbers > entity names in relationships > should allow numbers in relationships"), text: `erDiagram
1 ||--|| 2 : connects` },
  { name: "er-should-allow-decimal-numbers-in-relationships", spec: erSpec("syntax fixes for special characters and numbers > entity names in relationships > should allow decimal numbers in relationships"), text: `erDiagram
CUSTOMER ||--|| 2.5 : has` },
  { name: "er-should-allow-decimal-numbers-in-relationships-2", spec: erSpec("syntax fixes for special characters and numbers > entity names in relationships > should allow decimal numbers in relationships"), text: `erDiagram
1.5 ||--|| ORDER : places` },
  { name: "er-should-allow-decimal-numbers-in-relationships-3", spec: erSpec("syntax fixes for special characters and numbers > entity names in relationships > should allow decimal numbers in relationships"), text: `erDiagram
2.5 ||--|| 5.5 : connects` },
  { name: "er-should-handle-complex-diagram-with-special-entity-names", spec: erSpec("syntax fixes for special characters and numbers > mixed scenarios > should handle complex diagram with special entity names"), text: "erDiagram\n              CUSTOMER ||--o{ 1 : places\n              1 ||--|{ u : contains\n              u {\n                string name\n                int quantity\n              }\n              \"2.5\" ||--|| ORDER : processes\n              ORDER {\n                int id\n                date created\n              }\n        " },
  { name: "er-should-handle-attributes-with-numbers-in-names-but-not", spec: erSpec("syntax fixes for special characters and numbers > mixed scenarios > should handle attributes with numbers in names (but not starting)"), text: "erDiagram\n              ENTITY {\n                string name1\n                int value2\n                float point3_5\n              }\n        " },

  // ----------------------------------------------------------------------------------------- parser/subgraph.spec.js
  { name: "subgraph-should-handle-subgraph-with-simple-nodes-and-id-title", spec: subgraphSpec("should handle subgraph with simple nodes and id/title"), text: `erDiagram
subgraph Group
A
B
end` },
  { name: "subgraph-should-handle-subgraph-with-multiple-words-in-id-title", spec: subgraphSpec("should handle subgraph with multiple words in id/title"), text: `erDiagram
subgraph "Some Title"
A
B
end` },
  { name: "subgraph-should-handle-subgraph-with-id-and-title-notation", spec: subgraphSpec("should handle subgraph with id and title notation"), text: `erDiagram
subgraph id [Title]
A
B
end` },
  { name: "subgraph-should-handle-special-characters-in-subgraph-title", spec: subgraphSpec("should handle special characters in subgraph title"), text: `erDiagram
subgraph "Group 1.5"
A
end` },
  { name: "subgraph-should-handle-empty-lines-inside-subgraph", spec: subgraphSpec("should handle empty lines inside subgraph"), text: `erDiagram
subgraph Group

A

B

end` },
  { name: "subgraph-should-ignore-redundant-whitespace-around-end", spec: subgraphSpec("should ignore redundant whitespace around end"), text: "erDiagram\nsubgraph Group\nA\nend   \n" },
  { name: "subgraph-should-handle-empty-subgraph", spec: subgraphSpec("should handle empty subgraph"), text: `erDiagram
subgraph empty
end` },
  { name: "subgraph-should-correctly-parse-direction-rl-inside-a-subgraph", spec: subgraphSpec("should correctly parse direction RL inside a subgraph"), text: `erDiagram subgraph WithRL
direction RL
A
B
end` },
  { name: "subgraph-should-include-both-endpoints-of-a-relationship-inside-a", spec: subgraphSpec("should include both endpoints of a relationship inside a subgraph"), text: `erDiagram
subgraph Group
A }|--|| B : "relates to"
end` },
  { name: "subgraph-should-handle-relationship-between-subgraph-entity-and", spec: subgraphSpec("should handle relationship between subgraph entity and external entity"), text: `erDiagram
subgraph Group
A
end
A }|--|| B : "relates to"
` },
  { name: "subgraph-should-handle-relationship-between-subgraphs", spec: subgraphSpec("should handle relationship between subgraphs"), text: `erDiagram
subgraph G1
  A
end
subgraph G2
  B
end
G1 }|--|| G2 : "relates to"
` },
  { name: "subgraph-should-handle-relationship-between-entities-in-different", spec: subgraphSpec("should handle relationship between entities in different subgraphs"), text: `erDiagram
subgraph G1
  A
end
subgraph G2
  B
end
A }|--|| B : "relates to"
` },
  { name: "subgraph-should-handle-relationship-between-entity-and-subgraph", spec: subgraphSpec("should handle relationship between entity and subgraph"), text: `erDiagram
subgraph Group
A
end
Group }|--|| B : "relates to"
` },
  { name: "subgraph-should-handle-multiple-relationships-inside-a-subgraph", spec: subgraphSpec("should handle multiple relationships inside a subgraph"), text: `erDiagram
subgraph Group
  A }|--|| B : "r1"
  B }|--|| C : "r2"
end` },
  { name: "subgraph-should-handle-nested-subgraphs-correctly", spec: subgraphSpec("should handle nested subgraphs correctly"), text: `erDiagram
subgraph outer
  A
  subgraph inner
    B
  end
end` },
  { name: "subgraph-should-handle-nested-subgraphs-with-irregular", spec: subgraphSpec("should handle nested subgraphs with irregular indentation"), text: `erDiagram
subgraph outer
    A
      B
  subgraph inner
        C
  end
end` },
  { name: "subgraph-should-handle-relationships-inside-nested-subgraphs", spec: subgraphSpec("should handle relationships inside nested subgraphs"), text: `erDiagram
subgraph outer
  subgraph inner
    A }|--|| B : "relates to"
  end
end` },
  { name: "subgraph-should-handle-relationship-between-outer-and-inner", spec: subgraphSpec("should handle relationship between outer and inner subgraph entities"), text: `erDiagram
subgraph outer
  A
  subgraph inner
    B
  end
end
A }|--|| B : "relates to"
` },

  // --------------------------------------------------------------------------- original: erDb.spec.js as diagram text
  { name: 'db-subgraph-title-and-nodes', spec: db('adds subgraphs and returns provided id, title and nodes'), text: lines('erDiagram', 'subgraph sub1 ["Group One"]', '  A', '  B', 'end') },
  { name: 'db-subgraph-without-title', spec: db('handles subgraph without explicit title fallback'), text: lines('erDiagram', 'subgraph sub1', '  A', 'end') },
  { name: 'db-subgraph-nodes-unique', spec: db('prevents nodes from being added to subsequent subgraphs if already present'), text: lines('erDiagram', 'subgraph sub1', '  A', '  B', 'end', 'subgraph sub2', '  A', '  C', 'end') },
  { name: 'db-subgraph-all-nodes-taken', spec: db('allows full removal if all nodes already exist elsewhere'), text: lines('erDiagram', 'subgraph sub1', '  A', 'end', 'subgraph sub2', '  A', 'end') },
  { name: 'db-relationship-to-subgraph', spec: db('stores relationships with subgraph id when entity is a subgraph'), text: lines('erDiagram', 'subgraph sub1', '  X', 'end', 'sub1 ||--o| Y : roleA') },

  // ------------------------------------------------------------------- original: every cardinality on both sides
  ...leftCards.map((card, i) => ({ name: `card-left-${slug(card)}`, spec: 'original', text: lines('erDiagram', `A ${card}--${rightCards[(i + 3) % rightCards.length]} B : rel`) })),
  ...rightCards.map((card, i) => ({ name: `card-right-${slug(card)}`, spec: 'original', text: lines('erDiagram', `A ${leftCards[(i + 5) % leftCards.length]} to ${card} B : rel`) })),

  // ------------------------------------------------------------------------------------- original: not in the specs
  { name: 'card-words-without-spaces', spec: 'original', text: lines('erDiagram', 'A |o..o| B : x', 'C }|--|{ D : y', 'E one or zero..zero or one F : z') },
  { name: 'rel-types-symbols', spec: 'original', text: lines('erDiagram', 'A ||--|| B : identifying', 'A ||..|| C : "non identifying"', 'A ||.-|| F : mixed1', 'A ||-.|| G : mixed2') },
  { name: 'rel-types-words', spec: 'original', text: lines('erDiagram', 'A only one to only one D : to', 'A only one optionally to only one E : optional') },
  { name: 'error-rel-type-word-after-symbol', spec: 'original', text: lines('erDiagram', 'A || to || D : to') },
  { name: 'names-starting-like-keywords', spec: 'original', text: lines('erDiagram', 'onerous ||--o{ toaster : uses', 'manyfold ||--|| user : has', 'only ||--|| zero : rel') },
  { name: 'names-quoted-with-spaces', spec: 'original', text: lines('erDiagram', '"Two Words" ||--o{ "Three Word Name" : "has many"', '"Two Words" {', '  string id PK', '}') },
  { name: 'role-quoted-empty-and-unquoted', spec: 'original', text: lines('erDiagram', 'A ||--|| B : ""', 'B ||--|| C : plain words here', 'C ||--|| D : "quoted: with colon"') },
  { name: 'error-missing-role', spec: 'original', text: lines('erDiagram', 'A ||--o{ B') },
  { name: 'error-alias-in-relationship', spec: 'original', text: lines('erDiagram', 'p["Person"] ||--o{ q : rel') },
  { name: 'alias-forms', spec: 'original', text: lines('erDiagram', 'a["Quoted alias"]', 'b[Bare]', 'c["First"]', 'c["Second is ignored"]', 'd', 'd["Late alias"]') },
  { name: 'alias-with-block-and-class', spec: 'original', text: lines('erDiagram', 'classDef hot fill:#f96', 'e["Entity"]:::hot {', '  int id PK', '}') },
  { name: 'attributes-repeated-blocks', spec: 'original', text: lines('erDiagram', 'A {', '  int one', '}', 'A {', '  int two', '}', 'A ||--|| B : r', 'A {', '  int three', '}') },
  { name: 'attributes-keys-all-forms', spec: 'original', text: lines('erDiagram', 'T {', '  int a PK', '  int b FK', '  int c UK', '  int d PK, FK', '  int e PK,UK', '  int f FK, UK, PK "all three"', '  int g "only comment"', '}') },
  { name: 'attributes-key-like-names', spec: 'original', text: lines('erDiagram', 'T {', '  int PKey', '  string fkName', '  string uk_code UK', '}') },
  { name: 'attributes-types', spec: 'original', text: lines('erDiagram', 'T {', '  List~int~ ids', '  Map~string,List~int~~ nested', '  string[] tags', '  decimal? amount', '  varchar(255) note', '  public.geo(point,4326) area', '  *int starred', '}') },
  { name: 'attributes-comment-with-quotes-content', spec: 'original', text: lines('erDiagram', 'T {', '  string name "a comment with: colon, comma and PK"', '}') },
  { name: 'direction-root', spec: 'original', text: lines('erDiagram', 'direction RL', 'A ||--o{ B : r', 'direction BT') },
  { name: 'styles-classdef-color-text-styles', spec: 'original', text: lines('erDiagram', 'classDef warm fill:#f96,color:#036,stroke:#333', 'A:::warm', 'B', 'class B warm') },
  { name: 'styles-classdef-default', spec: 'original', text: lines('erDiagram', 'classDef default fill:#eee,stroke:#999', 'A ||--|| B : r') },
  { name: 'styles-style-before-entity', spec: 'original', text: lines('erDiagram', 'style A fill:#f96', 'A ||--|| B : r') },
  { name: 'styles-class-unknown-entity', spec: 'original', text: lines('erDiagram', 'classDef hot fill:#f96', 'class Missing hot', 'A') },
  { name: 'styles-subgraph', spec: 'original', text: lines('erDiagram', 'classDef box fill:#eef', 'subgraph S', '  A', 'end', 'class S box', 'style S stroke:#f00') },
  { name: 'subgraph-entity-ids-in-two', spec: 'original', text: lines('erDiagram', 'subgraph First', '  A ||--o{ B : r', 'end', 'subgraph Second', '  B ||--o{ C : s', 'end') },
  { name: 'subgraph-direction-and-nesting', spec: 'original', text: lines('erDiagram', 'subgraph Outer', '  direction LR', '  subgraph Inner', '    direction TB', '    A ||--|| B : r', '  end', '  C', 'end') },
  { name: 'subgraph-title-forms', spec: 'original', text: lines('erDiagram', 'subgraph s1["Quoted title"]', '  A', 'end', 'subgraph s2 [Bare title]', '  B', 'end', 'subgraph "three four"', '  C', 'end') },
  { name: 'error-subgraph-id-keyword', spec: 'original', text: lines('erDiagram', 'subgraph one["Quoted title"]', '  A', 'end') },
  { name: 'acc-title-and-descr', spec: 'original', text: lines('erDiagram', 'accTitle: The title', 'accDescr {', '  first line', '  second line', '}', 'A') },
  { name: 'title-statement', spec: 'original', text: lines('erDiagram', 'title A title', 'A') },
  { name: 'unicode-names', spec: 'original', text: lines('erDiagram', 'Café ||--o{ Naïve : serves', 'Café {', '  string name PK "déjà vu"', '}') },
  { name: 'comment-lines', spec: 'original', text: lines('erDiagram', '%% a comment', 'A ||--|| B : r', '%% another') },
  { name: 'comment-trailing', spec: 'original', text: lines('erDiagram', 'A ||--|| B : r %% trailing comment') },
  { name: 'semicolon-statements', spec: 'original', text: 'erDiagram\nA ||--|| B : r;\nC' },
  { name: 'error-attribute-leading-digit-type', spec: 'original', text: lines('erDiagram', 'T {', '  1int id', '}') },
  { name: 'error-unclosed-block', spec: 'original', text: lines('erDiagram', 'T {', '  int id') },
  { name: 'error-unclosed-subgraph', spec: 'original', text: lines('erDiagram', 'subgraph S', '  A') },
  { name: 'error-percent-in-quoted-name', spec: 'original', text: lines('erDiagram', '"50% off" ||--|| B : r') },

  // ------------------------------------------------------------------------------------------- the ER fixtures
  ...fixtures
];