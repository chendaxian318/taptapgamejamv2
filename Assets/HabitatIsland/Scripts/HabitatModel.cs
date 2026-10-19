using System;
using System.Collections.Generic;

namespace HabitatIsland
{
    public enum Species { Jellyfish, Shrimp, Goldfish, Crab, Turtle, Starfish }
    public enum PropKind { None, Crate, Float, Climb, Road, Fan, Tide, Obstacle, Chest, ClimbBlock, Shell, Raft }
    public enum ChestReward { Coins=0, FishingRod=1, Unspecified=2, BackBasket=3 }
    [Serializable] public class Tile
    {
        public int height; public PropKind prop; public int rotation; public bool trap;
        public PropKind coveredProp; // A pressure plate or road under a movable block.
        public ChestReward chestReward;
    }
    [Serializable] public class Animal
    {
        public int id,x,z; public Species species; public float progress; public int offspring;
        public bool carried; public int homeX,homeZ; public float moveClock; public int spawnedTotal;
        // An authored crab sheds at most one collectible shell. This belongs to the island/save.
        public bool shellShed;
        public bool catchClaimed;
        public bool roamAnchored;public int roamX,roamZ;
    }
    [Serializable] public class Tuning
    {
        public float breedSeconds=18,moveSeconds=2.5f; public bool animalMovement=true;
        public int shrimpRadius=1,friendlyRadius=1,minWater=3,minPond=4,minHighland=2;
    }
    [Serializable] public class HabitatState
    {
        public int width=9,depth=9; public Tile[] tiles; public List<Animal> animals=new List<Animal>();
        public int playerX,playerZ,facingX=0,facingZ=1,coins=30,cargo=3,fish=0,shrimp=0,carryingAnimal=-1;
        public PropKind carryingProp=PropKind.None; public int waterLevel=0,tickCount;
        public float elapsed; public bool rod=true; public int traps=2;
        public float playSeconds; // Shared real active play time; never ecological/simulation time.
        public int selectedX=-1,selectedZ=-1; public string message=""; public List<string> events=new List<string>();
        public int levelIndex; public string levelName,objective; public Species[] goals=new Species[0]; public bool won;
        public int dockX=1,dockZ=1,dockRotation=-1,seed=271828; public float movementClock; public bool tideActive;
        public int tideBaseLevel; public List<Species> earnedSpecies=new List<Species>();
        public bool authoredLevel; public string sourceNodeId;
        public int openedChests; public bool rodFromChest,tideEverActivated,crabMovedByShrimp;
        // Continuous harbor movement leaves the authored grid anchor and carried inventory intact.
        public bool onHarbor;
        public float harborX,harborZ,harborFacingX,harborFacingZ;
        public int day=1;
        public bool openingCompleted,openingCatchGranted;
        public int deliveredFish,deliveredShrimp,deliveryCount,deliveryIncome;
        // Offer/completion travel with the player; observation and dismissal belong to this map.
        public bool tutorialOfferPending,tutorialCompleted;
        public bool tutorialGuideDismissed,tutorialObservedCrab,tutorialObservedHabitat;
        // The first commission and its distinct catch travel with the player.
        public bool chapterOneAccepted,chapterOneSailed,chapterOneDelivered,chapterOneMorningSeen,specialRod;
        public int glassFish,glassFishCaught; public bool firstClearCgSeen;
        public bool backBasketUnlocked;
        public int shells;
        public int boatCrates, shovels, magnets, launchers;
        public int jellyfish,deliveredShells;
        public int campaignUnlockedStage,campaignBriefedMask;
        public List<string> seenGuideKeys=new List<string>();
        public List<PropKind> bagProps=new List<PropKind>();
    }
    public class HabitatReport
    {
        public bool suitable,safe,protectedByTurtle,areaClosed; public string reason="";
        public int waterSize,friendCount;
    }
    public static class HabitatRules
    {
        static readonly int[] DX={0,1,0,-1},DZ={1,0,-1,0};
        public static bool UsesConfirmedPlan(HabitatState s)
            =>HabitatMechanicLevels.HasHabitatGoal(s);
        // Single price table for purchase preview, actual transactions and UI labels.
        public static int ShopPrice(HabitatState s,string item)
        {
            bool campaign=HabitatChapterRules.IsCampaignShop(s);
            switch((item??"").ToLowerInvariant())
            {
                case "crate":return campaign?150:6;
                case "fan":return campaign?200:10;
                case "shovel":return 300;
                case "magnet":return 300;
                case "launcher":return 400;
                case "rod":return 10;
                case "trap":return 3;
                case "float":return 7;
                case "tide":return 12;
                case "road":return 3;
                case "soil":return s!=null&&s.cargo>0?0:5;
                default:return -1;
            }
        }
        public static int CatchSellPrice(Species species)
        {
            switch(species)
            {
                case Species.Goldfish:return 100;
                case Species.Shrimp:return 150;
                case Species.Crab:return 200;
                case Species.Jellyfish:return 300;
                default:return 0;
            }
        }
        public static int CatchSaleValue(HabitatState s)
        {
            if(s==null)return 0;
            bool campaign=HabitatChapterRules.IsCampaignShop(s);
            return s.fish*(campaign?CatchSellPrice(Species.Goldfish):HabitatJourneyRules.FishSellPrice)
                +(s.shrimp+CarriedShrimpCatchCount(s))*(campaign?CatchSellPrice(Species.Shrimp):HabitatJourneyRules.ShrimpSellPrice)
                +s.shells*CatchSellPrice(Species.Crab)+s.jellyfish*CatchSellPrice(Species.Jellyfish);
        }
        public static int CatchCount(HabitatState s,Species species)
        {
            if(s==null)return 0;
            switch(species)
            {
                case Species.Goldfish:return s.fish;
                case Species.Shrimp:return s.shrimp+CarriedShrimpCatchCount(s);
                case Species.Crab:return s.shells;
                case Species.Jellyfish:return s.jellyfish;
                default:return 0;
            }
        }
        public static bool SellCatch(HabitatState s,Species species,int quantity)
        {
            if(s==null)return false;
            if(!AtDock(s)){Log(s,"请回到码头旁进行交易");return false;}
            if(quantity<1||quantity>CatchCount(s,species)||CatchSellPrice(species)<=0)
            {Log(s,"可出售数量不足，请重新选择数量。");return false;}
            bool campaign=HabitatChapterRules.IsCampaignShop(s);
            int unit=species==Species.Goldfish&&!campaign?HabitatJourneyRules.FishSellPrice:
                species==Species.Shrimp&&!campaign?HabitatJourneyRules.ShrimpSellPrice:CatchSellPrice(species);
            // Validate the complete quantity before converting a carried shrimp or touching stock.
            if(species==Species.Shrimp)StoreCarriedShrimp(s);
            switch(species)
            {
                case Species.Goldfish:s.fish-=quantity;s.deliveredFish+=quantity;break;
                case Species.Shrimp:s.shrimp-=quantity;s.deliveredShrimp+=quantity;break;
                case Species.Crab:s.shells-=quantity;s.deliveredShells+=quantity;break;
                case Species.Jellyfish:s.jellyfish-=quantity;break;
            }
            int income=quantity*unit;s.coins+=income;s.deliveryIncome+=income;s.deliveryCount++;
            Log(s,"出售 "+quantity+" 份"+(species==Species.Crab?"寄居蟹壳":Name(species))+"：+"+income+" 贝币");return true;
        }
        static readonly string[] SpeciesNames={"水母","虾","小金鱼","螃蟹","乌龟","海星"};
        public static string Name(Species sp)
        { return SpeciesNames[(int)sp]; }
        public static bool Inside(HabitatState s,int x,int z) { return x>=0&&z>=0&&x<s.width&&z<s.depth; }
        public static Tile TileAt(HabitatState s,int x,int z) { return Inside(s,x,z)?s.tiles[z*s.width+x]:null; }
        public static int Height(HabitatState s,int x,int z)
        {
            Tile q=TileAt(s,x,z); if(q==null)return -99;
            if(q.coveredProp==PropKind.Float&&IsBox(q.prop))return Math.Max(q.height,s.waterLevel+1)+1;
            if(q.prop==PropKind.Float||q.prop==PropKind.Raft)return s.waterLevel+1;
            return q.height+((q.prop==PropKind.Crate||q.prop==PropKind.Climb||q.prop==PropKind.ClimbBlock)?1:0);
        }
        public static bool IsWater(HabitatState s,int x,int z)
        { Tile q=TileAt(s,x,z); return q!=null&&q.prop!=PropKind.Float&&q.prop!=PropKind.Raft&&Height(s,x,z)<=s.waterLevel; }
        static bool Blocked(HabitatState s,int x,int z)
        { Tile q=TileAt(s,x,z); return q==null||q.prop==PropKind.Obstacle||q.prop==PropKind.Chest||q.prop==PropKind.Fan; }
        static bool Occupied(HabitatState s,int x,int z,Animal except=null)
        { foreach(Animal a in s.animals)if(a!=except&&!a.carried&&a.x==x&&a.z==z)return true; return false; }
        static bool High(HabitatState s,int x,int z) { return !Blocked(s,x,z)&&Height(s,x,z)>=Math.Max(2,s.waterLevel+1); }
        static bool Shore(HabitatState s,int x,int z)
        {
            if(Blocked(s,x,z)||IsWater(s,x,z))return false;
            for(int i=0;i<4;i++)if(IsWater(s,x+DX[i],z+DZ[i]))return true; return false;
        }
        static List<int> Flood(HabitatState s,int x,int z,Func<int,int,bool> allowed,bool stepLimited=false)
        {
            List<int> result=new List<int>(); if(!Inside(s,x,z)||!allowed(x,z))return result;
            bool[] seen=new bool[s.width*s.depth]; Queue<int> pending=new Queue<int>(); pending.Enqueue(z*s.width+x);seen[z*s.width+x]=true;
            while(pending.Count>0)
            {
                int p=pending.Dequeue(),px=p%s.width,pz=p/s.width;result.Add(p);
                for(int i=0;i<4;i++)
                {
                    int nx=px+DX[i],nz=pz+DZ[i];if(!Inside(s,nx,nz))continue;int n=nz*s.width+nx;
                    if(seen[n]||!allowed(nx,nz)||(stepLimited&&Math.Abs(Height(s,nx,nz)-Height(s,px,pz))>1))continue;
                    seen[n]=true;pending.Enqueue(n);
                }
            }
            return result;
        }
        static bool TurtleHome(HabitatState s,Animal a,int required)
        {
            if(a.carried||!High(s,a.homeX,a.homeZ)||Math.Abs(a.x-a.homeX)>1||Math.Abs(a.z-a.homeZ)>1||IsWater(s,a.x,a.z))return false;
            return Flood(s,a.homeX,a.homeZ,(x,z)=>High(s,x,z)&&Math.Abs(x-a.homeX)<=1&&Math.Abs(z-a.homeZ)<=1,true).Count>=Math.Max(1,required);
        }
        public static bool IsClosedWaterArea(HabitatState s,int x,int z)
        {
            if(s==null||s.tiles==null||!Inside(s,x,z))return false;
            var water=Flood(s,x,z,(px,pz)=>IsWater(s,px,pz)&&!Blocked(s,px,pz));
            if(water.Count==0)return false;
            foreach(int p in water)
                if(p%s.width==0||p%s.width==s.width-1||p/s.width==0||p/s.width==s.depth-1)return false;
            return true;
        }
        public static bool IsProtected(HabitatState s,int x,int z) { return IsProtected(s,x,z,2); }
        public static bool IsProtected(HabitatState s,int x,int z,int minHighland)
        { foreach(Animal a in s.animals)if(a.species==Species.Turtle&&TurtleHome(s,a,minHighland)&&Math.Abs(x-a.homeX)<=1&&Math.Abs(z-a.homeZ)<=1)return true;return false; }
        public static bool IsThreatened(HabitatState s,Animal a,Tuning t)
        {
            if(a.carried||a.species==Species.Shrimp||(!s.authoredLevel&&a.species==Species.Turtle)||IsProtected(s,a.x,a.z,t.minHighland))return false;
            foreach(Animal b in s.animals)
                if(!b.carried&&b.species==Species.Shrimp&&!IsProtected(s,b.x,b.z,t.minHighland)&&Height(s,b.x,b.z)==Height(s,a.x,a.z))
                {
                    int dx=Math.Abs(b.x-a.x),dz=Math.Abs(b.z-a.z);
                    if(s.authoredLevel?dx+dz==1:dx<=Math.Max(0,t.shrimpRadius)&&dz<=Math.Max(0,t.shrimpRadius))return true;
                }
            return false;
        }
        public static HabitatReport Evaluate(HabitatState s,Animal a,Tuning t)
        {
            HabitatReport r=new HabitatReport();r.protectedByTurtle=IsProtected(s,a.x,a.z,t.minHighland);r.safe=!IsThreatened(s,a,t);
            if(a.carried)
            {
                if(UsesConfirmedPlan(s)&&a.species==Species.Shrimp&&s.backBasketUnlocked&&s.carryingAnimal==a.id)
                {r.suitable=true;r.reason="背篓携虾：两栖条件满足";return r;}
                r.reason="携带中：繁衍暂停并重置";return r;
            }
            if(Blocked(s,a.x,a.z)){r.reason="所在格被障碍占用";return r;}
            if(!r.safe){r.reason="同高度邻近虾驱赶：请隔离或引入乌龟保护";return r;}
            HashSet<Species> friends=new HashSet<Species>(); bool other=false;
            foreach(Animal b in s.animals)
            {
                if(b==a||b.carried)continue;
                if(Math.Abs(b.x-a.x)<=Math.Max(0,t.friendlyRadius)&&Math.Abs(b.z-a.z)<=Math.Max(0,t.friendlyRadius))
                {
                    if(b.species!=a.species)other=true;
                    if(b.species!=Species.Shrimp&&b.species!=a.species&&(a.species!=Species.Starfish||b.species==Species.Crab||b.species==Species.Jellyfish))friends.Add(b.species);
                }
            }
            r.friendCount=friends.Count;
            if(a.species==Species.Jellyfish||a.species==Species.Goldfish)
            {
                var water=Flood(s,a.x,a.z,(x,z)=>IsWater(s,x,z)&&!Blocked(s,x,z));r.waterSize=water.Count;r.areaClosed=true;
                foreach(int p in water)if(p%s.width==0||p%s.width==s.width-1||p/s.width==0||p/s.width==s.depth-1){r.areaClosed=false;break;}
                // The first commission requires enclosure, without the laboratory's minimum pond size.
                bool confirmed=UsesConfirmedPlan(s);
                int need=a.species==Species.Goldfish?(s.authoredLevel&&HabitatLevels.IsFormal(s.levelIndex)?1:Math.Max(1,t.minPond)):(confirmed?1:Math.Max(1,t.minWater));
                if(water.Count<need){r.reason="需要连通水域 ≥ "+need+" 格（当前 "+water.Count+"）";return r;}
                if(a.species==Species.Goldfish&&!r.areaClosed){r.reason="池塘仍与外海相连：用土块封闭缺口";return r;}
                if(confirmed&&a.species==Species.Jellyfish&&r.areaClosed){r.reason="水母需要连通外海的水域：打开通向海域的水路";return r;}
            }
            else if(a.species==Species.Shrimp)
            {
                if(!UsesConfirmedPlan(s))
                {
                    r.waterSize=Flood(s,a.x,a.z,(x,z)=>!Blocked(s,x,z),true).Count;
                    if(r.waterSize<3){r.reason="需要至少 3 格可到达区域";return r;}
                    if(other){r.reason="3×3 邻域存在其他物种：虾需要独处";return r;}
                }
            }
            else if(a.species==Species.Crab)
            {
                if(IsWater(s,a.x,a.z)){r.reason="螃蟹需要陆地";return r;}
                if(UsesConfirmedPlan(s))
                {
                    if(!Shore(s,a.x,a.z)){r.reason="寄居蟹需要与水域相邻的陆地";return r;}
                    bool aquaticExists=false,nearAquatic=false;
                    foreach(Animal b in s.animals)
                    {
                        if(b.species!=Species.Goldfish&&b.species!=Species.Jellyfish)continue;
                        aquaticExists=true;
                        if(!b.carried&&IsWater(s,b.x,b.z)&&!Blocked(s,b.x,b.z)&&Math.Abs(b.x-a.x)<=1&&Math.Abs(b.z-a.z)<=1)nearAquatic=true;
                    }
                    if(aquaticExists&&!nearAquatic){r.reason="3×3 内需要水中的金鱼或水母陪伴";return r;}
                }
                else if(friends.Count<2){r.reason="3×3 内需要 2 种其他友好物种（当前 "+friends.Count+"）";return r;}
            }
            else if(a.species==Species.Turtle)
            {
                if(!TurtleHome(s,a,t.minHighland)){r.reason="高地家园失效：中心 3×3 内需 ≥ "+Math.Max(1,t.minHighland)+" 格连通高地";return r;}
            }
            else if(a.species==Species.Starfish)
            {
                r.waterSize=Flood(s,a.x,a.z,(x,z)=>Shore(s,x,z),true).Count;
                if(r.waterSize<2){r.reason="需要至少 2 格连通岸边陆地";return r;}
                if(friends.Count<1){r.reason="3×3 内需要螃蟹或水母陪伴";return r;}
            }
            r.suitable=true;r.reason=r.protectedByTurtle?"环境适宜 · 乌龟保护中":"环境适宜 · 持续计时繁衍";return r;
        }
        public static bool TryShedCrabShell(HabitatState s,Animal crab,Tuning tuning)
        {
            if(!UsesConfirmedPlan(s)||crab==null||crab.species!=Species.Crab||crab.shellShed
                ||!s.animals.Contains(crab)||!Evaluate(s,crab,tuning??new Tuning()).suitable)return false;
            Tile tile=TileAt(s,crab.x,crab.z);
            // Preserve authored machinery. The one shell waits until this crab has a free support.
            if(!CanReceive(tile)||tile.prop==PropKind.Shell)return false;
            PutProp(tile,PropKind.Shell);crab.shellShed=true;
            Log(s,"寄居蟹在适宜环境中出壳了；靠近它的贝壳，空手按 E 拾取。");return true;
        }
        public static void Log(HabitatState s,string text)
        { s.message=text;s.events.Add(text);if(s.events.Count>60)s.events.RemoveAt(0); }
        static bool Near(HabitatState s,int x,int z) { return Inside(s,x,z)&&Math.Abs(s.playerX-x)+Math.Abs(s.playerZ-z)<=1; }
        static bool EmptyHand(HabitatState s) { return s.carryingAnimal<0&&s.carryingProp==PropKind.None; }
        static PropKind Under(Tile t) { return t.coveredProp; }
        static void RemoveProp(Tile t) { t.prop=t.coveredProp;t.coveredProp=PropKind.None; }
        static void PutProp(Tile t,PropKind p) { t.coveredProp=t.prop;t.prop=p; }
        static bool CanReceive(Tile q) { return q!=null&&(q.prop==PropKind.None||q.prop==PropKind.Road||q.prop==PropKind.Tide||q.prop==PropKind.Shell); }
        public static bool IsBox(PropKind kind)=>kind==PropKind.Crate||kind==PropKind.Float;
        static bool CanReceiveBox(Tile q,PropKind box)=>CanReceive(q)||(box!=PropKind.Float&&q!=null&&q.prop==PropKind.Float&&q.coveredProp==PropKind.None&&!q.trap);
        static int BoxBase(HabitatState s,Tile q)=>q.coveredProp==PropKind.Float?Math.Max(q.height,s.waterLevel+1):q.prop==PropKind.Float?s.waterLevel:q.height;
        public static bool PushBox(HabitatState s,int x,int z)
        {
            int dx=x-s.playerX,dz=z-s.playerZ;var box=TileAt(s,x,z);
            if(Math.Abs(dx)+Math.Abs(dz)!=1||box==null||!IsBox(box.prop)||!EmptyHand(s))
            {Log(s,"空手靠近单个箱子后推拉，箱子不能抱起。");return false;}
            int nx=x+dx,nz=z+dz;var dest=TileAt(s,nx,nz);
            int source=BoxBase(s,box),standing=Height(s,s.playerX,s.playerZ);
            int landing=dest==null?int.MaxValue:dest.prop==PropKind.Float?Height(s,nx,nz):dest.height<=s.waterLevel?s.waterLevel:dest.height;
            if(!CanReceiveBox(dest,box.prop)||Occupied(s,x,z)||Occupied(s,nx,nz)||box.trap||dest.trap||source>standing||landing>source+(box.prop==PropKind.Float?1:0))
            {Log(s,"只能推动一个箱子；前方须是空位或空浮箱支撑，不能连推或推上高地。");return false;}
            var preview=Clone(s);var origin=TileAt(preview,x,z);var target=TileAt(preview,nx,nz);PropKind moved=origin.prop;
            if(moved==PropKind.Crate&&target.height<=s.waterLevel&&target.prop!=PropKind.Tide&&target.prop!=PropKind.Float)moved=PropKind.Float;
            else if(moved==PropKind.Float&&target.height>s.waterLevel)moved=PropKind.Crate;
            RemoveProp(origin);PutProp(target,moved);UpdateTide(preview);
            if(IsWater(preview,s.playerX,s.playerZ)) {Log(s,"推动后潮汐会淹没脚下，不能在这里推箱。");return false;}
            RemoveProp(box);PutProp(dest,moved);s.facingX=dx;s.facingZ=dz;UpdateTide(s);
            Log(s,dest.coveredProp==PropKind.Float?"箱子已推上浮箱支撑，可站在浮箱上继续推向水面。":"已推动单个箱子一格。");return true;
        }
        static void UpdateTide(HabitatState s)
        {
            bool pressed=false;foreach(Tile q in s.tiles)if(q.coveredProp==PropKind.Tide&&(q.prop==PropKind.Crate||q.prop==PropKind.Float)){pressed=true;break;}
            if(pressed)s.tideEverActivated=true;
            if(pressed==s.tideActive)return;
            s.tideActive=pressed;s.waterLevel=s.tideBaseLevel+(pressed?1:0);
            foreach(Tile q in s.tiles)
            {
                if(q.prop==PropKind.Crate&&q.height<=s.waterLevel&&q.coveredProp!=PropKind.Tide&&q.coveredProp!=PropKind.Float)q.prop=PropKind.Float;
                else if(q.prop==PropKind.Float&&q.height>s.waterLevel)q.prop=PropKind.Crate;
            }
            Log(s,pressed?"潮汐引擎启动：全岛海平面升高 1 层":"潮汐引擎释放：海平面恢复");
        }
        static void SyncCarried(HabitatState s)
        { if(s.carryingAnimal<0)return;foreach(Animal a in s.animals)if(a.id==s.carryingAnimal){a.x=s.playerX;a.z=s.playerZ;return;} }
        public static bool MovePlayer(HabitatState s,int dx,int dz,bool climb=false,Tuning tuning=null)
        {
            if(Math.Abs(dx)+Math.Abs(dz)!=1)return false;s.facingX=dx;s.facingZ=dz;
            int x=s.playerX+dx,z=s.playerZ+dz;Tile q=TileAt(s,x,z);bool pushedAnything=false;
            if(q==null||Blocked(s,x,z)){Log(s,"前方无法通行");return false;}
            if(IsWater(s,x,z)&&TileAt(s,s.playerX,s.playerZ)?.prop==PropKind.Raft)return RideRaft(s,dx,dz,tuning);
            bool climbable=q.prop==PropKind.Crate;
            if(climbable&&!climb)
            {
                if(!PushBox(s,x,z))return false;pushedAnything=true;
            }
            int heightDifference=Height(s,x,z)-Height(s,s.playerX,s.playerZ);
            bool mayClimb=climb&&q.prop==PropKind.ClimbBlock&&heightDifference>0&&heightDifference<=2;
            // Ordinary surfaces never provide an upward step, even a one-layer rise.
            // Descending onto a dry, unblocked support is allowed and animated by the view.
            if(heightDifference>0&&!mayClimb){Log(s,"不能登高：只有按 Shift 才能攀上高差两层以内的攀爬方块");return pushedAnything;}
            if(IsWater(s,x,z)){Log(s,pushedAnything?"机关已触发，前格被潮水淹没；请使用浮箱或土块铺路":"不能直接进入水域：使用浮箱或土块铺路");return pushedAnything;}
            int previousPlayerX=s.playerX,previousPlayerZ=s.playerZ;
            s.playerX=x;s.playerZ=z;SyncCarried(s);
            // The new island layouts explicitly ask the player to carry shrimp toward wildlife.
            // This belongs to the successful movement candidate, so cancelled animations never scare animals.
            if(s.authoredLevel&&(UsesConfirmedPlan(s)||s.levelIndex==9)&&s.carryingAnimal>=0)
            {
                Animal held=s.animals.Find(a=>a.id==s.carryingAnimal&&a.species==Species.Shrimp);
                if(held!=null)ScareAfterShrimpStep(s,held,previousPlayerX,previousPlayerZ,tuning??new Tuning(),true);
            }
            return true;
        }
        /// <summary>Step backwards by (dx,dz), pulling the opposite adjacent block into the player's old cell.</summary>
        public static bool PullPlayer(HabitatState s,int dx,int dz)
        {
            if(Math.Abs(dx)+Math.Abs(dz)!=1){Log(s,"后拉需要选择四向相邻的一格退路");return false;}
            if(!EmptyHand(s)){Log(s,"后拉需要空手：请先放下手中的动物或道具");return false;}
            int fromX=s.playerX,fromZ=s.playerZ,boxX=fromX-dx,boxZ=fromZ-dz,toX=fromX+dx,toZ=fromZ+dz;
            Tile origin=TileAt(s,fromX,fromZ),box=TileAt(s,boxX,boxZ),retreat=TileAt(s,toX,toZ);
            if(box==null||(box.prop!=PropKind.Crate&&box.prop!=PropKind.Float))
            {Log(s,"后拉只支持身前相邻的木箱或浮箱；台阶固定，竹排需站上去搭乘。");return false;}
            if(!CanReceiveBox(origin,box.prop)||retreat==null||Blocked(s,toX,toZ)||retreat.prop==PropKind.Crate||retreat.prop==PropKind.ClimbBlock)
            {Log(s,"无法后拉：原站位须能容纳箱子，身后须有不被箱子或障碍占用的退路");return false;}
            if(Occupied(s,boxX,boxZ)||Occupied(s,fromX,fromZ)||Occupied(s,toX,toZ))
            {Log(s,"无法后拉：箱子、原站位或退路上有动物，请先让开");return false;}
            int standingHeight=Height(s,fromX,fromZ);
            int boxBase=BoxBase(s,box);
            // A floating box can be dragged onto the adjacent one-layer bank; solid blocks
            // only slide on level ground or downhill and cannot be lifted uphill by pulling.
            int highestLanding=boxBase+(box.prop==PropKind.Float?1:0);
            int originBase=origin.prop==PropKind.Float?standingHeight:origin.height;
            if(Math.Abs(boxBase-standingHeight)>1||originBase>highestLanding)
            {Log(s,"无法后拉：箱子不能被拉上高地，浮箱最多拖上一层岸地");return false;}

            // Check the complete post-move tide before committing. Releasing or pressing a
            // plate may change the retreat tile or a floating support under the player.
            HabitatState preview=Clone(s);
            Tile previewOrigin=TileAt(preview,fromX,fromZ);
            RemoveProp(TileAt(preview,boxX,boxZ));PutProp(previewOrigin,box.prop);UpdateTide(preview);
            PropKind pulled=box.prop;
            if(pulled==PropKind.Crate&&origin.height<=preview.waterLevel&&origin.prop!=PropKind.Tide&&origin.prop!=PropKind.Float)pulled=PropKind.Float;
            else if(pulled==PropKind.Float&&origin.height>preview.waterLevel)pulled=PropKind.Crate;
            previewOrigin.prop=pulled;
            if(IsWater(preview,toX,toZ)||Height(preview,toX,toZ)>standingHeight)
            {Log(s,"无法后拉：退路必须同高或更低，潮汐变化后也不能被淹没");return false;}

            PropKind originalKind=box.prop;
            RemoveProp(box);PutProp(origin,pulled);
            s.playerX=toX;s.playerZ=toZ;s.facingX=-dx;s.facingZ=-dz;
            UpdateTide(s);
            Log(s,originalKind==PropKind.Float&&pulled==PropKind.Crate?"浮箱已拖上岸，恢复为木箱":
                originalKind==PropKind.Crate&&pulled==PropKind.Float?"木箱已后拉入水，成为浮箱":
                pulled==PropKind.Float?"已后拉浮箱一格":"已后拉木箱一格");
            return true;
        }
        /// <summary>Identifies waterborne or lower-ground boxes; adjacency and clearance are checked separately.</summary>
        public static bool IsRecoveryTarget(HabitatState s,int x,int z)
        {
            if(s==null)return false;
            Tile box=TileAt(s,x,z);
            return box!=null&&(box.prop==PropKind.Crate||box.prop==PropKind.Float)&&
                (box.prop==PropKind.Float||BoxBase(s,box)<Height(s,s.playerX,s.playerZ));
        }
        // Validate the full post-recovery tide on a clone. Queries do not change the
        // source state, including its message, event history, facing or random seed.
        static bool ValidateRecovery(HabitatState s,int x,int z,out PropKind recovered,out string reason)
        {
            recovered=PropKind.None;reason="";
            if(s==null){reason="没有可打捞的关卡";return false;}
            if(s.onHarbor){reason="请回到岛屿岸格再打捞方块";return false;}
            if(Math.Abs(s.playerX-x)+Math.Abs(s.playerZ-z)!=1||!IsRecoveryTarget(s,x,z))
            {reason="打捞只适用于四向相邻的水中或低处方块";return false;}
            if(!EmptyHand(s)){reason="打捞需要空手，请先放下手中的物品";return false;}
            int dx=x-s.playerX,dz=z-s.playerZ,toX=s.playerX-dx,toZ=s.playerZ-dz;
            Tile box=TileAt(s,x,z),origin=TileAt(s,s.playerX,s.playerZ),retreat=TileAt(s,toX,toZ);
            if(origin==null||origin.prop!=PropKind.None||origin.coveredProp!=PropKind.None||origin.trap||IsWater(s,s.playerX,s.playerZ))
            {reason="打捞后方块要放在原站位：请站在没有道具或虾笼的干燥空地上";return false;}
            if(retreat==null||Blocked(s,toX,toZ)||retreat.prop==PropKind.Crate||retreat.prop==PropKind.ClimbBlock||retreat.trap)
            {reason="无法打捞：身后需要一格可站立且没有箱子、障碍或虾笼的退路";return false;}
            if(box.trap||Occupied(s,x,z)||Occupied(s,s.playerX,s.playerZ)||Occupied(s,toX,toZ))
            {reason="无法打捞：方块、原站位和退路不能被动物或虾笼占用";return false;}
            recovered=box.prop==PropKind.Float?PropKind.Crate:box.prop;
            HabitatState preview=Clone(s);
            RemoveProp(TileAt(preview,x,z));PutProp(TileAt(preview,s.playerX,s.playerZ),recovered);UpdateTide(preview);
            if(origin.height<=preview.waterLevel||IsWater(preview,toX,toZ)||Height(preview,toX,toZ)>Height(s,s.playerX,s.playerZ))
            {reason="无法打捞：原站位和身后必须保持干燥，退路不能高于当前位置";return false;}
            return true;
        }
        public static bool CanRecoverBox(HabitatState s,int x,int z)
        { return ValidateRecovery(s,x,z,out _,out _); }
        /// <summary>Recover onto the old standing cell and retreat one cell, with no carried box.</summary>
        public static bool RecoverBox(HabitatState s,int x,int z)
        {
            if(!ValidateRecovery(s,x,z,out PropKind recovered,out string reason))
            {if(s!=null)Log(s,reason);return false;}
            int fromX=s.playerX,fromZ=s.playerZ,dx=x-fromX,dz=z-fromZ;
            Tile box=TileAt(s,x,z),origin=TileAt(s,fromX,fromZ);
            bool wasFloat=box.prop==PropKind.Float;
            int rotation=box.rotation;
            RemoveProp(box);PutProp(origin,recovered);origin.rotation=rotation;
            s.playerX=fromX-dx;s.playerZ=fromZ-dz;s.facingX=dx;s.facingZ=dz;
            UpdateTide(s);
            Log(s,wasFloat?"浮箱已捞上岸并恢复木箱：方块放在原站位，你已后退一格":
                "方块已从低处取回并放在原站位，你已后退一格");
            return true;
        }
        public static bool Interact(HabitatState s,int x,int z)
        {
            if(!Near(s,x,z)){Log(s,"请走到目标相邻格再交互");return false;}Tile q=TileAt(s,x,z);
            if(s.carryingAnimal>=0)
            {
                Animal a=s.animals.Find(v=>v.id==s.carryingAnimal); if(a==null){s.carryingAnimal=-1;return false;}
                bool water=IsWater(s,x,z);
                if(Occupied(s,x,z)||Blocked(s,x,z)||Math.Abs(Height(s,x,z)-Height(s,s.playerX,s.playerZ))>1||(a.species==Species.Jellyfish&&!water)||(a.species==Species.Turtle&&water))
                {Log(s,"无法放下：需要空格、合适地形及一层以内高度");return false;}
                a.carried=false;a.x=x;a.z=z;a.progress=0;a.moveClock=0;a.roamAnchored=false;
                if(a.species==Species.Turtle){a.homeX=x;a.homeZ=z;}s.carryingAnimal=-1;Log(s,"放下"+Name(a.species));return true;
            }
            if(s.carryingProp!=PropKind.None)
            {
                if(s.carryingProp==PropKind.Climb)return PlaceSoil(s,x,z);
                // A box lands at the current water surface, not the submerged seabed.
                // Raised-tide floating supports are one level above that surface.
                int landingHeight=IsWater(s,x,z)?s.waterLevel:Height(s,x,z);
                if(!CanReceive(q)||Occupied(s,x,z)||x==s.playerX&&z==s.playerZ||Math.Abs(landingHeight-Height(s,s.playerX,s.playerZ))>1)
                {Log(s,"此处不能放置道具");return false;}
                PropKind p=s.carryingProp;if(p==PropKind.Crate&&IsWater(s,x,z)&&q.prop!=PropKind.Tide)p=PropKind.Float;
                if(p==PropKind.Float&&!IsWater(s,x,z)){Log(s,"浮箱只能放入水域");return false;}
                PutProp(q,p);s.carryingProp=PropKind.None;UpdateTide(s);Log(s,"已放置道具");return true;
            }
            if(q.prop==PropKind.ClimbBlock){Log(s,"这是固定台阶：按 Shift + 方向攀爬，不可搬运、推拉或打捞。");return false;}
            if(q.prop==PropKind.Raft){Log(s,"走上竹排后，沿水纹方向按方向键往返搭乘；风箱可改变航向。");return false;}
            if(IsBox(q.prop))return PushBox(s,x,z);
            // A shed shell shares the crab's square; pick it up before rejecting crab carrying.
            if(q.prop==PropKind.Shell)
            { RemoveProp(q);s.shells++;Log(s,"拾取贝壳，已收集 "+s.shells+" 枚。");return true; }
            Animal found=s.animals.Find(a=>!a.carried&&a.x==x&&a.z==z);
            if(found!=null)
            {
                if(UsesConfirmedPlan(s)&&found.species==Species.Shrimp&&!s.backBasketUnlocked)
                {Log(s,"先取得第二关宝箱里的背篓，才能捕获并携带红虾。");return false;}
                if(found.species==Species.Crab||found.species==Species.Starfish){Log(s,Name(found.species)+"不能搬起，需通过环境引导");return false;}
                if(found.species==Species.Goldfish){Log(s,UsesConfirmedPlan(s)?"金鱼不能搬起：请围住水域，满足自身繁衍条件后再用鱼竿捕捞。":"小金鱼不能搬起，请用鱼竿引导");return false;}
                if(Math.Abs(Height(s,x,z)-Height(s,s.playerX,s.playerZ))>1){Log(s,"目标高度超出搬运范围");return false;}
                found.carried=true;found.progress=0;s.carryingAnimal=found.id;SyncCarried(s);Log(s,"已搬起"+Name(found.species)+"，靠近空格再次交互放下");return true;
            }
            if(q.prop==PropKind.Chest)
            {
                RemoveProp(q);s.openedChests++;
                if(q.chestReward==ChestReward.FishingRod)
                {
                    s.rod=true;s.rodFromChest=true;
                    if(HabitatChapterRules.IsMainIsland(s)){s.specialRod=true;Log(s,"打开宝箱：获得特制鱼竿，可以捕捞围住的琉璃鱼");}
                    else Log(s,"打开宝箱：获得钓鱼竿");
                }
                else if(q.chestReward==ChestReward.BackBasket)
                {s.backBasketUnlocked=true;Log(s,"取得背篓，探索记录已保存。靠近红虾按 1、E 抱起，再到空格放下。");}
                else if(q.chestReward==ChestReward.Unspecified)Log(s,"宝箱已打开。");
                else{s.coins+=20;Log(s,"打开宝箱：获得 20 贝币");}
                return true;
            }
            if(q.prop==PropKind.Crate||q.prop==PropKind.Float||q.prop==PropKind.Fan||q.prop==PropKind.Road||q.prop==PropKind.Tide||q.prop==PropKind.Climb)
            {
                if(x==s.playerX&&z==s.playerZ){Log(s,"请先离开脚下道具");return false;}
                s.carryingProp=q.prop;RemoveProp(q);UpdateTide(s);Log(s,"已拿起道具，手中最多一件");return true;
            }
            Log(s,"这里没有可交互物品");return false;
        }
        public static bool PlaceSoil(HabitatState s,int x,int z)
        {
            if(!Inside(s,x,z)||Math.Abs(s.playerX-x)>1||Math.Abs(s.playerZ-z)>1||s.carryingProp!=PropKind.Climb||s.carryingAnimal>=0){Log(s,"请先在码头领取一块土，携带至周围 3×3 目标格");return false;}
            Tile q=TileAt(s,x,z);
            if(q.prop!=PropKind.None&&q.prop!=PropKind.Road||Occupied(s,x,z)||q.height>=3||q.height+1>Height(s,s.playerX,s.playerZ)+1)
            {Log(s,"不能填土：格子须空闲，高度不超过 3 且放置不高于自身一层");return false;}
            q.height++;s.carryingProp=PropKind.None;Log(s,"土块已填入：该格升高 1 层");return true;
        }
        public static bool Rotate(HabitatState s,int x,int z)
        {
            Tile q=TileAt(s,x,z);if(!Near(s,x,z)||q==null||(q.prop!=PropKind.Road&&q.prop!=PropKind.Fan)){Log(s,"靠近道路或风扇后旋转");return false;}
            q.rotation=(q.rotation+1)%4;Log(s,"方向已旋转 90°");return true;
        }
        // Tools 5/6/7 are consumables. Preview callers use a clone; a failed action never spends inventory.
        public static bool UseUtility(HabitatState s,int tool,int x,int z,int dx,int dz)
        {
            if(s.onHarbor||!EmptyHand(s)){Log(s,"请回到岛上并先放下手持物");return false;}
            Tile q=TileAt(s,x,z);
            if(tool==5)
            {
                if(s.shovels<=0){Log(s,"没有一次性铲子");return false;}
                if(q==null||x!=s.playerX+s.facingX||z!=s.playerZ+s.facingZ
                    ||q.height!=Height(s,s.playerX,s.playerZ)+1||q.height<=s.waterLevel
                    ||(q.prop!=PropKind.None&&q.prop!=PropKind.Road)||q.trap||Occupied(s,x,z))
                {Log(s,"只能削低正前方高自己一层的空土格");return false;}
                q.height--;s.shovels--;Log(s,"削低一层土，消耗一把铲子");return true;
            }
            if(tool!=6&&tool!=7){Log(s,"未知道具");return false;}
            if((tool==6?s.magnets:s.launchers)<=0){Log(s,tool==6?"没有磁铁":"没有发射器");return false;}
            if(q==null||(q.prop!=PropKind.Crate&&q.prop!=PropKind.Float)||x==s.playerX&&z==s.playerZ||Occupied(s,x,z))
            {Log(s,"请选择空载木箱或浮箱");return false;}
            if(tool==6)
            {
                if(Math.Abs(x-s.playerX)>1||Math.Abs(z-s.playerZ)>1||Math.Abs(dx)+Math.Abs(dz)!=1)
                {Log(s,"选择周围3×3内箱子，再指定一个四向方向");return false;}
            }
            else
            {
                dx=s.facingX;dz=s.facingZ;
                if(Math.Abs(dx)+Math.Abs(dz)!=1||x!=s.playerX+dx||z!=s.playerZ+dz)
                {Log(s,"发射器只能发射正前方的箱子");return false;}
            }
            int endX=x,endZ=z,baseHeight=BoxBase(s,q);
            for(int step=1;step<=(tool==6?1:2);step++)
            {
                int tx=x+dx*step,tz=z+dz*step;Tile dest=TileAt(s,tx,tz);
                int landingHeight=dest==null?int.MaxValue:dest.prop==PropKind.Float?Height(s,tx,tz):dest.height;
                if(!CanReceiveBox(dest,q.prop)||landingHeight>baseHeight||dest.trap||Occupied(s,tx,tz)||tx==s.playerX&&tz==s.playerZ)break;
                endX=tx;endZ=tz;
            }
            if(endX==x&&endZ==z){Log(s,"方向被阻挡，没有消耗道具");return false;}
            Tile landing=TileAt(s,endX,endZ);
            PropKind prop=landing.height<=s.waterLevel&&landing.prop!=PropKind.Tide&&landing.prop!=PropKind.Float?PropKind.Float:PropKind.Crate;
            int rotation=q.rotation;RemoveProp(q);PutProp(landing,prop);landing.rotation=rotation;
            if(tool==6)s.magnets--;else s.launchers--;
            UpdateTide(s);Log(s,(tool==6?"磁铁移动":"发射箱子")+"至 "+(endX+1)+","+(endZ+1));return true;
        }
        public static bool StoreHeld(HabitatState s)
        {
            if(s.carryingAnimal>=0||s.carryingProp==PropKind.None){Log(s,"只能收纳手持道具，动物请放回地图");return false;}
            if(s.carryingProp==PropKind.Crate||s.carryingProp==PropKind.Float||s.carryingProp==PropKind.ClimbBlock)
            {Log(s,"箱子不能收入随身背包，请放置或在船旁装货");return false;}
            if(s.bagProps==null)s.bagProps=new List<PropKind>();
            s.bagProps.Add(s.carryingProp);s.carryingProp=PropKind.None;Log(s,"道具已收入背包");return true;
        }
        public static bool TakeBagProp(HabitatState s,int index)
        {
            if(!EmptyHand(s)||s.bagProps==null||index<0||index>=s.bagProps.Count){Log(s,"请先空出双手再取物");return false;}
            if(IsBox(s.bagProps[index])||s.bagProps[index]==PropKind.ClimbBlock||s.bagProps[index]==PropKind.Raft){Log(s,"箱子不能手持，请使用船舱装卸。旧背包中的箱子仍保留。");return false;}
            s.carryingProp=s.bagProps[index];s.bagProps.RemoveAt(index);Log(s,"已从背包取出道具");return true;
        }
        public static bool TransferBoatCrate(HabitatState s,bool unload)
        {
            // Controller additionally checks the actual boat approach on the continuous harbor.
            if(!AtDock(s)){Log(s,"请先回到船旁");return false;}
            if(s.onHarbor){Log(s,"请先沿木桥走到岛屿岸格，面朝落点装卸箱子。");return false;}
            int x=s.playerX+s.facingX,z=s.playerZ+s.facingZ;var tile=TileAt(s,x,z);
            if(unload)
            {
                if(!EmptyHand(s)||s.boatCrates<=0){Log(s,"船舱没有木箱，或双手仍被占用");return false;}
                int landing=tile==null?int.MaxValue:tile.prop==PropKind.Float?Height(s,x,z):Math.Max(tile.height,s.waterLevel);
                if(!CanReceiveBox(tile,PropKind.Crate)||Occupied(s,x,z)||tile.trap||Math.Abs(landing-Height(s,s.playerX,s.playerZ))>1)
                {Log(s,"面前没有可卸货的空位，转向空地、水面或空浮箱后再卸箱。");return false;}
                PutProp(tile,tile.height<=s.waterLevel&&tile.prop!=PropKind.Float&&tile.prop!=PropKind.Tide?PropKind.Float:PropKind.Crate);s.boatCrates--;
            }
            else
            {
                if(s.boatCrates>=3||s.carryingAnimal>=0){Log(s,"请空出双手，船舱最多装三箱。");return false;}
                if(s.carryingProp==PropKind.Crate)s.carryingProp=PropKind.None; // Old save compatibility.
                else if(EmptyHand(s)&&tile!=null&&!tile.trap&&(tile.prop==PropKind.Crate||tile.prop==PropKind.Float)&&!Occupied(s,x,z)&&Math.Abs(BoxBase(s,tile)-Height(s,s.playerX,s.playerZ))<=1)RemoveProp(tile);
                else {Log(s,"请在岸边面向相邻的木箱或浮箱装船。");return false;}
                s.boatCrates++;
            }
            UpdateTide(s);Log(s,unload?"已将一只箱子卸到面前，可推拉铺路。":"已将面前箱子装船。");return true;
        }
        public static bool Lure(HabitatState s,int x,int z)
        {
            if(!s.rod){Log(s,"需要在码头购买鱼竿");return false;}
            int reach=UsesConfirmedPlan(s)?3:4;
            if(!Inside(s,x,z)||Math.Abs(x-s.playerX)+Math.Abs(z-s.playerZ)>reach){Log(s,"鱼竿有效距离为 "+reach+" 格");return false;}
            Animal a=s.animals.Find(v=>!v.carried&&v.x==x&&v.z==z&&(v.species==Species.Goldfish||v.species==Species.Jellyfish));
            if(a==null){Log(s,"请选择水中的金鱼或水母");return false;}
            if(UsesConfirmedPlan(s)&&a.species!=Species.Jellyfish)
            {Log(s,"鱼竿只能牵引水母；金鱼需要围住，满足自身繁衍条件后再捕捞。");return false;}
            int best=-1,dist=Math.Abs(x-s.playerX)+Math.Abs(z-s.playerZ);
            for(int i=0;i<4;i++)if(IsWater(s,x+DX[i],z+DZ[i])&&!Blocked(s,x+DX[i],z+DZ[i])&&!Occupied(s,x+DX[i],z+DZ[i])&&Math.Abs(x+DX[i]-s.playerX)+Math.Abs(z+DZ[i]-s.playerZ)<dist){best=i;dist=Math.Abs(x+DX[i]-s.playerX)+Math.Abs(z+DZ[i]-s.playerZ);}
            if(best<0){Log(s,"没有更靠近你的连通空水格");return false;}a.x+=DX[best];a.z+=DZ[best];a.moveClock=0;a.roamAnchored=false;Log(s,"鱼竿引导"+Name(a.species)+"前进一步");return true;
        }
        public static bool PlaceTrap(HabitatState s,int x,int z)
        {
            Tile q=TileAt(s,x,z);if(!Near(s,x,z)||s.traps<1||q==null||Blocked(s,x,z)||q.trap){Log(s,"需要陷阱库存、相邻空地且不能重复放置");return false;}
            q.trap=true;s.traps--;Log(s,"陷阱已放置：进入的虾会停留，可空手搬走");return true;
        }
        public static bool Fish(HabitatState s,int x,int z,Species? targetSpecies=null,Tuning tuning=null)
        {
            Animal a=s.animals.Find(v=>!v.carried&&v.x==x&&v.z==z&&(!targetSpecies.HasValue||v.species==targetSpecies.Value));
            if(!CanHarvest(s,a,tuning??new Tuning(),out string invalid)){Log(s,invalid);return false;}
            bool glassFish=HabitatChapterRules.IsMainIsland(s)&&a.species==Species.Goldfish&&!s.chapterOneDelivered;
            bool firstFormalCatch=UsesConfirmedPlan(s)&&!a.catchClaimed;
            int n=firstFormalCatch?1:a.offspring;
            if(firstFormalCatch)a.catchClaimed=true;else a.offspring=0;
            if(glassFish)
            {
                s.glassFish+=n;s.glassFishCaught+=n;
                Log(s,"钓到 "+n+" 条琉璃鱼，亲本保留；委托渔获 "+s.glassFish+"/"+HabitatChapterRules.TargetFish);return true;
            }
            if(a.species==Species.Goldfish)s.fish+=n;else if(a.species==Species.Shrimp)s.shrimp+=n;
            else if(UsesConfirmedPlan(s)&&a.species==Species.Jellyfish)s.jellyfish+=n;
            else s.coins+=n*2;
            Log(s,(firstFormalCatch?"捕获 ":"收集 ")+n+" 只"+Name(a.species)+(firstFormalCatch?"":"幼体")+"，亲本保留"+(a.species==Species.Goldfish||a.species==Species.Shrimp||UsesConfirmedPlan(s)&&a.species==Species.Jellyfish?"，回码头出售":"，研究奖励 +"+(n*2)+" 贝币"));return true;
        }
        public static bool CanHarvest(HabitatState s,Animal animal,Tuning tuning,out string reason)
        {
            reason="";
            if(s==null||animal==null||animal.carried||!s.animals.Contains(animal))
            {reason="请选择地图上的可捕捞生物。";return false;}
            int range=UsesConfirmedPlan(s)&&animal.species==Species.Jellyfish?3:4;
            if(!s.rod||Math.Abs(animal.x-s.playerX)+Math.Abs(animal.z-s.playerZ)>range)
            {reason="需携带鱼竿，并站在目标 "+range+" 格以内。";return false;}
            if(UsesConfirmedPlan(s))
            {
                if(animal.species!=Species.Goldfish&&animal.species!=Species.Jellyfish)
                {reason="鱼竿只用于捕捞金鱼或水母；红虾使用背篓，蟹壳靠近后拾取。";return false;}
                if(!Evaluate(s,animal,tuning??new Tuning()).suitable)
                {reason="目标目前不满足繁衍条件，请先恢复适宜的水域环境。";return false;}
                if(animal.catchClaimed&&animal.offspring<=0){reason="本次渔获已经收取；保留亲本，等待下一次繁衍或返岛刷新后再钓。";return false;}
            }
            else if(animal.offspring<=0){reason="目标尚无可收集幼体，亲本保留。";return false;}
            if(HabitatChapterRules.IsMainIsland(s)&&animal.species==Species.Goldfish)
                return HabitatChapterRules.CanCatchGlassFish(s,animal,out reason);
            return true;
        }
        public static int CarriedShrimpCatchCount(HabitatState s)
        {
            if(!CanStoreCarriedShrimp(s))return 0;
            var a=s.animals.Find(v=>v.id==s.carryingAnimal);
            return s.won||!a.catchClaimed?1:a.offspring;
        }
        public static bool CanStoreCarriedShrimp(HabitatState s)
            =>UsesConfirmedPlan(s)&&s.backBasketUnlocked&&s.carryingAnimal>=0
                &&s.animals.Exists(a=>a.id==s.carryingAnimal&&a.carried&&a.species==Species.Shrimp
                    &&(s.won||(!a.catchClaimed||a.offspring>0)&&!Blocked(s,a.x,a.z)));
        public static bool StoreCarriedShrimp(HabitatState s)
        {
            if(!CanStoreCarriedShrimp(s))return false;
            Animal shrimp=s.animals.Find(a=>a.id==s.carryingAnimal&&a.carried&&a.species==Species.Shrimp);
            if(s.won){s.animals.Remove(shrimp);s.shrimp++;}
            else
            {
                int amount=shrimp.catchClaimed?shrimp.offspring:1;
                if(shrimp.catchClaimed)shrimp.offspring=0;else shrimp.catchClaimed=true;
                s.shrimp+=amount;shrimp.carried=false;shrimp.roamAnchored=false;
            }
            s.carryingAnimal=-1;
            Log(s,"已收取适居红虾渔获，可返航出售；未通关时亲本留在岛上继续解谜。");return true;
        }
        public static bool AtDock(HabitatState s) { return Math.Abs(s.playerX-s.dockX)+Math.Abs(s.playerZ-s.dockZ)<=1; }
        public static bool RemotePurchase(HabitatState s,string item)
            => s!=null&&s.authoredLevel&&HabitatLevels.IsFormal(s.levelIndex)
                &&(item=="crate"||item=="float"||item=="fan"||item=="shovel"||item=="magnet"||item=="launcher"||item=="trap"||item=="rod");
        public static bool Shop(HabitatState s,string item)
        {
            item=(item??"").ToLowerInvariant();
            if(!AtDock(s)&&!RemotePurchase(s,item)){Log(s,"请回到码头旁进行交易");return false;}
            if(!HabitatChapterRules.CanBuyItem(s,item,out string lockedReason)){Log(s,lockedReason);return false;}
            if(item=="sell")
            {
                StoreCarriedShrimp(s);
                int income=CatchSaleValue(s);
                if(income==0){Log(s,"没有可出售的鱼、虾、水母或蟹壳");return false;}
                s.deliveredFish+=s.fish;s.deliveredShrimp+=s.shrimp;s.deliveryCount++;s.deliveryIncome+=income;
                s.deliveredShells+=s.shells;
                s.coins+=income;s.fish=0;s.shrimp=0;s.shells=0;s.jellyfish=0;Log(s,"出售渔获与蟹壳：+"+income+" 贝币");return true;
            }
            if(item=="rod"){if(s.rod){Log(s,"已拥有鱼竿");return false;}if(s.coins<10){Log(s,"贝币不足");return false;}s.rod=true;s.coins-=10;Log(s,"购买鱼竿");return true;}
            if(item=="trap"){if(s.coins<3){Log(s,"贝币不足");return false;}s.coins-=3;s.traps++;Log(s,"购买陷阱");return true;}
            if(item=="shovel"||item=="magnet"||item=="launcher")
            {
                int price=ShopPrice(s,item);
                if(s.coins<price){Log(s,"贝币不足");return false;}
                s.coins-=price;
                if(item=="shovel")s.shovels++;else if(item=="magnet")s.magnets++;else s.launchers++;
                Log(s,"购买"+(item=="shovel"?"一次性铲子":item=="magnet"?"磁铁":"发射器")+"，已放入背包");return true;
            }
            if(item=="crate"||item=="float")
            {
                if(s.boatCrates>=3){Log(s,"船舱已满：最多装三只木箱，请先卸货");return false;}
                int price=ShopPrice(s,item);
                if(s.coins<price){Log(s,"贝币不足");return false;}
                s.coins-=price;s.boatCrates++;Log(s,"木箱已装船（"+s.boatCrates+"/3），到船旁打开背包逐件取出");return true;
            }
            if(item=="fan"&&HabitatChapterRules.IsCampaignShop(s))
            {
                int price=ShopPrice(s,item);if(s.coins<price){Log(s,"贝币不足");return false;}
                s.coins-=price;if(s.bagProps==null)s.bagProps=new List<PropKind>();s.bagProps.Add(PropKind.Fan);
                Log(s,"购买吹风机，已放入背包。");return true;
            }
            if(!EmptyHand(s)){Log(s,"手中只能携带一件，请先放下当前物品");return false;}
            PropKind p;int cost;
            switch(item){case "soil":p=PropKind.Climb;break;case "crate":p=PropKind.Crate;break;case "float":p=PropKind.Float;break;case "fan":p=PropKind.Fan;break;case "tide":p=PropKind.Tide;break;case "road":p=PropKind.Road;break;default:Log(s,"未知商品");return false;}
            cost=ShopPrice(s,item);
            if(s.coins<cost){Log(s,"贝币不足");return false;}s.coins-=cost;if(item=="soil"&&s.cargo>0)s.cargo--;s.carryingProp=p;Log(s,item=="soil"?"从船上领取一块土，请携带至岛内填放":"购买并拿起道具");return true;
        }
        static int Random(HabitatState s,int count)
        { unchecked {uint x=(uint)s.seed;x^=x<<13;x^=x>>17;x^=x<<5;s.seed=(int)x;return (int)(x%(uint)Math.Max(1,count));} }
        static int ThreatDistance(HabitatState s,int x,int z)
        { int d=999;foreach(Animal b in s.animals)if(!b.carried&&b.species==Species.Shrimp&&!IsProtected(s,b.x,b.z)&&Height(s,b.x,b.z)==Height(s,x,z))d=Math.Min(d,Math.Abs(b.x-x)+Math.Abs(b.z-z));return d; }
        static bool AnimalCanStep(HabitatState s,Animal a,int x,int z)
        {
            if(Blocked(s,x,z)||Occupied(s,x,z,a)||Math.Abs(Height(s,x,z)-Height(s,a.x,a.z))>1)return false;
            switch(a.species)
            {
                case Species.Jellyfish:case Species.Goldfish:return IsWater(s,x,z);
                case Species.Shrimp:return true;
                case Species.Crab:return !IsWater(s,x,z);
                case Species.Turtle:return !IsWater(s,x,z)&&Math.Abs(x-a.homeX)<=1&&Math.Abs(z-a.homeZ)<=1;
                case Species.Starfish:return Shore(s,x,z);
            }return false;
        }
        static void MoveAnimal(HabitatState s,Animal a,Tuning t)
        {
            if(UsesConfirmedPlan(s))
            {
                if(!s.won||a.carried)return;
                MoveSettledAnimal(s,a,t);return;
            }
            if(a.carried)return;Tile q=TileAt(s,a.x,a.z);if(q.trap&&a.species==Species.Shrimp)return;
            // Authored levels use event-driven, directional fear (FigJam 92:1921).
            // A stationary neighbor must never repeatedly drive the crab on its own turn.
            bool scared=!s.authoredLevel&&IsThreatened(s,a,t);if(a.species==Species.Crab&&!scared)return;
            List<int> choices=new List<int>();for(int i=0;i<4;i++)if(AnimalCanStep(s,a,a.x+DX[i],a.z+DZ[i]))choices.Add(i);if(choices.Count==0)return;
            int direction=choices[Random(s,choices.Count)];
            if(!scared&&a.species==Species.Jellyfish)
            {
                int current=StarDistance(s,a.x,a.z),best=current;List<int> closer=new List<int>();
                foreach(int i in choices){int d=StarDistance(s,a.x+DX[i],a.z+DZ[i]);if(d<best){best=d;closer.Clear();}if(d==best)closer.Add(i);}
                if(current<999&&closer.Count==0)return;
                if(closer.Count>0)direction=closer[Random(s,closer.Count)];
            }
            if(scared)
            {
                int best=ThreatDistance(s,a.x,a.z);
                int road=(q.prop==PropKind.Road?q.rotation:-1);
                bool found=false;foreach(int i in choices){int d=ThreatDistance(s,a.x+DX[i],a.z+DZ[i]);if(d>best||(d==best&&i==road)){best=d;direction=i;found=true;}}
                if(!found)return;
            }
            int previousX=a.x,previousZ=a.z;
            a.x+=DX[direction];a.z+=DZ[direction];
            if(s.authoredLevel&&a.species==Species.Shrimp)ScareAfterShrimpStep(s,a,previousX,previousZ,t);
        }
        static void MoveSettledAnimal(HabitatState s,Animal a,Tuning t)
        {
            if(TileAt(s,a.x,a.z).trap&&a.species==Species.Shrimp)return;
            if(!a.roamAnchored){a.roamAnchored=true;a.roamX=a.x;a.roamZ=a.z;}
            // Never wander away from the solved neighborhood or spoil another resident's habitat.
            int ox=a.x,oz=a.z,start=Random(s,4);
            for(int i=0;i<4;i++)
            {
                int d=(start+i)%4,nx=ox+DX[d],nz=oz+DZ[d];
                if(Math.Abs(nx-a.roamX)+Math.Abs(nz-a.roamZ)>1||!AnimalCanStep(s,a,nx,nz))continue;
                if(!s.onHarbor&&nx==s.playerX&&nz==s.playerZ)continue;
                a.x=nx;a.z=nz;bool suitable=true;
                foreach(var other in s.animals)if(!Evaluate(s,other,t).suitable){suitable=false;break;}
                if(suitable)return;
                a.x=ox;a.z=oz;
            }
        }
        static void ScareAfterShrimpStep(HabitatState s,Animal shrimp,int previousX,int previousZ,Tuning t,bool carriedApproach=false)
        {
            if(IsProtected(s,shrimp.x,shrimp.z,t.minHighland))return;
            foreach(Animal other in s.animals)
            {
                if(other==shrimp||other.carried||other.species==Species.Shrimp)continue;
                int dx=other.x-shrimp.x,dz=other.z-shrimp.z;
                if(Math.Abs(dx)+Math.Abs(dz)!=1||Math.Abs(other.x-previousX)+Math.Abs(other.z-previousZ)<=1)continue;
                // A held shrimp can reach the adjacent water from a one-layer bank, matching pickup reach.
                if(Math.Abs(Height(s,other.x,other.z)-Height(s,shrimp.x,shrimp.z))>(carriedApproach?1:0)||IsProtected(s,other.x,other.z,t.minHighland))continue;
                int steps=UsesConfirmedPlan(s)&&other.species==Species.Goldfish?2:1,moved=0;
                for(int step=0;step<steps;step++)
                {
                    int nx=other.x+dx,nz=other.z+dz;
                    if(!AnimalCanStep(s,other,nx,nz))break;
                    other.x=nx;other.z=nz;moved++;
                }
                if(moved==0)continue;
                other.moveClock=0;other.progress=0;other.roamAnchored=false;
                if(other.species==Species.Crab)s.crabMovedByShrimp=true;
                Log(s,Name(other.species)+"被主动靠近的红虾惊吓，朝相反方向退 "+moved+" 格");
            }
        }
        // Logical waterLevel is the highest flooded ground layer (0 at low tide).
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
            // A single input sails the whole unobstructed lane. Candidate state is
            // committed only after the rider and raft arrive at this final cell.
            while(true)
            {
                int tx=nx+dx,tz=nz+dz;Tile next=TileAt(s,tx,tz);
                if(!CanReceive(next)||!IsWater(s,tx,tz)||next.trap||Occupied(s,tx,tz))break;
                nx=tx;nz=tz;dest=next;
            }
            int direction=raft.rotation;RemoveProp(raft);PutProp(dest,PropKind.Raft);dest.rotation=direction;
            s.playerX=nx;s.playerZ=nz;SyncCarried(s);
            if(s.carryingAnimal>=0)
            {
                var shrimp=s.animals.Find(a=>a.id==s.carryingAnimal&&a.species==Species.Shrimp);
                if(shrimp!=null)ScareAfterShrimpStep(s,shrimp,ox,oz,tuning??new Tuning(),true);
            }
            Log(s,"竹排沿航道滑至障碍物前停下；可反向返回。");return true;
        }

        static int StarDistance(HabitatState s,int x,int z)
        {int d=999;foreach(Animal a in s.animals)if(!a.carried&&a.species==Species.Starfish)d=Math.Min(d,Math.Max(Math.Abs(x-a.x),Math.Abs(z-a.z)));return d;}
        static void Fans(HabitatState s)
        {
            HashSet<int> moved=new HashSet<int>();
            for(int z=0;z<s.depth;z++)for(int x=0;x<s.width;x++)
            {
                Tile fan=TileAt(s,x,z);if(fan.prop!=PropKind.Fan)continue;int d=((fan.rotation%4)+4)%4,fx=x+DX[d],fz=z+DZ[d];
                // Wind travels across up to five unobstructed cells to the first floating block.
                for(int distance=1;distance<=5&&Inside(s,fx,fz);distance++,fx+=DX[d],fz+=DZ[d])
                {
                    Tile f=TileAt(s,fx,fz);
                    // Authored bamboo rafts wait for their passenger. Wind redirects the rail,
                    // rather than sending the empty raft away and stranding the player.
                    if(f.prop==PropKind.Raft){f.rotation=d;break;}
                    if(f.prop==PropKind.Float)
                    {
                        int nx=fx+DX[d],nz=fz+DZ[d];Tile dest=TileAt(s,nx,nz);
                        if(!moved.Contains(fz*s.width+fx)&&CanReceive(dest)&&IsWater(s,nx,nz)&&!Occupied(s,nx,nz))
                        {
                            RemoveProp(f);PutProp(dest,PropKind.Float);moved.Add(nz*s.width+nx);
                            if(!s.onHarbor&&s.playerX==fx&&s.playerZ==fz){s.playerX=nx;s.playerZ=nz;SyncCarried(s);}
                            foreach(Animal a in s.animals)if(!a.carried&&a.x==fx&&a.z==fz){a.x=nx;a.z=nz;}
                        }break;
                    }
                    if(f.prop!=PropKind.None&&f.prop!=PropKind.Road&&f.prop!=PropKind.Tide&&f.prop!=PropKind.Shell)break;
                }
            }
        }
        public static void Tick(HabitatState s,Tuning t,float dt)
        {
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            // Substeps keep long frame updates from skipping unsuitable intervals or movement.
            int steps=Math.Max(1,(int)Math.Ceiling(dt/0.1f));float step=dt/steps;
            for(int k=0;k<steps;k++)
            {
                s.elapsed+=step;UpdateTide(s);
                s.movementClock+=step;float move=Math.Max(0.2f,t.moveSeconds);
                if(s.movementClock>=move){s.movementClock-=move;s.tickCount++;Fans(s);}
                // Process authored shrimp first, so an immediate reaction does not depend
                // on the order of species in the level data. A startled animal's clock resets.
                if(s.authoredLevel&&t.animalMovement)foreach(Animal a in s.animals)
                {
                    if(a.carried||a.species!=Species.Shrimp)continue;
                    a.moveClock+=step;
                    if(a.moveClock>=move){a.moveClock-=move;MoveAnimal(s,a,t);}
                }
                foreach(Animal a in s.animals)
                {
                    if(t.animalMovement&&!a.carried&&(!s.authoredLevel||a.species!=Species.Shrimp))
                    {
                        a.moveClock+=step;float every=move*(a.species==Species.Turtle||a.species==Species.Starfish?2:1);
                        if(a.moveClock>=every){a.moveClock-=every;MoveAnimal(s,a,t);}
                    }
                    HabitatReport report=Evaluate(s,a,t);
                    if(!report.suitable){a.progress=0;continue;}
                    // A basket-held shrimp can satisfy the level endpoint, but does not breed in the bag.
                    if(a.carried){a.progress=0;continue;}
                    if(a.species==Species.Crab)TryShedCrabShell(s,a,t);
                    a.progress+=step;float breed=Math.Max(0.5f,t.breedSeconds);
                    while(a.progress+0.00001f>=breed)
                    {
                        a.progress=Math.Max(0,a.progress-breed);a.offspring++;a.spawnedTotal++;
                        if(!s.earnedSpecies.Contains(a.species))s.earnedSpecies.Add(a.species);
                        Log(s,Name(a.species)+"繁衍成功：幼体 +1（亲本保留）");
                    }
                    if(a.species==Species.Shrimp&&TileAt(s,a.x,a.z).trap&&a.offspring>0)
                    {s.shrimp+=a.offspring;Log(s,"陷阱自动收集虾幼体 +"+a.offspring+"，亲本保留，可回码头出售");a.offspring=0;}
                }
                // Authored maps with unspecified task endings intentionally have no win condition.
                bool complete=s.goals!=null&&s.goals.Length>0;
                if(complete)foreach(Species goal in s.goals)
                {bool okay=false;foreach(Animal a in s.animals)if(a.species==goal&&Evaluate(s,a,t).suitable&&s.earnedSpecies.Contains(goal)){okay=true;break;}if(!okay){complete=false;break;}}
                if(complete&&!s.won){s.won=true;Log(s,"关卡目标达成！所需物种环境适宜且已首次繁衍。可继续自由测试。");}
            }
        }
        public static HabitatState Clone(HabitatState s)
        {
            HabitatState n=(HabitatState)s.MemberwiseCloneForRules();return n;
        }
    }
    internal static class HabitatCloneExtensions
    {
        internal static object MemberwiseCloneForRules(this HabitatState s)
        {
            HabitatState n=new HabitatState{width=s.width,depth=s.depth,playerX=s.playerX,playerZ=s.playerZ,facingX=s.facingX,facingZ=s.facingZ,coins=s.coins,cargo=s.cargo,fish=s.fish,shrimp=s.shrimp,carryingAnimal=s.carryingAnimal,carryingProp=s.carryingProp,waterLevel=s.waterLevel,tickCount=s.tickCount,elapsed=s.elapsed,rod=s.rod,traps=s.traps,selectedX=s.selectedX,selectedZ=s.selectedZ,message=s.message,levelIndex=s.levelIndex,levelName=s.levelName,objective=s.objective,won=s.won,dockX=s.dockX,dockZ=s.dockZ,dockRotation=s.dockRotation,seed=s.seed,movementClock=s.movementClock,tideActive=s.tideActive,tideBaseLevel=s.tideBaseLevel,authoredLevel=s.authoredLevel,sourceNodeId=s.sourceNodeId,openedChests=s.openedChests,rodFromChest=s.rodFromChest,tideEverActivated=s.tideEverActivated,crabMovedByShrimp=s.crabMovedByShrimp,backBasketUnlocked=s.backBasketUnlocked,shells=s.shells};
            n.onHarbor=s.onHarbor;n.harborX=s.harborX;n.harborZ=s.harborZ;n.harborFacingX=s.harborFacingX;n.harborFacingZ=s.harborFacingZ;
            n.boatCrates=s.boatCrates;n.shovels=s.shovels;n.magnets=s.magnets;n.launchers=s.launchers;
            n.jellyfish=s.jellyfish;n.deliveredShells=s.deliveredShells;
            n.playSeconds=s.playSeconds;
            n.campaignUnlockedStage=s.campaignUnlockedStage;n.campaignBriefedMask=s.campaignBriefedMask;
            n.seenGuideKeys=s.seenGuideKeys==null?new List<string>():new List<string>(s.seenGuideKeys);
            n.bagProps=s.bagProps==null?new List<PropKind>():new List<PropKind>(s.bagProps);
            n.day=s.day;n.openingCompleted=s.openingCompleted;n.openingCatchGranted=s.openingCatchGranted;
            n.deliveredFish=s.deliveredFish;n.deliveredShrimp=s.deliveredShrimp;n.deliveryCount=s.deliveryCount;n.deliveryIncome=s.deliveryIncome;
            n.tutorialOfferPending=s.tutorialOfferPending;n.tutorialCompleted=s.tutorialCompleted;
            n.tutorialGuideDismissed=s.tutorialGuideDismissed;n.tutorialObservedCrab=s.tutorialObservedCrab;n.tutorialObservedHabitat=s.tutorialObservedHabitat;
            n.chapterOneAccepted=s.chapterOneAccepted;n.chapterOneSailed=s.chapterOneSailed;n.chapterOneDelivered=s.chapterOneDelivered;
            n.firstClearCgSeen=s.firstClearCgSeen;n.chapterOneMorningSeen=s.chapterOneMorningSeen;n.specialRod=s.specialRod;n.glassFish=s.glassFish;n.glassFishCaught=s.glassFishCaught;
            n.tiles=new Tile[s.tiles.Length];for(int i=0;i<s.tiles.Length;i++){Tile q=s.tiles[i];n.tiles[i]=new Tile{height=q.height,prop=q.prop,rotation=q.rotation,trap=q.trap,coveredProp=q.coveredProp,chestReward=q.chestReward};}
            foreach(Animal a in s.animals)n.animals.Add(new Animal{id=a.id,x=a.x,z=a.z,species=a.species,progress=a.progress,offspring=a.offspring,carried=a.carried,homeX=a.homeX,homeZ=a.homeZ,moveClock=a.moveClock,spawnedTotal=a.spawnedTotal,shellShed=a.shellShed,catchClaimed=a.catchClaimed,roamAnchored=a.roamAnchored,roamX=a.roamX,roamZ=a.roamZ});
            n.events=new List<string>(s.events);n.goals=s.goals==null?new Species[0]:(Species[])s.goals.Clone();n.earnedSpecies=new List<Species>(s.earnedSpecies);return n;
        }
    }
}
