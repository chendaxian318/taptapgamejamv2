using System;

namespace HabitatIsland
{
    /// <summary>The four authored maps in the user's first screenshot, with source rows kept intact.</summary>
    public static class HabitatMechanicLevels
    {
        public const int FirstIndex = 5;
        public const int Count = 4; // Preserve the original contiguous save indices.
        public const int FifthIndex = 10;
        public static int MapNumber(int index) => index==4?6:index==9?7:index==FifthIndex?5:index-FirstIndex+1;
        public static readonly string[] Names = {
            "钓竿与金鱼 · 正式关卡", "大虾与吹风机 · 正式关卡",
            "寄居蟹 · 正式关卡", "水母与潮汐 · 正式关卡", "磁铁与发射器 · 正式关卡"
        };
        // Source coordinates start at the top left. World Z = depth - 1 - source row.
        // Dots are blue water cells; labels without numerals remain verbatim here.
        public static readonly string[][] SourceRows = {
            new[] {
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|3|1|.|.|.|.|.|.|.|.",
                ".|.|.|.|2|2|1|.|1|箱子|1|.|.|.|.",
                ".|.|2|2|1|1|1|.|.|1|1|1|2|2|.",
                ".|.|2|1|1|1|漂浮|.|.|.|.|1|1|2|.",
                ".|.|2|1|1|.|.|金鱼|.|.|.|1|钓竿|2|.",
                ".|.|1|1|1|1|1|1|箱子|.|.|.|.|.|.",
                ".|.|.|1|1|1|1|箱子|1|.|.|.|.|.|.",
                ".|.|.|1|.|.|1|1|2|.|.|.|.|.|.",
                ".|.|.|入口|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|."
            },
            new[] {
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|2|2|2|.|1|1|1|.|.|.|.|.",
                ".|.|1|1|1|1|.|1|.|.|小鱼|.|1|虾|.",
                ".|入口|1|1|1|1|.|箱子|1|箱子|.|1|箱子|1|.",
                ".|.|1|1|1|1|.|.|1|1|.|箱子|1|1|.",
                ".|.|.|1|漂浮|.|.|.|.|.|.|1|背篓|2|.",
                ".|.|.|.|.|.|.|.|.|.|.|1|2|2|.",
                ".|.|.|.|.|.|.|礁石|礁石|.|.|.|.|.|.",
                ".|.|.|1|1|1|.|.|小鱼|.|.|.|.|.|.",
                ".|.|.|2|1|1|礁石|.|1|1|2|.|.|.|.",
                ".|.|.|2|1|1|1|1|箱子|1|2|.|.|.|.",
                ".|.|.|.|2|1|箱子|箱子|1|.|.|.|.|.|.",
                ".|.|.|.|2|1|1|1|1|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|."
            },
            new[] {
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|3|2|2|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|2|1|1|2|2|礁石|礁石|.|.|.|.|.|.",
                ".|.|.|.|.|1|1|箱子|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|1|箱子|2|.|.|.|1|2|.|.|.",
                ".|.|.|入口|1|1|1|1|.|.|1|虾|1|.|.|.",
                ".|.|.|.|1|1|1|1|.|.|礁石|1|1|.|.|.",
                ".|.|.|.|1|1|寄居蟹|1|.|.|.|.|.|.|.|.",
                ".|.|.|.|1|1|2|1|.|.|.|礁石|.|.|.|.",
                ".|.|.|1|1|1|1|.|.|金鱼|.|.|.|.|.|.",
                ".|.|.|1|礁石|1|2|.|2|2|1|1|2|.|.|.",
                ".|.|.|1|1|1|1|.|1|1|箱子|1|2|.|.|.",
                ".|.|.|.|2箱子|2|1|1|1|2|1|1|2|.|.|.",
                ".|.|.|.|2|2|攀爬|1|箱子|1|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|."
            },
            new[] {
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|2箱子|3|.|.|.|.|.|.|.",
                ".|.|.|1|1|攀爬|2|2|2|漂浮|.|2|2|.|.|.",
                ".|.|.|1|1|1|2|2|2|潮汐|.|2|2|2|2箱子|.",
                ".|.|入口|1|1|1|2|1|1|礁石|.|.|2|2|2箱子|.",
                ".|.|.|.|漂浮|1|1|箱子|2|.|.|.|2|3|2大虾|.",
                ".|.|.|.|.|1|1|1|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|1|1|.|.|.|.|.",
                ".|.|.|1|.|.|.|.|.|1|1|1|.|.|.|.",
                ".|.|.|1|1|1|1|.|.|1|1|1|.|.|.|.",
                ".|.|.|1|1|箱子|水母|.|.|金鱼|1|箱子|.|.|.|.",
                ".|.|.|2|1|1|2|.|.|.|1|2|.|.|.|.",
                ".|.|.|.|箱子|1|1|.|.|1|1|2|.|.|.|.",
                ".|.|.|.|.|1|1|.|1|1|1|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|."
            },
            new[] {
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|入口|.|.|.|.|.",
                ".|.|.|1|1|.|.|.|.|漂浮|1|1|1|.|.|.|.",
                ".|.|.|1大虾|1|1|2|.|.|1|1|1|1|.|.|.|.",
                ".|.|.|2|1|箱子|1|.|.|1|2|箱子|1|.|.|.|.",
                ".|.|.|1|箱子|2|1|.|礁石|1|1|1|1|.|.|.|.",
                ".|.|.|1|1|1|.|.|.|2|.|箱子|1|.|.|.|.",
                ".|.|.|.|.|礁石|.|金鱼|.|.|.|礁石|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|礁石|箱子|1|.|.|.|.|.",
                ".|.|.|.|.|.|1|.|1|1|1|1|.|.|.|.|.",
                ".|.|.|.|.|.|1|2|1|1|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|1|寄居蟹|1|1|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|1|2|1|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|1|1|1|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|."
            },
            new[] {
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|1|1|1|.|.",
                ".|.|.|.|.|.|.|1|1|1入口|1|1|1|2|.|.",
                ".|.|.|.|.|.|.|1|1|1|1攀爬|1|1|1箱子|.|.",
                ".|.|.|.|.|.|.|1|1|2|2|2|漂浮|.|.|.",
                ".|.|2|2|2|.|.|1|1|2|3|2|.|.|.|.",
                ".|.|2|2|2|.|漂浮|2|2|2|2箱子|2|.|.|.|.",
                ".|.|2|2箱子|2箱子|.|.|2箱子|2|2潮汐|2|2|.|.|.|.",
                ".|.|2|2箱子|2|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|水母|.|.|1|1|1|.|.|.|.|.",
                ".|.|礁石|礁石|礁石|礁石|1|.|1|1|1|.|.|.|.|.",
                ".|.|.|1箱子|1|1|1|.|.|1|1|.|.|.|.|.",
                ".|.|.|1|1|2|金鱼|.|1|1箱子|2|.|.|.|.|.",
                ".|.|.|.|1|1|1|1|1|1|2|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|."
            },
            new[] {
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|1|1|.|.|.|1箱子|2|.|.|.|.|.|.|.|.",
                ".|.|.|.|1|1|1|.|.|2|1|.|.|.|.|.|.|.|.",
                ".|.|.|.|1|1|1|.|漂浮|1|1|.|.|.|.|.|.|.|.",
                ".|.|.|.|1|1小石子|1|.|金鱼|1|1|.|.|1|1寄居蟹|.|.|.|.",
                ".|.|.|.|1入口|1|1箱子|.|.|1|1|.|.|1|1|.|.|.|.",
                ".|.|.|.|1|1|1|1|1箱子|1|1|.|.|1|1箱子|1|1|.|.",
                ".|.|.|.|1|1|1|1|1|2|2|2|.|1|1|1|1|.|.",
                ".|.|.|.|.|.|.|.|.|2|2箱子|2|1|1|1|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|2|2|2|1|1|.|.|.|.|.",
                ".|.|.|.|1|1|1箱子|.|.|.|.|.|1|1|.|.|.|.|.",
                ".|.|.|.|1|1|.|.|.|漂浮|.|.|1|1|1|1|.|.|.",
                ".|.|.|.|1|1|1红虾|2|2|2箱子|2|2潮汐|1|1|1|1|.|.|.",
                ".|.|.|.|1|1|1|1攀爬|2|2|2|2|1攀爬|.|.|.|.|.|.",
                ".|.|.|.|1|1|1|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
                ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|."
            }
        };
        static readonly string[] Objectives = {
            "探索钓竿与金鱼关卡：取得岛上钓竿，利用箱子和漂浮方块整理金鱼水域。",
            "探索大虾与吹风机关卡：使用漂浮方块往返岛屿，接近大虾与背篓。风机需在商店购买。",
            "引导寄居蟹到邻水陆地，3×3 内有水生动物陪伴且不受惊吓，满足繁衍环境即可通关。",
            "调整水域与潮汐：水母连通外海、金鱼处于封闭水域，并安置好大虾；所有动物同时适居即可通关。",
            "使用磁铁或发射器搭建路线：让金鱼处于封闭水域、寄居蟹邻水并有水生邻居，同时安置好大虾。"
        };

        public static bool Contains(int index) { return index >= FirstIndex && index < FirstIndex + Count || index==FifthIndex; }
        public static bool IsOptionalIndex(int index)=>index==4||index==9;
        public static bool IsNewOptional(HabitatState s)=>s!=null&&IsOptionalIndex(s.levelIndex)&&s.sourceNodeId=="feedback-20261018-optional";
        public static bool HasHabitatGoal(HabitatState s) => s!=null && s.authoredLevel && (Contains(s.levelIndex)||IsNewOptional(s));
        public static string HabitatObjective(HabitatState s) => HasHabitatGoal(s)&&Contains(s.levelIndex)?Objectives[MapNumber(s.levelIndex)-1]:s.objective;
        public static bool ResidentsSuitable(HabitatState s,Tuning tuning,Species species)
        {
            bool found=false;
            foreach(Animal a in s.animals)
            {
                if(a.species!=species)continue;
                found=true;
                if(!HabitatRules.Evaluate(s,a,tuning).suitable)return false;
            }
            return found;
        }
        public static int HabitatGoalCount(HabitatState s)
        {
            var kinds=new System.Collections.Generic.HashSet<Species>();
            if(s!=null)foreach(var a in s.animals)kinds.Add(a.species);
            return kinds.Count;
        }
        public static int HabitatProgress(HabitatState s,Tuning tuning)
        {
            if(!HasHabitatGoal(s))return 0;
            var kinds=new System.Collections.Generic.HashSet<Species>();
            int suitable=0;
            foreach(var a in s.animals)if(kinds.Add(a.species)&&ResidentsSuitable(s,tuning,a.species))suitable++;
            return suitable;
        }
        // All current residents must qualify simultaneously. This is called only after visuals arrive.
        public static bool CompleteHabitat(HabitatState s,Tuning tuning)
        {
            if(!HasHabitatGoal(s)||s.won||s.animals.Count==0||HabitatProgress(s,tuning)!=HabitatGoalCount(s))return false;
            s.won=true;
            HabitatRules.Log(s,s.levelName+"完成：所有登场动物同时满足繁衍条件。可以捕捞、拾壳并返航交付。");
            return true;
        }
        public static string HabitatChecklist(HabitatState s,Tuning tuning)
        {
            if(s.won)return "本关已完成 · 可收获后返航";
            var kinds=new System.Collections.Generic.HashSet<Species>();
            var lines=new System.Collections.Generic.List<string>();
            foreach(var a in s.animals)
                if(kinds.Add(a.species))lines.Add(HabitatRules.Name(a.species)+" · "+(ResidentsSuitable(s,tuning,a.species)?"已适居":"待安置"));
            return string.Join("\n",lines);
        }

        public static HabitatState Create(int index)
        {
            if(!Contains(index)&&!IsOptionalIndex(index))throw new ArgumentOutOfRangeException(nameof(index));
            int map=MapNumber(index)-1, depth=SourceRows[map].Length, width=SourceRows[map][0].Split('|').Length;
            var s=new HabitatState { width=width, depth=depth, levelIndex=index,
                levelName=HabitatLevels.Names[index], authoredLevel=true, sourceNodeId=IsOptionalIndex(index)?"feedback-20261018-optional":"latest-plan-20261018-map"+(map+1),
                coins=0, cargo=0, traps=0, rod=false, seed=621016+map*917,
                dockRotation=map==0?2:map==4||map==5?0:3, facingX=map==0||map==4?0:1, facingZ=map==0?1:map==4?-1:0,
                goals=new Species[0], tiles=new Tile[width*depth], objective=map<5?Objectives[map]:"自由探索：运用竹排、潮汐和箱子，使本岛所有动物同时适居；工具可从商店准备。" };
            for(int row=0;row<depth;row++)
            {
                string[] cells=SourceRows[map][row].Split('|');
                if(cells.Length!=width)throw new InvalidOperationException("Authored source row width mismatch: "+index+"/"+row);
                for(int x=0;x<width;x++)
                {
                    string cell=cells[x];int z=depth-1-row;
                    var tile=new Tile { height=SourceHeight(cell) };
                    if(cell.Contains("漂浮")){tile.prop=PropKind.Raft;tile.rotation=map==1||map==3&&x==4||map==6&&row==12?0:1;}
                    else if(cell.Contains("攀爬"))tile.prop=PropKind.ClimbBlock;
                    else if(cell.Contains("潮汐"))tile.prop=PropKind.Tide;
                    else if(cell.Contains("礁石"))tile.prop=PropKind.Obstacle;
                    else if(cell.Contains("箱子"))tile.prop=PropKind.Crate;
                    else if(cell=="钓竿"||cell=="背篓")
                    {
                        tile.prop=PropKind.Chest;
                        tile.chestReward=cell=="钓竿"?ChestReward.FishingRod:ChestReward.BackBasket;
                    }
                    s.tiles[z*width+x]=tile;
                    if(cell.Contains("入口")) { s.playerX=s.dockX=x;s.playerZ=s.dockZ=z; }
                    if(cell.Contains("虾"))Add(s,Species.Shrimp,x,z);
                    else if(cell.Contains("金鱼")||cell.Contains("小鱼"))Add(s,Species.Goldfish,x,z);
                    else if(cell.Contains("寄居蟹"))Add(s,Species.Crab,x,z);
                    else if(cell.Contains("水母"))Add(s,Species.Jellyfish,x,z);
                }
            }
            s.message=s.objective;
            HabitatRules.Log(s,"载入 "+s.levelName+"："+width+"×"+depth+"，按最新策划独立正式地图逐格转录。");
            HabitatRules.Log(s,s.objective);
            return s;
        }

        static int SourceHeight(string cell)
        {
            if(cell.Length>0&&char.IsDigit(cell[0]))return cell[0]-'0';
            if(cell=="."||cell=="漂浮"||cell=="礁石"||cell=="金鱼"||cell=="小鱼"||cell=="水母")return 0;
            // The two unnumbered white labels are on the right height-2 platform in map four.
            if(cell=="潮汐"||cell=="大虾")return 2;
            // White, unnumbered entrances, crates, tools and land animals inherit the neighbouring height 1.
            return 1;
        }

        static void Add(HabitatState s,Species species,int x,int z)
        { s.animals.Add(new Animal { id=s.animals.Count+1,species=species,x=x,z=z,homeX=x,homeZ=z }); }

        public static string CellLabel(int index,int x,int z)
        {
            if(!Contains(index)&&!IsOptionalIndex(index))return "";
            string[] rows=SourceRows[MapNumber(index)-1];
            if(z<0||z>=rows.Length)return "";
            string[] cells=rows[rows.Length-1-z].Split('|');
            return x<0||x>=cells.Length?"":cells[x];
        }
    }
}
