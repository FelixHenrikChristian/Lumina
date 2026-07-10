export interface LiquidGlassSize {
  readonly width: number;
  readonly height: number;
}

export const EMPTY_LIQUID_GLASS_SIZE: LiquidGlassSize = Object.freeze({
  width: 0,
  height: 0,
});

export function hasRenderableLiquidGlassSize(size: LiquidGlassSize): boolean {
  return (
    Number.isFinite(size.width) &&
    Number.isFinite(size.height) &&
    size.width > 0 &&
    size.height > 0
  );
}
