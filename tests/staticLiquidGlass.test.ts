import assert from "node:assert/strict";
import test from "node:test";

import type { GlassConfig } from "../src/core/models.ts";
import { staticLiquidGlassProps } from "../src/components/staticLiquidGlass.ts";

test("staticLiquidGlassProps forwards visual settings, disables deformation, and tracks light", () => {
  const glass: GlassConfig = {
    mode: "polar",
    displacementScale: 117,
    blurAmount: 0.42,
    saturation: 235,
    aberrationIntensity: 7.5,
    elasticity: 0.91,
    cornerRadius: 38,
    overLight: true,
  };

  assert.deepEqual(staticLiquidGlassProps(glass), {
    mode: "polar",
    displacementScale: 117,
    blurAmount: 0.42,
    saturation: 235,
    aberrationIntensity: 7.5,
    elasticity: 0,
    cornerRadius: 38,
    overLight: true,
    animate: false,
    trackLight: true,
    padding: "0px",
  });
});
