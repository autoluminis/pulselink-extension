import {validateConfig, publicStableReleases, selectInstaller, selectOta, selectOtaFromIndex} from './release-core.mjs';
const root = document.getElementById('downloads');
const status = document.getElementById('release-status');
const title = document.getElementById('package-title');
const purpose = document.getElementById('package-purpose');
const metadata = document.getElementById('package-metadata');
const link = document.getElementById('package-download');
const copy = document.getElementById('copy-checksum');
const checksum = document.getElementById('checksum-download');
const refresh = document.getElementById('refresh-release');
let kind = 'setup';
let state = {setup:null, ota:null, setupError:false, otaError:false};
let started = false;
let loading = false;
function sizeText(size) { return (size / 1024 / 1024).toFixed(1) + ' MB'; }
function render() {
  document.querySelectorAll('[data-package]').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.package === kind)));
  const isSetup = kind === 'setup';
  title.textContent = isSetup ? 'Windows 首次安装' : 'Windows OTA 更新';
  purpose.textContent = isSetup ? '适用于尚未部署 PulseLink 的计算机，安装 EXE 包含完整部署流程与所需运行时。' : '仅适用于已经部署的 PulseLink。OTA 更新 ZIP 不能用于首次安装，需在系统内校验并应用。';
  document.getElementById('download-help-title').textContent = isSetup ? '首次使用，请选择安装程序。' : '已有部署，先检查升级兼容性。';
  document.getElementById('download-help-copy').textContent = isSetup ? '安装 EXE 与 OTA 更新包分别提供。若尚无正式安装程序，官网会保留不可用状态，不会以 OTA 替代。' : '官网显示稳定通道当前发布的 OTA；是否适用你的当前版本，由客户端结合最低版本、平台与签名校验判断。';
  link.hidden = true; link.removeAttribute('href'); metadata.hidden = true; copy.hidden = true; checksum.hidden = true; checksum.removeAttribute('href');
  refresh.disabled = loading;
  if (loading) { status.textContent = '正在读取官方发布信息…'; return; }
  if (state[kind + 'Error']) { status.textContent = state[kind + 'Error']; return; }
  const pkg = state[kind];
  if (!pkg) { status.textContent = isSetup ? '官方仓库尚无可用的首次安装程序。' : '稳定通道尚无可用的 OTA 更新包。'; return; }
  const pieces = ['版本 ' + pkg.version, sizeText(pkg.size)];
  if (pkg.minimumVersion) pieces.push('最低可升级版本 ' + pkg.minimumVersion);
  if (pkg.publishedAt && Number.isFinite(Date.parse(pkg.publishedAt))) pieces.push('发布于 ' + new Intl.DateTimeFormat('zh-CN', {timeZone:'Asia/Taipei', year:'numeric', month:'2-digit', day:'2-digit'}).format(new Date(pkg.publishedAt)));
  metadata.textContent = pieces.join(' · '); metadata.hidden = false;
  status.textContent = isSetup ? '已找到公开发行的安装程序。' : '来自官方稳定通道索引；下载后请在系统内校验并应用。';
  link.href = pkg.url; link.textContent = isSetup ? '下载首次安装程序 ↓' : '下载 OTA 更新包 ↓'; link.hidden = false;
  copy.hidden = !pkg.sha256;
  copy.textContent = '复制 SHA-256';
  if (pkg.checksumUrl) { checksum.href = pkg.checksumUrl; checksum.hidden = false; }
}
async function fetchJson(url, optional = false) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 10000);
  try {
    const requestUrl = new URL(url, location.href);
    const response = await fetch(requestUrl, {signal:controller.signal, credentials:requestUrl.origin === location.origin ? 'same-origin' : 'omit', cache:'no-store', headers:{Accept:'application/json'}});
    if (optional && response.status === 404) return null;
    if (!response.ok) { const error = new Error('Release request failed'); error.httpStatus = response.status; error.isLocal = requestUrl.origin === location.origin; throw error; }
    return await response.json();
  } finally { clearTimeout(timer); }
}
async function fetchReleases(config) {
  const all = [];
  for (let page = 1; page <= 5; page++) {
    const suffix = page === 1 ? '' : '&page=' + page;
    const chunk = await fetchJson(`https://api.github.com/repos/${config.repository}/releases?per_page=100${suffix}`);
    if (!Array.isArray(chunk)) throw new Error('Invalid release page');
    all.push(...chunk);
    if (chunk.length < 100) return all;
  }
  // An incomplete listing must not be mistaken for a confirmed absence of installers.
  throw new Error('Release listing exceeds the inspection limit');
}
function failureText(error, source) {
  if (error?.isLocal && [401, 403].includes(error.httpStatus)) return '站点配置需要有效登录，请刷新页面后重新检查。';
  if (error?.httpStatus === 403 || error?.httpStatus === 429) return source + '暂时拒绝请求或达到访问额度，请稍后重试，或查看官方发布记录。';
  if (error?.name === 'AbortError') return source + '读取超时，请重新检查，或直接查看官方发布记录。';
  if (error?.httpStatus) return source + '读取失败（HTTP ' + error.httpStatus + '），请重新检查。';
  return source + '暂时无法读取或核对，请检查网络并重试，或查看官方发布记录。';
}
async function load() {
  if (loading) return;
  started = true; loading = true; render();
  try {
    const config = validateConfig(await fetchJson(new URL('release-config.json', location.href)));
    const base = `https://raw.githubusercontent.com/${config.repository}/${config.releaseBranch}/`;
    const [releaseResult, indexResult] = await Promise.allSettled([
      fetchReleases(config),
      fetchJson(`${base}ota/${config.channel}/${config.runtimeIdentifier}/latest.release.json`, true)
    ]);
    state = {setup:null, ota:null, setupError:false, otaError:false};
    let releases = null;
    if (releaseResult.status === 'fulfilled') {
      try { releases = publicStableReleases(releaseResult.value); state.setup = selectInstaller(releases, config); }
      catch (error) { state.setupError = failureText(error, '安装包发布信息'); }
    } else state.setupError = failureText(releaseResult.reason, '安装包发布 API');
    if (indexResult.status === 'fulfilled') {
      try { state.ota = releases ? selectOta(indexResult.value, releases, config) : selectOtaFromIndex(indexResult.value, config); }
      catch (error) { state.otaError = failureText(error, '官方 OTA 索引'); }
    } else state.otaError = failureText(indexResult.reason, '官方 OTA 索引');
  } catch (error) { const message = failureText(error, '站点下载配置'); state = {setup:null, ota:null, setupError:message, otaError:message}; }
  finally { loading = false; render(); }
}
document.querySelectorAll('[data-package]').forEach(button => button.addEventListener('click', () => { kind = button.dataset.package; render(); if (!started) load(); }));
refresh.addEventListener('click', load);
copy.addEventListener('click', async () => {
  const sha = state[kind]?.sha256;
  if (!sha) return;
  try { await navigator.clipboard.writeText(sha); copy.textContent = '已复制 ✓'; }
  catch { copy.textContent = '复制未成功'; }
});
const observer = new IntersectionObserver(entries => { if (entries.some(e => e.isIntersecting) && !started) { load(); observer.disconnect(); } }, {rootMargin:'200px'});
observer.observe(root);
