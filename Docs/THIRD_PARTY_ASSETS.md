# 下载模型的选择与许可

本轮从 `E:/projectstudy/DownloadedAssets` 选择可直接用于立体格子的模型，不照搬旧方案完整房屋场景。模型仅替换显示，地格高度、可达性、碰撞和生态判定继续由游戏规则控制。

## 已导入的资源

| 来源 | 导入文件 | 选择理由 |
|---|---|---|
| KayKit Block Bits 1.0 Free，Kay Lousberg | `dirt_with_grass.fbx`、`sand_with_grass.fbx`、`sand_A.fbx`、`block_bits_texture.png` | 圆角立方轮廓保留棋盘格辨识度；草顶、泥土和沙地适合海岛高差与岸线。 |
| Kenney Nature Kit 2.1，Kenney | `stone_largeA.fbx`、`tree_palmShort.fbx` | 低面数石块与矮棕榈适合外围装饰，避免遮挡动物和可操作地格。 |

当前显示映射：`sand_A` 用于地下与支撑层；高度 1 的地表和土块道具用 `sand_with_grass`；高度至少 2 的地表用 `dirt_with_grass`。`tree_palmShort` 用于棋盘外两座装饰岛，`stone_largeA` 用于外围石块及障碍。

项目文件分别位于 `Assets/HabitatIsland/Resources/Models/KayKit/` 和 `Kenney/`。FBX、贴图及许可证共 8 个文件均与源包对应文件字节一致，详见 [复制核对清单](SourceEvidence/downloaded_model_manifest.json)。

KayKit 使用源包的 `Assets/fbx(unity)` 版本与 1024×1024 公用贴图；对应 OBJ 材质名为 `block`，贴图链接为 `map_Kd block_bits_texture.png`，运行时绑定到 URP Lit 的 Base Map。原始法线与 UV 保留。块体约 2 个源单位，草边和表面碎石略超出轮廓；运行时按包含 FBX 轴向与单位变换的包围盒，将每层归一到 `(0.985,0.7,0.985)` 并以底部中心对齐格子。

Kenney 石块与矮棕榈使用源包 `Models/FBX format`。对应 OBJ 材质为纯色，无额外纹理依赖：石块为 `stone`，棕榈为 `leafsGreen` 与 `woodBark`。它们以底面为原点；参考 OBJ 中石块高约 0.260、棕榈高约 1.056 个源单位，分别约 80／190 个三角形。Unity 导入后的实际包围盒仍以模型检查结果为准。

## 许可与来源

两套包自带的 `License.txt` 均标明 **CC0**，允许个人、教育与商业项目使用，署名为自愿。完整原文保留在各项目模型目录中的 `LICENSE.txt`，本项目同时记录作者来源：

- [KayKit Block Bits 官方页面](https://kaylousberg.itch.io/block-bits)，作者 Kay Lousberg。
- [Kenney Nature Kit 官方页面](https://kenney.nl/assets/nature-kit)，作者 Kenney。
- [CC0 1.0 说明](https://creativecommons.org/publicdomain/zero/1.0/)。

上述许可范围仅指这两套下载包，不能据此给用户提供的原画或其他项目资源统一标为 CC0。原压缩包与下载来源记录仍保留在 `DownloadedAssets`，未改动。
