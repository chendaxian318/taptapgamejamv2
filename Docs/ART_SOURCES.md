# 美术资源来源

主角当前接入用户提供的本地 Meshy 旅行者 3D 模型。海岸与动物原画仍取自 `C:/Users/陈大仙/Desktop/Asset/聚光灯草案`；先前的草帽、提灯正背面主角原画保留为模型无法加载时的备用显示。

## 主角模型与动画

| 项目 | 当前接入 |
|---|---|
| 用户原文件 | `C:/Users/陈大仙/Downloads/Meshy_AI_rigged_traveler_All_Animations.glb` |
| 项目原始模型 | `Assets/HabitatIsland/Art/MeshyTraveler/MeshyTraveler.glb` |
| 导入方式 | 项目中的 glTFast **6.20.0** 原生导入 GLB |
| 运行预制体 | `Assets/HabitatIsland/Resources/Models/MeshyTraveler/Player.prefab` |
| 动画控制器 | 同目录 `Traveler.controller`，`MoveSpeed` 驱动待机／行走／跑步 BlendTree |
| 已接入动画 | `Idle_9`、`Walking`、`Running` |

项目 GLB 与用户原文件已按字节核对一致，大小为 13,310,272 字节。原 GLB 中 8 个动画全部保留：`Running`、`Walking`、`Collect_Object`、`Idle_9`、`Ladder_Climb_Loop`、`Push_and_Walk_Forward`、`Regular_Jump`、`Armature|clip0|baselayer`。当前控制器只使用 `Idle_9`、`Walking`、`Running` 三段动画；其余动画没有绑定到采集、攀爬、推箱或跳跃等玩法触发。

Animator 关闭 Root Motion，格子规则继续决定人物位置；动画根据实际水平移动速度切换，模型随四向朝向旋转。原主角 Sprite 在模型启用时隐藏，模型加载失败时才显示备用原画。

模型、已提取的动画与预制体均在本地项目内，**无需另行下载模型、上传或重新生成**。使用 Unity 打开主场景并 Play 进行测试，本轮不打包。主角替换已通过 [56 项真实帧运行检查](../Verification/meshy-traveler-checks.txt)，覆盖动画切换、四向转身、重复换关、原箱攀爬、推箱潮汐与携带锚点；新外观见 [关卡预览](../Verification/meshy-traveler-game.png) 与 [模型近景](../Verification/traveler-preview-lit.png)。这次未接入其他互动动作，也未重跑整关通关流程。

## 原画资源

本轮未生成新图、重绘或改写原画像素。项目保留 5 份原画资源（含 2 份备用主角图），完整海港图另存为未使用素材；这 6 份文件均与对应源文件的 SHA-256 完全一致。此前原画接入的归档位置、透明通道和哈希记录见 [资源核对清单](SourceEvidence/artist_asset_manifest.json)。该清单记录原画，不包含本次 Meshy 模型。

下表中的项目资源均位于 `Assets/HabitatIsland/Resources/Art/Artist/`；原始路径相对上述 `聚光灯草案` 目录。

| 项目文件 | 原始文件（相对源目录） | 场景用途 |
|---|---|---|
| `HeroFront.png` | `part1/hona/正视图.png` | 备用主角正面；左右方向复用并镜像 |
| `HeroBack.png` | `part1/hona/背视图.png` | 备用主角背面 |
| `Water.png` | `part2/hona/地块素材.png` | 海面与水格的手绘水纹 |
| `HarborParts.png` | `part2/hona/房子场景拆件2.png` | 从透明拆件图中选取小船 |
| `Creatures.jpg` | `part1/vinda/怪物草案.jpg` | 水母、红虾、金鱼原画 |

寄居蟹继续使用来自用户 FigJam 文件的 `Assets/HabitatIsland/Resources/Art/Crab.jpg`。地块现已接入 KayKit 三种圆角方块，外围棕榈、石块和障碍使用 Kenney 模型，详见 [下载模型与许可](THIRD_PARTY_ASSETS.md)。海龟、海星暂无匹配成品图，暂用程序几何造型；机关保留可测试的几何结构。

## 原画与显示处理

- **备用主角正面**：347×382，RGB，原图为白底且带红色批注与色板。使用完整矩形 Sprite，由 `Habitat/WhiteKeySprite` shader 的 `_TrimArtistFront` 选项在显示时隐藏批注与色板；白底也在材质内处理。当前不再使用自定义 Sprite 多边形。
- **备用主角背面**：204×259，RGB 白底。运行时选取矩形区域并使用白底 shader；与正面按统一可见高度和脚底位置摆放。
- **小船**：使用原始 alpha 透明度，不做白色抠除，以保留浅色船沿。选区为原始 3909×2480 图集的 `(3294,501,466,602)`，坐标原点在左下；没有将整套房屋场景搬入棋盘。
- **水面**：原图为 3023×1659 的不透明手绘纹理，铺在 XZ 水面网格上，约 30×16.46 世界单位重复一幅。运行时采用镜像重复减轻接缝，`Habitat/QuietPaintedWater` shader 混合约 10% 原画与 90% 平静青绿色，保留笔触并降低背景对地格的干扰。
- **水母、红虾、金鱼与寄居蟹**：从原图按区域生成 Sprite。白底材质按像素亮度计算透明覆盖率，并降低半透明抗锯齿边缘中的白色底色，减轻亮边；主角白底图也使用该处理。它属于显示近似，原稿中接近纯白的浅色细节仍可能一并变淡。
- **纹理导入**：原画使用 `NPOT Scale = None`，不强制缩放为 2 的幂次尺寸；当前最大纹理尺寸为 4096，未压缩、关闭 Mip Maps、双线性过滤。现用原画均小于该上限，按原始尺寸导入，避免角色比例与图集选区因重采样而偏移。

以上均为 UV、网格、材质与导入设置，不另存抠图或覆盖源文件。原画字节保持不变，屏幕中的透明度、边缘和水面颜色由材质处理。替换正式精灵时可提供真实 alpha PNG，并保留脚底位置与可见尺寸。

## 界面与当前限制

界面配色参考原画中的米白、青绿及海港橙红。主角色板的取样值为青绿 `#64817D`、锈棕 `#93512C`、赭黄 `#B1873D`、米白 `#EADDC1`、深褐 `#413429`；它们是美术参考，界面文字需采用足够深的颜色保证阅读。

当前默认镜头采用正交投影，正对棋盘（yaw 0°）、俯角 58°、局部正交尺寸 6.5；Q／C 每次转动 90°。正式大地图增加 M 全图、Home 回到人物与中键平移，总览尺寸根据地图范围计算；本次 1920×1080 截图约为 13.04885。PC 场景采用 125% 渲染采样、4× MSAA 与 High SMAA，Overlay 界面按原生 1920×1080 绘制；正文、控件字重和对比度已提升。Unity Game 预览固定 Full HD，并以双线性过滤缩小预览。正式图画面以 [局部视图](../Verification/figma-level-local.png)和[全图视图](../Verification/figma-level-overview.png)为准，旧实验关截图保留为迭代记录。本轮只在编辑器内验证，不安排打包。

备用 Sprite 仍仅有两张静态正背面原画，左右使用正面镜像；这属于先前版本的备用表现，当前主角使用上方记录的 3D 模型与移动动画。小船原画带固定透视，作为装饰表现，不能等同于可自由环绕查看的 3D 模型。

旧方案完整 `Harbor.png` 及 `.meta` 已移至 `Docs/SourceEvidence/UnusedLegacy/`，不再作为场景内容，也不进入构建。

旧 4 张 AI `HeroIdle` PNG 及其 `.meta` 已移到 `Docs/SourceEvidence/RetiredAI/`，位于 `Assets` 之外，不进入运行资源与构建；原工作区动画包保持原样。`hero_frame_settings.original.json` 与 `README_动画接入.original.md` 仅作历史记录，不是当前角色的动画配置。

## 静态 3D 虾模型

根据用户选择，虾接入本地提供的 Meshy 静态模型；上文的红虾原画描述保留为来源记录，当前显示优先使用此 3D 模型。其他生物的显示资源不变。

| 项目 | 当前接入 |
|---|---|
| 用户原文件 | `F:/xwechat_files/wxid_8mef8sxck69x22_553c/msg/file/2026-10/Meshy_AI_Creature_Remeshed_30k_1013133822_texture.glb` |
| 项目原始模型 | `Assets/HabitatIsland/Art/MeshyShrimp/MeshyShrimp.glb` |
| SHA-256 | `917728BA77591FA3CE4C43D182B371675963B89355E8754DC6DB0418D441B30D` |
| 运行预制体 | `Assets/HabitatIsland/Resources/Models/MeshyShrimp/Shrimp.prefab` |
| 接入工具 | `Editor/HabitatShrimpSetup.cs` |
| 几何与贴图 | 1 个静态 mesh，27,205 顶点、30,252 三角面；2 张 4096×4096 PNG，分别为 baseColor 与 metallicRoughness |
| 摆放与尺寸 | 统一缩放 `0.4203701`，底部居中，`+Z` 为正面；包络约 `0.619 × 0.594 × 0.8 m` |

预制体保持原 PBR shader 与纹理，显示层负责移动和搬运时的朝向。该模型没有骨骼或动画；本轮按用户选择静态接入，未执行 UniRig，也没有新增动作动画。

此资源由用户提供，许可按原始来源及其适用条款使用，不将其标记为 CC0。已在 Unity 中确认真实帧推进，并完成 16 项模型、材质、转向、抱起放下、水位高度及状态条检查，记录见 `Verification/meshy-shrimp-runtime-checks.txt`。正式默认视角见 `Verification/meshy-shrimp-game.png`；`meshy-shrimp-close.png`、`meshy-shrimp-carry.png` 为临时放大后的 Game 画面，检查后已恢复默认镜头。控制台无编译错误或运行异常。本轮未打包。
