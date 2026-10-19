开场海鸟制作脚本（暂存，未写入Assets，未执行Unity）

执行方式：在Unity编辑模式加载BuildSeabird.cs并调用BuildSeabird.Run()。
输出：Resources/Models/Story/Seabird.prefab，SeabirdAssets内独立网格、5个不透明材质、Flight_Loop.anim及AnimatorController。

形体：流线躯体、完整头颈、分上下喙、眼与高光、后收脚趾、5片扇形尾羽；左右各肩/肘/腕3个关节，羽毛后缘、黑色翼尖与白斑。面向+X以匹配现有剧情路径。
运动：1.1秒循环，肩部先振、肘腕延迟，尾羽与身体轻微跟随；无位移RootMotion。
接入：既有MainStory的Resources/Models/Story/Seabird读取入口会自动使用新prefab；InstantiateStoryVisual已经登记Animator，在剧情暂停/继续时同步speed，取消和跳过由现有临时对象清理。不需新增运行时脚本或修改故事路径。
安全：构建脚本拒绝Play模式，保留既有资产GUID，不触碰场景/规则/交互；模型不带Collider。
性质：程序制作的完整可替换模型，配色复用现有海岛象牙白/青灰/暖黄；没有声称美术海鸟原画已交付或定稿。

需Unity验证：构建完成、零空网格/非法法线；正侧俯视轮廓；自然循环接缝；3只海鸟原路径可见；Timeline暂停翼动画冻结，恢复继续；跳过/取消不遗留临时对象。
离线语法验证：2026-10-18，使用本机 Unity 6000.6.5f1 的 UnityEngine/UnityEditor 程序集，PowerShell Add-Type 编译通过。未运行Unity API。
BuildSeabird.SyntaxCheck2.dll仅是离线编译产物；Unity中请加载BuildSeabird.cs源码，不要导入此DLL。

后续复核：三视图已人工查看，形体与黑翼尖可辨；Prefab根保持Y=90度，InstantiateStoryVisual未重置它，原路径父节点Z摆动不会改变向前+X的基本方向。Flight_Loop的10条编辑曲线端点/切线闭合，静态报告在Verification/latest-plan-20261018/seabird/static-audit.json。
运行验鸟：Play模式空闲时加载VerifySeabirdRuntime.cs并await VerifySeabirdRuntime.Run()。使用真实帧与真实MainStory/TogglePause/Cancel，检查模型实例、动画姿态、循环、bounds、暂停恢复与清理，然后恢复内存快照/Undo。报告输出seabird/runtime.txt。此脚本已离线编译，尚未由本子任务执行。
