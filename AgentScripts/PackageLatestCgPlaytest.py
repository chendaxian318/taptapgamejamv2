from pathlib import Path
import json, hashlib, zipfile

root=Path(__file__).resolve().parents[1]
ev=root/'Verification/latest-cg-campaign'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
build=read(ev/'build-report.json')
qa=read(ev/'player-verified/result.json')
specialties=read(ev/'player-visible-specialties/result.json')
assert build['result']=='Succeeded' and build['errors']==0
assert qa['originalSavesUnchanged'] and not qa['errors'],qa
assert any(c['name']=='campaign' and c['pass'] for c in qa['checks']),qa
assert all(c['pass'] or (c['name']=='input' and 'FileNotFoundException' in c['report'] and 'legacy-fishing-fixture.json' in c['report']) for c in qa['checks']),qa
assert specialties['pass'] and specialties['originalSavesUnchanged'] and not specialties['errors'] and specialties['performanceScenarios']==11,specialties
folder=Path(build['output'])
assert (folder/'HabitatIsland.exe').is_file()
for item in read(ev/'tested-binary-hashes.json'):
    with Path(item['Path']).open('rb') as f:
        assert hashlib.file_digest(f,'sha256').hexdigest().lower()==item['Hash'].lower()
instructions='''栖屿 · 最新规则与 CG 试玩版

完整解压 ZIP，进入文件夹后双击 HabitatIsland.exe。请保留 EXE、UnityPlayer.dll、HabitatIsland_Data 等文件之间的目录关系，无须安装 Unity。

本版更新
正式 UI 切件、入场/第一关通关/回家三段 CG；竹排一次滑至障碍前；钓鱼结束收竿；工具选择检查实际库存；家园仅保留木码头并拉近镜头。
所有在场动物同时适居才算通关，部分适居动物可提前采集、返港出售，再买物资回岛继续。

常用操作
WASD 移动；鼠标选格并转向；E/互动提示交互；Ctrl+后退方向拉箱或打捞。
Shift+方向攀爬固定攀爬块；I 背包；B 在岛上联系商人采购。
Z 撤销；长按 Backspace 重开当前检查点；Esc 菜单/关闭，垂钓时可取消。
空格暂停/恢复生态；钓鱼时按住/松开空格或鼠标左键控制收线。
岛上小船返航，家园小船选关；交付、出售和休息在家园进行。

本版验证
独立 EXE 自动控制器从真实新游戏连续跑五关、返航交付和结局，未赠送装备、金币或完成标记；资金来自实际繁衍、捕捞、售卖。生态等候使用测试内部加速，钓鱼和动作按真实帧运行，不等同于人工完整试玩。
实际通过范围以随包“本版检查说明.txt”为准。测试存档和内部 QA 路线不随包分发，普通启动不执行自动测试。
建议策划从新游戏检查谜题难度、提示、购买节奏和画面；有旧存档时先保存需要保留的进度。
'''
(folder/'开始试玩.txt').write_text(instructions,encoding='utf-8-sig')
(folder/'问题反馈模板.txt').write_text('版本（ZIP 文件名）：\n关卡/任务：\n新游戏还是读档：\n当前工具/资金：\n复现步骤：\n期望表现：\n实际表现：\n能否撤销/重开恢复：\n截图/录像/Player.log：\n',encoding='utf-8-sig')
lines=[f'构建：{folder.name}',f'Unity：{build["unity"]}；构建错误：0；警告：{build["warnings"]}', '']
lines += [c['name']+': '+c['report'] for c in qa['checks'] if c['name']=='campaign']
lines += [c['name']+': '+c['report'] for c in specialties['checks']]
lines += ['', '主线与专项在同一未修改的 EXE 上分别验证。首次专项遗漏旧版钓鱼测试样本，补齐后全部专项重跑通过；初测记录仍保留。']
lines += ['', '运行错误：0。真实玩家存档 SHA256 未变。', '上述为自动化测试，不能替代策划对节奏、美术和交互体验的判断。']
(folder/'本版检查说明.txt').write_text('\n'.join(lines),encoding='utf-8-sig')
archive=root/'Builds'/('栖屿_新版规则CG试玩版_'+folder.name.removeprefix('Windows_Playtest_')+'.zip')
assert not archive.exists()
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6,allowZip64=True) as z:
    for p in sorted(folder.rglob('*')):
        if p.is_file() and not any('ButDontShipItWithYourGame' in s for s in p.parts):
            z.write(p,Path(folder.name)/p.relative_to(folder))
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert len([n for n in z.namelist() if '/StreamingAssets/StoryCG/' in n and n.endswith('.mp4')])==3
with archive.open('rb') as f:sha=hashlib.file_digest(f,'sha256').hexdigest()
delivery={'zip':str(archive),'bytes':archive.stat().st_size,'sha256':sha,'crcVerified':True,'build':build,'campaignQa':str(ev/'player-verified'),'specialtyQa':str(ev/'player-visible-specialties')}
(ev/'delivery.json').write_text(json.dumps(delivery,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(delivery,ensure_ascii=False,indent=2))
