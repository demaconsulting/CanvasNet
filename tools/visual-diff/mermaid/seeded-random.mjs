// seeded-random.mjs - the fixed pseudo-random sequence of the git graph references. A git graph commit without an id
// gets "<seq>-<7 hex digits>" from mermaid's random({ length: 7 }) (makeRandomHex: seven times
// "0123456789abcdef".charAt(Math.floor(Math.random() * 16))), the only use of Math.random while mermaid parses and draws a
// git graph. The vector generator (gen-diagram-vectors.mjs --type gitgraph) and the golden renderer
// (render-mermaid.mjs --seed-random) replace Math.random in their pages by mulberry32 seeded with GIT_ID_SEED, so that
// mermaid's ids are the same in every run, and the same as CanvasNet's, which draws its digits (the top four bits of each
// 32-bit output) from the same sequence, restarted for every diagram.
//
// Developer tooling of tools/visual-diff; see the README there.

// The seed of the sequence (the first ids of a page are 0-eb28011, 1-cc99105, 2-43d14d3, ...)
export const GIT_ID_SEED = 0x2545f491;

// The function of mermaid that draws the digits of an id. mermaid-cli's page loads mermaid's ESM build, which keeps the
// function names, and one of its modules (@iconify/utils, for its random id prefix) draws a number when it loads; the
// golden renderer therefore installs the sequence before mermaid loads and gives it to this function's calls only.
export const ID_CALLER = 'makeRandomHex';

// Runs in the page (page.evaluate or page.evaluateOnNewDocument): replaces Math.random by mulberry32 seeded with seed;
// with a caller, only the calls made by that function (named in the call stack) get the sequence, the other calls get
// the browser's numbers
export function installSeededRandom(seed, caller = null) {
  const browserRandom = Math.random;
  let state = seed | 0;
  Math.random = function seededRandom() {
    if (caller !== null && !(new Error().stack ?? '').includes(caller)) {
      return browserRandom.call(Math);
    }
    state = (state + 0x6d2b79f5) | 0;
    let t = Math.imul(state ^ (state >>> 15), 1 | state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
