<script setup lang="ts">
import { site } from '~/composables/site';

const { t, locale } = useI18n();
const localePath = useLocalePath();
const switchLocalePath = useSwitchLocalePath();
const route = useRoute();

// The real theme is set before paint by a head script; this mirrors it once mounted.
const theme = ref<'dark' | 'light'>('dark');
onMounted(() => (theme.value = document.documentElement.dataset.theme === 'light' ? 'light' : 'dark'));
function toggleTheme() {
  theme.value = theme.value === 'dark' ? 'light' : 'dark';
  document.documentElement.dataset.theme = theme.value;
  try {
    localStorage.setItem('showcase.theme', theme.value);
  } catch {
    // private mode: the choice lasts for this page only
  }
}

const onAbout = computed(() => route.path.endsWith('/about'));
</script>

<template>
  <header class="header">
    <div class="container bar">
      <NuxtLink :to="localePath('/')" class="brand">
        <span class="logo" aria-hidden="true">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16ZM3.3 7l8.7 5 8.7-5M12 22V12" /></svg>
        </span>
        <span>{{ site.profile.displayName || 'Portfolio' }}</span>
      </NuxtLink>
      <div class="right">
        <nav :aria-label="t('nav.works')" class="nav">
          <NuxtLink :to="localePath('/')" :class="{ on: !onAbout }">{{ t('nav.works') }}</NuxtLink>
          <NuxtLink :to="localePath('/about')" :class="{ on: onAbout }">{{ t('nav.about') }}</NuxtLink>
        </nav>
        <div class="tools">
          <div role="group" :aria-label="t('nav.language')" class="lang">
            <NuxtLink v-for="l in (['en', 'vi'] as const)" :key="l" :to="switchLocalePath(l)" :aria-current="locale === l ? 'true' : undefined"
              :lang="l" :hreflang="l">{{ l.toUpperCase() }}</NuxtLink>
          </div>
          <button type="button" class="theme" :aria-label="theme === 'dark' ? t('nav.toLight') : t('nav.toDark')" @click="toggleTheme">
            <svg v-if="theme === 'dark'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8zM12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41" /></svg>
            <svg v-else width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z" /></svg>
          </button>
        </div>
      </div>
    </div>
  </header>
</template>

<style scoped>
.header { border-bottom: 1px solid var(--border); }
.bar { display: flex; flex-wrap: wrap; gap: 12px 16px; align-items: center; justify-content: space-between; padding-top: 14px; padding-bottom: 14px; }
.brand { display: flex; align-items: center; gap: 10px; color: var(--text); text-decoration: none; font-weight: 500; }
.logo { width: 32px; height: 32px; border-radius: 8px; background: var(--accent); color: var(--on-accent); display: flex; align-items: center; justify-content: center; }
.right { display: flex; flex-wrap: wrap; align-items: center; gap: 12px 32px; }
.nav { display: flex; gap: 24px; }
.nav a { color: var(--muted); font-weight: 500; text-decoration: none; padding: 8px 0; }
.nav a.on, .nav a:hover { color: var(--text); }
.tools { display: flex; align-items: center; gap: 12px; }
.lang { display: flex; gap: 2px; padding: 3px; border-radius: 999px; background: var(--surface2); border: 1px solid var(--border); }
.lang a { padding: 6px 12px; border-radius: 999px; color: var(--muted); font-size: 13px; font-weight: 500; text-decoration: none; }
.lang a[aria-current='true'] { background: var(--surface); color: var(--text); }
.theme { width: 40px; height: 40px; border-radius: 999px; background: var(--surface2); border: 1px solid var(--border); color: var(--text); display: flex; align-items: center; justify-content: center; cursor: pointer; }
</style>
