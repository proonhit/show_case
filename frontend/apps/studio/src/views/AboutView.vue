<script setup lang="ts">
import { ApiError, type ProfileDto } from '@showcase/api-client';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { computed, reactive, ref, watch } from 'vue';
import { onBeforeRouteLeave } from 'vue-router';
import AppIcon from '../components/AppIcon.vue';
import { api, queryKeys, uploadFile } from '../api';

const queryClient = useQueryClient();
const profile = useQuery({ queryKey: queryKeys.profile, queryFn: async () => (await api.GET('/api/admin/profile')).data! });

type Locale = 'en' | 'vi';
const form = reactive({
  displayName: '',
  headline: { en: '', vi: '' } as Record<Locale, string>,
  bio: { en: '', vi: '' } as Record<Locale, string>,
  links: [] as { name: string; url: string }[],
});
const tab = ref<Locale>('en');
const saved = ref('');
const dirty = computed(() => JSON.stringify(form) !== saved.value);

function load(p: ProfileDto) {
  form.displayName = p.displayName;
  form.headline = { en: p.headlineEn ?? '', vi: p.headlineVi ?? '' };
  form.bio = { en: p.bioEn ?? '', vi: p.bioVi ?? '' };
  form.links = Object.entries(p.links).map(([name, url]) => ({ name, url }));
  saved.value = JSON.stringify(form);
}
watch(() => profile.data.value, (p) => p && load(p), { immediate: true });

const SUGGESTED = ['artstation', 'linkedin', 'github', 'email', 'sketchfab', 'behance'];
const errors = ref<Record<string, string[]>>({});
const message = ref('');
const saving = ref(false);

/** Same rule as the server: an http(s) URL or an email address. */
const isLink = (value: string) => {
  if (/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) return true;
  try {
    return ['http:', 'https:'].includes(new URL(value).protocol);
  } catch {
    return false;
  }
};

// An error about the links goes away as soon as the links are edited.
watch(() => JSON.stringify(form.links), () => {
  errors.value = Object.fromEntries(Object.entries(errors.value).filter(([k]) => !k.toLowerCase().startsWith('links')));
});

function addLink() {
  const used = new Set(form.links.map((l) => l.name));
  form.links.push({ name: SUGGESTED.find((s) => !used.has(s)) ?? '', url: '' });
}

async function save() {
  errors.value = {};
  message.value = '';
  if (!form.displayName.trim()) return (errors.value = { DisplayName: ['Cần tên hiển thị.'] });
  const links: Record<string, string> = {};
  for (const l of form.links) if (l.name.trim() && l.url.trim()) links[l.name.trim().toLowerCase()] = l.url.trim();
  const bad = Object.entries(links).find(([, url]) => !isLink(url));
  if (bad) return (errors.value = { Links: [`Liên kết “${bad[0]}” phải là địa chỉ http(s)://… hoặc email.`] });
  const badName = Object.keys(links).find((n) => !/^[a-z0-9-]+$/.test(n));
  if (badName) return (errors.value = { Links: [`Tên liên kết “${badName}” chỉ gồm chữ thường, số và dấu gạch nối.`] });
  saving.value = true;
  try {
    const res = await api.PUT('/api/admin/profile', {
      body: {
        displayName: form.displayName.trim(),
        headlineEn: form.headline.en.trim() || null,
        headlineVi: form.headline.vi.trim() || null,
        bioEn: form.bio.en.trim() || null,
        bioVi: form.bio.vi.trim() || null,
        links,
      },
    });
    queryClient.setQueryData(queryKeys.profile, res.data);
    load(res.data!);
    message.value = 'Đã lưu.';
  } catch (e) {
    if (e instanceof ApiError && e.problem?.errors) errors.value = e.problem.errors;
    else message.value = e instanceof Error ? e.message : 'Không lưu được.';
  } finally {
    saving.value = false;
  }
}

const errorFor = (prefix: string) =>
  Object.entries(errors.value).find(([k]) => k.toLowerCase().startsWith(prefix.toLowerCase()))?.[1][0] ?? '';

// ---- files ----------------------------------------------------------------

const fileBusy = ref<'avatar' | 'cv' | null>(null);
const fileError = ref('');

async function upload(kind: 'avatar' | 'cv', file: File | undefined) {
  if (!file) return;
  fileError.value = '';
  const max = kind === 'avatar' ? 5 : 10;
  if (file.size > max * 1024 * 1024) return (fileError.value = `File lớn hơn ${max} MB.`);
  fileBusy.value = kind;
  try {
    const p = await uploadFile<ProfileDto>(`/api/admin/profile/${kind}`, file);
    queryClient.setQueryData(queryKeys.profile, p);
  } catch (e) {
    // The server checks the real file type; say it in Vietnamese.
    fileError.value = e instanceof ApiError && e.status === 400
      ? kind === 'avatar' ? 'Ảnh phải là file JPG, PNG hoặc WebP thật (dưới 5 MB).' : 'CV phải là file PDF thật (dưới 10 MB).'
      : e instanceof Error ? e.message : 'Upload không thành công.';
  } finally {
    fileBusy.value = null;
  }
}

async function removeFile(kind: 'avatar' | 'cv') {
  fileError.value = '';
  fileBusy.value = kind;
  try {
    const res = kind === 'avatar' ? await api.DELETE('/api/admin/profile/avatar') : await api.DELETE('/api/admin/profile/cv');
    queryClient.setQueryData(queryKeys.profile, res.data);
  } catch (e) {
    fileError.value = e instanceof Error ? e.message : 'Không xoá được.';
  } finally {
    fileBusy.value = null;
  }
}

onBeforeRouteLeave(() => (dirty.value && !saving.value ? window.confirm('Có thay đổi chưa lưu. Rời trang?') : true));
</script>

<template>
  <div class="page">
    <div class="head">
      <div>
        <h1>Trang About</h1>
        <p class="muted">Giới thiệu bản thân trên Showcase cho nhà tuyển dụng, bằng 2 ngôn ngữ.</p>
      </div>
      <button type="submit" form="about-form" class="btn primary" :disabled="saving || !dirty">{{ saving ? 'Đang lưu…' : dirty ? 'Lưu thay đổi' : 'Đã lưu' }}</button>
    </div>
    <p v-if="message" :class="message === 'Đã lưu.' ? 'muted' : 'error'" role="status">{{ message }}</p>

    <div class="grid">
      <form id="about-form" class="card form" novalidate @submit.prevent="save">
        <label class="field">Tên hiển thị
          <input v-model="form.displayName" maxlength="80" required :aria-invalid="!!errorFor('DisplayName')" />
          <span v-if="errorFor('DisplayName')" class="error">{{ errorFor('DisplayName') }}</span>
        </label>

        <div class="tabs" role="tablist" aria-label="Ngôn ngữ">
          <button v-for="l in (['en', 'vi'] as const)" :key="l" type="button" role="tab" :aria-selected="tab === l" @click="tab = l">
            {{ l === 'en' ? 'English' : 'Tiếng Việt' }}
          </button>
        </div>
        <div role="tabpanel" class="fields">
          <label class="field">Dòng giới thiệu ngắn
            <input v-model="form.headline[tab]" maxlength="160" :placeholder="tab === 'en' ? '3D artist — hard-surface & props' : 'Hoạ sĩ 3D — hard-surface & đồ vật'" />
          </label>
          <label class="field">Giới thiệu
            <textarea v-model="form.bio[tab]" rows="8" maxlength="5000" />
          </label>
        </div>

        <fieldset class="links">
          <legend>Liên kết</legend>
          <div v-for="(l, i) in form.links" :key="i" class="link-row">
            <label class="sr-only" :for="`link-name-${i}`">Tên liên kết {{ i + 1 }}</label>
            <input :id="`link-name-${i}`" v-model="l.name" class="name" maxlength="30" list="link-names" placeholder="artstation" />
            <label class="sr-only" :for="`link-url-${i}`">Địa chỉ liên kết {{ i + 1 }}</label>
            <input :id="`link-url-${i}`" v-model="l.url" class="url" maxlength="300" placeholder="https://… hoặc email" />
            <button type="button" class="icon-btn" :aria-label="`Xoá liên kết ${l.name || i + 1}`" @click="form.links.splice(i, 1)"><AppIcon name="trash" :size="16" /></button>
          </div>
          <datalist id="link-names"><option v-for="s in SUGGESTED" :key="s" :value="s" /></datalist>
          <span v-if="errorFor('Links')" class="error">{{ errorFor('Links') }}</span>
          <button type="button" class="btn add" :disabled="form.links.length >= 12" @click="addLink"><AppIcon name="plus" :size="16" />Thêm liên kết</button>
        </fieldset>
      </form>

      <div class="side">
        <section class="card file-box" aria-labelledby="avatar-title">
          <h2 id="avatar-title">Ảnh đại diện</h2>
          <div class="avatar">
            <img v-if="profile.data.value?.avatarUrl" :src="profile.data.value.avatarUrl" alt="Ảnh đại diện hiện tại" />
            <AppIcon v-else name="user" :size="40" />
          </div>
          <p class="muted">JPG, PNG hoặc WebP · tối đa 5 MB · nên dùng ảnh vuông.</p>
          <div class="file-actions">
            <label class="btn" :class="{ disabled: fileBusy }">{{ fileBusy === 'avatar' ? 'Đang upload…' : profile.data.value?.avatarUrl ? 'Đổi ảnh' : 'Chọn ảnh' }}
              <input class="sr-only" type="file" accept="image/jpeg,image/png,image/webp" :disabled="!!fileBusy"
                @change="upload('avatar', ($event.target as HTMLInputElement).files?.[0]); ($event.target as HTMLInputElement).value = ''" />
            </label>
            <button v-if="profile.data.value?.avatarUrl" type="button" class="btn danger" :disabled="!!fileBusy" @click="removeFile('avatar')">Xoá ảnh</button>
          </div>
        </section>

        <section class="card file-box" aria-labelledby="cv-title">
          <h2 id="cv-title">CV</h2>
          <p v-if="profile.data.value?.cvUrl" class="cv"><AppIcon name="check" :size="16" /><a :href="profile.data.value.cvUrl" target="_blank" rel="noopener">CV.pdf</a> · nhà tuyển dụng tải được từ trang About</p>
          <p v-else class="muted">Chưa có CV. PDF · tối đa 10 MB.</p>
          <div class="file-actions">
            <label class="btn" :class="{ disabled: fileBusy }">{{ fileBusy === 'cv' ? 'Đang upload…' : profile.data.value?.cvUrl ? 'Thay CV' : 'Chọn file PDF' }}
              <input class="sr-only" type="file" accept="application/pdf" :disabled="!!fileBusy"
                @change="upload('cv', ($event.target as HTMLInputElement).files?.[0]); ($event.target as HTMLInputElement).value = ''" />
            </label>
            <button v-if="profile.data.value?.cvUrl" type="button" class="btn danger" :disabled="!!fileBusy" @click="removeFile('cv')">Xoá CV</button>
          </div>
        </section>
        <p v-if="fileError" class="error" role="alert">{{ fileError }}</p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.page { display: flex; flex-direction: column; gap: 20px; }
.head { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: center; }
.head p, .page > p { margin: 4px 0 0; }
.grid { display: grid; grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr); gap: 16px; align-items: start; }
.form { display: flex; flex-direction: column; gap: 18px; }
.field { display: flex; flex-direction: column; gap: 6px; font-size: 13px; font-weight: 500; }
.field textarea { resize: vertical; }
.fields { display: flex; flex-direction: column; gap: 14px; }
.tabs { display: flex; gap: 4px; border-bottom: 1px solid var(--border); }
.tabs button { min-height: 40px; padding: 8px 14px; border: 0; background: none; color: var(--muted); font: 500 13px var(--font); cursor: pointer; border-bottom: 2px solid transparent; margin-bottom: -1px; }
.tabs button[aria-selected='true'] { color: var(--text); border-bottom-color: var(--accent); }
.links { border: 0; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 8px; min-width: 0; }
.links legend { font-size: 13px; font-weight: 500; padding: 0 0 6px; }
.link-row { display: flex; gap: 8px; align-items: center; }
.link-row .name { width: 140px; flex: none; }
.link-row .url { flex: 1; min-width: 0; }
.add { align-self: flex-start; }
.side { display: flex; flex-direction: column; gap: 16px; }
.file-box { display: flex; flex-direction: column; gap: 12px; }
.file-box p { margin: 0; }
.avatar { width: 120px; height: 120px; border-radius: 50%; overflow: hidden; background: var(--surface2); display: flex; align-items: center; justify-content: center; color: var(--muted); }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
.file-actions { display: flex; gap: 8px; flex-wrap: wrap; }
.file-actions .btn:focus-within { outline: 2px solid var(--accent); outline-offset: 2px; }
.btn.disabled { opacity: .5; pointer-events: none; }
.cv { display: flex; align-items: center; gap: 6px; font-size: 13px; flex-wrap: wrap; }
.cv :deep(svg) { color: var(--success); }
@media (max-width: 1000px) { .grid { grid-template-columns: 1fr; } }
@media (max-width: 480px) { .link-row .name { width: 100px; } }
</style>
