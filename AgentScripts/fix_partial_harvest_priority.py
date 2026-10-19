from pathlib import Path
r=Path('Assets/HabitatIsland/Scripts')
p=r/'HabitatGame.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('||tool!=0&&tool!=2&&tool!=4)return false;','||tool!=0&&tool!=4)return false;');s=s.replace('a.z==selectedZ&&a.species!=Species.Goldfish&&a.species!=Species.Jellyfish)))return false;','a.z==selectedZ&&a.species!=Species.Goldfish)))return false;');p.write_text(s,encoding='utf-8')
p=r/'HabitatHud.Harbor.cs';s=p.read_text(encoding='utf-8-sig').replace('storyCaption=Text(task,"",19,ink','storyCaption=Text(task,"",19,Color.white');p.write_text(s,encoding='utf-8')
p=r/'HabitatModel.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('(CanStoreCarriedShrimp(s)?1:0)','CarriedShrimpCatchCount(s)');point='        public static bool CanStoreCarriedShrimp(HabitatState s)';s=s.replace(point,'''        public static int CarriedShrimpCatchCount(HabitatState s)
        {
            if(!CanStoreCarriedShrimp(s))return 0;
            var a=s.animals.Find(v=>v.id==s.carryingAnimal);
            return s.won||!a.catchClaimed?1:a.offspring;
        }
'''+point);p.write_text(s,encoding='utf-8')
