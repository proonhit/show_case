<script setup lang="ts">
import { useQuery } from '@tanstack/vue-query';
import { computed } from 'vue';
import AppIcon from '../components/AppIcon.vue';
import StatusBadge from '../components/StatusBadge.vue';
import { api, queryKeys, titleOf, viTitleOf } from '../api';
import { formatBytes, formatTris, timeAgo } from '../format';

const stats = useQuery({ queryKey: queryKeys.stats, queryFn: async () => (await api.GET('/api/admin/stats')).data! });
const models = useQuery({ queryKey: queryKeys.models, queryFn: async () => (await api.GET('/api/admin/models')).data! });

const tiles = computed(() => {
  const s = stats.data.value;
  return [
    { label: 'Tổng model', value: s?.total ?? '—', note: 'Không tính model đã xoá' },
    { label: 'Đã publish', value: s?.published ?? '—', note: 'Đang hiển thị trên Showcase' },
    { label: 'Bản nháp', value: s?.draft ?? '—', note: 'Chưa công khai' },
    { label: 'Dung lượng', value: s ? formatBytes(s.storageBytes) : '—', note: 'File gốc đã upload, mọi phiên bản' },
  ];
});

const recent = computed(() =>
  [...(models.data.value ?? [])].sort((a, b) => b.updatedAt.localeCompare(a.updatedAt)).slice(0, 5));

/** Things to fix before a model looks right on the Showcase. */
const todo = computed(() => {
  const items: string[] = [];
  for (const m of models.data.value ?? []) {
    if (m.status === 'Archived') continue;
    if (!viTitleOf(m)) items.push(`${titleOf(m)}: thiếu bản tiếng Việt`);
    if (m.triangles == null) items.push(`${titleOf(m)}: chưa có file 3D đã xử lý`);
    if (!m.thumbnailUrl) items.push(`${titleOf(m)}: chưa có thumbnail`);
  }
  return items;
});
</script>

<template>
  <div class="page">
    <div class="head">
      <div>
        <h1>Dashboard</h1>
        <p class="muted">Tình trạng portfolio của bạn.</p>
      </div>
      <RouterLink class="btn primary" to="/models/new"><AppIcon name="plus" :size="16" />Upload model mới</RouterLink>
    </div>

    <p v-if="stats.isError.value || models.isError.value" class="error" role="alert">Không tải được dữ liệu. Thử tải lại trang.</p>

    <div class="tiles">
      <div v-for="t in tiles" :key="t.label" class="card tile">
        <span class="muted">{{ t.label }}</span>
        <span class="value">{{ t.value }}</span>
        <span class="muted">{{ t.note }}</span>
      </div>
    </div>

    <div class="row">
      <section class="card recent">
        <div class="section-head">
          <h2>Model gần đây</h2>
          <RouterLink to="/models" class="link">Xem tất cả</RouterLink>
        </div>
        <p v-if="models.isLoading.value" class="muted">Đang tải…</p>
        <p v-else-if="!recent.length" class="muted">Chưa có model nào. Bắt đầu bằng nút “Upload model mới”.</p>
        <RouterLink v-for="m in recent" :key="m.id" :to="`/models/${m.id}`" class="recent-row">
          <span class="thumb"><img v-if="m.thumbnailUrl" :src="m.thumbnailUrl" alt="" loading="lazy" /><AppIcon v-else name="box" :size="16" /></span>
          <span class="name">{{ titleOf(m) }}</span>
          <StatusBadge :status="m.status" />
          <span class="mono muted tris">{{ formatTris(m.triangles) }} tris</span>
          <span class="muted when">{{ timeAgo(m.updatedAt) }}</span>
        </RouterLink>
      </section>

      <div class="side">
        <section class="card stack">
          <h2>Showcase</h2>
          <p class="muted">Showcase là trang tĩnh: mỗi lần Publish, hệ thống tự build lại (khoảng 1–3 phút). Trạng thái build sẽ hiện ở đây từ GĐ5.</p>
        </section>
        <section class="card stack">
          <h2>Cần xử lý</h2>
          <p v-if="!todo.length" class="ok"><AppIcon name="check" :size="16" />Không có gì cần làm.</p>
          <p v-for="item in todo" :key="item" class="warn"><AppIcon name="alert" :size="16" />{{ item }}</p>
        </section>
      </div>
    </div>
  </div>
</template>

<style scoped>
.page { display: flex; flex-direction: column; gap: 24px; }
.head { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: center; }
.head p { margin: 4px 0 0; }
.tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; }
.tile { display: flex; flex-direction: column; gap: 6px; }
.value { font-size: 32px; line-height: 40px; font-weight: 700; }
.row { display: flex; flex-wrap: wrap; gap: 16px; align-items: flex-start; }
.recent { flex: 999 1 520px; min-width: 0; }
.side { flex: 1 1 320px; min-width: 0; display: flex; flex-direction: column; gap: 16px; }
.section-head { display: flex; justify-content: space-between; align-items: center; padding-bottom: 8px; }
.link { font-size: 13px; font-weight: 500; text-decoration: none; }
.recent-row { display: flex; align-items: center; gap: 14px; padding: 12px 0; border-top: 1px solid var(--border); color: var(--text); text-decoration: none; }
.recent-row:hover .name { color: var(--accent); }
.thumb { width: 40px; height: 40px; flex: none; border-radius: 8px; overflow: hidden; background: var(--viewport); display: flex; align-items: center; justify-content: center; color: var(--muted); }
.thumb img { width: 100%; height: 100%; object-fit: cover; }
.name { flex: 1; min-width: 0; font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.tris { width: 90px; text-align: right; }
.when { width: 110px; text-align: right; }
.stack { display: flex; flex-direction: column; gap: 12px; }
.stack p { margin: 0; }
.warn, .ok { display: flex; align-items: center; gap: 8px; font-size: 13px; }
.warn :deep(svg) { color: var(--warning); flex: none; }
.ok :deep(svg) { color: var(--success); }
@media (max-width: 560px) { .tris, .when { display: none; } }
</style>
