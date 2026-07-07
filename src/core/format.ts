// Port of FileExplorerItemViewModel.FormatSize and ModifiedText.
const SIZE_UNITS = ["B", "KB", "MB", "GB", "TB"];

export function formatSize(bytes: number): string {
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < SIZE_UNITS.length - 1) {
    value /= 1024;
    unit++;
  }
  const text =
    unit === 0 ? String(Math.round(value)) : (Math.round(value * 10) / 10).toString();
  return `${text} ${SIZE_UNITS[unit]}`;
}

export function formatModified(epochMs: number | null): string {
  if (epochMs === null) return "";
  const d = new Date(epochMs);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
