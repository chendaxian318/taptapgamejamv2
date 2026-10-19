namespace HabitatIsland
{
    public partial class HabitatGame
    {
        public HabitatState PreviewSale(Species species,int count,out bool valid)
        {
            var copy=HabitatRules.Clone(State);valid=false;
            if(Busy||StoryPlaying||!View.Harbor.NearShop(State))
            {copy.message="请在可操作时到商店旁交易。";return copy;}
            valid=HabitatRules.SellCatch(copy,species,count);return copy;
        }
        public bool SellQuantity(Species species,int count)
        {
            if(Busy)return false;
            var candidate=PreviewSale(species,count,out bool valid);
            if(!valid){State.message=candidate.message;HabitatAudio.Play(HabitatAudioCue.Error);return false;}
            return Mutate(()=>{State=candidate;return true;},HabitatActionCue.Trade);
        }
    }
}
