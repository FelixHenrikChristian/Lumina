import type { CSSProperties, ReactNode } from "react";
import type { GlassConfig } from "../core/models";
import LiquidGlass from "../vendor/liquid-glass";
import { staticLiquidGlassProps } from "./staticLiquidGlass";

const FULL_SIZE_GLASS_STYLE = {
  position: "absolute",
  top: "50%",
  left: "50%",
  width: "100%",
  height: "100%",
} satisfies CSSProperties;

interface StaticLiquidGlassSurfaceProps {
  glass: GlassConfig;
  className?: string;
  children: ReactNode;
}

export function StaticLiquidGlassSurface({
  glass,
  className,
  children,
}: StaticLiquidGlassSurfaceProps): ReactNode {
  const classes = ["structural-liquid-glass", className].filter(Boolean).join(" ");

  return (
    <LiquidGlass
      {...staticLiquidGlassProps(glass)}
      className={classes}
      style={FULL_SIZE_GLASS_STYLE}
    >
      {children}
    </LiquidGlass>
  );
}
