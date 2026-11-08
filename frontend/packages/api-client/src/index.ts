import createClient, { type Middleware } from 'openapi-fetch';
import type { components, paths } from './schema';

export type { components, paths };
type Schemas = components['schemas'];

export type CategoryDto = Schemas['CategoryDto'];
export type ModelDetailDto = Schemas['ModelDetailDto'];
export type ModelFileDto = Schemas['ModelFileDto'];
export type ModelSummaryDto = Schemas['ModelSummaryDto'];
export type ProfileDto = Schemas['ProfileDto'];
export type StudioStatsDto = Schemas['StudioStatsDto'];
export type TagDto = Schemas['TagDto'];
export type TranslationDto = Schemas['TranslationDto'];
export type UpsertModelRequest = Schemas['UpsertModelRequest'];

export type ModelStatus = 'Draft' | 'Published' | 'Archived';

/** Thrown for any non-2xx answer. `problem` is the ASP.NET ProblemDetails body when there is one. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem?: { title?: string; detail?: string; errors?: Record<string, string[]>; error?: string },
  ) {
    super(problem?.detail ?? problem?.error ?? problem?.title ?? `Request failed (${status})`);
  }
}

/**
 * Same-origin client: the session cookie (HttpOnly, SameSite=Strict) goes along automatically.
 * `onUnauthorized` runs on any 401 so the app can send the user to the login page.
 */
export function createApi(options: { baseUrl?: string; onUnauthorized?: () => void } = {}) {
  const client = createClient<paths>({ baseUrl: options.baseUrl ?? '', credentials: 'same-origin' });
  const errors: Middleware = {
    async onResponse({ response }) {
      if (response.ok) return undefined;
      if (response.status === 401) options.onUnauthorized?.();
      let problem: ApiError['problem'];
      try {
        problem = await response.clone().json();
      } catch {
        problem = undefined;
      }
      throw new ApiError(response.status, problem);
    },
  };
  client.use(errors);
  return client;
}

export type Api = ReturnType<typeof createApi>;
