using System;
using System.Collections.Generic;
using System.IO;

namespace HabitatIsland
{
    // Validate candidates before cancelling a live action or replacing any live state.
    public static class HabitatSaveValidation
    {
        static void Require(bool okay) { if (!okay) throw new InvalidDataException("存档数据不完整或超出有效范围。"); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static void Validate(HabitatState s)
        {
            Require(s != null);
            Require(s.width > 0 && s.depth > 0 && s.width <= 128 && s.depth <= 128);
            Require(s.tiles != null && s.tiles.Length == (long)s.width * s.depth && s.animals != null && s.animals.Count <= 4096);
            Require(s.levelIndex >= 0 && s.levelIndex < HabitatLevels.Names.Length);
            Require(HabitatRules.Inside(s,s.playerX,s.playerZ) && HabitatRules.Inside(s,s.dockX,s.dockZ));
            Require(s.waterLevel >= -64 && s.waterLevel <= 64 && s.tideBaseLevel >= -64 && s.tideBaseLevel <= 64);
            Require(Finite(s.elapsed) && s.elapsed >= 0 && Finite(s.movementClock) && Finite(s.playSeconds));
            Require(Finite(s.harborX) && Finite(s.harborZ) && Finite(s.harborFacingX) && Finite(s.harborFacingZ));
            Require(Math.Abs(s.harborX) <= 10000 && Math.Abs(s.harborZ) <= 10000);
            Require(Enum.IsDefined(typeof(PropKind),s.carryingProp));
            foreach (var t in s.tiles)
                Require(t != null && t.height >= -64 && t.height <= 64 && t.rotation >= 0 && t.rotation <= 3
                    && Enum.IsDefined(typeof(PropKind),t.prop) && Enum.IsDefined(typeof(PropKind),t.coveredProp)
                    && Enum.IsDefined(typeof(ChestReward),t.chestReward));
            var ids = new HashSet<int>(); int carried = 0;
            foreach (var a in s.animals)
            {
                Require(a != null && a.id >= 0 && ids.Add(a.id) && Enum.IsDefined(typeof(Species),a.species));
                Require(HabitatRules.Inside(s,a.x,a.z) && HabitatRules.Inside(s,a.homeX,a.homeZ));
                Require(Finite(a.progress) && Finite(a.moveClock) && a.progress >= 0 && a.offspring >= 0 && a.spawnedTotal >= 0);
                if(a.roamAnchored)Require(HabitatRules.Inside(s,a.roamX,a.roamZ));
                if (a.carried) { carried++; Require(a.id == s.carryingAnimal); }
            }
            Require(s.carryingAnimal == -1 ? carried == 0 : carried == 1 && ids.Contains(s.carryingAnimal));
            Require(s.coins >= 0 && s.cargo >= 0 && s.fish >= 0 && s.shrimp >= 0 && s.traps >= 0
                && s.shells >= 0 && s.glassFish >= 0 && s.jellyfish >= 0 && s.shovels >= 0 && s.magnets >= 0 && s.launchers >= 0
                && s.boatCrates >= 0 && s.boatCrates <= 3);
            if (s.bagProps != null) { Require(s.bagProps.Count <= 4096); foreach (var p in s.bagProps) Require(Enum.IsDefined(typeof(PropKind),p)); }
            if (s.goals != null) foreach (var p in s.goals) Require(Enum.IsDefined(typeof(Species),p));
            if (s.earnedSpecies != null) foreach (var p in s.earnedSpecies) Require(Enum.IsDefined(typeof(Species),p));
            // Fields absent in old saves are normalized only on this detached candidate.
            if (s.events == null) s.events = new List<string>();
            if (s.earnedSpecies == null) s.earnedSpecies = new List<Species>();
            if (s.goals == null) s.goals = new Species[0];
            HabitatJourneyRules.Normalize(s);
        }
        public static void Validate(Tuning t)
        {
            Require(t != null && Finite(t.breedSeconds) && t.breedSeconds > 0 && Finite(t.moveSeconds) && t.moveSeconds > 0);
            Require(t.shrimpRadius >= 0 && t.shrimpRadius <= 128 && t.friendlyRadius >= 0 && t.friendlyRadius <= 128);
            Require(t.minWater >= 0 && t.minPond >= 0 && t.minHighland >= 0);
        }
    }
}
