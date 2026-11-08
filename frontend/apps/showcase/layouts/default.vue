<script setup lang="ts">
// lang on <html>; canonical/hreflang/Open Graph come from usePageSeo on each page.
const { locale } = useI18n();
useHead(() => ({ htmlAttrs: { lang: locale.value } }));
</script>

<template>
  <div class="page">
    <a href="#main" class="skip btn">{{ $t('nav.skip') }}</a>
    <SiteHeader />
    <main id="main"><slot /></main>
    <SiteFooter />
  </div>
</template>

<style scoped>
.page { min-height: 100vh; display: flex; flex-direction: column; }
main { flex: 1; }
</style>
