import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const css = readFileSync(new URL("../src/index.css", import.meta.url), "utf8");

test("panel frost uses only the standard backdrop-filter property", () => {
  // Chromium (Electron) has no -webkit-backdrop-filter alias, and the CSS
  // minifier collapses a standard+prefixed pair into whichever declaration
  // comes last. Shipping the prefixed one made every menu and popover fully
  // transparent in packaged builds while dev (unminified CSS) looked right.
  assert.doesNotMatch(css, /-webkit-backdrop-filter\s*:/);
  const frosted = css.match(/backdrop-filter\s*:/g) ?? [];
  assert.ok(frosted.length >= 5, `expected the frost declarations to survive, found ${frosted.length}`);
});
