import { useRef, useState } from "react";
import LiquidGlass from "../vendor/liquid-glass";
import { useLumina, useT } from "../state/store";
import {
  LANGUAGE_CHINESE,
  LANGUAGE_ENGLISH,
  LANGUAGE_SYSTEM,
} from "../core/localization";
import type { GlassConfig, GlassMode } from "../core/models";
import { DEFAULT_GLASS_CONFIG } from "../core/models";
import { isElectron, nativeApi, type AppUpdateState } from "../fs/electronApi";
import { GlassDialog } from "./overlays";
import { FolderIcon, InfoIcon, RefreshIcon, SettingsIcon, TagIcon } from "./icons";
import { LiquidGlassButton } from "./LiquidGlassButton";

type SettingsCategory = "appearance" | "behavior" | "glass" | "about";

const CATEGORIES: { key: SettingsCategory; labelKey: string; Icon: typeof FolderIcon }[] = [
  { key: "appearance", labelKey: "SettingsAppearance", Icon: FolderIcon },
  { key: "behavior", labelKey: "SettingsBehavior", Icon: TagIcon },
  { key: "glass", labelKey: "SettingsGlass", Icon: SettingsIcon },
  { key: "about", labelKey: "SettingsAbout", Icon: InfoIcon },
];

export function SettingsDialog({
  onDismiss,
  updateState,
}: {
  onDismiss(): void;
  updateState: AppUpdateState | null;
}) {
  const t = useT();
  const [category, setCategory] = useState<SettingsCategory>("appearance");

  return (
    <GlassDialog title={t("Settings")} onDismiss={onDismiss} width={760}>
      <div className="settings-layout">
        <nav className="settings-nav" role="tablist" aria-orientation="vertical">
          {CATEGORIES.map(({ key, labelKey, Icon }) => (
            <button
              key={key}
              type="button"
              role="tab"
              aria-selected={category === key}
              className={`settings-nav-item${category === key ? " is-active" : ""}`}
              onClick={() => setCategory(key)}
            >
              <Icon size={14} />
              {t(labelKey)}
            </button>
          ))}
        </nav>
        <div className="settings-pane">
          {category === "appearance" && <AppearancePane />}
          {category === "behavior" && <BehaviorPane />}
          {category === "glass" && <GlassPane />}
          {category === "about" && <AboutPane updateState={updateState} />}
        </div>
      </div>
      <div className="lg-dialog-actions">
        <LiquidGlassButton variant="primary" onClick={onDismiss}>
          {t("Close")}
        </LiquidGlassButton>
      </div>
    </GlassDialog>
  );
}

function AppearancePane() {
  const t = useT();
  const settings = useLumina((s) => s.settings);
  const updateSettings = useLumina((s) => s.updateSettings);
  const [wallpaperError, setWallpaperError] = useState<string | null>(null);
  const canChooseWallpaper =
    isElectron() && typeof window.luminaNative?.chooseWallpaper === "function";

  const chooseWallpaper = async () => {
    if (!canChooseWallpaper) return;
    setWallpaperError(null);
    try {
      const wallpaper = await nativeApi().chooseWallpaper();
      if (wallpaper) updateSettings({ customWallpaper: wallpaper });
    } catch (error) {
      setWallpaperError(t("WallpaperChooseFailed", message(error)));
    }
  };

  return (
    <div className="settings-dialog-body">
      <section className="settings-section">
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
        <h3>{t("Background")}</h3>
        <div className="settings-row wallpaper-row">
          <span>{t("BackgroundImage")}</span>
          <span className="settings-row-value">
            {settings.customWallpaper?.name ?? t("BackgroundDefault")}
          </span>
        </div>
        <div className="settings-action-row">
          <LiquidGlassButton
            disabled={!canChooseWallpaper}
            onClick={() => void chooseWallpaper()}
          >
            {t("ChooseBackgroundImage")}
          </LiquidGlassButton>
          <LiquidGlassButton
            disabled={!settings.customWallpaper}
            onClick={() => {
              setWallpaperError(null);
              updateSettings({ customWallpaper: null });
            }}
          >
            {t("RestoreDefaultBackground")}
          </LiquidGlassButton>
        </div>
        {!canChooseWallpaper && (
          <p className="settings-note">{t("DesktopWallpaperOnly")}</p>
        )}
        {wallpaperError && <p className="settings-error">{wallpaperError}</p>}
      </section>
    </div>
  );
}

function AboutPane({ updateState }: { updateState: AppUpdateState | null }) {
  const t = useT();
  const [actionError, setActionError] = useState<string | null>(null);
  const busy = updateState !== null
    && ["checking", "downloading", "installing"].includes(updateState.status);
  const canCheck = updateState !== null && updateState.mode !== "development";
  const stateError = updateState?.status === "error" ? updateState.error : null;

  const run = async (action: () => Promise<unknown>) => {
    setActionError(null);
    try {
      await action();
    } catch (error) {
      setActionError(t("UpdateActionFailed", message(error)));
    }
  };

  return (
    <div className="settings-dialog-body">
      <section className="settings-section">
        <h3>{t("AboutApplication")}</h3>
        <div className="settings-row is-static">
          <span>{t("CurrentVersion")}</span>
          <span className="settings-row-value">{updateState?.currentVersion ?? "—"}</span>
        </div>
        <div className="settings-row is-static">
          <span>{t("Distribution")}</span>
          <span className="settings-row-value">
            {updateState === null
              ? "—"
              : t(distributionLabel(updateState.mode))}
          </span>
        </div>
      </section>

      <section className="settings-section">
        <h3>{t("Updates")}</h3>
        <div className="update-status-card" role="status" aria-live="polite">
          {updateState === null ? t("UpdateDesktopOnly") : updateStatusText(updateState, t)}
        </div>
        {updateState?.status === "downloading" && (
          <progress
            className="update-progress"
            max={100}
            value={updateState.progressPercent ?? 0}
            aria-label={t("UpdateDownloadingStatus", updateState.availableVersion ?? "", Math.round(updateState.progressPercent ?? 0))}
          />
        )}
        <div className="settings-action-row update-actions">
          {canCheck && (
            <LiquidGlassButton
              disabled={busy || updateState.status === "downloaded"}
              onClick={() => void run(() => nativeApi().checkForUpdates())}
            >
              <RefreshIcon size={12} />
              {t("CheckForUpdates")}
            </LiquidGlassButton>
          )}
          {updateState?.status === "available" && updateState.mode === "installed" && (
            <LiquidGlassButton
              variant="primary"
              onClick={() => void run(() => nativeApi().downloadUpdate())}
            >
              {t("DownloadUpdate")}
            </LiquidGlassButton>
          )}
          {updateState?.status === "downloaded" && updateState.mode === "installed" && (
            <LiquidGlassButton
              variant="primary"
              onClick={() => void run(() => nativeApi().installUpdate())}
            >
              {t("RestartAndInstall")}
            </LiquidGlassButton>
          )}
          {(updateState === null
            || updateState.mode !== "installed"
            || updateState.status === "error") && isElectron() && (
            <LiquidGlassButton onClick={() => void run(() => nativeApi().openUpdatePage())}>
              {t("OpenReleasePage")}
            </LiquidGlassButton>
          )}
        </div>
        {updateState?.mode === "portable" && (
          <p className="settings-note">{t("UpdatePortableNote")}</p>
        )}
        {updateState?.mode === "development" && (
          <p className="settings-note">{t("UpdateDevelopmentStatus")}</p>
        )}
        {(stateError || actionError) && (
          <p className="settings-error">{actionError ?? stateError}</p>
        )}
      </section>
    </div>
  );
}

function distributionLabel(mode: AppUpdateState["mode"]): string {
  if (mode === "installed") return "DistributionInstalled";
  if (mode === "portable") return "DistributionPortable";
  return "DistributionDevelopment";
}

function updateStatusText(
  state: AppUpdateState,
  t: ReturnType<typeof useT>,
): string {
  switch (state.status) {
    case "disabled":
      return t("UpdateDevelopmentStatus");
    case "idle":
      return t("UpdateReadyStatus");
    case "checking":
      return t("UpdateCheckingStatus");
    case "available":
      return t("UpdateAvailableStatus", state.availableVersion ?? "");
    case "up-to-date":
      return t("UpdateUpToDateStatus");
    case "downloading":
      return t(
        "UpdateDownloadingStatus",
        state.availableVersion ?? "",
        Math.round(state.progressPercent ?? 0),
      );
    case "downloaded":
      return t("UpdateDownloadedStatus", state.availableVersion ?? "");
    case "installing":
      return t("UpdateInstallingStatus");
    case "error":
      return t("UpdateErrorStatus");
  }
}

function BehaviorPane() {
  const t = useT();
  const settings = useLumina((s) => s.settings);
  const updateSettings = useLumina((s) => s.updateSettings);
  return (
    <section className="settings-section">
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
  );
}

const GLASS_MODES: { value: GlassMode; labelKey: string }[] = [
  { value: "standard", labelKey: "GlassModeStandard" },
  { value: "polar", labelKey: "GlassModePolar" },
  { value: "prominent", labelKey: "GlassModeProminent" },
  { value: "shader", labelKey: "GlassModeShader" },
];

function GlassPane() {
  const t = useT();
  const glass = useLumina((s) => s.settings.glass);
  const updateSettings = useLumina((s) => s.updateSettings);
  const previewRef = useRef<HTMLDivElement | null>(null);

  const update = (patch: Partial<GlassConfig>) =>
    updateSettings({ glass: { ...glass, ...patch } });

  return (
    <div className="glass-pane">
      <div className="glass-preview" ref={previewRef}>
        <LiquidGlass
          mode={glass.mode}
          displacementScale={glass.displacementScale}
          blurAmount={glass.blurAmount}
          saturation={glass.saturation}
          aberrationIntensity={glass.aberrationIntensity}
          elasticity={glass.elasticity}
          cornerRadius={glass.cornerRadius}
          overLight={glass.overLight}
          mouseContainer={previewRef}
          padding="18px 22px"
          style={{ position: "absolute", top: "40%", left: "50%" }}
        >
          <div className="glass-demo-card">
            <span className="glass-demo-avatar">JD</span>
            <div className="glass-demo-id">
              <strong>John Doe</strong>
              <span>Software Engineer</span>
            </div>
            <div className="glass-demo-meta">
              <span>john.doe@example.com</span>
              <span>San Francisco, CA</span>
            </div>
          </div>
        </LiquidGlass>
        <LiquidGlass
          mode={glass.mode}
          displacementScale={glass.displacementScale}
          blurAmount={glass.blurAmount}
          saturation={glass.saturation}
          aberrationIntensity={glass.aberrationIntensity}
          elasticity={glass.elasticity}
          cornerRadius={Math.max(glass.cornerRadius, 18)}
          overLight={glass.overLight}
          mouseContainer={previewRef}
          padding="10px 26px"
          onClick={() => undefined}
          style={{ position: "absolute", top: "86%", left: "50%" }}
        >
          <span className="glass-demo-logout">Log out</span>
        </LiquidGlass>
      </div>

      <section className="settings-section glass-controls">
        <label className="settings-row">
          <span title={t("GlassMode")}>{t("GlassMode")}</span>
          <select
            className="lg-input"
            value={glass.mode}
            onChange={(e) => update({ mode: e.currentTarget.value as GlassMode })}
          >
            {GLASS_MODES.map(({ value, labelKey }) => (
              <option key={value} value={value}>
                {t(labelKey)}
              </option>
            ))}
          </select>
        </label>

        <GlassSlider
          label={t("GlassDisplacement")}
          hint={t("GlassDisplacementHint")}
          value={glass.displacementScale}
          min={0}
          max={200}
          step={1}
          format={(v) => String(Math.round(v))}
          onChange={(displacementScale) => update({ displacementScale })}
        />
        <GlassSlider
          label={t("GlassBlur")}
          hint={t("GlassBlurHint")}
          value={glass.blurAmount}
          min={0}
          max={1}
          step={0.01}
          format={(v) => v.toFixed(2)}
          onChange={(blurAmount) => update({ blurAmount })}
        />
        <GlassSlider
          label={t("GlassSaturation")}
          hint={t("GlassSaturationHint")}
          value={glass.saturation}
          min={100}
          max={300}
          step={5}
          format={(v) => `${Math.round(v)}%`}
          onChange={(saturation) => update({ saturation })}
        />
        <GlassSlider
          label={t("GlassAberration")}
          hint={t("GlassAberrationHint")}
          value={glass.aberrationIntensity}
          min={0}
          max={20}
          step={0.5}
          format={(v) => v.toFixed(1)}
          onChange={(aberrationIntensity) => update({ aberrationIntensity })}
        />
        <GlassSlider
          label={t("GlassElasticity")}
          hint={t("GlassElasticityHint")}
          value={glass.elasticity}
          min={0}
          max={1}
          step={0.01}
          format={(v) => v.toFixed(2)}
          onChange={(elasticity) => update({ elasticity })}
        />
        <GlassSlider
          label={t("GlassCornerRadius")}
          hint={t("GlassCornerRadiusHint")}
          value={glass.cornerRadius}
          min={0}
          max={100}
          step={1}
          format={(v) => `${Math.round(v)}px`}
          onChange={(cornerRadius) => update({ cornerRadius })}
        />

        <label className="settings-row" title={t("GlassOverLightHint")}>
          <span>{t("GlassOverLight")}</span>
          <input
            type="checkbox"
            className="lg-switch"
            checked={glass.overLight}
            onChange={(e) => update({ overLight: e.currentTarget.checked })}
          />
        </label>

        <div className="glass-reset-row">
          <LiquidGlassButton
            size="compact"
            onClick={() => update(DEFAULT_GLASS_CONFIG)}
          >
            <RefreshIcon size={12} />
            {t("ResetDefaults")}
          </LiquidGlassButton>
        </div>
      </section>
    </div>
  );
}

function GlassSlider({
  label,
  hint,
  value,
  min,
  max,
  step,
  format,
  onChange,
}: {
  label: string;
  hint: string;
  value: number;
  min: number;
  max: number;
  step: number;
  format(value: number): string;
  onChange(value: number): void;
}) {
  return (
    <label className="glass-slider" title={hint}>
      <span className="glass-slider-label">{label}</span>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(e) => onChange(Number(e.currentTarget.value))}
      />
      <span className="glass-slider-value">{format(value)}</span>
    </label>
  );
}

function message(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
