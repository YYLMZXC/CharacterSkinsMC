# CharacterSkinsMC

Minecraft 皮肤 → Survivalcraft（生存战争）皮肤转换工具。

## 功能

- 读取 64×64 Minecraft 皮肤（Classic / Slim 3px 手臂，自动识别或手动切换）
- 支持合成第二层（帽子 / 外套）到图集
- 转换为 Survivalcraft `.scskin` 图集，输出到指定目录即可被游戏读取
- 预览：MC 皮肤平面图、SC 图集（可选 UV 分区参考线）、3D 模型预览（可自动旋转）
- 批量转换输入目录中的全部 PNG 皮肤
- 输入目录 `CharacterSkinsMC` 与输出目录 `CharacterSkins` 自动创建；也可把 PNG 直接拖入文件列表

## 版本号

版本号按**构建时间**自动生成，格式 `年.月.日-时分`（如 `26.11.06-1421`）：

- 由根目录 `src/CharacterSkinsMC/Directory.Build.props` 在每次构建时以 MSBuild 属性生成，写入程序集的 `InformationalVersion`
- 桌面端：主界面工具栏「关于」按钮弹窗展示
- Android 端：同时写入 APK 的 `versionName`，可在系统应用信息中查看；应用内点「关于」会在状态栏显示

## 构建

依赖 .NET 11 SDK（含 Android workload）。

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
