# 潮灯小岛 · MVP 帧动画补充包 v2

上一版每个对象只有一张静态图，不能直接组成姿态变化的帧动画。本包补充 4 组、每组 4 张独立透明 PNG，共 16 帧；文件名从 `_00` 到 `_03`。

本包配合 v1 使用，v1 的两张背景、潮晶和飞行泡泡继续保留。这里不重复打包背景，也不会覆盖 v1。

## 1. 动画内容

| 文件夹 | 帧数 | 播放建议 | 动作 |
|---|---:|---|---|
| hero_idle | 4 | 2 秒循环，首帧停留较久 | 睁眼、半闭眼、闭眼、恢复；围巾轻摆 |
| fish_swim | 4 | 6 FPS，循环 | 摆尾、胸鳍变化、苔草轻摆 |
| launcher_fire | 4 | 12 FPS，约 0.33 秒，单次 | 静止、蓄能发光、喷口回缩与发射、回弹 |
| bubble_pop | 4 | 12 FPS，约 0.33 秒，单次 | 完整泡泡、裂成弧线、散开水滴、变小消散 |

这是有姿态变化的序列帧，不是将一张图改名复制四遍。它们用于先验证 MVP 动画接入；少量形体和绘制细节会随帧变化，不等同于正式美术精修的动画。

## 2. 直接导入 Unity

1. 把本包 `Assets/TidelightMVPAnimations` 复制到你的 Unity 项目 `Assets` 中。
2. 等待脚本编译，执行 `Tools > Tidelight MVP > Create Animation Demo Assets`。
3. 菜单会导入 16 张图片为独立 Sprite，并在 `Assets/TidelightMVPAnimations/Generated` 新建 `.anim`、Animator Controller 和 4 个演示 Prefab。
4. 将主角或鱼的 Prefab 拖入场景，进入 Play Mode 后自动循环。发射器与破裂泡泡需要触发：`animator.SetTrigger("Play");`。
5. 发射器播放后返回静止帧；泡泡破裂后清空 Sprite。泡泡 GameObject 不会自动销毁，便于对象池复用。

工具仅改变本包图片的导入设置，并新建演示文件；重复执行会生成带序号的文件，不覆盖你已经修改的动画。演示 Prefab 不含鱼的移动、碰撞、耐久或资源逻辑，这些继续使用你的程序。

脚本为 Editor-only，不需要运行时插件或 2D Sprite 包。尚未在你的 Unity 项目版本里执行验证。如不使用工具，可以按下一节手动建立动画。

## 3. 手动创建帧动画

- 所有 PNG 设置为 `Sprite (2D and UI)`，`Sprite Mode = Single`。本包已拆帧，不必再对独立 PNG 设置 Multiple。
- 同一组帧设置相同 PPU、相同缩放；Mesh Type 使用 Full Rect，关闭 Mip Maps，Filter Mode 使用 Bilinear，Compression 初版使用 None。
- 按 `_00 → _01 → _02 → _03` 顺序选中图片，拖到带 SpriteRenderer 的对象或 Animation 窗口中创建 Clip。
- 鱼用 6 FPS 并开启 Loop Time。发射器和破裂泡泡用 12 FPS，关闭 Loop Time。
- 主角建议关键帧时间为 `0、1.25、1.50、1.625` 秒，2 秒回到首帧，避免持续快速眨眼。
- 单次动画应给最后一帧保留显示时长，再返回静止或隐藏；不要在第四帧刚切出时立刻销毁对象。

## 4. 轴心、尺寸与替换

每组的四张 PNG 具有相同画布尺寸，保留完整透明留白，未对每帧单独裁紧。原始动作图以 2×2 网格拆分，顺序为左上、右上、左下、右下。

为减少切帧位移，另外保存了各帧轴心：主角按脚底配准，鱼按眼部参考点推算身体中心，发射器按后部握柄配准，泡泡使用画布中心。轴心属于导入元数据，不改变原画像素。

`Assets/TidelightMVPAnimations/frame_settings.json` 中 `pivotX/pivotY` 使用 Unity 左下角原点的归一化坐标；助手会自动读取。手动导入且观察到跳动时，可照表设置 Custom Pivot。`animation_manifest.json` 同时记录帧尺寸、alpha、边界和哈希。

默认画布世界宽度：主角 2.2、鱼 1.5、发射器 1.3、破裂泡泡 1.7。PPU = 对应帧像素宽度 ÷ 画布世界宽度。根据项目调整整体大小即可。

此处破裂泡泡按“捕获圈”大小起步，和 v1 的飞行小泡泡大小不同；用于弹丸碰撞时缩小，用于包住鱼时按鱼尺寸调整。

建议把 Animator 和 SpriteRenderer 放在 `Visual` 子节点，移动／耐久／碰撞继续留在父节点。替换正式美术时主要更换 Clip 里的 Sprite，并重新确认轴心。不要让每帧图片的透明边界自动决定碰撞体大小。

发射器默认朝右（本地 +X）。发射弹丸的事件建议放在第 3 帧进入时，即约 `2 / 12 = 0.167` 秒；弹丸的生成、Muzzle 坐标与开火频率由程序控制，动画不会自动发射。

## 5. 文件说明与检查

- `Assets/TidelightMVPAnimations/Frames`：实际使用的 16 张独立 PNG。
- `SourceSheets`：4 张原始动画图集，方便核对或重新切片，项目不必导入。
- `动画预览.html`：本地动画预览，可暂停和逐帧查看，无联网依赖；保持与其他文件的相对位置。
- `PROMPTS.md`：内置 imagegen 的动作生成与透明背景提取提示词。
- `Tools`：生成结果来源与机械拆帧脚本，Unity 不需要导入。

所有动作与背景提取由 imagegen 完成；拆帧仅进行固定网格切分，未用程序伪造动作。检查项目包括真实 alpha、同组尺寸、非空帧、四帧不重复、网页播放与切帧。最终使用时仍需在你的 Game 视图确认尺寸、速度和材质。
