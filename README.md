# Tesla for WinUI

从 UWP 迁移的 WinUI 3 桌面客户端，使用 .NET 8、Windows App SDK 和 HelixToolkit。
保留原页面与文件夹结构，原生 3D 页面支持车辆状态展示、实车控制和本地驾驶模拟。

## 本机配置

打开 `Tesla for WinUI.slnx`，使用 Visual Studio 的 x64 / Debug 配置，启动
`Tesla for WinUI (Package)` 项目。

首次获取源码后，在仓库根目录运行：

```powershell
pwsh -File Tools/Prepare-LocalConfig.ps1
```

该脚本仅创建缺失的本地文件，不覆盖已有配置。

| 公开文件（提交 Git） | 本机文件（忽略） | 内容 |
| --- | --- | --- |
| `Tesla for WinUI/Tesla for WinUI/TeslaCredentials.example.json` | 同目录 `TeslaCredentials.local.json` | Tesla OAuth、命令代理地址、可选坐标转换 |
| `Tesla for WinUI/Tesla for WinUI/StoreConfiguration.example.json` | 同目录 `StoreConfiguration.local.json` | 商店订阅与应用产品编号 |
| `Tesla for WinUI/Tesla for WinUI (Package)/Package.appxmanifest` | 同目录 `Package.local.appxmanifest` | 包身份、发布者、版本等打包信息 |
| `Tesla for WinUI/Tesla for WinUI (Package)/PackageSigning.example.props` | 同目录 `PackageSigning.local.props` | 可选的本机签名设置 |

构建默认优先使用本地清单；缺少本地清单时使用公开开发清单。公开清单采用通用
开发身份，与正式商店身份分开。签名模板仅供需要发布签名时手动复制和配置。

实际凭据只填写在 `*.local.json`，修改后重新构建。登录令牌、车辆缓存和用户偏好
由 Windows 应用本地存储管理，不作为源文件提交。公共演示数据位于 `Assets/TestData`，
已经脱敏；本机原始数据备份位于被忽略的 `.local/TestData.original/`，不进入安装包资源。

## 提交前检查

在 PowerShell 7 中运行：

```powershell
pwsh -File Tools/Test-RepositoryPrivacy.ps1
```

脚本检查本地配置、签名文件、商店关联、构建产物、API 数据备份是否被 Git 忽略，
并比对候选文本文件中是否出现已知的本机私有值。输出只包含文件名和问题类别。
如果仓库已经初始化，还会检查是否有被忽略但已经被 Git 跟踪的文件。
新增凭据或接口响应样本后，应再次运行检查。

`.gitignore` 保留源码、公开配置模板和三个可移植发布配置；排除 `.vs`、`.build`、
`.local`、`bin`、`obj`、`AppPackages`、证书与私钥、用户配置和临时诊断结果。
本地构建会将 local 配置嵌入程序，生成的 DLL、EXE、MSIX 不作为源码提交。

## 公开构建

在 Visual Studio 开发者终端中使用：

```powershell
MSBuild.exe 'Tesla for WinUI.slnx' /t:Rebuild /restore /p:Platform=x64 /p:Configuration=Debug /p:UseLocalConfiguration=false /p:AppxPackageSigningEnabled=false /p:GenerateAppxPackageOnBuild=true /p:AppxBundle=Never
```

`UseLocalConfiguration=false` 会跳过本地凭据、商店配置、私有清单和签名设置。
这样生成的公共演示包不含本机凭据；实车登录与商店产品功能需要另行配置。
日常本机调试不设置该参数，继续使用已有 local 文件。

## 中文资源与模型

中文统一使用 `Strings/zh-Hans`，英文使用 `Strings/en-US`。旧 `zh-Hans1` 中独有的
61 条翻译已合并后删除，后续新增翻译只维护这两个目录。

迁移说明见 [MIGRATION.md](MIGRATION.md)，3D 功能及验证记录见 [MODEL3-3D.md](MODEL3-3D.md)。
模型来源与授权分别见应用的 `Assets/Model3/CREDITS.md` 和 `Assets/Traffic/CREDITS.md`。

## 首页地图

首页使用随应用打包的 MapLibre GL JS 与 OpenFreeMap 全球底图，无需地图 Key 或订阅。
支持拖动、缩放、应用深浅主题、车辆位置与附近充电站标记、站点选择和返回车辆位置。
车辆及站点数据继续来自 Tesla API；地图底图需要联网，失败时可以重试。
应用内不提供地址搜索或路线计算，原有搜索框与路线入口已移除。
选择附近充电站后，可以点击“在高德中查看路线”，在默认浏览器打开驾车路线页，
以车辆位置为起点、站点为终点，无需开发者 Key。缺少车辆位置时需要先刷新地图。
发给高德的起终点从本应用的 WGS84 坐标转换为 GCJ-02；地图里的点位保持原样。
高德页面可能要求登录或手动完成验证，网页路线显示由高德提供。

底图使用 WGS84 坐标。默认不转换 Tesla 返回值；仅在确认某个中国区接口返回 GCJ-02
而导致位置偏移时，在 CN 本地配置中将对应的 VehicleCoordinateConversion（车辆）或
ChargingCoordinateConversion（充电站）设置为 Gcj02ToWgs84。两个接口独立配置，避免重复转换。
库版本、授权、数据署名和网络来源见应用 Assets/Maps/CREDITS.md。
