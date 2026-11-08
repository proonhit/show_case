import { readFileSync } from 'node:fs';

// Static site: every page is generated at build time from data/site.json
// (written by scripts/fetch-data.mjs from the API, or from sample/ when no API is given).
const site = JSON.parse(readFileSync(new URL('./data/site.json', import.meta.url), 'utf8')) as { models: { slug: string }[] };
const modelRoutes = site.models.flatMap((m) => [`/en/models/${m.slug}`, `/vi/models/${m.slug}`]);
// Public address of the deployed site, for canonical/hreflang/Open Graph/sitemap (set in the build job).
const siteUrl = (process.env.SITE_URL ?? 'http://localhost:5175').replace(/\/$/, '');

export default defineNuxtConfig({
  compatibilityDate: '2024-11-01',
  modules: ['@nuxtjs/i18n'],
  // Fonts are self-hosted (latin + Vietnamese subsets only): no render-blocking third-party CSS.
  css: [
    ...[400, 500, 600, 700].flatMap((w) => [`@fontsource/be-vietnam-pro/latin-${w}.css`, `@fontsource/be-vietnam-pro/vietnamese-${w}.css`]),
    '@fontsource/jetbrains-mono/latin-400.css',
    '~/assets/css/main.css',
  ],
  ssr: true,
  devtools: { enabled: false },
  app: {
    head: {
      // Set the theme before first paint (no flash): saved choice, else the system setting.
      script: [{
        innerHTML: "try{var t=localStorage.getItem('showcase.theme');if(t!=='light'&&t!=='dark')t=matchMedia('(prefers-color-scheme: light)').matches?'light':'dark';document.documentElement.dataset.theme=t}catch(e){}",
        tagPosition: 'head',
      }],
    },
  },
  i18n: {
    strategy: 'prefix',
    defaultLocale: 'en',
    locales: [
      { code: 'en', language: 'en', name: 'English', file: 'en.json' },
      { code: 'vi', language: 'vi', name: 'Tiếng Việt', file: 'vi.json' },
    ],
    detectBrowserLanguage: false,
    bundle: { optimizeTranslationDirective: false },
  },
  nitro: {
    prerender: { crawlLinks: true, routes: ['/en', '/vi', '/en/about', '/vi/about', ...modelRoutes], failOnError: true },
  },
  runtimeConfig: { public: { siteUrl } },
  typescript: { strict: true },
});
