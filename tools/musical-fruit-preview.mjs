// Disposable visual/audio check using the production cloud renderer and audio engine.
// Run: node tools/musical-fruit-preview.mjs, then open http://localhost:5092 in Chrome.
import http from 'node:http';
import fs from 'node:fs';
const root=new URL('../src/AlternateEarth.Client2D/',import.meta.url);
const app=fs.readFileSync(new URL('app.js',root),'utf8');
const start=app.indexOf('  function drawAreaHazards('),end=app.indexOf('\n  function ',start+1);
const renderer=app.slice(start,end);
const html=`<!doctype html><meta charset="utf-8"><title>Musical Fruit preview</title>
<style>body{background:#14261d;color:#f4e7ba;font:18px system-ui;margin:32px}button{padding:12px;margin:8px;background:#3b5139;color:inherit;border:1px solid #c9b678;border-radius:8px;font:inherit}canvas{display:block;border:1px solid #65765a;max-width:100%}</style>
<h1>Musical Fruit</h1><p>Small clouds stay compact. The grand toot finale emits continuously for 20 seconds.</p>
<button id="small">Squeaker / honker</button><button id="finale">20-second grand toot finale</button><button id="stop">Clear</button>
<canvas width="1100" height="480"></canvas><p id="status">Click a button to release a cloud and hear it.</p>
<script src="/game-audio.js"></script><script>
const ctx=document.querySelector('canvas').getContext('2d');
const player={id:'me',position:{x:0,y:0},locationId:'outdoor',travelMode:'walk'};
const state={players:new Map([['me',player]]),playerId:'me',areaHazards:new Map(),scale:28,dungeon:null};
const toScreen=p=>({x:550+p.x*28,y:260+p.y*28*.69}),visibleRange=()=>100;
const hash=s=>[...s].reduce((n,c)=>(n*31+c.charCodeAt(0))>>>0,0)/4294967296;
const audio=GameAudio.create({getScene:()=>({listener:player,areaHazards:[...state.areaHazards.values()]})});
${renderer}
async function release(finale){await audio.unlock();state.areaHazards.clear();audio.clear();const now=Date.now(),radius=finale?5:1+Math.random()*1.25,duration=finale?20:5+Math.random()*5;
state.areaHazards.set('cloud',{id:crypto.randomUUID(),ownerId:'me',locationId:'outdoor',position:player.position,name:finale?'Grand toot finale':'Musical Fruit',effect:finale?'musicalFruitFinale':'musicalFruit',radiusMeters:radius,startedAtUtc:new Date(now).toISOString(),endsAtUtc:new Date(now+duration*1000).toISOString()});
document.querySelector('#status').textContent='Radius '+radius.toFixed(2)+' m · '+duration.toFixed(1)+' seconds';}
document.querySelector('#small').onclick=()=>release(false);document.querySelector('#finale').onclick=()=>release(true);document.querySelector('#stop').onclick=()=>{state.areaHazards.clear();audio.clear();};
setInterval(()=>audio.tick(),100);
function frame(now){ctx.fillStyle='#243b2a';ctx.fillRect(0,0,1100,480);ctx.strokeStyle='#354d36';for(let x=18;x<1100;x+=28){ctx.beginPath();ctx.moveTo(x,0);ctx.lineTo(x,480);ctx.stroke();}for(let y=8;y<480;y+=28*.69){ctx.beginPath();ctx.moveTo(0,y);ctx.lineTo(1100,y);ctx.stroke();}
ctx.fillStyle='#d8b17b';ctx.beginPath();ctx.arc(550,225,8,0,Math.PI*2);ctx.fill();ctx.fillStyle='#6288b4';ctx.fillRect(542,234,16,18);ctx.fillStyle='#c9c8a7';ctx.fillRect(542,252,6,10);ctx.fillRect(552,252,6,10);drawAreaHazards(now);requestAnimationFrame(frame);}requestAnimationFrame(frame);
</script>`;
http.createServer((req,res)=>{
 if(req.url==='/game-audio.js'){res.setHeader('Content-Type','text/javascript');res.end(fs.readFileSync(new URL('game-audio.js',root)));}
 else if(req.url==='/'){res.setHeader('Content-Type','text/html; charset=utf-8');res.end(html);}
 else res.writeHead(404).end();
}).listen(5092,'127.0.0.1',()=>console.log('Musical Fruit preview: http://localhost:5092'));
