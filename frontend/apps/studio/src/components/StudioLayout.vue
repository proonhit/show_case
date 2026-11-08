<script setup lang="ts">
import { useQueryClient } from '@tanstack/vue-query';
import { ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { api } from '../api';
import { setSignedIn } from '../router';
import { theme, toggleTheme } from '../theme';
import AppIcon, { type IconName } from './AppIcon.vue';

const router = useRouter();
const route = useRoute();
const queryClient = useQueryClient();
const open = ref(false);
watch(() => route.fullPath, () => (open.value = false));

const nav: { to: string; label: string; icon: IconName }[] = [
  { to: '/', label: 'Dashboard', icon: 'dashboard' },
  { to: '/models', label: 'Model 3D', icon: 'box' },
  { to: '/taxonomy', label: 'Danh mục & tag', icon: 'tag' },
  { to: '/about', label: 'Trang About', icon: 'user' },
];

const isActive = (to: string) => (to === '/' ? route.path === '/' : route.path.startsWith(to));

async function logout() {
  try {
    await api.POST('/api/auth/logout');
  } finally {
    setSignedIn(false);
    queryClient.clear();
    await router.push({ name: 'login' });
  }
}
</script>

<template>
  <div class="layout">
    <header class="topbar">
      <button class="icon-btn" type="button" aria-label="Mở menu" :aria-expanded="open" @click="open = !open">
        <AppIcon name="menu" />
      </button>
      <span class="brand-name">Studio</span>
    </header>
    <nav class="sidebar" :class="{ open }" aria-label="Studio">
      <div class="brand">
        <span class="logo"><AppIcon name="box" :size="18" /></span>
        <span class="brand-name">Studio</span>
      </div>
      <span class="section">QUẢN LÝ</span>
      <RouterLink v-for="item in nav" :key="item.to" :to="item.to" class="nav-link" :class="{ active: isActive(item.to) }"
        :aria-current="isActive(item.to) ? 'page' : undefined">
        <AppIcon :name="item.icon" />{{ item.label }}
      </RouterLink>
      <div class="grow" />
      <button type="button" class="nav-link" @click="toggleTheme">
        <AppIcon :name="theme === 'dark' ? 'sun' : 'moon'" />{{ theme === 'dark' ? 'Giao diện sáng' : 'Giao diện tối' }}
      </button>
      <a class="nav-link" href="/" target="_blank" rel="noopener" @click.prevent>
        <AppIcon name="eye" />Xem Showcase <span class="soon">GĐ5</span>
      </a>
      <button type="button" class="nav-link" @click="logout"><AppIcon name="logout" />Đăng xuất</button>
    </nav>
    <main class="content">
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.layout { min-height: 100vh; display: flex; }
.sidebar {
  width: 248px; flex: none; position: sticky; top: 0; height: 100vh; padding: 20px 16px; background: var(--surface);
  border-right: 1px solid var(--border); display: flex; flex-direction: column; gap: 4px;
}
.brand { display: flex; align-items: center; gap: 10px; padding: 0 4px 20px; }
.logo { width: 32px; height: 32px; border-radius: 8px; background: var(--accent); color: var(--on-accent); display: flex; align-items: center; justify-content: center; }
.brand-name { font-size: 15px; font-weight: 600; }
.section { font-size: 11px; font-weight: 500; letter-spacing: .6px; color: var(--muted); padding: 0 12px 6px; }
.nav-link {
  display: flex; align-items: center; gap: 10px; min-height: 40px; padding: 10px 12px; border-radius: 8px; border: 0; background: none;
  color: var(--muted); font: 500 13px var(--font); text-decoration: none; cursor: pointer; text-align: left;
}
.nav-link:hover { color: var(--text); background: var(--surface2); }
.nav-link.active { background: var(--surface2); color: var(--text); }
.nav-link.active :deep(svg) { color: var(--accent); }
.soon { margin-left: auto; font-size: 11px; padding: 1px 6px; border-radius: 6px; background: var(--surface2); }
.grow { flex: 1; }
.content { flex: 1; min-width: 0; padding: 32px 40px; }
.topbar { display: none; }

@media (max-width: 860px) {
  .layout { flex-direction: column; }
  .topbar { display: flex; align-items: center; gap: 8px; padding: 8px 12px; border-bottom: 1px solid var(--border); background: var(--surface); position: sticky; top: 0; z-index: 2; }
  .sidebar { display: none; position: fixed; inset: 53px 0 auto 0; width: auto; height: auto; z-index: 3; border-right: 0; border-bottom: 1px solid var(--border); }
  .sidebar.open { display: flex; }
  .sidebar .brand { display: none; }
  .content { padding: 20px 16px; }
}
</style>
