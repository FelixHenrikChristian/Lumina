import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const sidebar = readFileSync(
  new URL("../src/components/TagSidebar.tsx", import.meta.url),
  "utf8",
);
const explorer = readFileSync(
  new URL("../src/components/FileExplorer.tsx", import.meta.url),
  "utf8",
);
const css = readFileSync(new URL("../src/index.css", import.meta.url), "utf8");

test("the tag sidebar does not read or toggle explorer filters", () => {
  assert.doesNotMatch(sidebar, /selectedTagFilterIds|toggleTagFilter|is-filtered/);
});

test("the explorer filter popover owns a persistent clear action", () => {
  assert.match(explorer, /className="tag-filter-header"/);
  assert.match(explorer, /disabled=\{selectedIds\.size === 0\}/);
  assert.match(explorer, /void clearTagFilters\(\)/);
});

test("inactive filter tags use a neutral style while active tags keep their colors", () => {
  assert.match(
    explorer,
    /className=\{`tag-chip tag-filter-chip\$\{active \? " is-filtered" : ""\}`\}/,
  );
  assert.match(
    explorer,
    /style=\{active \? \{ background: cssColorFor\(style\.color\), color: style\.textColor \} : undefined\}/,
  );
  assert.match(css, /\.tag-filter-chip:not\(\.is-filtered\)/);
});
