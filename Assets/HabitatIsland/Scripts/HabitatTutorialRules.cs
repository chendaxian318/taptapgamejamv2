using System;

namespace HabitatIsland
{
    public enum HabitatTutorialStep { Arrival, ObserveCrab, PickShrimp, PlaceShrimp, ObserveHabitat, Resume, Wait, Complete }

    // Guidance describes the committed model. It never advances simulation, moves an animal,
    // changes the pause setting, or treats an attempted input as a completed action.
    public static class HabitatTutorialRules
    {
        public const int LevelIndex=0;
        public const int TrapX=1,TrapZ=3;

        public static bool IsTutorial(HabitatState s) => s!=null&&s.levelIndex==LevelIndex&&!s.authoredLevel;

        public static HabitatTutorialStep Evaluate(HabitatState s,Tuning t,bool paused)
        {
            if(s==null)return HabitatTutorialStep.Arrival;
            if(s.tutorialCompleted||HasCompletedObjective(s))return HabitatTutorialStep.Complete;
            if(s.onHarbor)return HabitatTutorialStep.Arrival;
            if(CarriedShrimp(s)!=null)return HabitatTutorialStep.PlaceShrimp;
            if(ShrimpInTrap(s))
            {
                HabitatReport report=CrabReport(s,t);
                if(!s.tutorialObservedHabitat||report==null||!report.suitable)return HabitatTutorialStep.ObserveHabitat;
                return paused?HabitatTutorialStep.Resume:HabitatTutorialStep.Wait;
            }
            return s.tutorialObservedCrab?HabitatTutorialStep.PickShrimp:HabitatTutorialStep.ObserveCrab;
        }

        // Call only when the game accepts normal observation (not busy, modal or fishing).
        // Returning false means there is no tutorial state change to record.
        public static bool Observe(HabitatState s,Tuning t)
        {
            if(!IsTutorial(s))return false;
            bool changed=false;
            if(HasCompletedObjective(s))
            {
                changed=!s.tutorialCompleted||s.tutorialOfferPending;
                s.tutorialCompleted=true;s.tutorialOfferPending=false;
                return changed;
            }
            if(s.onHarbor||s.animals==null)return false;
            Animal selected=s.animals.Find(a=>a!=null&&!a.carried&&a.species==Species.Crab&&a.x==s.selectedX&&a.z==s.selectedZ);
            if(selected==null)return false;
            if(!s.tutorialObservedCrab){s.tutorialObservedCrab=true;changed=true;}
            HabitatReport report=Report(s,selected,t);
            if(ShrimpInTrap(s)&&report!=null&&report.suitable&&!s.tutorialObservedHabitat)
            {s.tutorialObservedHabitat=true;changed=true;}
            return changed;
        }

        public static string Title(HabitatTutorialStep step)
        {
            switch(step)
            {
                case HabitatTutorialStep.Arrival:return "沿木桥走上教学小岛";
                case HabitatTutorialStep.ObserveCrab:return "先看看螃蟹的烦恼";
                case HabitatTutorialStep.PickShrimp:return "把附近的虾抱起来";
                case HabitatTutorialStep.PlaceShrimp:return "把虾安置到指定笼子";
                case HabitatTutorialStep.ObserveHabitat:return "再看看螃蟹的环境";
                case HabitatTutorialStep.Resume:return "让小岛的时间继续";
                case HabitatTutorialStep.Wait:return "等待螃蟹首次繁衍";
                default:return "新手教学完成";
            }
        }

        public static string Body(HabitatTutorialStep step,HabitatState s,Tuning t)
        {
            if(s==null)return "教学地图尚未准备好。";
            if(step==HabitatTutorialStep.Complete)return "螃蟹已首次繁衍。你可以继续探索，或回到船边选择正式岛屿。";
            if(step==HabitatTutorialStep.Arrival)return "用 WASD 或方向键沿木桥走回岸边。方向跟随镜头；暂停生态时也能走路。";
            Animal crab=Crab(s),shrimp=Shrimp(s);
            if(crab==null)return "岛上找不到螃蟹，暂时无法完成教学。可撤销改动，或在设置中重开本关。";
            if(shrimp==null)return "岛上找不到虾，暂时无法继续搬运教学。可撤销改动，或在设置中重开本关。";
            if(step==HabitatTutorialStep.ObserveCrab)
            {
                HabitatReport report=CrabReport(s,t);
                return "点击 "+Coordinate(crab.x,crab.z)+" 的螃蟹，查看右侧详情。它不能抱起，需要改变周围环境。"+
                    (report==null?"":report.safe?" 当前没有虾威胁。":" 邻近同高度的虾正在威胁它。");
            }
            if(step==HabitatTutorialStep.PickShrimp)
            {
                if(s.carryingAnimal>=0||s.carryingProp!=PropKind.None)return "你正拿着其他动物或道具。先在合法空格放下，再靠近虾，按 1 切换搬运工具。";
                return "走到虾 "+Coordinate(shrimp.x,shrimp.z)+" 的相邻格，点击它，按 1、E 抱起。等人物走到位后再操作；普通台阶不能向上爬。";
            }
            if(step==HabitatTutorialStep.PlaceShrimp)
            {
                Tile trap=Tile(s,TrapX,TrapZ);
                if(trap==null||!trap.trap)return "(2,4) 笼子已不在原处。撤销改动或重开本关，恢复布置后再放下虾。";
                if(trap.prop==PropKind.Obstacle||trap.prop==PropKind.Chest||trap.prop==PropKind.Fan||
                    s.animals.Exists(a=>a!=null&&!a.carried&&a.x==TrapX&&a.z==TrapZ))
                    return "(2,4) 笼子格被占用了。先移开占用物或撤销改动，再放下虾；不能覆盖其他动物或障碍。";
                return "靠近 (2,4) 的笼子格，点击笼子，按 1、E 放下虾。这里能让虾留在原地，远离螃蟹。";
            }
            HabitatReport habitat=CrabReport(s,t);
            if(habitat==null)return "暂时无法读取螃蟹的环境。请撤销地形改动或重开本关后再试。";
            if(!habitat.suitable)return "螃蟹还不适居："+habitat.reason+"。点击 "+Coordinate(crab.x,crab.z)+" 查看详情，恢复条件后才能继续计时。";
            if(step==HabitatTutorialStep.ObserveHabitat)return "点击 "+Coordinate(crab.x,crab.z)+" 的螃蟹，查看环境改善：威胁解除，水母和海星提供两种友好邻居。";
            if(step==HabitatTutorialStep.Resume)return "环境已经适宜。点击顶部「继续」或按空格，让生态时间运行。保持条件，等待螃蟹首次繁衍。";
            float duration=Math.Max(.5f,(t??new Tuning()).breedSeconds);
            float progress=float.IsNaN(crab.progress)||float.IsInfinity(crab.progress)?0:Math.Max(0,crab.progress);
            int remaining=(int)Math.Ceiling(Math.Max(0,duration-progress));
            return "螃蟹已适居，还需约 "+remaining+" 秒生态时间。保持虾远离它、友好邻居在附近；暂停或打开弹窗时不计时。";
        }

        public static bool TryTarget(HabitatTutorialStep step,HabitatState s,out int x,out int z)
        {
            x=z=-1;
            if(s==null||step==HabitatTutorialStep.Complete)return false;
            if(step==HabitatTutorialStep.Arrival){x=s.dockX;z=s.dockZ;return Tile(s,x,z)!=null;}
            if(step==HabitatTutorialStep.PlaceShrimp)
            {x=TrapX;z=TrapZ;Tile trap=Tile(s,x,z);return trap!=null&&trap.trap;}
            Animal animal=step==HabitatTutorialStep.PickShrimp?Shrimp(s):Crab(s);
            if(animal==null||animal.carried)return false;
            x=animal.x;z=animal.z;return Tile(s,x,z)!=null;
        }

        static bool HasCompletedObjective(HabitatState s)
            => IsTutorial(s)&&s.animals!=null&&s.animals.Exists(a=>a!=null&&a.species==Species.Crab&&a.spawnedTotal>0)&&
                (s.won||(s.earnedSpecies!=null&&s.earnedSpecies.Contains(Species.Crab)));
        static Animal Crab(HabitatState s) => s.animals?.Find(a=>a!=null&&a.species==Species.Crab&&!a.carried);
        static Animal CarriedShrimp(HabitatState s)
            => s.animals?.Find(a=>a!=null&&a.species==Species.Shrimp&&a.carried&&a.id==s.carryingAnimal);
        static Animal Shrimp(HabitatState s)
            => CarriedShrimp(s)??s.animals?.Find(a=>a!=null&&a.species==Species.Shrimp&&!a.carried&&a.x==TrapX&&a.z==TrapZ)
                ??s.animals?.Find(a=>a!=null&&a.species==Species.Shrimp&&!a.carried);
        static bool ShrimpInTrap(HabitatState s)
        {
            Tile trap=Tile(s,TrapX,TrapZ);
            return trap!=null&&trap.trap&&s.animals!=null&&s.animals.Exists(a=>a!=null&&a.species==Species.Shrimp&&!a.carried&&a.x==TrapX&&a.z==TrapZ);
        }
        static HabitatReport CrabReport(HabitatState s,Tuning t) => Report(s,Crab(s),t);
        static HabitatReport Report(HabitatState s,Animal crab,Tuning t)
            => crab!=null&&Tile(s,crab.x,crab.z)!=null?HabitatRules.Evaluate(s,crab,t??new Tuning()):null;
        static Tile Tile(HabitatState s,int x,int z)
        {
            if(s==null||s.tiles==null||x<0||z<0||x>=s.width||z>=s.depth)return null;
            int index=z*s.width+x;return index>=0&&index<s.tiles.Length?s.tiles[index]:null;
        }
        static string Coordinate(int x,int z) => "("+(x+1)+","+(z+1)+")";
    }
}
