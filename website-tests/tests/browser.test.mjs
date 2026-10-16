import test from 'node:test';
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile, stat, mkdir} from 'node:fs/promises';
import {join, resolve, extname, sep} from 'node:path';
import {fileURLToPath} from 'node:url';
import {chromium} from 'playwright';
const site=fileURLToPath(new URL('../../website/',import.meta.url));
const release=JSON.parse(await readFile(new URL('../fixtures/releases.json',import.meta.url)));
const catalog=JSON.parse(await readFile(new URL('../fixtures/catalog.json',import.meta.url)));
const mime={'.html':'text/html; charset=utf-8','.js':'text/javascript','.mjs':'text/javascript','.css':'text/css','.json':'application/json','.pdf':'application/pdf','.png':'image/png','.ico':'image/x-icon'};
async function checkButtonContrast(page, label){
  const actions=await page.locator('.button').evaluateAll(elements=>elements.filter(el=>el.getClientRects().length).map(el=>{
    const luminance=color=>{const rgb=color.match(/[\d.]+/g).slice(0,3).map(x=>{const v=Number(x)/255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4;});return rgb[0]*.2126+rgb[1]*.7152+rgb[2]*.0722;};
    const style=getComputedStyle(el),a=luminance(style.color),b=luminance(style.backgroundColor);
    return {label:el.textContent,ratio:(Math.max(a,b)+.05)/(Math.min(a,b)+.05)};
  }));
  for(const action of actions){assert(action.label.trim(),`${label}: empty button`);assert(action.ratio>=4.5,`${label}: ${action.label} contrast ${action.ratio}`);}
}
test('whole website: scrolling, private auth, downloads, trusted plugins, public guides and PDF', {timeout:120000}, async()=>{
  const server=createServer(async(req,res)=>{
    try{
      const relative=decodeURIComponent(new URL(req.url,'http://localhost').pathname).replace(/^\/pulselink-extension(?:\/|$)/,'/').replace(/^\/+/,'')||'index.html';
      const file=resolve(site,relative);if(!file.startsWith(resolve(site)+sep)){res.writeHead(403);res.end();return;}
      if(relative.endsWith('config.json')&&!(req.headers.cookie||'').includes('test_site_session=authenticated')){res.writeHead(401);res.end('Unauthorized');return;}
      const data=await readFile(file);res.writeHead(200,{'Content-Type':mime[extname(file)]||'application/octet-stream'});res.end(data);
    }catch{res.writeHead(404);res.end('Not found');}
  });
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const base=`http://127.0.0.1:${server.address().port}/pulselink-extension`;
  const publicReleases=structuredClone(release.releases);
  const stableRelease=publicReleases.find(r=>r.tag_name==='ota-stable-win-x64-'+release.index.version);
  assert(stableRelease);
  stableRelease.assets.push({name:'PulseLink-Setup.exe',state:'uploaded',size:372566496,browser_download_url:`https://github.com/autoluminis/pulselink-extension/releases/download/${stableRelease.tag_name}/PulseLink-Setup.exe`});
  let browser;
  try{
    browser=await chromium.launch({headless:true,args:['--no-sandbox'],...(process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH?{executablePath:process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH}:{})});
    const context=await browser.newContext({viewport:{width:1440,height:900},acceptDownloads:true});
    await context.addCookies([{name:'test_site_session',value:'authenticated',domain:'127.0.0.1',path:'/',httpOnly:true,sameSite:'Lax'}]);
    const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
    let apiDown=false,indexDown=false,tamperCatalog=false;
    await page.route('https://api.github.com/**',r=>r.fulfill({status:apiDown?403:200,contentType:'application/json',body:JSON.stringify(apiDown?{}:publicReleases)}));
    await page.route('https://raw.githubusercontent.com/**',r=>{
      const path=r.request().url().split('/release/')[1];
      if(path.startsWith('ota/'))return r.fulfill({status:indexDown?503:200,contentType:'application/json',body:JSON.stringify(release.index)});
      if(!catalog[path])return r.fulfill({status:404,body:'missing'});
      const data=Buffer.from(catalog[path],'base64');if(tamperCatalog&&path==='notifications/index.json')data[30]^=1;
      return r.fulfill({contentType:'application/json',body:data});
    });
    await page.goto(base+'/#home');
    await page.waitForFunction(()=>document.getElementById('release-status').textContent.includes('已找到公开发行的安装程序'));
    assert.match(await page.locator('#package-download').getAttribute('href'),/PulseLink-Setup\.exe$/);
    await page.locator('.nav-links a[href="#connections"]').click();
    await page.waitForFunction(()=>document.documentElement.classList.contains('is-transitioning'));
    const positions=await page.evaluate(()=>new Promise(resolve=>{const points=[];const started=performance.now();function tick(){points.push(scrollY);if(performance.now()-started<1150)requestAnimationFrame(tick);else resolve(points);}requestAnimationFrame(tick);}));
    await page.waitForFunction(()=>!document.documentElement.classList.contains('is-transitioning'));
    assert(new Set(positions.map(Math.round)).size>8,'scroll interpolates instead of jumping');
    await page.mouse.move(700,500);for(let i=0;i<6;i++)await page.mouse.wheel(0,180);
    await page.waitForFunction(()=>document.querySelector('.deck-count').textContent==='03 / 07'&&!document.documentElement.classList.contains('is-transitioning'));
    const original=await page.locator('#notifications').innerText();assert(!/WxPusher|DTU/.test(original));
    await page.emulateMedia({reducedMotion:'reduce'});
    for(const [width,height] of [[1920,900],[1440,900],[1366,768],[1024,768],[980,900],[820,900],[768,900],[390,844],[320,800]]){
      await page.setViewportSize({width,height});
      assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`no horizontal overflow at ${width}`);
      const actionsFit=await page.evaluate(()=>[...document.querySelectorAll('.actions .button,.guide-actions .button,.package-actions .button,.package-tabs button,.release-tools a,.release-tools button,.nav-links a,.menu-toggle,.plugin-entry-actions button')]
        .filter(el=>el.getClientRects().length&&getComputedStyle(el).visibility!=='hidden')
        .every(el=>{const rect=el.getBoundingClientRect();return rect.left>=-1&&rect.right<=innerWidth+1&&el.scrollWidth<=el.clientWidth+2;}));
      assert(actionsFit,`action labels fit within viewport at ${width}`);
      await checkButtonContrast(page,`homepage ${width}`);
      if(width>760&&width<=1020){assert(await page.locator('.menu-toggle').isVisible(),`responsive navigation available at ${width}`);await page.locator('.menu-toggle').click();assert(await page.locator('.nav-links a[href="#downloads"]').isVisible());await page.locator('.menu-toggle').click();}
      if(width>=1024){const sizes=await page.locator('.slide').evaluateAll(slides=>slides.map(s=>({height:s.getBoundingClientRect().height,available:innerHeight-parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--header'))})));assert(sizes.every(s=>s.height<=s.available+2),`full screens fit ${width}`);}
    }
    await page.setViewportSize({width:1440,height:900});
    for(const id of ['home','connections','notifications','rules','manual','trial','downloads']){
      await page.locator(`.nav-links a[href="#${id}"]`).click();
      await page.waitForFunction(id=>document.querySelector(`.nav-links a[href="#${id}"]`).getAttribute('aria-current')==='true',id);
      assert(Math.abs(await page.locator('#'+id).evaluate(s=>s.getBoundingClientRect().top)-82)<3);
      assert.deepEqual(await page.locator('#'+id+' .slide-inner').evaluate(el=>({transform:getComputedStyle(el).transform,animations:el.getAnimations({subtree:true}).filter(a=>a.animationName?.startsWith('chapter-')).length})),{transform:'none',animations:0},'cards stay stationary after navigation');
    }
    await page.waitForFunction(()=>!document.getElementById('release-status').textContent.includes('正在'));
    assert.match(await page.locator('#release-status').innerText(),/已找到公开发行的安装程序/);
    await page.locator('[data-package=ota]').click();
    assert.match(await page.locator('#package-download').getAttribute('href'),/\.ota\.zip$/);
    assert.match(await page.locator('#package-metadata').innerText(),new RegExp(release.index.version));
    assert.equal(await page.evaluate(async()=>{const r=await fetch('release-config.json',{credentials:'omit'});return r.status;}),401);
    apiDown=true;await page.locator('#refresh-release').click();await page.waitForFunction(()=>!document.getElementById('release-status').textContent.includes('正在'));
    assert(await page.locator('#package-download').isVisible(),'API rate limiting does not hide valid OTA');
    indexDown=true;await page.locator('#refresh-release').click();await page.waitForFunction(()=>!document.getElementById('release-status').textContent.includes('正在'));
    assert.equal(await page.locator('#package-download').getAttribute('href'),null);assert.match(await page.locator('#release-status').innerText(),/503/);indexDown=false;
    await page.locator('.nav-links a[href="#notifications"]').click();await page.locator('[data-open-plugins=catalog]').click();await page.waitForFunction(()=>document.getElementById('plugin-catalog-status').textContent.includes('已加载'));
    assert.equal(await page.locator('.catalog-plugin').count(),2);
    const docs=await page.locator('.catalog-plugin-actions a.text-link').evaluateAll(a=>a.map(x=>x.href));assert(docs.every(x=>x.startsWith(base+'/guides/')));
    for(const url of docs)assert.equal((await page.request.get(url)).status(),200);
    await page.locator('#catalog-panel').focus();await page.keyboard.press('PageDown');assert.equal(await page.locator('.deck-count').innerText(),'03 / 07');
    await page.getByRole('tab',{name:'开发与发布'}).click();assert(await page.locator('#development-panel').isVisible());
    await page.getByRole('tab',{name:'开发与发布'}).focus();await page.keyboard.press('ArrowLeft');assert.equal(await page.locator('#catalog-tab').getAttribute('aria-selected'),'true');
    tamperCatalog=true;await page.locator('#refresh-plugins').click();await page.waitForFunction(()=>document.getElementById('plugin-catalog-status').textContent.includes('暂时无法'));assert.equal(await page.locator('.catalog-plugin').count(),0);tamperCatalog=false;
    await page.keyboard.press('Escape');assert(!(await page.locator('#plugins-dialog').isVisible()));
    await page.locator('.nav-links a[href="#manual"]').click();await page.getByRole('button',{name:'在线阅读'}).click();assert(await page.locator('#guide-dialog').isVisible());await page.keyboard.press('Escape');
    const event=page.waitForEvent('download');await page.getByRole('link',{name:'下载 PDF ↓'}).click();assert.equal(await (await event).failure(),null);
    for(const file of ['developer.html','guides/dtu.html','guides/wxpusher.html']){
      for(const width of [1440,390,320]){await page.setViewportSize({width,height:900});await page.goto(base+'/'+file);assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`guide ${file} at ${width}`);}
      await checkButtonContrast(page,file);
      const hrefs=await page.locator('a[href]').evaluateAll(a=>a.map(x=>x.href));for(const href of hrefs.filter(x=>x.startsWith(base)))assert.equal((await page.request.get(href)).status(),200,href);
    }
    await page.goto(base+'/developer.html');
    if(process.env.WEBSITE_SCREENSHOT_DIR){
      await mkdir(process.env.WEBSITE_SCREENSHOT_DIR,{recursive:true});
      for(const width of [1440,320]){await page.setViewportSize({width,height:900});await page.screenshot({path:join(process.env.WEBSITE_SCREENSHOT_DIR,`developer-${width}.png`)});}
    }
    const sdkDownload=page.waitForEvent('download');await page.getByRole('link',{name:'下载通知插件开发包',exact:true}).click();assert.equal(await (await sdkDownload).failure(),null);
    await page.locator('.doc-nav a[href="#trust"]').click();
    await page.waitForFunction(()=>{const y=document.getElementById('trust').getBoundingClientRect().top;return y>parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--header'))&&y<200;});
    await page.goto(base+'/#home');await page.getByRole('button',{name:'菜单'}).click();await page.locator('.nav-links a[href="#trial"]').click();assert.equal(await page.locator('.menu-toggle').getAttribute('aria-expanded'),'false');
    await context.clearCookies();await page.goto(base+'/#downloads');await page.locator('#refresh-release').click();await page.waitForFunction(()=>!document.getElementById('release-status').textContent.includes('正在'));assert.match(await page.locator('#release-status').innerText(),/登录/);
    assert.deepEqual(errors,[]);
  }finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
});
