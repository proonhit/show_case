<script setup lang="ts">
import { ApiError } from '@showcase/api-client';
import { ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppIcon from '../components/AppIcon.vue';
import { api } from '../api';
import { setSignedIn } from '../router';

const router = useRouter();
const route = useRoute();
const password = ref('');
const busy = ref(false);
const error = ref('');

async function submit() {
  if (!password.value || busy.value) return;
  busy.value = true;
  error.value = '';
  try {
    await api.POST('/api/auth/login', { body: { password: password.value } });
    setSignedIn(true);
    const next = typeof route.query.next === 'string' && route.query.next.startsWith('/') ? route.query.next : '/';
    await router.replace(next);
  } catch (e) {
    const status = e instanceof ApiError ? e.status : 0;
    error.value = status === 401 ? 'Sai mật khẩu.'
      : status === 429 ? 'Thử quá nhiều lần. Đợi 1 phút rồi thử lại.'
      : status === 503 ? 'Máy chủ chưa cấu hình mật khẩu (ADMIN_PASSWORD_HASH).'
      : 'Không kết nối được máy chủ.';
    password.value = '';
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <div class="page">
    <form class="card form" @submit.prevent="submit">
      <span class="logo"><AppIcon name="box" :size="24" /></span>
      <div>
        <h1 class="title">Studio</h1>
        <p class="muted">Trang quản lý model 3D — chỉ dành cho chủ sở hữu.</p>
      </div>
      <label class="field">Mật khẩu
        <input v-model="password" type="password" autocomplete="current-password" required autofocus
          :aria-invalid="!!error" aria-describedby="login-error" />
      </label>
      <p v-if="error" id="login-error" class="error" role="alert">{{ error }}</p>
      <button class="btn primary" type="submit" :disabled="busy || !password">{{ busy ? 'Đang đăng nhập…' : 'Đăng nhập' }}</button>
      <p class="muted note"><AppIcon name="lock" :size="14" />Giữ đăng nhập 30 ngày · tối đa 5 lần thử mỗi phút</p>
    </form>
  </div>
</template>

<style scoped>
.page { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; }
.form { width: 100%; max-width: 400px; border-radius: 20px; padding: 32px; display: flex; flex-direction: column; gap: 20px; }
.logo { width: 44px; height: 44px; border-radius: 12px; background: var(--accent); color: var(--on-accent); display: flex; align-items: center; justify-content: center; }
.title { font-size: 24px; line-height: 32px; font-weight: 600; letter-spacing: 0; }
.field { display: flex; flex-direction: column; gap: 6px; font-size: 13px; font-weight: 500; }
.field input { font-size: 15px; }
.btn { font-size: 15px; min-height: 46px; }
.note { margin: 0; display: flex; align-items: center; gap: 8px; }
p { margin: 0; }
</style>
