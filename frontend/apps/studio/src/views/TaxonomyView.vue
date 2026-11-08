<script setup lang="ts">
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { computed } from 'vue';
import TermTable, { type TermInput } from '../components/TermTable.vue';
import { api, queryKeys } from '../api';

const queryClient = useQueryClient();
const categories = useQuery({ queryKey: queryKeys.categories, queryFn: async () => (await api.GET('/api/admin/categories')).data! });
const tags = useQuery({ queryKey: queryKeys.tags, queryFn: async () => (await api.GET('/api/admin/tags')).data! });
const models = useQuery({ queryKey: queryKeys.models, queryFn: async () => (await api.GET('/api/admin/models')).data! });

const categoryUsage = computed(() => {
  const count: Record<string, number> = {};
  for (const m of models.data.value ?? []) if (m.categorySlug) count[m.categorySlug] = (count[m.categorySlug] ?? 0) + 1;
  return count;
});
const tagUsage = computed(() => {
  const count: Record<string, number> = {};
  for (const m of models.data.value ?? []) for (const t of m.tagSlugs) count[t] = (count[t] ?? 0) + 1;
  return count;
});

/** Renaming or deleting changes what the model list shows, so it reloads too. */
const refresh = (key: readonly string[]) => Promise.all([
  queryClient.invalidateQueries({ queryKey: key }),
  queryClient.invalidateQueries({ queryKey: queryKeys.models }),
]);

async function saveCategory(input: TermInput, id: string | null) {
  const body = { nameEn: input.nameEn, nameVi: input.nameVi, slug: input.slug, sortOrder: input.sortOrder ?? 0 };
  if (id) await api.PUT('/api/admin/categories/{id}', { params: { path: { id } }, body });
  else await api.POST('/api/admin/categories', { body });
  await refresh(queryKeys.categories);
}
async function removeCategory(id: string) {
  await api.DELETE('/api/admin/categories/{id}', { params: { path: { id } } });
  await refresh(queryKeys.categories);
}
async function saveTag(input: TermInput, id: string | null) {
  const body = { nameEn: input.nameEn, nameVi: input.nameVi, slug: input.slug };
  if (id) await api.PUT('/api/admin/tags/{id}', { params: { path: { id } }, body });
  else await api.POST('/api/admin/tags', { body });
  await refresh(queryKeys.tags);
}
async function removeTag(id: string) {
  await api.DELETE('/api/admin/tags/{id}', { params: { path: { id } } });
  await refresh(queryKeys.tags);
}
</script>

<template>
  <div class="page">
    <div>
      <h1>Danh mục &amp; tag</h1>
      <p class="muted">Mỗi model thuộc 1 danh mục và có nhiều tag. Tên có 2 ngôn ngữ để hiển thị trên Showcase EN/VI.</p>
    </div>
    <p v-if="categories.isError.value || tags.isError.value" class="error" role="alert">Không tải được dữ liệu.</p>
    <div class="grid">
      <TermTable title="Danh mục" noun="danh mục" :items="categories.data.value ?? []" :usage="categoryUsage" with-order
        :save="saveCategory" :remove="removeCategory" />
      <TermTable title="Tag" noun="tag" :items="tags.data.value ?? []" :usage="tagUsage" :save="saveTag" :remove="removeTag" />
    </div>
  </div>
</template>

<style scoped>
.page { display: flex; flex-direction: column; gap: 20px; }
.page p { margin: 4px 0 0; }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 420px), 1fr)); gap: 16px; align-items: start; }
</style>
