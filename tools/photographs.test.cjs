const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
function fixture(){
 const nodes=[],source=fs.readFileSync('src/AlternateEarth.Client2D/app.js','utf8');
 const document={createElement(tag){const node={tag,children:[],listeners:{},append(...items){this.children.push(...items);},addEventListener(name,fn){this.listeners[name]=fn;},showModal(){this.open=true;},close(){this.open=false;this.listeners.close?.();},remove(){this.removed=true;}};nodes.push(node);return node;},body:{append(){}}};
 const photo={subject:'Robot <test>',kind:'event',takenAtUtc:'2026-09-08T12:00:00Z',thumbnailUrl:'/api/photographs/abcdef'},state={privateState:{inventory:{items:[{itemType:'photograph:p',photograph:photo}]}}};
 const c=vm.createContext({document,state,Date});for(const name of ['photographForItem','itemDisplayName','createItemArt','viewPhotograph'])vm.runInContext(source.split('\n').find(line=>line.startsWith('  function '+name+'(')),c);
 return {c,photo,nodes};
}
test('inventory pictures use named lazy thumbnails instead of raw photo IDs',()=>{
 const {c,photo}=fixture();assert.equal(c.itemDisplayName('photograph:p'),'Photograph: Robot <test>');const image=c.createItemArt('photograph:p');assert.equal(image.tag,'img');assert.equal(image.loading,'lazy');assert.equal(image.src,photo.thumbnailUrl);assert.equal(image.alt,'Photograph: Robot <test>');
});
test('view photograph opens its picture and evidence details and cleans up on close',()=>{
 const {c,photo,nodes}=fixture();c.viewPhotograph(photo);const dialog=nodes.find(n=>n.tag==='dialog');assert.equal(dialog.open,true);assert.equal(dialog.children[0].textContent,photo.subject);assert.equal(dialog.children[1].src,photo.thumbnailUrl);assert.match(dialog.children[2].textContent,/Event creature evidence/);dialog.children.at(-1).listeners.click();assert.equal(dialog.removed,true);
});
