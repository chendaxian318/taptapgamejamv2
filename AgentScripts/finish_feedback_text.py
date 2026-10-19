from pathlib import Path
r=Path('Assets/HabitatIsland/Scripts')
p=r/'HabitatHud.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('HabitatAudio.SetDucked(ModalOpen||(game.Fishing&&game.Fishing.IsOpen));','HabitatAudio.SetDucked(ModalOpen||game.StoryPlaying||(game.Fishing&&game.Fishing.IsOpen));');p.write_text(s,encoding='utf-8')
# Expose the chapter's current actual objective after a pre-clear hand-in.
p=r/'HabitatGame.Chapter.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('case HabitatChapterStage.RestAtHome: return "委托已交付，100 贝币已到账。走到家门口按 E 休息，或与老板继续交谈后回家。";','case HabitatChapterStage.RestAtHome: return HasCompletedLevel(5)?"委托已交付，100 贝币已到账。走到家门口按 E 休息，或与老板继续交谈后回家。":"委托报酬已到账。第一关动物尚未全部适居，请补给后重返岛屿；完成环境目标后休息才会开放第二关。";');p.write_text(s,encoding='utf-8')
