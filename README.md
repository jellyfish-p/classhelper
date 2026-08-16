# ClassHelper 课堂助手

面向中小学教室固定电脑的 Windows 课堂辅助软件。仓库包含可运行的 `0.1 Preview` 初版实现和产品设计文档。

## 0.1 Preview

当前可用：

- WPF 主控面板；
- 固定名单的新增、删除、学号区间生成、启用/停用与本地保存；
- 独立随机和均衡轮选两种点名方式；
- 始终置顶、可拖动并自动吸边的快捷启动器；
- 当前 Windows 用户可选开机自启；
- 按 Alpha、Beta、预发行和稳定版通道自动检查更新，并在应用内下载、校验和安装；
- 本地 JSON 数据文件和原子写入。

课程表、节假日、教学日历和桌面课程栏由 ClassIsland 提供，本项目不再实现。多屏增强、SQLite 和独立安装器暂未接入。

### 运行

需要 Windows 10/11 x64 和 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)：

```powershell
dotnet restore ClassHelper.sln
dotnet build ClassHelper.sln
dotnet test ClassHelper.sln
dotnet run --project src/ClassHelper.App/ClassHelper.App.csproj
```

首次运行会生成一份可编辑的示例名单，数据保存在：

```text
%LocalAppData%\ClassHelper\classroom.preview.json
```

自动化环境可使用 `--smoke-test` 加载所有 XAML 资源并立即退出：

```powershell
dotnet run --project src/ClassHelper.App/ClassHelper.App.csproj -- --smoke-test
```

### 自动构建与发布

GitHub Actions 会在分支推送和 Pull Request 时自动执行格式检查、Release 构建、测试及 WPF 冒烟验证。

发布标签使用完整的 [SemVer 2.0.0](https://semver.org/lang/zh-CN/) 格式，并在版本号前添加 `v`。正式版本例如：

```powershell
git tag v0.1.0
git push origin v0.1.0
```

先行版本和构建元数据同样受支持：

```text
v0.1.0-alpha.1
v0.1.0-beta.2
v0.1.0-rc.1
v0.1.0-rc.1+build.42
```

Alpha、Beta 和 Preview 标签会发布为 GitHub Prerelease；RC 标签和无后缀稳定版会显示为普通 GitHub Release。RC 在客户端更新策略中仍属于“预发行”通道，不会推送给只接收 Stable 的用户。工作流会拒绝 `v01.2.3`、`v1.2`、`v1.2.3-rc.01` 等不符合 SemVer 的标签。

推送标签后会通过 GitHub Actions 原生矩阵生成六种单文件 EXE：`win-x64`、`win-x86`、`win-arm64` 分别提供内含 .NET 的自包含版和需要预装 .NET 10 Desktop Runtime 的框架依赖版。Release 同时包含每个 EXE 的 SHA-256 校验文件，以及供客户端检查更新的 `update-manifest.json`。

更新通道按稳定程度向下包含：Alpha 接收全部版本，Beta 接收 Beta、预发行和稳定版，预发行接收 RC/Preview 和稳定版，Stable 只接收稳定版。首次运行默认采用当前安装版本所属层级，也可以在设置中更改。更新检查只读取公开的发布信息，不上传本地数据。客户端优先使用 GitHub；连接失败时可回退 OSS，并在下载后校验发布清单记录的 SHA-256，再退出旧版本、就地替换和自动重启。

国内 OSS 镜像是可选配置。将仓库变量 `CLASSHELPER_OSS_BASE_URL` 设置为公开 HTTPS 根地址后，发布产物会内置该地址，更新清单也会为每个 EXE 写入 `mirrorDownloadUrl`。OSS 对象按以下结构同步：

```text
releases/<tag>/<release asset>
channels/alpha/update-manifest.json
channels/beta/update-manifest.json
channels/prerelease/update-manifest.json
channels/stable/update-manifest.json
```

版本资源只需上传一次；各通道固定路径保存该通道当前最新兼容版本的清单。未配置 OSS 时，GitHub 检查、应用内下载和安装仍会正常工作。开发环境也可以通过 `CLASSHELPER_OSS_BASE_URL` 环境变量临时覆盖镜像根地址。

也可以先在 GitHub 中发布同格式标签的 Release，工作流会自动构建并补充产物；失败时可从 Actions 页面输入已有标签手动重跑。

## 设计文档

- [领域词汇表](./CONTEXT.md)
- [产品范围](./docs/product-scope.md)
- [技术设计](./docs/technical-design.md)
- [UI/UX 设计规范](./docs/uiux/ui-ux-spec.md)
- [UI/UX 审查清单](./docs/uiux/review-checklist.md)
- [历史 UI/UX 高保真预览](./docs/uiux/previews/README.md)
- [历史 Imagine 提示记录](./docs/uiux/preview-prompts.md)
- [0.1 Preview 实现说明](./docs/implementation-v0.1.md)
- [架构决策记录](./docs/adr/)
