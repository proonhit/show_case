<script setup lang="ts">
import { site } from '~/composables/site';

const { t, locale } = useI18n();
const p = site.profile;
const headline = computed(() => (locale.value === 'vi' ? p.headlineVi ?? p.headlineEn : p.headlineEn ?? p.headlineVi));
const bio = computed(() => (locale.value === 'vi' ? p.bioVi ?? p.bioEn : p.bioEn ?? p.bioVi));
const links = computed(() => Object.entries(p.links).map(([name, value]) => ({
  name,
  href: value.includes('@') && !value.startsWith('http') ? `mailto:${value}` : value,
  label: value.replace(/^https?:\/\/(www\.)?/, '').replace(/\/$/, ''),
})));

useSeoMeta({ title: () => `${t('about.title')} — ${p.displayName}`, description: () => headline.value ?? '' });
</script>

<template>
  <section class="container about">
    <div class="avatar">
      <img v-if="p.avatarUrl" :src="p.avatarUrl" :alt="p.displayName" width="200" height="200" />
      <span v-else aria-hidden="true">{{ p.displayName.slice(0, 1) }}</span>
    </div>
    <div class="text">
      <h1>{{ p.displayName }}</h1>
      <p v-if="headline" class="headline">{{ headline }}</p>
      <p v-for="(para, i) in (bio ?? '').split(/\n{2,}/).filter(Boolean)" :key="i" class="bio">{{ para }}</p>
      <a v-if="p.cvUrl" :href="p.cvUrl" class="btn primary big" download>{{ t('about.cv') }}</a>
      <p v-else class="muted">{{ t('about.noCv') }}</p>
      <section v-if="links.length" :aria-label="t('about.links')">
        <h2 class="muted small">{{ t('about.links') }}</h2>
        <ul class="links">
          <li v-for="l in links" :key="l.name"><span class="muted">{{ l.name }}</span><a :href="l.href" rel="me noopener" target="_blank">{{ l.label }}</a></li>
        </ul>
      </section>
    </div>
  </section>
</template>

<style scoped>
.about { display: flex; flex-wrap: wrap; gap: 48px; padding-top: 72px; padding-bottom: 80px; align-items: flex-start; }
.avatar { width: 200px; height: 200px; flex: none; border-radius: 50%; overflow: hidden; background: var(--surface2); display: flex; align-items: center; justify-content: center; font-size: 72px; font-weight: 700; color: var(--muted); }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.text { flex: 1 1 420px; min-width: 0; max-width: 720px; display: flex; flex-direction: column; gap: 16px; align-items: flex-start; }
.headline { margin: 0; font-size: 18px; color: var(--accent); }
.bio { margin: 0; line-height: 26px; color: var(--muted); white-space: pre-line; }
.big { font-size: 15px; min-height: 46px; padding: 12px 20px; }
.small { font-size: 12px; font-weight: 500; letter-spacing: .6px; text-transform: uppercase; margin: 8px 0; }
.links { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
.links li { display: flex; gap: 12px; }
.links li span { width: 100px; flex: none; }
@media (max-width: 560px) { .about { padding-top: 40px; } .avatar { width: 120px; height: 120px; font-size: 48px; } }
</style>
