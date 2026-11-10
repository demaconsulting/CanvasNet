// state-vector-cases.mjs - the state diagram texts of gen-diagram-vectors.mjs --type state. Each case is { name, spec,
// text } (and an optional site configuration). The expectations are not written here: the generator records what the
// pinned mermaid makes of each text.
//
// Attribution: most texts are the inputs of mermaid's own state parser tests at the pinned version (mermaid 11.17.2,
// packages/mermaid/src/diagrams/state/: stateDiagram-v2.spec.js, stateDiagram.spec.js, parser/state-parser.spec.js and
// parser/state-style.spec.js; MIT, Copyright (c) 2014 - 2022 Knut Sveidqvist, the notice of LICENSE-mermaid.txt). Their
// spec field names the upstream file and test. Only the texts are taken (each text that a test gives to the parser,
// once); the upstream assertions are replaced by the facts mermaid records. The texts of stateDb.spec.js, which calls the
// database directly, are written as diagram lines in the "original" cases that model them. The cases whose spec field
// is "original" cover features and quirks the upstream specs do not (concurrency at the top level, the legacy renderer,
// pseudo-state forms, styles, notes and errors), and the "fixture" cases are the state fixtures of the tests (their
// frontmatter is removed by mermaid's preprocessing; its configuration and title are not applied).

import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const provenance = 'The inputs are those of mermaid\'s state parser specs (stateDiagram-v2.spec.js, stateDiagram.spec.js, parser/state-parser.spec.js, parser/state-style.spec.js; MIT), named in each case\'s spec field, plus original texts and the state fixtures.';

const lines = (...rows) => rows.join('\n');
const v2 = (test) => `stateDiagram-v2.spec.js: ${test}`;
const v1 = (test) => `stateDiagram.spec.js: ${test}`;
const parserSpec = (test) => `parser/state-parser.spec.js: ${test}`;
const styleSpec = (test) => `parser/state-style.spec.js: ${test}`;
const db = (test) => `original, modeled on stateDb.spec.js: ${test}`;

// The state fixtures of the tests (original), read from the fixture folder
const fixtureFolder = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'test',
  'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'state');
const fixtures = readdirSync(fixtureFolder).filter((f) => f.endsWith('.mmd')).sort().map((f) => ({
  name: `fixture-${f.replace(/\.mmd$/, '')}`,
  spec: `fixture state/${f}`,
  text: readFileSync(join(fixtureFolder, f), 'utf8')
}));

// The legacy state renderer: a state.defaultRenderer other than dagre-wrapper selects the legacy "state" id
const legacy = { state: { defaultRenderer: 'dagre-d3' } };

export const cases = [  // ----------------------------------------------------------------------------------------- stateDiagram-v2.spec.js
  { name: "v2-super-simple", spec: v2("when parsing an info graph it > super simple"), text: "\n      stateDiagram-v2\n      [*] --> State1\n      State1 --> [*]\n      " },
  { name: "v2-simple", spec: v2("when parsing an info graph it > simple"), text: "stateDiagram-v2\n\n      State1 : this is another string\n      [*] --> State1\n      State1 --> [*]\n      " },
  { name: "v2-simple-with-acc-description", spec: v2("when parsing an info graph it > simple with accDescription"), text: "stateDiagram-v2\n\n      accDescr: a simple description of the diagram\n      State1 : this is another string\n      [*] --> State1\n      State1 --> [*]\n      " },
  { name: "v2-simple-with-title", spec: v2("when parsing an info graph it > simple with title"), text: "stateDiagram-v2\n\n      accTitle: a simple title of the diagram\n      State1 : this is another string\n      [*] --> State1\n      State1 --> [*]\n      " },
  { name: "v2-should-handle-relation-definitions", spec: v2("when parsing an info graph it > should handle relation definitions"), text: "stateDiagram-v2\n\n      [*] --> State1\n      State1 --> [*]\n      State1 : this is a string\n      State1 : this is another string\n\n      State1 --> State2\n      State2 --> [*]\n      " },
  { name: "v2-hide-empty-description", spec: v2("when parsing an info graph it > hide empty description"), text: "stateDiagram-v2\n\n      hide empty description\n      [*] --> State1\n      State1 --> [*]\n      State1 : this is a string\n      State1 : this is another string\n\n      State1 --> State2\n      State2 --> [*]\n      " },
  { name: "v2-handle-as-in-state-names", spec: v2("when parsing an info graph it > handle \"as\" in state names"), text: "stateDiagram-v2\n      assemble\n      state assemble\n      " },
  { name: "v2-handle-as-in-state-names-2", spec: v2("when parsing an info graph it > handle \"as\" in state names 2"), text: "stateDiagram-v2\n      assemblies\n      state assemblies\n      " },
  { name: "v2-handle-as-in-state-names-3", spec: v2("when parsing an info graph it > handle \"as\" in state names 3"), text: "stateDiagram-v2\n      state \"as\" as as\n      " },
  { name: "v2-simple-states-with-labels", spec: v2("when parsing an info graph it > relationship labels > simple states with : labels"), text: "\n          stateDiagram-v2\n          [*] --> State1\n          State1 --> State2 : Transition 1\n          State1 --> State3 : Transition 2\n          State1 --> State4 : Transition 3\n          State1 --> [*]\n        " },
  { name: "v2-scale", spec: v2("when parsing an info graph it > scale"), text: "stateDiagram-v2\n\n      scale 350 width\n      [*] --> State1\n      State1 --> [*]\n      State1 : this is a string with - in it\n      State1 : this is another string\n\n      State1 --> State2\n      State2 --> [*]\n      " },
  { name: "v2-description-after-second-state", spec: v2("when parsing an info graph it > description after second state"), text: "stateDiagram-v2\n\n      scale 350 width\n      [*] --> State1 : This is the description with - in it\n      State1 --> [*]\n      " },
  { name: "v2-shall-handle-descriptions-including-minus-signs", spec: v2("when parsing an info graph it > shall handle descriptions including minus signs"), text: "stateDiagram-v2\n\n      scale 350 width\n      [*] --> State1 : This is the description +-!\n      State1 --> [*]\n      " },
  { name: "v2-should-handle-state-statements", spec: v2("when parsing an info graph it > should handle state statements"), text: "stateDiagram-v2\n\n      state Configuring {\n        [*] --> NewValueSelection\n        NewValueSelection --> NewValuePreview : EvNewValue\n        NewValuePreview --> NewValueSelection : EvNewValueRejected\n        NewValuePreview --> NewValueSelection : EvNewValueSaved1\n      }\n      " },
  { name: "v2-should-handle-recursive-state-definitions", spec: v2("when parsing an info graph it > should handle recursive state definitions"), text: "stateDiagram-v2\n\n      state Configuring {\n        [*] --> NewValueSelection\n        NewValueSelection --> NewValuePreview : EvNewValue\n        NewValuePreview --> NewValueSelection : EvNewValueRejected\n        NewValuePreview --> NewValueSelection : EvNewValueSaved\n\n        state NewValuePreview {\n          State1 --> State2\n        }\n      }\n      " },
  { name: "v2-should-handle-multiple-recursive-state-definitions", spec: v2("when parsing an info graph it > should handle multiple recursive state definitions"), text: "stateDiagram-v2\n\n      scale 350 width\n      [*] --> NotShooting\n\n      state NotShooting {\n        [*] --> Idle\n        Idle --> Configuring : EvConfig\n        Configuring --> Idle : EvConfig\n      }\n\n      state Configuring {\n        [*] --> NewValueSelection\n        NewValueSelection --> NewValuePreview : EvNewValue\n        NewValuePreview --> NewValueSelection : EvNewValueRejected\n        NewValuePreview --> NewValueSelection : EvNewValueSaved\n\n        state NewValuePreview {\n          State1 --> State2\n        }\n      }\n      " },
  { name: "v2-should-handle-state-definitions-with-separation-of-id", spec: v2("when parsing an info graph it > should handle state definitions with separation of id"), text: "stateDiagram-v2\n\n      state \"Long state description\" as state1\n      " },
  { name: "v2-should-handle-state-definitions-with-separation-of-id-2", spec: v2("when parsing an info graph it > should handle state definitions with separation of id"), text: "stateDiagram-v2\n      state \"Not Shooting State\" as NotShooting {\n        state \"Idle mode\" as Idle\n        state \"Configuring mode\" as Configuring\n        [*] --> Idle\n        Idle --> Configuring : EvConfig\n        Configuring --> Idle : EvConfig\n      }\n      " },
  { name: "v2-should-state-definition-with-quotes", spec: v2("when parsing an info graph it > should State definition with quotes"), text: "stateDiagram-v2\n\n      scale 600 width\n\n      [*] --> State1\n      State1 --> State2 : Succeeded\n      State1 --> [*] : Aborted\n      State2 --> State3 : Succeeded\n      State2 --> [*] : Aborted\n      state State3 {\n        state \"Accumulate Enough Data\nLong State Name\" as long1\n        long1 : Just a test\n        [*] --> long1\n        long1 --> long1 : New Data\n        long1 --> ProcessData : Enough Data\n      }\n      State3 --> State3 : Failed\n      State3 --> [*] : Succeeded / Save Result\n      State3 --> [*] : Aborted\n      " },
  { name: "v2-should-handle-fork-statements", spec: v2("when parsing an info graph it > should handle fork statements"), text: "stateDiagram-v2\n\n      state fork_state <<fork>>\n      [*] --> fork_state\n      fork_state --> State2\n      fork_state --> State3\n\n      state join_state <<join>>\n      State2 --> join_state\n      State3 --> join_state\n      join_state --> State4\n      State4 --> [*]\n      " },
  { name: "v2-should-handle-concurrent-state", spec: v2("when parsing an info graph it > should handle concurrent state"), text: "stateDiagram-v2\n\n      [*] --> Active\n\n      state Active {\n        [*] --> NumLockOff\n        NumLockOff --> NumLockOn : EvNumLockPressed\n        NumLockOn --> NumLockOff : EvNumLockPressed\n        --\n        [*] --> CapsLockOff\n        CapsLockOff --> CapsLockOn : EvCapsLockPressed\n        CapsLockOn --> CapsLockOff : EvCapsLockPressed\n        --\n        [*] --> ScrollLockOff\n        ScrollLockOff --> ScrollLockOn : EvCapsLockPressed\n        ScrollLockOn --> ScrollLockOff : EvCapsLockPressed\n      }\n      " },
  { name: "v2-should-handle-concurrent-state-2", spec: v2("when parsing an info graph it > should handle concurrent state"), text: "stateDiagram-v2\n\n      [*] --> Active\n\n      state Active {\n        [*] --> NumLockOff\n        --\n        [*] --> CapsLockOff\n        --\n        [*] --> ScrollLockOff\n      }\n      " },
  { name: "v2-should-handle-note-statements", spec: v2("when parsing an info graph it > should handle note statements"), text: "stateDiagram-v2\n\n      [*] --> Active\n      Active --> Inactive\n\n      note left of Active : this is a short<br/>note\n\n      note right of Inactive\n      A note can also\n      be defined on\n      several lines\n      end note\n      " },
  { name: "v2-should-handle-multiline-notes-with-different-line-breaks", spec: v2("when parsing an info graph it > should handle multiline notes with different line breaks"), text: "stateDiagram-v2\n      State1\n      note right of State1\n      Line1<br>Line2<br/>Line3<br />Line4<br\t/>Line5\n      end note\n      " },
  { name: "v2-should-handle-floating-notes", spec: v2("when parsing an info graph it > should handle floating notes"), text: "stateDiagram-v2\n      foo: bar\n      note \"This is a floating note\" as N1\n      " },
  { name: "v2-should-handle-floating-notes-2", spec: v2("when parsing an info graph it > should handle floating notes"), text: "stateDiagram-v2\n      state foo\n      note \"This is a floating note\" as N1\n      " },
  { name: "v2-should-handle-notes-for-composite-nested-states", spec: v2("when parsing an info graph it > should handle notes for composite (nested) states"), text: "stateDiagram-v2\n      [*] --> NotShooting\n\n      state \"Not Shooting State\" as NotShooting {\n        state \"Idle mode\" as Idle\n        state \"Configuring mode\" as Configuring\n        [*] --> Idle\n        Idle --> Configuring : EvConfig\n        Configuring --> Idle : EvConfig\n      }\n\n      note right of NotShooting : This is a note on a composite state\n      " },
  { name: "v2-a-composite-state-should-be-able-to-link-to-itself", spec: v2("when parsing an info graph it > A composite state should be able to link to itself"), text: "\n          stateDiagram-v2\n            state Active {\n              Idle\n            }\n            Inactive --> Idle: ACT\n            Active --> Active: LOG\n        " },
  { name: "v2-should-check-default-diagram-direction", spec: v2("when parsing an info graph it > should check default diagram direction"), text: "\n        stateDiagram\n          [*] --> Still\n          Still --> [*]\n        " },
  { name: "v2-retrieve-the-diagram-direction-correctly", spec: v2("when parsing an info graph it > retrieve the diagram direction correctly"), text: "\n        stateDiagram\n          direction LR\n          [*] --> Still\n          Still --> [*]\n        " },

  // -------------------------------------------------------------------------------------------- stateDiagram.spec.js
  { name: "v1-super-simple", spec: v1("when parsing an info graph it > super simple"), text: "\n      stateDiagram\n        [*] --> State1\n        State1 --> [*]\n      " },
  { name: "v1-simple", spec: v1("when parsing an info graph it > simple"), text: "stateDiagram\n\n          State1 : this is another string\n          [*] --> State1\n          State1 --> [*]\n      " },
  { name: "v1-simple-with-acc-description-acc-descr", spec: v1("when parsing an info graph it > simple with accDescription (accDescr)"), text: "stateDiagram\n\n          accDescr: a simple description of the diagram\n          State1 : this is another string\n          [*] --> State1\n          State1 --> [*]\n      " },
  { name: "v1-simple-with-multiline-acc-description-acc-descr", spec: v1("when parsing an info graph it > simple with multiline accDescription (accDescr)"), text: "stateDiagram\n\n          accDescr {\n             a simple description of the diagram\n            using multiple lines\n          }\n          State1 : this is another string\n          [*] --> State1\n          State1 --> [*]\n      " },
  { name: "v1-simple-with-title-acc-descr", spec: v1("when parsing an info graph it > simple with title  (accDescr)"), text: "stateDiagram\n\n          accTitle: a simple title of the diagram\n          State1 : this is another string\n          [*] --> State1\n          State1 --> [*]\n      " },
  { name: "v1-should-handle-relation-definitions", spec: v1("when parsing an info graph it > should handle relation definitions"), text: "stateDiagram\n\n        [*] --> State1\n        State1 --> [*]\n        State1 : this is a string\n        State1 : this is another string\n\n        State1 --> State2\n        State2 --> [*]\n      " },
  { name: "v1-hide-empty-description", spec: v1("when parsing an info graph it > hide empty description"), text: "stateDiagram\n\n        hide empty description\n        [*] --> State1\n        State1 --> [*]\n        State1 : this is a string\n        State1 : this is another string\n\n        State1 --> State2\n        State2 --> [*]\n      " },
  { name: "v1-handle-as-in-state-names", spec: v1("when parsing an info graph it > handle \"as\" in state names"), text: "stateDiagram\n      assemble\n      state assemble\n      " },
  { name: "v1-handle-as-in-state-names-2", spec: v1("when parsing an info graph it > handle \"as\" in state names 2"), text: "stateDiagram\n      assemblies\n      state assemblies\n      " },
  { name: "v1-handle-as-in-state-names-3", spec: v1("when parsing an info graph it > handle \"as\" in state names 3"), text: "stateDiagram\n      state \"as\" as as\n      " },
  { name: "v1-scale", spec: v1("when parsing an info graph it > scale"), text: "stateDiagram\n\n        scale 350 width\n        [*] --> State1\n        State1 --> [*]\n        State1 : this is a string with - in it\n        State1 : this is another string\n\n        State1 --> State2\n        State2 --> [*]\n      " },
  { name: "v1-description-after-second-state", spec: v1("when parsing an info graph it > description after second state"), text: "stateDiagram\n\n        scale 350 width\n        [*] --> State1 : This is the description with - in it\n        State1 --> [*]\n      " },
  { name: "v1-shall-handle-descriptions-including-minus-signs", spec: v1("when parsing an info graph it > shall handle descriptions including minus signs"), text: "stateDiagram\n\n        scale 350 width\n        [*] --> State1 : This is the description +-!\n        State1 --> [*]\n      " },
  { name: "v1-should-handle-state-statements", spec: v1("when parsing an info graph it > should handle state statements"), text: "stateDiagram\n\n        state Configuring {\n          [*] --> NewValueSelection\n          NewValueSelection --> NewValuePreview : EvNewValue\n          NewValuePreview --> NewValueSelection : EvNewValueRejected\n          NewValuePreview --> NewValueSelection : EvNewValueSaved1\n        }\n      " },
  { name: "v1-should-handle-recursive-state-definitions", spec: v1("when parsing an info graph it > should handle recursive state definitions"), text: "stateDiagram\n\n        state Configuring {\n          [*] --> NewValueSelection\n          NewValueSelection --> NewValuePreview : EvNewValue\n          NewValuePreview --> NewValueSelection : EvNewValueRejected\n          NewValuePreview --> NewValueSelection : EvNewValueSaved\n\n          state NewValuePreview {\n          State1 --> State2\n          }\n        }\n      " },
  { name: "v1-should-handle-multiple-recursive-state-definitions", spec: v1("when parsing an info graph it > should handle multiple recursive state definitions"), text: "stateDiagram\n\n        scale 350 width\n        [*] --> NotShooting\n\n        state NotShooting {\n          [*] --> Idle\n          Idle --> Configuring : EvConfig\n          Configuring --> Idle : EvConfig\n        }\n\n        state Configuring {\n          [*] --> NewValueSelection\n          NewValueSelection --> NewValuePreview : EvNewValue\n          NewValuePreview --> NewValueSelection : EvNewValueRejected\n          NewValuePreview --> NewValueSelection : EvNewValueSaved\n\n          state NewValuePreview {\n          State1 --> State2\n          }\n        }\n      " },
  { name: "v1-should-handle-state-definitions-with-separation-of-id", spec: v1("when parsing an info graph it > should handle state definitions with separation of id"), text: "stateDiagram\n\n        state \"Long state description\" as state1\n        " },
  { name: "v1-should-handle-state-definitions-with-separation-of-id-2", spec: v1("when parsing an info graph it > should handle state definitions with separation of id"), text: "stateDiagram\n      state \"Not Shooting State\" as NotShooting {\n        state \"Idle mode\" as Idle\n        state \"Configuring mode\" as Configuring\n        [*] --> Idle\n        Idle --> Configuring : EvConfig\n        Configuring --> Idle : EvConfig\n      }\n        " },
  { name: "v1-should-state-definition-with-quotes", spec: v1("when parsing an info graph it > should State definition with quotes"), text: "stateDiagram\n\n        scale 600 width\n\n        [*] --> State1\n        State1 --> State2 : Succeeded\n        State1 --> [*] : Aborted\n        State2 --> State3 : Succeeded\n        State2 --> [*] : Aborted\n        state State3 {\n          state \"Accumulate Enough Data\nLong State Name\" as long1\n          long1 : Just a test\n          [*] --> long1\n          long1 --> long1 : New Data\n          long1 --> ProcessData : Enough Data\n        }\n        State3 --> State3 : Failed\n        State3 --> [*] : Succeeded / Save Result\n        State3 --> [*] : Aborted\n      " },
  { name: "v1-should-handle-fork-statements", spec: v1("when parsing an info graph it > should handle fork statements"), text: "stateDiagram\n\n        state fork_state <<fork>>\n        [*] --> fork_state\n        fork_state --> State2\n        fork_state --> State3\n\n        state join_state <<join>>\n        State2 --> join_state\n        State3 --> join_state\n        join_state --> State4\n        State4 --> [*]\n      " },
  { name: "v1-should-handle-concurrent-state", spec: v1("when parsing an info graph it > should handle concurrent state"), text: "stateDiagram\n\n        [*] --> Active\n\n        state Active {\n          [*] --> NumLockOff\n          NumLockOff --> NumLockOn : EvNumLockPressed\n          NumLockOn --> NumLockOff : EvNumLockPressed\n          --\n          [*] --> CapsLockOff\n          CapsLockOff --> CapsLockOn : EvCapsLockPressed\n          CapsLockOn --> CapsLockOff : EvCapsLockPressed\n          --\n          [*] --> ScrollLockOff\n          ScrollLockOff --> ScrollLockOn : EvCapsLockPressed\n          ScrollLockOn --> ScrollLockOff : EvCapsLockPressed\n        }\n      " },
  { name: "v1-should-handle-concurrent-state-2", spec: v1("when parsing an info graph it > should handle concurrent state"), text: "stateDiagram\n\n        [*] --> Active\n\n        state Active {\n          [*] --> NumLockOff\n          --\n          [*] --> CapsLockOff\n          --\n          [*] --> ScrollLockOff\n        }\n      " },
  { name: "v1-should-handle-note-statements", spec: v1("when parsing an info graph it > should handle note statements"), text: "stateDiagram\n\n        [*] --> Active\n        Active --> Inactive\n\n        note left of Active : this is a short<br/>note\n\n        note right of Inactive\n          A note can also\n          be defined on\n          several lines\n        end note\n      " },
  { name: "v1-should-handle-multiline-notes-with-different-line-breaks", spec: v1("when parsing an info graph it > should handle multiline notes with different line breaks"), text: "stateDiagram\n        State1\n        note right of State1\n          Line1<br>Line2<br/>Line3<br />Line4<br\t/>Line5\n        end note\n      " },
  { name: "v1-should-handle-floating-notes", spec: v1("when parsing an info graph it > should handle floating notes"), text: "stateDiagram\n        foo: bar\n        note \"This is a floating note\" as N1\n      " },
  { name: "v1-should-handle-floating-notes-2", spec: v1("when parsing an info graph it > should handle floating notes"), text: "stateDiagram\n\n        state foo\n        note \"This is a floating note\" as N1\n      " },
  { name: "v1-should-handle-notes-for-composite-states", spec: v1("when parsing an info graph it > should handle notes for composite states"), text: "stateDiagram\n\n        [*] --> NotShooting\n\n        state \"Not Shooting State\" as NotShooting {\n          state \"Idle mode\" as Idle\n          state \"Configuring mode\" as Configuring\n          [*] --> Idle\n          Idle --> Configuring : EvConfig\n          Configuring --> Idle : EvConfig\n        }\n\n        note right of NotShooting : This is a note on a composite state\n      " },

  // ------------------------------------------------------------------------------------- parser/state-parser.spec.js
  { name: "parser-should-only-accept-1-word", spec: parserSpec("invalid name between state and curly bracket > valid syntax > should only accept 1 word"), text: `stateDiagram-v2
        state valid { X }` },
  { name: "parser-should-throw-error-with-2-words", spec: parserSpec("invalid name between state and curly bracket > invalid syntax > should throw error with 2 words"), text: `stateDiagram-v2
        state invalid syntax { Y }` },
  { name: "parser-should-also-throw-with-more-than-2-words", spec: parserSpec("invalid name between state and curly bracket > invalid syntax > should also throw with more than 2 words"), text: `stateDiagram-v2
        state invalid syntax with more than 2 words { Z }` },
  { name: "parser-state-id-as-some-name", spec: parserSpec("states with id displayed as a (name) > syntax 1: stateID as \"name in quotes\" > stateID as \"some name\""), text: `stateDiagram-v2
        state "Small State 1" as namedState1` },
  { name: "parser-space-before-and-after-the-colon", spec: parserSpec("states with id displayed as a (name) > syntax 2: stateID: \"name in quotes\" [colon after the id] > space before and after the colon"), text: `stateDiagram-v2
        namedState1 : Small State 1` },
  { name: "parser-no-spaces-before-and-after-the-colon", spec: parserSpec("states with id displayed as a (name) > syntax 2: stateID: \"name in quotes\" [colon after the id] > no spaces before and after the colon"), text: `stateDiagram-v2
        namedState1:Small State 1` },
  { name: "parser-assemble-assemblies-state-assemble-state-assemblies", spec: parserSpec("can handle \"as\" in a state name > assemble, assemblies, state assemble, state assemblies"), text: "stateDiagram-v2\n      assemble\n      assemblies\n      state assemble\n      state assemblies\n      " },
  { name: "parser-state-group-name-as-state-identifier", spec: parserSpec("groups (clusters/containers) > state \"Group Name\" as stateIdentifier"), text: "stateDiagram-v2\n        state \"Small State 1\" as namedState1\n        %% Notice that this is named \"Big State 1\" with an \"as\"\n        state \"Big State 1\" as bigState1 {\n            bigState1InternalState\n        }\n        namedState1 --> bigState1: should point to \\nBig State 1 container\n\n        state \"Small State 2\" as namedState2\n        %% Notice that bigState2 does not have a name; no \"as\"\n        state bigState2 {\n            bigState2InternalState\n        }\n        namedState2 --> bigState2: should point to \\nbigState2 container" },
  { name: "parser-group-has-a-state-with-state-id-as-state-name-and", spec: parserSpec("groups (clusters/containers) > group has a state with stateID AS \"state name\" and state2ID: \"another state name\""), text: `stateDiagram-v2
        state "Big State 1" as bigState1 {
            state "inner state 1" as inner1
            inner2: inner state 2
            inner1 --> inner2
        }` },
  { name: "parser-should-allow-a-colon-inside-transition-text", spec: parserSpec("colons in transition descriptions (issue #7418) > should allow a colon inside transition text"), text: `stateDiagram-v2
        locked --> pending : recoverable error (ex: timeout)` },
  { name: "parser-should-allow-multiple-colons-inside-transition-text", spec: parserSpec("colons in transition descriptions (issue #7418) > should allow multiple colons inside transition text"), text: `stateDiagram-v2
        A --> B : info: key: value` },
  { name: "parser-should-allow-a-colon-inside-state-description-text", spec: parserSpec("colons in transition descriptions (issue #7418) > should allow a colon inside state description text"), text: `stateDiagram-v2
        myState : status: active` },
  { name: "parser-should-allow-proto-as-a-state-name", spec: parserSpec("unsafe properties as state names > should allow __proto__ as a state name"), text: `
stateDiagram-v2
[*] --> __proto__
__proto__ --> [*]` },
  { name: "parser-should-allow-constructor-as-a-state-name", spec: parserSpec("unsafe properties as state names > should allow constructor as a state name"), text: `
stateDiagram-v2
[*] --> constructor
constructor --> [*]` },
  { name: "parser-should-not-terminate-a-note-when-send-note-appears", spec: parserSpec("note parsing (issue #7089) > should not terminate a note when \"send note\" appears within the note text"), text: `stateDiagram-v2
      State1
      note right of State1
        this sentence contains send note inside the note text
      end note
      State1 --> State2` },
  { name: "parser-should-not-treat-end-note-as-a-closing-keyword-when-it", spec: parserSpec("note parsing (issue #7089) > should not treat \"end note\" as a closing keyword when it is part of the note text"), text: `stateDiagram-v2
      State1
      note right of State1
        this sentence contains end note as part of the note text
      end note
      State1 --> State2` },

  // -------------------------------------------------------------------------------------- parser/state-style.spec.js
  { name: "style-has-class-def-as-a-keyword-an-id-and-can-set-a-css-style", spec: styleSpec("class for a state (classDef) > defining (classDef) > has \"classDef\" as a keyword, an id, and can set a css style attribute"), text: `stateDiagram-v2
 classDef exampleClass background:#bbb;` },
  { name: "style-can-define-multiple-attributes-separated-by-commas", spec: styleSpec("class for a state (classDef) > defining (classDef) > can define multiple attributes separated by commas"), text: `stateDiagram-v2
 classDef exampleClass background:#bbb, font-weight:bold, font-style:italic;` },
  { name: "style-an-attribute-can-have-a-dot-in-the-style", spec: styleSpec("class for a state (classDef) > defining (classDef) > an attribute can have a dot in the style"), text: `stateDiagram-v2
 classDef exampleStyleClass background:#bbb,border:1.5px solid red;` },
  { name: "style-an-attribute-can-have-a-space-in-the-style", spec: styleSpec("class for a state (classDef) > defining (classDef) > an attribute can have a space in the style"), text: `stateDiagram-v2
 classDef exampleStyleClass background:  #bbb,border:1.5px solid red;` },
  { name: "style-can-have-proto-or-constructor-as-a-class-name", spec: styleSpec("class for a state (classDef) > defining (classDef) > can have __proto__ or constructor as a class name"), text: `stateDiagram-v2
 classDef __proto__ background:#bbb,border:1.5px solid red;
 classDef constructor background:#bbb,border:1.5px solid red;` },
  { name: "style-can-apply-a-class-to-a-state", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > can apply a class to a state"), text: `stateDiagram-v2

classDef exampleStyleClass background:#bbb,border:1px solid red;
a --> b class a exampleStyleClass` },
  { name: "style-can-be-applied-to-a-state-with-an-id-containing", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > can be applied to a state with an id containing _"), text: `stateDiagram-v2

classDef exampleStyleClass background:#bbb,border:1px solid red;
a_a --> b_b
class a_a exampleStyleClass` },
  { name: "style-can-be-applied-to-a-state-using-syntax", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > ::: syntax > can be applied to a state using ::: syntax"), text: `stateDiagram-v2

classDef exampleStyleClass background:#bbb,border:1px solid red;
a --> b:::exampleStyleClass
` },
  { name: "style-can-be-applied-to-a-state", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > ::: syntax > can be applied to a [*] state"), text: `stateDiagram-v2

classDef exampleStyleClass background:#bbb,border:1px solid red;
[*]:::exampleStyleClass --> b
` },
  { name: "style-can-be-applied-to-a-comma-separated-list-of-states", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > ::: syntax > can be applied to a comma separated list of states"), text: `stateDiagram-v2

classDef exampleStyleClass background:#bbb,border:1px solid red;
a-->b
class a,b exampleStyleClass` },
  { name: "style-a-comma-separated-list-of-states-may-or-may-not-have", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > ::: syntax > a comma separated list of states may or may not have spaces after commas"), text: `stateDiagram-v2

classDef exampleStyleClass background:#bbb,border:1px solid red;
a-->b
class a,b,c, d, e exampleStyleClass` },
  { name: "style-can-be-applied-to-a-state-inside-a-composite-state", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > ::: syntax inside composite states > can be applied to a state inside a composite state"), text: `stateDiagram-v2

classDef exampleStyleClass background:#bbb,border:1px solid red;
state A {
  a:::exampleStyleClass --> b
}
` },
  { name: "style-can-be-applied-to-a-state-inside-a-composite-state-2", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > ::: syntax inside composite states > can be applied to a [*] state inside a composite state"), text: `stateDiagram-v2

classDef exampleStyleClass background:#bbb,border:1px solid red;
state A {
  [*]:::exampleStyleClass --> a
}
` },
  { name: "style-working-inside-states", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > comments parsing > working inside states"), text: `stateDiagram-v2

[*] --> Moving
Moving --> Still
Moving --> Crash
state Moving {
%% comment inside state
slow  --> fast
}
` },
  { name: "style-should-handle-comments-correctly", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > comments parsing > should handle comments correctly"), text: `stateDiagram-v2
%%initial_comment
[*] --> Moving %%inline_comment
%%final_comment
` },
  { name: "style-should-handle-comments-correctly-inside-states", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > comments parsing > should handle comments correctly inside states"), text: `stateDiagram-v2
state Moving {
%%comment_inside_state
slow  --> fast %%inline_comment
}
` },
  { name: "style-should-handle-comments-correctly-after-a-blank-line", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > comments parsing > should handle comments correctly after a blank line"), text: `stateDiagram-v2
[*] --> Moving

%% comment after a blank line
Moving --> Still
` },
  { name: "style-should-parse-single-as-normal-syntax-not-a-comment", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > comments parsing > should parse single % as normal syntax, not a comment"), text: `stateDiagram-v2
% not a comment
Moving --> Still %inline
` },
  { name: "style-should-skip-multiple-consecutive-comment-lines", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > comments parsing > should skip multiple consecutive comment lines"), text: `stateDiagram-v2
%% first comment
%% second comment
%% third comment
[*] --> Moving
` },
  { name: "style-transitions-should-be-preserved-when-separated-by-a", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > comments parsing > transitions should be preserved when separated by a comment"), text: `stateDiagram-v2
Moving --> Crash
%% comment between
Moving --> Still
` },
  { name: "style-should-correctly-handle-inline-comments-in-state-syntax", spec: styleSpec("class for a state (classDef) > applying to states in the diagram > comments parsing > should correctly handle inline %% comments in state syntax"), text: `stateDiagram-v2
[*] --> Moving%% this is a comment
Moving --> Still%Active
` },
  { name: "style-has-style-as-a-keyword-an-id-and-can-set-a-css-style", spec: styleSpec("style statement for a state (style) > defining (style) > has \"style\" as a keyword, an id, and can set a css style attribute"), text: `stateDiagram-v2
        id1
        style id1 background:#bbb` },
  { name: "style-has-style-as-a-keyword-an-id-and-can-set-a-css-style-2", spec: styleSpec("style statement for a state (style) > defining (style) > has \"style\" as a keyword, an id, and can set a css style attribute"), text: `stateDiagram-v2
        id1
        id2
        style id1,id2 background:#bbb` },
  { name: "style-can-define-multiple-attributes-separated-by-commas-2", spec: styleSpec("style statement for a state (style) > defining (style) > can define multiple attributes separated by commas"), text: `stateDiagram-v2
        id1
        id2
        style id1,id2 background:#bbb, font-weight:bold, font-style:italic;` },

  // ------------------------------------------------------------------------- original: stateDb.spec.js as diagram text
  { name: 'db-description-leading-colons', spec: db('addDescription removes only the first leading :'), text: lines('stateDiagram-v2', 'state1', 'state1 : rest of the description', 'state1 : :rest of the description', 'state1 : ::rest of the description') },
  { name: 'db-description-double-colon', spec: db('addDescription removes only the first leading :'), text: lines('stateDiagram-v2', 'state1', 'state1 ::rest of the description') },
  { name: 'db-descriptions-accumulate', spec: db('addDescription adds each description to the array of descriptions'), text: lines('stateDiagram-v2', 'state1 : one', 'state1 : two', 'state1 : three') },
  { name: 'db-classdef-attributes', spec: db('addStyleClass is added to the list of style classes'), text: lines('stateDiagram-v2', 'classDef newStyleClass font-weight:bold, border:blue;', 'A') },

  // ------------------------------------------------------------------------------------- original: not in the specs
  { name: 'concurrency-top-level-dashes', spec: 'original', text: lines('stateDiagram-v2', 'A --> B', '--', 'C') },
  { name: 'concurrency-trailing-divider', spec: 'original', text: lines('stateDiagram-v2', 'state Busy {', '  a --> b', '  --', '  c --> d', '  --', '}') },
  { name: 'concurrency-nested', spec: 'original', text: lines('stateDiagram-v2', 'state Outer {', '  state Inner {', '    x --> y', '    --', '    z', '  }', '  --', '  w', '}') },
  { name: 'pseudo-states-bracket-forms', spec: 'original', text: lines('stateDiagram-v2', 'state f1 [[fork]]', 'state j1 [[join]]', 'state c1 [[choice]]', 'state f2 <<fork>>', '[*] --> f1', 'f1 --> c1', 'c1 --> j1 : yes', 'c1 --> f2 : no', 'f2 --> j1', 'j1 --> [*]') },
  { name: 'pseudo-state-with-description', spec: 'original', text: lines('stateDiagram-v2', 'state c1 <<choice>>', 'c1 : a description', 'A --> c1') },
  { name: 'start-end-only', spec: 'original', text: lines('stateDiagram-v2', '[*] --> [*]') },
  { name: 'start-end-ids-in-composites', spec: 'original', text: lines('stateDiagram-v2', '[*] --> A', 'state A {', '  [*] --> B', '  B --> [*]', '  state B {', '    [*] --> C', '    C --> [*]', '  }', '}', 'A --> [*]') },
  { name: 'composite-multi-word-name', spec: 'original', text: lines('stateDiagram-v2', 'state Two Words {', '  A', '}') },
  { name: 'composite-with-descriptions', spec: 'original', text: lines('stateDiagram-v2', 'state "Title" as Comp {', '  A', '}', 'Comp : another') },
  { name: 'composite-description-only-title', spec: 'original', text: lines('stateDiagram-v2', 'state "Long composite title" as Comp {', '  A --> B', '}') },
  { name: 'composite-reopened', spec: 'original', text: lines('stateDiagram-v2', 'state Comp {', '  A', '}', 'state Comp {', '  B', '}') },
  { name: 'composite-empty', spec: 'original', text: lines('stateDiagram-v2', 'state Empty {', '}', 'Empty --> Next') },
  { name: 'direction-nested-and-last-wins', spec: 'original', text: lines('stateDiagram-v2', 'direction LR', 'state Comp {', '  direction BT', '  A --> B', '}', 'direction RL') },
  { name: 'direction-inline-swallows-line', spec: 'original', text: lines('stateDiagram-v2', 'A --> B %% direction TB is not a statement here', 'C') },
  { name: 'ids-with-hyphen', spec: 'original', text: lines('stateDiagram-v2', 'first-state --> second') },
  { name: 'ids-inline-comment', spec: 'original', text: lines('stateDiagram-v2', 'A --> B %% a comment', 'B --> C') },
  { name: 'transitions-without-spaces', spec: 'original', text: lines('stateDiagram-v2', 'A-->B', 'B-->C:go') },
  { name: 'transition-label-sanitized', spec: 'original', text: lines('stateDiagram-v2', 'A --> B : <b>bold</b> and <script>x</script>') },
  { name: 'state-unicode-names', spec: 'original', text: lines('stateDiagram-v2', 'Café --> Naïve : cliché', 'Naïve : déjà vu') },
  { name: 'state-description-forms', spec: 'original', text: lines('stateDiagram-v2', 'state "Quoted name" as Q', 'Q : more', 'R : only text', 'state S', 'S : first', 'S : second') },
  { name: 'notes-same-state-twice', spec: 'original', text: lines('stateDiagram-v2', 'A --> B', 'note left of A : first', 'note right of A : second') },
  { name: 'notes-in-composite', spec: 'original', text: lines('stateDiagram-v2', 'state Comp {', '  A', '  note right of A : inside', '}', 'note left of Comp : outside') },
  { name: 'notes-multiline-indented', spec: 'original', text: lines('stateDiagram-v2', 'A', 'note left of A', '    first line', '      second line', 'end note') },
  { name: 'notes-on-start-state', spec: 'original', text: lines('stateDiagram-v2', '[*] --> A', 'note right of [*] : start note') },
  { name: 'floating-note-dropped', spec: 'original', text: lines('stateDiagram-v2', 'A --> B', 'note "floating" as N1', 'N1 --> B') },
  { name: 'error-classdef-default', spec: 'original', text: lines('stateDiagram-v2', 'classDef default fill:#eee', 'A --> B') },
  { name: 'styles-class-statement-list', spec: 'original', text: lines('stateDiagram-v2', 'classDef hot fill:#f96,color:#036', 'A --> B', 'class A,B hot', 'class C hot') },
  { name: 'styles-shorthand-both-ends', spec: 'original', text: lines('stateDiagram-v2', 'classDef a fill:#f00', 'classDef b fill:#00f', '[*]:::a --> X:::b', 'X:::a --> [*]') },
  { name: 'styles-style-statement-list', spec: 'original', text: lines('stateDiagram-v2', 'A --> B', 'style A,B fill:#f9f,stroke:#333;', 'style C stroke-width:4px') },
  { name: 'styles-classdef-after-use', spec: 'original', text: lines('stateDiagram-v2', 'A:::late --> B', 'classDef late fill:#ff0') },
  { name: 'click-and-href', spec: 'original', text: lines('stateDiagram-v2', 'A --> B', 'click A "https://example.com/a" "Tooltip A"', 'click B href "https://example.com/b"') },
  { name: 'hide-empty-and-scale', spec: 'original', text: lines('stateDiagram-v2', 'hide empty description', 'scale 200 width', 'A --> B') },
  { name: 'acc-descr-block', spec: 'original', text: lines('stateDiagram-v2', 'accTitle: The title', 'accDescr {', '  first line', '  second line', '}', 'A') },
  { name: 'title-statement-is-a-state', spec: 'original', text: lines('stateDiagram-v2', 'title A title', 'A') },
  { name: 'legacy-renderer-site', spec: 'original', site: legacy, text: lines('stateDiagram', '[*] --> A', 'A --> B : go', 'state B {', '  C', '}') },
  { name: 'legacy-renderer-v2-header', spec: 'original', site: legacy, text: lines('stateDiagram-v2', '[*] --> A') },
  { name: 'semicolon-statements', spec: 'original', text: 'stateDiagram-v2\nA --> B;\nB --> C' },
  { name: 'error-missing-target', spec: 'original', text: lines('stateDiagram-v2', 'A -->') },
  { name: 'error-unclosed-composite', spec: 'original', text: lines('stateDiagram-v2', 'state A {', '  B') },
  { name: 'error-unclosed-note', spec: 'original', text: lines('stateDiagram-v2', 'A', 'note left of A', '  never closed') },
  { name: 'error-classdef-default-keyword', spec: 'original', text: lines('stateDiagram-v2', 'classDef DEFAULT fill:#eee', 'A') },
  { name: 'stray-brace-is-a-state', spec: 'original', text: lines('stateDiagram-v2', 'A --> B', '}') },

  // ------------------------------------------------------------------------------------------ the state fixtures
  ...fixtures
];