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
  css: ['~/assets/css/main.css'],
  ssr: true,
  devtools: { enabled: false },
  app: {
    head: {
      link: [
        { rel: 'preconnect', href: 'https://fonts.googleapis.com' },
        { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' },
        { rel: 'stylesheet', href: 'https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;600;700&family=JetBrains+Mono:wght@400&display=swap' },
      ],
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
