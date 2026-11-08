<script setup lang="ts">
import { site, termName, text } from '~/composables/site';

const { t, locale } = useI18n();
const localePath = useLocalePath();

const featured = computed(() => site.models.find((m) => m.isFeatured) ?? site.models[0]);
const usedCategories = computed(() => site.categories.filter((c) => site.models.some((m) => m.categorySlug === c.slug)));
const usedTags = computed(() => site.tags.filter((tg) => site.models.some((m) => m.tagSlugs.includes(tg.slug))));
const softwareList = computed(() => [...new Set(site.models.flatMap((m) => (m.software ?? '').split(',').map((s) => s.trim()).filter(Boolean)))]);

const category = ref<string | null>(null);
const tag = ref<string | null>(null);
const shown = computed(() => site.models.filter((m) =>
  (!category.value || m.categorySlug === category.value) && (!tag.value || m.tagSlugs.includes(tag.value))));

const headline = computed(() => (locale.value === 'vi' ? site.profile.headlineVi : site.profile.headlineEn));
useSeoMeta({
  title: () => `${site.profile.displayName} — ${t('home.works')}`,
  description: () => headline.value ?? t('home.lead'),
});
</script>

<template>
  <div>
    <section class="container hero">
      <div class="intro">
        <span class="eyebrow">{{ headline || t('home.eyebrow') }}</span>
        <h1>{{ t('home.title') }}</h1>
        <p class="lead">{{ t('home.lead') }}</p>
        <div class="actions">
          <a href="#works" class="btn primary big">{{ t('home.explore') }}</a>
          <a v-if="site.profile.cvUrl" :href="site.profile.cvUrl" class="btn big" download>{{ t('home.cv') }}</a>
          <NuxtLink v-else :to="localePath('/about')" class="btn big">{{ t('nav.about') }}</NuxtLink>
        </div>
        <dl class="facts">
          <div><dt>{{ site.models.length }}</dt><dd>{{ t('home.models') }}</dd></div>
          <div><dt>{{ usedCategories.length }}</dt><dd>{{ t('home.categories') }}</dd></div>
          <div v-if="softwareList.length"><dt>{{ softwareList[0] }}</dt><dd>{{ softwareList.slice(1, 3).map((s) => `+ ${s}`).join(' ') || '&nbsp;' }}</dd></div>
        </dl>
      </div>
      <NuxtLink v-if="featured" :to="localePath(`/models/${featured.slug}`)" class="feature"
        :aria-label="t('home.openFeatured', { name: text(featured, locale).title })">
        <img v-if="featured.thumbnailUrl" :src="featured.thumbnailUrl" alt="" fetchpriority="high" width="800" height="600" />
        <svg v-else width="120" height="120" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1" aria-hidden="true"><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16ZM3.3 7l8.7 5 8.7-5M12 22V12" /></svg>
        <span class="pill">★ {{ t('home.featured') }} · {{ text(featured, locale).title }}</span>
        <span class="drag muted">{{ t('home.drag') }} →</span>
      </NuxtLink>
    </section>

    <section id="works" class="container works">
      <div class="works-head">
        <div class="title-row"><h2>{{ t('home.works') }}</h2><span class="muted">{{ shown.length }} {{ t('home.models') }}</span></div>
        <div v-if="usedCategories.length > 1" role="group" :aria-label="t('home.filter')" class="chips">
          <button type="button" class="chip" :aria-pressed="category === null" @click="category = null">{{ t('home.all') }}</button>
          <button v-for="c in usedCategories" :key="c.slug" type="button" class="chip" :aria-pressed="category === c.slug" @click="category = c.slug">
            {{ termName(c, locale) }}
          </button>
        </div>
      </div>
      <div v-if="usedTags.length" role="group" :aria-label="t('home.tagFilter')" class="chips">
        <button v-for="tg in usedTags" :key="tg.slug" type="button" class="chip" :aria-pressed="tag === tg.slug" @click="tag = tag === tg.slug ? null : tg.slug">
          #{{ termName(tg, locale) }}
        </button>
      </div>
      <p v-if="!shown.length" class="muted">{{ t('home.empty') }}</p>
      <div class="grid">
        <ModelCard v-for="m in shown" :key="m.id" :model="m" />
      </div>
    </section>
  </div>
</template>

<style scoped>
.hero { display: flex; flex-wrap: wrap; gap: 48px 64px; align-items: center; padding-top: 72px; padding-bottom: 56px; }
.intro { flex: 1 1 420px; min-width: 0; display: flex; flex-direction: column; gap: 20px; }
.eyebrow { font-size: 12px; font-weight: 500; letter-spacing: .6px; text-transform: uppercase; color: var(--accent); }
h1 { font-size: clamp(36px, 5vw, 56px); line-height: 1.12; letter-spacing: -1.5px; max-width: 560px; }
.lead { margin: 0; color: var(--muted); max-width: 520px; line-height: 24px; }
.actions { display: flex; flex-wrap: wrap; gap: 12px; }
.big { font-size: 15px; min-height: 46px; padding: 12px 20px; }
.facts { margin: 12px 0 0; display: flex; flex-wrap: wrap; gap: 24px 40px; }
.facts dt { font-size: 24px; line-height: 32px; font-weight: 600; }
.facts dd { margin: 0; font-size: 13px; color: var(--muted); }
.feature { flex: 1 1 480px; min-width: 0; aspect-ratio: 8 / 7; max-height: 480px; position: relative; border-radius: 20px; overflow: hidden; background: var(--viewport); border: 1px solid var(--border); display: flex; align-items: center; justify-content: center; color: var(--muted); text-decoration: none; }
.feature img { width: 100%; height: 100%; object-fit: cover; }
.feature:hover { border-color: var(--accent); }
.pill { position: absolute; top: 20px; left: 20px; padding: 6px 12px; border-radius: 999px; background: var(--surface); color: var(--text); font-size: 13px; font-weight: 500; }
.drag { position: absolute; right: 20px; bottom: 20px; }
.works { display: flex; flex-direction: column; gap: 20px; padding-top: 24px; padding-bottom: 80px; }
.works-head { display: flex; flex-wrap: wrap; gap: 16px; justify-content: space-between; align-items: center; }
.title-row { display: flex; align-items: baseline; gap: 12px; }
h2 { font-size: 36px; line-height: 44px; font-weight: 700; letter-spacing: -.8px; }
.chips { display: flex; flex-wrap: wrap; gap: 8px; }
.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 260px), 1fr)); gap: 32px; }
@media (max-width: 560px) { .hero { padding-top: 40px; } .grid { gap: 20px; } }
</style>
