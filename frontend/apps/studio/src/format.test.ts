import { describe, expect, it } from 'vitest';
import { formatBytes, formatTris, moveItem, timeAgo } from './format';

describe('format', () => {
  it('compacts triangle counts', () => {
    expect([formatTris(950), formatTris(148_200), formatTris(1_250_000), formatTris(2_000_000), formatTris(null)])
      .toEqual(['950', '148K', '1.3M', '2M', '—']);
  });

  it('formats bytes', () => {
    expect([formatBytes(512), formatBytes(1536), formatBytes(486 * 1024 * 1024)]).toEqual(['512 B', '1.5 KB', '486 MB']);
  });

  it('says how long ago in Vietnamese', () => {
    const now = new Date('2026-11-08T12:00:00Z');
    const ago = (sec: number) => timeAgo(new Date(now.getTime() - sec * 1000).toISOString(), now);
    expect([ago(5), ago(600), ago(7200), ago(30 * 3600), ago(3 * 86400), ago(15 * 86400), ago(60 * 86400)])
      .toEqual(['Vừa xong', '10 phút trước', '2 giờ trước', 'Hôm qua', '3 ngày trước', '2 tuần trước', '2 tháng trước']);
  });

  it('moves an item for drag and drop', () => {
    expect(moveItem(['a', 'b', 'c', 'd'], 3, 0)).toEqual(['d', 'a', 'b', 'c']);
    expect(moveItem(['a', 'b', 'c'], 0, 2)).toEqual(['b', 'c', 'a']);
    expect(moveItem(['a', 'b'], 0, 5)).toEqual(['a', 'b']);
  });
});
