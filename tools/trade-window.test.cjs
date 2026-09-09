const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const source=fs.readFileSync(require.resolve('../src/AlternateEarth.Client2D/app.js'),'utf8'),line=source.split('\n').find(l=>l.includes('function openTrade('));
test('trade icon adds exactly one item, caps at stock, and opens the floating window',()=>{
 const element=()=>({children:[],dataset:{},handlers:{},append(...items){this.children.push(...items);},replaceChildren(){this.children=[];},setAttribute(){},addEventListener(type,fn){this.handlers[type]=fn;},dispatchEvent(){}}),ui={tradeTitle:element(),tradeFriend:element(),tradeOffers:element(),tradeWindow:element()},state={};let opened;
 const open=new Function('state','ui','document','createItemArt','title','showInteractionWindow','refreshNpcConversations','Event',line+';return openTrade;')(state,ui,{createElement:element},element,x=>x,(p,w)=>opened=[p,w],()=>{},class{});
 open({merchantName:'Merchant',friendRating:1,offers:[{itemType:'bullet',quantity:2,unitPriceCents:50}],buyOffers:[]});
 const row=ui.tradeOffers.children.find(e=>e.className==='trade-offer'),icon=row.children[0],input=row.children.at(-1);
 assert.equal(opened[0],ui.tradeWindow);assert.equal(opened[1],820);assert.equal(input.value,'0');
 for(const expected of ['1','2','2']){icon.handlers.click({preventDefault(){}});assert.equal(input.value,expected);}
});
