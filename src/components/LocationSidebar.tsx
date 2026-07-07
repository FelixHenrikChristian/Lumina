import { useState } from "react";
import { useLumina, useT } from "../state/store";
import { addDemoLocation, addRealLocation, canAddRealFolder } from "./locationActions";
import { useOverlay } from "./overlays";
import { ConfirmDialog } from "./overlays";
import { FolderIcon, MoreIcon, PlusIcon, TrashIcon, EditIcon } from "./icons";

export function LocationSidebar() {
  const t = useT();
  const locations = useLumina((s) => s.locations);
  const selectedId = useLumina((s) => s.selectedLocationId);
  const selectLocation = useLumina((s) => s.selectLocation);
  const renameLocation = useLumina((s) => s.renameLocation);
  const removeLocation = useLumina((s) => s.removeLocation);
  const clearLocations = useLumina((s) => s.clearLocations);
  const { openMenu } = useOverlay();

  const [renamingId, setRenamingId] = useState<string | null>(null);
  const [confirmClear, setConfirmClear] = useState(false);
  const canAddFolder = canAddRealFolder();

  const menuFor = (id: string) => [
    {
      key: "rename",
      label: t("RenameLocation"),
      icon: <EditIcon />,
      onSelect: () => setRenamingId(id),
    },
    {
      key: "remove",
      label: t("RemoveLocation"),
      icon: <TrashIcon />,
      danger: true,
      onSelect: () => removeLocation(id),
    },
  ];

  return (
    <div className="sidebar-pane">
      <header className="sidebar-header">
        <h2>{t("Locations")}</h2>
        <div className="sidebar-actions">
          <button
            type="button"
            className="lg-button icon-only"
            title={canAddFolder ? t("AddLocation") : t("FsaUnsupported")}
            disabled={!canAddFolder}
            onClick={() => void addRealLocation()}
          >
            <PlusIcon />
          </button>
        </div>
      </header>

      <div className="sidebar-scroll">
        {locations.length === 0 && (
          <p className="sidebar-empty">{t("LocationsEmpty")}</p>
        )}
        <ul className="location-list">
          {locations.map((location) => (
            <li key={location.id}>
              {renamingId === location.id ? (
                <input
                  className="lg-input location-rename"
                  defaultValue={location.name}
                  autoFocus
                  onFocus={(e) => e.currentTarget.select()}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      renameLocation(location.id, e.currentTarget.value);
                      setRenamingId(null);
                    } else if (e.key === "Escape") {
                      setRenamingId(null);
                    }
                  }}
                  onBlur={(e) => {
                    renameLocation(location.id, e.currentTarget.value);
                    setRenamingId(null);
                  }}
                />
              ) : (
                <div
                  className={`location-row${location.id === selectedId ? " is-selected" : ""}`}
                  role="button"
                  tabIndex={0}
                  onClick={() => void selectLocation(location.id)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") void selectLocation(location.id);
                  }}
                  onContextMenu={(e) => {
                    e.preventDefault();
                    openMenu(e.clientX, e.clientY, menuFor(location.id));
                  }}
                >
                  <FolderIcon />
                  <span className="location-name" title={location.name}>
                    {location.name}
                  </span>
                  {location.kind === "demo" && <span className="location-badge">demo</span>}
                  <button
                    type="button"
                    className="row-more"
                    title={t("RenameLocation")}
                    onClick={(e) => {
                      e.stopPropagation();
                      const rect = e.currentTarget.getBoundingClientRect();
                      openMenu(rect.left, rect.bottom + 4, menuFor(location.id));
                    }}
                  >
                    <MoreIcon />
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      </div>

      <footer className="sidebar-footer">
        <button type="button" className="lg-chip" onClick={addDemoLocation}>
          <PlusIcon size={12} />
          {t("AddDemoLocation")}
        </button>
        {locations.length > 0 && (
          <button type="button" className="lg-chip" onClick={() => setConfirmClear(true)}>
            <TrashIcon size={12} />
            {t("ClearLocations")}
          </button>
        )}
      </footer>

      {confirmClear && (
        <ConfirmDialog
          title={t("ClearLocations")}
          message={t("ClearLocationsConfirm")}
          confirmLabel={t("ClearLocations")}
          cancelLabel={t("Cancel")}
          danger
          onConfirm={() => {
            clearLocations();
            setConfirmClear(false);
          }}
          onCancel={() => setConfirmClear(false)}
        />
      )}
    </div>
  );
}
