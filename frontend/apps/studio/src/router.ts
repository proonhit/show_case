import { createRouter, createWebHistory } from 'vue-router';

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean;
    title?: string;
  }
}

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: () => import('./views/LoginView.vue'), meta: { public: true, title: 'Đăng nhập' } },
    {
      path: '/',
      component: () => import('./components/StudioLayout.vue'),
      children: [
        { path: '', name: 'dashboard', component: () => import('./views/DashboardView.vue'), meta: { title: 'Dashboard' } },
        { path: 'models', name: 'models', component: () => import('./views/ModelsView.vue'), meta: { title: 'Model 3D' } },
        { path: 'models/new', name: 'model-new', component: () => import('./views/ModelEditView.vue'), meta: { title: 'Model mới' } },
        { path: 'models/:id', name: 'model-edit', component: () => import('./views/ModelEditView.vue'), meta: { title: 'Sửa model' } },
        { path: 'taxonomy', name: 'taxonomy', component: () => import('./views/ComingSoonView.vue'), meta: { title: 'Danh mục & tag' } },
        { path: 'about', name: 'about', component: () => import('./views/ComingSoonView.vue'), meta: { title: 'Trang About' } },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
});

let signedIn: boolean | null = null;
export const setSignedIn = (value: boolean) => (signedIn = value);

/** Checks the session once per page load; after that the 401 hook in api.ts takes over. */
router.beforeEach(async (to) => {
  if (to.meta.public) return true;
  if (signedIn === null) {
    const { api } = await import('./api');
    try {
      await api.GET('/api/auth/me');
      signedIn = true;
    } catch {
      signedIn = false;
    }
  }
  return signedIn ? true : { name: 'login', query: { next: to.fullPath } };
});

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} · Studio` : 'Studio';
});
