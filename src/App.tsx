import { useEffect } from "react";
import { useLumina, useT } from "./state/store";
import { OverlayProvider } from "./components/overlays";
import { LocationSidebar } from "./components/LocationSidebar";
import { TagSidebar } from "./components/TagSidebar";
import { SettingsPanel } from "./components/SettingsPanel";
import { FileExplorer } from "./components/FileExplorer";
import { FolderIcon, SettingsIcon, TagIcon } from "./components/icons";
import type { SidebarView } from "./core/models";

const SIDEBAR_TABS: { view: SidebarView; labelKey: string; Icon: typeof FolderIcon }[] = [
  { view: "locations", labelKey: "Locations", Icon: FolderIcon },
  { view: "tags", labelKey: "Tags", Icon: TagIcon },
  { view: "settings", labelKey: "Settings", Icon: SettingsIcon },
];

export default function App() {
  const t = useT();
  const sidebarView = useLumina((s) => s.settings.sidebarView);
  const setSidebarView = useLumina((s) => s.setSidebarView);

  // Restore the last session's location. FSA locations may still need a
  // permission grant; the explorer surfaces that as an error until the
  // location is clicked (a user gesture).
  useEffect(() => {
    const { selectedLocationId, selectLocation } = useLumina.getState();
    if (selectedLocationId) void selectLocation(selectedLocationId);
  }, []);

  return (
    <OverlayProvider>
      <div className="lg-wallpaper" aria-hidden="true" />
      <div className="app-shell">
        <aside className="app-sidebar lg-panel">
          <div className="sidebar-brand">
            <span className="sidebar-brand-mark" />
            {t("AppTitle")}
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
          {sidebarView === "settings" && <SettingsPanel />}
        </aside>
        <main className="app-main lg-panel">
          <FileExplorer />
        </main>
      </div>
    </OverlayProvider>
  );
}
