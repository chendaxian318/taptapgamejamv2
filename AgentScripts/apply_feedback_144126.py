from pathlib import Path
import shutil

root=Path(__file__).resolve().parents[1]
src=root/'Assets/HabitatIsland/Scripts'
if 'Shell, Raft }' in (src/'HabitatModel.cs').read_text(encoding='utf-8-sig'):
    raise SystemExit('This one-time migration is already applied; do not rerun it.')
backup=root/'ChangeBackups/20261018_Feedback144126'
backup.mkdir(parents=True,exist_ok=True)
def edit(name, fn):
    p=src/(name+'.cs')
    dest=backup/p.name
    if not dest.exists(): shutil.copy2(p,dest)
    text=p.read_text(encoding='utf-8-sig')
    p.write_text(fn(text),encoding='utf-8')
def model(s):
    s=s.replace('ClimbBlock, Shell }','ClimbBlock, Shell, Raft }')
    s=s.replace('if(q.prop==PropKind.Float)return s.waterLevel+1;', 'if(q.prop==PropKind.Float||q.prop==PropKind.Raft)return s.waterLevel+1;')
    s=s.replace('q.prop!=PropKind.Float&&Height', 'q.prop!=PropKind.Float&&q.prop!=PropKind.Raft&&Height')
    s=s.replace('public static bool IsBox(PropKind kind)=>kind==PropKind.Crate||kind==PropKind.Float||kind==PropKind.ClimbBlock;', 'public static bool IsBox(PropKind kind)=>kind==PropKind.Crate||kind==PropKind.Float;')
    s=s.replace('bool climbable=q.prop==PropKind.Crate||q.prop==PropKind.ClimbBlock;', 'if(IsWater(s,x,z)&&TileAt(s,s.playerX,s.playerZ)?.prop==PropKind.Raft)return RideRaft(s,dx,dz,tuning);\n            bool climbable=q.prop==PropKind.Crate;')
    s=s.replace('box.prop!=PropKind.Crate&&box.prop!=PropKind.ClimbBlock&&box.prop!=PropKind.Float', 'box.prop!=PropKind.Crate&&box.prop!=PropKind.Float')
    s=s.replace('(box.prop==PropKind.Crate||box.prop==PropKind.Float||box.prop==PropKind.ClimbBlock)', '(box.prop==PropKind.Crate||box.prop==PropKind.Float)')
    s=s.replace('if(IsBox(q.prop))return PushBox(s,x,z);', 'if(q.prop==PropKind.ClimbBlock){Log(s,"这是固定台阶：按 Shift + 方向攀爬，不可搬运、推拉或打捞。");return false;}\n            if(q.prop==PropKind.Raft){Log(s,"走上竹排后，沿水纹方向按方向键往返搭乘；风箱可改变航向。");return false;}\n            if(IsBox(q.prop))return PushBox(s,x,z);')
    s=s.replace('||q.prop==PropKind.Climb||q.prop==PropKind.ClimbBlock)', '||q.prop==PropKind.Climb)')
    s=s.replace('if(IsBox(s.bagProps[index]))', 'if(IsBox(s.bagProps[index])||s.bagProps[index]==PropKind.ClimbBlock||s.bagProps[index]==PropKind.Raft)')
    s=s.replace('Tile f=TileAt(s,fx,fz);if(f.prop==PropKind.Float)', '''Tile f=TileAt(s,fx,fz);
                    // Authored bamboo rafts wait for their passenger. Wind redirects the rail,
                    // rather than sending the empty raft away and stranding the player.
                    if(f.prop==PropKind.Raft){f.rotation=d;break;}
                    if(f.prop==PropKind.Float)''')
    at='        static int StarDistance(HabitatState s,int x,int z)'
    methods='''        // Logical waterLevel is the highest flooded ground layer (0 at low tide).
        // The visible water surface is the next layer, level with height-one land.
        public static float WaterSurfaceY(int waterLevel)=>(waterLevel+1)*.7f+.025f;
        public static bool RaftDirection(Tile raft,int dx,int dz)
            =>raft!=null&&raft.prop==PropKind.Raft&&Math.Abs(dx)+Math.Abs(dz)==1
                &&((raft.rotation%2==1&&dx!=0)||(raft.rotation%2==0&&dz!=0));
        static bool RideRaft(HabitatState s,int dx,int dz,Tuning tuning)
        {
            int ox=s.playerX,oz=s.playerZ,nx=ox+dx,nz=oz+dz;
            Tile raft=TileAt(s,ox,oz),dest=TileAt(s,nx,nz);
            if(!RaftDirection(raft,dx,dz)){Log(s,"竹排只能沿水纹方向往返；用风箱改变航向。");return false;}
            if(raft.trap||!CanReceive(dest)||!IsWater(s,nx,nz)||dest.trap||Occupied(s,nx,nz)
                ||Occupied(s,ox,oz)){Log(s,"航道被挡住了：可反向搭乘，或从岸边下竹排。");return false;}
            int direction=raft.rotation;RemoveProp(raft);PutProp(dest,PropKind.Raft);dest.rotation=direction;
            s.playerX=nx;s.playerZ=nz;SyncCarried(s);
            if(s.carryingAnimal>=0)
            {
                var shrimp=s.animals.Find(a=>a.id==s.carryingAnimal&&a.species==Species.Shrimp);
                if(shrimp!=null)ScareAfterShrimpStep(s,shrimp,ox,oz,tuning??new Tuning(),true);
            }
            Log(s,"搭乘竹排：沿水纹方向可继续前进或原路返回。");return true;
        }

'''
    assert at in s
    return s.replace(at,methods+at)
edit('HabitatModel',model)
edit('HabitatMechanicLevels',lambda s:s.replace('if(cell.Contains("漂浮"))tile.prop=PropKind.Float;', 'if(cell.Contains("漂浮")){tile.prop=PropKind.Raft;tile.rotation=map==1||map==3&&x==4?0:1;}'))
for name in ['HabitatFigJamLevel','HabitatMountainLevel']:
    edit(name,lambda s:s.replace('if(cell.Contains("漂流方块"))tile.prop=PropKind.Float;', 'if(cell.Contains("漂流方块")){tile.prop=PropKind.Raft;tile.rotation=1;}'))
def game(s):
    s=s.replace('(target.prop == PropKind.Crate || target.prop == PropKind.ClimbBlock)', 'target.prop == PropKind.Crate')
    s=s.replace('SelectFacingCell(candidate);\n            pendingBefore=', '''SelectFacingCell(candidate);
            if(HabitatRules.TileAt(State,State.playerX,State.playerZ)?.prop==PropKind.Raft
                &&HabitatRules.TileAt(candidate,candidate.playerX,candidate.playerZ)?.prop==PropKind.Raft
                &&HabitatRules.TileAt(candidate,State.playerX,State.playerZ)?.prop!=PropKind.Raft)
                motion=HabitatPlayerMotion.Ride;
            pendingBefore=''',1)
    s=s.replace('if(TryNearbyCatch(out int fishX,out int fishZ))\n', '''GetTarget(out int raftX,out int raftZ);
            if(tool==0&&!State.onHarbor&&State.carryingProp==PropKind.None&&State.carryingAnimal<0
                &&HabitatRules.RaftDirection(HabitatRules.TileAt(State,State.playerX,State.playerZ),raftX-State.playerX,raftZ-State.playerZ)
                &&HabitatRules.IsWater(State,raftX,raftZ))
            {Move(raftX-State.playerX,raftZ-State.playerZ);return;}
            if(TryNearbyCatch(out int fishX,out int fishZ))
''',1)
    s=s.replace('State.waterLevel * .7f + .07f','HabitatRules.WaterSurfaceY(State.waterLevel) + .045f')
    return s
edit('HabitatGame',game)
def view(s):
    s=s.replace('waterY = s.waterLevel * Step + .025f','waterY = HabitatRules.WaterSurfaceY(s.waterLevel)')
    # Floating crates retain their full submerged body. The deck stays at Height().
    s=s.replace('BuildProp(v.root, t.coveredProp, visibleSurface, t.rotation)', 'BuildProp(v.root, t.coveredProp, t.coveredProp==PropKind.Float?s.waterLevel*Step+.04f:visibleSurface, t.rotation)')
    s=s.replace('BuildProp(v.root, t.prop, visibleSurface+(t.coveredProp==PropKind.Float?Step:0), t.rotation)', 'BuildProp(v.root, t.prop, PropSurfacePosition(s,z*s.width+x).y, t.rotation)')
    s=s.replace('kind == PropKind.Float || kind == PropKind.ClimbBlock', 'kind == PropKind.Float || kind == PropKind.Raft')
    s=s.replace('float y = tile.height <= s.waterLevel ? s.waterLevel * Step + .04f : tile.height * Step + .045f;\n            if(tile.coveredProp==PropKind.Float)y+=Step;', '''float y = tile.height <= s.waterLevel ? HabitatRules.WaterSurfaceY(s.waterLevel)+.015f : tile.height * Step + .045f;
            if(tile.prop==PropKind.Float)y=s.waterLevel*Step+.04f;
            if(tile.coveredProp==PropKind.Float)y=(s.waterLevel+1)*Step+.045f;''')
    s=s.replace('case PropKind.Float:\n', '''case PropKind.Raft:
                    // Thin, lashed bamboo deck: clearly distinct from a buoyant crate.
                    for(int i=0;i<6;i++)
                    {
                        var pole=Cylinder("Bamboo pole",p,new Vector3(-.35f+i*.14f,-.045f,0),new Vector3(.13f,.46f,.13f),materials[i%2==0?"woodLight":"sandLight"]);
                        pole.transform.localRotation=Quaternion.Euler(90,0,0);
                        for(int j=-1;j<=1;j++)Box("Bamboo joint",p,new Vector3(-.35f+i*.14f,.013f,j*.29f),new Vector3(.14f,.012f,.018f),materials["wood"]);
                    }
                    for(int i=-1;i<=1;i+=2)Box("Raft lashings",p,new Vector3(0,.018f,i*.25f),new Vector3(.88f,.022f,.035f),materials["wood"]);
                    break;
                case PropKind.Float:
''',1)
    # Keep the cargo box recognisable; bamboo now belongs only to the raft.
    s=s.replace('"Bamboo raft deck"','"Floating crate planks"')
    s=s.replace('.22f + s.waterLevel * Step', '.22f + (s.waterLevel+1) * Step')
    s=s.replace('s.waterLevel * Step + .05f','HabitatRules.WaterSurfaceY(s.waterLevel) + .025f')
    s=s.replace('s.waterLevel * Step + .052f','HabitatRules.WaterSurfaceY(s.waterLevel) + .027f')
    s=s.replace('state.waterLevel * Step + .025f','HabitatRules.WaterSurfaceY(state.waterLevel)')
    # Authored directions are represented by ripples along the valid water corridor.
    s=s.replace('int dockHeight = HabitatRules.Height(s, s.dockX, s.dockZ);','UpdateRaftRipples(s);\n            int dockHeight = HabitatRules.Height(s, s.dockX, s.dockZ);')
    at='        void EnsureTerrain(HabitatState s)'
    method='''        Transform raftRipples;
        int raftRippleSignature;
        void UpdateRaftRipples(HabitatState s)
        {
            int signature=s.width*397+s.depth;
            foreach(var t in s.tiles)signature=unchecked(signature*31+(int)t.prop*7+t.rotation+t.height*13);
            signature=unchecked(signature*31+s.waterLevel);
            if(raftRipples&&signature==raftRippleSignature)return;
            raftRippleSignature=signature;
            if(!raftRipples)raftRipples=NewRoot("Raft current direction",world);
            ClearChildren(raftRipples);
            for(int z=0;z<s.depth;z++)for(int x=0;x<s.width;x++)
            {
                Tile raft=HabitatRules.TileAt(s,x,z);if(raft.prop!=PropKind.Raft)continue;
                int dx=raft.rotation%2==1?1:0,dz=dx==0?1:0;
                for(int sign=-1;sign<=1;sign+=2)for(int n=1;n<Math.Max(s.width,s.depth);n++)
                {
                    int rx=x+dx*n*sign,rz=z+dz*n*sign;var tile=HabitatRules.TileAt(s,rx,rz);
                    if(tile==null||!HabitatRules.IsWater(s,rx,rz)||tile.prop!=PropKind.None&&tile.prop!=PropKind.Shell)break;
                    Transform mark=NewRoot("Current ripple",raftRipples);mark.localPosition=new Vector3(rx,waterY+.018f,rz);
                    mark.localRotation=Quaternion.Euler(0,dx!=0?90:0,0);
                    for(int side=-1;side<=1;side+=2)
                    {
                        var stroke=Box("Water chevron",mark,new Vector3(side*.09f,0,0),new Vector3(.025f,.008f,.23f),materials["foam"]);
                        stroke.transform.localRotation=Quaternion.Euler(0,side*48,0);
                    }
                }
            }
        }

'''
    return s.replace(at,method+at)
edit('HabitatView',view)
edit('HabitatHarborBoat',lambda s:s.replace('berth.y = waterLevel * .7f + .21f;', 'berth.y = HabitatRules.WaterSurfaceY((int)waterLevel) + .185f;'))
edit('HabitatMapHarbor',lambda s:s.replace('s.waterLevel * GridStep + .05f','HabitatRules.WaterSurfaceY(s.waterLevel) + .025f'))
edit('HabitatOcclusionFade',lambda s:s.replace('state.waterLevel * Step + .04f','HabitatRules.WaterSurfaceY(state.waterLevel) + .015f').replace('state.waterLevel * Step + .052f','HabitatRules.WaterSurfaceY(state.waterLevel) + .027f'))
edit('HabitatHud',lambda s:s.replace('case PropKind.Float:return "漂流块";', 'case PropKind.Float:return "水中浮箱";case PropKind.Raft:return "往返竹排";'))
edit('HabitatHud.Plan',lambda s:s.replace('if(game.TryNearbyCatch(out int fishX,out int fishZ))return', '''if(!s.onHarbor&&game.tool==0&&HabitatRules.RaftDirection(HabitatRules.TileAt(s,s.playerX,s.playerZ),x-s.playerX,z-s.playerZ)&&HabitatRules.IsWater(s,x,z))return "方向键 / E · 搭乘竹排（可往返）";
            if(HabitatRules.TileAt(s,x,z)?.prop==PropKind.ClimbBlock)return "固定台阶 · Shift + 方向攀爬";
            if(HabitatRules.TileAt(s,x,z)?.prop==PropKind.Raft)return "走上竹排 · 沿水纹方向往返";
            if(game.TryNearbyCatch(out int fishX,out int fishZ))return''').replace('Mathf.Max(s.waterLevel,HabitatRules.Height(s,x,z))','Mathf.Max(s.waterLevel+1,HabitatRules.Height(s,x,z))'))
edit('HabitatHud.Workflow',lambda s:s.replace('s.waterLevel*.7f','HabitatRules.WaterSurfaceY(s.waterLevel)').replace('candidate.waterLevel*.7f','HabitatRules.WaterSurfaceY(candidate.waterLevel)'))
edit('HabitatMainStory.Chapter',lambda s:s.replace('game.State.waterLevel * .7f + .06f','HabitatRules.WaterSurfaceY(game.State.waterLevel) + .035f').replace('Mathf.Max(game.State.waterLevel,','Mathf.Max(game.State.waterLevel+1,'))
edit('HabitatChapterRules',lambda s:s.replace('TargetFish=2,RewardCoins=200','TargetFish=2,RewardCoins=100').replace('获得 200 贝币','获得 100 贝币'))
def chapter(s):
    for a,b in [('两条合计 200 贝币','两条合计 100 贝币'),('领取 200 贝币','领取 100 贝币'),('委托已交付，200 贝币','委托已交付，100 贝币'),('每条 100 贝币，两条合计 100 贝币','这次委托合计 100 贝币'),('报酬：200 贝币','报酬：100 贝币'),('两条琉璃鱼按每条 100 贝币收购，合计 200 贝币已经付给你了','两条琉璃鱼的委托报酬 100 贝币已经付给你了')]:s=s.replace(a,b)
    s=s.replace('商店已上架吹风机（200 贝币），准备后乘船前往第二关。','商店已上架吹风机（200 贝币）。若贝币不足，可继续捕获幼鱼并出售，每条 100 贝币，再出发。')
    s=s.replace('能推动漂浮方块','能改变竹排的往返航向，也能推动水中浮箱')
    s=s.replace('Mathf.Max(State.waterLevel,','Mathf.Max(State.waterLevel+1,').replace('State.waterLevel*.7f+.12f','HabitatRules.WaterSurfaceY(State.waterLevel)+.095f')
    return s
edit('HabitatGame.Chapter',chapter)
print('Updated feedback rules, rendering, targeted prompts and confirmed 100-coin reward; originals backed up.')
