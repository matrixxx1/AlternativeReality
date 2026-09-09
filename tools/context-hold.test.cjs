const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
function setup(){
  const listeners={},timers=new Map(),opened=[];let now=0,id=0;
  const surface=name=>({addEventListener(type,fn){(listeners[`${name}:${type}`]??=[]).push(fn);}});
  const state={pointer:{down:false},camera:{x:0,y:0},scale:1,pitch:1,shear:0,players:new Map(),relationships:new Map()};
  const document=surface('document');
  const context={state,document,canvas:surface('canvas'),addEventListener:surface('window').addEventListener,
    ui:{actionMenu:{hidden:true},tooltip:{}},toWorld:p=>p,openActionChoices:(...args)=>opened.push(args),
    actorAtScreen:()=>null,hoverBuildingAtScreen:()=>null,send:()=>{},
    setTimeout(fn,delay){timers.set(++id,{at:now+delay,fn});return id;},clearTimeout:id=>timers.delete(id)};
  vm.runInNewContext(source.slice(source.indexOf('  let contextHoldTimer='),source.indexOf('  let primaryClickTimer='))+'\nlet primaryClickTimer=null;',context);
  return {state,opened,document,fire(name,type,props={}){const event={button:0,clientX:10,clientY:20,...props};for(const fn of listeners[`${name}:${type}`]||[])fn(event);return event;},advance(ms){now+=ms;for(const [key,timer] of [...timers])if(timer.at<=now){timers.delete(key);timer.fn();}}};
}
test('stationary left hold opens once at 600 ms and consumes its release click',()=>{
  const h=setup();h.fire('canvas','mousedown');h.advance(599);assert.equal(h.opened.length,0);
  h.advance(1);assert.equal(h.opened.length,1);h.advance(1000);assert.equal(h.opened.length,1);
  h.fire('window','mouseup');let prevented=false,stopped=false;
  h.fire('window','click',{preventDefault(){prevented=true;},stopImmediatePropagation(){stopped=true;}});
  assert.ok(prevented&&stopped);assert.equal(h.opened.length,1);
});
test('short clicks and drags do not open the action menu',()=>{
  for(const drag of [false,true]){const h=setup();h.fire('canvas','mousedown');h.advance(100);
    if(drag)h.fire('window','mousemove',{clientX:20});else h.fire('window','mouseup');
    h.advance(600);assert.equal(h.opened.length,0);
  }
});
test('right click still opens on release',()=>{
  const h=setup();h.fire('canvas','mousedown',{button:2});h.advance(1000);assert.equal(h.opened.length,0);
  h.fire('window','mouseup',{button:2});assert.equal(h.opened.length,1);
});
test('leaving the canvas, losing focus, hidden tabs and furniture placement cancel or exclude holds',()=>{
  for(const kind of ['leave','blur','hidden','furniture']){const h=setup();if(kind==='furniture')h.state.movingFurniture={};h.fire('canvas','mousedown');
    if(kind==='leave')h.fire('canvas','mouseleave');if(kind==='blur')h.fire('window','blur');
    if(kind==='hidden'){h.document.hidden=true;h.fire('document','visibilitychange');}
    h.advance(600);assert.equal(h.opened.length,0);
  }
});
