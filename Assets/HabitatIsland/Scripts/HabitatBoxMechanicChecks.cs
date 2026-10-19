using System;
using System.Collections;
using System.Reflection;

namespace HabitatIsland
{
    /// <summary>Pure rule checks for the revised climbing limit, backwards pulling and float recovery.</summary>
    public static class HabitatBoxMechanicChecks
    {
        static int assertions;
        static void Check(bool condition,string label)
        { assertions++;if(!condition)throw new InvalidOperationException("Box mechanic check failed: "+label); }
        static HabitatState Flat(int height=1)
        {
            var state=new HabitatState{width=7,depth=7,playerX=3,playerZ=3};
            state.tiles=new Tile[state.width*state.depth];
            for(int i=0;i<state.tiles.Length;i++)state.tiles[i]=new Tile{height=height};
            return state;
        }
        static Tile Q(HabitatState state,int x,int z){return HabitatRules.TileAt(state,x,z);}
        static HabitatState PullFixture(PropKind kind=PropKind.Crate,int height=1)
        { var state=Flat(height);Q(state,4,3).prop=kind;return state; }
        static Animal Add(HabitatState state,int x,int z,Species species=Species.Shrimp)
        {
            var animal=new Animal{id=state.animals.Count+1,x=x,z=z,homeX=x,homeZ=z,species=species};
            state.animals.Add(animal);return animal;
        }
        static bool SameValue(object before,object after)
        {
            if(ReferenceEquals(before,after))return true;
            if(before==null||after==null||before.GetType()!=after.GetType())return false;
            Type type=before.GetType();
            if(type.IsValueType||before is string)return before.Equals(after);
            if(before is IEnumerable left&&after is IEnumerable right)
            {
                IEnumerator a=left.GetEnumerator(),b=right.GetEnumerator();
                while(true)
                {
                    bool moreA=a.MoveNext(),moreB=b.MoveNext();
                    if(moreA!=moreB)return false;if(!moreA)return true;
                    if(!SameValue(a.Current,b.Current))return false;
                }
            }
            foreach(FieldInfo field in type.GetFields(BindingFlags.Public|BindingFlags.Instance))
            {
                // Rejection must explain itself, but must leave every gameplay field intact.
                if(type==typeof(HabitatState)&&(field.Name=="message"||field.Name=="events"))continue;
                if(!SameValue(field.GetValue(before),field.GetValue(after)))return false;
            }
            return true;
        }
        static void Reject(HabitatState state,Func<bool> action,string label)
        {
            HabitatState before=HabitatRules.Clone(state);
            Check(!action(),label+" is rejected");
            Check(SameValue(before,state),label+" preserves all gameplay fields, including facing and tide");
            Check(!string.IsNullOrEmpty(state.message),label+" gives a player-facing explanation");
        }
        static void CheckRecoveryQuery(HabitatState state,int x,int z,bool expected,string label)
        {
            HabitatState before=HabitatRules.Clone(state);
            Check(HabitatRules.CanRecoverBox(state,x,z)==expected,label+" reports the expected availability");
            Check(SameValue(before,state)&&before.message==state.message&&SameValue(before.events,state.events),
                label+" does not mutate gameplay, message or event history");
        }
        static void RecoveryChecks()
        {
            int[] dx={1,-1,0,0},dz={0,0,1,-1};
            HabitatState state;
            for(int direction=0;direction<4;direction++)
            {
                state=Flat(3);int x=3+dx[direction],z=3+dz[direction];
                Q(state,x,z).height=0;Q(state,x,z).prop=PropKind.Float;Q(state,x,z).rotation=direction;
                state.message="before query";state.events.Add("preserve query history");
                CheckRecoveryQuery(state,x,z,true,"recovery direction "+direction);
                Check(HabitatRules.RecoverBox(state,x,z)&&state.playerX==3-dx[direction]&&state.playerZ==3-dz[direction]&&
                    state.facingX==dx[direction]&&state.facingZ==dz[direction]&&state.carryingProp==PropKind.None&&state.carryingAnimal<0,
                    "recovery direction "+direction+" retreats one cell facing the old box with empty hands");
                Check(Q(state,3,3).prop==PropKind.Crate&&Q(state,3,3).rotation==direction&&Q(state,x,z).prop==PropKind.None,
                    "recovery direction "+direction+" converts the float to a crate while preserving box rotation");
            }
            state=PullFixture(height:3);Q(state,4,3).height=1;
            Check(HabitatRules.RecoverBox(state,4,3)&&Q(state,3,3).prop==PropKind.Crate&&state.playerX==2,
                "a dry box two layers below the bank can be recovered without being carried");
            state=PullFixture(PropKind.ClimbBlock,height:3);Q(state,4,3).height=0;
            Check(HabitatRules.RecoverBox(state,4,3)&&Q(state,3,3).prop==PropKind.ClimbBlock&&state.carryingProp==PropKind.None,
                "a submerged climbing block is recoverable without becoming a wooden crate");
            state=PullFixture(height:2);Q(state,4,3).height=1;Q(state,4,3).coveredProp=PropKind.Road;Q(state,4,3).rotation=2;
            Check(HabitatRules.RecoverBox(state,4,3)&&Q(state,4,3).prop==PropKind.Road&&Q(state,4,3).coveredProp==PropKind.None&&Q(state,4,3).rotation==2,
                "recovery leaves the source road and its direction intact");
            state=PullFixture(PropKind.Float,height:3);Q(state,4,3).height=0;Q(state,4,3).coveredProp=PropKind.Tide;
            state.tideActive=state.tideEverActivated=true;state.waterLevel=1;Q(state,2,3).height=1;
            CheckRecoveryQuery(state,4,3,true,"recovery releasing a tide plate");
            Check(HabitatRules.RecoverBox(state,4,3)&&Q(state,4,3).prop==PropKind.Tide&&!state.tideActive&&state.waterLevel==0&&
                state.tideEverActivated&&state.playerX==2&&!HabitatRules.IsWater(state,2,3),
                "recovery may expose a dry retreat by releasing tide and retains the historical checkpoint");
            state=PullFixture(PropKind.Float,height:3);Q(state,4,3).height=0;Q(state,4,3).coveredProp=PropKind.Tide;
            Q(state,5,5).prop=PropKind.Crate;Q(state,5,5).coveredProp=PropKind.Tide;
            state.tideActive=state.tideEverActivated=true;state.waterLevel=1;Q(state,2,3).height=1;
            CheckRecoveryQuery(state,4,3,false,"recovery while another plate keeps the retreat underwater");
            Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"recovery whose retreat remains flooded after the source plate releases");
            state=PullFixture();CheckRecoveryQuery(state,4,3,false,"same-height ordinary box");
            Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"explicit recovery of a same-height ordinary box");
            Check(HabitatRules.Interact(state,4,3)&&state.carryingProp==PropKind.Crate&&state.playerX==3&&Q(state,4,3).prop==PropKind.None,
                "same-height ordinary box interaction still picks up normally without a recovery retreat");
            state=PullFixture(height:2);Q(state,4,3).height=1;state.carryingProp=PropKind.Road;
            Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"recovery with a carried prop");
            state=PullFixture(height:2);Q(state,4,3).height=1;Animal carried=Add(state,3,3);carried.carried=true;state.carryingAnimal=carried.id;
            Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"recovery with a carried animal");
            foreach(PropKind prop in new[]{PropKind.Road,PropKind.Tide,PropKind.Float,PropKind.Crate,PropKind.Climb,PropKind.ClimbBlock,PropKind.Obstacle})
            {
                state=PullFixture(height:3);Q(state,4,3).height=0;Q(state,3,3).prop=prop;
                CheckRecoveryQuery(state,4,3,false,"occupied recovery landing "+prop);
                Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"recovery onto an origin containing "+prop);
            }
            foreach(PropKind prop in new[]{PropKind.Crate,PropKind.ClimbBlock,PropKind.Fan,PropKind.Obstacle,PropKind.Chest})
            {
                state=PullFixture(height:3);Q(state,4,3).height=1;Q(state,2,3).prop=prop;
                Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"recovery retreat blocked by "+prop);
            }
            foreach(int occupiedX in new[]{2,3,4})
            {
                state=PullFixture(height:3);Q(state,4,3).height=1;Add(state,occupiedX,3);
                Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"recovery with an animal at "+occupiedX);
                state=PullFixture(height:3);Q(state,4,3).height=1;Q(state,occupiedX,3).trap=true;
                Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"recovery with a trap at "+occupiedX);
            }
            state=PullFixture(height:2);Q(state,4,3).height=1;Q(state,2,3).height=3;
            Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"recovery requiring an ordinary one-layer uphill retreat");
            state=PullFixture(height:3);Q(state,4,3).height=1;Q(state,2,3).height=0;
            Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"recovery into open-water retreat");
            state=PullFixture(height:3);Q(state,4,3).height=1;Q(state,2,3).height=1;
            Check(HabitatRules.RecoverBox(state,4,3)&&state.playerX==2,"recovery can retreat down to a dry lower cell");
            state=PullFixture(height:3);Q(state,4,3).height=1;state.onHarbor=true;
            Reject(state,()=>HabitatRules.RecoverBox(state,4,3),"recovery from a stale harbor grid anchor");
            state=Flat(2);state.playerX=0;Q(state,1,3).prop=PropKind.Float;Q(state,1,3).height=0;
            Reject(state,()=>HabitatRules.RecoverBox(state,1,3),"recovery requiring retreat beyond the board");
            state=Flat(2);Q(state,4,4).prop=PropKind.Float;Q(state,4,4).height=0;
            Reject(state,()=>HabitatRules.RecoverBox(state,4,4),"diagonal recovery target");
            state=Flat(2);Q(state,5,3).prop=PropKind.Float;Q(state,5,3).height=0;
            Reject(state,()=>HabitatRules.RecoverBox(state,5,3),"non-adjacent recovery target");
        }

        public static string Run()
        {
            assertions=0;
            HabitatState state=Flat();Q(state,4,3).height=2;Q(state,4,3).prop=PropKind.Crate;
            Check(!HabitatRules.MovePlayer(state,1,0,true)&&state.playerX==3&&Q(state,4,3).prop==PropKind.Crate,
                "Shift cannot climb an ordinary crate whose top is two layers above the player");
            state=Flat();Q(state,4,3).prop=PropKind.Crate;Q(state,5,3).prop=PropKind.Obstacle;
            Check(!HabitatRules.MovePlayer(state,1,0,true)&&state.playerX==3&&Q(state,4,3).prop==PropKind.Crate,
                "Shift cannot climb a one-layer ordinary crate");
            state=Flat();Q(state,4,3).prop=PropKind.ClimbBlock;
            Check(HabitatRules.MovePlayer(state,1,0,true)&&state.playerX==4&&Q(state,4,3).prop==PropKind.ClimbBlock,
                "Shift climbs a one-layer dedicated block without pushing it");
            state=Flat();Q(state,4,3).height=2;Q(state,4,3).prop=PropKind.ClimbBlock;
            Check(HabitatRules.MovePlayer(state,1,0,true)&&state.playerX==4&&Q(state,4,3).prop==PropKind.ClimbBlock,
                "Shift climbs a dedicated climbing block up two layers without moving it");
            state=Flat();Q(state,4,3).height=3;Q(state,4,3).prop=PropKind.ClimbBlock;
            Check(!HabitatRules.MovePlayer(state,1,0,true)&&state.playerX==3&&Q(state,4,3).prop==PropKind.ClimbBlock,
                "a dedicated climbing block cannot bypass a three-layer rise");
            state=Flat();Q(state,4,3).height=2;
            Check(!HabitatRules.MovePlayer(state,1,0)&&state.playerX==3,"ordinary terrain forbids even a one-layer rise");
            Check(!HabitatRules.MovePlayer(state,1,0,true)&&state.playerX==3,"Shift cannot bypass a one-layer ordinary terrain rise");
            state=Flat();Q(state,4,3).height=3;
            Check(!HabitatRules.MovePlayer(state,1,0,true)&&state.playerX==3,"Shift cannot bypass a bare two-layer cliff");
            state=PullFixture();
            Check(HabitatRules.MovePlayer(state,1,0)&&state.playerX==4&&Q(state,5,3).prop==PropKind.Crate,
                "ordinary forward movement still pushes crates");
            state=Flat();Q(state,4,3).height=2;Q(state,4,3).prop=PropKind.Crate;
            Check(!HabitatRules.MovePlayer(state,1,0)&&state.playerX==3&&Q(state,4,3).prop==PropKind.Crate&&Q(state,5,3).prop==PropKind.None,
                "pushing cannot move a box first and then expose a forbidden uphill player step");
            state=Flat(4);Q(state,4,3).height=1;
            Check(HabitatRules.MovePlayer(state,1,0)&&state.playerX==4,"player can drop multiple layers onto a dry ordinary surface");
            state=Flat(4);Q(state,4,3).height=0;
            Check(!HabitatRules.MovePlayer(state,1,0)&&state.playerX==3,"a long drop never bypasses the open-water restriction");
            state=Flat();Q(state,4,3).height=0;Q(state,4,3).prop=PropKind.Float;state.waterLevel=1;
            Check(!HabitatRules.MovePlayer(state,1,0)&&state.playerX==3,"an elevated floating box does not permit an ordinary uphill step");

            int[] dx={1,-1,0,0},dz={0,0,1,-1};
            for(int direction=0;direction<4;direction++)
            {
                state=Flat();Q(state,3-dx[direction],3-dz[direction]).prop=PropKind.Crate;
                Check(HabitatRules.PullPlayer(state,dx[direction],dz[direction])&&state.playerX==3+dx[direction]&&state.playerZ==3+dz[direction],
                    "pull direction "+direction+" retreats exactly one orthogonal cell");
                Check(Q(state,3,3).prop==PropKind.Crate&&Q(state,3-dx[direction],3-dz[direction]).prop==PropKind.None&&
                    state.facingX==-dx[direction]&&state.facingZ==-dz[direction],
                    "pull direction "+direction+" moves the box into the old cell and keeps facing it");
            }
            state=PullFixture(PropKind.ClimbBlock);
            Check(HabitatRules.PullPlayer(state,-1,0)&&Q(state,3,3).prop==PropKind.ClimbBlock,
                "pulling a climbing block preserves its type");
            state=PullFixture();Q(state,4,3).height=2;
            Check(HabitatRules.PullPlayer(state,-1,0)&&Q(state,3,3).prop==PropKind.Crate,
                "a solid box may be pulled down one terrain layer");
            state=PullFixture();Q(state,2,3).height=2;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"pulling cannot bypass a one-layer uphill restriction");
            state=PullFixture(height:2);Q(state,2,3).height=1;
            Check(HabitatRules.PullPlayer(state,-1,0)&&state.playerX==2,"pulling permits a one-layer downhill retreat");
            state=PullFixture();Q(state,4,3).coveredProp=PropKind.Road;Q(state,4,3).rotation=2;
            Q(state,3,3).prop=PropKind.Road;Q(state,3,3).rotation=1;
            Check(HabitatRules.PullPlayer(state,-1,0)&&Q(state,4,3).prop==PropKind.Road&&Q(state,4,3).coveredProp==PropKind.None&&
                Q(state,3,3).coveredProp==PropKind.Road&&Q(state,4,3).rotation==2&&Q(state,3,3).rotation==1,
                "pulling exposes the source road and preserves the destination road and their directions");

            state=PullFixture();Reject(state,()=>HabitatRules.PullPlayer(state,0,0),"neutral pull direction");
            state=PullFixture();Reject(state,()=>HabitatRules.PullPlayer(state,1,1),"diagonal pull direction");
            state=PullFixture();state.playerX=0;Q(state,1,3).prop=PropKind.Crate;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"retreat beyond the board");
            foreach(PropKind kind in new[]{PropKind.None,PropKind.Climb,PropKind.Road,PropKind.Fan,PropKind.Tide,PropKind.Obstacle,PropKind.Chest})
            {
                state=PullFixture(kind);Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"pulling unsupported prop "+kind);
            }
            state=PullFixture();state.carryingProp=PropKind.Float;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"pulling while carrying a prop");
            state=PullFixture();Animal carried=Add(state,3,3);carried.carried=true;state.carryingAnimal=carried.id;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"pulling while carrying an animal");
            state=PullFixture();Q(state,3,3).prop=PropKind.Float;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"pulling into a support already under the player");
            foreach(PropKind kind in new[]{PropKind.Obstacle,PropKind.Chest,PropKind.Fan,PropKind.Crate,PropKind.ClimbBlock})
            {
                state=PullFixture();Q(state,2,3).prop=kind;
                Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"retreat blocked by "+kind);
            }
            foreach(int animalX in new[]{2,3,4})
            {
                state=PullFixture();Add(state,animalX,3);
                Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"animal on pull path at column "+animalX);
            }
            state=PullFixture();Q(state,2,3).height=0;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"retreat into open water");
            state=PullFixture();Q(state,2,3).height=3;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"retreat up two layers");
            state=PullFixture(height:3);Q(state,2,3).height=1;
            Check(HabitatRules.PullPlayer(state,-1,0)&&state.playerX==2,"pulling may retreat down two layers onto dry ground");
            state=PullFixture();Q(state,3,3).height=2;Q(state,2,3).height=2;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"dragging a solid box uphill");
            state=PullFixture();Q(state,4,3).height=3;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"box beyond the one-layer pulling reach");

            state=PullFixture(PropKind.Float);Q(state,4,3).height=0;
            Check(HabitatRules.PullPlayer(state,-1,0)&&Q(state,3,3).prop==PropKind.Crate&&Q(state,4,3).prop==PropKind.None&&state.playerX==2,
                "pulling an adjacent float onto a one-layer dry bank restores an ordinary crate");
            Check(state.message.Contains("拖上岸")&&state.message.Contains("木箱"),"float-to-crate conversion is explained in the log");
            state=PullFixture(PropKind.Float);state.waterLevel=state.tideBaseLevel=1;Q(state,4,3).height=0;Q(state,2,3).height=2;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"pulling a float cannot make the player climb out of a flooded lower cell");
            state=PullFixture(PropKind.Float,height:2);Q(state,4,3).height=0;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"pulling a float onto a two-layer high bank");
            state=PullFixture();Q(state,3,3).height=0;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"pulling a crate does not permit an uphill escape from water");
            state=PullFixture(PropKind.ClimbBlock);Q(state,3,3).height=0;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"pulling a climbing block does not permit an uphill retreat");
            state=PullFixture();Q(state,2,3).height=0;Q(state,2,3).prop=PropKind.Float;
            Check(HabitatRules.PullPlayer(state,-1,0)&&state.playerX==2&&Q(state,2,3).prop==PropKind.Float,
                "an unoccupied adjacent float is a legal retreat support and is not pushed");

            state=PullFixture(PropKind.Float);Q(state,4,3).height=0;
            Check(HabitatRules.Interact(state,4,3)&&state.carryingProp==PropKind.None&&Q(state,4,3).prop==PropKind.None&&
                Q(state,3,3).prop==PropKind.Crate&&state.playerX==2,
                "adjacent interaction now recovers a float onto the old bank cell and retreats without carrying it");
            state=Flat();Q(state,3,3).prop=PropKind.Float;Q(state,3,3).height=0;
            Reject(state,()=>HabitatRules.Interact(state,3,3),"recovering the float currently supporting the player");
            state=PullFixture(PropKind.Float);Q(state,4,3).height=0;Add(state,4,3,Species.Crab);
            Reject(state,()=>HabitatRules.Interact(state,4,3),"recovering a float occupied by an unliftable animal");
            state=PullFixture(PropKind.Float);Q(state,4,3).height=0;Q(state,4,3).coveredProp=PropKind.Tide;
            Q(state,3,3).height=0;Q(state,3,3).prop=PropKind.Float;state.waterLevel=1;state.tideActive=true;
            Reject(state,()=>HabitatRules.Interact(state,4,3),"recovery cannot replace a floating support under the player");

            state=PullFixture(height:2);Q(state,4,3).coveredProp=PropKind.Tide;
            state.waterLevel=1;state.tideActive=true;state.tideEverActivated=true;
            Check(HabitatRules.PullPlayer(state,-1,0)&&Q(state,4,3).prop==PropKind.Tide&&!state.tideActive&&state.waterLevel==0&&state.tideEverActivated,
                "pulling a crate off its pressure plate restores low tide and preserves the observed checkpoint");
            state=PullFixture(PropKind.Float,height:2);Q(state,4,3).height=0;Q(state,4,3).coveredProp=PropKind.Tide;
            state.waterLevel=1;state.tideActive=true;state.tideEverActivated=true;
            Check(HabitatRules.PullPlayer(state,-1,0)&&Q(state,4,3).prop==PropKind.Tide&&Q(state,3,3).prop==PropKind.Crate&&
                !state.tideActive&&state.waterLevel==0&&!HabitatRules.IsWater(state,state.playerX,state.playerZ),
                "pulling a float off its pressure plate restores low tide and converts it to a crate on the dry bank");
            state=PullFixture(height:2);Q(state,3,3).prop=PropKind.Tide;
            Check(HabitatRules.PullPlayer(state,-1,0)&&Q(state,3,3).coveredProp==PropKind.Tide&&state.tideActive&&state.waterLevel==1&&state.tideEverActivated,
                "pulling onto a pressure plate activates tide when the retreat remains safe");
            state=PullFixture(height:2);Q(state,3,3).prop=PropKind.Tide;Q(state,2,3).height=1;
            Reject(state,()=>HabitatRules.PullPlayer(state,-1,0),"a pull that would flood its own retreat after activating tide");
            state=PullFixture(height:2);Q(state,4,3).coveredProp=PropKind.Tide;Q(state,2,3).prop=PropKind.Float;Q(state,2,3).height=0;
            state.waterLevel=1;state.tideActive=true;state.tideEverActivated=true;
            Check(HabitatRules.PullPlayer(state,-1,0)&&state.playerX==2&&state.waterLevel==0&&HabitatRules.Height(state,2,3)==1,
                "retreat onto a float is checked at its lowered height after releasing tide");
            state=PullFixture(height:2);Q(state,4,3).coveredProp=PropKind.Tide;
            Q(state,5,5).prop=PropKind.Crate;Q(state,5,5).coveredProp=PropKind.Tide;
            state.waterLevel=1;state.tideActive=true;
            Check(HabitatRules.PullPlayer(state,-1,0)&&state.tideActive&&state.waterLevel==1&&Q(state,5,5).coveredProp==PropKind.Tide,
                "another occupied pressure plate keeps tide active after the pulled crate leaves its plate");

            RecoveryChecks();
            return "PASS: "+assertions+" box mechanic assertions (exclusive climbing, dry falls, pull, atomic recovery, carrying, floats, roads and tide)";
        }
    }
}
