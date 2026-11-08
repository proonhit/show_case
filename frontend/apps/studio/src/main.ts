import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query';
import { createApp } from 'vue';
import App from './App.vue';
import { router } from './router';
import './styles.css';
import './theme';

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000, retry: (count, error) => (error as { status?: number }).status !== 401 && count < 2 } },
});

createApp(App).use(router).use(VueQueryPlugin, { queryClient }).mount('#app');
