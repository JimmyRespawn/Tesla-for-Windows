// Adapt Ameer Studio's CC BY 4.0 Tesla 2018 Model 3 for the offline viewer.
// Rebuild with Node.js. The source file and original license are beside this tool.
import * as THREE from '../Assets/Model3/vendor/three.module.js';
import path from 'node:path';
import fs from 'node:fs';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {readAsset,writeAsset,splitGeometry,splitAtPlane,splitComponents} from './model3-asset.mjs';

const folder=path.dirname(fileURLToPath(import.meta.url));
const input=path.join(folder,'Model3Source/tesla_2018_model_3.glb');
const source=readAsset(input);
const hash=crypto.createHash('sha256').update(fs.readFileSync(input)).digest('hex');
if(hash!=='d6d78c9bd1bd9c7ca87a07509a2e7b6585995fcbdec054f297167ba7c15cd878')throw new Error('Unexpected source asset: review part selections before using another model.');
const car=new THREE.Group();car.name='Model3';
const bounds=new THREE.Box3();for(const m of source.meshes)bounds.union(m.geometry.boundingBox);
const body=source.meshes.find(m=>m.name==='body_primary_0').geometry.boundingBox;
// Source FBX axes: Z = length (front negative), Y = height, X = width (left negative).
const scale=new THREE.Vector3(1.85/(body.max.x-body.min.x),1.443/(bounds.max.y-bounds.min.y),4.694/(bounds.max.z-bounds.min.z));
const normalize=new THREE.Matrix4().makeRotationY(Math.PI/2).multiply(new THREE.Matrix4().makeScale(...scale.toArray())).multiply(new THREE.Matrix4().makeTranslation(-(bounds.max.x+bounds.min.x)/2,-bounds.min.y,-(bounds.max.z+bounds.min.z)/2));
for(const m of source.meshes)m.geometry.applyMatrix4(normalize);

const materials=source.document.materials;
function material(name,color,metal=0,rough=.5,emissive){const m={name,pbrMetallicRoughness:{baseColorFactor:[...new THREE.Color(color).toArray(),1],metallicFactor:metal,roughnessFactor:rough},doubleSided:true};if(emissive)m.emissiveFactor=new THREE.Color(emissive).toArray();materials.push(m);return materials.length-1;}
const black=material('Charge_Port_Recess',0x141820,.05,.5);
const socketMat=material('Charge_Socket',0x1b2028,.1,.65);
const pins=material('Charge_Contacts',0xb8bdc7,.8,.25);
const blue=material('Charge_Port_LED',0x318cff,.1,.3,0x318cff);
const amber=material('Indicators',0xf1a12b,.05,.25,0xff7e11);
const headlight=material('Daytime_Running_Lights',0xe7effa,.15,.18,0xddeaff);
for(const m of materials){
  if(m.name.startsWith('primary')){m.pbrMetallicRoughness={baseColorFactor:[.88,.90,.93,1],metallicFactor:.18,roughnessFactor:.28};m.extensions={KHR_materials_clearcoat:{clearcoatFactor:1,clearcoatRoughnessFactor:.16}};}
  if(m.name==='movsteer_1.0.1'){m.pbrMetallicRoughness.metallicFactor=.88;m.pbrMetallicRoughness.roughnessFactor=.24;}
  if(m.name==='wheels.6'){m.pbrMetallicRoughness.metallicFactor=.8;m.pbrMetallicRoughness.roughnessFactor=.25;}
  if(m.name==='wheels.3')m.pbrMetallicRoughness.roughnessFactor=.87;
  if(m.name==='glass.0'||m.name==='glass.1'){m.pbrMetallicRoughness={baseColorFactor:m.name==='glass.1'?[.016,.025,.038,.72]:[.04,.055,.075,.35],metallicFactor:.12,roughnessFactor:.15};m.extensions={KHR_materials_clearcoat:{clearcoatFactor:1,clearcoatRoughnessFactor:.10}};m.doubleSided=false;}
  if(m.name==='tembus_red.0'){m.pbrMetallicRoughness.roughnessFactor=.18;m.pbrMetallicRoughness.baseColorFactor=[1,1,1,.35];m.doubleSided=false;}
}
function add(name,geometry,material,parent=car){if(!geometry.attributes.position.count)return;const mesh=new THREE.Mesh(geometry);mesh.name=name;mesh.userData.material=material;parent.add(mesh);return mesh;}
function hinge(name,position){const group=new THREE.Group();group.name=name;group.position.set(...position);car.add(group);return group;}
function onHinge(name,geometry,material,parent){geometry.translate(-parent.position.x,-parent.position.y,-parent.position.z);return add(name,geometry,material,parent);}
const left=hinge('Mirror_Left_Hinge',[-.835,.93,.851]);
const right=hinge('Mirror_Right_Hinge',[-.835,.93,-.851]);
const port=hinge('Charge_Port_Hinge',[1.749,.956,.782]);
let mirrorMeshes=0,lidMeshes=0;
for(const original of source.meshes){
  let {name,geometry,material:mat}=original;
  if(name.includes('indicator_')){mat=amber;name=(geometry.boundingBox?.max.x<0?'Front_Indicator_':'Rear_Indicator_')+name;}
  if(name.startsWith('chrome_foglight_')||name.startsWith('chrome_Lights_head_')){mat=headlight;name='Headlight_DRL_'+name;}
  if(name.startsWith('door_lf_')||name.startsWith('door_rf_')){
    const side=name.startsWith('door_lf_')?1:-1;
    const [rest,mirror]=splitComponents(geometry,b=>b.min.y>.85&&b.min.x>-1.05&&b.max.x<-.45&&(side===1?b.max.z>.95:b.min.z<-.95));
    if(mirror.attributes.position.count){
      if(name.includes('movsteer_1.0.1')){const [stem,cap]=splitAtPlane(mirror,0,-.835);add('Mirror_Stem_'+name,stem,mat);onHinge('Mirror_'+name,cap,mat,side===1?left:right);}
      else onHinge('Mirror_'+name,mirror,mat,side===1?left:right);
      mirrorMeshes++;
    }geometry=rest;
  }
  // Cut the side-facing section at a clean shut line, leaving rear lamps intact.
  if(/^(black_lights_|pantulans_|tembus_belakang_)/.test(name)){
    const [rest,lamp]=splitGeometry(geometry,points=>points.every(p=>p.z>.55&&p.x>1.69&&p.y>.75));
    const [lid,rear]=splitAtPlane(lamp,0,1.976);
    if(lid.attributes.position.count){onHinge('Charge_Port_Lid_'+name,lid,mat,port);lidMeshes++;}add(name+'_Rear',rear,mat);geometry=rest;
  }
  add(name,geometry,mat);
}
if(mirrorMeshes!==6||lidMeshes<3)throw new Error('Interactive part selection failed.');

// Add a demonstration socket beneath the original left tail-lamp cover.
const socket=new THREE.Group();socket.name='Charge_Socket_Assembly';socket.position.set(1.855,.866,.782);socket.quaternion.setFromUnitVectors(new THREE.Vector3(0,0,1),new THREE.Vector3(.18,.025,.98).normalize());car.add(socket);
function local(name,geometry,material,position=[0,0,0]){const mesh=add(name,geometry,material,socket);mesh.position.set(...position);return mesh;}
const recess=local('Charge_Port_Pocket',new THREE.SphereGeometry(1,40,24),black,[0,0,-.005]);recess.scale.set(.106,.072,.016);
const connector=local('Charge_Socket',new THREE.CylinderGeometry(.047,.05,.022,48),socketMat,[0,0,.008]);connector.rotation.x=Math.PI/2;
local('Charge_Port_LED',new THREE.TorusGeometry(.049,.0035,10,64),blue,[0,0,.023]);
const pinLocations=[[-.021,.014],[.021,.014],[0,.031],[-.020,-.020],[.020,-.020]];
for(let i=0;i<pinLocations.length;i++){const [x,y]=pinLocations[i],pin=local('Charge_Contact_'+i,new THREE.CylinderGeometry(.005,.005,.009,16),pins,[x,y,.026]);pin.rotation.x=Math.PI/2;}
socket.userData.visibleWithPort=true;
car.userData={units:'metres',frontAxis:'-X',modelYear:2018,source:source.document.asset.extras.source,license:'CC-BY-4.0',author:'Ameer Studio',sourceSha256:hash,chargePortHinge:'Charge_Port_Hinge',mirrorHinges:['Mirror_Left_Hinge','Mirror_Right_Hinge'],adaptations:'Normalized dimensions, pearl-white materials, isolated mirrors and side tail-lamp flap, added demo charge socket.'};
const output=path.resolve(folder,'../Assets/Model3/model3.glb');
console.log(JSON.stringify({output,mirrorMeshes,lidMeshes,...writeAsset(output,car,source)}));
