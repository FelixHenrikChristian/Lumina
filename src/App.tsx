import { useEffect, useState, type CSSProperties } from "react";
import { useLumina, useT } from "./state/store";
import { OverlayProvider } from "./components/overlays";
import { LocationSidebar } from "./components/LocationSidebar";
import { TagSidebar } from "./components/TagSidebar";
import { SettingsDialog } from "./components/SettingsDialog";
import { FileExplorer } from "./components/FileExplorer";
import { StaticLiquidGlassSurface } from "./components/StaticLiquidGlassSurface";
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  FolderIcon,
  SettingsIcon,
  TagIcon,
} from "./components/icons";
import type { SidebarView } from "./core/models";
import { isElectron, nativeApi, type AppUpdateState } from "./fs/electronApi";

const SIDEBAR_TABS: { view: SidebarView; labelKey: string; Icon: typeof FolderIcon }[] = [
  { view: "locations", labelKey: "Locations", Icon: FolderIcon },
  { view: "tags", labelKey: "Tags", Icon: TagIcon },
];

export default function App() {
  const t = useT();
  const sidebarView = useLumina((s) => s.settings.sidebarView);
  const sidebarCollapsed = useLumina((s) => s.settings.sidebarCollapsed);
  const glass = useLumina((s) => s.settings.glass);
  const customWallpaper = useLumina((s) => s.settings.customWallpaper);
  const setSidebarView = useLumina((s) => s.setSidebarView);
  const toggleSidebar = useLumina((s) => s.toggleSidebar);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [appUpdate, setAppUpdate] = useState<AppUpdateState | null>(null);
  const wallpaperStyle = customWallpaper
    ? ({
        "--lumina-wallpaper-image": `url(${JSON.stringify(customWallpaper.url)})`,
      } as CSSProperties)
    : undefined;

  // Restore the last session's location. FSA locations may still need a
  // permission grant; the explorer surfaces that as an error until the
  // location is clicked (a user gesture).
  useEffect(() => {
    const { selectedLocationId, selectLocation } = useLumina.getState();
    if (selectedLocationId) void selectLocation(selectedLocationId);
  }, []);

  useEffect(() => {
    if (!isElectron()) return;
    const api = nativeApi();
    let active = true;
    const unsubscribe = api.onUpdateState((state) => {
      if (active) setAppUpdate(state);
    });
    void api.getUpdateState().then((state) => {
      if (active) setAppUpdate(state);
    }).catch(() => undefined);
    return () => {
      active = false;
      unsubscribe();
    };
  }, []);

  // Chromium navigates to any file dropped where the app didn't claim the
  // drag. The explorer grid marks its drop zones with preventDefault; every
  // other surface must refuse OS drags outright.
  useEffect(() => {
    const onDragOver = (event: DragEvent) => {
      if (event.defaultPrevented) return;
      event.preventDefault();
      if (event.dataTransfer) event.dataTransfer.dropEffect = "none";
    };
    const onDrop = (event: DragEvent) => {
      if (!event.defaultPrevented) event.preventDefault();
    };
    window.addEventListener("dragover", onDragOver);
    window.addEventListener("drop", onDrop);
    return () => {
      window.removeEventListener("dragover", onDragOver);
      window.removeEventListener("drop", onDrop);
    };
  }, []);

  // Ctrl+B mirrors the seam handle button.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (
        event.ctrlKey &&
        !event.altKey &&
        !event.metaKey &&
        !event.shiftKey &&
        event.key.toLowerCase() === "b"
      ) {
        event.preventDefault();
        useLumina.getState().toggleSidebar();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  const sidebarToggleLabel = t(sidebarCollapsed ? "ShowSidebar" : "HideSidebar");
  const updateNeedsAttention = appUpdate !== null
    && ["available", "downloading", "downloaded"].includes(appUpdate.status);
  const settingsLabel = appUpdate?.status === "downloaded"
    ? t("UpdateReadyToInstallTitle", appUpdate.availableVersion ?? "")
    : updateNeedsAttention
      ? t("UpdateAvailableTitle", appUpdate?.availableVersion ?? "")
      : t("Settings");

  return (
    <OverlayProvider>
      <div
        className={`lg-wallpaper${customWallpaper ? " has-custom-wallpaper" : ""}`}
        style={wallpaperStyle}
        aria-hidden="true"
      />
      <div className={`app-shell${sidebarCollapsed ? " sidebar-collapsed" : ""}`}>
        <div className="app-sidebar-frame">
          <StaticLiquidGlassSurface glass={glass} className="sidebar-liquid-glass">
            <aside id="app-sidebar" className="app-sidebar" inert={sidebarCollapsed}>
              <div className="sidebar-brand">
                <span className="sidebar-brand-mark" />
                {t("AppTitle")}
                <button
                  type="button"
                  className={`nav-button settings-button${updateNeedsAttention ? " has-update" : ""}`}
                  title={settingsLabel}
                  aria-label={settingsLabel}
                  aria-haspopup="dialog"
                  onClick={() => setSettingsOpen(true)}
                >
                  <SettingsIcon />
                </button>
              </div>
              <nav className="sidebar-tabs" role="tablist">
                {SIDEBAR_TABS.map(({ view, labelKey, Icon }) => (
                  <button
                    key={view}
                    type="button"
                    role="tab"
                    aria-selected={sidebarView === view}
                    className={`sidebar-tab${sidebarView === view ? " is-active" : ""}`}
                    onClick={() => setSidebarView(view)}
                  >
                    <Icon size={15} />
                    {t(labelKey)}
                  </button>
                ))}
              </nav>
              {sidebarView === "locations" && <LocationSidebar />}
              {sidebarView === "tags" && <TagSidebar />}
            </aside>
          </StaticLiquidGlassSurface>
        </div>
        <button
          type="button"
          className="shell-handle"
          title={`${sidebarToggleLabel} (Ctrl+B)`}
          aria-label={sidebarToggleLabel}
          aria-expanded={!sidebarCollapsed}
          aria-controls="app-sidebar"
          onClick={toggleSidebar}
        >
          <span className="shell-handle-grip">
            {sidebarCollapsed ? <ChevronRightIcon size={13} /> : <ChevronLeftIcon size={13} />}
          </span>
        </button>
        <div className="app-main-frame">
          <StaticLiquidGlassSurface glass={glass} className="main-liquid-glass">
            <main className="app-main">
              <FileExplorer />
            </main>
          </StaticLiquidGlassSurface>
        </div>
      </div>
      {settingsOpen && (
        <SettingsDialog
          updateState={appUpdate}
          onDismiss={() => setSettingsOpen(false)}
        />
      )}
    </OverlayProvider>
  );
}
