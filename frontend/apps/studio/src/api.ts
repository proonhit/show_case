import { ApiError, createApi, type ModelSummaryDto } from '@showcase/api-client';
import { router } from './router';

/** Any 401 outside the login page means the session ended: go log in again, then come back. */
export const api = createApi({
  onUnauthorized: () => {
    const current = router.currentRoute.value;
    if (!current.meta.public) void router.push({ name: 'login', query: { next: current.fullPath } });
  },
});

export const STATUSES = ['Published', 'Draft', 'Archived'] as const;

/** The English title, falling back to whatever exists. */
export const titleOf = (m: ModelSummaryDto) =>
  m.translations.find((t) => t.locale === 'en')?.title ?? m.translations[0]?.title ?? m.slug;

/** The Vietnamese title, or null when it is still missing. */
export const viTitleOf = (m: ModelSummaryDto) => m.translations.find((t) => t.locale === 'vi')?.title ?? null;

export const queryKeys = {
  models: ['models'] as const,
  model: (id: string) => ['models', id] as const,
  files: (id: string) => ['models', id, 'files'] as const,
  categories: ['categories'] as const,
  profile: ['profile'] as const,
  tags: ['tags'] as const,
  stats: ['stats'] as const,
  me: ['me'] as const,
};

/** Largest upload the server accepts (Upload:MaxMegabytes). */
export const MAX_UPLOAD_MB = 100;

/** Sends one file as multipart field "file" (openapi-fetch only does JSON bodies). Returns the JSON answer. */
export async function uploadFile<T>(url: string, file: Blob, method: 'PUT' | 'POST' = 'PUT', fields: Record<string, string> = {}, fileName?: string): Promise<T> {
  const form = new FormData();
  form.append('file', file, fileName ?? (file instanceof File ? file.name : 'upload'));
  for (const [k, v] of Object.entries(fields)) form.append(k, v);
  const res = await fetch(url, { method, body: form, credentials: 'same-origin' });
  if (!res.ok) {
    let problem: ApiError['problem'];
    try {
      problem = await res.json();
    } catch {
      problem = undefined;
    }
    const first = problem?.errors ? Object.values(problem.errors)[0]?.[0] : undefined;
    throw new ApiError(res.status, first ? { ...problem, detail: first } : problem);
  }
  return (await res.json()) as T;
}
