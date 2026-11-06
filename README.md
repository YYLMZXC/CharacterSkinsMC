# CharacterSkinsMC

Minecraft 皮肤 → Survivalcraft（生存战争）皮肤转换工具。

## 功能

- 读取 64×64 Minecraft 皮肤（Classic / Slim 3px 手臂，自动识别或手动切换）
- 支持合成第二层（帽子 / 外套）到图集
- 转换为 Survivalcraft `.scskin` 图集，输出到指定目录即可被游戏读取
- 预览：MC 皮肤平面图（MC 分区）、SC 图集（SC 男/女分区参考线）、MC 3D 立方体预览、SC 专属 3D 预览（真实 HumanMale/HumanFemale.dae 模型，自动旋转速度可调 5–180 度/秒，默认 30）
- SC 图集对比：转译结果 / 任意内置默认皮肤 / 差异高亮三栏并列，显示差异像素数与相似度（灰=一致，红=不同）
- 内置 Survivalcraft 全部 8 张默认人物皮肤（男 4 / 女 4）：按性别切换、查看图集、在 SC 3D 预览中试穿、导出 `.scskin`
- 批量转换输入目录中的全部 PNG 皮肤
- 输入目录 `CharacterSkinsMC` 与输出目录 `CharacterSkins` 自动创建；也可把 PNG 直接拖入文件列表

## UV 分区（两套互不混用）

- **MC 分区**（`McUvLayouts`）：Minecraft 64×64 双层皮肤布局，以 `MinecraftSkin.GetFaceRect` 为唯一事实源；蓝框为基础层、橙框为第二层，仅用于「MC 皮肤」页。
- **SC 分区**（`ScUvLayouts`）：Survivalcraft 原版 64×64 图集区块，绿框用于「SC 图集」「SC 默认皮肤」「SC 图集对比」页，按性别维护两套表：
  - 男（Walter）：头部 16×16，躯干/手臂高 24，腿部自 Y=40 起；
  - 女（Doris）：头部与男一致，躯干/手臂高 20，腿部自 Y=36 起。
- `ScSkinAtlas.Regions` 是「MC 盒面 → SC 图集」的转译落点，只供 `MinecraftSkinConverter` 生成像素，**不是**原版 UV 分区，也不参与任何参考线绘制。
- 「SC 图集」页按左侧「SC 模型性别」切表；「SC 默认皮肤」页按选中皮肤的性别切表；对比页的对照皮肤列按其自身性别切表，状态栏会显示实际使用的表名。
- `--dump` 额外导出 `preview-sc-uv-male.png` 与 `preview-sc-uv-female.png`，`--smoke` 会校验两套表的坐标、边界与渲染互异性。

## 版本号

版本号按**构建时间**自动生成，格式 `年.月.日-时分`（如 `26.11.06-1421`）：

- 由根目录 `src/CharacterSkinsMC/Directory.Build.props` 在每次构建时以 MSBuild 属性生成，写入程序集的 `InformationalVersion`
- 桌面端：主界面工具栏「关于」按钮弹窗展示
- Android 端：同时写入 APK 的 `versionName`，可在系统应用信息中查看；应用内点「关于」会在状态栏显示

## 构建

依赖 .NET 11 SDK（含 Android workload）。工具位于主仓 `tools/` 下，用自身的 `src/CharacterSkinsMC/Directory.Packages.props` 关闭中央包管理，避免还原时向上命中主仓配置而触发 `NU1008`。

```powershell
# 桌面 + Android 双目标
dotnet build src/CharacterSkinsMC/CharacterSkinsMC.App/CharacterSkinsMC.App.csproj -f net11.0
dotnet build src/CharacterSkinsMC/CharacterSkinsMC.App/CharacterSkinsMC.App.csproj -f net11.0-android
```

## 项目结构

| 项目 | 说明 |
| --- | --- |
| `CharacterSkinsMC.Core` | 纯托管转换核心（PNG 编解码用 StbImageSharp / StbImageWriteSharp，无 System.Drawing 依赖） |
| `CharacterSkinsMC.App` | Avalonia UI（桌面 net11.0 + Android net11.0-android 单项目模式） |
| `CharacterSkinsMC.Core/Assets` | 内嵌的 8 张默认皮肤 PNG 与男女人物 DAE 模型 |
