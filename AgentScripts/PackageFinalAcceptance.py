from pathlib import Path
import json, hashlib, zipfile

root=Path(__file__).resolve().parents[1]
evidence=root/'Verification/final-acceptance-20261018'
qa=evidence/'player-complete'
read=lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
result=read(qa/'result.json')
assert result['pass'] and result['originalSavesUnchanged'] and not result['errors'], result
assert result['performanceScenarios']==11
build=read(evidence/'build-report.json')
folder=Path(build['output'])
for item in read(evidence/'delivery-binary-hashes.json'):
    assert hashlib.file_digest(Path(item['Path']).open('rb'),'sha256').hexdigest().lower()==item['Hash'].lower(), item['Path']
environment=read(qa/'environment.json')
perf=read(qa/'performance.json')
journey=read(qa/'journey-performance.json')
log=(qa/'campaign/full-campaign-runtime.txt').read_text(encoding='utf-8-sig')
assert 'PASS all seven latest maps passed in continuous journey' in log
lines=[
'最终通关与独立运行版验收｜2026-10-18',
f'交付目录：{folder.name}；Unity {build["unity"]}；Windows 64 位。',
'验证对象就是本次打包的 EXE，检查完成后没有再修改游戏代码或重新构建。',
'',
'一、最新规则通关复验',
'从 NewSession 开始，连续完成五个主线岛和两个自由探索岛；不是旧规则报告，也不是注入完成标记。',
'开场→第一关特制竿/围鱼→真实捕鱼与繁衍→交付两鱼100贝币→次日→出售额外鱼、买风箱→第二关背篓与携虾→第三关两把铲子及蟹出壳→售壳解锁→第四关涨潮、竹排、退潮及金鱼/水母同时适居→第五关发射器/磁铁→返航结局→两张自由探索地图→存读档。',
'七图路线分别为49/157/178/102/130/29/329条编码动作；此外还执行实际岸边/码头行走、商店、捕鱼、返航、存读档。',
'全程通过正式控制器驱动、Unity真实帧和到位结算。捕鱼按真实进度逐帧按住/松开；没有发放测试金币、装备或通关标记。',
'为缩短重复赚钱等待，繁衍段使用内部4倍生态速度；风箱验证也调用正式模拟入口推进。并非当前UI提供4倍按钮。另有独立1倍速度自然繁衍检查，证明正常时间下也可完成。',
'这些是找到的合法可行路线，不等于策划所有标准解或所有可能分支；部分路线需等额外幼体后出售，未据此宣称经济节奏最优。',
'',
'二、本次发现并修复',
'1. 新自由探索地图寄居蟹/水母误沿用旧环境判定：统一执行当前确认规则，旧来源存档保留兼容。',
'2. 竹排上按E优先划船，导致前方风箱和龙虾无法交互：改为道具/动物交互优先，空水面仍正常划船。补验箱子推动、风箱拿取、抱虾和到位结算。',
'3. 当前规则下金鱼仍提示“鱼竿引导”：改为围水适居、通关后捕捞说明；旧规则提示保留。',
'没有更改UI美术或页面布局。',
'',
'三、交付EXE专项检查',
*[f'- {c["name"]}: {c["report"]}' for c in result['checks']],
'所有运行检查使用隔离存档；真实玩家存档前后SHA256核对不变。',
'验收入口仅在显式 --habitat-acceptance 参数下启用，平常双击游戏不会运行测试或生成测试档。',
'',
'四、性能实测',
f'{environment["cpu"].strip()} / {environment["gpu"]} / {environment["ramMB"]} MB RAM；1920×1080；锁60帧。',
'以下为11个真实渲染场景，每个预热后采300帧。不能据此推算低配电脑帧率或声称无锁帧性能提升。',
]
for p in perf:
    f=p['frameMilliseconds']
    lines.append(f'地图{p["level"]} {p["view"]}: 平均{p["meanFps"]:.2f}fps，P95 {f["p95"]:.2f}ms，P99 {f["p99"]:.2f}ms，最大{f["max"]:.2f}ms；>33.34ms {p["framesOver33ms"]}/300；Unity已分配内存{p["allocatedMemory"]/1048576:.1f}MiB。')
lines+=['','实际整线动态帧记录（包含首次加载/切场景及自动化路径计算、截图、快照写盘开销，不能与预热后的静态场景混为一谈，也未用逐帧Profiler归因长帧）：']
for p in journey:
    f=p['frameMilliseconds']
    lines.append(f'{p["scenario"]}: {f["count"]}帧，平均{p["meanFps"]:.2f}fps，P95 {f["p95"]:.2f}ms，P99 {f["p99"]:.2f}ms，最大{f["max"]:.2f}ms；>33.34ms {p["framesOver33ms"]}帧。')
lines += ['', '五、画面与交付边界',
'本机1920×1080检查地图、主角、木船/码头、动物和道具材质，以及打捞收网/箱子出水/人物后退截图；未发现缺失模型或粉色错误材质。',
'UI按用户要求冻结；主封面/开场插画及高清透明切件仍待美术交付，当前主菜单白底不能算正式封面验收完成。',
'验证是自动化控制器与输入专项，加截图人工审阅，不冒称人工逐步游玩或覆盖所有操作组合。',
f'构建0错误、{build["warnings"]}条提示：开发用Pipeline运行连接未启用；已有automaticGuide字段未使用。游戏运行Error/Exception/Assert记录为0。',
'性能计数器不可用项为null，不能解释为零分配；本轮没有低配机器或其他分辨率的验收结论。',
'整线偶发长帧已如实列出；不能以接近60帧的平均值宣称完全没有卡顿。若继续做性能优化，应先用Profiler区分游戏加载与验收脚本的开销。',
'', '证据：Verification/final-acceptance-20261018/player-complete/ 下的 result.json、checks.json、performance.json、journey-performance.json、campaign/full-campaign-runtime.txt、通关快照和截图。',
'历史失败尝试保留用于定位原因，不作为最终通过证据。']
report='\n'.join(lines)+'\n'
(root/'Docs/最终通关与独立版验收_20261018.txt').write_text(report,encoding='utf-8-sig')
(folder/'验收说明.txt').write_text(report,encoding='utf-8-sig')
(folder/'开始试玩.txt').write_text('栖屿 Windows 64位试玩版\n\n请完整解压文件夹，然后双击 HabitatIsland.exe。不要只移动exe文件。\n运行不需要安装Unity，不要附加测试命令行参数。\nWASD移动，鼠标点选转向，E交互，Ctrl+方向后拉/打捞，Shift攀爬固定攀爬块，B关内联系商人，I背包，Esc暂停，空格暂停/恢复生态模拟（剧情中控制演出暂停）。\n岛上小船返航，码头小船选关。交付与出售需回码头。\n玩法及验证范围见验收说明.txt。\n',encoding='utf-8-sig')
archive=root/'Builds'/f'{folder.name}_已复验.zip'
assert not archive.exists(), archive
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=1,allowZip64=True) as z:
    for p in sorted(folder.rglob('*')):
        if p.is_file() and not any('ButDontShipItWithYourGame' in s for s in p.parts):
            z.write(p,Path(folder.name)/p.relative_to(folder))
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
sha=hashlib.file_digest(archive.open('rb'),'sha256').hexdigest()
manifest={'zip':str(archive),'bytes':archive.stat().st_size,'sha256':sha,'crcVerified':True,'build':build,'qa':str(qa)}
(evidence/'delivery.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(manifest,ensure_ascii=False,indent=2))
