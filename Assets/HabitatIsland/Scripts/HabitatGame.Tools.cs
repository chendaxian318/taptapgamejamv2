namespace HabitatIsland
{
    public partial class HabitatGame
    {
        public bool HasTool(int selected)
        {
            if(State==null)return false;
            switch(selected)
            {
                case 0:return true;
                case 1:return State.cargo>0||State.carryingProp==PropKind.Climb;
                case 2:case 4:return State.rod;
                case 3:return State.traps>0;
                case 5:return State.shovels>0;
                case 6:return State.magnets>0;
                case 7:return State.launchers>0;
                default:return false;
            }
        }
        public bool TrySelectTool(int selected)
        {
            if(Busy||Fishing&&Fishing.IsOpen||selected<0||selected>=ToolNames.Length)return false;
            if(!HasTool(selected)){State.message="尚未持有"+ToolNames[selected]+"，请先取得或购买。";HabitatAudio.Play(HabitatAudioCue.Error);return false;}
            tool=selected;State.message="已选择"+ToolNames[selected]+"，选择目标后按 E";return true;
        }
    }
}
