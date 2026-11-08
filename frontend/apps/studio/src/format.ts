/** "148K", "1.2M" — compact triangle counts for tables. */
export function formatTris(n: number | null | undefined): string {
  if (n == null) return '—';
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1).replace(/\.0$/, '')}M`;
  if (n >= 1_000) return `${Math.round(n / 1_000)}K`;
  return String(n);
}

/** "486 MB" */
export function formatBytes(bytes: number): string {
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let v = bytes;
  let i = 0;
  while (v >= 1024 && i < units.length - 1) {
    v /= 1024;
    i++;
  }
  return `${i === 0 ? v : v < 10 ? v.toFixed(1) : Math.round(v)} ${units[i]}`;
}

/** "2 giờ trước", "Hôm qua", "3 tuần trước" — Vietnamese relative time. */
export function timeAgo(iso: string, now: Date = new Date()): string {
  const s = Math.max(0, (now.getTime() - new Date(iso).getTime()) / 1000);
  const m = s / 60, h = m / 60, d = h / 24;
  if (s < 60) return 'Vừa xong';
  if (m < 60) return `${Math.floor(m)} phút trước`;
  if (h < 24) return `${Math.floor(h)} giờ trước`;
  if (d < 2) return 'Hôm qua';
  if (d < 7) return `${Math.floor(d)} ngày trước`;
  if (d < 30) return `${Math.floor(d / 7)} tuần trước`;
  if (d < 365) return `${Math.floor(d / 30)} tháng trước`;
  return `${Math.floor(d / 365)} năm trước`;
}

/** Moves one item to a new index (drag and drop), returning a new array. */
export function moveItem<T>(list: readonly T[], from: number, to: number): T[] {
  const copy = list.slice();
  if (from < 0 || from >= copy.length || to < 0 || to >= copy.length || from === to) return copy;
  const [item] = copy.splice(from, 1);
  copy.splice(to, 0, item);
  return copy;
}
