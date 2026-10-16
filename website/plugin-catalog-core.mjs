import {parseVersion, compareVersions} from './release-core.mjs';
const MAX_INDEX_BYTES = 2 * 1024 * 1024;
const ID = /^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$/;
function requireValue(condition, message) { if (!condition) throw new Error(message); }
function parseJson(bytes) { requireValue(bytes.length > 0 && bytes.length <= MAX_INDEX_BYTES, 'Invalid index size'); return JSON.parse(new TextDecoder('utf-8', {fatal:true}).decode(bytes)); }
export function catalogPath(path) {
  requireValue(typeof path === 'string' && path.startsWith('notifications/') && path.split('/').every(s => ID.test(s)) && !path.includes('\\'), 'Invalid catalog path');
  return path;
}
function base64Bytes(text) { requireValue(typeof text === 'string' && /^[A-Za-z0-9+/]+={0,2}$/.test(text), 'Invalid signature encoding'); return Uint8Array.from(atob(text), c => c.charCodeAt(0)); }
function httpsUrl(value) {
  if (typeof value !== 'string' || !value) return null;
  try { const url = new URL(value); return url.protocol === 'https:' && !url.username && !url.password ? url.href : null; } catch { return null; }
}
function latestStable(plugin, provider) {
  requireValue(Array.isArray(plugin.versions) && plugin.versions.length <= 1000, 'Invalid plugin versions');
  const versions = plugin.versions.filter(v => v && v.isPrerelease !== true);
  versions.forEach(v => requireValue(typeof v.version === 'string' && /^\d+\.\d+\.\d+$/.test(v.version) && parseVersion(v.version), 'Invalid plugin version'));
  const unique = new Set(versions.map(v => v.version));
  requireValue(unique.size === versions.length, 'Duplicate plugin version');
  versions.sort((a, b) => compareVersions(b.version, a.version));
  if (!versions.length) return null;
  const version = versions[0];
  const compatibility = version.hostCompatibility;
  requireValue(compatibility && parseVersion(compatibility.minimumVersion) && (!compatibility.maximumVersionExclusive || (parseVersion(compatibility.maximumVersionExclusive) && compareVersions(compatibility.maximumVersionExclusive, compatibility.minimumVersion) > 0)), 'Invalid host compatibility');
  const pkg = version.package;
  requireValue(pkg && Number.isSafeInteger(pkg.sizeBytes) && pkg.sizeBytes > 0 && pkg.sizeBytes <= 50 * 1024 * 1024 && /^[a-f0-9]{64}$/i.test(pkg.archiveSha256 || '') && pkg.signatureAlgorithm === 'ECDSA-P256-SHA256' && provider.publisherKeyIds.includes(pkg.signatureKeyId) && typeof pkg.signature === 'string' && base64Bytes(pkg.signature).length === 64, 'Invalid plugin package metadata');
  const path = catalogPath(pkg.path);
  const prefix = `notifications/providers/${provider.providerId}/plugins/${plugin.pluginId}/${version.version}/`;
  requireValue(path.startsWith(prefix) && path.slice(prefix.length).indexOf('/') < 0 && path.endsWith('.zip'), 'Package identity mismatch');
  requireValue(typeof plugin.name === 'string' && plugin.name.trim() && plugin.name.length <= 120, 'Invalid plugin name');
  const description = plugin.summary || plugin.description || '';
  requireValue(typeof description === 'string' && description.length <= 2000, 'Invalid plugin description');
  return {providerId:provider.providerId, providerName:typeof plugin.providerName === 'string' ? plugin.providerName : provider.providerId, pluginId:plugin.pluginId, name:plugin.name, description, version:version.version, minimumHostVersion:compatibility.minimumVersion, maximumHostVersionExclusive:compatibility.maximumVersionExclusive || null, packagePath:path, packageSize:pkg.sizeBytes, documentationUrl:httpsUrl(plugin.documentationUrl), sourceUrl:httpsUrl(plugin.sourceUrl), releasedAt:version.releasedAt || null};
}
/** Verify all directory signatures and parent hashes before showing official-provider metadata. */
export async function readOfficialCatalog({catalogConfig, readBytes, cryptoProvider = globalThis.crypto}) {
  requireValue(cryptoProvider?.subtle && catalogConfig && Array.isArray(catalogConfig.officialProviderIds) && catalogConfig.officialProviderIds.length > 0 && catalogConfig.officialProviderIds.length <= 8 && catalogConfig.officialProviderIds.every(id => ID.test(id)) && catalogConfig.catalogPublicKeys && typeof catalogConfig.catalogPublicKeys === 'object', 'Invalid catalog trust configuration');
  const subtle = cryptoProvider.subtle;
  const importedKeys = new Map();
  async function keyFor(id) {
    requireValue(Object.hasOwn(catalogConfig.catalogPublicKeys, id), 'Unknown catalog signing key');
    if (!importedKeys.has(id)) {
      const pem = catalogConfig.catalogPublicKeys[id];
      requireValue(typeof pem === 'string' && pem.includes('-----BEGIN PUBLIC KEY-----'), 'Invalid public key');
      const der = base64Bytes(pem.replace(/-----BEGIN PUBLIC KEY-----|-----END PUBLIC KEY-----|\s/g, ''));
      importedKeys.set(id, subtle.importKey('spki', der, {name:'ECDSA', namedCurve:'P-256'}, false, ['verify']));
    }
    return importedKeys.get(id);
  }
  async function signedIndex(path, expectedHash = null) {
    catalogPath(path);
    const [bytes, signatureBytes] = await Promise.all([readBytes(path), readBytes(path + '.sig')]);
    requireValue(bytes instanceof Uint8Array && bytes.length <= MAX_INDEX_BYTES, 'Invalid index bytes');
    if (expectedHash !== null) {
      requireValue(typeof expectedHash === 'string' && /^[a-f0-9]{64}$/i.test(expectedHash), 'Invalid parent digest');
      const actual = [...new Uint8Array(await subtle.digest('SHA-256', bytes))].map(v => v.toString(16).padStart(2, '0')).join('');
      requireValue(actual === expectedHash.toLowerCase(), 'Catalog digest mismatch');
    }
    const meta = parseJson(signatureBytes);
    requireValue(meta.schemaVersion === 1 && meta.algorithm === 'ECDSA-P256-SHA256' && typeof meta.keyId === 'string', 'Invalid signature metadata');
    const signature = base64Bytes(meta.signature);
    requireValue(signature.length === 64, 'Invalid signature length');
    const valid = await subtle.verify({name:'ECDSA', hash:'SHA-256'}, await keyFor(meta.keyId), signature, bytes);
    requireValue(valid, 'Catalog signature mismatch');
    return parseJson(bytes);
  }
  const root = await signedIndex('notifications/index.json');
  requireValue(root.schemaVersion === 1 && root.catalogId === 'notifications' && root.packageKind === 'notification-plugin' && Array.isArray(root.providers) && root.providers.length <= 100, 'Invalid notification catalog');
  const providerIds = root.providers.map(p => p.providerId);
  requireValue(providerIds.every(id => typeof id === 'string' && ID.test(id)) && new Set(providerIds).size === providerIds.length, 'Duplicate provider identity');
  const official = root.providers.filter(p => catalogConfig.officialProviderIds.includes(p.providerId));
  const pluginIds = new Set();
  const groups = await Promise.all(official.map(async entry => {
    const expected = `notifications/providers/${entry.providerId}/index.json`;
    requireValue(entry.indexPath === expected, 'Provider path mismatch');
    const provider = await signedIndex(expected, entry.indexSha256);
    requireValue(provider.schemaVersion === 1 && provider.providerId === entry.providerId && provider.packageKind === 'notification-plugin' && Array.isArray(provider.publisherKeyIds) && provider.publisherKeyIds.length > 0 && provider.publisherKeyIds.every(k => typeof k === 'string') && Array.isArray(provider.plugins) && provider.plugins.length <= 100, 'Invalid provider metadata');
    const plugins = await Promise.all(provider.plugins.map(async record => {
      requireValue(typeof record.pluginId === 'string' && ID.test(record.pluginId) && !pluginIds.has(record.pluginId), 'Duplicate plugin identity');
      pluginIds.add(record.pluginId);
      const path = `notifications/providers/${provider.providerId}/plugins/${record.pluginId}/index.json`;
      requireValue(record.indexPath === path, 'Plugin path mismatch');
      const plugin = await signedIndex(path, record.indexSha256);
      requireValue(plugin.schemaVersion === 1 && plugin.packageKind === 'notification-plugin' && plugin.providerId === provider.providerId && plugin.pluginId === record.pluginId, 'Plugin identity mismatch');
      return latestStable(plugin, provider);
    }));
    return plugins.filter(Boolean);
  }));
  return {generatedAt:root.generatedAt || null, items:groups.flat().sort((a, b) => a.pluginId.localeCompare(b.pluginId))};
}
