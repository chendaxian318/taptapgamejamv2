"""Read only explicitly selected delivered assets. No directory walking or imports."""
import hashlib, io, json, struct
from pathlib import Path
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[1]
delivery=Path('C:/Users/admin/Desktop/聚光灯草案')
selected={
 'fishing_rod':delivery/'part3/hona/鱼竿/3d/模型/fishing_rod_unirig_animated.glb',
 'shovel_player':delivery/'part3/hona/铲子/3d/02_成品模型/Shovel_PlayerReady.glb',
 'shovel_static':delivery/'part3/hona/铲子/3d/02_成品模型/Shovel_Sculpted.glb',
 'shell_bellows':delivery/'part3/hona/吹风机/3d/02_成品模型/Shell_Bellows_Rigged.glb',
}
comparisons={
 'shrimp_animated':('part3/vinda/龙虾/3d/03_动画模型_大幅版/creature_animated.glb','Assets/HabitatIsland/Art/AnimatedShrimp/creature_animated.glb'),
 'shrimp_original':('part3/vinda/龙虾/3d/01_原始模型/MeshyShrimp.glb','Assets/HabitatIsland/Art/MeshyShrimp/MeshyShrimp.glb'),
 'trap_animated':('part3/hona/笼子/3d/lobster_trap_rigged_v2.glb','Assets/HabitatIsland/Art/AnimatedTrap/lobster_trap_rigged_v2.glb'),
 'dock_original':('part2/hona/3d/Meshy_AI_Wooden_Dock_Scene_Uni_1014170130_texture (1).glb','Assets/HabitatIsland/Art/WoodenDock/WoodenDock.glb'),
 'dock_current':('part2/hona/3d/Meshy_AI_Wooden_Dock_Scene_Uni_1014170130_texture (1).glb','Assets/HabitatIsland/Art/WoodenDock/WoodenDock_NoStairs.glb'),
 'player_original':('part1/hona/3d/01_人物模型/MeshyTraveler.glb','Assets/HabitatIsland/Art/MeshyTraveler/MeshyTraveler.glb'),
 'player_current':('part1/hona/3d/01_人物模型/MeshyTraveler.glb','Assets/HabitatIsland/Art/MeshyTraveler/MeshyTraveler_EmptyHands.glb'),
}
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def inspect(path):
 raw=path.read_bytes(); n=struct.unpack_from('<I',raw,12)[0]; doc=json.loads(raw[20:20+n]); data=raw[28+n:]
 def acc(i):
  a=doc['accessors'][i]; b=doc['bufferViews'][a['bufferView']]
  typ={5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]
  dims={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
  return np.ndarray((a['count'],dims),dtype=typ,buffer=data,offset=b.get('byteOffset',0)+a.get('byteOffset',0),strides=(b.get('byteStride',np.dtype(typ).itemsize*dims),np.dtype(typ).itemsize)).copy()
 nodes=doc.get('nodes',[]); parent={child:i for i,node in enumerate(nodes) for child in node.get('children',[])}
 def local(node):
  if 'matrix' in node:return np.array(node['matrix']).reshape(4,4).T
  x,y,z,w=node.get('rotation',[0,0,0,1]); m=np.eye(4)
  m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(node.get('scale',[1,1,1]));m[:3,3]=node.get('translation',[0,0,0]);return m
 def world(i): return (world(parent[i]) if i in parent else np.eye(4))@local(nodes[i])
 meshes=[];scene_points=[]
 for mi,mesh in enumerate(doc.get('meshes',[])):
  prims=[]
  for primitive in mesh['primitives']:
   points=acc(primitive['attributes']['POSITION'])
   influence={}
   if 'WEIGHTS_0' in primitive['attributes']:
    w=acc(primitive['attributes']['WEIGHTS_0']); influence={'maxInfluences':int((w>1e-6).sum(1).max()),'weightSumMin':float(w.sum(1).min()),'weightSumMax':float(w.sum(1).max())}
   prims.append({'vertexCount':len(points),'triangles':int(len(acc(primitive['indices']))//3) if 'indices' in primitive else len(points)//3,'boundsMin':points.min(0).tolist(),'boundsMax':points.max(0).tolist(),'size':(points.max(0)-points.min(0)).tolist(),'finite':bool(np.isfinite(points).all()),'material':primitive.get('material'),'attributes':list(primitive['attributes']),**influence})
   for ni,node in enumerate(nodes):
    if node.get('mesh')==mi:scene_points.append((np.c_[points,np.ones(len(points))]@world(ni).T)[:,:3])
  meshes.append({'name':mesh.get('name'),'primitives':prims})
 animations=[]
 for animation in doc.get('animations',[]):
  times=[acc(s['input']).flatten() for s in animation['samplers']]
  start=min(float(t.min()) for t in times);end=max(float(t.max()) for t in times)
  animations.append({'name':animation.get('name'),'start':start,'end':end,'duration':end-start,'channels':len(animation['channels']),'keyCounts':sorted(set(len(t) for t in times)),'targets':sorted(set(nodes[c['target']['node']].get('name','') for c in animation['channels'])),'paths':sorted(set(c['target']['path'] for c in animation['channels'])),'interpolations':sorted(set(s.get('interpolation','LINEAR') for s in animation['samplers']))})
 images=[]
 for img in doc.get('images',[]):
  b=doc['bufferViews'][img['bufferView']];rawimg=data[b.get('byteOffset',0):b.get('byteOffset',0)+b['byteLength']];im=Image.open(io.BytesIO(rawimg))
  images.append({'name':img.get('name'),'mimeType':img.get('mimeType'),'dimensions':list(im.size),'sha256':hashlib.sha256(rawimg).hexdigest(),'bytes':len(rawimg)})
 allpoints=np.concatenate(scene_points)
 return {'path':str(path),'sha256':sha(path),'bytes':len(raw),'generator':doc.get('asset',{}),'sceneRoots':doc.get('scenes'),'nodes':[{'index':i,'name':node.get('name'),'parent':parent.get(i),'mesh':node.get('mesh'),'skin':node.get('skin'),'translation':node.get('translation',[0,0,0]),'rotation':node.get('rotation',[0,0,0,1]),'scale':node.get('scale',[1,1,1]),'worldOrigin':world(i)[:3,3].tolist()} for i,node in enumerate(nodes)],'meshes':meshes,'worldRestBounds':{'min':allpoints.min(0).tolist(),'max':allpoints.max(0).tolist(),'size':(allpoints.max(0)-allpoints.min(0)).tolist()},'skins':[{'name':s.get('name'),'joints':[nodes[j].get('name') for j in s['joints']]} for s in doc.get('skins',[])],'animations':animations,'materials':doc.get('materials',[]),'images':images,'extensionsUsed':doc.get('extensionsUsed',[])}

report={'comparisons':{},'assets':{}}
for label,(source,target) in comparisons.items():
 a=delivery/source;b=root/target;ah=sha(a);bh=sha(b)
 report['comparisons'][label]={'source':str(a),'current':str(b),'sourceSha256':ah,'currentSha256':bh,'identical':ah==bh}
for label,path in selected.items(): report['assets'][label]=inspect(path)
out=root/'Logs/asset-inventory-20261016/non-backpack-assets.json';out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'comparisons':report['comparisons'],'assets':{label:{k:item[k] for k in ['sha256','bytes','worldRestBounds','skins','animations','images']} for label,item in report['assets'].items()}},ensure_ascii=False,indent=2))
