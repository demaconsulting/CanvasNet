// gitgraph-vector-cases.mjs - the git graph texts of gen-diagram-vectors.mjs --type gitgraph. Each case is
// { name, spec, text } (and an optional site configuration). The expectations are not written here: the generator
// records what the pinned mermaid makes of each text (with Math.random seeded, see seeded-random.mjs, so that the ids
// mermaid generates for commits without one are the same in every run).
//
// Attribution: many texts are the inputs of the git graph tests at the pinned version: mermaid's
// packages/mermaid/src/diagrams/git/gitGraph.spec.ts (MIT, Copyright (c) 2014 - 2022 Knut Sveidqvist, the notice of
// LICENSE-mermaid.txt) and @mermaid-js/parser's packages/parser/tests/gitGraph.test.ts (MIT, Copyright (c) 2023
// Yokozuna59, the notice of LICENSE-mermaid-parser.txt). Their spec field names the upstream file and test. Only the
// texts are taken; the upstream assertions are replaced by the facts mermaid records. The parser tests call the Langium
// parser directly; here every text goes through mermaid's preprocessing and detection first, as a diagram does. The
// cases whose spec field is "original" cover the header, the keywords and terminals of the lexer, the options of each
// statement, every database error and the operations that are not errors, the sanitizing of the texts, the title and
// accessibility texts, and the main branch name and order (given as a site configuration, because getDiagramFromText
// does not apply frontmatter configuration); the "fixture" cases are the git graph fixtures of the tests, without their
// frontmatter (a fixture's gitGraph settings are given as the case's site configuration).

import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const provenance = 'The inputs are those of mermaid\'s git graph spec (diagrams/git/gitGraph.spec.ts; MIT) and of @mermaid-js/parser\'s git graph tests (tests/gitGraph.test.ts; MIT), named in each case\'s spec field, plus original texts and the git graph fixtures.';

const lines = (...rows) => rows.join('\n');
const gitSpec = (test) => `diagrams/git/gitGraph.spec.ts: ${test}`;
const parserSpec = (test) => `@mermaid-js/parser tests/gitGraph.test.ts: ${test}`;

// A git graph of the given statement lines (each indented by two spaces)
const graph = (...rows) => lines('gitGraph', ...rows.map((row) => `  ${row}`));

// A site configuration with a gitGraph section
const site = (gitGraph) => ({ gitGraph });

// The value of a frontmatter setting: a boolean, a number or a (possibly quoted) string
const settingValue = (text) => {
  if (text === 'true' || text === 'false') {
    return text === 'true';
  }
  if (/^-?\d+(\.\d+)?$/.test(text)) {
    return Number(text);
  }
  return text.replace(/^"(.*)"$/, '$1');
};

// A fixture without its frontmatter, and the gitGraph settings of the frontmatter (the "key: value" lines indented by
// four spaces under "  gitGraph:") as its site configuration
const fixtureCase = (file, text) => {
  const match = /^---\n([\s\S]*?)\n---\n/.exec(text);
  const result = { name: `fixture-${file.replace(/\.mmd$/, '')}`, spec: `fixture gitgraph/${file}`, text: match ? text.slice(match[0].length) : text };
  if (match) {
    const settings = {};
    let inSection = false;
    for (const row of match[1].split('\n')) {
      if (/^ {2}gitGraph:\s*$/.test(row)) {
        inSection = true;
      } else if (inSection && /^ {4}\S/.test(row)) {
        const [, key, value] = /^ {4}(\w+):\s*(.*)$/.exec(row);
        settings[key] = settingValue(value.trim());
      } else {
        inSection = false;
      }
    }
    if (Object.keys(settings).length > 0) {
      result.site = site(settings);
    }
  }
  return result;
};

// The git graph fixtures of the tests (original), read from the fixture folder
const fixtureFolder = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'test',
  'DemaConsulting.CanvasNet.Mermaid.Tests', 'Fixtures', 'gitgraph');
const fixtures = readdirSync(fixtureFolder).filter((f) => f.endsWith('.mmd')).sort()
  .map((f) => fixtureCase(f, readFileSync(join(fixtureFolder, f), 'utf8')));

// A branch from main with one commit, back on main: the start of many merge and cherry-pick cases
const devBranch = ['commit id: "A"', 'branch dev', 'commit id: "B"', 'checkout main'];
// A merge commit M of dev into main, whose parents are C (main) and B (dev)
const mergeCommit = ['commit id: "A"', 'branch dev', 'commit id: "B"', 'checkout main', 'commit id: "C"', 'merge dev id: "M"'];

export const cases = [
  // ---------------------------------------------------------------------------- diagrams/git/gitGraph.spec.ts
  { name: "spec-handle-a-gitgraph-definition", spec: gitSpec("should handle a gitGraph definition"), text: "gitGraph:\n commit\n" },
  { name: "spec-handle-set-direction-top-to-bottom", spec: gitSpec("should handle set direction top to bottom"), text: "gitGraph TB:\ncommit\n" },
  { name: "spec-handle-set-direction-bottom-to-top", spec: gitSpec("should handle set direction bottom to top"), text: "gitGraph BT:\ncommit\n" },
  { name: "spec-checkout-a-branch", spec: gitSpec("should checkout a branch"), text: "gitGraph:\nbranch new\ncheckout new\n" },
  { name: "spec-switch-a-branch", spec: gitSpec("should switch a branch"), text: "gitGraph:\nbranch new\nswitch new\n" },
  { name: "spec-add-commits-to-checked-out-branch", spec: gitSpec("should add commits to checked out branch"), text: "gitGraph:\nbranch new\ncheckout new\ncommit\ncommit\n" },
  { name: "spec-handle-commit-with-args", spec: gitSpec("should handle commit with args"), text: "gitGraph:\ncommit \"a commit\"\n" },
  { name: "spec-handle-cases-when-merge-is-a-noop", spec: gitSpec("should handle cases when merge is a noop"), text: "gitGraph:\ncommit\nbranch newbranch\ncheckout newbranch\ncommit\ncommit\nmerge main\n" },
  { name: "spec-handle-merge-with-2-parents", spec: gitSpec("should handle merge with 2 parents"), text: "gitGraph:\ncommit\nbranch newbranch\ncheckout newbranch\ncommit\ncommit\ncheckout main\ncommit\nmerge newbranch\n" },
  { name: "spec-generate-an-array-of-known-branches", spec: gitSpec("should generate an array of known branches"), text: "gitGraph:\ncommit\nbranch b1\ncheckout b1\ncommit\ncommit\nbranch b2\n" },
  { name: "spec-handle-a-gitgraph-commit-with-no-params-get-auto", spec: gitSpec("should handle a gitGraph commit with NO params, get auto-generated read-only ID"), text: "gitGraph:\n        commit\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-id-only", spec: gitSpec("should handle a gitGraph commit with custom commit id only"), text: "gitGraph:\n        commit id:\"1111\"\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-tag-only", spec: gitSpec("should handle a gitGraph commit with custom commit tag only"), text: "gitGraph:\n        commit tag:\"test\"\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-type-highlight", spec: gitSpec("should handle a gitGraph commit with custom commit type HIGHLIGHT only"), text: "gitGraph:\n        commit type: HIGHLIGHT\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-type-reverse", spec: gitSpec("should handle a gitGraph commit with custom commit type REVERSE only"), text: "gitGraph:\n        commit type: REVERSE\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-type-normal", spec: gitSpec("should handle a gitGraph commit with custom commit type NORMAL only"), text: "gitGraph:\n        commit type: NORMAL\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-msg-only", spec: gitSpec("should handle a gitGraph commit with custom commit msg only"), text: "gitGraph:\n        commit \"test commit\"\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-msg-key", spec: gitSpec("should handle a gitGraph commit with custom commit \"msg:\" key only"), text: "gitGraph:\n        commit msg: \"test commit\"\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-id-tag", spec: gitSpec("should handle a gitGraph commit with custom commit id, tag  only"), text: "gitGraph:\n        commit id:\"1111\" tag: \"test tag\"\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-type-tag", spec: gitSpec("should handle a gitGraph commit with custom commit type, tag  only"), text: "gitGraph:\n        commit type:HIGHLIGHT tag: \"test tag\"\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-tag-and", spec: gitSpec("should handle a gitGraph commit with custom commit tag and type only"), text: "gitGraph:\n        commit tag: \"test tag\" type:HIGHLIGHT\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-id-type", spec: gitSpec("should handle a gitGraph commit with custom commit id, type and tag only"), text: "gitGraph:\n        commit id:\"1111\" type:REVERSE tag: \"test tag\"\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-commit-id-type-2", spec: gitSpec("should handle a gitGraph commit with custom commit id, type,  tag and msg"), text: "gitGraph:\n        commit id:\"1111\" type:REVERSE tag: \"test tag\" msg:\"test msg\"\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-type-tag-msg", spec: gitSpec("should handle a gitGraph commit with custom  type,tag, msg, commit id,"), text: "gitGraph:\n        commit type:REVERSE tag: \"test tag\" msg: \"test msg\" id: \"1111\"\n\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-tag-msg-commit", spec: gitSpec("should handle a gitGraph commit with custom  tag, msg, commit id, type,"), text: "gitGraph:\n        commit  tag: \"test tag\" msg:\"test msg\" id:\"1111\" type:REVERSE\n        " },
  { name: "spec-handle-a-gitgraph-commit-with-custom-msg-commit-id", spec: gitSpec("should handle a gitGraph commit with custom msg, commit id, type,tag"), text: "gitGraph:\n        commit msg:\"test msg\" id:\"1111\" type:REVERSE tag: \"test tag\"\n        " },
  { name: "spec-handle-3-straight-commits", spec: gitSpec("should handle 3 straight commits"), text: "gitGraph:\n        commit\n        commit\n        commit\n        " },
  { name: "spec-handle-new-branch-creation", spec: gitSpec("should handle new branch creation"), text: "gitGraph:\n        commit\n        branch testBranch\n        " },
  { name: "spec-allow-quoted-branch-names", spec: gitSpec("should allow quoted branch names"), text: "gitGraph:\n        commit\n        branch \"branch\"\n        checkout \"branch\"\n        commit\n        checkout main\n        merge \"branch\"\n        " },
  { name: "spec-allow-characters-in-branch-names", spec: gitSpec("should allow _-./ characters in branch names"), text: "gitGraph:\n        commit\n        branch azAZ_-./test\n        " },
  { name: "spec-allow-branch-names-starting-with-numbers", spec: gitSpec("should allow branch names starting with numbers"), text: "gitGraph:\n        commit\n        %% branch names starting with numbers are not recommended, but are supported by git\n        branch 1.0.1\n        " },
  { name: "spec-allow-branch-names-starting-with-unusual-prefixes", spec: gitSpec("should allow branch names starting with unusual prefixes"), text: "gitGraph:\n        commit\n        %% branch names starting with numbers are not recommended, but are supported by git\n        branch branch01\n        branch checkout02\n        branch cherry-pick03\n        branch branch/example-branch\n        branch merge/test_merge\n        %% single character branch name\n        branch A\n        " },
  { name: "spec-handle-new-branch-checkout", spec: gitSpec("should handle new branch checkout"), text: "gitGraph:\n        commit\n        branch testBranch\n        checkout testBranch\n        " },
  { name: "spec-handle-new-branch-checkout-with-order", spec: gitSpec("should handle new branch checkout with order"), text: "gitGraph:\n        commit\n        branch test1 order: 3\n        branch test2 order: 2\n        branch test3 order: 1\n        " },
  { name: "spec-handle-new-branch-checkout-with-and-without-order", spec: gitSpec("should handle new branch checkout with and without order"), text: "gitGraph:\n        commit\n        branch test1 order: 1\n        branch test2\n        branch test3\n        " },
  { name: "spec-handle-new-branch-checkout-commit", spec: gitSpec("should handle new branch checkout & commit"), text: "gitGraph:\n        commit\n        branch testBranch\n        checkout testBranch\n        commit\n        " },
  { name: "spec-handle-new-branch-checkout-commit-and-merge", spec: gitSpec("should handle new branch checkout & commit and merge"), text: "gitGraph:\n        commit\n        branch testBranch\n        checkout testBranch\n        commit\n        commit\n        checkout main\n        merge testBranch\n        " },
  { name: "spec-handle-new-branch-switch", spec: gitSpec("should handle new branch switch"), text: "gitGraph:\n        commit\n        branch testBranch\n        switch testBranch\n        " },
  { name: "spec-handle-new-branch-switch-commit", spec: gitSpec("should handle new branch switch & commit"), text: "gitGraph:\n        commit\n        branch testBranch\n        switch testBranch\n        commit\n        " },
  { name: "spec-handle-new-branch-switch-commit-and-merge", spec: gitSpec("should handle new branch switch & commit and merge"), text: "gitGraph:\n        commit\n        branch testBranch\n        switch testBranch\n        commit\n        commit\n        switch main\n        merge testBranch\n        " },
  { name: "spec-handle-merge-tags", spec: gitSpec("should handle merge tags"), text: "gitGraph:\n        commit\n        branch testBranch\n        checkout testBranch\n        commit\n        checkout main\n        merge testBranch tag: \"merge-tag\"\n        " },
  { name: "spec-handle-merge-with-custom-ids-tags-and-type", spec: gitSpec("should handle merge with custom ids, tags and type"), text: "gitGraph:\n          commit\n          branch testBranch\n          checkout testBranch\n          commit\n          checkout main\n          %% Merge Tag and ID\n          merge testBranch tag: \"merge-tag\" id: \"2-222\"\n          branch testBranch2\n          checkout testBranch2\n          commit\n          checkout main\n          %% Merge ID and Tag (reverse order)\n          merge testBranch2 id: \"4-444\" tag: \"merge-tag2\" type:HIGHLIGHT\n          branch testBranch3\n          checkout testBranch3\n          commit\n          checkout main\n          %% just Merge ID\n          merge testBranch3 id: \"6-666\"\n        " },
  { name: "spec-support-cherry-picking-commits", spec: gitSpec("should support cherry-picking commits"), text: "gitGraph\n        commit id: \"ZERO\"\n        branch develop\n        commit id:\"A\"\n        checkout main\n        cherry-pick id:\"A\"\n        " },
  { name: "spec-support-cherry-picking-commits-with-custom-tag", spec: gitSpec("should support cherry-picking commits with custom tag"), text: "gitGraph\n        commit id: \"ZERO\"\n        branch develop\n        commit id:\"A\"\n        checkout main\n        cherry-pick id:\"A\" tag:\"MyTag\"\n        " },
  { name: "spec-support-cherry-picking-commits-with-no-tag", spec: gitSpec("should support cherry-picking commits with no tag"), text: "gitGraph\n        commit id: \"ZERO\"\n        branch develop\n        commit id:\"A\"\n        checkout main\n        cherry-pick id:\"A\" tag:\"\"\n        " },
  { name: "spec-support-cherry-picking-of-merge-commits", spec: gitSpec("should support cherry-picking of merge commits"), text: "gitGraph\n        commit id: \"ZERO\"\n        branch feature\n        branch release\n        checkout feature\n        commit id: \"A\"\n        commit id: \"B\"\n        checkout main\n        merge feature id: \"M\"\n        checkout release\n        cherry-pick id: \"M\" parent:\"B\"\n        " },
  { name: "spec-support-cherry-picking-of-merge-commits-with-tag", spec: gitSpec("should support cherry-picking of merge commits with tag"), text: "gitGraph\n        commit id: \"ZERO\"\n        branch feature\n        branch release\n        checkout feature\n        commit id: \"A\"\n        commit id: \"B\"\n        checkout main\n        merge feature id: \"M\"\n        checkout release\n        cherry-pick id: \"M\" parent:\"ZERO\" tag: \"v1.0\"\n        " },
  { name: "spec-support-cherry-picking-of-merge-commits-with-additional-commit", spec: gitSpec("should support cherry-picking of merge commits with additional commit"), text: "gitGraph\n        commit id: \"ZERO\"\n        branch feature\n        branch release\n        checkout feature\n        commit id: \"A\"\n        commit id: \"B\"\n        checkout main\n        merge feature id: \"M\"\n        checkout release\n        commit id: \"C\"\n        cherry-pick id: \"M\" tag: \"v2.1:ZERO\" parent:\"ZERO\"\n        commit id: \"D\"\n        " },
  { name: "spec-support-cherry-picking-of-merge-commits-with-empty-tag", spec: gitSpec("should support cherry-picking of merge commits with empty tag"), text: "gitGraph\n        commit id: \"ZERO\"\n        branch feature\n        branch release\n        checkout feature\n        commit id: \"A\"\n        commit id: \"B\"\n        checkout main\n        merge feature id: \"M\"\n        checkout release\n        commit id: \"C\"\n        cherry-pick id:\"M\" parent: \"ZERO\" tag:\"\"\n        commit id: \"D\"\n        cherry-pick id:\"M\" tag:\"\" parent: \"B\"\n        " },
  { name: "spec-fail-cherry-picking-of-merge-commits-if-the-parent", spec: gitSpec("should fail cherry-picking of merge commits if the parent of merge commits is not specified"), text: "gitGraph\n        commit id: \"ZERO\"\n        branch feature\n        branch release\n        checkout feature\n        commit id: \"A\"\n        commit id: \"B\"\n        checkout main\n        merge feature id: \"M\"\n        checkout release\n        commit id: \"C\"\n        cherry-pick id:\"M\"\n        " },
  { name: "spec-fail-cherry-picking-of-merge-commits-when-the-parent", spec: gitSpec("should fail cherry-picking of merge commits when the parent provided is not an immediate parent of cherry picked commit"), text: "gitGraph\n        commit id: \"ZERO\"\n        branch feature\n        branch release\n        checkout feature\n        commit id: \"A\"\n        commit id: \"B\"\n        checkout main\n        merge feature id: \"M\"\n        checkout release\n        commit id: \"C\"\n        cherry-pick id:\"M\" parent: \"A\"\n        " },
  { name: "spec-throw-error-when-try-to-branch-existing-branch-main", spec: gitSpec("should throw error when try to branch existing branch: main"), text: "gitGraph\n        commit\n        branch testBranch\n        commit\n        branch main\n        commit\n        checkout main\n        merge testBranch\n        " },
  { name: "spec-throw-error-when-try-to-branch-existing-branch-testbranch", spec: gitSpec("should throw error when try to branch existing branch: testBranch"), text: "gitGraph\n        commit\n        branch testBranch\n        commit\n        branch testBranch\n        commit\n        checkout main\n        merge testBranch\n        " },
  { name: "spec-throw-error-when-try-to-checkout-unknown-branch-testbranch", spec: gitSpec("should throw error when try to checkout unknown branch: testBranch"), text: "gitGraph\n        commit\n        checkout testBranch\n        commit\n        branch testBranch\n        commit\n        checkout main\n        merge testBranch\n        " },
  { name: "spec-throw-error-when-trying-to-merge-when-current-branch", spec: gitSpec("should throw error when trying to merge, when current branch has no commits"), text: "gitGraph\n        merge testBranch\n        commit\n        checkout testBranch\n        commit\n        branch testBranch\n        commit\n        checkout main\n        merge testBranch\n        " },
  { name: "spec-throw-error-when-trying-to-merge-unknown-branch", spec: gitSpec("should throw error when trying to merge unknown branch"), text: "gitGraph\n        commit\n        merge testBranch\n        commit\n        checkout testBranch\n        commit\n        branch testBranch\n        commit\n        checkout main\n        merge testBranch\n        " },
  { name: "spec-throw-error-when-trying-to-merge-branch-to-itself", spec: gitSpec("should throw error when trying to merge branch to itself"), text: "gitGraph\n        commit\n        branch testBranch\n        merge testBranch\n        " },
  { name: "spec-throw-error-when-using-existing-id-as-merge-id", spec: gitSpec("should throw error when using existing id as merge ID"), text: "gitGraph\n        commit id: \"1-111\"\n        branch testBranch\n        commit id: \"2-222\"\n        commit id: \"3-333\"\n        checkout main\n        merge testBranch id: \"1-111\"\n        " },
  { name: "spec-throw-error-when-trying-to-merge-branches-having-same", spec: gitSpec("should throw error when trying to merge branches having same heads"), text: "gitGraph\n        commit\n        branch testBranch\n        checkout main\n        merge testBranch\n        " },
  { name: "spec-throw-error-when-trying-to-merge-branch-which-has", spec: gitSpec("should throw error when trying to merge branch which has no commits"), text: "gitGraph\n        branch test1\n\n        checkout main\n        commit\n        merge test1\n        " },
  { name: "spec-handle-a-title-and-a-description-accdescr", spec: gitSpec("should handle a title and a description (accDescr)"), text: "gitGraph:\n          accTitle: This is a title\n          accDescr: This is a description\n        commit\n        " },
  { name: "spec-handle-a-title-and-a-multiline-description-accdescr", spec: gitSpec("should handle a title and a multiline description (accDescr)"), text: "gitGraph:\n          accTitle: This is a title\n          accDescr {\n            This is a description\n            using multiple lines\n          }\n        commit\n        " },
  { name: "spec-work-with-custom-commit-id-or-branch-name-proto", spec: gitSpec("should work with custom commit id or branch name __proto__"), text: "gitGraph\n        commit id:\"__proto__\"\n        branch __proto__\n        checkout __proto__\n        commit\n        checkout main\n        merge __proto__\n        " },
  { name: "spec-work-with-custom-commit-id-or-branch-name-constructor", spec: gitSpec("should work with custom commit id or branch name constructor"), text: "gitGraph\n        commit id:\"constructor\"\n        branch constructor\n        checkout constructor\n        commit\n        checkout main\n        merge constructor\n        " },
  { name: "spec-log-a-warning-when-two-commits-have-the-same", spec: gitSpec("should log a warning when two commits have the same ID"), text: "gitGraph\n     commit id:\"initial commit\"\n     commit id:\"work on first release\"\n     commit id:\"design freeze from here\"\n     branch v1-rc\n     checkout v1-rc\n     commit id:\"bugfix 1\"\n     commit id:\"bigfix 2\" tag:\"v1.0.1\"\n     branch FORK-v1.0-MDR\n     checkout FORK-v1.0-MDR\n     commit id:\"working on MDR\"\n     checkout v1-rc\n     commit id:\"minor design changes for MDR\" tag:\"v1.0.2\"\n     checkout FORK-v1.0-MDR\n     merge v1-rc\n     checkout main\n     commit id:\"new feature for v1.1…\"\n     checkout FORK-v1.0-MDR\n     commit id:\"working on MDR\"\n     commit id:\"finishing MDR\"\n     branch v1.0-MDR\n     checkout v1.0-MDR\n     commit id:\"brush up release\" tag:\"v1.0.2-MDR\"\n     checkout v1-rc\n     commit id:\"bugfix without MDR\"\n     checkout main\n     commit id:\"work on v1.1\"\n  " },

  // ----------------------------------------------------------------------- @mermaid-js/parser tests/gitGraph.test.ts
  { name: "parser-parse-a-simple-commit", spec: parserSpec("should parse a simple commit"), text: "gitGraph\n  commit\n" },
  { name: "parser-parse-multiple-commits", spec: parserSpec("should parse multiple commits"), text: "gitGraph\n  commit\n  commit\n  commit\n" },
  { name: "parser-parse-commits-with-all-properties", spec: parserSpec("should parse commits with all properties"), text: "gitGraph\n  commit id:\"1\" msg:\"Fix bug\" tag:\"v1.2\" type:NORMAL\n" },
  { name: "parser-handle-commit-messages-with-special-characters", spec: parserSpec("should handle commit messages with special characters"), text: "gitGraph\n  commit msg:\"Fix issue #123: Handle errors\"\n" },
  { name: "parser-parse-commits-with-only-a-message-and-no-other", spec: parserSpec("should parse commits with only a message and no other properties"), text: "gitGraph\n  commit msg:\"Initial release\"\n" },
  { name: "parser-ignore-malformed-properties-and-not-break-parsing", spec: parserSpec("should ignore malformed properties and not break parsing"), text: "gitGraph\n  commit id:\"2\" msg:\"Malformed commit\" oops:\"ignored\"\n" },
  { name: "parser-parse-multiple-commits-with-different-types", spec: parserSpec("should parse multiple commits with different types"), text: "gitGraph\n  commit type:NORMAL\n  commit type:REVERSE\n" },
  { name: "parser-parse-a-branch-with-a-simple-name", spec: parserSpec("should parse a branch with a simple name"), text: "gitGraph\n commit\n commit\n branch master\n" },
  { name: "parser-parse-a-branch-name-starting-with-numbers", spec: parserSpec("should parse a branch name starting with numbers"), text: "gitGraph\n commit\n branch 1.0.1\n" },
  { name: "parser-parse-a-branch-with-an-order-property", spec: parserSpec("should parse a branch with an order property"), text: "gitGraph\n commit\n  branch feature order:1\n" },
  { name: "parser-handle-branch-names-with-special-characters", spec: parserSpec("should handle branch names with special characters"), text: "gitGraph\n  branch feature/test-branch\n" },
  { name: "parser-parse-branches-with-hyphens-and-underscores", spec: parserSpec("should parse branches with hyphens and underscores"), text: "gitGraph\n  branch my-feature_branch\n" },
  { name: "parser-correctly-handle-branch-without-order-property", spec: parserSpec("should correctly handle branch without order property"), text: "gitGraph\n  branch feature\n" },
  { name: "parser-parse-a-merge-with-a-branch-name", spec: parserSpec("should parse a merge with a branch name"), text: "gitGraph\n merge master\n" },
  { name: "parser-handle-merges-with-additional-properties", spec: parserSpec("should handle merges with additional properties"), text: "gitGraph\n  merge feature id:\"m1\" tag:\"release\" type:HIGHLIGHT\n" },
  { name: "parser-parse-merge-without-any-properties", spec: parserSpec("should parse merge without any properties"), text: "gitGraph\n  merge feature\n" },
  { name: "parser-ignore-malformed-properties-in-merge-statements", spec: parserSpec("should ignore malformed properties in merge statements"), text: "gitGraph\n  merge feature random:\"ignored\"\n" },
  { name: "parser-parse-a-checkout-to-a-named-branch", spec: parserSpec("should parse a checkout to a named branch"), text: "gitGraph\n commit id:\"1\"\n branch develop\n branch fun\n checkout develop\n" },
  { name: "parser-parse-checkout-to-branches-with-complex-names", spec: parserSpec("should parse checkout to branches with complex names"), text: "gitGraph\n  checkout hotfix-123\n" },
  { name: "parser-parse-checkouts-with-hyphens-and-numbers", spec: parserSpec("should parse checkouts with hyphens and numbers"), text: "gitGraph\n  checkout release-2021\n" },
  { name: "parser-parse-cherry-picking-with-a-commit-id", spec: parserSpec("should parse cherry-picking with a commit id"), text: "gitGraph\n commit id:\"123\" commit id:\"321\" cherry-pick id:\"123\"\n" },
  { name: "parser-parse-cherry-picking-with-multiple-properties", spec: parserSpec("should parse cherry-picking with multiple properties"), text: "gitGraph\n  cherry-pick id:\"123\" tag:\"urgent\" parent:\"100\"\n" },
  { name: "parser-parse-accessibility-titles", spec: parserSpec("should parse accessibility titles"), text: "gitGraph\n  accTitle: Accessible Graph\n  commit\n" },
  { name: "parser-parse-multiline-accessibility-descriptions", spec: parserSpec("should parse multiline accessibility descriptions"), text: "gitGraph\n  accDescr {\n    Detailed description\n    across multiple lines\n  }\n  commit\n" },
  { name: "parser-correctly-parse-a-complex-graph-with-various-elements", spec: parserSpec("should correctly parse a complex graph with various elements"), text: "\n        gitGraph TB:\n        accTitle: Complex Example\n        commit id:\"init\" type:NORMAL\n        branch feature\n        commit id:\"feat1\" msg:\"Add feature\"\n        checkout main\n        merge feature tag:\"v1.0\"\n        cherry-pick id:\"feat1\" tag:\"critical fix\"\n      " },
  { name: "parser-report-errors-for-unknown-properties-in-commit", spec: parserSpec("should report errors for unknown properties in commit"), text: "gitGraph\n  commit unknown:\"oops\"\n" },
  { name: "parser-report-errors-for-invalid-branch-order", spec: parserSpec("should report errors for invalid branch order"), text: "gitGraph\n  branch feature order:xyz\n" },

  // ------------------------------------------------------------------------------------------------- original
  // The header: the four forms of the entry rule, the suffix of gitGraph, and what may follow the header
  { name: 'header-colon', spec: 'original', text: lines('gitGraph:', '  commit') },
  { name: 'header-space-colon', spec: 'original', text: lines('gitGraph :', '  commit') },
  { name: 'header-left-to-right-colon', spec: 'original', text: lines('gitGraph LR:', '  commit') },
  { name: 'header-left-to-right-space-colon', spec: 'original', text: lines('gitGraph LR :', '  commit') },
  { name: 'header-top-to-bottom-tab', spec: 'original', text: lines('gitGraph\tTB:', '  commit') },
  { name: 'header-bottom-to-top-comment', spec: 'original', text: lines('gitGraph BT: %% the direction', '  commit') },
  { name: 'header-same-line-statement', spec: 'original', text: lines('gitGraph commit', '  commit') },
  { name: 'header-colon-same-line-statement', spec: 'original', text: 'gitGraph: commit id: "A"' },
  { name: 'header-percent', spec: 'original', text: lines('gitGraph%% comment', '  commit') },
  { name: 'header-comment', spec: 'original', text: lines('gitGraph %% comment', '  commit') },
  { name: 'header-leading-blank-lines', spec: 'original', text: lines('', '', 'gitGraph', '  commit') },
  { name: 'header-indented', spec: 'original', text: lines('   gitGraph', '  commit') },
  { name: 'header-only', spec: 'original', text: 'gitGraph' },
  { name: 'header-same-line-title', spec: 'original', text: lines('gitGraph title On the header line', '  commit') },
  { name: 'header-crlf', spec: 'original', text: 'gitGraph\r\n  commit\r\n  branch dev\r\n  commit\r\n' },
  { name: 'error-header-glued', spec: 'original', text: lines('gitGraphx', '  commit') },
  { name: 'error-header-bottom-to-top-no-colon', spec: 'original', text: lines('gitGraph BT', '  commit') },
  { name: 'error-header-left-to-right-no-colon', spec: 'original', text: lines('gitGraph LR', '  commit') },
  { name: 'error-header-top-down', spec: 'original', text: lines('gitGraph TD:', '  commit') },
  { name: 'error-header-lowercase-direction', spec: 'original', text: lines('gitGraph tb:', '  commit') },
  { name: 'error-header-string', spec: 'original', text: lines('gitGraph "name"', '  commit') },
  { name: 'error-header-colon-word', spec: 'original', text: lines('gitGraph:x', '  commit') },
  { name: 'error-header-direction-colon-word', spec: 'original', text: lines('gitGraph LR:x', '  commit') },
  { name: 'error-header-double-colon', spec: 'original', text: lines('gitGraph::', '  commit') },
  { name: 'error-header-twice', spec: 'original', text: lines('gitGraph', 'gitGraph', '  commit') },
  { name: 'error-header-direction-on-next-line', spec: 'original', text: lines('gitGraph', '  TB:', '  commit') },

  // The lexer: keywords end where a longer reference would, the TITLE and INT terminals come before REFERENCE
  { name: 'reference-commits', spec: 'original', text: graph('commit', 'branch commits', 'commit') },
  { name: 'reference-cherry-picked', spec: 'original', text: graph('commit', 'branch cherry-picked', 'commit') },
  { name: 'reference-direction-prefix', spec: 'original', text: graph('commit', 'branch LRx', 'checkout main') },
  { name: 'reference-commit-number', spec: 'original', text: graph('commit', 'branch commit2', 'commit') },
  { name: 'reference-merged', spec: 'original', text: graph('commit', 'branch merged', 'commit') },
  { name: 'reference-type-prefix', spec: 'original', text: graph('commit', 'branch NORMALS', 'commit') },
  { name: 'reference-header-prefix', spec: 'original', text: graph('commit', 'branch gitGraphs', 'commit') },
  { name: 'reference-title-inside', spec: 'original', text: graph('commit', 'branch my_title', 'commit') },
  { name: 'reference-path', spec: 'original', text: graph('commit', 'branch feature/x.y-z', 'commit') },
  { name: 'reference-trailing-dash', spec: 'original', text: graph('commit', 'branch a-', 'commit') },
  { name: 'reference-underscore-first', spec: 'original', text: graph('commit', 'branch _private', 'commit') },
  { name: 'reference-version', spec: 'original', text: graph('commit', 'branch 1.0', 'commit') },
  { name: 'reference-quoted-keyword', spec: 'original', text: graph('branch "commit"', 'commit') },
  { name: 'error-reference-keyword-commit', spec: 'original', text: graph('commit', 'branch commit') },
  { name: 'error-reference-keyword-direction', spec: 'original', text: graph('branch LR') },
  { name: 'error-reference-keyword-type', spec: 'original', text: graph('branch NORMAL') },
  { name: 'error-reference-keyword-header', spec: 'original', text: graph('branch gitGraph') },
  { name: 'error-reference-title-prefix', spec: 'original', text: graph('commit', 'branch titles') },
  { name: 'error-reference-accent', spec: 'original', text: graph('commit', 'branch café') },
  { name: 'error-reference-trailing-dot', spec: 'original', text: graph('commit', 'branch a.') },
  { name: 'error-reference-number', spec: 'original', text: graph('commit', 'branch 10') },
  { name: 'error-reference-zero-letter', spec: 'original', text: graph('commit', 'branch 0x') },
  { name: 'error-reference-decimal', spec: 'original', text: graph('commit', 'branch 10.1') },
  { name: 'error-reference-zero-decimal', spec: 'original', text: graph('commit', 'branch 0.5') },
  { name: 'error-statement-glued', spec: 'original', text: graph('commitx') },
  { name: 'error-statement-glued-next-line', spec: 'original', text: graph('commit', 'commit2') },

  // The branch order: an INT, sorted stably with the unordered branches at 0
  { name: 'order-sort', spec: 'original', text: graph('branch dev order: 2', 'branch b order: 1', 'branch c', 'branch d order: 0') },
  { name: 'order-ties-stable', spec: 'original', text: graph('commit', 'branch e order: 1', 'branch d order: 1', 'branch c', 'branch b order: 1', 'branch a') },
  { name: 'order-no-spaces', spec: 'original', text: graph('commit', 'branch dev order:3', 'commit') },
  { name: 'order-many-spaces', spec: 'original', text: graph('commit', 'branch   dev   order:   3   ', 'commit') },
  { name: 'order-huge', spec: 'original', text: graph('commit', 'branch dev order: 99999999999999999999999', 'branch b order: 7') },
  { name: 'order-infinite', spec: 'original', text: graph('commit', `branch c order: 1${'0'.repeat(400)}`, 'branch b order: 3', `branch a order: 2${'0'.repeat(400)}`, 'branch d') },
  { name: 'error-order-leading-zero', spec: 'original', text: graph('commit', 'branch dev order: 02') },
  { name: 'error-order-negative', spec: 'original', text: graph('commit', 'branch dev order: -1') },
  { name: 'error-order-decimal', spec: 'original', text: graph('commit', 'branch dev order: 1.5') },
  { name: 'error-order-missing', spec: 'original', text: graph('commit', 'branch dev order:', 'commit') },
  { name: 'error-order-twice', spec: 'original', text: graph('commit', 'branch dev order: 1 order: 2') },
  { name: 'error-order-word', spec: 'original', text: graph('commit', 'branch dev order: high') },
  { name: 'error-order-on-commit', spec: 'original', text: graph('commit order: 1') },

  // Commits: the options in any order, the last id, message and type win, the tags add up
  { name: 'commit-options-glued', spec: 'original', text: graph('commit id:"a"tag:"b"type:HIGHLIGHT') },
  { name: 'commit-id-last-wins', spec: 'original', text: graph('commit id: "x" id: "y"') },
  { name: 'commit-type-last-wins', spec: 'original', text: graph('commit type: REVERSE type: HIGHLIGHT') },
  { name: 'commit-message-last-wins', spec: 'original', text: graph('commit msg: "first" "second"') },
  { name: 'commit-tags-add-up', spec: 'original', text: graph('commit tag: "a" tag:"b"tag:\'c\'') },
  { name: 'commit-every-option-twice', spec: 'original', text: graph('commit msg: "m" id: "x" id: "y" type: REVERSE type: HIGHLIGHT tag: "a" tag: "b"') },
  { name: 'commit-empty-id', spec: 'original', text: graph('commit id: ""', 'commit id: ""') },
  { name: 'commit-bare-message', spec: 'original', text: graph('commit "hello"') },
  { name: 'commit-single-quotes', spec: 'original', text: graph("commit id: 'single' msg: 'it\\'s'") },
  { name: 'commit-escapes', spec: 'original', text: graph('commit id: "esc\\"aped\\n" msg: "tab\\there"') },
  { name: 'commit-id-spaces', spec: 'original', text: graph('commit id: "a b"', 'commit id: "  padded  "') },
  { name: 'commit-id-unicode', spec: 'original', text: graph('commit id: "Café ☕"') },
  { name: 'commit-empty-tag', spec: 'original', text: graph('commit tag: ""', 'commit tag: "" tag: "x"') },
  { name: 'commit-string-over-two-lines', spec: 'original', text: graph('commit id: "first', 'second"') },
  { name: 'commit-generated-ids', spec: 'original', text: graph(...Array.from({ length: 24 }, () => 'commit')) },
  { name: 'commit-duplicate-id-replaced', spec: 'original', text: graph('commit id: "A"', 'commit id: "B"', 'commit id: "A"', 'commit') },
  { name: 'commit-duplicate-id-other-branch', spec: 'original', text: graph('commit id: "A"', 'branch dev', 'commit id: "A"', 'checkout main', 'commit') },
  { name: 'error-commit-id-unquoted', spec: 'original', text: graph('commit id: foo') },
  { name: 'error-commit-id-no-colon', spec: 'original', text: graph('commit id "a"') },
  { name: 'error-commit-trailing-word', spec: 'original', text: graph('commit id: "a" foo') },
  { name: 'error-commit-type-lowercase', spec: 'original', text: graph('commit type: normal') },
  { name: 'error-commit-type-merge', spec: 'original', text: graph('commit type: MERGE') },
  { name: 'error-commit-type-missing', spec: 'original', text: graph('commit type:') },
  { name: 'error-commit-message-missing', spec: 'original', text: graph('commit msg:') },
  { name: 'error-commit-tag-unquoted', spec: 'original', text: graph('commit tag: v1') },
  { name: 'error-commit-two-on-a-line', spec: 'original', text: graph('commit commit') },
  { name: 'error-commit-semicolon', spec: 'original', text: graph('commit;') },
  { name: 'error-commit-parent', spec: 'original', text: graph('commit parent: "A"') },

  // Branches and checkouts
  { name: 'branch-before-commit', spec: 'original', text: graph('branch dev', 'commit', 'checkout main', 'commit') },
  { name: 'branch-quoted-spaces', spec: 'original', text: graph('commit', 'branch "my feature"', 'commit', 'checkout main', 'checkout "my feature"') },
  { name: 'branch-single-quotes', spec: 'original', text: graph('commit', "branch 'dev'", 'commit', 'checkout dev') },
  { name: 'branch-old-head', spec: 'original', text: graph('commit id: "A"', 'branch dev', 'checkout main', 'commit id: "B"', 'checkout dev', 'commit id: "C"') },
  { name: 'checkout-switch-quoted', spec: 'original', text: graph('commit', 'branch dev', 'switch "main"', 'commit', 'switch dev') },
  { name: 'checkout-current', spec: 'original', text: graph('commit', 'checkout main', 'commit') },
  { name: 'checkout-sanitized', spec: 'original', text: graph('commit', 'branch "a<b"', 'checkout main', 'checkout "a<b"', 'commit') },
  { name: 'error-branch-existing-main', spec: 'original', text: graph('commit', 'branch main') },
  { name: 'error-branch-existing', spec: 'original', text: graph('commit', 'branch dev', 'commit', 'branch dev') },
  { name: 'error-branch-existing-after-sanitizing', spec: 'original', text: graph('commit', 'branch "a<b"', 'branch "a&lt;b"') },
  { name: 'error-branch-missing-name', spec: 'original', text: graph('branch', 'commit') },
  { name: 'error-branch-two-names', spec: 'original', text: graph('branch a b') },
  { name: 'error-checkout-unknown', spec: 'original', text: graph('commit', 'checkout nope') },
  { name: 'error-switch-unknown', spec: 'original', text: graph('commit', 'switch "no such branch"') },
  { name: 'error-checkout-missing', spec: 'original', text: graph('checkout') },
  { name: 'error-checkout-number', spec: 'original', text: graph('checkout 10') },

  // Merges: the options, the composed message, the order of the checks of each error
  { name: 'merge-options', spec: 'original', text: graph(...devBranch, 'merge dev id: "M" type: REVERSE tag: "v"') },
  { name: 'merge-options-reordered', spec: 'original', text: graph(...devBranch, 'merge dev tag: "a" type: HIGHLIGHT id: "M" tag: "b" type: NORMAL') },
  { name: 'merge-quoted-branch', spec: 'original', text: graph(...devBranch, 'merge "dev"') },
  { name: 'merge-again-unchanged', spec: 'original', text: graph(...devBranch, 'merge dev', 'merge dev') },
  { name: 'merge-tags-not-sanitized', spec: 'original', text: graph(...devBranch, 'merge dev tag: "a<b>=c"') },
  { name: 'merge-id-sanitized', spec: 'original', text: graph(...devBranch, 'merge dev id: "x<y=z"') },
  { name: 'merge-empty-id', spec: 'original', text: graph(...devBranch, 'merge dev id: ""') },
  { name: 'merge-main-into-branch', spec: 'original', text: graph('commit', 'branch dev', 'commit', 'checkout main', 'commit', 'checkout dev', 'merge main', 'commit') },
  { name: 'merge-head-of-other-branch', spec: 'original', text: graph('commit id: "A"', 'branch dev', 'commit id: "B"', 'branch feat', 'checkout main', 'commit id: "C"', 'merge feat') },
  { name: 'error-merge-into-itself', spec: 'original', text: graph('commit', 'branch dev', 'checkout main', 'checkout dev', 'merge main') },
  { name: 'error-merge-same-branch', spec: 'original', text: graph('commit', 'merge main') },
  { name: 'error-merge-current-without-commits', spec: 'original', text: graph('branch dev', 'commit', 'checkout main', 'merge dev') },
  { name: 'error-merge-unknown', spec: 'original', text: graph('commit', 'merge nope') },
  { name: 'error-merge-other-without-commits', spec: 'original', text: graph('branch dev', 'checkout main', 'commit', 'merge dev') },
  { name: 'error-merge-same-head', spec: 'original', text: graph('commit', 'branch dev', 'checkout main', 'merge dev') },
  { name: 'error-merge-existing-id', spec: 'original', text: graph(...devBranch, 'merge dev id: "A"') },
  { name: 'error-merge-existing-id-after-sanitizing', spec: 'original', text: graph('commit id: "x<y"', 'branch dev', 'commit', 'checkout main', 'merge dev id: "x<y"') },
  { name: 'error-merge-empty-repository', spec: 'original', text: graph('merge main') },
  { name: 'error-merge-missing-branch', spec: 'original', text: graph('commit', 'merge') },
  { name: 'error-merge-two-branches', spec: 'original', text: graph(...devBranch, 'merge dev main') },
  { name: 'error-merge-type-lowercase', spec: 'original', text: graph(...devBranch, 'merge dev type: reverse') },
  { name: 'error-merge-message', spec: 'original', text: graph(...devBranch, 'merge dev msg: "m"') },

  // Cherry-picks: the default tag, empty tags, merge commits and their parent, the order of the checks of each error
  { name: 'cherry-pick-default-tag', spec: 'original', text: graph(...devBranch, 'cherry-pick id: "B"') },
  { name: 'cherry-pick-custom-tags', spec: 'original', text: graph(...devBranch, 'cherry-pick id: "B" tag: "x" tag: "y"') },
  { name: 'cherry-pick-empty-tag', spec: 'original', text: graph(...devBranch, 'cherry-pick id: "B" tag: ""') },
  { name: 'cherry-pick-empty-tag-among-others', spec: 'original', text: graph(...devBranch, 'cherry-pick id: "B" tag: "x" tag: "" tag: "y"') },
  { name: 'cherry-pick-sanitized-tag', spec: 'original', text: graph(...devBranch, 'cherry-pick id: "B" tag: "a<b=c"') },
  { name: 'cherry-pick-twice', spec: 'original', text: graph(...devBranch, 'cherry-pick id: "B"', 'cherry-pick id: "B"') },
  { name: 'cherry-pick-head-of-other-branch', spec: 'original', text: graph('commit id: "A"', 'branch dev', 'checkout main', 'commit id: "B"', 'checkout dev', 'cherry-pick id: "B"') },
  // (2-eb28011 is the id of the first commit without one, see seeded-random.mjs)
  { name: 'cherry-pick-of-cherry-pick', spec: 'original', text: graph('commit id: "A"', 'branch dev', 'commit id: "B" msg: "fix"', 'checkout main', 'cherry-pick id: "B"', 'checkout dev', 'commit', 'cherry-pick id: "2-eb28011"') },
  { name: 'cherry-pick-merge-with-parent', spec: 'original', text: graph(...mergeCommit, 'checkout dev', 'cherry-pick id: "M" parent: "C"') },
  { name: 'cherry-pick-merge-with-parent-and-tag', spec: 'original', text: graph(...mergeCommit, 'branch rel', 'commit', 'cherry-pick id: "M" parent: "B" tag: "picked"') },
  { name: 'cherry-pick-parent-of-normal-commit', spec: 'original', text: graph('commit id: "A"', 'branch dev', 'commit id: "B"', 'checkout main', 'cherry-pick id: "B" parent: "A"') },
  { name: 'cherry-pick-options-glued', spec: 'original', text: graph(...mergeCommit, 'checkout dev', 'cherry-pick parent:"B"id:"Q"id:"M"tag:"t"') },
  { name: 'error-cherry-pick-no-id', spec: 'original', text: graph('commit', 'cherry-pick') },
  { name: 'error-cherry-pick-empty-id', spec: 'original', text: graph('commit', 'cherry-pick id: ""') },
  { name: 'error-cherry-pick-unknown', spec: 'original', text: graph('commit id: "A"', 'cherry-pick id: "Z"') },
  { name: 'error-cherry-pick-parent-not-immediate', spec: 'original', text: graph(...mergeCommit, 'checkout dev', 'cherry-pick id: "M" parent: "A"') },
  { name: 'error-cherry-pick-parent-before-same-branch', spec: 'original', text: graph('commit id: "A"', 'commit id: "B"', 'cherry-pick id: "B" parent: "Q"') },
  { name: 'error-cherry-pick-merge-without-parent', spec: 'original', text: graph(...mergeCommit, 'checkout dev', 'cherry-pick id: "M"') },
  { name: 'error-cherry-pick-same-branch', spec: 'original', text: graph('commit id: "A"', 'cherry-pick id: "A"') },
  { name: 'error-cherry-pick-current-without-commits', spec: 'original', text: graph('branch dev', 'checkout main', 'commit id: "A"', 'checkout dev', 'cherry-pick id: "A"') },
  { name: 'error-cherry-pick-type', spec: 'original', text: graph(...devBranch, 'cherry-pick id: "B" type: NORMAL') },
  { name: 'error-cherry-pick-id-unquoted', spec: 'original', text: graph(...devBranch, 'cherry-pick id: B') },

  // Titles and accessibility texts: the value converters, the %% look-ahead, the last one wins
  { name: 'title-spaces-collapsed', spec: 'original', text: graph('  title   A   B\t\tC  ', 'commit') },
  { name: 'title-comment', spec: 'original', text: graph('title A %% not part of it', 'commit') },
  { name: 'title-empty', spec: 'original', text: graph('title', 'commit') },
  { name: 'title-twice-last-wins', spec: 'original', text: graph('title First', 'title Second', 'commit') },
  { name: 'title-after-statements', spec: 'original', text: graph('commit', 'title Late') },
  { name: 'title-html', spec: 'original', text: graph('title <b>Bold</b> & <script>x</script>', 'commit') },
  { name: 'acc-title-and-description', spec: 'original', text: graph('accTitle:   a   title  ', 'accDescr:   one   two  ', 'commit') },
  { name: 'acc-description-multiline', spec: 'original', text: graph('accDescr {', '      first   line', '', '      second line   ', '   }', 'commit') },
  { name: 'acc-all-three-together', spec: 'original', text: graph('accTitle: x', 'accDescr: y', 'title t', '', "commit id: 'A' msg: 'm'") },
  { name: 'frontmatter-title', spec: 'original', text: lines('---', 'title: From the frontmatter', '---', 'gitGraph', '  commit') },
  { name: 'frontmatter-and-title', spec: 'original', text: lines('---', 'title: From the frontmatter', '---', 'gitGraph', '  title From the text', '  commit') },
  { name: 'frontmatter-and-empty-title', spec: 'original', text: lines('---', 'title: From the frontmatter', '---', 'gitGraph', '  title', '  commit') },
  { name: 'error-title-glued', spec: 'original', text: graph('titles', 'commit') },
  { name: 'error-acc-title-no-colon', spec: 'original', text: graph('accTitle x', 'commit') },

  // The sanitizing of ids, messages, tags and branch names (sanitizeText of the gitGraph configuration)
  { name: 'sanitize-markup', spec: 'original', text: graph('commit id: "a<b>c=d&e" msg: "<i>m</i>" tag: "t<br>u"') },
  { name: 'sanitize-line-breaks', spec: 'original', text: graph('commit id: "a<BR/>b<br />c" tag: "<br>"') },
  { name: 'sanitize-script', spec: 'original', text: graph('commit id: "<script>alert(1)</script>"') },
  { name: 'sanitize-entities', spec: 'original', text: graph('commit id: "A #amp; B #35; C" tag: "&lt;"') },
  { name: 'sanitize-branch-name', spec: 'original', text: graph('commit', 'branch "x<y>z"', 'commit', 'checkout main', 'merge "x<y>z"') },
  { name: 'sanitize-loose-security', spec: 'original', text: graph('commit id: "a<b>c" tag: "<br>"'), site: { securityLevel: 'loose' } },

  // Comments and blank lines
  { name: 'comment-lines', spec: 'original', text: graph('%% a comment', 'commit', '   %% indented', 'commit id: "x" %% trailing', 'branch dev %% after a name', 'commit') },
  { name: 'blank-lines', spec: 'original', text: lines('gitGraph', '', '', '  commit', '', '  commit', '') },
  { name: 'error-comment-inside-statement', spec: 'original', text: graph('commit %% c', 'id: "x"') },

  // The main branch name and order, and the other settings (site configuration)
  { name: 'site-main-branch-name', spec: 'original', text: graph('commit', 'branch dev', 'commit', 'checkout trunk', 'merge dev'), site: site({ mainBranchName: 'trunk' }) },
  { name: 'site-main-branch-order', spec: 'original', text: graph('commit', 'branch a', 'branch b order: 1'), site: site({ mainBranchName: 'trunk', mainBranchOrder: 2 }) },
  { name: 'site-main-branch-order-fraction', spec: 'original', text: graph('commit', 'branch a', 'branch b order: 1'), site: site({ mainBranchOrder: 0.5 }) },
  { name: 'site-main-branch-order-negative', spec: 'original', text: graph('commit', 'branch a', 'branch b order: 1'), site: site({ mainBranchOrder: -1 }) },
  { name: 'site-main-branch-name-spaces', spec: 'original', text: graph('commit', 'branch dev', 'commit', 'checkout "the main"', 'merge dev'), site: site({ mainBranchName: 'the main' }) },
  { name: 'site-main-branch-name-not-sanitized', spec: 'original', text: graph('commit', 'branch dev', 'checkout "a<b"'), site: site({ mainBranchName: 'a<b' }) },
  { name: 'error-site-main-branch-renamed', spec: 'original', text: graph('commit', 'checkout main'), site: site({ mainBranchName: 'trunk' }) },
  { name: 'site-settings', spec: 'original', text: graph('commit'), site: site({ parallelCommits: true, rotateCommitLabel: false, showBranches: false, showCommitLabel: false, titleTopMargin: 40, diagramPadding: 20 }) },

  // Larger graphs (1-eb28011 is the id of the first commit without one, see seeded-random.mjs)
  { name: 'large-many-branches', spec: 'original', text: graph('commit', ...Array.from({ length: 12 }, (_, i) => [`branch b${i}${i % 3 === 0 ? '' : ` order: ${12 - i}`}`, 'commit', 'checkout main']).flat(), 'merge b5', 'merge b11') },
  { name: 'large-cherry-pick-chain', spec: 'original', text: graph('commit id: "A" msg: "start"', 'branch one', 'cherry-pick id: "A"', 'branch two', 'commit', 'checkout one', 'commit', 'checkout two', 'cherry-pick id: "1-eb28011"') },
  { name: 'large-mixed', spec: 'original', text: graph('commit id: "A"', 'branch dev', 'commit id: "B"', 'branch feat', 'commit id: "C"', 'checkout main', 'commit id: "D"', 'checkout dev', 'commit id: "E"', 'merge main id: "N"', 'checkout main', 'commit id: "F"', 'checkout dev', 'commit id: "G"', 'checkout main', 'merge dev id: "M" tag: "v1"', 'checkout feat', 'cherry-pick id: "F"', 'merge main') },

  // ------------------------------------------------------------------------------------- the git graph fixtures
  ...fixtures
];
