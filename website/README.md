# PulseLink 官方网站

原生 HTML / CSS / JavaScript 网站，七个章节以整屏滚动呈现。支持手机阅读、操作手册 PDF、开放通知 SDK 说明、试用授权说明，以及动态 OTA / 安装包和官方通知插件目录。

此目录只包含公开官网与帮助资料，不包含 PulseLink 应用源码、私钥、令牌、客户数据或私有 Sites 托管身份。

## 本地查看与验证

在仓库根目录运行静态服务器，例如：

```sh
python3 -m http.server 8000 --directory website
```

打开 `http://localhost:8000`。动态模块需 HTTP / HTTPS 访问；不要通过 file:// 打开。

网站本身没有依赖安装或构建步骤。验证工具单独存放在 `website-tests/`：

```sh
npm ci --prefix website-tests
cd website-tests
npx playwright install --with-deps chromium
cd ..
npm test --prefix website-tests
```

已装 Chromium 时，可通过 `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` 指定其路径。测试包含整屏导航、平滑翻屏、滚轮防连跳、320–1920 像素布局、私有会话配置读取、下载分类与异常处理、官方目录签名链、公共帮助页和 PDF 下载。目录与发行测试使用冻结的公开响应作为输入，不依赖 CI 当时的最新版本或外部可用性。

## GitHub Pages

`.github/workflows/website.yml` 会在官网变更的 PR 中检查；合并至 `release` 后，若 Pages 已配置为 GitHub Actions，则部署 `website/`。

首次启用：

1. 在仓库 **Settings → Pages → Build and deployment** 选择 **GitHub Actions**。
2. 确认 `github-pages` 环境允许从 `release` 分支部署。
3. 合并官网 PR 后，或在 `release` 上手动运行 **Website checks and Pages**。
4. 以部署任务实际返回的 URL 为准；默认项目路径为 `/pulselink-extension/`，网站使用相对资源路径兼容该路径。

Pages 未配置时，检查仍执行，部署明确跳过，不会将跳过报告为已经上线。官网 PR 不会合并插件包，也不修改任何已签名目录或 OTA 发布索引。插件工作流只监听自己的工作流文件及插件相关路径，避免官网流程变化触发发行。

## 下载源与首次安装 / OTA

`release-config.json` 指定公开仓库、`release` 分支、`stable` 通道及 `win-x64` 平台。网页在进入下载章节时读取；用户可重新检查。外域 GitHub 请求不带登录凭据，同源配置保留正常会话，兼容私有审核环境与公开 Pages。

- **首次安装**：只选正式、非预发布 Release 中 uploaded 的 `PulseLink-Setup.exe`。标签支持 `setup-stable-win-x64-{version}` 或 `v{version}`，版本按数值排序；可附同名 `.sha256` 文件。此命名是官网识别约定，不会自动创建安装程序或改动产品打包器。
- **OTA**：从 `ota/stable/win-x64/latest.release.json` 读取稳定通道，并定位同仓库的 `ota-stable-win-x64-{version}` / `PulseLink-{version}-win-x64.ota.zip`。不使用全局 releases/latest，不把 OTA 当作首次安装 EXE。
- API 可用时交叉核对资产；API 受限时仍可从有效官方稳定索引导出同一受控地址。索引不可读或字段不合法时停止提供 OTA 链接。签名、完整文件摘要、最低版本与平台兼容性最终由已安装客户端检查。
- 官网不扫描访问者电脑，也不执行更新。用户选择下载用途，客户端决定适用性。

读取登录失效、限流、超时和 HTTP 错误时分别提示；失败不会静默沿用旧下载链接。GitHub 网络不可用时保留官方发布记录入口。

## 通知能力、SDK 与官方目录

主屏介绍可通过插件实现的通知类型，不将所有通知服务宣传为内置能力。开发与交付指引为 [developer.html](developer.html)，官方插件公开使用说明在 `guides/`。

官方插件列表按需读取 `notifications/index.json` → 发行方 → 插件索引，选择每款最新稳定版。`plugin-catalog-config.json` 明确官方发行方 ID，并包含与项目信任注册表一致的公开验签公钥；它不含私钥。Web Crypto 检查各级 ECDSA-P256-SHA256 签名和父级 SHA-256 摘要。校验失败清空列表并允许重试。

`documentationPaths` 将已维护的官方插件帮助映射到公开官网页面，避免引用私有产品源码仓库。新插件可随公开发行元数据提供可访问的 HTTPS 帮助，或补充本站帮助页。第三方发行方不会自动标成官方；ZIP 安装与信任授权仍由宿主完成。

## 维护资料

- `assets/PulseLink-User-Guide.pdf`：v0.1 快速入门手册，5 页；后续更新 PDF 时同步页面页数、版本与日期。
- Logo 与 favicon 来自项目现用图标，保持原始品牌图形。
- 试用额度依据当前项目实现：100 点位、100 条规则、30 天热历史保留；当前未设固定试用到期日。999 台设备是基础容量。
- 产品能力变化、正式安装包发布、公钥轮换、官方发行方新增及试用规则变更，应同步对应页面和配置。不要手改已签名插件目录来更新官网文案。
