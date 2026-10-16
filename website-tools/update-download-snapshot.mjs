import {readFile, writeFile} from 'node:fs/promises';
import {validateConfig, publicStableReleases, selectInstaller, selectOta, readDownloadSnapshot} from '../website/release-core.mjs';

const config=validateConfig(JSON.parse(await readFile(new URL('../website/release-config.json',import.meta.url))));
async function github(path){
  const response=await fetch(`https://api.github.com/repos/${config.repository}/${path}`,{
    headers:{Accept:'application/vnd.github+json',...(process.env.GITHUB_TOKEN?{Authorization:`Bearer ${process.env.GITHUB_TOKEN}`}:{})},
    signal:AbortSignal.timeout(15000)
  });
  if(!response.ok)throw new Error(`Release snapshot request failed: HTTP ${response.status}`);
  return response.json();
}
let releases,index;
if(process.argv.length>=4){
  releases=JSON.parse(await readFile(process.argv[2],'utf8'));
  index=JSON.parse(await readFile(process.argv[3],'utf8'));
}else{
  releases=[];
  for(let page=1;page<=5;page++){
    const chunk=await github(`releases?per_page=100&page=${page}`);
    if(!Array.isArray(chunk))throw new Error('Invalid releases');
    releases.push(...chunk);
    if(chunk.length<100)break;
    if(page===5)throw new Error('Release listing exceeds snapshot limit');
  }
  const content=await github(`contents/ota/${config.channel}/${config.runtimeIdentifier}/latest.release.json?ref=${config.releaseBranch}`);
  index=JSON.parse(Buffer.from(content.content,'base64').toString('utf8'));
}
releases=publicStableReleases(releases);
const setup=selectInstaller(releases,config);
selectOta(index,releases,config);
// Retain only metadata for the selected installer and indexed OTA, never credentials or draft assets.
const selected=releases.filter(r=>r.assets.some(a=>a.browser_download_url===setup?.url)||r.tag_name===`ota-${config.channel}-${config.runtimeIdentifier}-${index.version}`)
  .map(r=>({draft:false,prerelease:false,tag_name:r.tag_name,published_at:r.published_at,
    assets:r.assets.filter(a=>a.name==='PulseLink-Setup.exe'||a.name==='PulseLink-Setup.exe.sha256'||a.name===`PulseLink-${index.version}-${config.runtimeIdentifier}.ota.zip`)
      .map(a=>({name:a.name,state:a.state,size:a.size,digest:a.digest,browser_download_url:a.browser_download_url}))}));
const snapshot={schemaVersion:1,repository:config.repository,channel:config.channel,runtimeIdentifier:config.runtimeIdentifier,generatedAt:new Date().toISOString(),releases:selected,index};
readDownloadSnapshot(snapshot,config);
await writeFile(process.argv[4] || new URL('../website/download-snapshot.json',import.meta.url),JSON.stringify(snapshot,null,2)+'\n');
console.log(`Download snapshot: setup ${setup?.version||'unavailable'}, OTA ${index.version}`);
