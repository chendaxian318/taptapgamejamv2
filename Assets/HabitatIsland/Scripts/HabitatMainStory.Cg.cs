using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace HabitatIsland
{
    public sealed partial class HabitatMainStory
    {
        VideoPlayer cgPlayer;
        AudioSource cgAudio;
        GameObject cgCanvas;
        RawImage cgImage;
        AspectRatioFitter cgAspect;
        Action cgCompleted;
        float cgPrepareStarted;
        public bool IsCgPlaying => cgPlayer != null && IsPlaying;
        public string CurrentCg { get; private set; } = "";
        public VideoPlayer CgPlayer => cgPlayer;

        public bool BeginCg(string key, Action completed)
        {
            string path=Path.Combine(Application.streamingAssetsPath,"StoryCG",key+".mp4");
            if(IsPlaying||!game||!view||!File.Exists(path)||!view.BeginCinematic())return false;
            IsPlaying=true;IsPaused=false;CurrentCg=key;CurrentShot="CG/"+key;
            CurrentCaption="";cgCompleted=completed;cgPrepareStarted=Time.realtimeSinceStartup;
            try
            {
                cgCanvas=new GameObject("Story CG "+key,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
                cgCanvas.transform.SetParent(transform,false);
                var canvas=cgCanvas.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=90;
                var backdrop=new GameObject("CG letterbox",typeof(RectTransform),typeof(Image));backdrop.transform.SetParent(cgCanvas.transform,false);
                var rect=backdrop.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                backdrop.GetComponent<Image>().color=Color.black;backdrop.GetComponent<Image>().raycastTarget=false;
                var screen=new GameObject("CG original aspect",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));screen.transform.SetParent(backdrop.transform,false);
                cgImage=screen.GetComponent<RawImage>();cgImage.raycastTarget=false;
                cgAspect=screen.GetComponent<AspectRatioFitter>();cgAspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent;cgAspect.aspectRatio=16f/9f;
                cgAudio=cgCanvas.AddComponent<AudioSource>();cgAudio.playOnAwake=false;cgAudio.spatialBlend=0;
                cgPlayer=cgCanvas.AddComponent<VideoPlayer>();cgPlayer.playOnAwake=false;cgPlayer.isLooping=false;
                cgPlayer.source=VideoSource.Url;cgPlayer.url=path;cgPlayer.renderMode=VideoRenderMode.APIOnly;
                cgPlayer.waitForFirstFrame=true;cgPlayer.skipOnDrop=false;cgPlayer.timeUpdateMode=VideoTimeUpdateMode.UnscaledGameTime;
                cgPlayer.audioOutputMode=VideoAudioOutputMode.AudioSource;cgPlayer.controlledAudioTrackCount=1;cgPlayer.SetTargetAudioSource(0,cgAudio);
                cgPlayer.prepareCompleted+=CgPrepared;cgPlayer.loopPointReached+=CgEnded;cgPlayer.errorReceived+=CgError;
                cgCanvas.SetActive(true);cgPlayer.Prepare();return true;
            }
            catch(Exception e){Debug.LogError("[Story CG] "+e.Message);Cancel();return false;}
        }
        void CgPrepared(VideoPlayer player)
        {
            if(!IsCgPlaying||player!=cgPlayer)return;
            cgAspect.aspectRatio=player.height>0?(float)player.width/player.height:16f/9f;
            cgImage.texture=player.texture;
            if(!IsPaused)player.Play();
        }
        void CgEnded(VideoPlayer player){if(player==cgPlayer&&IsCgPlaying)CompleteCg();}
        void CgError(VideoPlayer player,string message)
        {
            if(player!=cgPlayer)return;
            // Do not award or advance on a playback failure. Skip remains an explicit action.
            CurrentCaption="CG 播放失败，可跳过继续。";
            Debug.LogWarning("[Story CG] "+message);
        }
        void TickCg()
        {
            if(!IsCgPlaying)return;
            if(cgPlayer.isPrepared&&cgPlayer.texture&&cgImage.texture!=cgPlayer.texture)cgImage.texture=cgPlayer.texture;
            if(!cgPlayer.isPrepared&&Time.realtimeSinceStartup-cgPrepareStarted>15)CurrentCaption="CG 加载较慢，可跳过继续。";
            var audio=HabitatAudio.Instance;
            if(cgAudio){cgAudio.mute=audio&&audio.Muted;cgAudio.volume=audio?audio.EffectsVolume:.5f;}
        }
        void CompleteCg()
        {
            if(!IsCgPlaying)return;
            var completed=cgCompleted;IsPlaying=IsPaused=false;ClearCg();Cleanup(true);completed?.Invoke();
        }
        void ClearCg()
        {
            cgCompleted=null;CurrentCg="";
            if(cgPlayer){cgPlayer.prepareCompleted-=CgPrepared;cgPlayer.loopPointReached-=CgEnded;cgPlayer.errorReceived-=CgError;cgPlayer.Stop();}
            cgPlayer=null;cgAudio=null;cgImage=null;cgAspect=null;
            if(cgCanvas){cgCanvas.SetActive(false);Destroy(cgCanvas);}cgCanvas=null;
        }
    }
}
