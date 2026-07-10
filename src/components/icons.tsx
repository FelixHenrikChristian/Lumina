import type { ReactElement, SVGProps } from "react";
import type { FileGlyphKind } from "../core/models";

// Stroke icon set drawn on a 24px grid, sized via the CSS `--icon-size` of
// the parent (defaults to 16px). Fluent-glyph replacements for the WinUI app.
type IconProps = SVGProps<SVGSVGElement> & { size?: number };

function Icon({ size = 16, children, ...rest }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...rest}
    >
      {children}
    </svg>
  );
}

export const FolderIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M3 7a2 2 0 0 1 2-2h4l2 2.5h8a2 2 0 0 1 2 2V17a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z" />
  </Icon>
);

export const TagIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12.6 3.6 20.4 11.4a2 2 0 0 1 0 2.8l-6.2 6.2a2 2 0 0 1-2.8 0L3.6 12.6A2 2 0 0 1 3 11.2V5a2 2 0 0 1 2-2h6.2a2 2 0 0 1 1.4.6Z" />
    <circle cx="8.5" cy="8.5" r="1.4" fill="currentColor" stroke="none" />
  </Icon>
);

export const SettingsIcon = (p: IconProps) => (
  <Icon {...p}>
    <circle cx="12" cy="12" r="3.2" />
    <path d="M12 2.8 13 5.4a7 7 0 0 1 2.3 1l2.7-.8 1.6 2.8-2 2a7 7 0 0 1 0 2.6l2 2-1.6 2.8-2.7-.8a7 7 0 0 1-2.3 1L12 21.2 11 18.6a7 7 0 0 1-2.3-1l-2.7.8-1.6-2.8 2-2a7 7 0 0 1 0-2.6l-2-2L6 6.4l2.7.8a7 7 0 0 1 2.3-1Z" />
  </Icon>
);

export const InfoIcon = (p: IconProps) => (
  <Icon {...p}>
    <circle cx="12" cy="12" r="9" />
    <path d="M12 10.5V17" />
    <circle cx="12" cy="7.2" r="0.8" fill="currentColor" stroke="none" />
  </Icon>
);

export const PlusIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12 5v14M5 12h14" />
  </Icon>
);

export const MinusIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M5 12h14" />
  </Icon>
);

export const CloseIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="m6 6 12 12M18 6 6 18" />
  </Icon>
);

export const CheckIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="m5 12.5 5 5L19 7" />
  </Icon>
);

export const BackIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M19 12H5m0 0 6-6m-6 6 6 6" />
  </Icon>
);

export const ForwardIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M5 12h14m0 0-6-6m6 6-6 6" />
  </Icon>
);

export const UpIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12 19V5m0 0-6 6m6-6 6 6" />
  </Icon>
);

export const RefreshIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M20 12a8 8 0 1 1-2.4-5.7M20 3.5V8h-4.5" />
  </Icon>
);

export const SearchIcon = (p: IconProps) => (
  <Icon {...p}>
    <circle cx="11" cy="11" r="6.5" />
    <path d="m16 16 5 5" />
  </Icon>
);

export const TrashIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M4 7h16M9.5 7V5a1.5 1.5 0 0 1 1.5-1.5h2A1.5 1.5 0 0 1 14.5 5v2M6.5 7l1 12.2A1.8 1.8 0 0 0 9.3 21h5.4a1.8 1.8 0 0 0 1.8-1.8l1-12.2M10 11v6M14 11v6" />
  </Icon>
);

export const EditIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M4 20h4.5L20 8.5a2.1 2.1 0 0 0-3-3L5.5 17 4 20ZM14.5 7 17 9.5" />
  </Icon>
);

export const RenameIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M10 4h4M12 4v16M10 20h4M3 8h5v8H3M16 8h5v8h-5" opacity={0.9} />
  </Icon>
);

export const SortIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M7 5v14m0 0-3.5-3.5M7 19l3.5-3.5M17 19V5m0 0-3.5 3.5M17 5l3.5 3.5" />
  </Icon>
);

export const FilterIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M4 5h16l-6.2 7.2V19l-3.6-1.8v-5L4 5Z" />
  </Icon>
);

export const ChevronDownIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="m6 9 6 6 6-6" />
  </Icon>
);

export const ChevronRightIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="m9 6 6 6-6 6" />
  </Icon>
);

export const ChevronLeftIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="m15 6-6 6 6 6" />
  </Icon>
);

export const MoreIcon = (p: IconProps) => (
  <Icon {...p}>
    <circle cx="5" cy="12" r="1.3" fill="currentColor" stroke="none" />
    <circle cx="12" cy="12" r="1.3" fill="currentColor" stroke="none" />
    <circle cx="19" cy="12" r="1.3" fill="currentColor" stroke="none" />
  </Icon>
);

export const ExportIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12 15V3m0 0L8 7m4-4 4 4M5 15v4a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-4" />
  </Icon>
);

export const ImportIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M12 3v12m0 0-4-4m4 4 4-4M5 15v4a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-4" />
  </Icon>
);

export const OpenIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M14 4h6v6M20 4l-9 9M9 5H6a2 2 0 0 0-2 2v11a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-3" />
  </Icon>
);

export const ImageIcon = (p: IconProps) => (
  <Icon {...p}>
    <rect x="3.5" y="4.5" width="17" height="15" rx="2" />
    <circle cx="9" cy="10" r="1.6" />
    <path d="m5 18 5-5 3 3 3.5-3.5L20 16" />
  </Icon>
);

export const VideoIcon = (p: IconProps) => (
  <Icon {...p}>
    <rect x="3" y="6" width="13" height="12" rx="2" />
    <path d="m16 10 5-3v10l-5-3" />
  </Icon>
);

export const AudioIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M9 18V6l10-2v12" />
    <circle cx="6.5" cy="18" r="2.5" />
    <circle cx="16.5" cy="16" r="2.5" />
  </Icon>
);

export const DocumentIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M7 3h7l4 4v12a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z" />
    <path d="M14 3v4h4M9 12h6M9 16h6" />
  </Icon>
);

export const ArchiveIcon = (p: IconProps) => (
  <Icon {...p}>
    <rect x="3.5" y="4" width="17" height="5" rx="1.2" />
    <path d="M5 9v9a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V9M10 13h4" />
  </Icon>
);

export const FileIcon = (p: IconProps) => (
  <Icon {...p}>
    <path d="M7 3h7l4 4v12a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z" />
    <path d="M14 3v4h4" />
  </Icon>
);

const GLYPHS: Record<FileGlyphKind, (p: IconProps) => ReactElement> = {
  folder: FolderIcon,
  image: ImageIcon,
  video: VideoIcon,
  audio: AudioIcon,
  document: DocumentIcon,
  archive: ArchiveIcon,
  generic: FileIcon,
};

export function GlyphIcon({ kind, ...rest }: IconProps & { kind: FileGlyphKind }) {
  const Component = GLYPHS[kind];
  return <Component {...rest} />;
}
