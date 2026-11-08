<script setup lang="ts">
import type { components } from '@showcase/api-client';
import { MAP_MODES, MODE_LABELS, availableModes, modeForKey, type CameraView, type MapMode, type ModelInfo } from '@showcase/viewer-core';
import { ModelViewerView } from '@showcase/viewer-core/vue';
import { computed, ref, watch } from 'vue';
import { formatTris } from '../format';

type Settings = components['schemas']['ViewerSettingsDto'];

/** `src` is a same-origin URL (the session cookie goes along). `settings` is the (draft) viewer setup. */
const props = defineProps<{ src: string | null; settings?: Settings | null }>();
const emit = defineEmits<{ thumbnail: [image: Blob]; camera: [view: CameraView]; available: [modes: Record<MapMode, boolean>] }>();

const mode = ref<MapMode>('final');
const spin = ref(props.settings?.autoRotate ?? true);
const busyShot = ref(false);

// Null background = transparent canvas, so the theme's viewport colour shows through.
const background = computed(() => props.settings?.background ?? null);
const speed = computed(() => props.settings?.autoRotateSpeed ?? 20);
const camera = computed<CameraView | null>(() => {
  const s = props.settings;
  return s?.cameraPosition && s.cameraTarget
    ? { position: s.cameraPosition as CameraView['position'], target: s.cameraTarget as CameraView['target'] }
    : null;
});
watch(() => props.settings?.autoRotate, (on) => on !== undefined && (spin.value = on));
watch(() => props.settings?.defaultMode, (m) => {
  if (m && info.value && available.value[m as MapMode]) mode.value = m as MapMode;
});
const info = ref<ModelInfo | null>(null);
const loading = ref(false);
const progress = ref<number | null>(null);
const error = ref('');
const available = computed(() => info.value?.available ?? availableModes([]));
const viewer = ref<{
  resetCamera(): void;
  toggleFullscreen(): void;
  getCamera(): CameraView | undefined;
  snapshot(): Promise<Blob> | undefined;
} | null>(null);

function onLoad(i: ModelInfo) {
  info.value = i;
  loading.value = false;
  emit('available', i.available);
  const wanted = props.settings?.defaultMode as MapMode | undefined;
  if (wanted && i.available[wanted]) mode.value = wanted;
}

async function takeThumbnail() {
  busyShot.value = true;
  try {
    const blob = await viewer.value?.snapshot();
    if (blob) emit('thumbnail', blob);
  } finally {
    busyShot.value = false;
  }
}

function useThisView() {
  const view = viewer.value?.getCamera();
  if (view) emit('camera', view);
}

watch(() => props.src, (src) => {
  info.value = null;
  error.value = '';
  mode.value = 'final';
  loading.value = !!src;
}, { immediate: true });

function onKey(e: KeyboardEvent) {
  const m = modeForKey(e.key);
  if (m && available.value[m]) mode.value = m;
}
</script>

<template>
  <section class="card preview" aria-label="Xem trước 3D">
    <div class="stage" tabindex="0" aria-label="Khung xem 3D: kéo để xoay, phím 1–7 đổi chế độ map" @keydown="onKey">
      <ModelViewerView v-if="src" ref="viewer" :src="src" v-model:mode="mode" v-model:autoRotate="spin"
        :auto-rotate-speed="speed" :background="background" :camera="camera"
        @progress="progress = $event.ratio" @load="onLoad"
        @error="error = $event.message; loading = false" />
      <p v-if="!src" class="overlay muted">Chưa có file .glb để xem trước. File .fbx sẽ xem được sau khi chuyển đổi (GĐ2).</p>
      <p v-else-if="loading" class="overlay muted">Đang tải{{ progress != null ? ` ${Math.round(progress * 100)}%` : '…' }}</p>
      <p v-if="error" class="overlay error" role="alert">Không mở được model: {{ error }}</p>
    </div>
    <div class="modes" role="group" aria-label="Chế độ map">
      <button v-for="(m, i) in MAP_MODES" :key="m" type="button" :aria-pressed="mode === m" :disabled="!info || !available[m]"
        :title="info && !available[m] ? 'Model không có map này' : `Phím ${i + 1}`" @click="mode = m">
        {{ MODE_LABELS.vi[m] }}
      </button>
    </div>
    <div class="bar">
      <span class="muted mono">{{ info ? `${formatTris(info.triangles)} tris · ${info.meshes} mesh · ${info.materials} vật liệu` : '' }}</span>
      <span class="tools">
        <button type="button" class="btn" :aria-pressed="spin" @click="spin = !spin">Tự xoay</button>
        <button type="button" class="btn" :disabled="!info" @click="viewer?.resetCamera()">Góc mặc định</button>
        <button type="button" class="btn" :disabled="!info" @click="viewer?.toggleFullscreen()">Toàn màn hình</button>
        <button type="button" class="btn" :disabled="!info" title="Lưu góc camera hiện tại làm góc mở đầu" @click="useThisView">Dùng góc này</button>
        <button type="button" class="btn" :disabled="!info || busyShot" title="Chụp khung xem hiện tại làm thumbnail" @click="takeThumbnail">{{ busyShot ? 'Đang chụp…' : 'Chụp thumbnail' }}</button>
      </span>
    </div>
  </section>
</template>

<style scoped>
.preview { display: flex; flex-direction: column; gap: 12px; padding: 12px; }
.stage { position: relative; height: 420px; border-radius: 10px; overflow: hidden; background: var(--viewport); }
.overlay { position: absolute; inset: 0; margin: 0; display: flex; align-items: center; justify-content: center; text-align: center; padding: 24px; pointer-events: none; }
.modes { display: flex; flex-wrap: wrap; gap: 4px; padding: 4px; border-radius: 12px; background: var(--surface2); }
.modes button { min-height: 36px; padding: 6px 10px; border: 0; border-radius: 8px; background: none; color: var(--text); font: 500 13px var(--font); cursor: pointer; }
.modes button[aria-pressed='true'] { background: var(--accent); color: var(--on-accent); }
.modes button:disabled { color: var(--muted); opacity: .45; cursor: not-allowed; }
.bar { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; }
.tools { display: flex; gap: 8px; flex-wrap: wrap; }
.btn[aria-pressed='true'] { border-color: var(--accent); color: var(--accent); }
@media (max-width: 560px) { .stage { height: 300px; } }
</style>
