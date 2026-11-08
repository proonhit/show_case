<script setup lang="ts">
import type { ModelImageDto } from '@showcase/api-client';
import { computed, ref } from 'vue';
import { api, uploadFile } from '../api';
import { moveItem } from '../format';
import AppIcon from './AppIcon.vue';

const props = defineProps<{ modelId: string; images: ModelImageDto[] }>();
const emit = defineEmits<{ changed: [] }>();

const KINDS = [
  { value: 'Render', label: 'Render' },
  { value: 'Wireframe', label: 'Wireframe' },
  { value: 'Process', label: 'Quá trình làm' },
  { value: 'Thumbnail', label: 'Thumbnail' },
] as const;
const kindLabel = (k: string) => KINDS.find((x) => x.value === k)?.label ?? k;

const kind = ref<(typeof KINDS)[number]['value']>('Render');
const busy = ref(false);
const error = ref('');
const confirming = ref<string | null>(null);

const thumbnail = computed(() => props.images.find((i) => i.kind === 'Thumbnail') ?? null);
const gallery = computed(() => props.images.filter((i) => i.kind !== 'Thumbnail'));

async function run(task: () => Promise<unknown>, failure: string) {
  busy.value = true;
  error.value = '';
  try {
    await task();
    emit('changed');
  } catch {
    error.value = failure;
  } finally {
    busy.value = false;
  }
}

async function upload(files: FileList | null | undefined) {
  if (!files?.length) return;
  const list = [...files];
  const tooBig = list.find((f) => f.size > 10 * 1024 * 1024);
  if (tooBig) return (error.value = `${tooBig.name} lớn hơn 10 MB.`);
  await run(async () => {
    // One thumbnail per model: only the first file counts for that kind.
    for (const f of kind.value === 'Thumbnail' ? list.slice(0, 1) : list)
      await uploadFile(`/api/admin/models/${props.modelId}/images`, f, 'POST', { kind: kind.value });
  }, 'Upload ảnh không thành công (chỉ nhận JPG, PNG, WebP thật).');
}

const saveCaption = (img: ModelImageDto, captionEn: string, captionVi: string) => {
  if ((img.captionEn ?? '') === captionEn && (img.captionVi ?? '') === captionVi) return;
  void run(() => api.PATCH('/api/admin/images/{imageId}', { params: { path: { imageId: img.id } }, body: { captionEn, captionVi } }), 'Không lưu được chú thích.');
};

const move = (img: ModelImageDto, delta: number) => {
  const list = gallery.value;
  const from = list.indexOf(img);
  const ids = moveItem(list, from, from + delta).map((i) => i.id);
  void run(() => api.PUT('/api/admin/models/{id}/images/order', { params: { path: { id: props.modelId } }, body: { ids } }), 'Không đổi được thứ tự.');
};

function remove(img: ModelImageDto) {
  if (confirming.value !== img.id) return (confirming.value = img.id);
  confirming.value = null;
  void run(() => api.DELETE('/api/admin/images/{imageId}', { params: { path: { imageId: img.id } } }), 'Không xoá được ảnh.');
}
</script>

<template>
  <section class="card panel" aria-labelledby="images-title">
    <h2 id="images-title">Hình ảnh</h2>

    <div class="thumb-row">
      <div class="thumb">
        <img v-if="thumbnail" :src="thumbnail.url" alt="Thumbnail hiện tại" />
        <AppIcon v-else name="box" :size="28" />
      </div>
      <p class="muted">
        <strong class="label">Thumbnail</strong><br />
        {{ thumbnail ? 'Ảnh đại diện của model trong lưới Showcase.' : 'Chưa có. Bấm “Chụp thumbnail” ở khung xem 3D, hoặc upload ảnh với loại Thumbnail.' }}
      </p>
      <button v-if="thumbnail" type="button" class="icon-btn" :class="{ confirm: confirming === thumbnail.id }"
        :aria-label="confirming === thumbnail.id ? 'Bấm lần nữa để xoá thumbnail' : 'Xoá thumbnail'" @click="remove(thumbnail)" @blur="confirming = null">
        <AppIcon name="trash" :size="16" />
      </button>
    </div>

    <div class="upload">
      <label class="field">Loại ảnh
        <select v-model="kind">
          <option v-for="k in KINDS" :key="k.value" :value="k.value">{{ k.label }}</option>
        </select>
      </label>
      <label class="btn" :class="{ disabled: busy }">{{ busy ? 'Đang xử lý…' : 'Upload ảnh' }}
        <input class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" multiple :disabled="busy"
          @change="upload(($event.target as HTMLInputElement).files); ($event.target as HTMLInputElement).value = ''" />
      </label>
      <span class="muted">JPG, PNG, WebP · tối đa 10 MB/ảnh</span>
    </div>
    <p v-if="error" class="error" role="alert">{{ error }}</p>

    <p v-if="!gallery.length" class="muted">Chưa có ảnh phụ (render, wireframe, quá trình làm).</p>
    <ul class="gallery">
      <li v-for="(img, i) in gallery" :key="img.id">
        <img :src="img.url" :alt="img.captionVi || img.captionEn || `${kindLabel(img.kind)} ${i + 1}`" loading="lazy" />
        <div class="meta">
          <span class="kind">{{ kindLabel(img.kind) }}</span>
          <label class="sr-only" :for="`cap-en-${img.id}`">Chú thích tiếng Anh ảnh {{ i + 1 }}</label>
          <input :id="`cap-en-${img.id}`" :value="img.captionEn ?? ''" maxlength="200" placeholder="Caption (EN)"
            @change="saveCaption(img, ($event.target as HTMLInputElement).value, img.captionVi ?? '')" />
          <label class="sr-only" :for="`cap-vi-${img.id}`">Chú thích tiếng Việt ảnh {{ i + 1 }}</label>
          <input :id="`cap-vi-${img.id}`" :value="img.captionVi ?? ''" maxlength="200" placeholder="Chú thích (VI)"
            @change="saveCaption(img, img.captionEn ?? '', ($event.target as HTMLInputElement).value)" />
          <div class="tools">
            <button type="button" class="icon-btn" :disabled="i === 0 || busy" :aria-label="`Đưa ảnh ${i + 1} lên trước`" @click="move(img, -1)">↑</button>
            <button type="button" class="icon-btn" :disabled="i === gallery.length - 1 || busy" :aria-label="`Đưa ảnh ${i + 1} ra sau`" @click="move(img, 1)">↓</button>
            <button type="button" class="icon-btn" :class="{ confirm: confirming === img.id }"
              :aria-label="confirming === img.id ? `Bấm lần nữa để xoá ảnh ${i + 1}` : `Xoá ảnh ${i + 1}`" @click="remove(img)" @blur="confirming = null">
              <AppIcon name="trash" :size="16" />
            </button>
          </div>
        </div>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.panel { display: flex; flex-direction: column; gap: 14px; }
.panel p { margin: 0; }
.thumb-row { display: flex; align-items: center; gap: 14px; }
.thumb { width: 96px; height: 96px; flex: none; border-radius: 10px; overflow: hidden; background: var(--viewport); display: flex; align-items: center; justify-content: center; color: var(--muted); }
.thumb img { width: 100%; height: 100%; object-fit: cover; }
.thumb-row p { flex: 1; }
.label { color: var(--text); font-weight: 500; }
.upload { display: flex; align-items: flex-end; gap: 10px; flex-wrap: wrap; }
.field { display: flex; flex-direction: column; gap: 6px; font-size: 13px; font-weight: 500; }
.upload .btn:focus-within { outline: 2px solid var(--accent); outline-offset: 2px; }
.btn.disabled { opacity: .5; pointer-events: none; }
.gallery { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 12px; }
.gallery li { border: 1px solid var(--border); border-radius: 12px; overflow: hidden; background: var(--surface2); display: flex; flex-direction: column; }
.gallery img { width: 100%; aspect-ratio: 16 / 10; object-fit: cover; background: var(--viewport); }
.meta { display: flex; flex-direction: column; gap: 6px; padding: 10px; }
.meta input { padding: 6px 10px; font-size: 13px; background: var(--surface); }
.kind { font-size: 12px; color: var(--muted); }
.tools { display: flex; justify-content: flex-end; gap: 2px; }
.confirm { color: var(--danger); background: var(--surface); }
</style>
