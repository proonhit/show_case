<script setup lang="ts">
import { ApiError, type components, type ModelDetailDto, type UpsertModelRequest } from '@showcase/api-client';
import type { CameraView, MapMode } from '@showcase/viewer-core';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { computed, reactive, ref, watch } from 'vue';
import { onBeforeRouteLeave, useRoute, useRouter } from 'vue-router';
import AppIcon from '../components/AppIcon.vue';
import ImagesPanel from '../components/ImagesPanel.vue';
import ModelPreview from '../components/ModelPreview.vue';
import ViewerSettingsPanel from '../components/ViewerSettingsPanel.vue';
import StatusBadge from '../components/StatusBadge.vue';
import UploadPanel from '../components/UploadPanel.vue';
import { api, queryKeys, uploadFile } from '../api';
import { SLUG_PATTERN, slugify, timeAgo } from '../format';

const route = useRoute();
const router = useRouter();
const queryClient = useQueryClient();
const id = computed(() => (typeof route.params.id === 'string' ? route.params.id : null));
const isNew = computed(() => !id.value);

const categories = useQuery({ queryKey: queryKeys.categories, queryFn: async () => (await api.GET('/api/admin/categories')).data! });
const tags = useQuery({ queryKey: queryKeys.tags, queryFn: async () => (await api.GET('/api/admin/tags')).data! });
const model = useQuery({
  queryKey: computed(() => queryKeys.model(id.value ?? 'new')),
  queryFn: async () => (await api.GET('/api/admin/models/{id}', { params: { path: { id: id.value! } } })).data!,
  enabled: computed(() => !!id.value),
});
const files = useQuery({
  queryKey: computed(() => queryKeys.files(id.value ?? 'new')),
  queryFn: async () => (await api.GET('/api/admin/models/{id}/files', { params: { path: { id: id.value! } } })).data!,
  enabled: computed(() => !!id.value),
});

// ---- form ----------------------------------------------------------------

type Locale = 'en' | 'vi';
const form = reactive({
  slug: '',
  categoryId: '' as string,
  software: '',
  hoursSpent: '' as string | number,
  completedAt: '',
  tagIds: [] as string[],
  text: { en: { title: '', description: '' }, vi: { title: '', description: '' } } as Record<Locale, { title: string; description: string }>,
});
const tab = ref<Locale>('en');
const slugTouched = ref(false);
const saved = ref<string>('');

function load(m: ModelDetailDto) {
  form.slug = m.slug;
  form.categoryId = m.categoryId ?? '';
  form.software = m.software ?? '';
  form.hoursSpent = m.hoursSpent ?? '';
  form.completedAt = m.completedAt ?? '';
  form.tagIds = [...m.tagIds];
  for (const l of ['en', 'vi'] as const) {
    const t = m.translations.find((x) => x.locale === l);
    form.text[l] = { title: t?.title ?? '', description: t?.description ?? '' };
  }
  slugTouched.value = true;
  saved.value = JSON.stringify(form);
}
watch(() => model.data.value, (m) => m && load(m), { immediate: true });
if (isNew.value) saved.value = JSON.stringify(form);

// A new model's slug follows the English title until the slug is edited by hand.
watch(() => form.text.en.title, (title) => {
  if (!slugTouched.value) form.slug = slugify(title);
});

const dirty = computed(() => JSON.stringify(form) !== saved.value);

// ---- validation ------------------------------------------------------------

const serverErrors = ref<Record<string, string[]>>({});
const generalError = ref('');
const attempted = ref(false);

const clientErrors = computed(() => {
  const e: Record<string, string> = {};
  if (!form.text.en.title.trim()) e.titleEn = 'Cần tên tiếng Anh.';
  if (!SLUG_PATTERN.test(form.slug)) e.slug = 'Chỉ chữ thường, số và dấu gạch nối, ví dụ sci-fi-helmet.';
  if (form.hoursSpent !== '' && (!Number.isInteger(Number(form.hoursSpent)) || Number(form.hoursSpent) < 0)) e.hours = 'Số giờ là số nguyên ≥ 0.';
  return e;
});

/** Client message first, then the server's (keys like "Slug" or "Translations[0].Title"). */
function errorFor(key: string, serverPrefix: string) {
  if (attempted.value && clientErrors.value[key]) return clientErrors.value[key];
  const hit = Object.entries(serverErrors.value).find(([k]) => k.toLowerCase().startsWith(serverPrefix.toLowerCase()));
  return hit?.[1][0] ?? '';
}

// ---- save ----------------------------------------------------------------

const saving = ref(false);

function body(): UpsertModelRequest {
  const translations = (['en', 'vi'] as const)
    .filter((l) => form.text[l].title.trim())
    .map((l) => ({ locale: l, title: form.text[l].title.trim(), description: form.text[l].description.trim() || null }));
  return {
    slug: form.slug,
    categoryId: form.categoryId || null,
    software: form.software.trim() || null,
    hoursSpent: form.hoursSpent === '' ? null : Number(form.hoursSpent),
    completedAt: form.completedAt || null,
    translations,
    tagIds: form.tagIds,
  };
}

async function save() {
  attempted.value = true;
  serverErrors.value = {};
  generalError.value = '';
  if (Object.keys(clientErrors.value).length) {
    if (clientErrors.value.titleEn) tab.value = 'en';
    return;
  }
  saving.value = true;
  try {
    if (isNew.value) {
      const res = await api.POST('/api/admin/models', { body: body() });
      saved.value = JSON.stringify(form);
      await queryClient.invalidateQueries({ queryKey: queryKeys.models });
      await router.replace({ name: 'model-edit', params: { id: res.data!.id } });
    } else {
      const res = await api.PUT('/api/admin/models/{id}', { params: { path: { id: id.value! } }, body: body() });
      queryClient.setQueryData(queryKeys.model(id.value!), res.data);
      load(res.data!);
      void queryClient.invalidateQueries({ queryKey: queryKeys.models });
    }
    attempted.value = false;
  } catch (e) {
    if (e instanceof ApiError && e.problem?.errors) serverErrors.value = e.problem.errors;
    else if (e instanceof ApiError && e.status === 409) serverErrors.value = { Slug: ['Slug này đã được dùng (kể cả model đã xoá).'] };
    else generalError.value = e instanceof Error ? e.message : 'Không lưu được.';
  } finally {
    saving.value = false;
  }
}

// ---- status ----------------------------------------------------------------

const statusBusy = ref(false);
async function setStatus(status: 'Draft' | 'Published' | 'Archived') {
  if (!model.data.value) return;
  if (status === 'Published' && !form.text.vi.title.trim()) {
    generalError.value = 'Nên có bản tiếng Việt trước khi publish (trang Showcase có 2 ngôn ngữ).';
  }
  statusBusy.value = true;
  try {
    await api.PATCH('/api/admin/models/{id}/status', {
      params: { path: { id: id.value! } },
      body: { status, isFeatured: model.data.value.isFeatured },
    });
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.model(id.value!) }),
      queryClient.invalidateQueries({ queryKey: queryKeys.models }),
      queryClient.invalidateQueries({ queryKey: queryKeys.stats }),
    ]);
  } catch (e) {
    generalError.value = e instanceof Error ? e.message : 'Không đổi được trạng thái.';
  } finally {
    statusBusy.value = false;
  }
}

// ---- preview ----------------------------------------------------------------

/** The current original if it is a .glb; .fbx needs the GĐ2 conversion first. */
/** The optimized .glb once processing has run, else the uploaded .glb (an .fbx needs converting first). */
const previewSrc = computed(() => {
  const current = (kind: string) => files.data.value?.find((f) => f.isCurrent && f.kind === kind && f.format === 'glb');
  const file = current('Optimized') ?? current('Original');
  return file ? `/api/admin/files/${file.id}/content` : null;
});

// ---- viewer settings and images ----------------------------------------------

type ViewerSettings = components['schemas']['ViewerSettingsDto'];
const DEFAULT_SETTINGS: ViewerSettings = { autoRotate: true, autoRotateSpeed: 20, background: null, defaultMode: 'final', cameraPosition: null, cameraTarget: null };
const viewerDraft = ref<ViewerSettings>({ ...DEFAULT_SETTINGS });
const viewerSaved = ref(JSON.stringify(DEFAULT_SETTINGS));
const viewerDirty = computed(() => JSON.stringify(viewerDraft.value) !== viewerSaved.value);
const viewerSaving = ref(false);
const viewerError = ref('');
const availableModes = ref<Record<MapMode, boolean> | null>(null);

watch(() => model.data.value?.viewerSettings, (v) => {
  if (!v) return;
  // Keep unsaved edits if the model reloads in the background.
  const hadEdits = viewerDirty.value;
  const next = { ...DEFAULT_SETTINGS, ...v };
  viewerSaved.value = JSON.stringify(next);
  if (!hadEdits || viewerSaving.value) viewerDraft.value = next;
}, { immediate: true });

async function saveViewer() {
  viewerSaving.value = true;
  viewerError.value = '';
  try {
    await api.PUT('/api/admin/models/{id}/viewer', { params: { path: { id: id.value! } }, body: viewerDraft.value });
    viewerSaved.value = JSON.stringify(viewerDraft.value);
    void queryClient.invalidateQueries({ queryKey: queryKeys.model(id.value!) });
  } catch {
    viewerError.value = 'Không lưu được cài đặt viewer.';
  } finally {
    viewerSaving.value = false;
  }
}

function useCamera(view: CameraView) {
  viewerDraft.value = { ...viewerDraft.value, cameraPosition: [...view.position], cameraTarget: [...view.target] };
}

const shotMessage = ref('');
async function saveThumbnail(image: Blob) {
  shotMessage.value = '';
  try {
    await uploadFile(`/api/admin/models/${id.value}/images`, image, 'POST', { kind: 'Thumbnail' }, 'thumbnail.webp');
    shotMessage.value = 'Đã lưu thumbnail từ khung xem.';
    await refreshImages();
  } catch {
    shotMessage.value = 'Không lưu được thumbnail.';
  }
}

const refreshImages = () => Promise.all([
  queryClient.invalidateQueries({ queryKey: queryKeys.model(id.value!) }),
  queryClient.invalidateQueries({ queryKey: queryKeys.models }),
]);

onBeforeRouteLeave(() => ((dirty.value || viewerDirty.value) && !saving.value
  ? window.confirm('Có thay đổi chưa lưu. Rời trang?')
  : true));

const toggleTag = (tagId: string) => {
  const i = form.tagIds.indexOf(tagId);
  if (i >= 0) form.tagIds.splice(i, 1);
  else form.tagIds.push(tagId);
};
</script>

<template>
  <div class="page">
    <RouterLink to="/models" class="back muted">← Model 3D</RouterLink>
    <div class="head">
      <div>
        <h1>{{ isNew ? 'Model mới' : form.text.en.title || 'Sửa model' }}</h1>
        <p v-if="model.data.value" class="meta muted">
          <StatusBadge :status="model.data.value.status" /> Cập nhật {{ timeAgo(model.data.value.updatedAt) }}
        </p>
        <p v-else class="muted">Điền thông tin và lưu, rồi upload file 3D.</p>
      </div>
      <div class="head-actions">
        <template v-if="model.data.value">
          <button v-if="model.data.value.status !== 'Published'" type="button" class="btn" :disabled="statusBusy || dirty" @click="setStatus('Published')">Publish</button>
          <button v-if="model.data.value.status === 'Published'" type="button" class="btn" :disabled="statusBusy" @click="setStatus('Draft')">Chuyển về nháp</button>
          <button v-if="model.data.value.status !== 'Archived'" type="button" class="btn" :disabled="statusBusy" @click="setStatus('Archived')">Lưu trữ</button>
        </template>
        <button type="submit" form="model-form" class="btn primary" :disabled="saving || (!dirty && !isNew)">
          {{ saving ? 'Đang lưu…' : isNew ? 'Tạo model' : dirty ? 'Lưu thay đổi' : 'Đã lưu' }}
        </button>
      </div>
    </div>

    <p v-if="generalError" class="error" role="alert">{{ generalError }}</p>
    <p v-if="model.isError.value" class="error" role="alert">Không tìm thấy model này.</p>

    <div class="grid">
      <form id="model-form" class="card form" novalidate @submit.prevent="save">
        <div class="tabs" role="tablist" aria-label="Ngôn ngữ">
          <button v-for="l in (['en', 'vi'] as const)" :key="l" type="button" role="tab" :aria-selected="tab === l" @click="tab = l">
            {{ l === 'en' ? 'English' : 'Tiếng Việt' }}
            <span v-if="l === 'vi' && !form.text.vi.title.trim()" class="missing" title="Chưa có bản tiếng Việt">!</span>
          </button>
        </div>
        <div role="tabpanel" class="fields">
          <label class="field">Tên {{ tab === 'en' ? '(bắt buộc)' : '' }}
            <input v-model="form.text[tab].title" maxlength="120" :aria-invalid="tab === 'en' && !!errorFor('titleEn', 'Translations')" />
            <span v-if="tab === 'en' && errorFor('titleEn', 'Translations')" class="error">{{ errorFor('titleEn', 'Translations') }}</span>
          </label>
          <label class="field">Mô tả
            <textarea v-model="form.text[tab].description" rows="5" maxlength="5000" />
          </label>
        </div>

        <div class="cols">
          <label class="field">Slug (đường dẫn)
            <input v-model="form.slug" maxlength="120" :aria-invalid="!!errorFor('slug', 'Slug')" @input="slugTouched = true" />
            <span v-if="errorFor('slug', 'Slug')" class="error">{{ errorFor('slug', 'Slug') }}</span>
            <span v-else class="hint muted">/models/{{ form.slug || '…' }}</span>
          </label>
          <label class="field">Danh mục
            <select v-model="form.categoryId">
              <option value="">— Không —</option>
              <option v-for="c in categories.data.value" :key="c.id" :value="c.id">{{ c.nameEn }} · {{ c.nameVi }}</option>
            </select>
          </label>
          <label class="field">Phần mềm
            <input v-model="form.software" maxlength="200" placeholder="Blender, Substance Painter" />
          </label>
          <label class="field">Số giờ làm
            <input v-model="form.hoursSpent" type="number" min="0" step="1" inputmode="numeric" :aria-invalid="!!errorFor('hours', 'HoursSpent')" />
            <span v-if="errorFor('hours', 'HoursSpent')" class="error">{{ errorFor('hours', 'HoursSpent') }}</span>
          </label>
          <label class="field">Ngày hoàn thành
            <input v-model="form.completedAt" type="date" />
          </label>
        </div>

        <fieldset class="field tags">
          <legend>Tag</legend>
          <p v-if="!tags.data.value?.length" class="muted">Chưa có tag. Tạo ở mục “Danh mục &amp; tag”.</p>
          <button v-for="t in tags.data.value" :key="t.id" type="button" class="chip" :aria-pressed="form.tagIds.includes(t.id)" @click="toggleTag(t.id)">
            {{ t.nameEn }}
          </button>
        </fieldset>
      </form>

      <div class="side">
        <UploadPanel v-if="id" :model-id="id" />
        <section v-else class="card muted locked"><AppIcon name="lock" :size="16" />Lưu model trước để upload file 3D.</section>
        <ModelPreview v-if="id" :src="previewSrc" :settings="viewerDraft"
          @thumbnail="saveThumbnail" @camera="useCamera" @available="availableModes = $event" />
        <p v-if="shotMessage" class="muted" role="status">{{ shotMessage }}</p>
        <ViewerSettingsPanel v-if="id" v-model="viewerDraft" :available="availableModes" :dirty="viewerDirty"
          :saving="viewerSaving" :error="viewerError" @save="saveViewer"
          @reset="viewerDraft = { ...viewerDraft, cameraPosition: null, cameraTarget: null }" />
      </div>
    </div>
    <ImagesPanel v-if="id && model.data.value" :model-id="id" :images="model.data.value.images" @changed="refreshImages" />
  </div>
</template>

<style scoped>
.page { display: flex; flex-direction: column; gap: 20px; }
.back { text-decoration: none; }
.head { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: flex-start; }
.meta { display: flex; align-items: center; gap: 8px; margin: 6px 0 0; }
.head p { margin: 4px 0 0; }
.head-actions { display: flex; gap: 8px; flex-wrap: wrap; }
.grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.1fr); gap: 16px; align-items: start; }
.form { display: flex; flex-direction: column; gap: 18px; }
.tabs { display: flex; gap: 4px; border-bottom: 1px solid var(--border); }
.tabs button { position: relative; min-height: 40px; padding: 8px 14px; border: 0; background: none; color: var(--muted); font: 500 13px var(--font); cursor: pointer; border-bottom: 2px solid transparent; margin-bottom: -1px; }
.tabs button[aria-selected='true'] { color: var(--text); border-bottom-color: var(--accent); }
.missing { display: inline-flex; width: 16px; height: 16px; margin-left: 6px; border-radius: 50%; background: var(--warning); color: var(--on-accent); font-size: 11px; align-items: center; justify-content: center; }
.fields { display: flex; flex-direction: column; gap: 14px; }
.cols { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 14px; }
.field { display: flex; flex-direction: column; gap: 6px; font-size: 13px; font-weight: 500; }
.field textarea { resize: vertical; }
.hint { font-weight: 400; }
.tags { border: 0; padding: 0; margin: 0; flex-direction: row; flex-wrap: wrap; gap: 8px; }
.tags legend { padding: 0 0 6px; }
.chip { min-height: 36px; padding: 6px 12px; border-radius: 999px; background: var(--surface2); border: 1px solid var(--border); color: var(--muted); font: 500 13px var(--font); cursor: pointer; }
.chip[aria-pressed='true'] { background: var(--accent-soft); border-color: var(--accent); color: var(--accent); }
.side { display: flex; flex-direction: column; gap: 16px; }
.locked { display: flex; align-items: center; gap: 8px; }
@media (max-width: 1100px) { .grid { grid-template-columns: 1fr; } }
</style>
