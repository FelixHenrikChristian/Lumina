import { useLumina, useT } from "../state/store";
import {
  LANGUAGE_CHINESE,
  LANGUAGE_ENGLISH,
  LANGUAGE_SYSTEM,
} from "../core/localization";

export function SettingsPanel() {
  const t = useT();
  const settings = useLumina((s) => s.settings);
  const updateSettings = useLumina((s) => s.updateSettings);

  return (
    <div className="sidebar-pane">
      <header className="sidebar-header">
        <h2>{t("Settings")}</h2>
      </header>

      <div className="sidebar-scroll settings-scroll">
        <section className="settings-section">
          <h3>{t("SettingsAppearance")}</h3>
          <label className="settings-row">
            <span>{t("Language")}</span>
            <select
              className="lg-input"
              value={settings.language}
              onChange={(e) => updateSettings({ language: e.currentTarget.value })}
            >
              <option value={LANGUAGE_SYSTEM}>{t("LanguageSystem")}</option>
              <option value={LANGUAGE_ENGLISH}>{t("LanguageEnglish")}</option>
              <option value={LANGUAGE_CHINESE}>{t("LanguageChinese")}</option>
            </select>
          </label>
        </section>

        <section className="settings-section">
          <h3>{t("SettingsBehavior")}</h3>
          <label className="settings-row">
            <span>{t("HideFileExtension")}</span>
            <input
              type="checkbox"
              className="lg-switch"
              checked={settings.hideFileExtension}
              onChange={(e) => updateSettings({ hideFileExtension: e.currentTarget.checked })}
            />
          </label>
          <label className="settings-row">
            <span>{t("ShowParentFolder")}</span>
            <input
              type="checkbox"
              className="lg-switch"
              checked={settings.showParentFolderInRecursiveSearch}
              onChange={(e) =>
                updateSettings({ showParentFolderInRecursiveSearch: e.currentTarget.checked })
              }
            />
          </label>
        </section>
      </div>
    </div>
  );
}
