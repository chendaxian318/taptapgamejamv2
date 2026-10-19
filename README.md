# 推推小岛 · 海风物语

Unity 游戏开发交接仓库，工程根目录就是仓库根目录。当前快照：2026-10-19。

## 接手启动

1. 安装 Git LFS，执行 `git lfs install`。
2. `git clone https://github.com/chendaxian318/taptapgamejamv2.git`
3. 进入仓库执行 `git lfs pull`。模型和图片必须是实际文件，不能是 LFS 指针文本。
4. Unity Hub 添加仓库目录，使用 **Unity 6000.6.5f1**；首次等待 Package Manager 和资源导入完成。
5. 打开 `Assets/HabitatIsland/Scenes/HabitatIsland.unity`，Play 后从主菜单开始。

不要运行 `Create Playtest Scene`，它会重建场景。首次打开不需要复制旧电脑 Library 或安装旧电脑全局 MCP 配置。Python/Blender/超分工具仅在重新制作素材时需要；正常打开已有游戏资产无需重新生成。

## 优先阅读

- [最新开发交接](Docs/开发交接_20261019.md)：当前状态、代码入口、验证边界和未覆盖内容。
- [AGENTS.md](AGENTS.md)：约定按时间追加，后面的用户确认覆盖前面的旧规则。
- [新版完整流程与试玩交付](Docs/新版完整流程与试玩交付_20261018.txt)：上一版 EXE 的完整验收范围。
- [新版规则与 CG](Docs/新版反馈与CG接入_20261018_221708.txt)。

历史 README 已保留在 Docs/README_历史20261016.md；其中旧关卡数量、奖励和未完成描述不是当前状态。

## 当前主要内容

- 五张主线关卡（索引 5/6/7/8/10）、两张额外地图（4/9），码头、船只旅行、商店及存档。
- 动物环境判定、繁衍、推拉箱、浮箱搭路、竹排滑行、攀爬、磁铁与发射器、收网及钓鱼到位结算。
- 三段正式 CG、正式 UI、两张随机启动封面。
- 10 月 19 日最新布局：主菜单文字左置并缩小 20%；对话框底部居中，老板大半身在左侧前景，正文/按钮在右侧。

## 仓库范围

包含 Assets（含 .meta）、Packages、ProjectSettings、开发脚本、历史设计资料与交接文档。大二进制使用 Git LFS。

不提交 Library、Temp、Logs、UserSettings、历史备份、导出成品、试玩 EXE/ZIP、玩家存档和本机认证配置。Verification 大量运行截图与日志不提交；文档中的本机历史证据路径可能不在仓库，不能当作新环境测试通过。

最新界面改动已在 Editor 预览，尚未重建 EXE；不能用旧试玩包验收新界面。
