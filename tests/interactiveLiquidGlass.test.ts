import assert from "node:assert/strict";
import test from "node:test";

import type { GlassConfig } from "../src/core/models.ts";
import { interactiveLiquidGlassProps } from "../src/components/interactiveLiquidGlass.ts";

const glass: GlassConfig = {
  mode: "prominent",
  displacementScale: 82,
  blurAmount: 0.31,
  saturation: 205,
  aberrationIntensity: 4.5,
  elasticity: 0.47,
  cornerRadius: 19,
  overLight: true,
};

test("interactiveLiquidGlassProps forwards every visual setting and enables motion", () => {
  assert.deepEqual(interactiveLiquidGlassProps(glass), {
    mode: "prominent",
    displacementScale: 82,
    blurAmount: 0.31,
    saturation: 205,
    aberrationIntensity: 4.5,
    elasticity: 0.47,
    cornerRadius: 19,
    overLight: true,
    animate: true,
    trackLight: true,
    padding: "0px",
  });
});

test("interactiveLiquidGlassProps makes disabled controls motionless", () => {
  assert.deepEqual(interactiveLiquidGlassProps(glass, true), {
    mode: "prominent",
    displacementScale: 82,
    blurAmount: 0.31,
    saturation: 205,
    aberrationIntensity: 4.5,
    elasticity: 0,
    cornerRadius: 19,
    overLight: true,
    animate: false,
    trackLight: false,
    padding: "0px",
  });
});
