import { defineComponent, h, onBeforeUnmount, onMounted, ref, shallowRef, watch, type PropType } from 'vue';
import { ModelViewer, type CameraView, type ModelInfo, type ViewerOptions, type ViewerProgress } from './ModelViewer';
import type { MapMode } from './modes';

/**
 * Thin Vue wrapper: `<ModelViewerView :src="url" v-model:mode="mode" />`.
 * The page draws its own controls; this component only owns the canvas.
 */
export const ModelViewerView = defineComponent({
  name: 'ModelViewerView',
  props: {
    src: { type: String, default: '' },
    mode: { type: String as PropType<MapMode>, default: 'final' },
    autoRotate: { type: Boolean, default: true },
    /** Degrees per second. */
    autoRotateSpeed: { type: Number, default: 20 },
    /** CSS colour; null = transparent. Undefined keeps the viewer's default. */
    background: { type: String as PropType<string | null>, default: undefined },
    /** Saved opening view; null = automatic framing. */
    camera: { type: Object as PropType<CameraView | null>, default: null },
    options: { type: Object as PropType<ViewerOptions>, default: () => ({}) },
  },
  emits: {
    'update:mode': (_: MapMode) => true,
    'update:autoRotate': (_: boolean) => true,
    progress: (_: ViewerProgress) => true,
    load: (_: ModelInfo) => true,
    error: (_: Error) => true,
  },
  setup(props, { emit, expose }) {
    const host = ref<HTMLDivElement>();
    const viewer = shallowRef<ModelViewer>();

    const load = async (src: string) => {
      if (!viewer.value || !src) return;
      try {
        await viewer.value.load(src);
        viewer.value.setMode(props.mode);
      } catch {
        // reported through the 'error' event
      }
    };

    onMounted(() => {
      const v = new ModelViewer(host.value!, {
        ...props.options,
        autoRotate: props.autoRotate,
        autoRotateSpeed: props.autoRotateSpeed,
        camera: props.camera,
        ...(props.background !== undefined ? { background: props.background } : {}),
      });
      v.on('progress', (p) => emit('progress', p));
      v.on('load', (i) => emit('load', i));
      v.on('error', (e) => emit('error', e));
      v.on('modechange', (m) => emit('update:mode', m));
      v.on('autorotatechange', (on) => emit('update:autoRotate', on));
      viewer.value = v;
      void load(props.src);
    });

    watch(() => props.src, load);
    watch(() => props.mode, (m) => viewer.value?.setMode(m));
    watch(() => props.autoRotate, (on) => viewer.value?.setAutoRotate(on));
    watch(() => props.autoRotateSpeed, (s) => viewer.value?.setAutoRotateSpeed(s));
    watch(() => props.background, (c) => c !== undefined && viewer.value?.setBackground(c));
    watch(() => props.camera, (c) => viewer.value?.setCamera(c), { deep: true });

    onBeforeUnmount(() => viewer.value?.dispose());

    expose({
      resetCamera: () => viewer.value?.resetCamera(),
      toggleFullscreen: () => viewer.value?.toggleFullscreen(),
      getCamera: () => viewer.value?.getCamera(),
      snapshot: () => viewer.value?.snapshot(),
      viewer,
    });

    return () => h('div', { ref: host, class: 'model-viewer', style: 'position:relative;width:100%;height:100%' });
  },
});
