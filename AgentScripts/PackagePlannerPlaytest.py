from pathlib import Path
import hashlib
import json
import zipfile
import argparse

root = Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--evidence')
parser.add_argument('--optimized',action='store_true')
args=parser.parse_args()
evidence = root / (args.evidence or (root / 'Verification/current-planner-build.txt').read_text(encoding='utf-8-sig').strip())
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
build = read(evidence / 'build-report.json')
qa = read(evidence / 'player-verified/result.json')
assert build['result'] == 'Succeeded' and build['errors'] == 0, build
assert qa['pass'] and qa['originalSavesUnchanged'] and not qa['errors'], qa
assert qa['performanceScenarios'] == 11, qa
folder = Path(build['output'])
assert (folder / 'HabitatIsland.exe').is_file()
assert (folder / 'UnityPlayer.dll').is_file()
assert (folder / 'HabitatIsland_Data').is_dir()
for item in read(evidence / 'tested-binary-hashes.json'):
    with Path(item['Path']).open('rb') as f:
        assert hashlib.file_digest(f, 'sha256').hexdigest().lower() == item['Hash'].lower(), item['Path']

instructions = '''栖屿 · 策划玩法验证版

一、开始试玩
完整解压 ZIP 后，进入文件夹，双击 HabitatIsland.exe。
EXE、UnityPlayer.dll、HabitatIsland_Data 等文件必须保持在一起；不需要安装 Unity。
建议从“新游戏”验证当前完整流程。有旧存档时，开始新游戏前请先保存需要保留的进度。

二、常用操作
WASD：移动；鼠标点击地块：选择并转向；E / 互动提示：交互。
Ctrl + 后退方向：后拉箱子，符合条件时打捞低处或水中箱子。
Shift + 方向：攀爬固定攀爬块；普通地块不能直接向上攀爬。
I：背包；B：关内联系商人购买；交付任务与出售请返回家园港口。
Z：撤销；Esc：菜单/关闭窗口。钓鱼与收鱼期间 Esc 可取消。
空格：暂停/恢复生态；钓鱼时按住/松开空格或鼠标左键控制收线。
岛上小船用于返航，家园港口小船用于选关。

三、本次更新
钓鱼抛竿、收线、鱼出水入篓表现增强；入篓动作结束才结算。
修复收鱼时人物跟随鱼转向而突然扭身的问题。
修复开场海鸥中途消失/跳位，缩小 E / CLICK 互动提示。

四、建议策划验证
1. 从新游戏、开场与委托开始，逐关完成五个主线岛，检查返航、交付、解锁与结局。
2. 第一关交付两条琉璃鱼，奖励 100 贝币；检查后续物资购买与资金是否会卡住。
3. 检查推拉、打捞、浮箱搭路、固定攀爬块、竹排、风箱、背篓、铲子、磁铁和发射器。
4. 检查第三关寄居蟹、第四关金鱼/水母与潮汐、第五关工具分支；确认替代解法和失败后恢复。
5. 检查钓鱼动作衔接、取消、渔获入篓后库存变化，以及保存/继续、重开和撤销。
6. 主线结束后检查两张自由探索地图，记录提示不清、操作困难及节奏问题。

五、验证范围
本包构建成功后，在该独立 EXE 上运行了输入、鼠标朝向、打捞/竹排、工具、正常繁衍和 11 个场景性能检查。
完整五关与两张自由探索图的连续通过记录来自此前版本；本轮没有重新执行整线通关，不将旧记录冒充本包全通证明。
新增钓鱼与开场改动另有工程内专项检查。当前 UI 延续现有版本，正式封面、开场插画及高清透明切件仍待替换。
程序检查不能替代策划对谜题解法、提示和经济节奏的试玩判断。

六、反馈
请按“问题反馈模板.txt”记录。游戏日志通常位于：
%USERPROFILE%\\AppData\\LocalLow\\HabitatLab\\栖屿 · 玩法实验室\\Player.log
若发生问题，退出后及时复制日志，下次启动可能覆盖它。
'''
(folder / '开始试玩.txt').write_text(instructions, encoding='utf-8-sig')
(folder / '问题反馈模板.txt').write_text('''玩法验证反馈
版本：本 ZIP 文件名
关卡/任务阶段：
新游戏还是读档：
人物位置/朝向（或截图）：
当前道具与库存：
复现步骤：
期望表现：
实际表现：
是否必现：
能否撤销/重开恢复：
截图、录像或 Player.log：
''', encoding='utf-8-sig')
perf = read(evidence / 'player-verified/performance.json')
summary = ['本次独立运行版专项检查', f'构建：{folder.name}', f'Unity：{build["unity"]}；构建错误：0；警告：{build["warnings"]}', '']
summary += [c['name'] + ': ' + c['report'] for c in qa['checks']]
summary += ['', '运行 Error/Exception/Assert：0；真实玩家存档 SHA256 前后未变。', '1080p 场景采样（每个场景预热后 300 帧，包含锁帧；不代表低配电脑表现）：']
summary += [f'地图 {p["level"]} / {p["view"]}：平均 {p["meanFps"]:.2f} fps，P95 {p["frameMilliseconds"]["p95"]:.2f} ms，>33.34ms {p["framesOver33ms"]}/300 帧。' for p in perf]
summary += ['', '本轮为专项回归，不是重新执行完整剧情通关；测试存档和内部测试资料未随包分发。']
(folder / '本版检查说明.txt').write_text('\n'.join(summary) + '\n', encoding='utf-8-sig')

if args.optimized:
    assert len(read(evidence / 'asset-checks.json'))==18
    fish=(evidence/'fishing/recording.txt').read_text(encoding='utf-8-sig')
    assert 'PASS original save SHA256 unchanged' in fish and 'FAIL ' not in fish
    old=read(root/'Verification/planner-playtest-20261018_192436/delivery.json')
    details='''体积优化说明
13 张道具贴图采用 BC7 压缩；保持原 4K/2K 分辨率、颜色通道、UV、网格、骨骼和动作文件。
5 张 UI 整图使用 BC7，关闭屏幕界面不需要的多级缩略层，保留尺寸、切片坐标和布局。
旧静态龙虾移出 Resources，工程仍保留原件；游戏使用现有动画龙虾。
码头 8K 贴图、其他已优化模型及原有玩法保持不变。
ZIP 使用更高压缩档。原始设计图和 GLB 文件未覆盖。
本轮另复验钓鱼抛竿、入篓后结算、取消、读档、撤销、重复回调和水母捕捞。
BC7 是有损格式，做了纹理和游戏截图检查，不宣称逐像素无损。
'''
    details+=f'构建体积：原 {old["build"]["bytes"]/1e9:.2f} GB → 当前 {build["bytes"]/1e9:.2f} GB。\n'
    (folder/'体积优化说明.txt').write_text(details,encoding='utf-8-sig')
prefix='栖屿_策划试玩精简版' if args.optimized else '栖屿_策划试玩版'
archive = root / 'Builds' / f'{prefix}_{folder.name.removeprefix("Windows_Playtest_")}.zip'
assert not archive.exists(), archive
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6 if args.optimized else 1, allowZip64=True) as z:
    for p in sorted(folder.rglob('*')):
        if p.is_file() and not any('ButDontShipItWithYourGame' in s for s in p.parts):
            z.write(p, Path(folder.name) / p.relative_to(folder))
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert not any('ButDontShipItWithYourGame' in n for n in z.namelist())
with archive.open('rb') as f:
    sha = hashlib.file_digest(f, 'sha256').hexdigest()
manifest = {'zip': str(archive), 'bytes': archive.stat().st_size, 'sha256': sha, 'crcVerified': True, 'build': build, 'qa': str(evidence / 'player-verified')}
(evidence / 'delivery.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(manifest, ensure_ascii=False, indent=2))
