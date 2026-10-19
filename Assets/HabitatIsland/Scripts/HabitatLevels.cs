using System;

namespace HabitatIsland
{
    public static class HabitatLevels
    {
        public static string[] Names={"教学 · 螃蟹的欢迎会","练习 · 池塘与高地","练习 · 潮汐机关","沙盒 · 六物种生态","自由探索一 · 潮汐群岛",
            "关卡一 · 钓竿与金鱼","关卡二 · 大虾与吹风机","关卡三 · 寄居蟹","关卡四 · 水母与潮汐","自由探索二 · 回环海湾","关卡五 · 磁铁与发射器","家园港口"};
        public const int HomeHarborIndex = 11;
        public static bool IsHomeHarbor(HabitatState s) => s != null && s.levelIndex == HomeHarborIndex;
        // Keep saved indices stable; only these destinations belong to the normal boat route.
        public static readonly int[] CampaignDestinations={5,6,7,8,10,4,9,0};
        public static bool IsFormal(int index) => index>=4&&index<HomeHarborIndex;
        public static string CellLabel(HabitatState s,int x,int z)
        {
            if(s==null||!HabitatRules.Inside(s,x,z))return "";
            if(HabitatMechanicLevels.IsNewOptional(s))return HabitatMechanicLevels.CellLabel(s.levelIndex,x,z);
            if(s.levelIndex==HabitatFigJamLevel.Index)return HabitatFigJamLevel.CellLabel(x,z);
            if(HabitatMechanicLevels.Contains(s.levelIndex))return HabitatMechanicLevels.CellLabel(s.levelIndex,x,z);
            if(s.levelIndex==HabitatMountainLevel.Index)return HabitatMountainLevel.CellLabel(x,z);
            return "";
        }
        public static string Checklist(HabitatState s)
        {
            int remaining=0;foreach(var tile in s.tiles)if(tile.prop==PropKind.Chest)remaining++;
            return "宝箱 "+s.openedChests+" / "+(s.openedChests+remaining)+" · 海平面 "+(s.waterLevel+1)+
                "\n鱼竿 "+(s.rod?"已取得":"未取得")+" · 贝壳 "+s.shells+(s.backBasketUnlocked?" · 背篓已取得":"");
        }
        public static HabitatState Create(int index)
        {
            index=Math.Max(0,Math.Min(Names.Length-1,index));
            if(index==HomeHarborIndex)return CreateHomeHarbor();
            if(HabitatMechanicLevels.IsOptionalIndex(index))return HabitatMechanicLevels.Create(index);
            if(HabitatMechanicLevels.Contains(index))return HabitatMechanicLevels.Create(index);
            
            HabitatState s=Base(index);
            if(index==0)Welcome(s);else if(index==1)Lab(s);else if(index==2)Tide(s);else Sandbox(s);
            s.message=s.objective;HabitatRules.Log(s,"载入 "+s.levelName);HabitatRules.Log(s,s.objective);return s;
        }
        static HabitatState CreateHomeHarbor()
        {
            // Same starting economy as the first island, without spawning its puzzle.
            var s=HabitatMechanicLevels.Create(HabitatChapterRules.MainLevel);
            s.levelIndex=HomeHarborIndex;s.levelName=Names[HomeHarborIndex];
            s.width=s.depth=1;s.tiles=new[]{new Tile{height=1}};
            s.playerX=s.playerZ=s.dockX=s.dockZ=0;s.dockRotation=0;
            s.animals.Clear();s.goals=new Species[0];s.earnedSpecies.Clear();
            s.onHarbor=true;s.won=false;s.authoredLevel=true;s.sourceNodeId="home-harbor";
            s.objective="在港口与老板交谈、回家休息，或乘小船前往岛屿。";
            s.message=s.objective;s.events.Clear();return s;
        }
        static HabitatState Base(int index)
        {
            HabitatState s=new HabitatState{levelIndex=index,levelName=Names[index],playerX=1,playerZ=2,dockX=1,dockZ=2,coins=30,cargo=4,seed=271828+index*9127};
            s.tiles=new Tile[s.width*s.depth];
            for(int z=0;z<s.depth;z++)for(int x=0;x<s.width;x++)s.tiles[z*s.width+x]=new Tile{height=x==0||z==0||x==8||z==8?-1:1};
            return s;
        }
        static Tile Tile(HabitatState s,int x,int z){return HabitatRules.TileAt(s,x,z);}
        static void Water(HabitatState s,int x,int z){Tile(s,x,z).height=0;}
        static void Prop(HabitatState s,int x,int z,PropKind p,int rotation=0){Tile q=Tile(s,x,z);q.prop=p;q.rotation=rotation;}
        static void Add(HabitatState s,Species species,int x,int z)
        {s.animals.Add(new Animal{id=s.animals.Count+1,species=species,x=x,z=z,homeX=x,homeZ=z});}
        static void Welcome(HabitatState s)
        {
            s.goals=new[]{Species.Crab};
            s.objective="搬走 (3,3) 的虾，放到 (2,4) 陷阱；让螃蟹与海星、水母相伴并首次繁衍。螃蟹和海星不能搬起。";
            for(int z=3;z<=4;z++)for(int x=3;x<=4;x++)Water(s,x,z);
            Tile(s,3,1).height=3; // Prevent the frightened crab escaping before the first interaction.
            Tile(s,4,1).height=3; // Keep the tutorial's shoreline companion beside the crab.
            Tile(s,2,2).trap=true; // Keep the tutorial's named shrimp in place until the planner acts.
            Tile(s,1,3).trap=true;
            Add(s,Species.Crab,3,2);Add(s,Species.Jellyfish,3,3);Add(s,Species.Starfish,4,2);Add(s,Species.Shrimp,2,2);
            Prop(s,6,6,PropKind.Chest);Prop(s,6,4,PropKind.Climb);Tile(s,7,4).height=3;
        }
        static void Lab(HabitatState s)
        {
            s.goals=new[]{Species.Goldfish,Species.Turtle};
            s.objective="在码头领取土块：填 (5,3) 封闭金鱼池，再填 (7,6) 连起两格高地。金鱼与乌龟各首次繁衍。";
            for(int z=3;z<=4;z++)for(int x=3;x<=4;x++)Water(s,x,z);
            Water(s,4,1);Water(s,4,2);
            Tile(s,6,4).height=2;Tile(s,6,5).height=1;
            Add(s,Species.Goldfish,3,3);Add(s,Species.Jellyfish,4,4);Add(s,Species.Turtle,6,4);
            Add(s,Species.Shrimp,7,7);Tile(s,7,7).trap=true;
            Prop(s,2,6,PropKind.Road,1);Prop(s,7,2,PropKind.Chest);
        }
        static void Tide(HabitatState s)
        {
            s.goals=new[]{Species.Jellyfish,Species.Turtle};
            s.objective="先走到 (3,3)，按 Shift 向上攀上 (3,4) 攀爬方块，再落到 (3,5)，向右推箱到潮汐踏板；海平面 +1。普通台阶不能登高。让水母、乌龟首次繁衍。";
            // Recreates the source's key height pattern: ocean, low interior, enclosing raised paths,
            // pressure plate, crate, floating rider route, directed roads and reachable highland.
            for(int z=1;z<=7;z++)for(int x=1;x<=7;x++)Tile(s,x,z).height=2;
            for(int z=2;z<=6;z++)for(int x=3;x<=6;x++)Tile(s,x,z).height=1;
            Tile(s,1,2).height=1;Tile(s,2,2).height=1;Tile(s,2,3).height=2;
            // This practice map used to rely on an ordinary one-layer step out of
            // its spawn pocket. Only the dedicated block now permits that ascent.
            Prop(s,2,3,PropKind.ClimbBlock);
            Tile(s,3,4).height=2;Tile(s,4,4).height=2;Prop(s,3,4,PropKind.Crate);Prop(s,4,4,PropKind.Tide);
            Tile(s,5,5).height=0;Tile(s,6,5).height=1;Tile(s,5,6).height=1;
            Add(s,Species.Jellyfish,5,5);Add(s,Species.Turtle,6,7);
            // The home remains connected highland after tide rises.
            Tile(s,5,7).height=2;Tile(s,6,7).height=2;Tile(s,7,7).height=2;
            Tile(s,4,2).height=0;Tile(s,5,2).height=0;Tile(s,6,2).height=0;
            Prop(s,3,2,PropKind.Fan,1);Prop(s,4,2,PropKind.Float);
            Prop(s,2,5,PropKind.Road,0);Prop(s,7,3,PropKind.Road,2);Prop(s,1,6,PropKind.Chest);
            s.cargo=6;s.coins=50;
        }
        static void Sandbox(HabitatState s)
        {
            s.goals=new[]{Species.Jellyfish,Species.Shrimp,Species.Goldfish,Species.Crab,Species.Turtle,Species.Starfish};
            s.objective="六物种各首次繁衍，且目标达成时均有适宜亲本。可暂停生物移动，对比高度、保护、封闭池塘与邻居多样性。";
            for(int z=3;z<=4;z++)for(int x=3;x<=4;x++)Water(s,x,z);
            Tile(s,3,1).height=3;Tile(s,5,1).height=2;Tile(s,6,1).height=2;Tile(s,6,2).height=2;
            Add(s,Species.Crab,3,2);Add(s,Species.Starfish,4,2);Add(s,Species.Jellyfish,4,3);Add(s,Species.Goldfish,3,4);
            Add(s,Species.Turtle,6,1);Add(s,Species.Shrimp,7,7);Tile(s,7,7).trap=true;
            Prop(s,2,6,PropKind.Road,1);Prop(s,3,6,PropKind.Road,0);Prop(s,4,6,PropKind.Crate);
            Prop(s,5,6,PropKind.Tide);Tile(s,5,6).height=2;
            Prop(s,7,4,PropKind.Fan,2);Water(s,7,3);Prop(s,7,3,PropKind.Float);Water(s,7,2);
            Prop(s,1,7,PropKind.Chest);s.coins=100;s.cargo=12;s.traps=6;
        }
    }
}
