# UWP → WinUI 3 迁移

源项目保留原样。WinUI 项目继续使用 `TeslaMurphy` 命名空间，保留 Controls、Converters、Helpers、Models、Services、ViewModels、Views、Styles、Strings、Assets 结构，以及原页面、对话框、样式、车辆图片和体验模式数据。打包项目也使用原应用的图标与启动图。

## 打开与运行

用 Visual Studio 打开根目录 `Tesla for WinUI.slnx`，选择 x64 / Debug，把 **Tesla for WinUI (Package)** 设为启动项目，然后启动。项目使用 .NET 8 和已安装的 Windows App SDK 2.5.1。

通过打包项目启动，确保应用具备包身份；直接运行构建目录中的 exe 不等同于启动调试包。新项目的包身份和本地存储独立于旧 UWP 应用，需要重新登录。

## 配置与 Git

配置位于 `Tesla for WinUI/Tesla for WinUI/`：

- `TeslaCredentials.example.json`：保留占位配置，可以提交到 Git。
- `TeslaCredentials.local.json`：本机实际配置，已从源项目复制，由 `.gitignore` 排除。

构建仅将 local 作为 `TeslaMurphy.TeslaCredentials.json` 嵌入应用；example 不作为实际凭据读取。其他电脑首次使用时复制 example 为 local，然后填入自己的配置并重新构建。

打包清单也分为公开 `Package.appxmanifest` 和被忽略的 `Package.local.appxmanifest`，默认构建优先选择 local。商店订阅及应用产品编号已从 C# 移至 `StoreConfiguration.local.json`，对应空白模板为 `StoreConfiguration.example.json`。可选签名设置通过 `PackageSigning.local.props` 导入，本机证书与私钥不提交。

设置 `UseLocalConfiguration=false` 可生成不包含本机凭据、商店编号和私有包身份的公开构建。完整准备步骤与提交前检查见 [README.md](README.md)。体验模式 JSON 已替换个人标识，原始文件保存在被忽略的 `.local/TestData.original/`。

首页地图无需申请 Key，使用本地 MapLibre 渲染库和 OpenFreeMap 在线底图。车辆及充电站仍读取 Tesla API；本阶段移除地址搜索、驾车路线及对应入口。

## 平台适配

- App / MainWindow 负责桌面窗口、单实例与 `teslauwp` 协议激活；MainPage 继续承载导航框架。
- 桌面标题栏与 Mica、DispatcherQueue、ContentDialog.XamlRoot、StoreContext 窗口关联均已适配。
- WebView2 在 NavigationStarting 拦截 localhost 登录回调，避免请求不存在的本地页面后遗漏授权码。
- UWP 地图改为 MapLibre GL JS + OpenFreeMap，保留车辆与充电站标记、站点定位及主题交互，并支持返回车辆位置。
- Strings 的中文目录规范为 `zh-Hans`，中英文资源显式作为 PRIResource 打包；重复的 `zh-Hans1` 中独有的 61 条翻译合并后删除。
- 关闭发布裁剪，保留现有 Newtonsoft.Json 和动态 Binding 所需类型；本次不启用 Native AOT。

## 验证与后续联调

x64 Debug 与 Release 应用和完整打包解决方案均已构建通过，生成 MSIX。体验模式运行验证覆盖主页面、所有子页面、8 个对话框、样例车辆数据和明暗主题；记录见 `migration-smoke-results.txt`。

真实 Tesla 登录与车辆指令需要使用实际账号继续联调。地图底图无需账号，车辆定位与附近充电站仍需实车联调。本次测试未向真实车辆发送指令；没有进行逐像素的视觉对比。Store 上架身份、产品关联和发行签名仍需使用新项目的正式配置。

仓库准备验证（2026-10-02）：合并后中英文各 381 条资源；公开模式与默认本地模式的 x64 Debug 重建及 MSIX 打包均通过。公开模式程序集不包含两个本地配置资源，并采用通用开发包身份；默认模式核对嵌入内容与 local 文件一致、包身份与 local 清单一致。Git 忽略规则检查和源码／资源隐私扫描通过，脱敏 JSON 格式与跨文件车辆标识一致性检查通过；既有 57 组、1017 个离线断言通过。未创建远端仓库、提交或上传。

地图适配参考：[MapLibre GL JS](https://maplibre.org/maplibre-gl-js/docs/)、[OpenFreeMap 接入说明](https://openfreemap.org/quick_start/)。

首页地图更新验证（2026-10-02）：x64 Debug 重建与最终 MSIX 打包通过。后台浏览器完成 10 项验证，覆盖上海及旧金山真实底图、三类标记、名称弹窗、主题切换后保留视角、主动定位、断网提示与重试、窄宽度布局及清空标记；没有脚本异常或旧地图请求。安装包确认包含最终地图页面、本地渲染库和授权说明。中英文各 392 条资源，键集合一致；隐私扫描通过。浏览器验证使用公开测试坐标，未进行实际 WinUI 窗口交互或实车定位联调。

外部路线入口（2026-10-02）：选中充电站后显示“在高德中查看路线”，以车辆为起点，在默认浏览器打开官方高德 URI 页面；没有车辆位置时禁用按钮并提示刷新。生产链接辅助类的 12 项检查通过，覆盖坐标转换方向、充电站还原、中文和特殊字符、不同区域性的小数格式及无效坐标。公共上海坐标在线测试成功跳转到高德路线页并带入起终点名称；高德触发登录和滑块验证，未自动处理验证，尚未确认计算后的路线。新入口提供中英文资源，各语言资源增至 396 条。
