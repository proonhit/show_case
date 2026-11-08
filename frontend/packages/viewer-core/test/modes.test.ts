import { describe, expect, it } from 'vitest';
import { availableModes, MAP_MODES, modeForKey, resolveMode, type MaterialMaps } from '../src/modes';

const none: MaterialMaps = { map: false, metalnessMap: false, roughnessMap: false, normalMap: false, aoMap: false };

describe('availableModes', () => {
  it('always offers final, wireframe and base color', () => {
    const a = availableModes([none]);
    expect([a.final, a.wireframe, a.baseColor]).toEqual([true, true, true]);
    expect([a.metallic, a.roughness, a.normal, a.ao]).toEqual([false, false, false, false]);
  });

  it('offers a map mode when any material has that map', () => {
    const a = availableModes([none, { ...none, roughnessMap: true, metalnessMap: true }, { ...none, aoMap: true }]);
    expect(a.metallic && a.roughness && a.ao).toBe(true);
    expect(a.normal).toBe(false);
  });

  it('works with no materials', () => {
    expect(availableModes([]).final).toBe(true);
  });
});

describe('resolveMode', () => {
  const a = availableModes([{ ...none, normalMap: true }]);
  it('keeps an available mode', () => expect(resolveMode('normal', a)).toBe('normal'));
  it('falls back to final when the map is missing', () => expect(resolveMode('ao', a)).toBe('final'));
  it('falls back to final for unknown or empty input', () => {
    expect(resolveMode('sparkle', a)).toBe('final');
    expect(resolveMode(null, a)).toBe('final');
  });
});

describe('modeForKey', () => {
  it('maps 1–7 in order', () => expect(MAP_MODES.map((_, i) => modeForKey(String(i + 1)))).toEqual([...MAP_MODES]));
  it('ignores other keys', () => {
    for (const k of ['0', '8', 'a', '1.5', ' 1', '01']) expect(modeForKey(k)).toBeNull();
  });
});
