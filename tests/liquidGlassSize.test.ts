import assert from "node:assert/strict";
import test from "node:test";

import {
  EMPTY_LIQUID_GLASS_SIZE,
  hasRenderableLiquidGlassSize,
} from "../src/vendor/liquid-glass-size.ts";

test("liquid glass starts without a guessed render size", () => {
  assert.deepEqual(EMPTY_LIQUID_GLASS_SIZE, { width: 0, height: 0 });
  assert.equal(hasRenderableLiquidGlassSize(EMPTY_LIQUID_GLASS_SIZE), false);
});

test("liquid glass renders auxiliary layers only for finite positive sizes", () => {
  assert.equal(hasRenderableLiquidGlassSize({ width: 102, height: 26 }), true);
  assert.equal(hasRenderableLiquidGlassSize({ width: 0, height: 26 }), false);
  assert.equal(hasRenderableLiquidGlassSize({ width: 102, height: 0 }), false);
  assert.equal(hasRenderableLiquidGlassSize({ width: Number.NaN, height: 26 }), false);
  assert.equal(hasRenderableLiquidGlassSize({ width: 102, height: Infinity }), false);
});
