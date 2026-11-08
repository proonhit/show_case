/** The seven ways a model can be shown. */
export type MapMode = 'final' | 'wireframe' | 'baseColor' | 'metallic' | 'roughness' | 'normal' | 'ao';

export const MAP_MODES: readonly MapMode[] = ['final', 'wireframe', 'baseColor', 'metallic', 'roughness', 'normal', 'ao'];

export const MODE_LABELS: Record<'en' | 'vi', Record<MapMode, string>> = {
  en: {
    final: 'Final',
    wireframe: 'Wireframe',
    baseColor: 'Base color',
    metallic: 'Metallic',
    roughness: 'Roughness',
    normal: 'Normal',
    ao: 'AO',
  },
  vi: {
    final: 'Hoàn chỉnh',
    wireframe: 'Khung lưới',
    baseColor: 'Màu gốc',
    metallic: 'Kim loại',
    roughness: 'Độ nhám',
    normal: 'Normal',
    ao: 'AO',
  },
};

/** Which texture maps one material carries. */
export interface MaterialMaps {
  map: boolean;
  metalnessMap: boolean;
  roughnessMap: boolean;
  normalMap: boolean;
  aoMap: boolean;
}

/**
 * A mode is offered when at least one material has the map it shows.
 * Final, wireframe and base color always work (base color falls back to the color factor).
 */
export function availableModes(materials: readonly MaterialMaps[]): Record<MapMode, boolean> {
  const any = (key: keyof MaterialMaps) => materials.some((m) => m[key]);
  return {
    final: true,
    wireframe: true,
    baseColor: true,
    metallic: any('metalnessMap'),
    roughness: any('roughnessMap'),
    normal: any('normalMap'),
    ao: any('aoMap'),
  };
}

/** Falls back to 'final' when the requested mode is unknown or not available for this model. */
export function resolveMode(requested: string | null | undefined, available: Record<MapMode, boolean>): MapMode {
  return MAP_MODES.includes(requested as MapMode) && available[requested as MapMode] ? (requested as MapMode) : 'final';
}

/** Keys 1–7 pick a mode, in the order of {@link MAP_MODES}. */
export function modeForKey(key: string): MapMode | null {
  const i = Number.parseInt(key, 10);
  return Number.isInteger(i) && i >= 1 && i <= MAP_MODES.length && String(i) === key ? MAP_MODES[i - 1] : null;
}
