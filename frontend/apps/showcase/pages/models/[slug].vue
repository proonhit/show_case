<script setup lang="ts">
import type { ModelInfo } from '@showcase/viewer-core';
import { categoryOf, formatBytes, site, tagsOf, termName, text } from '~/composables/site';

const route = useRoute();
const { t, locale } = useI18n();
const localePath = useLocalePath();

const index = site.models.findIndex((m) => m.slug === route.params.slug);
if (index < 0) throw createError({ statusCode: 404, statusMessage: 'Model not found', fatal: true });
const model = site.models[index];
const prev = site.models[(index - 1 + site.models.length) % site.models.length];
const next = site.models[(index + 1) % site.models.length];
const info = computed(() => text(model, locale.value));
const live = ref<ModelInfo | null>(null);

const MAP_NAMES: Record<string, string> = { baseColor: 'Base', metallicRoughness: 'M/R', normal: 'Normal', occlusion: 'AO', emissive: 'Emissive' };
const specs = computed(() => {
  const s = model.stats;
  const rows: { k: string; v: string }[] = [];
  const tris = s?.triangles ?? live.value?.triangles;
  if (tris != null) rows.push({ k: t('detail.triangles'), v: tris.toLocaleString(locale.value) });
  if (s) rows.push({ k: t('detail.vertices'), v: s.vertices.toLocaleString(locale.value) });
  const meshes = s?.meshes ?? live.value?.meshes;
  const materials = s?.materials ?? live.value?.materials;
  if (meshes != null) rows.push({ k: t('detail.meshesMaterials'), v: `${meshes} / ${materials ?? '—'}` });
  if (s?.textures) rows.push({ k: t('detail.textures'), v: `${s.textures} · ${s.maxTextureSize} px` });
  if (s?.availableMaps.length) rows.push({ k: t('detail.maps'), v: s.availableMaps.map((m) => MAP_NAMES[m] ?? m).join(' · ') });
  const anim = s?.hasAnimation ?? (live.value ? live.value.animations > 0 : null);
  if (anim != null) rows.push({ k: t('detail.animation'), v: anim ? t('detail.yes') : t('detail.no') });
  if (model.software) rows.push({ k: t('detail.software'), v: model.software });
  if (model.hoursSpent) rows.push({ k: t('detail.hours'), v: t('detail.hoursValue', { n: model.hoursSpent }) });
  if (model.modelBytes) rows.push({ k: t('detail.file'), v: `.glb · ${formatBytes(model.modelBytes)}${model.sourceFormat === 'fbx' ? ` (${t('detail.fromFbx')})` : ''}` });
  return rows;
});

const meta = computed(() => {
  const date = model.completedAt ? new Date(model.completedAt).toLocaleDateString(locale.value, { month: 'short', year: 'numeric' }) : null;
  return [termName(categoryOf(model), locale.value), model.software, date].filter(Boolean).join(' · ');
});

const copied = ref(false);
async function copyLink() {
  try {
    await navigator.clipboard.writeText(location.href);
    copied.value = true;
    setTimeout(() => (copied.value = false), 2000);
  } catch {
    // clipboard blocked: nothing to do
  }
}

usePageSeo({
  title: () => `${info.value.title} — ${site.profile.displayName}`,
  description: () => (info.value.description ?? `${info.value.title} · ${meta.value}`).slice(0, 200),
  image: model.thumbnailUrl,
  type: 'article',
  // schema.org 3DModel: search engines can show the model as a 3D asset.
  jsonLd: (absoluteUrl) => ({
    '@type': '3DModel',
    name: info.value.title,
    description: info.value.description ?? undefined,
    inLanguage: locale.value,
    image: absoluteUrl(model.thumbnailUrl),
    creator: { '@type': 'Person', name: site.profile.displayName },
    dateCreated: model.completedAt ?? undefined,
    keywords: tagsOf(model).map((tg) => termName(tg, locale.value)).join(', ') || undefined,
    encoding: model.modelUrl ? [{ '@type': 'MediaObject', contentUrl: absoluteUrl(model.modelUrl), encodingFormat: 'model/gltf-binary' }] : undefined,
  }),
});
</script>

<template>
  <div class="container page">
    <div class="top">
      <nav :aria-label="t('nav.works')" class="crumbs">
        <NuxtLink :to="localePath('/')">{{ t('nav.works') }}</NuxtLink>
        <template v-if="categoryOf(model)"><span aria-hidden="true">›</span><span>{{ termName(categoryOf(model), locale) }}</span></template>
        <span aria-hidden="true">›</span><span aria-current="page">{{ info.title }}</span>
      </nav>
      <div v-if="site.models.length > 1" class="pager">
        <NuxtLink :to="localePath(`/models/${prev.slug}`)" rel="prev" class="btn">← {{ t('detail.previous') }}</NuxtLink>
        <NuxtLink :to="localePath(`/models/${next.slug}`)" rel="next" class="btn">{{ t('detail.next') }} →</NuxtLink>
      </div>
    </div>

    <div class="main">
      <div class="viewport">
        <ModelStage v-if="model.modelUrl" :src="model.modelUrl" :mobile-src="model.mobileModelUrl" :settings="model.viewerSettings" :poster="model.thumbnailUrl"
          :title="info.title" @info="live = $event" />
        <template v-else>
          <img v-if="model.thumbnailUrl" :src="model.thumbnailUrl" :alt="info.title" class="still" />
          <p class="muted waiting">{{ t('detail.noModel') }}</p>
        </template>
      </div>

      <aside class="side">
        <div>
          <h1>{{ info.title }}</h1>
          <p v-if="meta" class="muted">{{ meta }}</p>
        </div>
        <div v-if="tagsOf(model).length" class="tags">
          <span v-for="tg in tagsOf(model)" :key="tg.slug" class="chip">#{{ termName(tg, locale) }}</span>
        </div>
        <p v-if="info.description" class="description">{{ info.description }}</p>
        <section v-if="specs.length" class="card specs" aria-labelledby="specs-title">
          <h2 id="specs-title">{{ t('detail.specs') }}</h2>
          <dl>
            <div v-for="s in specs" :key="s.k"><dt>{{ s.k }}</dt><dd>{{ s.v }}</dd></div>
          </dl>
        </section>
        <button type="button" class="btn" @click="copyLink">{{ copied ? t('detail.copied') : t('detail.copy') }}</button>
      </aside>
    </div>

    <section v-if="model.images.length" class="gallery" aria-labelledby="gallery-title">
      <h2 id="gallery-title">{{ t('detail.gallery') }}</h2>
      <div class="grid">
        <figure v-for="img in model.images" :key="img.id">
          <a v-if="img.url" :href="img.url" target="_blank" rel="noopener">
            <img :src="img.smallUrl ?? img.url" :alt="(locale === 'vi' ? img.captionVi : img.captionEn) || info.title" loading="lazy" decoding="async" />
          </a>
          <figcaption v-if="img.captionEn || img.captionVi" class="muted">{{ locale === 'vi' ? img.captionVi || img.captionEn : img.captionEn || img.captionVi }}</figcaption>
        </figure>
      </div>
    </section>
  </div>
</template>

<style scoped>
.page { display: flex; flex-direction: column; gap: 24px; padding-top: 28px; padding-bottom: 80px; }
.top { display: flex; flex-wrap: wrap; gap: 12px; justify-content: space-between; align-items: center; }
.crumbs { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; font-size: 13px; color: var(--muted); }
.crumbs a { color: var(--muted); text-decoration: none; }
.crumbs [aria-current] { color: var(--text); }
.pager { display: flex; gap: 8px; }
.main { display: flex; flex-wrap: wrap; gap: 32px; align-items: flex-start; }
.viewport { flex: 999 1 560px; min-width: 0; height: min(640px, 75vh); position: relative; border-radius: 20px; background: var(--viewport); border: 1px solid var(--border); overflow: hidden; }
.still { width: 100%; height: 100%; object-fit: cover; }
.waiting { position: absolute; inset: auto 0 20px; text-align: center; margin: 0; }
.side { flex: 1 1 340px; min-width: 0; display: flex; flex-direction: column; gap: 20px; align-items: flex-start; }
.side > div:first-child p { margin: 8px 0 0; }
h1 { font-size: 36px; line-height: 44px; font-weight: 700; letter-spacing: -.8px; }
.tags { display: flex; flex-wrap: wrap; gap: 8px; }
.tags .chip { cursor: default; }
.description { margin: 0; line-height: 24px; color: var(--muted); white-space: pre-line; }
.specs { align-self: stretch; padding: 14px 18px 6px; }
.specs h2 { font-size: 12px; font-weight: 500; letter-spacing: .6px; text-transform: uppercase; color: var(--muted); margin-bottom: 4px; }
.specs dl { margin: 0; }
.specs dl div { display: flex; justify-content: space-between; gap: 12px; padding: 9px 0; border-top: 1px solid var(--border); }
.specs dt { font-size: 13px; color: var(--muted); }
.specs dd { margin: 0; font-family: var(--mono); font-size: 13px; text-align: right; }
.gallery { display: flex; flex-direction: column; gap: 16px; padding-top: 24px; }
.gallery h2 { font-size: 24px; line-height: 32px; font-weight: 600; }
.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 240px), 1fr)); gap: 24px; }
figure { margin: 0; display: flex; flex-direction: column; gap: 10px; }
figure img { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 14px; background: var(--viewport); display: block; }
@media (max-width: 700px) { .viewport { height: 70vh; border-radius: 14px; } h1 { font-size: 28px; line-height: 36px; } }
</style>
