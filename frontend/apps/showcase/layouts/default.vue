<script setup lang="ts">
const i18nHead = useLocaleHead();
useHead(() => ({
  htmlAttrs: { lang: i18nHead.value.htmlAttrs?.lang },
  link: [...(i18nHead.value.link ?? [])],
  meta: [...(i18nHead.value.meta ?? [])],
}));
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
