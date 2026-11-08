<script setup lang="ts">
import { ApiError } from '@showcase/api-client';
import { computed, reactive, ref } from 'vue';
import { SLUG_PATTERN, slugify } from '../format';
import AppIcon from './AppIcon.vue';

/** One row of a category or tag list. `sortOrder` exists for categories only. */
export interface Term {
  id: string;
  nameEn: string;
  nameVi: string;
  slug: string;
  sortOrder?: number;
}
export type TermInput = Omit<Term, 'id'>;

const props = defineProps<{
  title: string;
  noun: string;
  items: Term[];
  usage: Record<string, number>;
  withOrder?: boolean;
  save: (input: TermInput, id: string | null) => Promise<unknown>;
  remove: (id: string) => Promise<unknown>;
}>();

const editing = ref<string | 'new' | null>(null);
const draft = reactive<TermInput>({ nameEn: '', nameVi: '', slug: '', sortOrder: 0 });
const slugTouched = ref(false);
const error = ref('');
const busy = ref(false);
const confirming = ref<string | null>(null);

const sorted = computed(() =>
  [...props.items].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.nameEn.localeCompare(b.nameEn)));

function start(item: Term | null) {
  editing.value = item?.id ?? 'new';
  Object.assign(draft, item
    ? { nameEn: item.nameEn, nameVi: item.nameVi, slug: item.slug, sortOrder: item.sortOrder ?? 0 }
    : { nameEn: '', nameVi: '', slug: '', sortOrder: props.items.length });
  slugTouched.value = !!item;
  error.value = '';
}

function onNameEn() {
  if (!slugTouched.value) draft.slug = slugify(draft.nameEn);
}

async function submit() {
  error.value = '';
  if (!draft.nameEn.trim() || !draft.nameVi.trim()) return (error.value = 'Cần tên tiếng Anh và tiếng Việt.');
  if (!SLUG_PATTERN.test(draft.slug)) return (error.value = 'Slug chỉ gồm chữ thường, số và dấu gạch nối.');
  busy.value = true;
  try {
    const input: TermInput = { nameEn: draft.nameEn.trim(), nameVi: draft.nameVi.trim(), slug: draft.slug };
    if (props.withOrder) input.sortOrder = Number(draft.sortOrder) || 0;
    await props.save(input, editing.value === 'new' ? null : editing.value);
    editing.value = null;
  } catch (e) {
    error.value = e instanceof ApiError && e.status === 409 ? `Slug “${draft.slug}” đã được dùng.` : e instanceof Error ? e.message : 'Không lưu được.';
  } finally {
    busy.value = false;
  }
}

async function askRemove(item: Term) {
  if (confirming.value !== item.id) return (confirming.value = item.id);
  confirming.value = null;
  error.value = '';
  try {
    await props.remove(item.id);
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Không xoá được.';
  }
}
</script>

<template>
  <section class="card box" :aria-labelledby="`${noun}-title`">
    <div class="head">
      <h2 :id="`${noun}-title`">{{ title }} <span class="muted">({{ items.length }})</span></h2>
      <button type="button" class="btn" :disabled="editing === 'new'" @click="start(null)"><AppIcon name="plus" :size="16" />Thêm {{ noun }}</button>
    </div>
    <p v-if="error" class="error" role="alert">{{ error }}</p>

    <form v-if="editing" class="editor" @submit.prevent="submit" @keydown.esc="editing = null">
      <label>Tên EN<input v-model="draft.nameEn" maxlength="80" required autofocus @input="onNameEn" /></label>
      <label>Tên VI<input v-model="draft.nameVi" maxlength="80" required /></label>
      <label>Slug<input v-model="draft.slug" maxlength="80" required @input="slugTouched = true" /></label>
      <label v-if="withOrder" class="narrow">Thứ tự<input v-model="draft.sortOrder" type="number" min="0" /></label>
      <div class="editor-actions">
        <button type="submit" class="btn primary" :disabled="busy">{{ editing === 'new' ? 'Thêm' : 'Lưu' }}</button>
        <button type="button" class="btn" @click="editing = null">Huỷ</button>
      </div>
    </form>

    <p v-if="!items.length && !editing" class="muted">Chưa có {{ noun }} nào.</p>
    <ul class="list">
      <li v-for="item in sorted" :key="item.id" :class="{ active: editing === item.id }">
        <div class="names">
          <span class="en">{{ item.nameEn }}</span>
          <span class="muted">{{ item.nameVi }} · <span class="mono">{{ item.slug }}</span></span>
        </div>
        <span class="count muted" :title="`${usage[item.slug] ?? 0} model dùng ${noun} này`">{{ usage[item.slug] ?? 0 }} model</span>
        <button type="button" class="icon-btn" :aria-label="`Sửa ${item.nameEn}`" @click="start(item)"><AppIcon name="pencil" :size="16" /></button>
        <button type="button" class="icon-btn" :class="{ confirm: confirming === item.id }"
          :aria-label="confirming === item.id ? `Bấm lần nữa để xoá ${item.nameEn}` : `Xoá ${item.nameEn}`"
          :title="confirming === item.id ? (usage[item.slug] ? `Bấm lần nữa: ${usage[item.slug]} model sẽ mất ${noun} này` : 'Bấm lần nữa để xoá') : 'Xoá'"
          @click="askRemove(item)" @blur="confirming = null">
          <AppIcon name="trash" :size="16" />
        </button>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.box { display: flex; flex-direction: column; gap: 12px; min-width: 0; }
.head { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; }
.head .muted { font-weight: 400; }
.editor { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 10px; padding: 14px; border-radius: 12px; background: var(--surface2); }
.editor label { display: flex; flex-direction: column; gap: 4px; font-size: 13px; font-weight: 500; min-width: 0; }
.editor input { background: var(--surface); min-width: 0; }
.editor-actions { display: flex; gap: 8px; align-items: flex-end; grid-column: 1 / -1; }
.list { list-style: none; margin: 0; padding: 0; }
.list li { display: flex; align-items: center; gap: 8px; padding: 10px 0; border-top: 1px solid var(--border); }
.list li.active { opacity: .5; }
.names { flex: 1; min-width: 0; display: flex; flex-direction: column; }
.en { font-weight: 500; }
.count { white-space: nowrap; }
.confirm { color: var(--danger); background: var(--surface2); }
.error { margin: 0; }
</style>
