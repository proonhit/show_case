<script setup lang="ts">
import type { ModelSummaryDto } from '@showcase/api-client';
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query';
import { computed, ref } from 'vue';
import AppIcon from '../components/AppIcon.vue';
import StatusBadge from '../components/StatusBadge.vue';
import { api, queryKeys, STATUSES, titleOf, viTitleOf } from '../api';
import { formatTris, moveItem, timeAgo } from '../format';

const queryClient = useQueryClient();
const models = useQuery({ queryKey: queryKeys.models, queryFn: async () => (await api.GET('/api/admin/models')).data! });

type Filter = 'All' | (typeof STATUSES)[number];
const filter = ref<Filter>('All');
const filters = computed(() => {
  const list = models.data.value ?? [];
  return (['All', ...STATUSES] as Filter[]).map((f) => ({
    value: f,
    label: f === 'All' ? 'Tất cả' : f,
    count: f === 'All' ? list.length : list.filter((m) => m.status === f).length,
  }));
});
const rows = computed(() => (models.data.value ?? []).filter((m) => filter.value === 'All' || m.status === filter.value));
const actionError = ref('');

const refresh = () => Promise.all([
  queryClient.invalidateQueries({ queryKey: queryKeys.models }),
  queryClient.invalidateQueries({ queryKey: queryKeys.stats }),
]);
const fail = (e: unknown) => (actionError.value = e instanceof Error ? e.message : 'Thao tác không thành công.');

const setStatus = useMutation({
  mutationFn: (v: { m: ModelSummaryDto; status: string; featured: boolean }) =>
    api.PATCH('/api/admin/models/{id}/status', { params: { path: { id: v.m.id } }, body: { status: v.status, isFeatured: v.featured } }),
  onSuccess: refresh,
  onError: fail,
});

const remove = useMutation({
  mutationFn: (id: string) => api.DELETE('/api/admin/models/{id}', { params: { path: { id } } }),
  onSuccess: refresh,
  onError: fail,
});
const confirmingDelete = ref<string | null>(null);
function askDelete(m: ModelSummaryDto) {
  if (confirmingDelete.value === m.id) {
    confirmingDelete.value = null;
    remove.mutate(m.id);
  } else {
    confirmingDelete.value = m.id;
  }
}

// Drag and drop: rows reorder at once, then the new order is saved; on failure the list reloads.
const reorder = useMutation({
  mutationFn: (ids: string[]) => api.PUT('/api/admin/models/order', { body: { ids } }),
  onError: (e) => {
    fail(e);
    void refresh();
  },
});
const dragging = ref<string | null>(null);
const canDrag = computed(() => filter.value === 'All');
function onDrop(targetId: string) {
  const list = models.data.value ?? [];
  const from = list.findIndex((m) => m.id === dragging.value);
  const to = list.findIndex((m) => m.id === targetId);
  dragging.value = null;
  if (from < 0 || to < 0 || from === to) return;
  const next = moveItem(list, from, to);
  queryClient.setQueryData(queryKeys.models, next);
  reorder.mutate(next.map((m) => m.id));
}
function nudge(m: ModelSummaryDto, delta: number) {
  const list = models.data.value ?? [];
  const from = list.findIndex((x) => x.id === m.id);
  const next = moveItem(list, from, from + delta);
  queryClient.setQueryData(queryKeys.models, next);
  reorder.mutate(next.map((x) => x.id));
}
</script>

<template>
  <div class="page">
    <div class="head">
      <div>
        <h1>Model 3D</h1>
        <p class="muted">{{ models.data.value?.length ?? 0 }} model · kéo thả để sắp xếp thứ tự hiển thị trên Showcase</p>
      </div>
      <RouterLink class="btn primary" to="/models/new"><AppIcon name="plus" :size="16" />Upload model mới</RouterLink>
    </div>

    <div class="chips" role="group" aria-label="Lọc theo trạng thái">
      <button v-for="f in filters" :key="f.value" type="button" class="chip" :aria-pressed="filter === f.value" @click="filter = f.value">
        {{ f.label }} ({{ f.count }})
      </button>
    </div>

    <p v-if="actionError" class="error" role="alert">{{ actionError }}</p>
    <p v-if="models.isError.value" class="error" role="alert">Không tải được danh sách model.</p>

    <div class="card table-box">
      <table>
        <thead>
          <tr>
            <th class="grip-col"><span class="sr-only">Sắp xếp</span></th>
            <th>Tên (EN / VI)</th>
            <th>Danh mục</th>
            <th>Trạng thái</th>
            <th>Nổi bật</th>
            <th>Tris</th>
            <th>Cập nhật</th>
            <th class="right">Thao tác</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="models.isLoading.value"><td colspan="8" class="muted">Đang tải…</td></tr>
          <tr v-else-if="!rows.length"><td colspan="8" class="muted">Không có model nào ở mục này.</td></tr>
          <tr v-for="m in rows" :key="m.id" :draggable="canDrag" :class="{ dragging: dragging === m.id }"
            @dragstart="dragging = m.id" @dragend="dragging = null" @dragover.prevent @drop.prevent="onDrop(m.id)">
            <td class="grip-col">
              <span v-if="canDrag" class="grip" title="Kéo để sắp xếp"><AppIcon name="grip" :size="16" :stroke-width="3" /></span>
              <span class="sr-only">
                <button type="button" @click="nudge(m, -1)">Lên trên</button>
                <button type="button" @click="nudge(m, 1)">Xuống dưới</button>
              </span>
            </td>
            <td>
              <RouterLink :to="`/models/${m.id}`" class="title">{{ titleOf(m) }}</RouterLink>
              <span class="sub" :class="{ missing: !viTitleOf(m) }">{{ viTitleOf(m) ?? 'Chưa có bản tiếng Việt' }}</span>
            </td>
            <td class="muted">{{ m.categorySlug ?? '—' }}</td>
            <td>
              <label class="sr-only" :for="`status-${m.id}`">Trạng thái của {{ titleOf(m) }}</label>
              <select :id="`status-${m.id}`" class="status" :value="m.status"
                @change="setStatus.mutate({ m, status: ($event.target as HTMLSelectElement).value, featured: m.isFeatured })">
                <option v-for="s in STATUSES" :key="s" :value="s">{{ s }}</option>
              </select>
              <StatusBadge class="sr-only" :status="m.status" />
            </td>
            <td>
              <button type="button" class="icon-btn" :aria-pressed="m.isFeatured"
                :aria-label="m.isFeatured ? `Bỏ nổi bật ${titleOf(m)}` : `Đánh dấu nổi bật ${titleOf(m)}`"
                @click="setStatus.mutate({ m, status: m.status, featured: !m.isFeatured })">
                <AppIcon name="star" :size="16" :fill="m.isFeatured ? 'currentColor' : 'none'" :class="{ starred: m.isFeatured }" />
              </button>
            </td>
            <td class="mono muted">{{ formatTris(m.triangles) }}</td>
            <td class="muted nowrap">{{ timeAgo(m.updatedAt) }}</td>
            <td class="right nowrap">
              <RouterLink :to="`/models/${m.id}`" class="icon-btn" :aria-label="`Sửa ${titleOf(m)}`"><AppIcon name="pencil" :size="16" /></RouterLink>
              <button type="button" class="icon-btn" :class="{ confirm: confirmingDelete === m.id }"
                :aria-label="confirmingDelete === m.id ? `Bấm lần nữa để xoá ${titleOf(m)}` : `Xoá ${titleOf(m)}`"
                :title="confirmingDelete === m.id ? 'Bấm lần nữa để xoá' : 'Xoá'" @click="askDelete(m)" @blur="confirmingDelete = null">
                <AppIcon name="trash" :size="16" />
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.page { display: flex; flex-direction: column; gap: 20px; }
.head { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: center; }
.head p { margin: 4px 0 0; }
.chips { display: flex; flex-wrap: wrap; gap: 8px; }
.chip { min-height: 36px; padding: 6px 12px; border-radius: 999px; background: var(--surface2); border: 1px solid var(--border); color: var(--muted); font: 500 13px var(--font); cursor: pointer; }
.chip[aria-pressed='true'] { background: var(--accent-soft); border-color: var(--accent); color: var(--accent); }
.table-box { position: relative; padding: 6px 12px; overflow-x: auto; } /* positioned: keeps the hidden keyboard buttons inside the scroll box */
table { width: 100%; min-width: 860px; border-collapse: collapse; }
th { text-align: left; padding: 10px 8px; font-size: 11px; font-weight: 500; letter-spacing: .6px; text-transform: uppercase; color: var(--muted); }
td { padding: 9px 8px; border-top: 1px solid var(--border); vertical-align: middle; }
tr.dragging { opacity: .4; }
.grip-col { width: 32px; }
.grip { color: var(--muted); cursor: grab; display: inline-flex; }
.title { display: block; color: var(--text); font-weight: 500; text-decoration: none; }
.title:hover { color: var(--accent); }
.sub { font-size: 13px; color: var(--muted); }
.sub.missing { color: var(--warning); }
.status { padding: 4px 8px; font-size: 13px; border-radius: 999px; }
.starred { color: var(--accent); }
.confirm { color: var(--danger); background: var(--surface2); }
.right { text-align: right; }
.nowrap { white-space: nowrap; }
</style>
