from pathlib import Path
r=Path('Assets/HabitatIsland/Scripts')
def edit(n,a,b):
 p=r/n;s=p.read_text(encoding='utf-8-sig');assert a in s,(n,a);p.write_text(s.replace(a,b),encoding='utf-8')
edit('HabitatMainStory.cs','public bool BeginOpening() => BeginSequence(false);','public bool BeginOpening() => BeginCg("cg00",()=>Completed?.Invoke(false));')
edit('HabitatMainStory.cs','public bool BeginMorning() => BeginSequence(true);','public bool BeginMorning() => BeginCg("cg03",()=>Completed?.Invoke(true));')
edit('HabitatMainStory.cs','if (chapterSequence == ChapterSequence.None)\n            { if (IsPaused)', 'if (IsCgPlaying) { if(IsPaused)cgPlayer.Pause(); else if(cgPlayer.isPrepared)cgPlayer.Play(); }\n            else if (chapterSequence == ChapterSequence.None)\n            { if (IsPaused)')
edit('HabitatMainStory.cs','            ClearChapterSequence();','            ClearCg();\n            ClearChapterSequence();')
edit('HabitatMainStory.cs','if (IsPlaying && chapterSequence == ChapterSequence.None) Complete();','if (IsPlaying && !IsCgPlaying && chapterSequence == ChapterSequence.None) Complete();')
edit('HabitatMainStory.cs','            if (chapterSequence != ChapterSequence.None) { CompleteChapterSequence(); return; }','            if(IsCgPlaying){CompleteCg();return;}\n            if (chapterSequence != ChapterSequence.None) { CompleteChapterSequence(); return; }')
edit('HabitatMainStory.Chapter.cs','''        void Update()
        {
            if (!IsPlaying''','''        void Update()
        {
            if(IsCgPlaying){TickCg();return;}
            if (!IsPlaying''')
edit('HabitatGame.cs','            Hud.Refresh();\n            TickSafety();','''            if(!Busy && !Fishing.IsOpen && State.levelIndex==5 && State.won && !State.firstClearCgSeen && !Hud.ModalOpen && Story)
            {
                Hud.ClosePanels();
                Story.BeginCg("cg01",()=>{State.firstClearCgSeen=true;CaptureRetryPoint();});
            }
            Hud.Refresh();
            TickSafety();''')
p=r/'HabitatHud.Harbor.cs';s=p.read_text(encoding='utf-8');start=s.index('        void BuildStoryControls()');end=s.index('        bool RefreshStoryControls()',start)
s=s[:start]+'''        GameObject storyTask;
        void BuildStoryControls()
        {
            var root=Rect("Main story controls",canvas.transform,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            storyPanel=root.gameObject;
            var layer=storyPanel.AddComponent<Canvas>();layer.overrideSorting=true;layer.sortingOrder=110;
            storyPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var task=Rect("Story task prompt",root,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-22),new Vector2(760,100));
            storyTask=task.gameObject;
            HabitatFinalUi.Art(task,"R20",Vector2.zero,new Vector2(760,100));
            storyCaption=Text(task,"",19,ink,new Vector2(36,-22),new Vector2(688,60),TextAlignmentOptions.Center,true);
            var controls=Rect("Story playback buttons",root,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,74),new Vector2(238,42));
            var pause=Button(controls,"暂停",Vector2.zero,new Vector2(110,38),()=>
            {if(game.Story&&game.Story.IsPlaying)game.Story.TogglePause();RefreshStoryControls();},storyControl:true);
            pause.name="Pause main story";storyPauseLabel=pause.GetComponentInChildren<TMP_Text>();storyPauseLabel.fontSize=14;
            var skip=Button(controls,"跳过",new Vector2(122,0),new Vector2(110,38),()=>
            {if(game.Story&&game.Story.IsPlaying)game.Story.Skip();RefreshStoryControls();nextRefresh=0;},storyControl:true);
            skip.name="Skip main story";skip.GetComponentInChildren<TMP_Text>().fontSize=14;
            storyPanel.SetActive(false);
        }

'''+s[end:]
s=s.replace('            storyState.text=$"第 {Mathf.Max(1,game.State.day)} 天 · "+(game.Story.IsPaused?"片段已暂停":"片段播放中");','            storyTask.SetActive(!game.Story.IsCgPlaying || !string.IsNullOrEmpty(game.Story.CurrentCaption));')
s=s.replace('game.Story.IsPaused?"继续片段":"暂停片段"','game.Story.IsPaused?"继续":"暂停"');p.write_text(s,encoding='utf-8')
print('CG player / transaction hooks / compact story controls connected')
