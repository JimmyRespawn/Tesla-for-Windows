# Model 3 模型署名与改动

本目录的 `model3.glb` 基于 **Tesla 2018 Model 3**，作者为 [Ameer Studio](https://sketchfab.com/uchiha.321abc)。

- 原作：https://sketchfab.com/3d-models/tesla-2018-model-3-5ef9b845aaf44203b6d04e2c677e444f
- 许可证：[Creative Commons Attribution 4.0 International（CC BY 4.0）](https://creativecommons.org/licenses/by/4.0/)
- 下载镜像：https://github.com/gotree94/Tesla_Control_UI/tree/main/Tesla_2018_Model3
- 原始 GLB：`tesla_2018_model_3.glb`，22,671,680 字节。
- 原始 SHA-256：`d6d78c9bd1bd9c7ca87a07509a2e7b6585995fcbdec054f297167ba7c15cd878`
- 原作者的授权文字保存在本目录 `MODEL-LICENSE.txt`；源 GLB 内嵌署名和许可证也保留在输出资产中。

原作者页面、原始 GLB 内嵌来源和下载归档的 `license.txt` 已于 2026-10-01 核验，均指向相同模型和 CC BY 4.0 授权。使用、修改或分发该模型时，应保留作者、原作及许可证信息，并说明改动。

## 本项目改动

- 将源网格统一到米制坐标及场景方向，并适配车身尺寸。
- 调整珍珠白车漆、玻璃、轮毂、灯光等材质；保留 22 张原始贴图及纹理坐标。
- 按网格连通块分离两侧后视镜的镜壳、镜面和镀铬部件，添加折叠支点。
- 从左侧尾灯区域分出可掀起的充电口盖，添加示范插座、触点和蓝色接口灯。
- 添加场景代码驱动的充电口及后视镜动画；GLB 没有预烘焙动画片段。

运行时模型为 22,940,516 字节，包含 688,395 个三角形、200 个节点和 195 个网格。模型动画本身只改变本地资源；真实车辆控制由原生页面连接 Tesla API，演示模式不发送指令。

模型及适配不是 Tesla 官方 CAD。Tesla、Model 3 是 Tesla, Inc. 的商标；本项目独立制作，不表示 Tesla 对本项目的认可。

本目录 `vendor/` 的 Three.js 0.180.0 采用 MIT 许可证，完整许可见 `vendor/LICENSE`。

`vendor/Reflector.js` 为 Three.js 官方 r180 平面反射组件，保留原代码并使用同一 MIT 许可证。来源：https://github.com/mrdoob/three.js/blob/r180/examples/jsm/objects/Reflector.js 。本项目 `studio.js` 添加本地灯光环境、柔和阴影、模糊且渐淡的地面倒影，并缓存静止视角的反射。

此前的原生模拟交通轻量化邻车衍生自本目录的已授权 Model 3：保留外部车身、玻璃、后视镜、轮毂、轮胎和灯组，删除内饰与贴图依赖，按独立面板简化后分组共享几何，以灰色 PBR 材质显示。较高的跨界车轮廓在运行时抬高座舱，轮胎及轮眉保持原位，是演示衍生外形，并非官方 Model Y／SUV 模型。原作者、来源及 CC BY 4.0 署名继续适用。

当前邻车已换为独立的 Quaternius Cars Pack 灰模，采用 CC0 授权；详细来源及改动说明见 ../Traffic/CREDITS.md。
