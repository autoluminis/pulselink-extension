import {readOfficialCatalog, catalogPath} from './plugin-catalog-core.mjs';
import {validateConfig} from './release-core.mjs';
const dialog = document.getElementById('plugins-dialog');
const list = document.getElementById('plugin-catalog-list');
const status = document.getElementById('plugin-catalog-status');
const refresh = document.getElementById('refresh-plugins');
const tabs = [...document.querySelectorAll('[data-plugin-tab]')];
let loaded = false;
let loading = false;
async function getBytes(url) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 10000);
  try {
    const response = await fetch(url, {credentials:new URL(url, location.href).origin === location.origin ? 'same-origin' : 'omit', cache:'no-store', signal:controller.signal, headers:{Accept:'application/json'}});
    if (!response.ok || Number(response.headers.get('content-length')) > 2097152) throw new Error('Catalog request failed');
    const bytes = new Uint8Array(await response.arrayBuffer());
    if (!bytes.length || bytes.length > 2097152) throw new Error('Invalid catalog size');
    return bytes;
  } finally { clearTimeout(timer); }
}
async function getJson(url) { return JSON.parse(new TextDecoder('utf-8', {fatal:true}).decode(await getBytes(url))); }
function node(tag, text, className) { const element = document.createElement(tag); if (text) element.textContent = text; if (className) element.className = className; return element; }
function externalLink(label, href, className) { const a = node('a', label, className); a.href = href; a.target = '_blank'; a.rel = 'noopener'; return a; }
function renderItem(item, base, documentationPaths = {}) {
  const card = node('article', null, 'catalog-plugin');
  const top = node('div', null, 'catalog-plugin-top');
  top.append(node('span', '官方发行', 'badge'), node('span', 'v' + item.version, 'catalog-version'));
  card.append(top, node('h4', item.name), node('p', item.description, 'catalog-description'));
  const meta = node('div', null, 'catalog-plugin-meta');
  meta.append(node('span', '发行方：' + item.providerName), node('span', '宿主 ≥ ' + item.minimumHostVersion + (item.maximumHostVersionExclusive ? ' 且 < ' + item.maximumHostVersionExclusive : '')), node('span', '安装包 ' + (item.packageSize / 1024).toFixed(1) + ' KB'));
  card.append(meta);
  const actions = node('div', null, 'catalog-plugin-actions');
  actions.append(externalLink('下载插件 ZIP ↓', base + item.packagePath, 'button small'));
  const localDoc = documentationPaths[item.pluginId];
  const publicDoc = typeof localDoc === 'string' && /^guides\/[A-Za-z0-9_-]+\.html$/.test(localDoc) ? new URL(localDoc, location.href).href : item.documentationUrl;
  if (publicDoc) actions.append(externalLink('使用说明 ↗', publicDoc, 'text-link'));
  card.append(actions);
  return card;
}
async function loadCatalog() {
  if (loading) return;
  loading = true;
  loaded = false;
  refresh.disabled = true;
  list.replaceChildren();
  status.textContent = '正在读取并核对官方通知目录…';
  try {
    const [releaseConfig, catalogConfig] = await Promise.all([getJson(new URL('release-config.json', location.href)), getJson(new URL('plugin-catalog-config.json', location.href))]);
    validateConfig(releaseConfig);
    const base = `https://raw.githubusercontent.com/${releaseConfig.repository}/${releaseConfig.releaseBranch}/`;
    const result = await readOfficialCatalog({catalogConfig, readBytes:path => getBytes(base + catalogPath(path))});
    list.replaceChildren(...result.items.map(item => renderItem(item, base, catalogConfig.documentationPaths || {})));
    status.textContent = result.items.length ? `已加载 ${result.items.length} 款官方已发布插件，目录签名与索引摘要已核对。` : '官方目录当前没有稳定版通知插件。';
    loaded = true;
  } catch {
    list.replaceChildren();
    status.textContent = '暂时无法读取或核对官方目录，请重试，或查看官方仓库目录。';
  } finally { loading = false; refresh.disabled = false; }
}
function activateTab(name, focus = false) {
  tabs.forEach(tab => {
    const selected = tab.dataset.pluginTab === name;
    tab.setAttribute('aria-selected', String(selected));
    tab.tabIndex = selected ? 0 : -1;
    document.getElementById(tab.getAttribute('aria-controls')).hidden = !selected;
    if (selected && focus) tab.focus();
  });
  if (name === 'catalog' && !loaded && !loading) loadCatalog();
}
document.querySelectorAll('[data-open-plugins]').forEach(button => button.addEventListener('click', () => {
  document.dispatchEvent(new Event('pulselink:modal-open'));
  dialog.showModal();
  activateTab(button.dataset.openPlugins);
}));
document.getElementById('close-plugins').addEventListener('click', () => dialog.close());
dialog.addEventListener('click', event => {
  if (event.target !== dialog) return;
  const rect = dialog.getBoundingClientRect();
  if (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom) dialog.close();
});
tabs.forEach((tab, i) => {
  tab.addEventListener('click', () => activateTab(tab.dataset.pluginTab));
  tab.addEventListener('keydown', event => {
    if (!['ArrowLeft','ArrowRight','Home','End'].includes(event.key)) return;
    event.preventDefault();
    const index = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : (i + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
    activateTab(tabs[index].dataset.pluginTab, true);
  });
});
refresh.addEventListener('click', loadCatalog);
