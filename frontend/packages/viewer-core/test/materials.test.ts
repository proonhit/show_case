import { BoxGeometry, DataTexture, Group, Mesh, MeshBasicMaterial, MeshStandardMaterial } from 'three';
import { describe, expect, it } from 'vitest';
import { buildModeMaterial, MaterialModes } from '../src/materials';

const tex = () => new DataTexture(new Uint8Array([255, 128, 0, 255]), 1, 1);

function scene() {
  const textured = new MeshStandardMaterial({ map: tex(), roughnessMap: tex(), metalnessMap: tex(), normalMap: tex() });
  const plain = new MeshStandardMaterial({ color: 0xff0000, roughness: 0.3, metalness: 1 });
  const root = new Group();
  const a = new Mesh(new BoxGeometry(), textured);
  const b = new Mesh(new BoxGeometry(), [plain, textured]);
  root.add(a, b);
  return { root, a, b, textured, plain };
}

describe('MaterialModes', () => {
  it('offers only the modes the model has maps for', () => {
    const { root } = scene();
    const m = new MaterialModes(root);
    expect(m.available.roughness).toBe(true);
    expect(m.available.normal).toBe(true);
    expect(m.available.ao).toBe(false);
  });

  it('swaps materials (including multi-material meshes) and restores the originals', () => {
    const { root, a, b, textured, plain } = scene();
    const m = new MaterialModes(root);

    expect(m.apply('roughness')).toBe(true);
    expect(m.mode).toBe('roughness');
    expect(a.material).toBeInstanceOf(MeshBasicMaterial);
    expect((a.material as MeshBasicMaterial).map).toBe(textured.roughnessMap);
    const [first, second] = b.material as MeshBasicMaterial[];
    expect(first.map).toBeNull(); // no roughness map → flat grey at the factor
    expect(second).toBe(a.material); // same source material → same stand-in

    expect(m.apply('final')).toBe(true);
    expect(a.material).toBe(textured);
    expect(b.material).toEqual([plain, textured]);
  });

  it('refuses a mode with no map and leaves materials alone', () => {
    const { root, a, textured } = scene();
    const m = new MaterialModes(root);
    expect(m.apply('ao')).toBe(false);
    expect(m.mode).toBe('final');
    expect(a.material).toBe(textured);
  });

  it('dispose puts the originals back', () => {
    const { root, a, textured } = scene();
    const m = new MaterialModes(root);
    m.apply('wireframe');
    m.dispose();
    expect(a.material).toBe(textured);
  });
});

describe('buildModeMaterial', () => {
  it('base color keeps the albedo texture and colour', () => {
    const src = new MeshStandardMaterial({ map: tex(), color: 0x336699 });
    const m = buildModeMaterial(src, 'baseColor') as MeshBasicMaterial;
    expect(m.map).toBe(src.map);
    expect(m.color.getHex()).toBe(0x336699);
  });

  it('wireframe is unlit lines', () => {
    expect((buildModeMaterial(new MeshStandardMaterial(), 'wireframe') as MeshBasicMaterial).wireframe).toBe(true);
  });

  it('channel modes use different shader programs per channel', () => {
    const src = new MeshStandardMaterial({ roughnessMap: tex(), metalnessMap: tex() });
    const r = buildModeMaterial(src, 'roughness');
    const mt = buildModeMaterial(src, 'metallic');
    expect(r.customProgramCacheKey()).not.toBe(mt.customProgramCacheKey());
  });
});
