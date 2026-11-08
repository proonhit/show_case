import { MAP_MODES, MODE_LABELS, ModelViewer, modeForKey, type MapMode } from '../src/index';

const $ = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;
const stage = $('stage');
const modesBar = $('modes');
const info = $('info');
const hint = $('hint');
const progress = $('progress');
const spin = $<HTMLButtonElement>('spin');
const lang = $<HTMLSelectElement>('lang');

const viewer = new ModelViewer(stage, {
  ktx2TranscoderPath: 'https://cdn.jsdelivr.net/npm/three@0.170.0/examples/jsm/libs/basis/',
});

const buttons = new Map<MapMode, HTMLButtonElement>();
for (const mode of MAP_MODES) {
  const b = document.createElement('button');
  b.onclick = () => viewer.setMode(mode);
  buttons.set(mode, b);
  modesBar.append(b);
}

function render() {
  const labels = MODE_LABELS[lang.value as 'en' | 'vi'];
  const available = viewer.availableModes;
  buttons.forEach((b, mode) => {
    b.textContent = labels[mode];
    b.disabled = !available[mode];
    b.title = available[mode] ? '' : 'This model has no map for this mode';
    b.setAttribute('aria-pressed', String(viewer.mode === mode));
  });
  spin.setAttribute('aria-pressed', String(viewer.autoRotate));
}

viewer.on('modechange', render);
viewer.on('autorotatechange', render);
viewer.on('progress', (p) => (progress.style.width = p.ratio === null ? '30%' : `${p.ratio * 100}%`));
viewer.on('load', (i) => {
  progress.style.width = '0';
  hint.hidden = true;
  info.textContent = `${i.triangles.toLocaleString()} triangles · ${i.meshes} meshes · ${i.materials} materials · ${i.animations} animations`;
  render();
});
viewer.on('error', (e) => {
  progress.style.width = '0';
  info.textContent = `Error: ${e.message}`;
});

lang.onchange = render;
spin.onclick = () => viewer.setAutoRotate(!viewer.autoRotate);
$('reset').onclick = () => viewer.resetCamera();
$('full').onclick = () => void viewer.toggleFullscreen();
addEventListener('keydown', (e) => {
  const mode = modeForKey(e.key);
  if (mode && !(e.target instanceof HTMLInputElement)) viewer.setMode(mode);
});

let objectUrl: string | undefined;
function open(file: File) {
  if (objectUrl) URL.revokeObjectURL(objectUrl);
  objectUrl = URL.createObjectURL(file);
  void viewer.load(objectUrl).catch(() => {});
}
$<HTMLInputElement>('file').onchange = (e) => {
  const f = (e.target as HTMLInputElement).files?.[0];
  if (f) open(f);
};
addEventListener('dragover', (e) => e.preventDefault());
addEventListener('drop', (e) => {
  e.preventDefault();
  const f = e.dataTransfer?.files[0];
  if (f) open(f);
});

const src = new URLSearchParams(location.search).get('src');
if (src) void viewer.load(src).catch(() => {});
render();
