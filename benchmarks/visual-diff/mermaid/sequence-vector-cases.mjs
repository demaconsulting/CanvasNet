// sequence-vector-cases.mjs - the original sequence texts of gen-sequence-vectors.mjs, one per grammar feature or quirk
// of mermaid's sequenceDiagram.jison and sequenceDb.ts. Each case is { name, text } (and an optional site
// configuration). The expectations are not written here: the generator records what the pinned mermaid makes of each
// text.

const lines = (...rows) => rows.join('\n');
const seq = (...rows) => lines('sequenceDiagram', ...rows.map((r) => `    ${r}`));

// The 26 message arrows (with the LINETYPE name they map to, for the reader)
const arrows = [
  ['solid-open', '->'], ['dotted-open', '-->'], ['solid', '->>'], ['dotted', '-->>'],
  ['bidirectional-solid', '<<->>'], ['bidirectional-dotted', '<<-->>'],
  ['solid-cross', '-x'], ['dotted-cross', '--x'], ['solid-point', '-)'], ['dotted-point', '--)'],
  ['solid-top', '-|\\'], ['solid-bottom', '-|/'], ['stick-top', '-\\\\'], ['stick-bottom', '-//'],
  ['solid-top-dotted', '--|\\'], ['solid-bottom-dotted', '--|/'], ['stick-top-dotted', '--\\\\'],
  ['stick-bottom-dotted', '--//'],
  ['solid-top-reverse', '/|-'], ['solid-bottom-reverse', '\\|-'], ['stick-top-reverse', '//-'],
  ['stick-bottom-reverse', '\\\\-'],
  ['solid-top-reverse-dotted', '/|--'], ['solid-bottom-reverse-dotted', '\\|--'],
  ['stick-top-reverse-dotted', '//--'], ['stick-bottom-reverse-dotted', '\\\\--']
];

const types = ['participant', 'actor', 'boundary', 'control', 'entity', 'database', 'collections', 'queue'];

export const cases = [
  // ---------------------------------------------------------------------------------------------------- headers
  { name: 'header-only', text: 'sequenceDiagram' },
  { name: 'header-leading-blank-lines', text: lines('', '', '   sequenceDiagram', '  Alpha->>Beta: hi') },
  { name: 'header-semicolons', text: 'sequenceDiagram;Alpha->>Beta: hi;Beta-->>Alpha: ok' },
  { name: 'header-trailing-spaces', text: lines('sequenceDiagram   ', '  Alpha->>Beta: hi') },
  { name: 'header-suffix-word', text: lines('sequenceDiagramX', '  Alpha->>Beta: hi') },
  { name: 'header-statement-on-same-line', text: 'sequenceDiagram Alpha->>Beta: hi' },
  { name: 'header-percent-comments', text: lines('%% before', 'sequenceDiagram', '  %% inside', '  Alpha->>Beta: hi') },
  { name: 'header-hash-comments', text: seq('# a hash comment', 'Alpha->>Beta: value # trailing comment') },
  { name: 'header-uppercase-keywords', text: lines('sequenceDiagram', '  PARTICIPANT Alpha', '  NOTE OVER Alpha: upper') },

  // ----------------------------------------------------------------------------------------------- participants
  { name: 'participant-declared-order', text: seq('participant Zed', 'participant Amy', 'Amy->>Zed: hi') },
  { name: 'participant-implicit-order', text: seq('Second->>First: hi', 'First->>Third: on') },
  { name: 'participant-declared-then-implicit', text: seq('participant Gate', 'Visitor->>Gate: knock') },
  { name: 'participant-alias', text: seq('participant G as Main gate', 'G->>G: self') },
  { name: 'participant-alias-trimmed', text: seq('participant G as    Spaced alias   ', 'G->>G: self') },
  { name: 'participant-name-with-spaces', text: seq('participant Big Server', 'Big Server->>Big Server: ping') },
  { name: 'participant-spaced-name-with-as', text: seq('participant Back Office as BO', 'BO->>BO: ping') },
  { name: 'participant-dashed-name', text: seq('participant Front-End', 'Front-End->>Back-End: call') },
  { name: 'participant-numeric-names', text: seq('1->>2: numbers', '2-->>1: back') },
  { name: 'participant-latin-names', text: seq('Café->>Naïve: hello', 'Naïve-->>Café: déjà vu') },
  { name: 'participant-redeclared-description', text: seq('participant P as First', 'participant P as Second', 'P->>P: x') },
  { name: 'participant-implicit-then-declared', text: seq('Alpha->>Beta: hi', 'participant Beta as Bee', 'participant Alpha') },
  { name: 'participant-redeclared-without-alias', text: seq('participant P as Kept', 'participant P', 'P->>P: x') },
  { name: 'participant-actor-keyword', text: seq('actor User', 'actor Admin as Administrator', 'User->>Admin: ask') },
  { name: 'participant-semicolon', text: 'sequenceDiagram\n  participant Alpha;participant Beta;Alpha->>Beta: hi' },
  { name: 'participant-hash-comment', text: seq('participant Alpha # the first', 'Alpha->>Alpha: x') },
  { name: 'participant-angle-bracket', text: seq('participant A<B', 'Alpha->>Beta: hi') },
  { name: 'participant-alias-empty', text: seq('participant Alpha as', 'Alpha->>Alpha: x') },
  { name: 'participant-alias-wrap', text: seq('participant Alpha as wrap:A long alias that wraps', 'Alpha->>Alpha: x') },
  { name: 'participant-alias-nowrap', text: seq('participant Alpha as nowrap:Kept on one line', 'Alpha->>Alpha: x') },
  { name: 'participant-alias-br', text: seq('participant Alpha as Two<br/>lines', 'Alpha->>Alpha: x') },
  { name: 'participant-error-missing-name', text: seq('participant', 'Alpha->>Beta: hi') },

  // ------------------------------------------------------------------------------------------- participant types
  ...types.map((t) => ({ name: `type-${t}`, text: seq(`participant Item@{ "type": "${t}" }`, 'Item->>Item: x') })),
  { name: 'type-unquoted-yaml', text: seq('participant Item@{ type: database }', 'Item->>Item: x') },
  { name: 'type-on-actor-keyword', text: seq('actor Item@{ "type": "queue" }', 'Item->>Item: x') },
  { name: 'type-alias-key', text: seq('participant Item@{ "type": "entity", "alias": "Stored item" }', 'Item->>Item: x') },
  { name: 'type-alias-key-and-as', text: seq('participant Item@{ "alias": "Ignored" } as Shown', 'Item->>Item: x') },
  { name: 'type-alias-key-only', text: seq('participant Item@{ "alias": "From metadata" }', 'Item->>Item: x') },
  { name: 'type-unknown', text: seq('participant Item@{ "type": "cloud" }', 'Item->>Item: x') },
  { name: 'type-unknown-key', text: seq('participant Item@{ "shape": "circle" }', 'Item->>Item: x') },
  { name: 'type-multiline-yaml', text: lines('sequenceDiagram', '  participant Item@{', '    type: control', '  }', '  Item->>Item: x') },
  { name: 'type-space-before-at', text: seq('participant Item @{ "type": "database" }', 'Item->>Item: x') },
  { name: 'type-error-invalid-yaml', text: seq('participant Item@{ "type": [ }', 'Item->>Item: x') },
  { name: 'type-uppercase-value', text: seq('participant Item@{ "type": "Database" }', 'Item->>Item: x') },

  // ---------------------------------------------------------------------------------------------------- arrows
  ...arrows.map(([name, arrow]) => ({ name: `arrow-${name}`, text: seq(`Left${arrow}Right: ${name}`) })),
  ...arrows.map(([name, arrow]) => ({ name: `arrow-${name}-spaced`, text: seq(`Left ${arrow} Right: ${name}`) })),
  { name: 'arrow-self', text: seq('Robot->>Robot: to itself') },
  { name: 'name-keyword-loop', text: seq('Loop->>Loop: a keyword as a name') },
  { name: 'name-keyword-prefixes', text: seq('Endpoint->>Optimizer: names that start with keywords', 'Notebook->>Boxer: more') },
  { name: 'name-keyword-participant-statement', text: seq('participant Loop', 'participant End as Finish') },
  { name: 'arrow-error-unknown', text: seq('Left=>Right: no such arrow') },
  { name: 'arrow-error-triple-head', text: seq('Left->>>Right: three') },

  // -------------------------------------------------------------------------------------------- message texts
  { name: 'text-missing', text: seq('Left->>Right') },
  { name: 'text-empty', text: seq('Left->>Right:') },
  { name: 'text-colons', text: seq('Left->>Right: a: b: c') },
  { name: 'text-semicolon', text: seq('Left->>Right: one; Right->>Left: two') },
  { name: 'text-hash', text: seq('Left->>Right: value #1 is next') },
  { name: 'text-entities', text: seq('Left->>Right: a &lt; b and #9829; and #quot;quoted#quot;') },
  { name: 'text-entity-codes', text: seq('Left->>Right: heart #9829; and quote #34;') },
  { name: 'text-html-literal', text: seq('Left->>Right: <b>bold</b> and <i>italic</i>') },
  { name: 'text-br-variants', text: seq('Left->>Right: one<br>two<br/>three<br />four<BR>five') },
  { name: 'text-wrap-prefix', text: seq('Left->>Right: wrap:A wrapped message') },
  { name: 'text-nowrap-prefix', text: seq('Left->>Right: nowrap:An unwrapped message') },
  { name: 'text-wrap-prefix-colon', text: seq('Left->>Right::wrap:Colon then wrap') },
  { name: 'text-wrap-prefix-not-first', text: seq('Left->>Right: say wrap:later') },
  { name: 'text-math', text: seq('Left->>Right: $$x^2$$ squared') },
  { name: 'text-percent', text: seq('Left->>Right: 50% done') },
  { name: 'text-double-percent', text: seq('Left->>Right: before %% after') },

  // ------------------------------------------------------------------------------------------------ activations
  { name: 'activate-shorthand', text: seq('Left->>+Right: start', 'Right-->>-Left: stop') },
  { name: 'activate-shorthand-nested', text: seq('Left->>+Right: one', 'Left->>+Right: two', 'Right-->>-Left: a', 'Right-->>-Left: b') },
  { name: 'activate-shorthand-self', text: seq('Left->>+Left: in', 'Left-->>-Left: out') },
  { name: 'activate-statements', text: seq('Left->>Right: go', 'activate Right', 'Right->>Left: busy', 'deactivate Right') },
  { name: 'activate-unknown-participant', text: seq('Left->>Right: go', 'activate Other', 'deactivate Other') },
  { name: 'activate-error-inactive-statement', text: seq('Left->>Right: go', 'deactivate Right') },
  { name: 'activate-error-inactive-shorthand', text: seq('Left->>-Right: go') },
  { name: 'activate-error-plus-minus', text: seq('Left->>+-Right: go') },

  // ------------------------------------------------------------------------------------------ central connections
  { name: 'central-to', text: seq('Left->>()Right: to the center') },
  { name: 'central-from', text: seq('Left()->>Right: from the center') },
  { name: 'central-dual', text: seq('Left()->>()Right: both') },
  { name: 'central-dotted', text: seq('Left()-->>Right: dotted') },
  { name: 'central-spaced', text: seq('Left () ->> () Right: spaced') },
  { name: 'central-error-with-plus', text: seq('Left->>+()Right: both modifiers') },

  // ------------------------------------------------------------------------------------------------------ notes
  { name: 'note-left-of', text: seq('participant Alpha', 'Note left of Alpha: left') },
  { name: 'note-right-of', text: seq('participant Alpha', 'Note right of Alpha: right') },
  { name: 'note-over-one', text: seq('participant Alpha', 'Note over Alpha: over') },
  { name: 'note-over-two', text: seq('participant Alpha', 'participant Beta', 'Note over Alpha,Beta: both') },
  { name: 'note-over-two-spaced', text: seq('Note over Alpha , Beta: spaced comma') },
  { name: 'note-over-reversed', text: seq('participant Alpha', 'participant Beta', 'Note over Beta,Alpha: reversed') },
  { name: 'note-implicit-participant', text: seq('Note right of Ghost: creates Ghost') },
  { name: 'note-lowercase-extra-spaces', text: seq('note   left   of   Alpha: spaces') },
  { name: 'note-wrap-prefix', text: seq('Note over Alpha: wrap:A note that wraps') },
  { name: 'note-error-over-three', text: seq('Note over Alpha,Beta,Gamma: three') },
  { name: 'note-error-missing-placement', text: seq('Note Alpha: no placement') },

  // ----------------------------------------------------------------------------------------------------- blocks
  { name: 'block-loop', text: seq('loop Every minute', '  Alpha->>Beta: tick', 'end') },
  { name: 'block-loop-no-title', text: seq('loop', '  Alpha->>Beta: tick', 'end') },
  { name: 'block-loop-empty', text: seq('loop Nothing', 'end') },
  { name: 'block-opt', text: seq('opt Maybe', '  Alpha->>Beta: perhaps', 'end') },
  { name: 'block-break', text: seq('break On failure', '  Alpha->>Beta: stop', 'end') },
  { name: 'block-rect-rgb', text: seq('rect rgb(10, 20, 30)', '  Alpha->>Beta: in', 'end') },
  { name: 'block-rect-rgba', text: seq('rect rgba(10, 20, 30, 0.5)', '  Alpha->>Beta: in', 'end') },
  { name: 'block-rect-named', text: seq('rect LightGreen', '  Alpha->>Beta: in', 'end') },
  { name: 'block-rect-no-color', text: seq('rect', '  Alpha->>Beta: in', 'end') },
  { name: 'block-rect-hex-comment', text: seq('rect #ff0000', '  Alpha->>Beta: in', 'end') },
  { name: 'block-alt-else', text: seq('alt Yes', '  Alpha->>Beta: a', 'else No', '  Alpha->>Beta: b', 'else', '  Alpha->>Beta: c', 'end') },
  { name: 'block-par-and', text: seq('par First', '  Alpha->>Beta: a', 'and Second', '  Alpha->>Gamma: b', 'and', '  Alpha->>Delta: c', 'end') },
  { name: 'block-par-over', text: seq('par_over Overlap', '  Alpha->>Beta: a', 'and Other', '  Gamma->>Delta: b', 'end') },
  { name: 'block-critical-option', text: seq('critical Must', '  Alpha->>Beta: a', 'option Fallback', '  Alpha->>Gamma: b', 'end') },
  { name: 'block-critical-alone', text: seq('critical Only', '  Alpha->>Beta: a', 'end') },
  { name: 'block-nested-deep', text: seq('loop L1', 'opt L2', 'alt L3', 'par L4', 'critical L5', 'break L6', 'rect rgb(1,2,3)',
    'Alpha->>Beta: deep', 'end', 'end', 'end', 'end', 'end', 'end', 'end') },
  { name: 'block-title-wrap-prefix', text: seq('loop wrap:A wrapped title', '  Alpha->>Beta: x', 'end') },
  { name: 'block-title-colon', text: seq('loop :Colon title', '  Alpha->>Beta: x', 'end') },
  { name: 'block-title-semicolon', text: seq('loop Title; Alpha->>Beta: x', 'end') },
  { name: 'block-error-missing-end', text: seq('loop Forever', '  Alpha->>Beta: x') },
  { name: 'block-error-stray-end', text: seq('Alpha->>Beta: x', 'end') },
  { name: 'block-error-else-outside-alt', text: seq('loop L', 'else E', 'end') },
  { name: 'block-error-option-outside-critical', text: seq('opt O', 'option P', 'end') },
  { name: 'block-error-and-outside-par', text: seq('alt A', 'and B', 'end') },

  // ------------------------------------------------------------------------------------------------------ boxes
  { name: 'box-color-title', text: seq('box Aqua Team one', 'participant Alpha', 'end', 'Alpha->>Alpha: x') },
  { name: 'box-title-only', text: seq('box Team two', 'participant Alpha', 'end', 'Alpha->>Alpha: x') },
  { name: 'box-color-only', text: seq('box Aqua', 'participant Alpha', 'end', 'Alpha->>Alpha: x') },
  { name: 'box-rgb', text: seq('box rgb(200, 10, 10) Red team', 'participant Alpha', 'end', 'Alpha->>Alpha: x') },
  { name: 'box-rgba-spaced', text: seq('box rgba (0, 0, 255, 0.2) Blue team', 'participant Alpha', 'end') },
  { name: 'box-hsl', text: seq('box hsl(120, 50%, 50%) Green team', 'participant Alpha', 'end') },
  { name: 'box-transparent', text: seq('box transparent Clear', 'participant Alpha', 'end') },
  { name: 'box-lowercase-color', text: seq('box aqua lower', 'participant Alpha', 'end') },
  { name: 'box-hex-comment', text: seq('box #00ff00 Hex', 'participant Alpha', 'end') },
  { name: 'box-wrap-title', text: seq('box Aqua wrap:A wrapped box title', 'participant Alpha', 'end') },
  { name: 'box-empty', text: seq('box Aqua Nobody', 'end', 'Alpha->>Beta: x') },
  { name: 'box-actor-and-outside', text: seq('box Pink Inside', 'actor Alpha', 'participant Beta', 'end', 'participant Gamma', 'Alpha->>Gamma: x') },
  { name: 'box-two-boxes', text: seq('box Aqua One', 'participant Alpha', 'end', 'box Pink Two', 'participant Beta', 'end') },
  { name: 'box-redeclare-same-box', text: seq('box Aqua One', 'participant Alpha', 'participant Alpha as Again', 'end') },
  { name: 'box-redeclare-outside', text: seq('box Aqua One', 'participant Alpha', 'end', 'participant Alpha as Again') },
  { name: 'box-error-two-boxes', text: seq('box Aqua One', 'participant Alpha', 'end', 'box Pink Two', 'participant Alpha', 'end') },
  { name: 'box-error-message-inside', text: seq('box Aqua One', 'participant Alpha', 'Alpha->>Alpha: x', 'end') },
  { name: 'box-error-nested', text: seq('box Aqua One', 'box Pink Two', 'participant Alpha', 'end', 'end') },

  // ------------------------------------------------------------------------------------------------- autonumber
  { name: 'autonumber-plain', text: seq('autonumber', 'Alpha->>Beta: a', 'Beta->>Alpha: b') },
  { name: 'autonumber-start', text: seq('autonumber 10', 'Alpha->>Beta: a', 'Beta->>Alpha: b') },
  { name: 'autonumber-start-step', text: seq('autonumber 10 5', 'Alpha->>Beta: a', 'Beta->>Alpha: b') },
  { name: 'autonumber-decimals', text: seq('autonumber 1.5 0.25', 'Alpha->>Beta: a', 'Beta->>Alpha: b') },
  { name: 'autonumber-leading-dot', text: seq('autonumber .5 .25', 'Alpha->>Beta: a') },
  { name: 'autonumber-off', text: seq('autonumber', 'Alpha->>Beta: a', 'autonumber off', 'Beta->>Alpha: b') },
  { name: 'autonumber-restart', text: seq('autonumber', 'Alpha->>Beta: a', 'autonumber 100', 'Beta->>Alpha: b') },
  { name: 'autonumber-three-decimals', text: seq('autonumber 1.234', 'Alpha->>Beta: a') },
  { name: 'autonumber-extra-word', text: seq('autonumber 10 5 7', 'Alpha->>Beta: a') },

  // --------------------------------------------------------------------------------------------- create, destroy
  { name: 'create-participant', text: seq('Alpha->>Beta: hi', 'create participant Gamma', 'Beta->>Gamma: make') },
  { name: 'create-actor-alias', text: seq('Alpha->>Beta: hi', 'create actor Gamma as Third', 'Beta->>Gamma: make') },
  { name: 'create-with-type', text: seq('Alpha->>Beta: hi', 'create participant Store@{ "type": "database" }', 'Beta->>Store: make') },
  { name: 'destroy-by-to', text: seq('Alpha->>Beta: hi', 'destroy Beta', 'Alpha-xBeta: bye') },
  { name: 'destroy-by-from', text: seq('Alpha->>Beta: hi', 'destroy Beta', 'Beta-xAlpha: bye') },
  { name: 'create-and-destroy', text: seq('Alpha->>Beta: hi', 'create participant Temp', 'Beta->>Temp: new', 'destroy Temp',
    'Temp-->>Beta: gone') },
  { name: 'create-error-no-message', text: seq('Alpha->>Beta: hi', 'create participant Gamma', 'Alpha->>Beta: not to Gamma') },
  { name: 'create-error-existing', text: seq('Alpha->>Beta: hi', 'create participant Beta', 'Alpha->>Beta: again') },
  { name: 'destroy-error-no-message', text: seq('Alpha->>Beta: hi', 'Alpha->>Gamma: hi', 'destroy Gamma', 'Alpha->>Beta: unrelated') },
  { name: 'create-at-end', text: seq('Alpha->>Beta: hi', 'create participant Gamma') },

  // ----------------------------------------------------------------------------------- links, properties, details
  { name: 'link-single', text: seq('participant Alpha', 'link Alpha: Docs @ https://example.com/docs', 'Alpha->>Alpha: x') },
  { name: 'link-two', text: seq('participant Alpha', 'link Alpha: One @ https://example.com/1', 'link Alpha: Two @ https://example.com/2') },
  { name: 'link-query-string', text: seq('participant Alpha', 'link Alpha: Search @ https://example.com/?q=a&b=c') },
  { name: 'link-no-at', text: seq('participant Alpha', 'link Alpha: no at sign xy') },
  { name: 'links-json', text: seq('participant Alpha', 'links Alpha: {"Home": "https://example.com/", "Help": "https://example.com/help"}') },
  { name: 'links-invalid-json', text: seq('participant Alpha', 'links Alpha: {not json}') },
  { name: 'links-unknown-participant', text: seq('participant Alpha', 'links Ghost: {"Home": "https://example.com/"}') },
  { name: 'properties-class', text: seq('participant Alpha', 'properties Alpha: {"class": "internal"}') },
  { name: 'properties-icon', text: seq('participant Alpha', 'properties Alpha: {"icon": "@computer"}') },
  { name: 'properties-invalid-json', text: seq('participant Alpha', 'properties Alpha: {class: internal}') },
  { name: 'details-ignored', text: seq('participant Alpha', 'details Alpha: some-element-id') },

  // ------------------------------------------------------------------------------------------ title, accessibility
  { name: 'title-statement', text: seq('title A diagram title', 'Alpha->>Beta: x') },
  { name: 'title-legacy-colon', text: seq('title: A legacy title', 'Alpha->>Beta: x') },
  { name: 'title-hash-cut', text: seq('title Before # after', 'Alpha->>Beta: x') },
  { name: 'title-entities', text: seq('title Fish &amp; chips', 'Alpha->>Beta: x') },
  { name: 'acc-title-descr', text: seq('accTitle: Accessible title', 'accDescr: Accessible description', 'Alpha->>Beta: x') },
  { name: 'acc-descr-multiline', text: seq('accDescr {', '  First line', '  Second line', '}', 'Alpha->>Beta: x') },

  // ----------------------------------------------------------------------------- wrap configuration (C2's rule)
  { name: 'wrap-default', text: seq('participant Alpha', 'Alpha->>Beta: x', 'Note over Alpha: n', 'box Aqua B', 'participant Gamma', 'end') },
  { name: 'wrap-site-top-true', site: { wrap: true }, text: seq('participant Alpha', 'Alpha->>Beta: x', 'Note over Alpha: n') },
  { name: 'wrap-site-sequence-true', site: { sequence: { wrap: true } }, text: seq('participant Alpha', 'Alpha->>Beta: x', 'Note over Alpha: n') },
  { name: 'wrap-site-top-false-sequence-true', site: { wrap: false, sequence: { wrap: true } }, text: seq('Alpha->>Beta: x') },
  { name: 'wrap-site-top-true-sequence-false', site: { wrap: true, sequence: { wrap: false } }, text: seq('Alpha->>Beta: x') },
  { name: 'wrap-frontmatter-top-true', text: lines('---', 'config:', '  wrap: true', '---', 'sequenceDiagram', '  Alpha->>Beta: x') },
  { name: 'wrap-frontmatter-sequence-true', text: lines('---', 'config:', '  sequence:', '    wrap: true', '---', 'sequenceDiagram', '  Alpha->>Beta: x') },
  { name: 'wrap-frontmatter-top-false-sequence-true', text: lines('---', 'config:', '  wrap: false', '  sequence:', '    wrap: true', '---',
    'sequenceDiagram', '  Alpha->>Beta: x') },
  { name: 'wrap-directive-top-true', text: lines('%%{init: {"wrap": true}}%%', 'sequenceDiagram', '  Alpha->>Beta: x') },
  { name: 'wrap-site-true-nowrap-prefix', site: { wrap: true }, text: seq('Alpha->>Beta: nowrap:kept', 'Note over Alpha: nowrap:kept') },
  { name: 'wrap-site-false-sequence-true-nowrap', site: { wrap: false, sequence: { wrap: true } }, text: seq('Alpha->>Beta: nowrap:kept') },

  // ----------------------------------------------------------------------------------------------- other errors
  { name: 'error-lone-actor', text: seq('Alpha') },
  { name: 'error-unknown-keyword', text: seq('jump Alpha') },
  { name: 'error-missing-target', text: seq('Alpha->>: nobody') },
  { name: 'error-double-arrow', text: seq('Alpha->>Beta->>Gamma: chain') }
];
