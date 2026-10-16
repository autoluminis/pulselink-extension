import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile, readdir} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {join} from 'node:path';
import {validateConfig, publicStableReleases, selectInstaller, selectOta, selectOtaFromIndex} from '../../website/release-core.mjs';
import {readOfficialCatalog, catalogPath} from '../../website/plugin-catalog-core.mjs';
const website = fileURLToPath(new URL('../../website/', import.meta.url));
const releases = JSON.parse(await readFile(new URL('../fixtures/releases.json', import.meta.url)));
const catalog = JSON.parse(await readFile(new URL('../fixtures/catalog.json', import.meta.url)));
const config = validateConfig(JSON.parse(await readFile(join(website, 'release-config.json'))));
const trust = JSON.parse(await readFile(join(website, 'plugin-catalog-config.json')));
const readBytes = path => { assert(catalog[path]); return Uint8Array.from(Buffer.from(catalog[path], 'base64')); };
test('first installation and OTA are independent, and the stable index is authoritative', () => {
  const available = publicStableReleases(releases.releases);
  assert.equal(selectInstaller(available, config), null);
  assert.equal(selectOta(releases.index, available, config).version, releases.index.version);
  assert.equal(selectOtaFromIndex(releases.index, config).url, selectOta(releases.index, available, config).url);
  assert.throws(() => selectOta({...releases.index, runtimeIdentifier:'win-arm64'}, available, config));
  assert.throws(() => selectOta({...releases.index, packagePath:'https://untrusted.invalid/update.zip'}, available, config));
  assert.throws(() => selectOta(releases.index, [], config));
  const setup = version => {const tag='v'+version;return {draft:false,prerelease:false,tag_name:tag,assets:[{name:'PulseLink-Setup.exe',state:'uploaded',size:100000,digest:'sha256:'+'A'.repeat(64),browser_download_url:`https://github.com/${config.repository}/releases/download/${tag}/PulseLink-Setup.exe`}]};};
  const candidates=[setup('26.10.16.002'), setup('26.10.16.010'), {...setup('26.10.17.001'),draft:true}];
  assert.equal(selectInstaller(publicStableReleases(candidates), config).version,'26.10.16.010');
  const poison=setup('26.10.18.001');poison.assets[0].browser_download_url='https://untrusted.invalid/a.exe';
  assert.equal(selectInstaller([poison],config),null);
});
test('official plugin directory validates real signatures, parent hashes and provider identity', async () => {
  const actual=await readOfficialCatalog({catalogConfig:trust, readBytes});
  assert.equal(actual.items.length,2);
  assert.equal(actual.items.find(x=>x.pluginId==='pulselink.wxpusher').version,'1.0.2');
  await assert.rejects(()=>readOfficialCatalog({catalogConfig:trust,readBytes:path=>{const data=readBytes(path);if(path==='notifications/index.json')data[30]^=1;return data;}}));
  await assert.rejects(()=>readOfficialCatalog({catalogConfig:trust,readBytes:path=>{const data=readBytes(path);if(path==='notifications/providers/autoluminis/index.json')data[35]^=1;return data;}}));
  await assert.rejects(()=>readOfficialCatalog({catalogConfig:{...trust,catalogPublicKeys:{}},readBytes}));
  assert.throws(()=>catalogPath('notifications/../index.json'));
  assert.throws(()=>catalogPath('https://untrusted.invalid/index.json'));
  const excluded=await readOfficialCatalog({catalogConfig:{...trust,officialProviderIds:['other-provider']},readBytes});
  assert.equal(excluded.items.length,0);
});
test('public website has no private-source documentation links or private hosting files', async () => {
  async function visit(dir) {const files=[];for(const item of await readdir(dir,{withFileTypes:true})){const full=join(dir,item.name);if(item.isDirectory())files.push(...await visit(full));else files.push(full);}return files;}
  const files=await visit(website);
  assert(!files.some(f=>f.includes('/.openai/')||f.includes('/.git/')));
  for(const file of files.filter(f=>/\.(html|mjs|css|json)$/.test(f))){
    const content=await readFile(file,'utf8');
    assert(!content.includes('https://github.com/autoluminis/PulseLink/blob/'),file);
    assert(!content.includes('/workspace/'),file);
    if(file.endsWith('.html'))assert(!/设计预览|待完善|演示数据/.test(content),file);
  }
  for(const path of Object.values(trust.documentationPaths))assert((await readFile(join(website,path))).length>0);
  const pdf=await readFile(join(website,'assets/PulseLink-User-Guide.pdf'));
  assert.equal(pdf.subarray(0,5).toString(),'%PDF-');
});
