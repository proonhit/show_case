<script setup lang="ts">
// WebGL viewer for one model. Client-only (".client.vue"): the static HTML shows the poster instead.
import type { CameraView, ModelInfo } from '@showcase/viewer-core';
import { MAP_MODES, availableModes, modeForKey, type MapMode } from '@showcase/viewer-core/modes';
import type { ViewerSettings } from '~/composables/site';

// three.js (~600 KB) is its own chunk, fetched once the page is idle: text and the poster image
// show first, then the live viewer takes over.
const ModelViewerView = defineAsyncComponent(() => import('@showcase/viewer-core/vue').then((m) => m.ModelViewerView));
const started = ref(false);
onMounted(() => {
  const start = () => (started.value = true);
  if ('requestIdleCallback' in window) window.requestIdleCallback(start, { timeout: 1500 });
  else setTimeout(start, 200);
});

const props = defineProps<{ src: string; settings: ViewerSettings; poster: string | null; title: string }>();
const emit = defineEmits<{ info: [info: ModelInfo] }>();
const { t } = useI18n();

const mode = ref<MapMode>('final');
const spin = ref(props.settings.autoRotate);
const info = ref<ModelInfo | null>(null);
const progress = ref<number | null>(null);
const error = ref('');
const available = computed(() => info.value?.available ?? availableModes([]));
const viewer = ref<{ resetCamera(): void; toggleFullscreen(): void } | null>(null);
const camera = computed<CameraView | null>(() =>
  props.settings.cameraPosition && props.settings.cameraTarget
    ? { position: props.settings.cameraPosition as CameraView['position'], target: props.settings.cameraTarget as CameraView['target'] }
    : null);

function onLoad(i: ModelInfo) {
  info.value = i;
  emit('info', i);
  const wanted = props.settings.defaultMode as MapMode;
  if (i.available[wanted]) mode.value = wanted;
}

function onKey(e: KeyboardEvent) {
  const m = modeForKey(e.key);
  if (m && available.value[m]) mode.value = m;
}

const badge = computed(() => {
  const label = t(`modes.${mode.value}`);
  if (mode.value === 'wireframe' && info.value) return `${label} · ${info.value.triangles.toLocaleString()} tris`;
  return { metallic: `${label} (B)`, roughness: `${label} (G)`, ao: `${label} (R)` }[mode.value as string] ?? label;
});
</script>

<template>
  <div class="stage" tabindex="0" :aria-label="`${title}: ${t('detail.hint')}`" @keydown="onKey">
    <ModelViewerView v-if="started" ref="viewer" :src="src" v-model:mode="mode" v-model:autoRotate="spin"
      :auto-rotate-speed="settings.autoRotateSpeed" :background="settings.background ?? null" :camera="camera"
      @progress="progress = $event.ratio" @load="onLoad" @error="error = $event.message" />
    <img v-if="!info && poster" :src="poster" alt="" class="poster" />
    <p v-if="!info && !error" class="overlay muted" role="status">{{ t('detail.loading', { pct: progress != null ? `${Math.round(progress * 100)}%` : '' }) }}</p>
    <p v-if="error" class="overlay error" role="alert">{{ t('detail.error', { msg: error }) }}</p>
    <span v-if="info" class="badge"><span class="dot" />{{ badge }}</span>
    <span class="hint muted">{{ t('detail.hint') }}</span>
    <div class="controls">
      <div role="group" :aria-label="t('detail.mapMode')" class="modes">
        <button v-for="(m, i) in MAP_MODES" :key="m" type="button" :aria-pressed="mode === m" :disabled="!info || !available[m]"
          :title="info && !available[m] ? t('detail.modeUnavailable', { mode: t(`modes.${m}`) }) : undefined" @click="mode = m">
          <span class="key" aria-hidden="true">{{ i + 1 }}</span>{{ t(`modes.${m}`) }}
        </button>
      </div>
      <div class="tools">
        <button type="button" class="tool" :aria-label="t('detail.turntable')" :aria-pressed="spin" @click="spin = !spin">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round" aria-hidden="true"><path d="M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8M21 3v5h-5" /></svg>
        </button>
        <button type="button" class="tool" :aria-label="t('detail.reset')" :disabled="!info" @click="viewer?.resetCamera()">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round" aria-hidden="true"><path d="M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1Z" /></svg>
        </button>
        <button type="button" class="tool" :aria-label="t('detail.fullscreen')" :disabled="!info" @click="viewer?.toggleFullscreen()">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M8 3H5a2 2 0 0 0-2 2v3M21 8V5a2 2 0 0 0-2-2h-3M3 16v3a2 2 0 0 0 2 2h3M16 21h3a2 2 0 0 0 2-2v-3" /></svg>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.stage { position: relative; width: 100%; height: 100%; }
.stage:focus-visible { outline: 2px solid var(--accent); outline-offset: -2px; }
.poster { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; opacity: .6; }
.overlay { position: absolute; inset: 0; margin: 0; display: flex; align-items: center; justify-content: center; text-align: center; padding: 24px; pointer-events: none; }
.badge { position: absolute; top: 20px; left: 20px; display: flex; align-items: center; gap: 8px; padding: 6px 12px; border-radius: 999px; background: var(--surface); font-size: 13px; font-weight: 500; }
.dot { width: 8px; height: 8px; border-radius: 50%; background: var(--accent); }
.hint { position: absolute; top: 24px; right: 20px; font-size: 13px; }
.controls { position: absolute; left: 20px; right: 20px; bottom: 20px; display: flex; flex-wrap: wrap; gap: 12px; justify-content: space-between; align-items: flex-end; }
.modes { display: flex; gap: 4px; padding: 4px; border-radius: 12px; background: var(--surface); border: 1px solid var(--border); overflow-x: auto; max-width: 100%; scrollbar-width: none; }
.modes button { display: flex; align-items: center; gap: 6px; flex: none; min-height: 36px; padding: 6px 10px; border-radius: 8px; border: 0; background: none; color: var(--text); font: 500 13px var(--font); cursor: pointer; white-space: nowrap; }
.modes button[aria-pressed='true'] { background: var(--accent); color: var(--on-accent); }
.modes button:disabled { color: var(--muted); opacity: .45; cursor: not-allowed; }
.key { font-family: var(--mono); opacity: .7; }
.tools { display: flex; gap: 8px; }
.tool { width: 40px; height: 40px; border-radius: 10px; background: var(--surface); border: 1px solid var(--border); color: var(--text); display: flex; align-items: center; justify-content: center; cursor: pointer; }
.tool[aria-pressed='true'] { color: var(--accent); border-color: var(--accent); }
.tool:disabled { opacity: .5; cursor: not-allowed; }
@media (max-width: 700px) {
  .hint, .key { display: none; }
  .controls { left: 12px; right: 12px; bottom: 12px; }
  .badge { top: 12px; left: 12px; }
}
</style>
