import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const sidebar = readFileSync(
  new URL("../src/components/TagSidebar.tsx", import.meta.url),
  "utf8",
);
const localization = readFileSync(
  new URL("../src/core/localization.ts", import.meta.url),
  "utf8",
);
const css = readFileSync(new URL("../src/index.css", import.meta.url), "utf8");

test("tag group menus do not duplicate the add-tag action", () => {
  const groupMenu = sidebar.slice(
    sidebar.indexOf("const groupMenu"),
    sidebar.indexOf("const tagMenu"),
  );
  assert.doesNotMatch(groupMenu, /add-tag|AddTag|setTagDraft/);
});

test("empty tag groups rely on the add button without extra copy", () => {
  assert.doesNotMatch(sidebar, /GroupEmpty|tag-group-empty/);
  assert.doesNotMatch(localization, /GroupEmpty:/);
  assert.doesNotMatch(css, /\.tag-group-empty/);
});
