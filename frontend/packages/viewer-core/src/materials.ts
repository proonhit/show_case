import {
  Color,
  Material,
  Mesh,
  MeshBasicMaterial,
  MeshStandardMaterial,
  Object3D,
  SRGBColorSpace,
  type Texture,
} from 'three';
import { availableModes, type MapMode, type MaterialMaps } from './modes';

type Channel = 'r' | 'g' | 'b' | 'rgb';

const WIREFRAME_COLOR = 0x9aa4b2;

/** Reads the maps of a standard (glTF PBR) material. Other material types count as having none. */
export function mapsOf(material: Material): MaterialMaps {
  const m = material as Partial<MeshStandardMaterial>;
  return {
    map: !!m.map,
    metalnessMap: !!m.metalnessMap,
    roughnessMap: !!m.roughnessMap,
    normalMap: !!m.normalMap,
    aoMap: !!m.aoMap,
  };
}

/**
 * Unlit material showing one channel of a data texture as grey, at the value stored in the file.
 * MeshBasicMaterial keeps skinning, morph targets, UV sets and texture transforms working;
 * only the map sampling is replaced. The value is pre-linearised so the final sRGB output equals it.
 */
function channelMaterial(source: MeshStandardMaterial, texture: Texture | null, channel: Channel, factor: number, fallback: Color): MeshBasicMaterial {
  const mat = new MeshBasicMaterial({
    map: texture,
    color: texture ? 0xffffff : fallback,
    side: source.side,
    transparent: source.transparent,
    alphaTest: source.alphaTest,
    toneMapped: false,
  });
  if (texture) {
    const f = factor.toFixed(4);
    const sample = channel === 'rgb' ? 't.rgb' : `vec3(t.${channel})`;
    mat.onBeforeCompile = (shader) => {
      shader.fragmentShader = shader.fragmentShader.replace(
        '#include <map_fragment>',
        `#ifdef USE_MAP
          vec4 t = texture2D( map, vMapUv );
          diffuseColor.rgb = pow( clamp( ${sample} * ${f}, 0.0, 1.0 ), vec3( 2.2 ) );
        #endif`,
      );
    };
    mat.customProgramCacheKey = () => `channel:${channel}:${f}`;
  }
  return mat;
}

/** Grey at the given value as it should appear on screen. */
const grey = (v: number) => new Color().setRGB(v, v, v, SRGBColorSpace);

/** Builds the stand-in material for one mode. `final` is the original and is not built here. */
export function buildModeMaterial(source: Material, mode: Exclude<MapMode, 'final'>): Material {
  const std = source as MeshStandardMaterial;
  const isStandard = (source as MeshStandardMaterial).isMeshStandardMaterial === true;

  switch (mode) {
    case 'wireframe':
      return new MeshBasicMaterial({ color: WIREFRAME_COLOR, wireframe: true, toneMapped: false });
    case 'baseColor':
      return new MeshBasicMaterial({
        map: isStandard ? std.map : null,
        color: isStandard ? std.color.clone() : new Color(0xcccccc),
        side: source.side,
        transparent: source.transparent,
        opacity: source.opacity,
        alphaTest: source.alphaTest,
        alphaMap: isStandard ? std.alphaMap : null,
        toneMapped: false,
      });
    // glTF stores metallic in B and roughness in G of one texture, occlusion in R.
    case 'metallic':
      return channelMaterial(std, isStandard ? std.metalnessMap : null, 'b', isStandard ? std.metalness : 0, grey(isStandard ? std.metalness : 0));
    case 'roughness':
      return channelMaterial(std, isStandard ? std.roughnessMap : null, 'g', isStandard ? std.roughness : 1, grey(isStandard ? std.roughness : 1));
    case 'ao':
      return channelMaterial(std, isStandard ? std.aoMap : null, 'r', 1, grey(1));
    case 'normal':
      return channelMaterial(std, isStandard ? std.normalMap : null, 'rgb', 1, new Color().setRGB(0.5, 0.5, 1, SRGBColorSpace));
  }
}

/**
 * Swaps every mesh's material for the chosen mode without reloading the file.
 * Stand-in materials are built on first use and kept until {@link dispose}.
 */
export class MaterialModes {
  private readonly originals = new Map<Mesh, Material | Material[]>();
  private readonly cache = new Map<Material, Map<MapMode, Material>>();
  private current: MapMode = 'final';
  readonly available: Record<MapMode, boolean>;

  constructor(root: Object3D) {
    const all: Material[] = [];
    root.traverse((o) => {
      const mesh = o as Mesh;
      if (!mesh.isMesh) return;
      this.originals.set(mesh, mesh.material);
      all.push(...(Array.isArray(mesh.material) ? mesh.material : [mesh.material]));
    });
    this.available = availableModes([...new Set(all)].map(mapsOf));
  }

  get mode(): MapMode {
    return this.current;
  }

  /** Applies a mode. Returns false (and changes nothing) when the model lacks the map for it. */
  apply(mode: MapMode): boolean {
    if (!this.available[mode]) return false;
    for (const [mesh, original] of this.originals) {
      mesh.material = mode === 'final'
        ? original
        : Array.isArray(original)
          ? original.map((m) => this.standIn(m, mode))
          : this.standIn(original, mode);
    }
    this.current = mode;
    return true;
  }

  /** Puts the original materials back and frees the stand-ins (textures belong to the originals and are kept). */
  dispose(): void {
    this.apply('final');
    for (const byMode of this.cache.values()) for (const m of byMode.values()) m.dispose();
    this.cache.clear();
  }

  private standIn(source: Material, mode: Exclude<MapMode, 'final'>): Material {
    let byMode = this.cache.get(source);
    if (!byMode) this.cache.set(source, (byMode = new Map()));
    let m = byMode.get(mode);
    if (!m) byMode.set(mode, (m = buildModeMaterial(source, mode)));
    return m;
  }
}
