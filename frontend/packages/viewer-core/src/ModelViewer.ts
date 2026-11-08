import {
  ACESFilmicToneMapping,
  AnimationMixer,
  Box3,
  Clock,
  Color,
  type InstancedMesh,
  MathUtils,
  type Mesh,
  Object3D,
  PerspectiveCamera,
  PMREMGenerator,
  Scene,
  SRGBColorSpace,
  Sphere,
  Vector3,
  WebGLRenderer,
  type Texture,
} from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFLoader, type GLTF } from 'three/addons/loaders/GLTFLoader.js';
import { KTX2Loader } from 'three/addons/loaders/KTX2Loader.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { MaterialModes } from './materials';
import { availableModes, MAP_MODES, type MapMode } from './modes';

export interface ViewerOptions {
  /** Spin the model until the user touches it. Default true. */
  autoRotate?: boolean;
  /** Degrees per second. Default 20. */
  autoRotateSpeed?: number;
  /** CSS colour, or null for a transparent canvas. Default '#1A1C21'. */
  background?: string | null;
  /** URL of an equirectangular .hdr for lighting. Default: a built-in neutral studio. */
  environmentUrl?: string;
  /** Folder with basis_transcoder.js/.wasm. Needed only for KTX2 textures. */
  ktx2TranscoderPath?: string;
  /** Upper limit for devicePixelRatio. Default 2 on desktop, 1.5 on touch devices. */
  maxPixelRatio?: number;
  /** Vertical orbit limits in degrees from straight up. Default 10–170. */
  minPolarAngle?: number;
  maxPolarAngle?: number;
  /** Play the first animation clip when the model has one. Default true. */
  playAnimation?: boolean;
  /** A saved view to open with instead of automatic framing. */
  camera?: CameraView | null;
}

/** Camera position and the point it looks at, in model units. */
export interface CameraView {
  position: [number, number, number];
  target: [number, number, number];
}

export interface ViewerProgress {
  loaded: number;
  /** 0 when the server sends no Content-Length. */
  total: number;
  /** 0–1, or null when the total is unknown. */
  ratio: number | null;
}

export interface ModelInfo {
  triangles: number;
  meshes: number;
  materials: number;
  animations: number;
  available: Record<MapMode, boolean>;
}

interface ViewerEvents {
  progress: ViewerProgress;
  load: ModelInfo;
  error: Error;
  modechange: MapMode;
  autorotatechange: boolean;
}

type Listener<T> = (value: T) => void;

/**
 * 360° viewer for one .glb/.gltf model: orbit + turntable, framing, animation,
 * and seven map modes that swap materials without reloading the file.
 */
export class ModelViewer {
  readonly canvas: HTMLCanvasElement;
  private readonly renderer: WebGLRenderer;
  private readonly scene = new Scene();
  private readonly camera = new PerspectiveCamera(40, 1, 0.01, 1000);
  private readonly controls: OrbitControls;
  private readonly loader: GLTFLoader;
  private readonly ktx2?: KTX2Loader;
  private readonly clock = new Clock();
  private readonly resizeObserver: ResizeObserver;
  private readonly visibilityObserver?: IntersectionObserver;
  private readonly listeners = new Map<keyof ViewerEvents, Set<Listener<never>>>();
  private envTexture?: Texture;
  private model?: Object3D;
  private modes?: MaterialModes;
  private mixer?: AnimationMixer;
  private home?: { position: Vector3; target: Vector3 };
  private autoHome?: { position: Vector3; target: Vector3 };
  private loadToken = 0;
  private visible = true;
  private disposed = false;

  constructor(private readonly container: HTMLElement, private readonly options: ViewerOptions = {}) {
    // Always alpha, so the background can switch to transparent later (setBackground(null)).
    this.renderer = new WebGLRenderer({ antialias: true, alpha: true });
    this.renderer.outputColorSpace = SRGBColorSpace;
    this.renderer.toneMapping = ACESFilmicToneMapping;
    this.renderer.setPixelRatio(pixelRatio(options.maxPixelRatio));
    this.canvas = this.renderer.domElement;
    this.canvas.style.display = 'block';
    this.canvas.style.width = '100%';
    this.canvas.style.height = '100%';
    this.canvas.style.touchAction = 'none';
    container.appendChild(this.canvas);

    if (options.background !== null) this.scene.background = new Color(options.background ?? '#1A1C21');

    this.controls = new OrbitControls(this.camera, this.canvas);
    this.controls.enableDamping = true;
    this.controls.enablePan = false;
    this.controls.minPolarAngle = MathUtils.degToRad(options.minPolarAngle ?? 10);
    this.controls.maxPolarAngle = MathUtils.degToRad(options.maxPolarAngle ?? 170);
    // OrbitControls' autoRotateSpeed 2 = 30 s per turn (12°/s).
    this.controls.autoRotateSpeed = ((options.autoRotateSpeed ?? 20) / 12) * 2;
    this.controls.autoRotate = options.autoRotate ?? true;
    this.controls.addEventListener('start', () => this.setAutoRotate(false));

    this.loader = new GLTFLoader().setMeshoptDecoder(MeshoptDecoder);
    if (options.ktx2TranscoderPath) {
      this.ktx2 = new KTX2Loader().setTranscoderPath(options.ktx2TranscoderPath).detectSupport(this.renderer);
      this.loader.setKTX2Loader(this.ktx2);
    }

    void this.setupEnvironment();

    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(container);
    this.resize();

    // Off-screen viewers (e.g. lower on a long page) stop rendering to save battery.
    if (typeof IntersectionObserver !== 'undefined') {
      this.visibilityObserver = new IntersectionObserver(([entry]) => (this.visible = entry.isIntersecting));
      this.visibilityObserver.observe(container);
    }

    this.renderer.setAnimationLoop(() => this.frame());
  }

  // ---- events -------------------------------------------------------------

  on<K extends keyof ViewerEvents>(event: K, listener: Listener<ViewerEvents[K]>): () => void {
    let set = this.listeners.get(event);
    if (!set) this.listeners.set(event, (set = new Set()));
    set.add(listener as Listener<never>);
    return () => set.delete(listener as Listener<never>);
  }

  private emit<K extends keyof ViewerEvents>(event: K, value: ViewerEvents[K]): void {
    this.listeners.get(event)?.forEach((l) => (l as Listener<ViewerEvents[K]>)(value));
  }

  // ---- loading ------------------------------------------------------------

  /** Loads a model from a URL (or object URL), replacing the current one. */
  async load(url: string): Promise<ModelInfo> {
    const token = ++this.loadToken;
    try {
      const gltf = await this.loader.loadAsync(url, (e) =>
        this.emit('progress', { loaded: e.loaded, total: e.total, ratio: e.total ? e.loaded / e.total : null }));
      if (token !== this.loadToken || this.disposed) throw new Error('Superseded by a newer load.');
      return this.show(gltf);
    } catch (e) {
      const error = e instanceof Error ? e : new Error(String(e));
      if (token === this.loadToken) this.emit('error', error);
      throw error;
    }
  }

  private show(gltf: GLTF): ModelInfo {
    this.unload();
    const model = gltf.scene;
    this.model = model;
    this.scene.add(model);
    this.modes = new MaterialModes(model);

    if (gltf.animations.length && (this.options.playAnimation ?? true)) {
      this.mixer = new AnimationMixer(model);
      this.mixer.clipAction(gltf.animations[0]).play();
    }

    this.frameModel(model);
    const info = { ...countModel(model), animations: gltf.animations.length, available: this.modes.available };
    // 'final' first, so a listener of 'load' can pick another mode without being overwritten.
    this.emit('modechange', 'final');
    this.emit('load', info);
    return info;
  }

  private unload(): void {
    if (!this.model) return;
    this.modes?.dispose();
    this.mixer?.stopAllAction();
    this.scene.remove(this.model);
    disposeObject(this.model);
    this.model = this.modes = this.mixer = undefined;
  }

  // ---- modes --------------------------------------------------------------

  get mode(): MapMode {
    return this.modes?.mode ?? 'final';
  }

  get availableModes(): Record<MapMode, boolean> {
    return this.modes?.available ?? availableModes([]);
  }

  /** Switches the map mode. Returns false when the model has no map for it. */
  setMode(mode: MapMode): boolean {
    if (!this.modes || !MAP_MODES.includes(mode)) return false;
    const changed = this.modes.mode !== mode;
    const ok = this.modes.apply(mode);
    if (ok && changed) this.emit('modechange', mode);
    return ok;
  }

  // ---- camera -------------------------------------------------------------

  get autoRotate(): boolean {
    return this.controls.autoRotate;
  }

  setAutoRotate(on: boolean): void {
    if (this.controls.autoRotate === on) return;
    this.controls.autoRotate = on;
    this.emit('autorotatechange', on);
  }

  /** Back to the framing chosen when the model loaded. */
  resetCamera(): void {
    if (!this.home) return;
    this.camera.position.copy(this.home.position);
    this.controls.target.copy(this.home.target);
    this.controls.update();
  }

  /** Where the camera is now — to save as the model's opening view. */
  getCamera(): CameraView {
    const p = this.camera.position;
    const t = this.controls.target;
    const r = (n: number) => Math.round(n * 10000) / 10000;
    return { position: [r(p.x), r(p.y), r(p.z)], target: [r(t.x), r(t.y), r(t.z)] };
  }

  /** Sets the opening view (null = automatic framing) and moves the camera there. */
  setCamera(view: CameraView | null): void {
    this.options.camera = view;
    if (view) this.home = { position: new Vector3(...view.position), target: new Vector3(...view.target) };
    else if (this.autoHome) this.home = { position: this.autoHome.position.clone(), target: this.autoHome.target.clone() };
    this.resetCamera();
  }

  /** CSS colour, or null for a transparent canvas background. */
  setBackground(color: string | null): void {
    this.scene.background = color === null ? null : new Color(color);
  }

  /** Degrees per second while the turntable runs. */
  setAutoRotateSpeed(degreesPerSecond: number): void {
    this.controls.autoRotateSpeed = (degreesPerSecond / 12) * 2;
  }

  /**
   * The current view as an image (for a thumbnail). Renders a frame and reads it straight away,
   * so it works without preserveDrawingBuffer.
   */
  async snapshot(type: 'image/webp' | 'image/png' | 'image/jpeg' = 'image/webp', quality = 0.9): Promise<Blob> {
    this.renderer.render(this.scene, this.camera);
    const url = this.canvas.toDataURL(type, quality);
    return (await fetch(url)).blob();
  }

  /** Fullscreen on the container (so overlays such as the mode bar stay visible). */
  async toggleFullscreen(): Promise<void> {
    if (document.fullscreenElement) await document.exitFullscreen();
    else await this.container.requestFullscreen?.();
  }

  /** Fits the camera to the model's bounding sphere, looking slightly from above. */
  private frameModel(model: Object3D): void {
    const box = new Box3().setFromObject(model);
    const sphere = box.getBoundingSphere(new Sphere());
    const radius = Math.max(sphere.radius, 1e-3);
    const fov = MathUtils.degToRad(this.camera.fov);
    const fitHeight = radius / Math.sin(fov / 2);
    const fitWidth = fitHeight / Math.min(1, this.camera.aspect);
    const distance = Math.max(fitHeight, fitWidth) * 1.05;

    const direction = new Vector3(0.6, 0.35, 1).normalize();
    this.autoHome = { position: sphere.center.clone().addScaledVector(direction, distance), target: sphere.center.clone() };
    const saved = this.options.camera;
    this.home = saved
      ? { position: new Vector3(...saved.position), target: new Vector3(...saved.target) }
      : { position: this.autoHome.position.clone(), target: this.autoHome.target.clone() };
    this.camera.near = distance / 100;
    this.camera.far = distance * 100;
    this.camera.updateProjectionMatrix();
    this.controls.minDistance = radius * 0.5;
    this.controls.maxDistance = distance * 4;
    this.resetCamera();
  }

  // ---- rendering ----------------------------------------------------------

  private async setupEnvironment(): Promise<void> {
    const pmrem = new PMREMGenerator(this.renderer);
    try {
      if (this.options.environmentUrl) {
        const { RGBELoader } = await import('three/addons/loaders/RGBELoader.js');
        const hdr = await new RGBELoader().loadAsync(this.options.environmentUrl);
        this.envTexture = pmrem.fromEquirectangular(hdr).texture;
        hdr.dispose();
      } else {
        this.envTexture = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
      }
      if (this.disposed) this.envTexture.dispose();
      else this.scene.environment = this.envTexture;
    } catch (e) {
      this.emit('error', e instanceof Error ? e : new Error(String(e)));
    } finally {
      pmrem.dispose();
    }
  }

  private resize(): void {
    const { clientWidth: w, clientHeight: h } = this.container;
    if (!w || !h) return;
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  private frame(): void {
    const dt = this.clock.getDelta();
    if (!this.visible) return;
    this.mixer?.update(dt);
    this.controls.update(dt);
    this.renderer.render(this.scene, this.camera);
  }

  dispose(): void {
    this.disposed = true;
    this.loadToken++;
    this.renderer.setAnimationLoop(null);
    this.resizeObserver.disconnect();
    this.visibilityObserver?.disconnect();
    this.unload();
    this.envTexture?.dispose();
    this.controls.dispose();
    this.ktx2?.dispose();
    this.renderer.dispose();
    this.canvas.remove();
    this.listeners.clear();
  }
}

/** Phones and tablets get a lower pixel ratio: big savings, little visible difference. */
function pixelRatio(max?: number): number {
  const touch = typeof matchMedia !== 'undefined' && matchMedia('(pointer: coarse)').matches;
  return Math.min(globalThis.devicePixelRatio || 1, max ?? (touch ? 1.5 : 2));
}

/** Counts triangles, meshes and distinct materials. */
export function countModel(root: Object3D): { triangles: number; meshes: number; materials: number } {
  let triangles = 0;
  let meshes = 0;
  const materials = new Set<unknown>();
  root.traverse((o) => {
    const mesh = o as Mesh;
    if (!mesh.isMesh) return;
    meshes++;
    const g = mesh.geometry;
    const count = g.index ? g.index.count : g.attributes.position?.count ?? 0;
    triangles += Math.floor(count / 3) * ((mesh as InstancedMesh).isInstancedMesh ? (mesh as InstancedMesh).count : 1);
    (Array.isArray(mesh.material) ? mesh.material : [mesh.material]).forEach((m) => materials.add(m));
  });
  return { triangles, meshes, materials: materials.size };
}

function disposeObject(root: Object3D): void {
  const textures = new Set<Texture>();
  root.traverse((o) => {
    const mesh = o as Mesh;
    if (!mesh.isMesh) return;
    mesh.geometry.dispose();
    for (const m of Array.isArray(mesh.material) ? mesh.material : [mesh.material]) {
      for (const value of Object.values(m)) if ((value as Texture)?.isTexture) textures.add(value as Texture);
      m.dispose();
    }
  });
  textures.forEach((t) => t.dispose());
}
