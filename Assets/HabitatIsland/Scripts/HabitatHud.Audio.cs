using TMPro;
using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatHud
    {
        GameObject audioPanel;
        TMP_Text ambienceVolumeLabel, effectsVolumeLabel, muteAudioLabel;

        void BuildAudioControls()
        {
            Button(settingsPanel.transform,"声音设置",new Vector2(540,-26),new Vector2(144,34),()=>Open(audioPanel)).name="Open Audio Settings";
            var card=ModalCard("Audio Settings",new Vector2(640,410),"声音设置","海浪鸟鸣与动作、界面音效可以分别调整。");
            audioPanel=card.gameObject;
            Text(card,"海浪与鸟鸣",18,ink,new Vector2(28,-138),new Vector2(310,34),emphasis:true);
            Text(card,"海岸环境声",13,muted,new Vector2(28,-176),new Vector2(310,26));
            Button(card,"−",new Vector2(420,-138),new Vector2(42,38),()=>AdjustAudio(true,-.05f)).name="Ambience volume down";
            ambienceVolumeLabel=Text(card,"",18,ink,new Vector2(464,-138),new Vector2(100,38),TextAlignmentOptions.Center);
            Button(card,"+",new Vector2(570,-138),new Vector2(42,38),()=>AdjustAudio(true,.05f)).name="Ambience volume up";
            Rule(card,28,-214,584);
            Text(card,"动作与界面",18,ink,new Vector2(28,-234),new Vector2(310,34),emphasis:true);
            Text(card,"脚步、工具、收获与界面提示",13,muted,new Vector2(28,-272),new Vector2(370,26));
            Button(card,"−",new Vector2(420,-234),new Vector2(42,38),()=>AdjustAudio(false,-.05f)).name="Effects volume down";
            effectsVolumeLabel=Text(card,"",18,ink,new Vector2(464,-234),new Vector2(100,38),TextAlignmentOptions.Center);
            Button(card,"+",new Vector2(570,-234),new Vector2(42,38),()=>AdjustAudio(false,.05f)).name="Effects volume up";
            var mute=Button(card,"",new Vector2(28,-340),new Vector2(176,42),()=>{
                var sound=HabitatAudio.Ensure();sound.SetMuted(!sound.Muted);RefreshAudioControls();
            });
            mute.name="Toggle Audio Mute";muteAudioLabel=mute.GetComponentInChildren<TMP_Text>();
            Button(card,"试听提示音",new Vector2(218,-340),new Vector2(176,42),()=>HabitatAudio.Play(HabitatAudioCue.Confirm)).name="Preview Audio Cue";
            Button(card,"返回设置",new Vector2(408,-340),new Vector2(204,42),()=>Open(settingsPanel,HabitatAudioCue.Back),primary:true,cue:null).name="Back from Audio Settings";
            audioPanel.SetActive(false);
            RefreshAudioControls();
        }

        void AdjustAudio(bool ambience,float amount)
        {
            var sound=HabitatAudio.Ensure();
            float before=ambience?sound.AmbienceVolume:sound.EffectsVolume;
            if(ambience)sound.SetAmbienceVolume(sound.AmbienceVolume+amount);
            else sound.SetEffectsVolume(sound.EffectsVolume+amount);
            if(!Mathf.Approximately(before,ambience?sound.AmbienceVolume:sound.EffectsVolume))HabitatAudio.Play(HabitatAudioCue.Confirm);
            RefreshAudioControls();
        }

        void RefreshAudioControls()
        {
            if(!ambienceVolumeLabel||!HabitatAudio.Instance)return;
            var sound=HabitatAudio.Instance;
            ambienceVolumeLabel.text=Mathf.RoundToInt(sound.AmbienceVolume*100)+"%";
            effectsVolumeLabel.text=Mathf.RoundToInt(sound.EffectsVolume*100)+"%";
            muteAudioLabel.text=sound.Muted?"取消静音":"全部静音";
        }
    }
}
