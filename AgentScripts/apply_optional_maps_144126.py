from pathlib import Path
import shutil
root=Path(__file__).resolve().parents[1];src=root/'Assets/HabitatIsland/Scripts';backup=root/'ChangeBackups/20261018_Feedback144126'
if 'feedback-20261018-optional' in (src/'HabitatMechanicLevels.cs').read_text(encoding='utf-8-sig'):
 raise SystemExit('Optional-map migration already applied; do not append duplicate source rows.')
a=[['.']*16 for _ in range(17)];b=[['.']*19 for _ in range(21)]
def row(g,r,cells):
    for x,v in cells.items():g[r][x]=str(v)
def fill(g,r,x1,x2,h=1):row(g,r,{x:h for x in range(x1,x2+1)})
fill(a,2,11,13);fill(a,3,7,12);row(a,3,{9:'1入口',13:2})
fill(a,4,7,13);row(a,4,{10:'1攀爬',13:'1箱子'})
row(a,5,{7:1,8:1,9:2,10:2,11:2,12:'漂浮'})
for r in range(6,10):fill(a,r,2,4,2)
row(a,6,{7:1,8:1,9:2,10:3,11:2});fill(a,7,7,11,2);row(a,7,{6:'漂浮',10:'2箱子'})
fill(a,8,7,11,2);row(a,8,{3:'2箱子',4:'2箱子',7:'2箱子',9:'2潮汐'});row(a,9,{3:'2箱子'})
row(a,10,{5:'水母'});fill(a,10,8,10)
fill(a,11,2,5,'礁石');row(a,11,{6:1});fill(a,11,8,10)
fill(a,12,3,6);row(a,12,{3:'1箱子',9:1,10:1})
row(a,13,{3:1,4:1,5:2,6:'金鱼',8:1,9:'1箱子',10:2});fill(a,14,4,9);row(a,14,{10:2})
row(b,2,{4:1,5:1,9:'1箱子',10:2});row(b,3,{4:1,5:1,6:1,9:2,10:1})
row(b,4,{4:1,5:1,6:1,8:'漂浮',9:1,10:1})
row(b,5,{4:1,5:'1小石子',6:1,8:'金鱼',9:1,10:1,13:1,14:'1寄居蟹'})
row(b,6,{4:'1入口',5:1,6:'1箱子',9:1,10:1,13:1,14:1})
fill(b,7,4,10);fill(b,7,13,16);row(b,7,{8:'1箱子',14:'1箱子'})
fill(b,8,4,8);fill(b,8,9,11,2);fill(b,8,13,16)
fill(b,9,9,11,2);fill(b,9,12,14);row(b,9,{10:'2箱子'})
fill(b,10,9,11,2);fill(b,10,12,13)
row(b,11,{4:1,5:1,6:'1箱子',12:1,13:1})
row(b,12,{4:1,5:1,9:'漂浮'});fill(b,12,12,15)
row(b,13,{4:1,5:1,6:'1红虾'});fill(b,13,7,11,2);fill(b,13,12,15);row(b,13,{9:'2箱子',11:'2潮汐'})
fill(b,14,4,6);row(b,14,{7:'1攀爬',12:'1攀爬'});fill(b,14,8,11,2);fill(b,15,4,6)
def update(name,fn):
 p=src/(name+'.cs');dest=backup/p.name
 if not dest.exists():shutil.copy2(p,dest)
 p.write_text(fn(p.read_text(encoding='utf-8-sig')),encoding='utf-8')
def maps(s):
 marker='\n        };\n        static readonly string[] Objectives'
 block=',\n'+',\n'.join('            new[] {\n'+',\n'.join('                "'+'|'.join(r)+'"' for r in g)+'\n            }' for g in [a,b])
 assert marker in s
 s=s.replace(marker,block+marker,1)
 s=s.replace('index==FifthIndex?5:index-FirstIndex+1','index==4?6:index==9?7:index==FifthIndex?5:index-FirstIndex+1')
 s=s.replace('public static bool HasHabitatGoal(HabitatState s) => s!=null && s.authoredLevel && Contains(s.levelIndex);','public static bool IsOptionalIndex(int index)=>index==4||index==9;\n        public static bool IsNewOptional(HabitatState s)=>s!=null&&IsOptionalIndex(s.levelIndex)&&s.sourceNodeId=="feedback-20261018-optional";\n        public static bool HasHabitatGoal(HabitatState s) => s!=null && s.authoredLevel && (Contains(s.levelIndex)||IsNewOptional(s));')
 s=s.replace('HasHabitatGoal(s)?Objectives[MapNumber(s.levelIndex)-1]:s.objective','HasHabitatGoal(s)&&Contains(s.levelIndex)?Objectives[MapNumber(s.levelIndex)-1]:s.objective')
 s=s.replace('if(!Contains(index))throw','if(!Contains(index)&&!IsOptionalIndex(index))throw')
 s=s.replace('sourceNodeId="latest-plan-20261018-map"+(map+1)', 'sourceNodeId=IsOptionalIndex(index)?"feedback-20261018-optional":"latest-plan-20261018-map"+(map+1)')
 s=s.replace('dockRotation=map==0?2:map==4?0:3','dockRotation=map==0?2:map==4||map==5?0:3')
 s=s.replace('objective=Objectives[map]', 'objective=map<5?Objectives[map]:"自由探索：运用竹排、潮汐和箱子，使本岛所有动物同时适居；工具可从商店准备。"')
 s=s.replace('tile.rotation=map==1||map==3&&x==4?0:1;', 'tile.rotation=map==1||map==3&&x==4||map==6&&row==12?0:1;')
 s=s.replace('if(cell=="入口")', 'if(cell.Contains("入口"))')
 s=s.replace('if(!Contains(index))return "";', 'if(!Contains(index)&&!IsOptionalIndex(index))return "";')
 s=s.replace('"关卡"+MapNumber(s.levelIndex)+"完成：','s.levelName+"完成：')
 return s
update('HabitatMechanicLevels',maps)
update('HabitatLevels',lambda s:s.replace('"荒岛 · 连续关卡"','"自由探索一 · 潮汐群岛"').replace('"群山之巅 · 正式关卡"','"自由探索二 · 回环海湾"').replace('if(index==HabitatFigJamLevel.Index)return HabitatFigJamLevel.Create();','if(HabitatMechanicLevels.IsOptionalIndex(index))return HabitatMechanicLevels.Create(index);').replace('if(index==HabitatMountainLevel.Index)return HabitatMountainLevel.Create();','').replace('if(s.levelIndex==HabitatFigJamLevel.Index)return','if(HabitatMechanicLevels.IsNewOptional(s))return HabitatMechanicLevels.CellLabel(s.levelIndex,x,z);\n            if(s.levelIndex==HabitatFigJamLevel.Index)return').replace('" · 海平面 "+s.waterLevel','" · 海平面 "+(s.waterLevel+1)'))
update('HabitatModel',lambda s:s.replace('s!=null&&s.authoredLevel&&HabitatMechanicLevels.Contains(s.levelIndex)','HabitatMechanicLevels.HasHabitatGoal(s)'))
print('Two optional maps transcribed from latest PDF; legacy layout source preserved.')
