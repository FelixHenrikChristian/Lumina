import type { GlassConfig } from "../core/models";
import type { LiquidGlassProps } from "../vendor/liquid-glass";

export type StaticLiquidGlassProps = Pick<
  LiquidGlassProps,
  | "mode"
  | "displacementScale"
  | "blurAmount"
  | "saturation"
  | "aberrationIntensity"
  | "elasticity"
  | "cornerRadius"
  | "overLight"
  | "animate"
  | "padding"
>;

export function staticLiquidGlassProps(glass: GlassConfig): StaticLiquidGlassProps {
  return {
    mode: glass.mode,
    displacementScale: glass.displacementScale,
    blurAmount: glass.blurAmount,
    saturation: glass.saturation,
    aberrationIntensity: glass.aberrationIntensity,
    elasticity: 0,
    cornerRadius: glass.cornerRadius,
    overLight: glass.overLight,
    animate: false,
    padding: "0px",
  };
}
