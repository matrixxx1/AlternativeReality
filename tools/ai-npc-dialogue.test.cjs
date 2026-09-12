const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

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
