<script setup lang="ts">
import { site } from '~/composables/site';
const year = new Date(site.generatedAt).getFullYear();
</script>

<template>
  <footer class="footer">
    <div class="container row">
      <span>© {{ year }} {{ site.profile.displayName }}</span>
      <span>{{ $t('footer.built') }}</span>
    </div>
  </footer>
</template>

<style scoped>
.footer { border-top: 1px solid var(--border); }
.row { display: flex; flex-wrap: wrap; gap: 12px; justify-content: space-between; padding-top: 28px; padding-bottom: 28px; font-size: 13px; color: var(--muted); }
</style>
