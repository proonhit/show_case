<script setup lang="ts">
import type { components } from '@showcase/api-client';
import { MAP_MODES, MODE_LABELS, type MapMode } from '@showcase/viewer-core';

type Settings = components['schemas']['ViewerSettingsDto'];

/** Edits the draft in place (the preview shows it live); the parent saves. */
const settings = defineModel<Settings>({ required: true });
defineProps<{ available: Record<MapMode, boolean> | null; dirty: boolean; saving: boolean; error: string }>();
defineEmits<{ save: []; reset: [] }>();

const PRESETS = [
  { label: 'Theo giao diện', value: null },
  { label: 'Than', value: '#1A1C21' },
  { label: 'Xám', value: '#3A3D44' },
  { label: 'Trắng ngà', value: '#E9E7E1' },
];
</script>

<template>
  <section class="card panel" aria-labelledby="viewer-title">
    <div class="head">
      <h2 id="viewer-title">Cài đặt viewer</h2>
      <button type="button" class="btn primary" :disabled="!dirty || saving" @click="$emit('save')">{{ saving ? 'Đang lưu…' : dirty ? 'Lưu cài đặt' : 'Đã lưu' }}</button>
    </div>
    <p class="muted">Cách model hiện ra khi khách mở trang chi tiết trên Showcase. Khung xem bên dưới cập nhật ngay.</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p>

    <div class="grid">
      <label class="field">Chế độ map mặc định
        <select v-model="settings.defaultMode">
          <option v-for="m in MAP_MODES" :key="m" :value="m" :disabled="!!available && !available[m]">{{ MODE_LABELS.vi[m] }}</option>
        </select>
      </label>
      <label class="field">Tốc độ tự xoay: {{ settings.autoRotateSpeed }}°/giây
        <input v-model.number="settings.autoRotateSpeed" type="range" min="5" max="90" step="5" :disabled="!settings.autoRotate" />
      </label>
      <label class="check"><input v-model="settings.autoRotate" type="checkbox" /> Tự xoay khi mở trang</label>
    </div>

    <fieldset class="bg">
      <legend>Nền</legend>
      <button v-for="p in PRESETS" :key="p.label" type="button" class="swatch" :aria-pressed="(settings.background ?? null) === p.value"
        @click="settings.background = p.value">
        <span class="dot" :style="{ background: p.value ?? 'var(--viewport)' }" />{{ p.label }}
      </button>
      <label class="swatch custom">
        <input type="color" :value="settings.background ?? '#1A1C21'" aria-label="Màu nền tuỳ chọn"
          @input="settings.background = ($event.target as HTMLInputElement).value.toUpperCase()" />
        Màu khác
      </label>
    </fieldset>

    <div class="camera">
      <span v-if="settings.cameraPosition" class="muted">Góc nhìn mở đầu: đã chọn (nút “Dùng góc này” ở khung xem). Trên màn hình hẹp, camera tự lùi ra để thấy trọn model.</span>
      <span v-else class="muted">Góc nhìn mở đầu: tự căn theo kích thước model. Xoay khung xem rồi bấm “Dùng góc này” để chọn.</span>
      <button v-if="settings.cameraPosition" type="button" class="btn" @click="$emit('reset')">Về tự căn</button>
    </div>
  </section>
</template>

<style scoped>
.panel { display: flex; flex-direction: column; gap: 14px; }
.panel p { margin: 0; }
.head { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 14px; align-items: end; }
.field { display: flex; flex-direction: column; gap: 6px; font-size: 13px; font-weight: 500; }
.field input[type='range'] { padding: 0; accent-color: var(--accent); }
.check { display: flex; align-items: center; gap: 8px; font-size: 13px; min-height: 40px; }
.check input { width: 18px; height: 18px; accent-color: var(--accent); }
.bg { border: 0; padding: 0; margin: 0; display: flex; flex-wrap: wrap; gap: 8px; }
.bg legend { font-size: 13px; font-weight: 500; padding-bottom: 6px; }
.swatch { display: inline-flex; align-items: center; gap: 8px; min-height: 36px; padding: 6px 12px; border-radius: 999px; border: 1px solid var(--border); background: var(--surface2); color: var(--text); font: 500 13px var(--font); cursor: pointer; }
.swatch[aria-pressed='true'] { border-color: var(--accent); color: var(--accent); }
.dot { width: 14px; height: 14px; border-radius: 50%; border: 1px solid var(--border); }
.custom input { width: 22px; height: 22px; padding: 0; border: 0; background: none; }
.camera { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; font-size: 13px; }
</style>
