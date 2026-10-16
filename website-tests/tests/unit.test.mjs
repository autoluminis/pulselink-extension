import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile, readdir, mkdtemp, writeFile, rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {join, relative, resolve, sep} from 'node:path';
import {validateConfig, publicStableReleases, selectInstaller, selectOta, selectOtaFromIndex, readDownloadSnapshot} from '../../website/release-core.mjs';
import {readOfficialCatalog, catalogPath} from '../../website/plugin-catalog-core.mjs';
const website = fileURLToPath(new URL('../../website/', import.meta.url));
const releases = JSON.parse(await readFile(new URL('../fixtures/releases.json', import.meta.url)));
const catalog = JSON.parse(await readFile(new URL('../fixtures/catalog.json', import.meta.url)));
const config = validateConfig(JSON.parse(await readFile(join(website, 'release-config.json'))));
const trust = JSON.parse(await readFile(join(website, 'plugin-catalog-config.json')));
const readBytes = path => { assert(catalog[path]); return Uint8Array.from(Buffer.from(catalog[path], 'base64')); };

test('same-origin download snapshot validates identity, timestamp and official asset URLs', async()=>{
  const snapshot=JSON.parse(await readFile(join(website,'download-snapshot.json')));
  const actual=readDownloadSnapshot(snapshot,config);
  assert(actual.setup?.url.endsWith('/PulseLink-Setup.exe'));
  assert(actual.ota?.url.endsWith('.ota.zip'));
  assert.equal(actual.setup.cachedAt,snapshot.generatedAt);
  for(const patch of [{repository:'other/repo'},{runtimeIdentifier:'win-arm64'},{channel:'preview'},{generatedAt:'invalid'},{generatedAt:'2999-01-01T00:00:00Z'},{schemaVersion:2}])assert.throws(()=>readDownloadSnapshot({...snapshot,...patch},config));
  const poison=structuredClone(snapshot);
  poison.releases.flatMap(r=>r.assets).find(a=>a.name==='PulseLink-Setup.exe').browser_download_url='https://untrusted.invalid/setup.exe';
  assert.equal(readDownloadSnapshot(poison,config).setup,null);
  const mismatch=structuredClone(snapshot);mismatch.index.packageSha256='0'.repeat(64);
  assert.throws(()=>readDownloadSnapshot(mismatch,config));
});

test('snapshot publisher keeps only validated public download metadata',async()=>{
  const dir=await mkdtemp(join(tmpdir(),'pulselink-download-snapshot-'));
  try{
    const snapshot=JSON.parse(await readFile(join(website,'download-snapshot.json')));
    const input=structuredClone(snapshot.releases);
    input.push({...input[0],draft:true,tag_name:'v99.1.1',body:'private draft text'});
    input[0].body='not needed in public snapshot';
    input[0].assets.push({name:'unrelated-file.txt',browser_download_url:'https://untrusted.invalid/file'});
    await writeFile(join(dir,'releases.json'),JSON.stringify(input));
    await writeFile(join(dir,'index.json'),JSON.stringify(snapshot.index));
    const output=join(dir,'snapshot.json');
    execFileSync(process.execPath,[fileURLToPath(new URL('../../website-tools/update-download-snapshot.mjs',import.meta.url)),join(dir,'releases.json'),join(dir,'index.json'),output]);
    const text=await readFile(output,'utf8');
    assert(!/private draft|not needed|unrelated-file/.test(text));
    const result=readDownloadSnapshot(JSON.parse(text),config);
    assert.equal(result.setup.url,readDownloadSnapshot(snapshot,config).setup.url);
    assert.equal(result.ota.url,readDownloadSnapshot(snapshot,config).ota.url);
  }finally{assert(resolve(dir).startsWith(resolve(tmpdir())+sep)&&dir.includes('pulselink-download-snapshot-'));await rm(dir,{recursive:true,force:true});}
});

test('every public HTML page has valid local assets, links and fragment destinations', async () => {
  async function pages(dir) {const result=[];for(const item of await readdir(dir,{withFileTypes:true})){const path=join(dir,item.name);if(item.isDirectory())result.push(...await pages(path));else if(path.endsWith('.html'))result.push(path);}return result;}
  const origin='https://website.invalid/pulselink-extension/';
  for(const file of await pages(website)){
    const content=await readFile(file,'utf8');
    const url=new URL(relative(website,file).replaceAll('\\','/'),origin);
    for(const match of content.matchAll(/\b(?:href|src)="([^"]+)"/g)){
      const target=new URL(match[1],url);
      if(target.origin!==new URL(origin).origin)continue;
      assert(target.pathname.startsWith('/pulselink-extension/'),`${file}: project-path escape ${match[1]}`);
      const path=resolve(website,decodeURIComponent(target.pathname.slice('/pulselink-extension/'.length))||'index.html');
      const data=await readFile(path,'utf8');
      if(target.hash&&path.endsWith('.html'))assert(data.includes(`id="${decodeURIComponent(target.hash.slice(1))}"`),`${file}: missing target ${match[1]}`);
    }
  }
});
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
  // Real stable OTA releases can ship a complete installer as a separate asset.
  const stableOta = releases.releases.find(r=>r.tag_name==='ota-stable-win-x64-'+releases.index.version);
  assert(stableOta);
  const installerUrl=`https://github.com/${config.repository}/releases/download/${stableOta.tag_name}/PulseLink-Setup.exe`;
  const withInstaller={...stableOta,assets:[...stableOta.assets,{name:'PulseLink-Setup.exe',state:'uploaded',size:372566496,browser_download_url:installerUrl}]};
  assert.equal(selectInstaller(publicStableReleases([withInstaller]),config).url,installerUrl);
  const unpublished={...withInstaller,assets:withInstaller.assets.map(a=>a.name==='PulseLink-Setup.exe'?{...a,state:'starter'}:a)};
  assert.equal(selectInstaller(publicStableReleases([unpublished]),config),null);
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
