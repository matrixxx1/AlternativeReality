const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const effects = require('../src/AlternateEarth.Client2D/combat-effects.js');
const combat = (weapon, hit = true) => ({ attackerId: 'shooter', targetId: 'target', weapon, hit,
  start: { x: 0, y: 0 }, end: { x: 45, y: 10 } });

function canvasHarness() {
  const calls = [];
  const ctx = new Proxy({}, { set(obj, key, value) { obj[key] = value; calls.push([key, value]); return true; },
    get(obj, key) { if (key === 'createRadialGradient') return () => ({ addColorStop() {} });
      return obj[key] ?? ((...args) => { calls.push([key, ...args]); }); } });
  const renderer = effects.createRenderer({ context: ctx, project: p => ({ x: p.x * 4, y: p.y * 4 }), scale: () => 4 });
  return { renderer, calls, clear: () => { calls.length = 0; } };
}

test('every physical ranged weapon travels, impacts, and fully expires', () => {
  const weapons = ['pistol', 'rifle', 'ar15', 'machineGun', 'rocketLauncher', 'crossbow', 'slingshot', 'rock', 'grenade', 'molotovCocktail', 'chlorineGasJar', 'napalmBottle'];
  for (const weapon of weapons) {
    const shot = effects.createShot(combat(weapon), 100);
    assert.ok(shot.duration >= 240, weapon);
    assert.equal(effects.phase(shot, 99).name, 'pending');
    assert.equal(effects.phase(shot, 100 + shot.duration - 1).name, 'flight');
    assert.equal(effects.phase(shot, 100 + shot.duration).name, 'impact');
    assert.equal(effects.phase(shot, 100 + shot.duration + shot.style.impact).name, 'done');
  }
});

test('rocket trail exists only in flight; explosion starts precisely at arrival, also on misses', () => {
  for (const hit of [true, false]) {
    const { renderer, calls, clear } = canvasHarness();
    const shot = renderer.enqueue(combat('rocketLauncher', hit), 0, 'outdoor');
    renderer.draw(shot.duration - 1, 'outdoor');
    assert.ok(calls.some(call => call[0] === 'strokeStyle' && call[1] === 'rgba(221,225,220,.24)'));
    assert.equal(calls.some(call => call[0] === 'fillStyle' && call[1] === '#fff0a0'), false);
    clear(); renderer.draw(shot.duration, 'outdoor');
    assert.equal(calls.some(call => call[1] === 'rgba(221,225,220,.24)'), false);
    assert.ok(calls.some(call => call[0] === 'fillStyle' && call[1] === '#fff0a0'));
    clear(); renderer.draw(shot.duration + shot.style.impact, 'outdoor');
    assert.equal(calls.length, 0);
  }
});

test('muzzle flashes originate at launch and do not linger through impact', () => {
  for (const weapon of ['pistol', 'rifle', 'ar15', 'machineGun', 'rocketLauncher']) {
    const { renderer, calls, clear } = canvasHarness();
    const shot = renderer.enqueue(combat(weapon), 0, 'outdoor');
    renderer.draw(50, 'outdoor');
    assert.ok(calls.some(call => call[0] === 'fillStyle' && call[1] === '#fffce0'), weapon);
    clear(); renderer.draw(shot.duration, 'outdoor');
    assert.equal(calls.some(call => call[1] === '#fffce0'), false);
  }
});

test('AR15 burst rounds are separately animated, while independent attackers overlap', () => {
  const { renderer } = canvasHarness();
  const first = renderer.enqueue(combat('ar15'), 100, 'outdoor');
  const second = renderer.enqueue(combat('ar15'), 101, 'outdoor');
  const third = renderer.enqueue(combat('ar15'), 102, 'outdoor');
  const other = renderer.enqueue({ ...combat('ar15'), attackerId: 'other' }, 102, 'outdoor');
  assert.deepEqual([first.started, second.started, third.started, other.started], [100, 185, 270, 102]);
});

test('splash damage, ongoing burns, and melee events do not create extra rockets or explosions', () => {
  for (const weapon of ['rocketLauncherExplosion', 'grenadeExplosion', 'molotovFire', 'areaHazard', 'probulator', 'greenBeam', 'fist', 'sword'])
    assert.equal(effects.createShot(combat(weapon), 0), null, weapon);
});

test('effects are isolated by location and invalid endpoints cannot poison the canvas', () => {
  const { renderer, calls } = canvasHarness();
  renderer.enqueue(combat('rocketLauncher'), 0, 'home:one');
  renderer.draw(50, 'outdoor'); assert.equal(calls.length, 0);
  renderer.draw(50, 'home:one'); assert.ok(calls.length > 0);
  assert.equal(renderer.enqueue({ ...combat('rifle'), end: { x: NaN, y: 0 } }, 0), null);
});

test('near and distant shots remain visible; all projectile styles render without stale trails', () => {
  for (const weapon of ['pistol', 'rifle', 'ar15', 'machineGun', 'crossbow', 'slingshot', 'rock', 'grenade', 'molotovCocktail', 'napalmJar']) {
    const { renderer, calls, clear } = canvasHarness();
    const shot = renderer.enqueue(combat(weapon), 0, 'outdoor');
    renderer.draw(shot.duration / 2, 'outdoor');
    assert.ok(calls.some(call => call[1] === 'rgba(255,240,184,.22)'), weapon);
    clear(); renderer.draw(shot.duration + 100, 'outdoor');
    assert.equal(calls.some(call => call[1] === 'rgba(255,240,184,.22)'), false, weapon);
  }
  const close = effects.createShot({ ...combat('rocketLauncher'), end: { x: 0, y: 0 } }, 0);
  const far = effects.createShot({ ...combat('rocketLauncher'), end: { x: 300, y: 0 } }, 0);
  assert.ok(close.duration >= 480); assert.ok(far.duration > close.duration);
});

test('live client loads the tested renderer and draws it outdoors and in interiors', () => {
  const html = fs.readFileSync('src/AlternateEarth.Client2D/index.html', 'utf8');
  const app = fs.readFileSync('src/AlternateEarth.Client2D/app.js', 'utf8');
  assert.ok(html.indexOf('combat-effects.js') < html.indexOf('app.js'));
  assert.equal((app.match(/combatEffects.draw\(/g) || []).length, 2);
  assert.ok(app.includes('combatEffects.enqueue(combat'));
  assert.equal(app.includes('state.explosions.push'), false);
});
