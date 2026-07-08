import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import LiquidGlass from "../vendor/liquid-glass";
import { useLumina } from "../state/store";

// ---------------------------------------------------------------------------
// Context menu / popover: one open surface at a time, closed on outside
// pointer-down, Escape, scroll, or resize.

export interface MenuEntry {
  key: string;
  label: string;
  icon?: ReactNode;
  danger?: boolean;
  disabled?: boolean;
  separatorAbove?: boolean;
  onSelect(): void;
}

interface OpenMenu {
  x: number;
  y: number;
  entries: MenuEntry[];
}

interface OverlayApi {
  openMenu(x: number, y: number, entries: MenuEntry[]): void;
  closeMenu(): void;
}

const OverlayContext = createContext<OverlayApi | null>(null);

export function useOverlay(): OverlayApi {
  const api = useContext(OverlayContext);
  if (!api) throw new Error("useOverlay must be used inside <OverlayProvider>");
  return api;
}

export function OverlayProvider({ children }: { children: ReactNode }) {
  const [menu, setMenu] = useState<OpenMenu | null>(null);
  const menuRef = useRef<HTMLDivElement | null>(null);

  const closeMenu = useCallback(() => setMenu(null), []);
  const openMenu = useCallback((x: number, y: number, entries: MenuEntry[]) => {
    setMenu({ x, y, entries });
  }, []);

  useEffect(() => {
    if (!menu) return;
    const onPointerDown = (event: PointerEvent) => {
      if (!menuRef.current?.contains(event.target as Node)) closeMenu();
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") closeMenu();
    };
    window.addEventListener("pointerdown", onPointerDown, true);
    window.addEventListener("keydown", onKeyDown, true);
    window.addEventListener("resize", closeMenu);
    window.addEventListener("wheel", closeMenu, true);
    return () => {
      window.removeEventListener("pointerdown", onPointerDown, true);
      window.removeEventListener("keydown", onKeyDown, true);
      window.removeEventListener("resize", closeMenu);
      window.removeEventListener("wheel", closeMenu, true);
    };
  }, [menu, closeMenu]);

  return (
    <OverlayContext.Provider value={{ openMenu, closeMenu }}>
      {children}
      {menu &&
        createPortal(
          <ContextMenuSurface ref={menuRef} menu={menu} onClose={closeMenu} />,
          document.body,
        )}
    </OverlayContext.Provider>
  );
}

function ContextMenuSurface({
  ref,
  menu,
  onClose,
}: {
  ref: React.RefObject<HTMLDivElement | null>;
  menu: OpenMenu;
  onClose(): void;
}) {
  const [pos, setPos] = useState({ x: menu.x, y: menu.y });

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const { innerWidth, innerHeight } = window;
    const rect = el.getBoundingClientRect();
    setPos({
      x: Math.max(8, Math.min(menu.x, innerWidth - rect.width - 8)),
      y: Math.max(8, Math.min(menu.y, innerHeight - rect.height - 8)),
    });
  }, [menu, ref]);

  return (
    <div
      ref={ref}
      className="lg-menu lg-panel"
      role="menu"
      style={{ left: pos.x, top: pos.y }}
    >
      {menu.entries.map((entry) => (
        <div key={entry.key} className="lg-menu-item-wrap">
          {entry.separatorAbove && <div className="lg-menu-separator" />}
          <button
            type="button"
            role="menuitem"
            className={`lg-menu-item${entry.danger ? " is-danger" : ""}`}
            disabled={entry.disabled}
            onClick={() => {
              onClose();
              entry.onSelect();
            }}
          >
            {entry.icon && <span className="lg-menu-icon">{entry.icon}</span>}
            <span>{entry.label}</span>
          </button>
        </div>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Anchored popover (sort menu, tag filter flyout).

export function Popover({
  anchor,
  onClose,
  align = "start",
  children,
}: {
  anchor: HTMLElement;
  onClose(): void;
  align?: "start" | "end";
  children: ReactNode;
}) {
  const ref = useRef<HTMLDivElement | null>(null);
  const [pos, setPos] = useState<{ x: number; y: number } | null>(null);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const anchorRect = anchor.getBoundingClientRect();
    const rect = el.getBoundingClientRect();
    const x =
      align === "end" ? anchorRect.right - rect.width : anchorRect.left;
    setPos({
      x: Math.max(8, Math.min(x, window.innerWidth - rect.width - 8)),
      y: Math.min(anchorRect.bottom + 8, window.innerHeight - rect.height - 8),
    });
  }, [anchor, align]);

  useEffect(() => {
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!ref.current?.contains(target) && !anchor.contains(target)) onClose();
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    window.addEventListener("pointerdown", onPointerDown, true);
    window.addEventListener("keydown", onKeyDown, true);
    window.addEventListener("resize", onClose);
    return () => {
      window.removeEventListener("pointerdown", onPointerDown, true);
      window.removeEventListener("keydown", onKeyDown, true);
      window.removeEventListener("resize", onClose);
    };
  }, [anchor, onClose]);

  return createPortal(
    <div
      ref={ref}
      className="lg-popover lg-panel"
      style={pos ? { left: pos.x, top: pos.y, visibility: "visible" } : undefined}
    >
      {children}
    </div>,
    document.body,
  );
}

// ---------------------------------------------------------------------------
// Modal dialog on a LiquidGlass surface.

export function GlassDialog({
  title,
  onDismiss,
  children,
  width = 420,
}: {
  title: string;
  onDismiss(): void;
  children: ReactNode;
  width?: number;
}) {
  const glass = useLumina((s) => s.settings.glass);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.stopPropagation();
        onDismiss();
      }
    };
    window.addEventListener("keydown", onKeyDown, true);
    return () => window.removeEventListener("keydown", onKeyDown, true);
  }, [onDismiss]);

  return createPortal(
    <div
      className="lg-dialog-backdrop"
      onPointerDown={(event) => {
        if (event.target === event.currentTarget) onDismiss();
      }}
    >
      <LiquidGlass
        // Positioning must go through the style prop: the library renders
        // its tint/shine layers as siblings that copy position/top/left
        // from here — a CSS class on the root would leave them stranded.
        style={{ position: "absolute", top: "50%", left: "50%" }}
        mode={glass.mode}
        cornerRadius={glass.cornerRadius}
        displacementScale={glass.displacementScale}
        blurAmount={glass.blurAmount}
        saturation={glass.saturation}
        aberrationIntensity={glass.aberrationIntensity}
        elasticity={0}
        animate={false} // dialogs are static: no hover motion, no transitions
        overLight={glass.overLight}
        padding="0px"
      >
        <div
          className="lg-dialog"
          style={{ width }}
          role="dialog"
          aria-modal="true"
          aria-label={title}
        >
          <div className="lg-dialog-title">{title}</div>
          {children}
        </div>
      </LiquidGlass>
    </div>,
    document.body,
  );
}

export function ConfirmDialog({
  title,
  message,
  confirmLabel,
  cancelLabel,
  danger,
  onConfirm,
  onCancel,
}: {
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  danger?: boolean;
  onConfirm(): void;
  onCancel(): void;
}) {
  return (
    <GlassDialog title={title} onDismiss={onCancel} width={380}>
      <p className="lg-dialog-message">{message}</p>
      <div className="lg-dialog-actions">
        <button type="button" className="lg-button" onClick={onCancel}>
          {cancelLabel}
        </button>
        <button
          type="button"
          className={`lg-button ${danger ? "is-danger" : "is-primary"}`}
          onClick={onConfirm}
          autoFocus
        >
          {confirmLabel}
        </button>
      </div>
    </GlassDialog>
  );
}
