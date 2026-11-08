<script setup lang="ts">
defineProps<{ status: string }>();
const dot: Record<string, string> = { Published: 'var(--success)', Draft: 'var(--warning)', Archived: 'var(--muted)' };
</script>

<template>
  <span class="badge"><span class="dot" :style="{ background: dot[status] ?? 'var(--muted)' }" />{{ status }}</span>
</template>

<style scoped>
.badge { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 999px; background: var(--surface2); font-size: 13px; font-weight: 500; white-space: nowrap; }
.dot { width: 6px; height: 6px; border-radius: 50%; }
</style>
