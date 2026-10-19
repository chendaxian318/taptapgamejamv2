using System;

namespace HabitatIsland
{
    /// <summary>The authored mountain island from the second supplied level board.</summary>
    public static class HabitatMountainLevel
    {
        public const int Index = 9;
        public const int Width = 19, Depth = 21;

        // Original rows run north to south; world Z runs in the opposite direction.
        // Keep the labels separate from runtime mappings so the source remains inspectable.
        public static readonly string[] SourceRows = {
            ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|.|2|2|2|.|.|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|.|2|2|2|2|3|.|.|.|.|.|.|.",
            ".|.|1|1|1|1|2|2|2吹风机|2|2|2|.|.|.|.|.|.|.",
            ".|.|1|1|1|1|1|1攀高方块|2|2|2|2|2|.|.|.|.|.|.",
            ".|船|1|1|1|2|2|漂流方块|.|.|.|2|2箱子|.|.|.|.|.|.",
            ".|小|1|1|1|2|2|.|.|.|2|1|1|.|.|.|.|.|.",
            ".|.|1|1|1|1|1攀爬方块|.|.|.|2|1箱子|1箱子|.|.|.|.|.|.",
            ".|.|.|.|.|1|1|3|2|3|3背篓|1|1|.|.|.|.|.|.",
            ".|.|.|.|.|1|1|2|2|2攀爬方块|1|1|1|贝壳|.|.|.|.|.",
            ".|.|.|.|1|1|1|1|1|1|1|.|.|.|.|.|.|.|.",
            ".|.|1|1|1|1|1|1|1|1|.|.|.|.|.|.|.|.|.",
            ".|.|1|1|1|1|1|.|.|.|水母|.|.|.|.|.|.|.|.",
            ".|.|.|.|1|1|1|.|.|.|.|.|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|.|.|2|1|1|1|1|1|.|.|.|.|.",
            ".|.|.|.|.|.|.|3|2|2|1|1|1|1|.|.|.|.|.",
            ".|.|.|.|.|.|.|.|1|1|1|1|1|1|.|.|.|.|.",
            ".|.|.|.|.|.|.|.|3|2|1|1|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|.|.|3|4|3|1|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.",
            ".|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|.|."
        };

        public static HabitatState Create()
        {
            var s = new HabitatState {
                width=Width, depth=Depth, levelIndex=Index,
                levelName="群山之巅 · 正式关卡", authoredLevel=true,
                sourceNodeId="user-image-20261016:2-right",
                playerX=2, playerZ=15, dockX=2, dockZ=15, dockRotation=3,
                coins=0, cargo=0, traps=0, rod=false, seed=621609,
                goals=new Species[0], tiles=new Tile[Width*Depth],
                objective="借助攀爬方块登上高地，调整木箱与浮块，探索群山之巅。"
            };
            if(SourceRows.Length!=Depth)throw new InvalidOperationException("群山之巅源图行数不匹配。");
            for(int row=0;row<Depth;row++)
            {
                string[] cells=SourceRows[row].Split('|');
                if(cells.Length!=Width)throw new InvalidOperationException("群山之巅源图第 "+(row+1)+" 行列数不匹配。");
                for(int x=0;x<Width;x++)
                {
                    string cell=cells[x]; int z=Depth-1-row;
                    int height=cell.Length>0&&char.IsDigit(cell[0])?cell[0]-'0':0;
                    var tile=new Tile { height=height };
                    if(cell.Contains("漂流方块")){tile.prop=PropKind.Raft;tile.rotation=1;}
                    else if(cell.Contains("攀爬方块")||cell.Contains("攀高方块"))tile.prop=PropKind.ClimbBlock;
                    else if(cell.Contains("吹风机"))
                    {
                        tile.prop=PropKind.Fan;
                        // The board's route arrow points south; the fan can still be rotated.
                        tile.rotation=2;
                    }
                    else if(cell.Contains("箱子"))tile.prop=PropKind.Crate;
                    else if(cell.Contains("背篓"))
                    {
                        tile.prop=PropKind.Chest;
                        tile.chestReward=ChestReward.BackBasket;
                    }
                    else if(cell.Contains("贝壳"))tile.prop=PropKind.Shell;
                    s.tiles[z*Width+x]=tile;
                    if(cell.Contains("水母"))
                        s.animals.Add(new Animal { id=s.animals.Count+1, species=Species.Jellyfish,
                            x=x, z=z, homeX=x, homeZ=z });
                }
            }
            HabitatRules.Log(s,"群山之巅：19×21 格，数字为地面高度；小船右侧上岸。");
            HabitatRules.Log(s,s.objective);
            return s;
        }

        public static string CellLabel(int x,int z)
        {
            if(x<0||x>=Width||z<0||z>=Depth)return "";
            return SourceRows[Depth-1-z].Split('|')[x];
        }
    }
}
