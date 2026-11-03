# 五子棋 · Avalonia

从原 WPF 五子棋迁移，采用 .NET 10 和固定版本 Avalonia 11.3.12。包含共享界面、桌面入口和 Android 入口；不再依赖 WPF。

## 功能与界面

- 保留 15×15 棋盘、黑白先后手、七档电脑难度、计时、悔棋、胜负判定与电脑最近落子标记。
- 手机竖屏采用棋盘在上、设置在下的布局；窄屏和横屏可纵向滚动。宽度达到 820 时切换到左右布局。
- 棋盘按可用宽度等比缩放，棋盘四周留有边距，边缘棋子完整显示。按钮和选择框最小高度为 48。
- 松手落子，拖动滚动不会落子；电脑在后台搜索。重新开始、悔棋或离开界面会取消旧搜索，旧结果不会写入新棋局。
- 默认炼狱难度，可自行切换其他难度。采用迭代加深、Alpha-Beta 剪枝、局面缓存和四条线增量估值；炼狱的搜索预算为 900 毫秒，普通开局和直接胜负会提前返回。
- 应用内置 Noto Sans CJK SC 常规和粗体字体，标题、回合和按钮使用同一套中文字体，避免系统字体回退缺字。字体许可证位于 `src/WUZHIQI/Assets/Fonts/LICENSE.txt`。
- 开始／重新开始和悔棋按钮采用 20 字号粗体，并在按钮内水平、垂直居中。
- 电脑在前中期优先争胜，识别双成五点和带空位的进攻形状。实际落子达到 180 子后放宽防守，203 子后进一步给玩家保留成五机会。让步只影响合法落点的选择，不改已有棋子和胜负规则；如果棋形已没有成五空间，仍可能和棋。
- Android 15 及以上应用系统栏与刘海安全边距。旋转屏幕由同一个 Activity 处理。进程被系统终止后不恢复对局，当前版本不含存档功能。
- Android 绘图库使用 SkiaSharp 3.119.4，兼容 16 KB 内存页；调试 APK 内嵌全部运行程序集，可直接安装，无需电脑部署额外文件。

## 运行桌面版

安装 .NET 10 SDK，在此目录运行：

```powershell
dotnet run --project .\src\WUZHIQI.Desktop
```

也可以在 Visual Studio 中打开 `WUZHIQI.slnx`，将 `WUZHIQI.Desktop` 设为启动项目。只运行桌面项目不需要 Android 环境。

## 生成可安装 APK

需要 .NET 10 SDK、.NET Android 工作负载、JDK 21，以及含 Android API 36 平台、platform-tools 和对应 build-tools 的 Android SDK。可通过 Visual Studio 的 Android 开发组件和 Android SDK 管理器配置，或使用 Android Studio SDK 管理器安装 SDK。

首次安装工作负载（可能需要管理员终端）：

```powershell
dotnet workload install android
```

设置环境变量 `ANDROID_HOME`、`JAVA_HOME` 后双击 `build-apk.cmd`，或运行：

```powershell
.\build-apk.cmd
```

本次工作目录已下载并配置所需环境，直接双击即可。`android-build.local.json` 保存本机 SDK、JDK 和 NuGet 缓存路径，脚本会自动读取；该文件不包含在源码压缩包中。在其他电脑上请设置环境变量或传入实际路径。Debug 签名文件自动保存在 `artifacts/keys/debug.keystore`，请保留它以便覆盖安装后续调试版本。

也可明确指定路径（以下路径请替换为实际安装位置）：

```powershell
.\build-apk.cmd -AndroidSdkDirectory "D:\Android\Sdk" -JavaSdkDirectory "D:\Java\jdk-21"
```

默认生成 **ARM64 Debug 已签名 APK**，适合个人安卓手机测试安装，输出到：

```text
artifacts/apk/Debug/android-arm64/*-Signed.apk
```

模拟器可传 `-Runtime android-x64`；32 位 ARM 手机可传 `-Runtime android-arm`。安装到手机时允许该来源安装应用，或用 `adb install -r <APK路径>`。

## 正式签名 APK

先使用 JDK 自带 keytool 创建自己的签名文件，按提示设置密码，并妥善备份：

```powershell
keytool -genkeypair -keystore wuzhiqi.keystore -alias wuzhiqi -keyalg RSA -keysize 2048 -validity 10000
```

在当前终端设置 `WUZHIQI_KEY_PASSWORD`、`WUZHIQI_STORE_PASSWORD` 环境变量，再运行：

```powershell
.\build-apk.cmd -Configuration Release -KeyStore .\wuzhiqi.keystore -KeyAlias wuzhiqi
```

密码通过环境变量传给 Android 构建工具。脚本不会把密码写入项目或命令参数。正式包位于 `artifacts/apk/Release/android-arm64`。Debug 与正式签名不同，切换签名安装前需要卸载旧包。

构建脚本参考 [Avalonia Android 发布说明](https://docs.avaloniaui.net/docs/deployment/android)。

## 项目结构

```text
src/WUZHIQI/                 共享 Avalonia 界面与独立棋局、电脑搜索
src/WUZHIQI.Desktop/         Windows/Linux/macOS 桌面入口
src/WUZHIQI.Android/         Android Activity、Manifest、主题、图标
tests/WUZHIQI.Checks/        不依赖 UI 的规则和搜索检查
tests/WUZHIQI.LayoutChecks/  实际界面渲染、布局和交互检查
build-apk.ps1                APK 构建与签名脚本
build-apk.cmd                Windows 双击入口
verify-apk.ps1               APK 签名、16 KB ZIP 对齐和 Manifest 校验
```

## 验证

```powershell
dotnet run --project .\tests\WUZHIQI.Checks
dotnet run --project .\tests\WUZHIQI.LayoutChecks -- .\artifacts\previews
```

迁移时已通过桌面项目编译（0 警告、0 错误）、规则/搜索检查，以及 320×640、390×844、844×390、1040×800 的实际布局渲染检查。界面检查还验证了落子、后台电脑应战、悔棋和计算期间重开。桌面依赖中的 Tmds.DBus.Protocol 固定为修复版本 0.21.3。

已下载并配置 Temurin JDK 21.0.13+9、Android API 36、Build Tools 36.0.0、Platform Tools 37.0.1 和 Android 命令行工具。SDK、JDK 及依赖缓存保存在本项目的 `.android-toolchain`，不更改系统级环境变量。

ARM64 和 x64 Debug APK 均已实际构建成功；ARM64 APK 已通过 v1/v2/v3 签名校验、Manifest 读取、16 KB ZIP 对齐，以及全部 295 个 ELF 库的内存段对齐检查。也确认游戏和 Android 入口程序集包含在 APK 中。

可随时重新校验：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify-apk.ps1
```

当前无连接的安卓手机。模拟器硬件加速检查未通过，已尝试 Android 16 软件模拟器；设备运行验证结果见输出目录中的 `APK验证报告.md`。安装包校验通过不代表已经完成真机测试。

这是独立迁移版本；原 `D:\WorkSpace\WPF\WUZHIQI` 因未获写入权限而保持原样。建议将本目录放在原项目旁，作为新的 Avalonia 版本打开，避免把旧 WPF 的入口文件混入共享项目。
