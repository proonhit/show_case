import data from '~/data/site.json';

// Shapes of data/site.json (the API's public DTOs, with file URLs rewritten to /media/…).
export interface Translation { locale: string; title: string; description: string | null }
export interface Term { id: string; nameEn: string; nameVi: string; slug: string; sortOrder?: number }
export interface ImageItem { id: string; kind: string; url: string | null; captionEn: string | null; captionVi: string | null; sortOrder: number }
export interface ViewerSettings {
  autoRotate: boolean; autoRotateSpeed: number; background: string | null; defaultMode: string;
  cameraPosition: number[] | null; cameraTarget: number[] | null;
}
export interface Stats {
  triangles: number; vertices: number; meshes: number; materials: number; textures: number;
  maxTextureSize: number; hasAnimation: boolean; availableMaps: string[];
}
export interface SiteModel {
  id: string; slug: string; isFeatured: boolean; sortOrder: number; categorySlug: string | null; tagSlugs: string[];
  translations: Translation[]; software: string | null; hoursSpent: number | null; completedAt: string | null;
  stats: Stats | null; viewerSettings: ViewerSettings; modelUrl: string | null; modelBytes: number | null;
  sourceFormat: string | null; thumbnailUrl: string | null; images: ImageItem[]; updatedAt: string;
}
export interface Profile {
  displayName: string; headlineEn: string | null; headlineVi: string | null; bioEn: string | null; bioVi: string | null;
  links: Record<string, string>; avatarUrl: string | null; cvUrl: string | null;
}
export interface Site { generatedAt: string; profile: Profile; categories: Term[]; tags: Term[]; models: SiteModel[] }

export const site = data as unknown as Site;

export type Locale = 'en' | 'vi';

/** Title/description in the page language, falling back to English. */
export function text(m: SiteModel, locale: string) {
  const t = m.translations.find((x) => x.locale === locale) ?? m.translations.find((x) => x.locale === 'en') ?? m.translations[0];
  return { title: t?.title ?? m.slug, description: t?.description ?? null };
}

export const termName = (t: Term | undefined, locale: string) => (t ? (locale === 'vi' ? t.nameVi : t.nameEn) : '');
export const categoryOf = (m: SiteModel) => site.categories.find((c) => c.slug === m.categorySlug);
export const tagsOf = (m: SiteModel) => m.tagSlugs.map((s) => site.tags.find((t) => t.slug === s)).filter((t): t is Term => !!t);

export function formatTris(n: number | null | undefined): string {
  if (n == null) return '—';
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1).replace(/\.0$/, '')}M`;
  if (n >= 1_000) return `${Math.round(n / 1_000)}K`;
  return String(n);
}

export function formatBytes(bytes: number | null | undefined): string {
  if (!bytes) return '—';
  const mb = bytes / 1024 / 1024;
  return mb >= 1 ? `${mb.toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}
