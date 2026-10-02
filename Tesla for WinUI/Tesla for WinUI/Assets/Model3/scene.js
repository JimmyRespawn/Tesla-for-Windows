import * as THREE from 'three';
import { OrbitControls } from './vendor/OrbitControls.js';
import { GLTFLoader } from './vendor/GLTFLoader.js';
import { createStudio } from './studio.js';

const canvas = document.querySelector('#scene');
const notify = message => {
  if (!window.chrome?.webview?.postMessage) return false;
  window.chrome.webview.postMessage(message);
  return true;
};
const loading = document.querySelector('#loading');
let renderer, controls, car, studio, animationFrame, disposed = false, ready = false;
const previewMode = new URLSearchParams(location.search).has('preview');
const state = { locked: null, portOpen: null };
const connection = { mode: 'unavailable', vehicleName: 'MODEL 3', canControl: false, busy: false, status: '正在连接车辆…', error: '', lastUpdated: null };
// Unknown API values stay unknown. The model keeps a neutral pose, or its last
// confirmed pose, until the vehicle supplies a new value.
const visualState = { locked: false, portOpen: false };
let pendingAction = null;
let portAngle = 0, mirrorAngle = 0, flashUntil = 0, cameraMotion = null;
let shadowPortAngle = Infinity, shadowMirrorAngle = Infinity;
const scene = new THREE.Scene();
scene.background = new THREE.Color('#10151e');
scene.fog = new THREE.Fog('#10151e', 15, 35);
const camera = new THREE.PerspectiveCamera(34, 1, .05, 60);
const carBounds = new THREE.Box3();
let activeView = 'front', safeArea = null;
const frontView = { position: new THREE.Vector3(-5.25, 2.13, 4.87), target: new THREE.Vector3(0, .55, 0) };
const portView = { position: new THREE.Vector3(4.15, 1.77, 3.25), target: new THREE.Vector3(1.5, .84, .6) };
let hinge, mirrorLeft, mirrorRight, portLED, socketAssembly, indicatorMaterials = [], headlightMaterials = [];
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

function showState() {
  document.querySelector('#lock-label').textContent = state.locked === null ? '锁车状态未知' : state.locked ? '已上锁' : '已解锁';
  document.querySelector('#port-label').textContent = state.portOpen === null ? '充电口状态未知' : state.portOpen ? '充电口已打开' : '充电口已关闭';
  document.querySelector('#lock-pill').classList.toggle('active', state.locked === false);
  document.querySelector('#port-pill').classList.toggle('active', state.portOpen === true);
  document.querySelector('#lock-pill').classList.toggle('unknown', state.locked === null);
  document.querySelector('#port-pill').classList.toggle('unknown', state.portOpen === null);
  document.querySelector('#vehicle-name').textContent = connection.vehicleName || 'MODEL 3';
  document.querySelector('#connection').className = 'connection ' + connection.mode;
  document.querySelector('#connection-label').textContent = { live: '实时车辆', demo: '演示模式', unavailable: '未连接车辆' }[connection.mode];
  const updated = connection.lastUpdated ? new Date(connection.lastUpdated) : null;
  document.querySelector('#last-updated').textContent = updated && !Number.isNaN(updated.getTime())
    ? '更新于 ' + updated.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false })
    : connection.mode === 'demo' ? '本地交互体验' : '等待车辆状态';
  document.querySelector('#operation-status').textContent = connection.status || (connection.busy ? '正在处理…' : '已准备就绪');
  document.querySelector('#mode-note').textContent = {
    live: '操作将发送到真实车辆，模型以车辆回报为准。',
    demo: '仅改变演示模型，不发送车辆指令。',
    unavailable: '登录并选择车辆后，可在这里进行操作。'
  }[connection.mode];
  const error = document.querySelector('#operation-error');
  error.textContent = connection.error;
  error.hidden = !connection.error;
  const panel = document.querySelector('#control-panel');
  panel.classList.toggle('busy', connection.busy);
  panel.setAttribute('aria-busy', String(connection.busy));
  const canAct = ready && !disposed && connection.canControl && connection.mode !== 'unavailable' && !connection.busy;
  const setEnabled = (id, enabled) => {
    const button = document.querySelector(id);
    button.disabled = !enabled;
    button.setAttribute('aria-disabled', String(!enabled));
    button.classList.toggle('pending', connection.busy && button.dataset.action === pendingAction);
  };
  const portButton = document.querySelector('#port-button');
  portButton.dataset.action = state.portOpen === true ? 'closePort' : 'openPort';
  document.querySelector('#port-button-label').textContent = state.portOpen === true ? '关闭充电口' : '打开充电口';
  setEnabled('#port-button', canAct);
  setEnabled('#unlock-button', canAct && state.locked !== false);
  setEnabled('#lock-button', canAct && state.locked !== true);
  setEnabled('#refresh-button', canAct);
  setEnabled('#port-view-button', ready && !disposed);
  setEnabled('#reset-view-button', ready && !disposed);
}
function setConnection(next = {}) {
  if (['live', 'demo', 'unavailable'].includes(next.mode)) connection.mode = next.mode;
  for (const key of ['canControl', 'busy']) if (typeof next[key] === 'boolean') connection[key] = next[key];
  for (const [key, limit] of [['vehicleName', 80], ['status', 240], ['error', 600]]) {
    if (typeof next[key] === 'string') connection[key] = next[key].slice(0, limit);
    else if (next[key] === null) connection[key] = '';
  }
  if ('lastUpdated' in next && (next.lastUpdated === null || typeof next.lastUpdated === 'string' || typeof next.lastUpdated === 'number')) connection.lastUpdated = next.lastUpdated;
  if (connection.mode === 'unavailable') connection.canControl = false;
  if (!connection.busy) pendingAction = null;
  showState();
  return { ...connection };
}
function applyVehicleState(next = {}, options = {}) {
  for (const key of ['locked', 'portOpen']) {
    if (!(key in next)) continue;
    if (next[key] !== null && typeof next[key] !== 'boolean') throw new Error('无效车辆状态');
    state[key] = next[key];
    if (typeof next[key] === 'boolean') visualState[key] = next[key];
  }
  if (options.action === 'lock' && state.locked === true) flashUntil = performance.now() + 850;
  if (options.action === 'unlock' && state.locked === false) flashUntil = performance.now() + 1100;
  if (options.focusPort || (options.action === 'openPort' && state.portOpen === true)) view('port');
  showState();
  return { ...state };
}
function view(preset) {
  if (!ready) return;
  activeView = preset;
  const to = preset === 'port' ? portView : frontView;
  cameraMotion = { fromPosition: camera.position.clone(), fromTarget: controls.target.clone(), to, start: performance.now(), duration: reducedMotion ? 0 : 850 };
}
function command(action) {
  if (!ready || disposed) throw new Error('3D 模型尚未准备就绪');
  if (action === 'resetView' || action === 'portView') {
    view(action === 'portView' ? 'port' : 'front');
    return { ...state };
  }
  if (!previewMode) throw new Error('车辆操作需要由应用确认');
  switch (action) {
    case 'togglePort': applyVehicleState({ portOpen: state.portOpen !== true }, { focusPort: state.portOpen !== true }); break;
    case 'openPort': applyVehicleState({ portOpen: true }, { action }); break;
    case 'closePort': applyVehicleState({ portOpen: false }, { action }); break;
    case 'lock': applyVehicleState({ locked: true }, { action }); break;
    case 'unlock': applyVehicleState({ locked: false }, { action }); break;
    case 'refresh': break;
    case 'reset': applyVehicleState({ locked: true, portOpen: false }); view('front'); break;
    default: throw new Error('未知模型动作');
  }
  setConnection({ busy: false, error: '', status: action === 'refresh' ? '演示状态已刷新' : '演示操作已完成', lastUpdated: new Date().toISOString() });
  const result = { ...state };
  notify({ type: 'state', ...result });
  return result;
}
function requestAction(action) {
  if (!ready || disposed) return;
  if (action === 'portView' || action === 'resetView') { command(action); return; }
  if (!connection.canControl || connection.mode === 'unavailable' || connection.busy) return;
  if (previewMode && connection.mode === 'demo') { command(action); return; }
  if (!['openPort', 'closePort', 'lock', 'unlock', 'refresh'].includes(action)) return;
  pendingAction = action;
  const labels = { openPort: '正在打开充电口…', closePort: '正在关闭充电口…', lock: '正在上锁…', unlock: '正在解锁…', refresh: '正在刷新车辆状态…' };
  setConnection({ busy: true, error: '', status: labels[action] });
  try {
    if (!notify({ type: 'command', action })) throw new Error('与应用的连接已断开，请返回后重新打开页面。');
  } catch (error) {
    setConnection({ busy: false, error: error.message, status: '操作未发送' });
  }
}
async function initialize() {
  try {
    renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false, powerPreference: 'high-performance' });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.VSMShadowMap;
    renderer.shadowMap.autoUpdate = false;
    renderer.shadowMap.needsUpdate = true;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = .92;
    studio = createStudio(renderer, scene);
    camera.position.copy(frontView.position);
    controls=new OrbitControls(camera,canvas);controls.target.copy(frontView.target);controls.enableDamping=true;controls.dampingFactor=.07;
    controls.minDistance=2.5;controls.maxDistance=13;controls.minPolarAngle=.15;controls.maxPolarAngle=Math.PI/2-.035;controls.maxTargetRadius=4;controls.update();
    controls.addEventListener('start',()=>cameraMotion=null);
    const gltf=await new GLTFLoader().loadAsync('./model3.glb');
    if(disposed){releaseResources(gltf.scene);return;}
    car=gltf.scene;scene.add(car);
    const indicators=new Set(),headlights=new Set();
    car.traverse(object=>{if(object.isMesh){object.castShadow=!object.material.transparent;object.receiveShadow=true;}
      if(object.material?.name.startsWith('primary')) { object.material.roughness=.24; object.material.clearcoatRoughness=.11; }
      if(object.name.startsWith('Front_Indicator')||object.name.startsWith('Rear_Indicator')) indicators.add(object.material);
      if(object.name.startsWith('Headlight_DRL')) headlights.add(object.material);
    });
    indicatorMaterials=[...indicators];headlightMaterials=[...headlights];
    hinge=car.getObjectByName('Charge_Port_Hinge');mirrorLeft=car.getObjectByName('Mirror_Left_Hinge');mirrorRight=car.getObjectByName('Mirror_Right_Hinge');portLED=car.getObjectByName('Charge_Port_LED');
    socketAssembly=car.getObjectByName('Charge_Socket_Assembly');
    if(!hinge||!mirrorLeft||!mirrorRight||!portLED)throw new Error('模型缺少可交互部件');
    carBounds.setFromObject(car);
    // The flap rotates outward/up around its upper longitudinal hinge.
    ready=true;showState();loading.classList.add('hidden');resize();
    notify({type:'ready'});
    const clock=new THREE.Clock();
    function render(now) {
      if(disposed)return;
      animationFrame=requestAnimationFrame(render);
      const dt=Math.min(clock.getDelta(),1),smooth=reducedMotion?1:1-Math.exp(-8*dt);
      portAngle=THREE.MathUtils.lerp(portAngle,visualState.portOpen?-1.92:0,smooth);
      mirrorAngle=THREE.MathUtils.lerp(mirrorAngle,visualState.locked?1.08:0,smooth);
      hinge.rotation.x=portAngle;mirrorLeft.rotation.y=mirrorAngle;mirrorRight.rotation.y=-mirrorAngle;
      if(socketAssembly)socketAssembly.visible=visualState.portOpen;
      portLED.visible=visualState.portOpen;
      portLED.material.emissiveIntensity=visualState.portOpen?1.2+.35*Math.sin(now*.003):0;
      const flashing=now<flashUntil && Math.floor((flashUntil-now)/180)%2===0;
      for(const material of indicatorMaterials)material.emissiveIntensity=flashing?3:0;
      for(const material of headlightMaterials)material.emissiveIntensity=visualState.locked ? .3 : 1.15;
      if(cameraMotion) {
        const motion=cameraMotion,t=motion.duration===0?1:Math.min((now-motion.start)/motion.duration,1),e=t*t*(3-2*t);
        camera.position.lerpVectors(motion.fromPosition,motion.to.position,e);controls.target.lerpVectors(motion.fromTarget,motion.to.target,e);
        if(t===1){cameraMotion=null;if(activeView==='front')fitFrontView();}
      }
      controls.update();
      if(Math.abs(portAngle-shadowPortAngle)>.001 || Math.abs(mirrorAngle-shadowMirrorAngle)>.001) {
        renderer.shadowMap.needsUpdate=true;
        shadowPortAngle=portAngle;shadowMirrorAngle=mirrorAngle;
      }
      studio.update(portAngle, mirrorAngle, flashing, visualState.locked);
      renderer.render(scene,camera);
    }
    animationFrame=requestAnimationFrame(render);
  } catch(error) {
    dispose();
    loading.classList.remove('hidden');document.querySelector('.loader').style.display='none';document.querySelector('#loading-text').textContent='3D 模型加载失败：'+error.message;
    setConnection({ canControl: false, busy: false, status: '3D 模型加载失败', error: error.message });
    notify({type:'error',message:error.message});
  }
}
function resize() {
  if(!renderer)return;const width=canvas.clientWidth,height=canvas.clientHeight;
  if(width===0||height===0)return;renderer.setSize(width,height,false);camera.aspect=width/height;
  // Render the whole page, but compose the car in the space between the overlays.
  const space = document.querySelector('.scene-space').getBoundingClientRect();
  const margin = width < 600 ? 12 : 24;
  safeArea = {left:margin, right:width-margin, top:space.top+8, bottom:Math.max(space.top+88,space.bottom-28)};
  const availableHeight = Math.max(80, safeArea.bottom-safeArea.top);
  const horizontalFit=THREE.MathUtils.radToDeg(2*Math.atan(Math.tan(THREE.MathUtils.degToRad(34)/2)*1.45/camera.aspect));
  const verticalFit=THREE.MathUtils.radToDeg(2*Math.atan(Math.tan(THREE.MathUtils.degToRad(20)/2)*height/availableHeight));
  camera.fov=Math.max(verticalFit,horizontalFit);
  camera.setViewOffset(width,height,0,height/2-(safeArea.top+safeArea.bottom)/2,width,height);
  camera.updateProjectionMatrix();
  if(ready && activeView==='front' && !cameraMotion)fitFrontView();
}
function getScreenBounds() {
  if(!car || carBounds.isEmpty())return null;
  camera.updateMatrixWorld();
  const width=canvas.clientWidth,height=canvas.clientHeight;
  let left=Infinity,top=Infinity,right=-Infinity,bottom=-Infinity;
  for(const x of [carBounds.min.x,carBounds.max.x])for(const y of [carBounds.min.y,carBounds.max.y])for(const z of [carBounds.min.z,carBounds.max.z]) {
    const point=new THREE.Vector3(x,y,z).project(camera);
    const sx=(point.x+1)*width/2,sy=(1-point.y)*height/2;
    left=Math.min(left,sx);right=Math.max(right,sx);top=Math.min(top,sy);bottom=Math.max(bottom,sy);
  }
  return {left,top,right,bottom};
}
function fitFrontView() {
  if(!safeArea || !car)return;
  camera.lookAt(controls.target);
  for(let attempt=0;attempt<8;attempt++) {
    const bounds=getScreenBounds();
    const cx=(safeArea.left+safeArea.right)/2,cy=(safeArea.top+safeArea.bottom)/2;
    const scale=Math.max((cx-bounds.left)/(cx-safeArea.left),(bounds.right-cx)/(safeArea.right-cx),
      (cy-bounds.top)/(cy-safeArea.top),(bounds.bottom-cy)/(safeArea.bottom-cy));
    if(scale<=1.001)break;
    camera.fov=THREE.MathUtils.radToDeg(2*Math.atan(Math.tan(THREE.MathUtils.degToRad(camera.fov)/2)*scale*1.015));
    camera.updateProjectionMatrix();
  }
}
function releaseResources(root) {
  const geometries=new Set(),materials=new Set(),textures=new Set();
  root.traverse(o=>{if(o.geometry)geometries.add(o.geometry);if(o.material)for(const material of Array.isArray(o.material)?o.material:[o.material])materials.add(material);});
  for(const material of materials)for(const value of Object.values(material))if(value?.isTexture)textures.add(value);
  geometries.forEach(g=>g.dispose());textures.forEach(t=>t.dispose());materials.forEach(m=>m.dispose());
}
function dispose() {
  if(disposed)return;
  disposed=true;ready=false;cancelAnimationFrame(animationFrame);controls?.dispose();observer.disconnect();
  showState();
  studio?.dispose();
  releaseResources(scene);renderer?.dispose();
}
const observer=new ResizeObserver(resize);observer.observe(canvas);
observer.observe(document.querySelector('.scene-space'));
window.addEventListener('pagehide',dispose,{once:true});
canvas.addEventListener('webglcontextlost',event=>{
  event.preventDefault();
  ready=false;
  const message='3D 图形设备连接中断，请返回后重新打开页面。';
  setConnection({ canControl: false, busy: false, status: '3D 图形设备连接中断', error: message });
  notify({type:'error',message});
});
window.model3Demo={command,setConnection,applyVehicleState,getState:()=>({...state}),getDiagnostics:()=>{const textures=new Set();car?.traverse(o=>{if(o.material?.map)textures.add(o.material.map);});const portPoint=hinge?hinge.getWorldPosition(new THREE.Vector3()).project(camera):null;return {ready,portAngle,mirrorAngle,hingeName:hinge?.name,mirrorParts:mirrorLeft?.children.length,portParts:hinge?.children.length,socketVisible:socketAssembly?.visible,drawCalls:renderer?.info.render.calls,triangles:renderer?.info.render.triangles,textures:textures.size,source:car?.getObjectByName('Model3')?.userData.source,connection:{...connection},state:{...state},camera:{position:camera.position.toArray(),target:controls?.target.toArray(),fov:camera.fov,aspect:camera.aspect},carScreenBounds:getScreenBounds(),safeArea,portScreenPoint:portPoint?{x:(portPoint.x+1)*canvas.clientWidth/2,y:(1-portPoint.y)*canvas.clientHeight/2}:null,studio:studio?.getDiagnostics()};},dispose};
document.querySelectorAll('[data-action]').forEach(button=>button.addEventListener('click',()=>requestAction(button.dataset.action)));
if(previewMode) {
  setConnection({ mode: 'demo', canControl: true, status: '演示模型已准备就绪', lastUpdated: new Date().toISOString() });
  applyVehicleState({ locked: true, portOpen: false });
} else showState();
initialize();
