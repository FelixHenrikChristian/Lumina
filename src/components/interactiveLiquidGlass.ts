import type { GlassConfig } from "../core/models";
import type { LiquidGlassProps } from "../vendor/liquid-glass";

export type InteractiveLiquidGlassProps = Pick<
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
  | "trackLight"
  | "padding"
>;

export function interactiveLiquidGlassProps(
  glass: GlassConfig,
  disabled = false,
): InteractiveLiquidGlassProps {
  return {
    mode: glass.mode,
    displacementScale: glass.displacementScale,
    blurAmount: glass.blurAmount,
    saturation: glass.saturation,
    aberrationIntensity: glass.aberrationIntensity,
    elasticity: disabled ? 0 : glass.elasticity,
    cornerRadius: glass.cornerRadius,
    overLight: glass.overLight,
    animate: !disabled,
    trackLight: !disabled,
    padding: "0px",
  };
}
