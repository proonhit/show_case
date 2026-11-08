// Lighthouse (mobile preset) on the generated site. Prints the scores and writes a Markdown table.
//   CHROME_PATH=… node scripts/lighthouse.mjs http://localhost:5175/en http://localhost:5175/en/models/x
// LH_MIN_PERFORMANCE (default 85) is the bar; LH_ENFORCE=1 makes a miss fail the run.
// A URL ending in "#report" is measured and shown but never fails the run: the 3D detail page
// in CI runs WebGL in software (no GPU), which says little about real phones.
import { execFileSync } from 'node:child_process';
import { appendFileSync, mkdirSync, readFileSync } from 'node:fs';

const urls = process.argv.slice(2);
const min = Number(process.env.LH_MIN_PERFORMANCE ?? 85);
const out = 'test-results/lighthouse';
mkdirSync(out, { recursive: true });

const rows = [];
let missed = false;
for (const [i, arg] of urls.entries()) {
  const reportOnly = arg.endsWith('#report');
  const url = arg.replace(/#report$/, '');
  const file = `${out}/report-${i}.json`;
  execFileSync('npx', ['-y', 'lighthouse@12.2.1', url, '--quiet', '--output=json', `--output-path=${file}`,
    '--only-categories=performance,accessibility,best-practices,seo',
    '--chrome-flags=--headless=new --no-sandbox --use-angle=swiftshader --enable-unsafe-swiftshader'], { stdio: 'inherit' });
  const r = JSON.parse(readFileSync(file, 'utf8'));
  const s = (k) => Math.round((r.categories[k]?.score ?? 0) * 100);
  const a = r.audits;
  const row = { url, perf: s('performance'), a11y: s('accessibility'), bp: s('best-practices'), seo: s('seo'),
    lcp: a['largest-contentful-paint']?.displayValue, tbt: a['total-blocking-time']?.displayValue, cls: a['cumulative-layout-shift']?.displayValue };
  if (row.perf < min && !reportOnly) missed = true;
  row.note = reportOnly ? 'report only (software WebGL)' : row.perf >= min ? 'ok' : `below ${min}`;
  rows.push(row);
  console.log(JSON.stringify(row));
  // What to fix: the LCP element, render-blocking files, the heaviest downloads and main-thread work.
  const items = (id, n = 5) => (a[id]?.details?.items ?? []).slice(0, n);
  console.log('  LCP element:', items('largest-contentful-paint-element', 1).map((x) => x.items?.[0]?.node?.snippet ?? x.node?.snippet).join(''));
  console.log('  LCP phases:', JSON.stringify(items('largest-contentful-paint-element', 2)[1]?.items ?? []));
  for (const x of items('render-blocking-resources')) console.log('  render-blocking:', x.url, `${Math.round(x.wastedMs)} ms`);
  for (const x of items('total-byte-weight')) console.log('  bytes:', x.url, `${Math.round(x.totalBytes / 1024)} KB`);
  for (const x of items('bootup-time')) console.log('  bootup:', x.url, `${Math.round(x.total)} ms`);
  for (const x of items('mainthread-work-breakdown')) console.log('  main thread:', x.groupLabel ?? x.group, `${Math.round(x.duration)} ms`);
}

const table = ['| Page | Performance | Accessibility | Best practices | SEO | LCP | TBT | CLS | |', '|---|---|---|---|---|---|---|---|---|',
  ...rows.map((r) => `| ${new URL(r.url).pathname} | ${r.perf} | ${r.a11y} | ${r.bp} | ${r.seo} | ${r.lcp} | ${r.tbt} | ${r.cls} | ${r.note} |`)].join('\n');
console.log(`\n${table}\n`);
if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `### Lighthouse (mobile)\n\n${table}\n`);
if (missed && process.env.LH_ENFORCE === '1') {
  console.error(`Performance below ${min} on at least one page.`);
  process.exit(1);
}
