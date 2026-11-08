<script setup lang="ts">
import { categoryOf, formatTris, termName, text, type SiteModel } from '~/composables/site';

const props = defineProps<{ model: SiteModel }>();
const { locale } = useI18n();
const localePath = useLocalePath();
const info = computed(() => text(props.model, locale.value));
const meta = computed(() => [termName(categoryOf(props.model), locale.value), props.model.software].filter(Boolean).join(' · '));
</script>

<template>
  <NuxtLink :to="localePath(`/models/${model.slug}`)" class="card">
    <span class="shot">
      <img v-if="model.thumbnailUrl" :src="model.thumbnailUrl" :alt="info.title" loading="lazy" decoding="async" width="400" height="300" />
      <svg v-else width="56" height="56" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16ZM3.3 7l8.7 5 8.7-5M12 22V12" /></svg>
    </span>
    <span class="body">
      <span class="title">{{ info.title }}</span>
      <span v-if="meta" class="muted">{{ meta }}</span>
      <span v-if="model.stats" class="mono muted">{{ formatTris(model.stats.triangles) }} tris</span>
    </span>
  </NuxtLink>
</template>

<style scoped>
.card { display: flex; flex-direction: column; border-radius: 14px; overflow: hidden; background: var(--surface); border: 1px solid var(--border); color: var(--text); text-decoration: none; transition: border-color .15s, transform .15s; }
.card:hover { border-color: var(--accent); color: var(--text); transform: translateY(-2px); }
.shot { aspect-ratio: 4 / 3; background: var(--viewport); display: flex; align-items: center; justify-content: center; color: var(--muted); }
.shot img { width: 100%; height: 100%; object-fit: cover; }
.body { padding: 14px 16px 16px; display: flex; flex-direction: column; gap: 6px; }
.title { font-size: 18px; line-height: 26px; font-weight: 600; }
@media (prefers-reduced-motion: reduce) { .card { transition: none; } .card:hover { transform: none; } }
</style>
