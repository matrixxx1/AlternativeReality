const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8');
const start=source.indexOf('  function dropControls('),end=source.indexOf('\n  function ',start+1);
test('drop shortcuts appear only at their exact quantity thresholds and send their labeled amount',()=>{
 const element=()=>({children:[],listeners:{},append(child){this.children.push(child);},addEventListener(type,fn){this.listeners[type]=fn;}}),sent=[];
 const drop=new Function('document','title','send',source.slice(start,end)+';return dropControls;')({createElement:element},x=>x,x=>sent.push(x));
 for(const [quantity,amounts] of [[0,[]],[1,[1]],[9,[1]],[10,[1,10]],[49,[1,10]],[50,[1,10,50]],[99,[1,10,50]]]){
  const controls=drop('bullet',quantity);assert.deepEqual(controls.children.map(x=>x.textContent),amounts.map(x=>`Drop ${x}`));
  controls.children.forEach((button,i)=>{button.listeners.click({stopPropagation(){}});assert.equal(sent.at(-1).quantity,amounts[i]);});
 }
});
