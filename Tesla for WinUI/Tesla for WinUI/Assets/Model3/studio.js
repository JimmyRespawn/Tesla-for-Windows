import * as THREE from 'three';
import { Reflector } from './vendor/Reflector.js';

// A local studio environment, seamless floor and a cached planar reflection.
// All lighting is generated here; viewing the car needs no remote HDR assets.
export function createStudio(renderer, scene) {
  scene.background = new THREE.Color('#252932');
  scene.fog = new THREE.Fog('#252932', 12, 45);
  const environmentScene = new THREE.Scene();
  environmentScene.background = new THREE.Color('#151820');
  for (const [position, size, color, intensity] of [
    [[-1.5, 5, 0], [1.6, .04, 8], 0xffffff, 4.5],
    [[2.2, 5, 0], [.8, .04, 7], 0xe7efff, 3.2],
    [[-5, 2.6, 1], [.04, 2.4, 7], 0xfff5e8, 2.5],
    [[4, 3, -4], [.04, 2.2, 6], 0xd6e3ff, 2.4],
    [[0, 2, 5], [7, .7, .04], 0xffffff, 1.5]
  ]) {
    const panel = new THREE.Mesh(new THREE.BoxGeometry(...size),
      new THREE.MeshBasicMaterial({ color: new THREE.Color(color).multiplyScalar(intensity) }));
    panel.position.set(...position);
    environmentScene.add(panel);
  }
  const generator = new THREE.PMREMGenerator(renderer);
  const environment = generator.fromScene(environmentScene, .045);
  scene.environment = environment.texture;
  scene.environmentIntensity = .85;
  generator.dispose();
  environmentScene.traverse(o => { o.geometry?.dispose(); o.material?.dispose(); });

  scene.add(new THREE.HemisphereLight(0xe9eef8, 0x282b35, .65));
  const key = new THREE.DirectionalLight(0xfff7ec, 2.5);
  key.position.set(-3, 10, 4);
  key.castShadow = true;
  key.shadow.mapSize.set(1024, 1024);
  Object.assign(key.shadow.camera, { left: -5, right: 5, top: 5, bottom: -5, near: .5, far: 25 });
  key.shadow.radius = 5;
  key.shadow.blurSamples = 8;
  key.shadow.normalBias = .015;
  key.shadow.bias = -.00015;
  scene.add(key);
  const fill = new THREE.DirectionalLight(0xdce7ff, .75);
  fill.position.set(4, 3.5, -5);
  scene.add(fill);
  const edge = new THREE.DirectionalLight(0xffffff, 1.1);
  edge.position.set(2, 5, -4);
  scene.add(edge);

  const reflectionSize = 768;
  const shader = {
    name: 'StudioFloor',
    uniforms: {
      color: { value: null }, tDiffuse: { value: null }, textureMatrix: { value: null },
      texel: { value: new THREE.Vector2(1 / reflectionSize, 1 / reflectionSize) }
    },
    vertexShader: `
      uniform mat4 textureMatrix;
      varying vec4 reflectionUv;
      varying vec3 worldPosition;
      void main() {
        reflectionUv = textureMatrix * vec4(position, 1.0);
        worldPosition = (modelMatrix * vec4(position, 1.0)).xyz;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: `
      uniform sampler2D tDiffuse;
      uniform vec2 texel;
      varying vec4 reflectionUv;
      varying vec3 worldPosition;
      void main() {
        vec2 uv = reflectionUv.xy / reflectionUv.w;
        vec2 blur = texel * 1.7;
        vec3 reflected = texture2D(tDiffuse, uv).rgb * .4;
        reflected += texture2D(tDiffuse, uv + vec2(blur.x, 0.0)).rgb * .15;
        reflected += texture2D(tDiffuse, uv - vec2(blur.x, 0.0)).rgb * .15;
        reflected += texture2D(tDiffuse, uv + vec2(0.0, blur.y)).rgb * .15;
        reflected += texture2D(tDiffuse, uv - vec2(0.0, blur.y)).rgb * .15;
        float distanceFromCar = length(worldPosition.xz / vec2(5.8, 4.8));
        float pool = exp(-distanceFromCar * distanceFromCar * 1.3);
        vec3 floorColor = mix(vec3(.038, .044, .055), vec3(.20, .21, .235), pool);
        float reflection = .23 * pool;
        vec3 result = mix(floorColor, reflected, reflection);
        float fog = smoothstep(10.0, 35.0, distance(cameraPosition, worldPosition));
        // Fade into the scene background before the far edge can form a horizon line.
        gl_FragColor = vec4(result, 1.0 - fog);
        #include <tonemapping_fragment>
        #include <colorspace_fragment>
      }`
  };
  const floor = new Reflector(new THREE.PlaneGeometry(160, 160), {
    textureWidth: reflectionSize, textureHeight: reflectionSize, multisample: 0, clipBias: .003, shader
  });
  floor.rotation.x = -Math.PI / 2;
  floor.position.y = -.012;
  floor.material.transparent = true;
  floor.material.depthWrite = false;
  scene.add(floor);

  const shadow = new THREE.Mesh(new THREE.PlaneGeometry(160, 160), new THREE.ShadowMaterial({ opacity: .34 }));
  shadow.rotation.x = -Math.PI / 2;
  shadow.position.y = -.008;
  shadow.receiveShadow = true;
  scene.add(shadow);

  let disposed = false, dirty = true, reflectionUpdates = 0, lastPose = '', nextPose = '';
  let lastUpdate = -Infinity;
  const lastWorld = new THREE.Matrix4(), lastProjection = new THREE.Matrix4();
  const reflectionRender = floor.onBeforeRender;
  const differs = (a, b) => a.elements.some((value, i) => Math.abs(value - b.elements[i]) > .00001);
  floor.onBeforeRender = function (renderer, scene, camera, ...args) {
    if (disposed) return;
    dirty ||= differs(camera.matrixWorld, lastWorld) || differs(camera.projectionMatrix, lastProjection) || nextPose !== lastPose;
    const now = performance.now();
    if (!dirty || now - lastUpdate < 50) return;
    reflectionRender.call(this, renderer, scene, camera, ...args);
    reflectionUpdates++;
    lastUpdate = now;
    lastWorld.copy(camera.matrixWorld);
    lastProjection.copy(camera.projectionMatrix);
    lastPose = nextPose;
    dirty = false;
  };

  return {
    update(portAngle, mirrorAngle, flashing, locked) {
      nextPose = `${portAngle.toFixed(3)}:${mirrorAngle.toFixed(3)}:${flashing}:${locked}`;
    },
    getDiagnostics: () => ({ reflectionUpdates, reflectionSize, disposed, reflectionStrength: .23, seamlessFloor: true }),
    dispose() {
      if (disposed) return;
      disposed = true;
      scene.remove(floor);
      floor.dispose();
      floor.geometry.dispose();
      key.shadow.dispose();
      environment.dispose();
    }
  };
}
