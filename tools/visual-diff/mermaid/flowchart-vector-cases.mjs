// flowchart-vector-cases.mjs - the original flowchart texts of gen-flowchart-vectors.mjs, one per grammar feature or
// quirk of mermaid's flow.jison and flowDb.ts. Each case is { name, text } (and an optional site configuration). The
// expectations are not written here: the generator records what the pinned mermaid makes of each text.

const shapeNames = [
  'rect', 'rounded', 'stadium', 'fr-rect', 'cyl', 'datastore', 'folder', 'bucket', 'console', 'browser', 'person',
  'circle', 'bang', 'cloud', 'diam', 'hex', 'lean-r', 'lean-l', 'trap-b', 'trap-t', 'dbl-circ', 'text', 'notch-rect',
  'lin-rect', 'sm-circ', 'fr-circ', 'fork', 'hourglass', 'brace', 'brace-r', 'braces', 'bolt', 'doc', 'delay', 'h-cyl',
  'lin-cyl', 'curv-trap', 'div-rect', 'tri', 'win-pane', 'f-circ', 'notch-pent', 'flip-tri', 'sl-rect', 'docs',
  'st-rect', 'bow-rect', 'cross-circ', 'tag-doc', 'tag-rect', 'flag', 'odd', 'lin-doc'
];
const shapeAliases = [
  'proc', 'process', 'rectangle', 'event', 'terminal', 'pill', 'subprocess', 'subproc', 'framed-rectangle',
  'subroutine', 'db', 'database', 'cylinder', 'data-store', 'directory', 'circ', 'decision', 'diamond', 'question',
  'hexagon', 'prepare', 'lean-right', 'in-out', 'lean-left', 'out-in', 'priority', 'trapezoid-bottom', 'trapezoid',
  'manual', 'trapezoid-top', 'inv-trapezoid', 'double-circle', 'doublecircle', 'card', 'notched-rectangle',
  'lined-rectangle', 'lined-process', 'lin-proc', 'shaded-process', 'start', 'small-circle', 'stop', 'framed-circle',
  'join', 'collate', 'comment', 'brace-l', 'com-link', 'lightning-bolt', 'document', 'half-rounded-rectangle', 'das',
  'horizontal-cylinder', 'disk', 'lined-cylinder', 'curved-trapezoid', 'display', 'div-proc', 'divided-rectangle',
  'divided-process', 'extract', 'triangle', 'internal-storage', 'window-pane', 'junction', 'filled-circle',
  'loop-limit', 'notched-pentagon', 'manual-file', 'flipped-triangle', 'manual-input', 'sloped-rectangle', 'documents',
  'st-doc', 'stacked-document', 'procs', 'processes', 'stacked-rectangle', 'stored-data', 'bow-tie-rectangle',
  'summary', 'crossed-circle', 'tagged-document', 'tagged-rectangle', 'tag-proc', 'tagged-process', 'paper-tape',
  'lined-document', 'state', 'choice', 'note', 'composite', 'icon', 'anchor'
];

const lines = (...rows) => rows.join('\n');

export const cases = [
  // ---------------------------------------------------------------------------------------------------- headers
  { name: 'header-graph-td', text: lines('graph TD', '  Start --> Stop') },
  { name: 'header-graph-tb', text: lines('graph TB', '  Start --> Stop') },
  { name: 'header-graph-bt', text: lines('graph BT', '  Start --> Stop') },
  { name: 'header-graph-lr', text: lines('graph LR', '  Start --> Stop') },
  { name: 'header-graph-rl', text: lines('graph RL', '  Start --> Stop') },
  { name: 'header-flowchart-lr', text: lines('flowchart LR', '  Start --> Stop') },
  { name: 'header-no-direction', text: lines('graph', '  Start --> Stop') },
  { name: 'header-flowchart-no-direction', text: lines('flowchart', '  Start --> Stop') },
  { name: 'header-arrow-right', text: lines('graph >', '  Start --> Stop') },
  { name: 'header-arrow-left', text: lines('graph <', '  Start --> Stop') },
  { name: 'header-arrow-up', text: lines('graph ^', '  Start --> Stop') },
  { name: 'header-arrow-down', text: lines('graph v', '  Start --> Stop') },
  { name: 'header-br', text: lines('graph BR', '  Start --> Stop') },
  { name: 'header-semicolon', text: 'graph TD;Start-->Stop;' },
  { name: 'header-trailing-spaces', text: lines('flowchart TB   ', '  Start --> Stop') },
  { name: 'header-leading-blank-lines', text: lines('', '', '   flowchart LR', '  Start --> Stop') },
  { name: 'header-only', text: 'flowchart LR' },
  { name: 'header-elk-keyword', text: lines('flowchart-elk TD', '  Start --> Stop') },
  { name: 'header-error-unknown-direction', text: lines('graph XY', '  Start --> Stop') },
  { name: 'header-error-statement-on-header-line', text: 'graph TD Start-->Stop' },
  { name: 'header-error-spaces-then-text', text: lines('graph TD  Start', '  Start --> Stop') },

  // ---------------------------------------------------------------------------------------------- vertex shapes
  {
    name: 'shapes-brackets',
    text: lines('flowchart TD', '  a[square] --> b(round)', '  c([stadium]) --> d[[subroutine]]', '  e[(cylinder)] --> f((circle))',
      '  g{diamond} --> h{{hexagon}}', '  i>odd] --> j[/lean right/]', '  k[\\lean left\\] --> l[/trapezoid\\]',
      '  m[\\inverted trapezoid/] --> n(((double circle)))')
  },
  { name: 'shapes-ellipse', text: lines('graph LR', '  A(-ellipse text-) --> B') },
  { name: 'shapes-vertex-with-props', text: lines('graph LR', '  A[|borders:lr|Bordered] --> B') },
  { name: 'vertex-redeclared-text', text: lines('graph TD', '  A[First] --> B', '  A[Second]', '  B --> A') },
  { name: 'vertex-redeclared-shape', text: lines('graph TD', '  A[Box] --> B', '  A((Round))') },
  { name: 'vertex-text-kept-on-plain-mention', text: lines('graph TD', '  A[Label]', '  A --> B', '  B --> A') },
  { name: 'vertex-ids-characters', text: lines('graph TD', '  node-1 --> node_2', '  n.3 --> n/4', '  a!b --> c?d', "  e'f --> g*h") },
  { name: 'vertex-ids-numbers', text: lines('graph TD', '  1 --> 2', '  10 --> 2a', '  3 & 4 --> 5') },
  { name: 'vertex-ids-unicode', text: lines('graph TD', '  café --> ñ', '  日本 --> Ω') },
  { name: 'vertex-ids-keywords-inside', text: lines('graph TD', '  styled --> subgraphs', '  endpoint --> graphical', '  clickable --> classy', '  defaults --> linkStyles') },
  { name: 'vertex-ids-default-and-v', text: lines('graph TD', '  default --> v', '  v --> x') },
  { name: 'vertex-ids-colon-and-amp', text: lines('graph TD', '  a:b --> c&d', '  1&2 --> e') },
  { name: 'vertex-ids-minus', text: lines('graph LR', '  -a --> b-', '  c - d') },
  { name: 'vertex-id-end-error', text: lines('graph TD', '  A --> end') },
  { name: 'vertex-statement-semicolons', text: lines('graph TD', '  A-->B;B-->C;', '  C-->A ;') },
  { name: 'vertex-trailing-spaces', text: lines('graph TD', '  A   ', '  B --> C   ', '  D') },
  { name: 'vertex-single-node', text: lines('flowchart LR', '  lonely') },

  // ---------------------------------------------------------------------------------------------- labels
  { name: 'label-quoted', text: lines('graph TD', '  A["quoted (with) [brackets] {braces}"] --> B["a | pipe"]') },
  { name: 'label-quoted-after-text', text: lines('graph TD', '  A["quoted" tail] --> B') },
  { name: 'label-markdown-node', text: lines('flowchart LR', '  A["`**bold** and *italic*`"] --> B["`line one', '  line two`"]') },
  { name: 'label-markdown-edge', text: lines('flowchart LR', '  A -- "`**strong** link`" --> B', '  B -->|"`_md_ pipe`"| C') },
  { name: 'label-backticks-without-quotes', text: lines('graph TD', '  A[`**not markdown**`] --> B') },
  { name: 'label-html-break', text: lines('graph TD', '  A[line one<br>line two] --> B[a<br/>b]', '  C[x <br /> y]') },
  { name: 'label-html-tags', text: lines('graph TD', '  A[<b>bold</b> and <i>italic</i>] --> B[<strong>s</strong><em>e</em>]') },
  { name: 'label-html-unclosed', text: lines('graph TD', '  A[<b>open bold] --> B[a<b]') },
  { name: 'label-html-script', text: lines('graph TD', '  A[safe <script>alert</script> text]') },
  { name: 'label-html-span-style', text: lines('graph TD', "  A[<span style='color:red'>red</span> text] --> B") },
  { name: 'label-html-unknown-tag', text: lines('graph TD', '  A[<foo>kept text</foo> after] --> B') },
  { name: 'label-less-than', text: lines('graph TD', '  A[a < b] --> B[c > d]', '  C[x<y]') },
  { name: 'label-ampersand', text: lines('graph TD', '  A[fish & chips] --> B[a &amp; b <br> c & d]') },
  { name: 'label-entity-code', text: lines('graph TD', '  A[#quot; quoted #quot;] --> B[#35; hash #9829;]') },
  { name: 'label-unicode', text: lines('graph TD', '  A[naïve café] --> B[日本語のラベル]') },
  { name: 'label-quotes-only', text: lines('graph TD', '  A[""] --> B["  spaced  "]') },
  { name: 'label-colon-and-semicolon', text: lines('graph TD', '  A[time: 10:30] --> B[a;b]') },
  { name: 'label-keywords', text: lines('graph TD', '  A[graph subgraph end style class] --> B(click linkStyle classDef)') },
  { name: 'label-trimmed', text: lines('graph TD', '  A[   padded   ] --> B(  round  )') },
  { name: 'label-error-unclosed-bracket', text: lines('graph TD', '  A[text --> B') },
  { name: 'label-error-nested-bracket', text: lines('graph TD', '  A[a [b] c] --> B') },
  { name: 'label-error-mismatched-close', text: lines('graph TD', '  A[[a] --> B') },
  { name: 'label-error-empty', text: lines('graph TD', '  A[] --> B') },

  // ---------------------------------------------------------------------------------------------- links
  {
    name: 'links-strokes-and-heads',
    text: lines('graph LR', '  a1 --> b1', '  a2 --- b2', '  a3 -.-> b3', '  a4 -.- b4', '  a5 ==> b5', '  a6 === b6',
      '  a7 ~~~ b7', '  a8 --x b8', '  a9 --o b9', '  a10 ==x b10', '  a11 -.-o b11')
  },
  {
    name: 'links-double-heads',
    text: lines('graph LR', '  a1 <--> b1', '  a2 x--x b2', '  a3 o--o b3', '  a4 <-.-> b4', '  a5 <==> b5', '  a6 x-.-x b6',
      '  a7 o==o b7')
  },
  {
    name: 'links-lengths',
    text: lines('graph TD', '  a1 ---> b1', '  a2 ----> b2', '  a3 -..-> b3', '  a4 -...-> b4', '  a5 ===> b5', '  a6 ====> b6',
      '  a7 ~~~~ b7', '  a8 ------ b8')
  },
  { name: 'links-length-capped', text: lines('graph TD', '  a --------------------> b', '  c -............-> d') },
  {
    name: 'links-text-forms',
    text: lines('graph LR', '  a1 -- text --> b1', '  a2 -->|text| b2', '  a3 -. dotted .-> b3', '  a4 == thick ==> b4',
      '  a5 ---|open text| b5', '  a6 -- open --- b6', '  a7 -.->|pipe| b7', '  a8 ==>|pipe| b8')
  },
  { name: 'links-text-quoted', text: lines('graph LR', '  a1 -- "quoted -->" --> b1', '  a2 -->|"pipe | quoted"| b2') },
  { name: 'links-text-with-letters-x-and-o', text: lines('graph LR', '  a -- box of xo --> b', '  c -- to --> d') },
  { name: 'links-text-unicode', text: lines('graph LR', '  a -- café au lait --> b', '  c -->|日本| d') },
  { name: 'links-text-double-heads', text: lines('graph LR', '  a <-- both --> b', '  c x-- cross --x d', '  e o-- circle --o f') },
  { name: 'links-text-spacing', text: lines('graph LR', '  a-->|t|b', '  c -->|t| d', '  e--text-->f') },
  { name: 'links-invalid-mixed-strokes', text: lines('graph LR', '  a -- text ~~~ b') },
  { name: 'links-invalid-mixed-heads', text: lines('graph LR', '  a x-- text --> b') },
  { name: 'links-chain', text: lines('graph LR', '  a --> b --> c --> d', '  d -- back --> a') },
  { name: 'links-chain-mixed', text: lines('graph LR', '  a --> b -.-> c ==> d ~~~ e') },
  { name: 'links-repeated-pair', text: lines('graph LR', '  a --> b', '  a --> b', '  a --> b', '  b --> a') },
  { name: 'links-self-loop', text: lines('graph LR', '  a --> a', '  a -- again --> a') },
  { name: 'links-ampersand-groups', text: lines('graph TD', '  a & b --> c & d', '  c & d --> e') },
  { name: 'links-ampersand-shapes', text: lines('graph TD', '  a[A] & b(B) --> c{C} & d((D))') },
  { name: 'links-ampersand-duplicate', text: lines('graph TD', '  a & a --> b') },
  { name: 'links-ids', text: lines('flowchart LR', '  a e1@--> b', '  b e2@-.-> c', '  c e3@-- text --> d') },
  { name: 'links-ids-duplicate', text: lines('flowchart LR', '  a e1@--> b', '  b e1@--> c') },
  { name: 'links-ids-groups', text: lines('flowchart LR', '  a & b e1@--> c & d') },
  { name: 'links-ids-pipe-text', text: lines('flowchart LR', '  a e1@-->|label| b') },
  { name: 'links-error-missing-end', text: lines('graph LR', '  a -- text b') },
  { name: 'links-error-mismatched-text-end', text: lines('graph LR', '  a-- text ==>b') },
  { name: 'links-error-two-spaces-after-pipe', text: lines('graph LR', '  a -->|t|  b') },
  { name: 'links-error-dangling', text: lines('graph LR', '  a -->') },
  { name: 'links-error-text-and-pipe', text: lines('graph LR', '  a -- t --> |u| b') },

  // ---------------------------------------------------------------------------------------------- subgraphs
  { name: 'subgraph-id', text: lines('flowchart TB', '  subgraph one', '    a1 --> a2', '  end') },
  { name: 'subgraph-id-and-title', text: lines('flowchart TB', '  subgraph one [First group]', '    a1 --> a2', '  end') },
  { name: 'subgraph-id-and-title-no-space', text: lines('flowchart TB', '  subgraph one[First group]', '    a1 --> a2', '  end') },
  { name: 'subgraph-title-with-spaces', text: lines('flowchart TB', '  subgraph A title with spaces', '    a1', '  end') },
  { name: 'subgraph-quoted-title', text: lines('flowchart TB', '  subgraph "Quoted title"', '    a1', '  end') },
  { name: 'subgraph-quoted-id-title', text: lines('flowchart TB', '  subgraph q ["Quoted [title]"]', '    a1', '  end') },
  { name: 'subgraph-markdown-title', text: lines('flowchart TB', '  subgraph m ["`**Bold** group`"]', '    a1', '  end') },
  { name: 'subgraph-markdown-id', text: lines('flowchart TB', '  subgraph "`md id`"', '    a1', '  end') },
  {
    name: 'subgraph-nested',
    text: lines('flowchart TB', '  subgraph outer', '    o1', '    subgraph inner', '      i1 --> i2', '    end', '    o1 --> i1', '  end')
  },
  {
    name: 'subgraph-three-levels',
    text: lines('flowchart LR', '  subgraph l1', '    subgraph l2', '      subgraph l3', '        deep', '      end', '    end', '  end', '  deep --> top')
  },
  { name: 'subgraph-direction', text: lines('flowchart TB', '  subgraph s', '    direction LR', '    a --> b', '  end') },
  { name: 'subgraph-direction-last-wins', text: lines('flowchart TB', '  subgraph s', '    direction LR', '    a', '    direction BT', '  end') },
  { name: 'subgraph-direction-td', text: lines('flowchart LR', '  subgraph s', '    direction TD', '    a', '  end') },
  {
    name: 'subgraph-inherit-direction',
    text: lines('---', 'config:', '  flowchart:', '    inheritDir: true', '---', 'flowchart LR', '  subgraph s', '    a', '  end', '  subgraph t', '    direction TB', '    b', '  end')
  },
  { name: 'subgraph-edges-to-subgraphs', text: lines('flowchart TB', '  subgraph one', '    a', '  end', '  subgraph two', '    b', '  end', '  one --> two', '  a --> two') },
  { name: 'subgraph-member-in-two', text: lines('flowchart TB', '  subgraph one', '    a --> b', '  end', '  subgraph two', '    b --> c', '  end') },
  { name: 'subgraph-member-order', text: lines('flowchart TB', '  subgraph s', '    a --> b --> c', '    d & e', '    a', '  end') },
  { name: 'subgraph-empty', text: lines('flowchart TB', '  subgraph empty', '  end', '  a --> b') },
  { name: 'subgraph-end-same-line-statement', text: lines('flowchart TB', '  subgraph s', '    a', '  end', '  b --> a') },
  { name: 'subgraph-acc-title-inside', text: lines('flowchart TB', '  subgraph s', '    accTitle: inside', '    a', '  end') },
  { name: 'subgraph-semicolon-line', text: lines('flowchart TB', '  subgraph s', '    a', '    ;', '  end') },
  { name: 'subgraph-styled-by-id', text: lines('flowchart TB', '  subgraph s', '    a', '  end', '  style s fill:#f9f', '  class s highlight', '  classDef highlight stroke:#f00') },
  { name: 'subgraph-referenced-as-node', text: lines('flowchart TB', '  subgraph s', '    a', '  end', '  s --> b', '  s[Not a label]') },
  { name: 'subgraph-duplicate-id', text: lines('flowchart TB', '  subgraph s', '    a', '  end', '  subgraph s', '    b', '  end') },
  { name: 'subgraph-keyword-title', text: lines('flowchart TB', '  subgraph graph style', '    a', '  end') },
  {
    name: 'subgraph-collapsed',
    text: lines('flowchart TB', '  subgraph sub1 [Collapsed]', '    a --> b', '    subgraph sub2', '      c', '    end', '  end', '  sub1@{ view: collapsed }', '  x --> a', '  b --> y', '  a --> c')
  },
  { name: 'subgraph-collapsed-before-declaration', text: lines('flowchart TB', '  sub1@{ view: collapsed }', '  subgraph sub1', '    a', '  end') },
  { name: 'subgraph-error-anonymous', text: lines('flowchart LR', '  subgraph', '  a', '  end') },
  { name: 'subgraph-error-unclosed', text: lines('flowchart LR', '  subgraph s', '  a --> b') },
  { name: 'subgraph-error-end-without-subgraph', text: lines('flowchart LR', '  a --> b', '  end') },
  { name: 'subgraph-error-bad-title-token', text: lines('flowchart LR', '  subgraph a,b', '  x', '  end') },

  // ---------------------------------------------------------------------------------------------- styles and classes
  { name: 'style-statement', text: lines('graph LR', '  A --> B', '  style A fill:#f9f,stroke:#333,stroke-width:4px', '  style B fill:#bbf, stroke-dasharray: 5 5') },
  { name: 'style-statement-creates-node', text: lines('graph LR', '  style Ghost fill:#ccc', '  A --> Ghost') },
  { name: 'style-statement-twice', text: lines('graph LR', '  A', '  style A fill:red', '  style A stroke:blue') },
  { name: 'style-error-two-spaces', text: lines('graph LR', '  A', '  style  A fill:red') },
  { name: 'classdef-and-class', text: lines('graph LR', '  A --> B --> C', '  classDef green fill:#9f6,stroke:#333,stroke-width:2px', '  class A,C green') },
  { name: 'classdef-color-text-styles', text: lines('graph LR', '  A', '  classDef c fill:#f00,color:#fff,stroke:#000', '  class A c') },
  { name: 'classdef-escaped-comma', text: lines('graph LR', '  A', '  classDef c font-family:a\\,b,fill:#eee', '  class A c') },
  { name: 'classdef-several-names', text: lines('graph LR', '  A --> B', '  classDef one,two fill:#ff0', '  class A one', '  class B two') },
  { name: 'classdef-default', text: lines('graph LR', '  A --> B', '  classDef default fill:#eef,stroke:#99c') },
  { name: 'classdef-extended', text: lines('graph LR', '  A', '  classDef c fill:#f00', '  classDef c stroke:#0f0', '  class A c') },
  { name: 'class-shorthand', text: lines('graph LR', '  A:::hot --> B:::cold', '  C[Label]:::hot & D:::cold', '  classDef hot fill:#f66', '  classDef cold fill:#66f') },
  { name: 'class-shorthand-twice', text: lines('graph LR', '  A:::one', '  A:::two --> B') },
  { name: 'class-on-edge', text: lines('flowchart LR', '  A e1@--> B', '  classDef thick stroke-width:4px', '  class e1 thick') },
  { name: 'class-unknown-target', text: lines('graph LR', '  A', '  class Nobody hot') },
  { name: 'class-before-node', text: lines('graph LR', '  class A hot', '  A --> B') },
  { name: 'link-style-index', text: lines('graph LR', '  A --> B --> C', '  linkStyle 1 stroke:#f00,stroke-width:2px') },
  { name: 'link-style-list', text: lines('graph LR', '  A --> B --> C --> D', '  linkStyle 0,2 stroke:blue') },
  { name: 'link-style-with-fill', text: lines('graph LR', '  A --> B', '  linkStyle 0 fill:none,stroke:red') },
  { name: 'link-style-default', text: lines('graph LR', '  A --> B --> C', '  linkStyle default stroke:#999') },
  { name: 'link-style-interpolate', text: lines('graph LR', '  A --> B --> C', '  linkStyle 0 interpolate linear') },
  { name: 'link-style-interpolate-default', text: lines('graph LR', '  A --> B', '  linkStyle default interpolate stepBefore', '  B --> C') },
  { name: 'link-style-interpolate-and-style', text: lines('graph LR', '  A --> B --> C', '  linkStyle 0,1 interpolate basis stroke:green') },
  { name: 'link-style-default-interpolate-and-style', text: lines('graph LR', '  A --> B', '  linkStyle default interpolate cardinal stroke:#333') },
  { name: 'link-style-twice', text: lines('graph LR', '  A --> B', '  linkStyle 0 stroke:red', '  linkStyle 0 stroke-width:3px') },
  { name: 'link-style-error-out-of-range', text: lines('graph LR', '  A --> B', '  linkStyle 3 stroke:red') },
  { name: 'link-style-error-leading-zero', text: lines('graph LR', '  A --> B', '  linkStyle 00 stroke:red') },
  { name: 'link-style-error-missing-style', text: lines('graph LR', '  A --> B', '  linkStyle 0') },

  // ---------------------------------------------------------------------------------------------- click
  { name: 'click-callback', text: lines('graph LR', '  A --> B', '  click A callback') },
  { name: 'click-callback-tooltip', text: lines('graph LR', '  A --> B', '  click A callback "Tooltip text"') },
  { name: 'click-call', text: lines('graph LR', '  A --> B', '  click A call doIt()', '  click B call doIt("arg", 2) "tip"') },
  { name: 'click-href', text: lines('graph LR', '  A --> B', '  click A href "https://example.com"', '  click B href "https://example.com" "Tip" _blank') },
  { name: 'click-url', text: lines('graph LR', '  A --> B', '  click A "https://example.com" _self', '  click B "https://example.com" "Tip"') },
  { name: 'click-several-ids', text: lines('graph LR', '  A --> B', '  click A,B callback') },
  { name: 'click-unknown-node', text: lines('graph LR', '  A --> B', '  click C callback') },
  { name: 'click-error-missing-action', text: lines('graph LR', '  A --> B', '  click A') },

  // ---------------------------------------------------------------------------------------------- shape data
  { name: 'shape-data-documented-shapes', text: lines('flowchart TD', ...shapeNames.map((s, i) => `  n${i}@{ shape: ${s} }`)) },
  { name: 'shape-data-aliases', text: lines('flowchart TD', ...shapeAliases.map((s, i) => `  n${i}@{ shape: ${s} }`)) },
  { name: 'shape-data-label', text: lines('flowchart TD', '  A@{ shape: rounded, label: "Rounded label" }', '  A --> B@{ shape: diam, label: Decide }') },
  { name: 'shape-data-label-type', text: lines('flowchart TD', '  A@{ label: "**md**", labelType: markdown }', '  B@{ label: plain, labelType: string }', '  C@{ label: x, labelType: other }') },
  { name: 'shape-data-label-over-bracket', text: lines('flowchart TD', '  A[Bracket]@{ label: "From data" }') },
  { name: 'shape-data-multiline', text: lines('flowchart TD', '  A@{', '    shape: hex', '    label: "Hexagon"', '  }', '  A --> B') },
  { name: 'shape-data-quoted-newline', text: lines('flowchart TD', '  A@{ shape: rect, label: "two', '     lines" }') },
  { name: 'shape-data-trailing-space', text: lines('flowchart TD', '  A@{ shape: circle }   ', '  A --> B') },
  { name: 'shape-data-in-chain', text: lines('flowchart TD', '  A@{ shape: circle } & B@{ shape: hex } --> C@{ shape: cyl }') },
  { name: 'shape-data-after-link', text: lines('flowchart TD', '  A --> B@{ shape: doc }', '  B --> C') },
  { name: 'shape-data-two-blocks', text: lines('flowchart TD', '  A@{ shape: rect }@{ label: x }') },
  { name: 'shape-data-icon', text: lines('flowchart TD', '  A@{ icon: "fa:user", form: circle, pos: b }', '  B@{ icon: "fa:car", label: "Car", form: square }', '  C@{ icon: "fa:bell", form: rounded, h: 60 }') },
  { name: 'shape-data-image', text: lines('flowchart TD', '  A@{ img: "part.png", w: 120, h: 80, pos: t, constraint: on }', '  B@{ img: "logo.svg", label: "Logo", w: "64" }') },
  { name: 'shape-data-image-bad-size', text: lines('flowchart TD', '  A@{ img: "x.png", w: wide, h: "0x20" }') },
  { name: 'shape-data-edge-animate', text: lines('flowchart LR', '  A e1@--> B', '  e1@{ animate: true }') },
  { name: 'shape-data-edge-animation-curve', text: lines('flowchart LR', '  A e1@--> B --> C', '  e1@{ animation: fast, curve: linear }') },
  { name: 'shape-data-edge-before-link', text: lines('flowchart LR', '  e1@{ animate: true }', '  A e1@--> B') },
  { name: 'shape-data-empty', text: lines('flowchart LR', '  A@{} --> B') },
  { name: 'shape-data-error-unknown-shape', text: lines('flowchart LR', '  A@{ shape: rectangles }') },
  { name: 'shape-data-error-upper-case', text: lines('flowchart LR', '  A@{ shape: Circle }') },
  { name: 'shape-data-error-underscore', text: lines('flowchart LR', '  A@{ shape: lean_right }') },
  { name: 'shape-data-error-internal-name', text: lines('flowchart LR', '  A@{ shape: squareRect }') },
  { name: 'shape-data-error-yaml', text: lines('flowchart LR', '  A@{ shape: [rect }') },
  { name: 'shape-data-error-space-then-text', text: lines('flowchart LR', '  A@{ shape: rect } ;') },

  // ---------------------------------------------------------------------------------------------- accessibility
  { name: 'acc-title-and-description', text: lines('flowchart LR', '  accTitle: The title', '  accDescr: A short description', '  A --> B') },
  { name: 'acc-description-multiline', text: lines('flowchart LR', '  accDescr {', '    A longer', '      description', '  }', '  A --> B') },
  { name: 'acc-title-spaces', text: lines('flowchart LR', '  accTitle   :   spaced title  ', '  A') },
  { name: 'acc-title-html', text: lines('flowchart LR', '  accTitle: a <b>bold</b> title', '  A') },

  // ---------------------------------------------------------------------------------------------- comments, directives and frontmatter
  { name: 'comments', text: lines('flowchart LR', '  %% a comment', '  A --> B %% not a comment', '  %% another') },
  { name: 'frontmatter-title', text: lines('---', 'title: My chart', '---', 'flowchart LR', '  A --> B') },
  { name: 'directive-init', text: lines('%%{init: {"flowchart": {"curve": "linear", "padding": 20}}}%%', 'flowchart LR', '  A --> B') },
  { name: 'site-config-look', site: { look: 'handDrawn', flowchart: { curve: 'step' } }, text: lines('flowchart LR', '  A --> B') },

  // ---------------------------------------------------------------------------------------------- quirks
  { name: 'quirk-direction-anywhere-on-line', text: lines('flowchart TB', '  A --> B direction LR', '  C --> D') },
  { name: 'quirk-direction-in-label', text: lines('flowchart TB', '  A[go direction TB] --> B') },
  { name: 'quirk-at-sign-in-id', text: lines('flowchart LR', '  user@host --> B') },
  { name: 'quirk-email-in-label', text: lines('flowchart LR', '  A[mail me@example.com] --> B') },
  { name: 'quirk-keyword-subgraph-title', text: lines('flowchart LR', '  subgraph s [graph]', '    a', '  end') },
  { name: 'quirk-second-graph-keyword', text: lines('flowchart LR', '  A --> B', '  graph TD') },
  { name: 'quirk-sep-token', text: lines('flowchart LR', '  A\\|B') },
  { name: 'quirk-quote-in-id', text: lines('flowchart LR', '  a"b --> c') },
  { name: 'quirk-hash-and-star', text: lines('flowchart LR', '  #1 --> *2', '  a#b --> c*d') },
  { name: 'quirk-tab-separated', text: 'flowchart LR\n\tA\t-->\tB\n\tB --> C' },
  { name: 'quirk-crlf', text: 'flowchart LR\r\n  A --> B\r\n  B --> C\r\n' },
  { name: 'quirk-brace-then-spaces', text: lines('flowchart LR', '  A{decision}    ', '  A --> B') },
  { name: 'quirk-node-named-like-shape', text: lines('flowchart LR', '  rect --> circle', '  round[round] --> square') },
  { name: 'quirk-numbers-in-styles', text: lines('flowchart LR', '  A', '  style A stroke-width:2.5px,stroke-dasharray:3 3') },
  { name: 'quirk-unicode-letters-in-label', text: lines('flowchart LR', '  A(naïve) --> B{Ω}') },
  { name: 'quirk-click-before-node', text: lines('flowchart LR', '  click A callback', '  A --> B') },
  { name: 'quirk-shape-data-on-subgraph-member', text: lines('flowchart LR', '  subgraph s', '    A@{ shape: circle }', '  end') },
  { name: 'quirk-font-awesome-label', text: lines('flowchart LR', '  A[fa:fa-car Car] --> B[fab:fa-github Repo]') },

  // ---------------------------------------------------------------------------------------------- larger documents
  {
    name: 'document-mixed',
    text: lines('flowchart TB', '  accTitle: Order flow', '  start([Start]) --> check{In stock?}', '  check -- yes --> ship[[Ship]]',
      '  check -- no --> order[/Order more/]', '  order --> check', '  ship --> done(((Done)))', '  subgraph warehouse [Warehouse]',
      '    direction LR', '    ship', '    order', '  end', '  classDef ok fill:#cfc', '  class ship,done ok', '  linkStyle 1 stroke:#0a0')
  },
  {
    name: 'document-many-edges',
    text: lines('graph LR', ...Array.from({ length: 30 }, (_, i) => `  n${i} --> n${(i * 7 + 3) % 30}`))
  }
];
