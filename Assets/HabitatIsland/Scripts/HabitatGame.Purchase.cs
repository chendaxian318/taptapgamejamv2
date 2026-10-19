namespace HabitatIsland
{
 public partial class HabitatGame
 {
  public bool RemoteMerchantAvailable => State!=null && State.authoredLevel && HabitatLevels.IsFormal(State.levelIndex);
  public bool CanPurchaseHere => RemoteMerchantAvailable || View.Harbor.NearShop(State);
  public HabitatState PreviewPurchase(string item,int count,out bool valid)
  {
   var copy=HabitatRules.Clone(State);valid=false;
   if(count<1||count>99){copy.message="购买数量无效";return copy;}
   if(Busy||StoryPlaying||!CanPurchaseHere || item=="sell"&&!View.Harbor.NearShop(State)){copy.message="请在可操作时到商店旁交易";return copy;}
   for(int i=0;i<count;i++)if(!HabitatRules.Shop(copy,item))return copy;
   valid=true;return copy;
  }
  public bool BuyQuantity(string item,int count)
  {
   if(Busy)return false;
   var candidate=PreviewPurchase(item,count,out bool valid);
   if(!valid){State.message=candidate.message;HabitatAudio.Play(HabitatAudioCue.Error);return false;}
   // One confirmation is one transaction and one undo, including all selected units.
   return Mutate(()=>{State=candidate;return true;},HabitatActionCue.Trade);
  }
 }
}
