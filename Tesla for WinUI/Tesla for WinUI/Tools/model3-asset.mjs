// Local glTF import/export helpers. Retain source texture coordinates and attribution.
import fs from 'node:fs';
import path from 'node:path';
import * as THREE from '../Assets/Model3/vendor/three.module.js';

const arity={SCALAR:1,VEC2:2,VEC3:3,VEC4:4,MAT4:16};
const types={5120:Int8Array,5121:Uint8Array,5122:Int16Array,5123:Uint16Array,5125:Uint32Array,5126:Float32Array};
export function readAsset(file) {
  const bytes=fs.readFileSync(file),folder=path.dirname(file);let document,buffers;
  if(bytes.readUInt32LE(0)===0x46546c67){const n=bytes.readUInt32LE(12);document=JSON.parse(bytes.subarray(20,20+n).toString());buffers=[bytes.subarray(28+n,28+n+bytes.readUInt32LE(20+n))];}
  else {document=JSON.parse(bytes.toString());buffers=document.buffers.map(b=>fs.readFileSync(path.join(folder,b.uri)));}
  if(document.skins?.length||document.animations?.length)throw new Error('This adapter requires a static, unskinned source asset.');
  if(document.accessors.some(a=>a.sparse)||document.bufferViews.some(v=>v.extensions?.EXT_meshopt_compression))throw new Error('Sparse or compressed buffers must be decoded before import.');
  function attribute(index) {
    const a=document.accessors[index],v=document.bufferViews[a.bufferView],Type=types[a.componentType],count=arity[a.type],array=new Type(a.count*count),buffer=buffers[v.buffer];
    const stride=v.byteStride||Type.BYTES_PER_ELEMENT*count,offset=(v.byteOffset||0)+(a.byteOffset||0);
    for(let i=0;i<a.count;i++){const src=buffer.subarray(offset+i*stride,offset+i*stride+count*Type.BYTES_PER_ELEMENT);new Uint8Array(array.buffer,i*count*Type.BYTES_PER_ELEMENT,count*Type.BYTES_PER_ELEMENT).set(src);}
    return new THREE.BufferAttribute(array,count,a.normalized||false);
  }
  const meshes=[];
  function walk(index,parent) {
    const n=document.nodes[index],matrix=new THREE.Matrix4();
    if(n.skin!==undefined||n.weights)throw new Error('Skinning and morph weights are unsupported.');
    if(n.matrix)matrix.fromArray(n.matrix);else matrix.compose(new THREE.Vector3(...(n.translation||[0,0,0])),new THREE.Quaternion(...(n.rotation||[0,0,0,1])),new THREE.Vector3(...(n.scale||[1,1,1])));
    matrix.premultiply(parent);
    if(n.mesh!==undefined)for(const p of document.meshes[n.mesh].primitives){if(p.targets||(p.mode!==undefined&&p.mode!==4)||p.extensions?.KHR_draco_mesh_compression)throw new Error('Only uncompressed static triangle primitives are supported.');for(const key of ['POSITION','NORMAL','TANGENT'])if(p.attributes[key]!==undefined&&document.accessors[p.attributes[key]].componentType!==5126)throw new Error('Quantized geometry must be decoded before applying transforms.');const geometry=new THREE.BufferGeometry();for(const [name,a]of Object.entries(p.attributes))geometry.setAttribute({POSITION:'position',NORMAL:'normal',TEXCOORD_0:'uv',TEXCOORD_1:'uv1',TANGENT:'tangent',COLOR_0:'color'}[name]||name,attribute(a));if(p.indices!==undefined)geometry.setIndex(attribute(p.indices));geometry.applyMatrix4(matrix);
      // Mirrored authoring nodes must keep their original outward-facing winding.
      if(matrix.determinant()<0){if(geometry.index){const a=geometry.index.array;for(let i=0;i<a.length;i+=3)[a[i+1],a[i+2]]=[a[i+2],a[i+1]];}else for(const attribute of Object.values(geometry.attributes)){const a=attribute.array,s=attribute.itemSize;for(let i=0;i<attribute.count;i+=3)for(let c=0;c<s;c++)[a[(i+1)*s+c],a[(i+2)*s+c]]=[a[(i+2)*s+c],a[(i+1)*s+c]];}const tangent=geometry.attributes.tangent;if(tangent)for(let i=0;i<tangent.count;i++)tangent.setW(i,-tangent.getW(i));}
      geometry.computeBoundingBox();meshes.push({name:n.name||document.meshes[n.mesh].name,geometry,material:p.material,node:index});}
    for(const child of n.children||[])walk(child,matrix);
  }
  for(const n of document.scenes[document.scene||0].nodes)walk(n,new THREE.Matrix4());
  const images=(document.images||[]).map(image=>{if(image.uri)return {bytes:fs.readFileSync(path.join(folder,image.uri)),mimeType:image.mimeType||(image.uri.endsWith('.png')?'image/png':'image/jpeg')};const v=document.bufferViews[image.bufferView];return {bytes:buffers[v.buffer].subarray(v.byteOffset||0,(v.byteOffset||0)+v.byteLength),mimeType:image.mimeType};});
  return {document,meshes,images};
}

export function writeAsset(file,root,source) {
  const gltf={asset:{...source.document.asset,generator:'Tesla for WinUI Model 3 asset adaptation'},scene:0,scenes:[{nodes:[0]}],nodes:[],meshes:[],materials:source.document.materials,accessors:[],bufferViews:[],buffers:[{byteLength:0}],images:[],textures:source.document.textures||[],samplers:source.document.samplers||[],extensionsUsed:[...new Set([...(source.document.extensionsUsed||[]),'KHR_materials_clearcoat'])]};
  const blocks=[];let size=0;
  function bufferView(raw,target){const pad=(4-size%4)%4;if(pad){blocks.push(Buffer.alloc(pad));size+=pad;}const index=gltf.bufferViews.length,v={buffer:0,byteOffset:size,byteLength:raw.length};if(target)v.target=target;gltf.bufferViews.push(v);blocks.push(raw);size+=raw.length;return index;}
  for(const image of source.images)gltf.images.push({bufferView:bufferView(image.bytes),mimeType:image.mimeType});
  function accessor(attribute,target){const a=attribute.array,Type=a.constructor,raw=Buffer.from(a.buffer,a.byteOffset,a.byteLength),out={bufferView:bufferView(raw,target),componentType:Number(Object.keys(types).find(k=>types[k]===Type)),count:attribute.count,type:Object.keys(arity).find(k=>arity[k]===attribute.itemSize)};if(attribute.normalized)out.normalized=true;if(target===34962&&attribute.itemSize===3){out.min=[Infinity,Infinity,Infinity];out.max=[-Infinity,-Infinity,-Infinity];for(let i=0;i<a.length;i++){const k=i%3;out.min[k]=Math.min(out.min[k],a[i]);out.max[k]=Math.max(out.max[k],a[i]);}}gltf.accessors.push(out);return gltf.accessors.length-1;}
  function node(object){const index=gltf.nodes.length,n={name:object.name,translation:object.position.toArray(),rotation:object.quaternion.toArray(),scale:object.scale.toArray()};gltf.nodes.push(n);if(object.geometry){const attributes={};for(const [name,a]of Object.entries(object.geometry.attributes))attributes[{position:'POSITION',normal:'NORMAL',uv:'TEXCOORD_0',uv1:'TEXCOORD_1',tangent:'TANGENT',color:'COLOR_0'}[name]||name]=accessor(a,34962);const primitive={attributes,material:object.userData.material};if(object.geometry.index)primitive.indices=accessor(object.geometry.index,34963);n.mesh=gltf.meshes.length;gltf.meshes.push({name:object.name,primitives:[primitive]});}if(Object.keys(object.userData).length)n.extras=object.userData;if(object.children.length)n.children=object.children.map(node);return index;}
  node(root);gltf.buffers[0].byteLength=size;
  let json=Buffer.from(JSON.stringify(gltf));json=Buffer.concat([json,Buffer.alloc((4-json.length%4)%4,0x20)]);let bin=Buffer.concat(blocks);bin=Buffer.concat([bin,Buffer.alloc((4-bin.length%4)%4)]);const header=Buffer.alloc(12);header.writeUInt32LE(0x46546c67,0);header.writeUInt32LE(2,4);header.writeUInt32LE(28+json.length+bin.length,8);function chunk(raw,type){const h=Buffer.alloc(8);h.writeUInt32LE(raw.length,0);h.writeUInt32LE(type,4);return Buffer.concat([h,raw]);}fs.writeFileSync(file,Buffer.concat([header,chunk(json,0x4e4f534a),chunk(bin,0x004e4942)]));let triangles=0;root.traverse(o=>{if(o.geometry)triangles+=(o.geometry.index?.count||o.geometry.attributes.position.count)/3;});return {nodes:gltf.nodes.length,meshes:gltf.meshes.length,bytes:fs.statSync(file).size,triangles};
}

// Remove a subset of triangles without changing the retained surfaces or normals.
export function splitGeometry(geometry,choose) {
  const indices=geometry.index?.array||Uint32Array.from({length:geometry.attributes.position.count},(_,i)=>i),parts=[[],[]];
  for(let i=0;i<indices.length;i+=3){const triangle=[indices[i],indices[i+1],indices[i+2]],points=triangle.map(v=>new THREE.Vector3().fromBufferAttribute(geometry.attributes.position,v));parts[choose(points,triangle)?1:0].push(...triangle);}
  return parts.map(indices=>{const result=new THREE.BufferGeometry(),lookup=new Map(),vertices=[];const compact=indices.map(i=>{if(!lookup.has(i)){lookup.set(i,vertices.length);vertices.push(i);}return lookup.get(i);});for(const [name,a]of Object.entries(geometry.attributes)){const array=new a.array.constructor(vertices.length*a.itemSize);vertices.forEach((source,index)=>{for(let c=0;c<a.itemSize;c++)array[index*a.itemSize+c]=a.array[source*a.itemSize+c];});result.setAttribute(name,new THREE.BufferAttribute(array,a.itemSize,a.normalized));}result.setIndex(compact);result.computeBoundingBox();return result;});
}

export function splitComponents(geometry,choose) {
  const p=geometry.attributes.position,indices=geometry.index?.array||Uint32Array.from({length:p.count},(_,i)=>i),parents=Uint32Array.from({length:p.count},(_,i)=>i),keys=new Map();
  const find=i=>{while(parents[i]!==i){parents[i]=parents[parents[i]];i=parents[i];}return i;},unite=(a,b)=>{a=find(a);b=find(b);if(a!==b)parents[b]=a;};
  for(let i=0;i<p.count;i++){const key=[p.getX(i),p.getY(i),p.getZ(i)].map(v=>Math.round(v*100000)).join(',');if(keys.has(key))unite(i,keys.get(key));else keys.set(key,i);}
  for(let i=0;i<indices.length;i+=3){unite(indices[i],indices[i+1]);unite(indices[i],indices[i+2]);}
  const components=new Map();for(let i=0;i<p.count;i++){const root=find(i);if(!components.has(root))components.set(root,new THREE.Box3());components.get(root).expandByPoint(new THREE.Vector3().fromBufferAttribute(p,i));}
  const selected=new Set([...components].filter(([root,bounds])=>choose(bounds)).map(([root])=>root));
  return splitGeometry(geometry,(points,triangle)=>selected.has(find(triangle[0])));
}

// Clip triangles at the flap's shut line instead of selecting triangle centroids.
export function splitAtPlane(geometry,axis,value) {
  const attributes=Object.entries(geometry.attributes),index=geometry.index?.array||Uint32Array.from({length:geometry.attributes.position.count},(_,i)=>i),outputs=[[],[]];
  function vertex(i){const v={};for(const [name,a]of attributes)v[name]=Array.from({length:a.itemSize},(_,c)=>a.getComponent(i,c));return v;}
  function intersection(a,b){const t=(value-a.position[axis])/(b.position[axis]-a.position[axis]),v={};for(const [name,attribute]of attributes)v[name]=a[name].map((n,i)=>n+(b[name][i]-n)*t);return v;}
  for(let i=0;i<index.length;i+=3){const triangle=[vertex(index[i]),vertex(index[i+1]),vertex(index[i+2])];for(let side=0;side<2;side++){const inside=p=>side===0?p.position[axis]<=value:p.position[axis]>=value,polygon=[];for(let k=0;k<3;k++){const a=triangle[k],b=triangle[(k+1)%3],aIn=inside(a),bIn=inside(b);if(aIn)polygon.push(a);if(aIn!==bIn)polygon.push(intersection(a,b));}for(let k=1;k<polygon.length-1;k++)outputs[side].push(polygon[0],polygon[k],polygon[k+1]);}}
  return outputs.map(vertices=>{const result=new THREE.BufferGeometry();for(const [name,a]of attributes)result.setAttribute(name,new THREE.Float32BufferAttribute(vertices.flatMap(v=>v[name]),a.itemSize));if(result.attributes.normal)result.normalizeNormals();result.computeBoundingBox();return result;});
}
