// Types for the vendored liquid-glass-react (MIT, github.com/rdev/liquid-glass-react).
// Local additions over upstream: `animate` prop (false = static surface, no
// transitions or cursor-tracking) and ResizeObserver-based size tracking.
import type { CSSProperties, ReactNode, RefObject } from "react";

export interface LiquidGlassProps {
  children: ReactNode;
  displacementScale?: number;
  blurAmount?: number;
  saturation?: number;
  aberrationIntensity?: number;
  elasticity?: number;
  cornerRadius?: number;
  globalMousePos?: { x: number; y: number };
  mouseOffset?: { x: number; y: number };
  mouseContainer?: RefObject<HTMLElement | null> | null;
  className?: string;
  padding?: string;
  style?: CSSProperties;
  overLight?: boolean;
  mode?: "standard" | "polar" | "prominent" | "shader";
  animate?: boolean;
  onClick?: () => void;
}

declare function LiquidGlass(props: LiquidGlassProps): ReactNode;
export default LiquidGlass;
