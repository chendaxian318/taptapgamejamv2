using System;

namespace HabitatIsland
{
    /// <summary>Continuous FigJam map with the user's 2026-10-16 height revision; cell coordinates remain unchanged.</summary>
    public static class HabitatFigJamLevel
    {
        public const int Index = 4;
        public const int Width = 18, Depth = 19;
        // Rows run north to south exactly as on the board. World Z runs south to north.
        public static readonly string[] SourceRows = {
            ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|2|2|2|2|.|2|2|.|.|.|.|.",
            ".|.|.|.|.|2|2|2箱子|1潮汐引擎|漂流方块|.|2|2宝箱（可以获得钓鱼竿）|1|1|.|.|.",
            ".|.|.|.|.|2|1攀爬方块|2|2|1|1|1障碍物|1|1|1|.|.|.",
            ".|.|.|.|1|1|1|1|1|1|1|1|1|1|1|.|.|.",
            ".|.|.|1|1|1|1|1|1|1|1|1|.|.|.|.|.|.",
            ".|.|小|1|1|1|2|3红虾|2|1|1|.|.|.|.|.|.|.",
            ".|.|船|1|1|1|1|小鱼|小鱼|1|1|.|.|.|.|.|.|.",
            ".|.|.|1|.|.|.|.|.|.|.|.|.|.|.|1|2|2",
            ".|.|.|.|.|.|.|1|1|1|1|.|.|.|.|1|宝箱|1箱子",
            ".|.|.|.|漂流方块|漂流方块|.|1|1|1|1|.|.|.|.|1|1|1",
            ".|.|漂流方块|1|1|1寄居蟹|1|1|2箱子|1|1|.|.|.|.|.|.|.",
            ".|.|.|1|1|1|1|1障碍物|1|2|1|.|.|.|.|.|.|.",
            ".|.|.|.|.|1|1箱子|1|1|3|1|1箱子|1|.|.|.|.|.",
            ".|.|.|.|.|1|1|1|1|.|.|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|."
        };
        public static HabitatState Create()
        {
            var s = new HabitatState { width=Width, depth=Depth, levelIndex=Index,
                levelName="荒岛 · 连续关卡", authoredLevel=true, sourceNodeId="0:1",
                playerX=3, playerZ=10, dockX=3, dockZ=10, dockRotation=3,
                coins=0, cargo=0, traps=0, rod=false, seed=620013,
                goals=new Species[0], tiles=new Tile[Width*Depth],
                objective="找到特制鱼竿，围住并钓获两条琉璃鱼，回商店领取报酬。" };
            for(int row=0;row<Depth;row++)
            {
                string[] cells=SourceRows[row].Split('|');
                for(int x=0;x<Width;x++)
                {
                    string cell=cells[x]; int z=Depth-1-row;
                    int height=cell.Length>0&&char.IsDigit(cell[0])?cell[0]-'0':0;
                    // This white chest cell has no numeral. Its neighbouring land is height 1.
                    if(cell=="宝箱")height=1;
                    var tile=new Tile { height=height };
                    if(cell.Contains("漂流方块")){tile.prop=PropKind.Raft;tile.rotation=1;}
                    else if(cell.Contains("攀爬方块"))tile.prop=PropKind.ClimbBlock;
                    else if(cell.Contains("潮汐引擎"))tile.prop=PropKind.Tide;
                    else if(cell.Contains("障碍物"))tile.prop=PropKind.Obstacle;
                    else if(cell.Contains("箱子"))tile.prop=PropKind.Crate;
                    else if(cell.Contains("宝箱"))
                    {
                        tile.prop=PropKind.Chest;
                        tile.chestReward=cell.Contains("钓鱼竿")?ChestReward.FishingRod:ChestReward.Unspecified;
                    }
                    s.tiles[z*Width+x]=tile;
                    if(cell.Contains("红虾"))Add(s,Species.Shrimp,x,z);
                    else if(cell.Contains("小鱼"))Add(s,Species.Goldfish,x,z);
                    else if(cell.Contains("寄居蟹"))Add(s,Species.Crab,x,z);
                }
            }
            // Requested model placement: a usable cage beside the landing path.
            // Keep the source terrain/props and the player's empty inventory intact.
            s.tiles[9*Width+4].trap=true;
            HabitatRules.Log(s,"荒岛连续图：18×19 格，按 2026-10-16 截图更新五处地面高度；道具另叠加。");
            HabitatRules.Log(s,"从小船右侧陆地上岸。开局没有鱼竿、补给和货币；按原图路径测试。");
            return s;
        }
        static void Add(HabitatState s,Species species,int x,int z)
        { s.animals.Add(new Animal { id=s.animals.Count+1,species=species,x=x,z=z,homeX=x,homeZ=z }); }
        public static string CellLabel(int x,int z)
        {
            if(x<0||x>=Width||z<0||z>=Depth)return "";
            return SourceRows[Depth-1-z].Split('|')[x];
        }
        public static string Checklist(HabitatState s)
        {
            return "潮汐 "+(s.tideEverActivated?"已触发":"未触发")+" · 鱼竿 "+(s.rodFromChest?"已取得":"未取得")+
                "\n宝箱 "+s.openedChests+"/2 · 蟹受惊移动 "+(s.crabMovedByShrimp?"已观察":"未观察");
        }
    }
}

