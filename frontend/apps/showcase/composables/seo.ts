import { site } from '~/composables/site';

/**
 * Head tags for one page: title/description, canonical, hreflang alternates (en, vi, x-default),
 * Open Graph/Twitter card and optional schema.org JSON-LD. URLs are absolute (SITE_URL).
 */
export function usePageSeo(opts: {
  title: () => string;
  description: () => string;
  image?: string | null;
  type?: 'website' | 'article' | 'profile';
  /** Gets `abs` to turn site paths into absolute URLs. */
  jsonLd?: (abs: (path: string | null | undefined) => string | undefined) => Record<string, unknown>;
}) {
  const { locale } = useI18n();
  const route = useRoute();
  const switchLocalePath = useSwitchLocalePath();
  const base = useRuntimeConfig().public.siteUrl as string;
  const abs = (path: string) => `${base}${path === '/' ? '' : path}`;

  useSeoMeta({
    title: opts.title,
    description: opts.description,
    ogTitle: opts.title,
    ogDescription: opts.description,
    ogType: opts.type ?? 'website',
    ogUrl: () => abs(route.path),
    ogSiteName: site.profile.displayName,
    ogLocale: () => (locale.value === 'vi' ? 'vi_VN' : 'en_US'),
    ogImage: opts.image ? abs(opts.image) : undefined,
    twitterCard: opts.image ? 'summary_large_image' : 'summary',
  });

  useHead(() => ({
    link: [
      { rel: 'canonical', href: abs(route.path) },
      { rel: 'alternate', hreflang: 'en', href: abs(switchLocalePath('en')) },
      { rel: 'alternate', hreflang: 'vi', href: abs(switchLocalePath('vi')) },
      { rel: 'alternate', hreflang: 'x-default', href: abs(switchLocalePath('en')) },
    ],
    script: opts.jsonLd
      ? [{ type: 'application/ld+json', innerHTML: JSON.stringify({ '@context': 'https://schema.org', ...opts.jsonLd((p) => (p ? abs(p) : undefined)) }) }]
      : [],
  }));
}
