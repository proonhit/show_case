import { ref, watchEffect } from 'vue';

export type Theme = 'dark' | 'light';
const KEY = 'studio.theme';

function initial(): Theme {
  try {
    const saved = localStorage.getItem(KEY);
    if (saved === 'dark' || saved === 'light') return saved;
  } catch {
    // storage blocked: fall through to the system setting
  }
  return matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
}

/** Dark by default (as designed); the choice is remembered per browser. */
export const theme = ref<Theme>(initial());

watchEffect(() => {
  document.documentElement.dataset.theme = theme.value;
  try {
    localStorage.setItem(KEY, theme.value);
  } catch {
    // not critical
  }
});

export const toggleTheme = () => (theme.value = theme.value === 'dark' ? 'light' : 'dark');
