<script setup lang="ts">
import { useRoute } from 'vue-router';
const route = useRoute();
</script>

<template>
  <h1>{{ route.meta.title }}</h1>
  <p class="muted">Màn hình này đang được làm (GĐ4, các PR tiếp theo).</p>
  <RouterLink to="/models">← Về danh sách model</RouterLink>
</template>
