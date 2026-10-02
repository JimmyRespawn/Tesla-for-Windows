# 原始 Model 3 资产

本目录保存未修改的源模型 `tesla_2018_model_3.glb` 与原始 `license.txt`，供 `../build-model3.mjs` 重新构建运行时模型。源资产不随应用内容打包。

## 来源与许可证

- 原作：**Tesla 2018 Model 3**。
- 作者：[Ameer Studio](https://sketchfab.com/uchiha.321abc)。
- 原模型页面：https://sketchfab.com/3d-models/tesla-2018-model-3-5ef9b845aaf44203b6d04e2c677e444f
- 许可证：[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)，要求署名，允许修改和分发。
- 下载镜像：https://github.com/gotree94/Tesla_Control_UI/tree/main/Tesla_2018_Model3
- 镜像归档：`Tesla-2018-Model-3-1.z01` 至 `.z06`，以及 `Tesla-2018-Model-3-1.zip`。
- 源 GLB 大小：22,671,680 字节。
- 源 GLB SHA-256：`d6d78c9bd1bd9c7ca87a07509a2e7b6585995fcbdec054f297167ba7c15cd878`。

源 GLB 的 `asset.extras` 保留作者、原作链接和 CC BY 4.0 许可证。2026-10-01 核验原作者页面的公开元数据、GLB 内嵌信息及归档原 `license.txt`，三者一致。源模型包含 301 个节点、176 个网格、58 种材质和 22 张贴图，没有预烘焙动画。

## 重建与适配

在应用项目目录运行：

```powershell
node Tools/build-model3.mjs
```

脚本使用同目录的 `model3-asset.mjs` 读写 GLB，先检查源文件摘要，再规范化单位与坐标方向、适配车身尺寸及材质。原始纹理图像和 UV 保留；后视镜部件按连通块分离并添加支点，左侧尾灯区域切出充电口盖，另补充示范充电插座和蓝色接口灯。

输出为 `../../Assets/Model3/model3.glb`，22,940,516 字节、688,395 个三角形、200 个节点和 195 个网格。输出与本目录源文件分别保存。动画由场景代码实时驱动，没有烘焙到 GLB；模型资产本身不调用 Tesla API，真实控制由原生页面提供。

分发适配模型时保留 `Assets/Model3/CREDITS.md`、`MODEL-LICENSE.txt` 及资产内嵌署名，并注明上述改动。Three.js 的许可独立保存在 `Assets/Model3/vendor/LICENSE`。
