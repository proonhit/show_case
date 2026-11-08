<script setup lang="ts">
import type { ModelFileDto } from '@showcase/api-client';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Upload } from 'tus-js-client';
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { api, MAX_UPLOAD_MB, queryKeys } from '../api';
import { formatBytes, timeAgo } from '../format';
import AppIcon from './AppIcon.vue';

const props = defineProps<{ modelId: string }>();
const emit = defineEmits<{ uploaded: [file: ModelFileDto] }>();
const queryClient = useQueryClient();

const files = useQuery({
  queryKey: computed(() => queryKeys.files(props.modelId)),
  queryFn: async () => (await api.GET('/api/admin/models/{id}/files', { params: { path: { id: props.modelId } } })).data!,
});

type State = 'idle' | 'uploading' | 'paused' | 'checking' | 'done' | 'error';
const state = ref<State>('idle');
const progress = ref(0);
const fileName = ref('');
const message = ref('');
const dragOver = ref(false);
let upload: Upload | null = null;

function pick(file: File | undefined) {
  if (!file || state.value === 'uploading') return;
  const ext = file.name.split('.').pop()?.toLowerCase();
  if (ext !== 'glb' && ext !== 'fbx') return fail('Chỉ nhận file .glb hoặc .fbx.');
  if (file.size > MAX_UPLOAD_MB * 1024 * 1024) return fail(`File lớn hơn ${MAX_UPLOAD_MB} MB.`);
  start(file);
}

function fail(text: string) {
  state.value = 'error';
  message.value = text;
}

function start(file: File) {
  const before = new Set((files.data.value ?? []).map((f) => f.id));
  fileName.value = file.name;
  progress.value = 0;
  message.value = '';
  state.value = 'uploading';

  upload = new Upload(file, {
    endpoint: '/api/admin/uploads',
    chunkSize: 8 * 1024 * 1024, // small requests resume well on a flaky connection
    retryDelays: [0, 1000, 3000, 5000, 10000],
    metadata: { modelId: props.modelId, filename: file.name },
    removeFingerprintOnSuccess: true,
    onProgress: (sent, total) => (progress.value = total ? sent / total : 0),
    onError: (err) => {
      const status = (err as { originalResponse?: { getStatus(): number } }).originalResponse?.getStatus();
      fail(status === 401 ? 'Phiên đăng nhập đã hết. Đăng nhập lại rồi upload lại.'
        : status === 413 ? `File lớn hơn giới hạn của máy chủ (${MAX_UPLOAD_MB} MB).`
        : `Upload lỗi: ${err.message}`);
    },
    onSuccess: async () => {
      // The server checks the file's first bytes after the last chunk; a fake .glb is dropped silently.
      state.value = 'checking';
      await queryClient.invalidateQueries({ queryKey: queryKeys.files(props.modelId) });
      const added = (files.data.value ?? []).find((f) => !before.has(f.id) && f.kind === 'Original');
      if (!added) return fail('Máy chủ không nhận file: nội dung không phải .glb/.fbx hợp lệ.');
      state.value = 'done';
      emit('uploaded', added);
      void queryClient.invalidateQueries({ queryKey: jobKey.value });
      void queryClient.invalidateQueries({ queryKey: queryKeys.stats });
    },
  });

  // Resume an upload of the same file that was interrupted (closed tab, lost connection).
  void upload.findPreviousUploads().then((previous) => {
    if (previous.length) upload?.resumeFromPreviousUpload(previous[0]);
    upload?.start();
  });
}

function pause() {
  void upload?.abort();
  state.value = 'paused';
}
function resume() {
  upload?.start();
  state.value = 'uploading';
}
function cancel() {
  void upload?.abort(true);
  upload = null;
  state.value = 'idle';
}

onBeforeUnmount(() => void upload?.abort());

const busy = computed(() => state.value === 'uploading' || state.value === 'paused' || state.value === 'checking');

// ---- processing (Worker pipeline) ---------------------------------------------

const STEP_LABELS: Record<string, string> = {
  upload: 'Nhận file',
  convert: 'Chuyển .fbx → .glb',
  stats: 'Đọc thông số',
  optimize: 'Tối ưu (Meshopt + WebP)',
};
const STATUS_LABELS: Record<string, string> = { pending: 'Chờ', running: 'Đang chạy', done: 'Xong', skipped: 'Bỏ qua', failed: 'Lỗi' };
const KIND_LABELS: Record<string, string> = { Original: 'Gốc', Optimized: 'Tối ưu', Mobile: 'Điện thoại' };

const jobKey = computed(() => ['models', props.modelId, 'job'] as const);
const job = useQuery({
  queryKey: jobKey,
  queryFn: async () => {
    try {
      return (await api.GET('/api/admin/models/{id}/jobs/latest', { params: { path: { id: props.modelId } } })).data!;
    } catch (e) {
      if ((e as { status?: number }).status === 404) return null; // nothing uploaded yet
      throw e;
    }
  },
  // Poll while the Worker is busy; stop once the job has finished.
  refetchInterval: (q) => (q.state.data && ['Queued', 'Running'].includes(q.state.data.status) ? 1500 : false),
});

// When a job finishes, the files (optimized/phone copies), stats and preview change.
watch(() => `${job.data.value?.id}:${job.data.value?.status}`, (now, before) => {
  const status = job.data.value?.status;
  if (now !== before && status && !['Queued', 'Running'].includes(status)) {
    void queryClient.invalidateQueries({ queryKey: ['models', props.modelId] });
    void queryClient.invalidateQueries({ queryKey: queryKeys.models });
    void queryClient.invalidateQueries({ queryKey: queryKeys.stats });
  }
});

const seconds = (ms: number | null | undefined) => (ms == null ? '' : ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1)} s`);
</script>

<template>
  <section class="card panel" aria-labelledby="files-title">
    <h2 id="files-title">File 3D</h2>

    <label v-if="!busy" class="drop" :class="{ over: dragOver }"
      @dragover.prevent="dragOver = true" @dragleave="dragOver = false"
      @drop.prevent="dragOver = false; pick($event.dataTransfer?.files[0])">
      <AppIcon name="box" :size="24" />
      <span><strong>Chọn file</strong> hoặc kéo thả vào đây</span>
      <span class="muted">.glb hoặc .fbx · tối đa {{ MAX_UPLOAD_MB }} MB · upload tiếp được nếu rớt mạng</span>
      <input class="sr-only" type="file" accept=".glb,.fbx" @change="pick(($event.target as HTMLInputElement).files?.[0]); ($event.target as HTMLInputElement).value = ''" />
    </label>

    <div v-else class="progress-box">
      <div class="row">
        <span class="name">{{ fileName }}</span>
        <span class="mono muted">{{ Math.round(progress * 100) }}%</span>
      </div>
      <div class="bar" role="progressbar" :aria-valuenow="Math.round(progress * 100)" aria-valuemin="0" aria-valuemax="100" :aria-label="`Đang upload ${fileName}`">
        <span :style="{ width: `${progress * 100}%` }" />
      </div>
      <div class="row">
        <span class="muted">{{ state === 'paused' ? 'Đã tạm dừng' : state === 'checking' ? 'Đang kiểm tra file…' : 'Đang upload…' }}</span>
        <span class="actions">
          <button v-if="state === 'uploading'" type="button" class="btn" @click="pause">Tạm dừng</button>
          <button v-if="state === 'paused'" type="button" class="btn" @click="resume">Tiếp tục</button>
          <button v-if="state !== 'checking'" type="button" class="btn danger" @click="cancel">Huỷ</button>
        </span>
      </div>
    </div>

    <p v-if="state === 'error'" class="error" role="alert">{{ message }}</p>
    <p v-if="state === 'done'" class="ok" role="status"><AppIcon name="check" :size="16" />Đã nhận {{ fileName }}.</p>

    <div v-if="job.data.value" class="job" aria-live="polite">
      <div class="job-head">
        <strong>Xử lý file</strong>
        <span class="job-status" :data-status="job.data.value.status">{{
          { Queued: 'Đang chờ', Running: 'Đang xử lý…', Succeeded: 'Hoàn tất', Failed: 'Lỗi' }[job.data.value.status] ?? job.data.value.status }}</span>
      </div>
      <ol class="steps">
        <li v-for="s in job.data.value.steps" :key="s.name" :data-status="s.status">
          <span class="dot" aria-hidden="true" />
          <span class="step-name">{{ STEP_LABELS[s.name] ?? s.name }}</span>
          <span class="muted step-state">{{ STATUS_LABELS[s.status] ?? s.status }}<template v-if="s.durationMs != null"> · {{ seconds(s.durationMs) }}</template></span>
          <span v-if="s.message" class="step-msg muted">{{ s.message }}</span>
        </li>
      </ol>
      <p v-if="job.data.value.error" class="error" role="alert">{{ job.data.value.error }}</p>
    </div>

    <ul v-if="files.data.value?.length" class="files">
      <li v-for="f in files.data.value" :key="f.id">
        <span class="mono">v{{ f.version }} · {{ KIND_LABELS[f.kind] ?? f.kind }} · .{{ f.format }}</span>
        <span class="muted">{{ formatBytes(f.sizeBytes) }} · {{ timeAgo(f.uploadedAt) }}</span>
        <span v-if="f.isCurrent" class="current">Đang dùng</span>
      </li>
    </ul>
    <p v-else-if="files.isSuccess.value" class="muted">Chưa có file nào.</p>
  </section>
</template>

<style scoped>
.panel { display: flex; flex-direction: column; gap: 14px; }
.drop {
  display: flex; flex-direction: column; align-items: center; gap: 6px; padding: 28px 16px; text-align: center;
  border: 1.5px dashed var(--border); border-radius: 12px; background: var(--surface2); cursor: pointer; color: var(--text);
}
.drop:hover, .drop.over { border-color: var(--accent); }
.drop:focus-within { outline: 2px solid var(--accent); outline-offset: 2px; }
.drop :deep(svg) { color: var(--accent); }
.progress-box { display: flex; flex-direction: column; gap: 8px; }
.row { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; }
.name { font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
.bar { height: 6px; border-radius: 999px; background: var(--surface2); overflow: hidden; }
.bar span { display: block; height: 100%; background: var(--accent); transition: width .2s; }
.actions { display: flex; gap: 8px; }
.ok { display: flex; align-items: center; gap: 8px; font-size: 13px; margin: 0; }
.ok :deep(svg) { color: var(--success); flex: none; }
.error { margin: 0; }
.files { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; }
.files li { display: flex; gap: 12px; align-items: center; padding: 8px 0; border-top: 1px solid var(--border); flex-wrap: wrap; }
.job { display: flex; flex-direction: column; gap: 10px; padding: 12px 14px; border-radius: 12px; background: var(--surface2); }
.job-head { display: flex; justify-content: space-between; align-items: center; font-size: 13px; }
.job-status { font-size: 12px; padding: 2px 8px; border-radius: 999px; background: var(--surface); }
.job-status[data-status='Succeeded'] { color: var(--success); }
.job-status[data-status='Failed'] { color: var(--danger); }
.job-status[data-status='Running'], .job-status[data-status='Queued'] { color: var(--accent); }
.steps { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
.steps li { display: grid; grid-template-columns: 12px 1fr auto; column-gap: 10px; align-items: center; font-size: 13px; }
.steps .dot { width: 8px; height: 8px; border-radius: 50%; background: var(--border); }
.steps li[data-status='done'] .dot { background: var(--success); }
.steps li[data-status='running'] .dot { background: var(--accent); animation: pulse 1s infinite alternate; }
.steps li[data-status='failed'] .dot { background: var(--danger); }
.steps li[data-status='skipped'] .step-name { color: var(--muted); }
.step-msg { grid-column: 2 / -1; font-size: 12px; overflow-wrap: anywhere; }
@keyframes pulse { to { opacity: .3; } }
@media (prefers-reduced-motion: reduce) { .steps li[data-status='running'] .dot { animation: none; } }
.current { margin-left: auto; font-size: 12px; padding: 2px 8px; border-radius: 999px; background: var(--accent-soft); color: var(--accent); }
</style>
