using System;
using System.Collections.Generic;

namespace HabitatIsland
{
    public static class HabitatModelChecks
    {
        static int assertions;
        static void Check(bool condition,string label){assertions++;if(!condition)throw new InvalidOperationException("Habitat check failed: "+label);}
        static HabitatState Flat(int height=1)
        {HabitatState s=new HabitatState{width=9,depth=9,playerX=1,playerZ=1,dockX=1,dockZ=1};s.tiles=new Tile[81];for(int i=0;i<81;i++)s.tiles[i]=new Tile{height=height};return s;}
        static Animal A(HabitatState s,Species sp,int x,int z)
        {Animal a=new Animal{id=s.animals.Count+1,species=sp,x=x,z=z,homeX=x,homeZ=z};s.animals.Add(a);return a;}
        static Tile Q(HabitatState s,int x,int z){return HabitatRules.TileAt(s,x,z);}
        public static string Run()
        {
            assertions=0;Tuning t=new Tuning{animalMovement=false,breedSeconds=1};
            HabitatState s=Flat(0);Animal j=A(s,Species.Jellyfish,3,3),shr=A(s,Species.Shrimp,4,3);
            Check(HabitatRules.IsThreatened(s,j,t),"same-height shrimp threatens jellyfish");
            Q(s,4,3).height=1;Check(!HabitatRules.IsThreatened(s,j,t),"different-height shrimp does not threaten");
            Q(s,4,3).height=0;Animal turtle=A(s,Species.Turtle,3,2);Q(s,3,2).height=2;Q(s,4,2).height=2;
            Check(HabitatRules.IsProtected(s,3,3),"valid turtle protects home neighborhood");
            Check(!HabitatRules.IsThreatened(s,j,t),"turtle suppresses shrimp fear");
            Q(s,4,2).height=1;Check(HabitatRules.IsThreatened(s,j,t),"invalid turtle home loses protection");
            t.minHighland=1;Check(!HabitatRules.IsThreatened(s,j,t),"protection follows highland tuning");t.minHighland=2;
            s=Flat();for(int z=3;z<=4;z++)for(int x=3;x<=4;x++)Q(s,x,z).height=0;
            Animal f=A(s,Species.Goldfish,3,3);j=A(s,Species.Jellyfish,4,4);
            Check(HabitatRules.Evaluate(s,f,t).suitable,"closed four-cell pond breeds fish");
            Check(HabitatRules.Evaluate(s,j,t).suitable,"connected pond breeds jellyfish");
            for(int z=0;z<=2;z++)Q(s,3,z).height=0;
            Check(!HabitatRules.Evaluate(s,f,t).suitable,"ocean connection invalidates fish pond");
            Check(HabitatRules.Evaluate(s,j,t).suitable,"jellyfish accepts ocean connection");
            s=Flat();shr=A(s,Species.Shrimp,4,4);Check(HabitatRules.Evaluate(s,shr,t).suitable,"solitary shrimp has reachable habitat");
            Animal neighbor=A(s,Species.Turtle,5,4);Q(s,5,4).height=3;
            Check(!HabitatRules.Evaluate(s,shr,t).suitable,"shrimp loneliness includes other heights");neighbor.carried=true;
            Q(s,3,4).prop=Q(s,5,4).prop=Q(s,4,3).prop=Q(s,4,5).prop=PropKind.Obstacle;
            Check(!HabitatRules.Evaluate(s,shr,t).suitable,"shrimp trapped in one-cell region cannot breed");
            s=Flat();Animal crab=A(s,Species.Crab,4,4);A(s,Species.Jellyfish,3,4);A(s,Species.Jellyfish,5,4);
            Check(!HabitatRules.Evaluate(s,crab,t).suitable,"crab needs species diversity rather than animal count");
            A(s,Species.Starfish,4,5);Check(HabitatRules.Evaluate(s,crab,t).suitable,"two other friendly species satisfy crab");
            s=Flat();Animal shoreStar=A(s,Species.Starfish,4,4);Q(s,3,4).height=0;A(s,Species.Crab,4,5);
            Check(!HabitatRules.Evaluate(s,shoreStar,t).suitable,"one shoreline cell is insufficient");
            Q(s,3,5).height=0;Check(HabitatRules.Evaluate(s,shoreStar,t).suitable,"connected shoreline plus crab friend satisfies starfish");
            s=Flat();s.playerX=3;s.playerZ=2;Animal c=A(s,Species.Crab,3,3);
            Check(!HabitatRules.Interact(s,3,3)&&!c.carried,"crab cannot be lifted");s.animals.Clear();Animal star=A(s,Species.Starfish,3,3);
            Check(!HabitatRules.Interact(s,3,3)&&!star.carried,"starfish cannot be lifted");
            Check(!HabitatRules.MovePlayer(s,1,1),"diagonal player movement rejected");
            s=Flat();Q(s,2,1).prop=PropKind.Crate;Q(s,3,1).prop=PropKind.Obstacle;
            Check(!HabitatRules.MovePlayer(s,1,0)&&s.playerX==1&&Q(s,2,1).prop==PropKind.Crate,"blocked crate push atomic");
            Q(s,3,1).prop=PropKind.None;Q(s,3,1).height=0;
            Check(HabitatRules.MovePlayer(s,1,0)&&Q(s,3,1).prop==PropKind.Float,"crate entering water becomes floating block");
            Check(HabitatRules.MovePlayer(s,1,0)&&s.playerX==3,"player can board floating block");
            s=Flat(0);s.playerX=1;s.playerZ=1;Q(s,1,1).height=1;Q(s,2,1).prop=PropKind.Crate;
            Check(!HabitatRules.MovePlayer(s,1,0)&&Q(s,2,1).prop==PropKind.Crate&&Q(s,3,1).prop==PropKind.None,"push rejected before exposing unwalkable source");
            s=Flat();int cargo=s.cargo;Check(HabitatRules.Shop(s,"soil")&&s.cargo==cargo-1,"soil withdrawn at dock");
            Check(!HabitatRules.Shop(s,"crate"),"one carried item only");
            Check(HabitatRules.PlaceSoil(s,2,1)&&Q(s,2,1).height==2&&s.carryingProp==PropKind.None,"carried soil raises one tile");
            Check(HabitatRules.Shop(s,"soil")&&HabitatRules.PlaceSoil(s,2,2),"soil permits diagonal neighborhood placement");
            s.playerX=6;s.playerZ=6;Check(!HabitatRules.Shop(s,"soil"),"remote dock transaction rejected");
            s=Flat();for(int z=3;z<=4;z++)for(int x=3;x<=4;x++)Q(s,x,z).height=0;
            f=A(s,Species.Goldfish,3,3);HabitatRules.Tick(s,t,0.5f);Check(f.progress>0.45f&&f.offspring==0,"suitable progress accumulates");
            shr=A(s,Species.Shrimp,4,3);HabitatRules.Tick(s,t,0.1f);Check(f.progress==0,"unsuitable interval resets breeding");
            shr.carried=true;HabitatRules.Tick(s,t,1.01f);Check(f.offspring==1&&f.spawnedTotal==1,"continuous suitable interval spawns separate offspring");
            s.playerX=2;s.playerZ=3;Check(HabitatRules.Fish(s,3,3)&&f.offspring==0&&s.animals.Contains(f)&&s.fish==1,"harvest keeps parent");
            s.playerX=s.dockX;s.playerZ=s.dockZ;int money=s.coins;Check(HabitatRules.Shop(s,"sell")&&s.coins==money+8,"fish offspring sale economy");
            s=Flat();shr=A(s,Species.Shrimp,4,4);Q(s,4,4).trap=true;HabitatRules.Tick(s,t,1.1f);
            Check(s.shrimp==1&&shr.offspring==0&&shr.spawnedTotal==1&&!shr.carried,"trap harvests offspring and keeps parent");
            HabitatState animalClone=HabitatRules.Clone(s);animalClone.animals[0].progress=77;
            Check(s.animals[0].progress!=77,"clone deep-copies animal progress");
            s=Flat(0);s.playerX=3;s.playerZ=3;Q(s,2,3).prop=PropKind.Fan;Q(s,2,3).rotation=1;Q(s,3,3).prop=PropKind.Float;
            HabitatRules.Tick(s,new Tuning{animalMovement=false,moveSeconds=0.2f},0.21f);
            Check(s.playerX==4&&Q(s,4,3).prop==PropKind.Float,"fan transports float and rider");
            s=Flat(2);s.playerX=1;s.playerZ=1;Q(s,2,1).prop=PropKind.Crate;Q(s,3,1).prop=PropKind.Tide;
            Check(HabitatRules.MovePlayer(s,1,0)&&s.waterLevel==1&&s.tideActive,"crate on pressure plate raises global tide");
            Check(HabitatRules.MovePlayer(s,1,0)&&s.waterLevel==0&&Q(s,3,1).prop==PropKind.Tide,"moving crate off restores plate and tide");
            HabitatState clone=HabitatRules.Clone(s);clone.tiles[0].height=99;clone.events.Add("test");clone.earnedSpecies.Add(Species.Crab);
            Check(s.tiles[0].height!=99&&!s.events.Contains("test")&&!s.earnedSpecies.Contains(Species.Crab),"clone deep-copies tiles events goals");
            s=HabitatLevels.Create(0);t=new Tuning();
            HabitatRules.Tick(s,t,30);
            Check(s.animals.Find(a=>a.species==Species.Shrimp).x==2&&s.animals.Find(a=>a.species==Species.Shrimp).z==2&&!s.won,"tutorial remains actionable after30 seconds idle");
            Check(HabitatRules.Interact(s,2,2)&&HabitatRules.Interact(s,1,3),"tutorial two-action shrimp relocation");
            HabitatRules.Tick(s,t,18.1f);Check(s.won,"tutorial solvable after default18-second breed with live movement");
            s=HabitatLevels.Create(1);t=new Tuning{animalMovement=false,breedSeconds=1};
            Check(HabitatRules.Shop(s,"soil"),"lab soil available");s.playerX=3;s.playerZ=2;Check(HabitatRules.PlaceSoil(s,4,2),"lab pond closure");
            s.playerX=s.dockX;s.playerZ=s.dockZ;Check(HabitatRules.Shop(s,"soil"),"lab second soil");s.playerX=5;s.playerZ=5;Check(HabitatRules.PlaceSoil(s,6,5),"lab highland extension");
            HabitatRules.Tick(s,t,1.1f);Check(s.won,"lab both species goal solvable");
            s=HabitatLevels.Create(2);s.playerX=2;s.playerZ=4;Check(HabitatRules.MovePlayer(s,1,0)&&s.waterLevel==1,"tide level plate reachable solution");
            HabitatRules.Tick(s,new Tuning{animalMovement=false,breedSeconds=1},1.1f);Check(s.won,"tide level breeds jellyfish and turtle");
            s=HabitatLevels.Create(3);HabitatRules.Tick(s,new Tuning{animalMovement=false,breedSeconds=1},1.1f);Check(s.won,"sandbox six-species goal initially attainable");
            return "PASS: "+assertions+" model assertions (species, threats, pond, carry, push, economy, breeding, fan, tide, clone, four levels)";
        }
    }
}
