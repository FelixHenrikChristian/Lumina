import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

import { searchResultParentPath } from "../src/components/searchResultNavigation.ts";

const explorer = readFileSync(
  new URL("../src/components/FileExplorer.tsx", import.meta.url),
  "utf8",
);
const css = readFileSync(new URL("../src/index.css", import.meta.url), "utf8");

test("searchResultParentPath returns the containing directory for a recursive result", () => {
  assert.equal(
    searchResultParentPath(
      { path: "loc:demo/photos/trips/image.jpg", relativePath: "photos/trips" },
      true,
    ),
    "loc:demo/photos/trips",
  );
});

test("searchResultParentPath hides non-recursive or disabled parent labels", () => {
  assert.equal(
    searchResultParentPath({ path: "loc:demo/image.jpg", relativePath: "" }, true),
    null,
  );
  assert.equal(
    searchResultParentPath(
      { path: "loc:demo/photos/image.jpg", relativePath: "photos" },
      false,
    ),
    null,
  );
});

test("search result parent links precede filenames and open the containing directory", () => {
  const fileInfo = explorer.slice(
    explorer.indexOf('<div className="file-info">'),
    explorer.indexOf("function FileTagChip"),
  );
  const parentLinkIndex = fileInfo.indexOf('className="file-parent"');
  const fileNameIndex = fileInfo.indexOf('className="file-name"');

  assert.ok(parentLinkIndex >= 0 && parentLinkIndex < fileNameIndex);
  assert.match(fileInfo, /void useLumina\.getState\(\)\.openDirectory\(parentPath\)/);
  assert.match(fileInfo, /onPointerDown=\{\(event\) => event\.stopPropagation\(\)\}/);
  assert.match(css, /\.file-name-row/);
});
