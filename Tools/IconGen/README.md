# IconGen（图标生成器）

把 `UiIcon.cs` 里的矢量几何渲染成 `Assets/Icon/harpkit.ico`（9 个尺寸：16/20/24/32/40/48/64/128/256，PNG 负载）。

## 重建命令

```powershell
dotnet run --project .\Tools\IconGen\IconGen.csproj -c Release
```

默认输出到 `Assets\Icon\harpkit.ico`；也可以显式指定路径：

```powershell
dotnet run --project .\Tools\IconGen\IconGen.csproj -c Release -- D:\somewhere\harpkit.ico
```

## 三份图标数据必须保持一致

| 位置 | 作用 |
| --- | --- |
| `Assets/Icon/harpkit.svg` | 设计源（viewBox 0 0 64 64，可被任意 SVG 工具打开） |
| `UiIcon.cs` | 程序内矢量渲染（标题栏 Logo、窗口/任务栏图标） |
| `Assets/Icon/harpkit.ico` | EXE 文件图标（`<ApplicationIcon>`），由本工具生成 |

改图标时改前两个文件的 path 数据（两边逐字相同），再运行本工具覆盖 `.ico`。
`UiIcon.Validate()` 会用 `Geometry.Parse` 校验每一条 path，几何写错会直接报错而不是静默画错。

本工具不参与主程序与测试的常规构建（主 csproj 已 `Compile Remove="Tools/**/*.cs"`）。
