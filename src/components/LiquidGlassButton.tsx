import {
  forwardRef,
  type ButtonHTMLAttributes,
  type ReactNode,
} from "react";
import LiquidGlass from "../vendor/liquid-glass";
import { useLumina } from "../state/store";
import { interactiveLiquidGlassProps } from "./interactiveLiquidGlass";

export type LiquidGlassButtonVariant = "default" | "primary" | "danger";
export type LiquidGlassButtonSize = "regular" | "compact";

export interface LiquidGlassButtonProps
  extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children"> {
  children: ReactNode;
  variant?: LiquidGlassButtonVariant;
  size?: LiquidGlassButtonSize;
}

const ENABLE_GLASS_INTERACTION = () => undefined;

export const LiquidGlassButton = forwardRef<
  HTMLButtonElement,
  LiquidGlassButtonProps
>(function LiquidGlassButton(
  {
    children,
    className = "",
    disabled = false,
    size = "regular",
    style,
    type = "button",
    variant = "default",
    ...buttonProps
  },
  ref,
) {
  const glass = useLumina((state) => state.settings.glass);
  const classes = [
    "liquid-glass-button__control",
    className,
  ].filter(Boolean).join(" ");

  return (
    <div
      className={`liquid-glass-button liquid-glass-button--${size} liquid-glass-button--${variant}${disabled ? " is-disabled" : ""}`}
    >
      <LiquidGlass
        {...interactiveLiquidGlassProps(glass, disabled)}
        className="liquid-glass-button-surface"
        style={{ position: "relative", top: "50%", left: "50%" }}
        onClick={disabled ? undefined : ENABLE_GLASS_INTERACTION}
      >
        <button
          {...buttonProps}
          ref={ref}
          type={type}
          className={classes}
          disabled={disabled}
          style={{ borderRadius: `${glass.cornerRadius}px`, ...style }}
        >
          {children}
        </button>
      </LiquidGlass>
    </div>
  );
});

LiquidGlassButton.displayName = "LiquidGlassButton";
