import { createApi, type ModelSummaryDto } from '@showcase/api-client';
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
  stats: ['stats'] as const,
  me: ['me'] as const,
};
