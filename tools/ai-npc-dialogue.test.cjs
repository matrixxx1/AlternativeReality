const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

test('history renders before the greeting, loads older pages without greeting again, and ignores stale opens', () => {
  const vm = require('node:vm');
  class Element {
    constructor(fragment = false) { this.fragment = fragment; this.children = []; this.scrollTop = 0; this.className = ''; }
    get scrollHeight() { return this.children.length * 20; }
    append(child) { child.parent = this; this.children.push(child); }
    prepend(fragment) { for (const child of fragment.children) child.parent = this; this.children.unshift(...fragment.children); }
    querySelector() { return this.children.find(c => c.className === 'npc-history-older'); }
    remove() { this.parent.children.splice(this.parent.children.indexOf(this), 1); }
  }
  const box = new Element(), greetings = [], sent = [];
  const state = { npcDialogue: { actorId: 'npc', openId: 'current', loadingHistory: true } };
  const scope = { state, ui: { npcDialogueMessages: box }, document: { createElement: () => new Element(), createDocumentFragment: () => new Element(true) },
    send: m => sent.push(m), setNpcDialogueWaiting: () => {}, addNpcDialogueBubble: () => {},
    sendNpcDialogueMessage: text => { greetings.push({ text, previousMessages: box.children.map(c => c.textContent) }); const bubble = new Element(); bubble.textContent = text; box.append(bubble); } };
  const client = read('src/AlternateEarth.Client2D/app.js');
  vm.createContext(scope);
  vm.runInContext(client.slice(client.indexOf('  function receiveNpcDialogueHistory('), client.indexOf('  function closeNpcDialogue(')), scope);
  const entry = text => ({ role: 'npc', text, saidAtUtc: '2026-09-12T00:00:00Z' });
  const receive = message => scope.receiveNpcDialogueHistory({ actorId: 'npc', openId: 'current', older: false, ...message });
  receive({ openId: 'stale', history: { messages: [entry('Wrong conversation')] } });
  assert.equal(box.children.length, 0);
  receive({ history: { beforeSequence: 15, messages: [entry('Previous reply')] } });
  assert.equal(greetings.length, 1);
  assert.ok(greetings[0].previousMessages.includes('Previous reply'));
  assert.equal(greetings[0].text, 'Hello!');
  assert.equal(state.npcDialogue.loadingHistory, false);
  box.querySelector().onclick();
  assert.equal(sent[0].beforeSequence, 15);
  const previousHeight = box.scrollHeight; box.scrollTop = 10;
  receive({ older: true, history: { beforeSequence: null, messages: [entry('Oldest reply'), entry('Older reply')] } });
  assert.equal(greetings.length, 1);
  assert.deepEqual(box.children.map(c => c.textContent), ['Oldest reply', 'Older reply', 'Previous reply', 'Hello!']);
  assert.equal(box.scrollTop, 10 + box.scrollHeight - previousHeight);
});

test('AI NPC dialogue is wired through the authoritative server and private client window', () => {
  const client = read('src/AlternateEarth.Client2D/app.js');
  const hub = read('src/AlternateEarth.Server/RealitySocketHub.cs');
  const service = read('src/AlternateEarth.Server/LocalAiNpcDialogueService.cs');
  const config = JSON.parse(read('src/AlternateEarth.Server/appsettings.json'));

  assert.match(client, /type:'npcDialogue'/);
  assert.match(client, /npcDialogueResult/);
  assert.match(client, /npc-dialogue-bubble/);
  assert.match(hub, /BeginAiNpcDialogue/);
  assert.match(hub, /ApplyAiNpcDialogueAsync/);
  assert.match(service, /Sorry, I can't talk now/);
  assert.match(service, /request_type = "npc_chats"/);
  assert.equal(config.LocalAI.BaseUrl, 'http://mmac.local:80');
});

test('home AI NPC lifecycle has a five-minute replacement delay', () => {
  const lifecycle = read('src/AlternateEarth.Server/RealityWorld.HomeAiNpcs.cs');
  assert.match(lifecycle, /TimeSpan\.FromMinutes\(5\)/);
  assert.match(lifecycle, /AiDialogueEnabled: true/);
  assert.match(lifecycle, /HomeBuildingId:/);
});
