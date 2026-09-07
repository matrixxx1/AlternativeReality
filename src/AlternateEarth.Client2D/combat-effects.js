/* Shared by the live canvas client and deterministic animation tests. */
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.CombatEffects = api;
})(globalThis, function () {
  'use strict';
  const firearms = new Set(['pistol', 'rifle', 'ar15', 'machineGun']);
  const thrown = new Set(['rock', 'grenade', 'molotovCocktail']);
  const explosives = { craftingExplosion: 3, rocketLauncher: 12, grenade: 8, molotovCocktail: 6 };
  const clamp = (x, lo, hi) => Math.max(lo, Math.min(hi, x));
  function profile(weapon) {
    if (weapon === 'craftingExplosion') return { kind: 'thrown', speed: 1, minimum: 0, maximum: 0, arc: 0, impact: 1800 };
    if (firearms.has(weapon)) return { kind: 'bullet', muzzle: true, speed: 240, minimum: 240, maximum: 950, arc: 0, impact: 420 };
    if (weapon === 'rocketLauncher') return { kind: 'rocket', muzzle: true, speed: 90, minimum: 480, maximum: 2200, arc: 0, impact: 1800 };
    if (weapon === 'crossbow') return { kind: 'arrow', speed: 75, minimum: 380, maximum: 1600, arc: 5, impact: 450 };
    if (weapon === 'slingshot' || weapon === 'ballBearing') return { kind: 'pellet', speed: 95, minimum: 300, maximum: 1300, arc: 8, impact: 400 };
    if (thrown.has(weapon) || /^(chloramineGas|chlorineGas|chloroformGas|peraceticAcidGas|napalm)(Bottle|Jar)$/.test(weapon))
      return { kind: 'thrown', speed: 35, minimum: 450, maximum: 1500, arc: 32, impact: explosives[weapon] ? 1600 : 420 };
    return null;
  }
  function createShot(combat, started, locationId = 'outdoor') {
    const style = profile(combat.weapon);
    if (!style || ![combat.start?.x, combat.start?.y, combat.end?.x, combat.end?.y].every(Number.isFinite)) return null;
    const distance = Math.hypot(combat.end.x - combat.start.x, combat.end.y - combat.start.y);
    return { ...combat, style, locationId, started, duration: clamp(distance / style.speed * 1000, style.minimum, style.maximum) };
  }
  function phase(shot, now) {
    const elapsed = now - shot.started;
    if (elapsed < 0) return { name: 'pending', progress: 0 };
    if (elapsed < shot.duration) return { name: 'flight', progress: elapsed / shot.duration };
    if (elapsed < shot.duration + shot.style.impact) return { name: 'impact', progress: (elapsed - shot.duration) / shot.style.impact };
    return { name: 'done', progress: 1 };
  }
  function createRenderer({ context: ctx, project, scale, isVisible = () => true }) {
    let shots = [];
    function enqueue(combat, now, locationId) {
      let started = now;
      // Burst consequences arrive together; show each round leaving the muzzle separately.
      if (firearms.has(combat.weapon)) {
        const prior = shots.findLast(shot => shot.attackerId === combat.attackerId && shot.weapon === combat.weapon && Math.abs(now - shot.receivedAt) < 50);
        if (prior) started = Math.max(now, prior.started + 85);
      }
      const shot = createShot(combat, started, locationId);
      if (!shot) return null;
      shot.receivedAt = now;
      shots.push(shot);
      return shot;
    }
    function point(shot, t) {
      const a = project(shot.start), b = project(shot.end), height = clamp(scale() * .6, 9, 22);
      const groundImpact = shot.style.kind === 'rocket' || shot.style.kind === 'thrown' || !shot.hit;
      return { x: a.x + (b.x - a.x) * t,
        y: a.y + (b.y - a.y) * t - height * (groundImpact ? 1 - t : 1) - Math.sin(Math.PI * t) * shot.style.arc };
    }
    function glow(x, y, radius, color) {
      const fill = ctx.createRadialGradient(x, y, 0, x, y, radius);
      fill.addColorStop(0, color); fill.addColorStop(1, 'rgba(255,130,25,0)');
      ctx.fillStyle = fill; ctx.beginPath(); ctx.arc(x, y, radius, 0, Math.PI * 2); ctx.fill();
    }
    function muzzle(shot, now) {
      const age = now - shot.started, duration = 200;
      if (!shot.style.muzzle || age < 0 || age >= duration) return;
      const a = point(shot, 0), b = point(shot, 1), strength = 1 - age / duration;
      const length = (shot.style.kind === 'rocket' ? 34 : 24) * (.65 + .35 * Math.cos(age / 24));
      ctx.save(); ctx.translate(a.x, a.y); ctx.rotate(Math.atan2(b.y - a.y, b.x - a.x));
      ctx.globalAlpha = strength; glow(9, 0, 30, 'rgba(255,194,71,.8)');
      ctx.fillStyle = '#ff941f'; ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(length * .65, -11);
      ctx.lineTo(length * .55, -3); ctx.lineTo(length, 0); ctx.lineTo(length * .55, 3); ctx.lineTo(length * .65, 11); ctx.closePath(); ctx.fill();
      ctx.fillStyle = '#fffce0'; ctx.beginPath(); ctx.ellipse(9, 0, 12, 4, 0, 0, Math.PI * 2); ctx.fill(); ctx.restore();
    }
    function flight(shot, progress, now) {
      const tip = point(shot, progress), previous = point(shot, Math.max(0, progress - .02));
      ctx.save(); ctx.lineCap = 'round'; ctx.lineWidth = shot.style.kind === 'rocket' ? 2.5 : 1.4;
      ctx.strokeStyle = shot.style.kind === 'rocket' ? 'rgba(221,225,220,.24)' : 'rgba(255,240,184,.22)';
      ctx.beginPath();
      for (let i = 0; i <= 24; i++) { const p = point(shot, progress * i / 24); if (i === 0) ctx.moveTo(p.x, p.y); else ctx.lineTo(p.x, p.y); }
      ctx.stroke();
      ctx.translate(tip.x, tip.y); ctx.rotate(Math.atan2(tip.y - previous.y, tip.x - previous.x));
      if (shot.style.kind === 'rocket') {
        ctx.fillStyle = '#ff811c'; ctx.beginPath(); ctx.moveTo(-11, -4); ctx.lineTo(-24 - 7 * Math.sin(now / 30), 0); ctx.lineTo(-11, 4); ctx.fill();
        ctx.fillStyle = '#fff2ac'; ctx.beginPath(); ctx.moveTo(-10, -2); ctx.lineTo(-20, 0); ctx.lineTo(-10, 2); ctx.fill();
        ctx.strokeStyle = '#26352e'; ctx.lineWidth = 2; ctx.fillStyle = '#d3d9cc'; ctx.beginPath();
        ctx.moveTo(13, 0); ctx.lineTo(5, -5); ctx.lineTo(-10, -5); ctx.lineTo(-14, -8); ctx.lineTo(-12, 0); ctx.lineTo(-14, 8); ctx.lineTo(-10, 5); ctx.lineTo(5, 5); ctx.closePath(); ctx.fill(); ctx.stroke();
      } else if (shot.style.kind === 'arrow') {
        ctx.strokeStyle = '#e4cf9d'; ctx.lineWidth = 2.5; ctx.beginPath(); ctx.moveTo(-12, 0); ctx.lineTo(10, 0); ctx.stroke();
        ctx.fillStyle = '#f2f8ec'; ctx.beginPath(); ctx.moveTo(12, 0); ctx.lineTo(5, -4); ctx.lineTo(5, 4); ctx.fill();
        ctx.strokeStyle = '#d1975f'; ctx.beginPath(); ctx.moveTo(-6, 0); ctx.lineTo(-12, -4); ctx.moveTo(-6, 0); ctx.lineTo(-12, 4); ctx.stroke();
      } else if (shot.style.kind === 'bullet') {
        ctx.shadowColor = '#ffd459'; ctx.shadowBlur = 8; ctx.fillStyle = '#fff6ae'; ctx.beginPath(); ctx.ellipse(0, 0, 6, 2.2, 0, 0, Math.PI * 2); ctx.fill();
      } else {
        ctx.fillStyle = shot.style.kind === 'pellet' ? '#e5e9e8' : shot.weapon === 'rock' ? '#a9a092' : '#bbcd84';
        ctx.strokeStyle = '#28322b'; ctx.lineWidth = 1.5; ctx.beginPath(); ctx.arc(0, 0, shot.style.kind === 'pellet' ? 3.5 : 5.5, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
      }
      ctx.restore();
    }
    function impact(shot, t) {
      const p = point(shot, 1), blast = explosives[shot.weapon];
      ctx.save();
      if (blast) {
        const radius = Math.max(34, blast * scale()), expansion = Math.min(1, .12 + t * 3);
        if (t < .2) { ctx.globalAlpha = 1 - t / .2; glow(p.x, p.y, radius * .8, 'rgba(255,247,196,.95)'); }
        ctx.globalAlpha = Math.max(0, 1 - t * 1.15); ctx.strokeStyle = '#ffda8a'; ctx.lineWidth = Math.max(1, 5 * (1 - t));
        ctx.beginPath(); ctx.ellipse(p.x, p.y, radius * Math.min(1.2, .2 + t * 2), radius * Math.min(1.2, .2 + t * 2) * .6, 0, 0, Math.PI * 2); ctx.stroke();
        for (let i = 0; i < 14; i++) {
          const angle = i * 2.399963, spread = radius * expansion * Math.sqrt((i + 1) / 14) * .7;
          const x = p.x + Math.cos(angle) * spread, y = p.y + Math.sin(angle) * spread * .6 - radius * t * .4;
          const size = Math.max(4, radius * (.17 + .09 * Math.sin(i * 7)) * (.6 + expansion));
          ctx.globalAlpha = Math.max(0, (1 - t) * .8);
          ctx.fillStyle = t < .22 ? '#fff0a0' : t < .5 ? i % 2 ? '#ffab32' : '#f06523' : i % 2 ? '#777872' : '#494e4b';
          ctx.beginPath(); ctx.arc(x, y, size, 0, Math.PI * 2); ctx.fill();
        }
      } else {
        ctx.globalAlpha = 1 - t; glow(p.x, p.y, 18 * (1 - t) + 3, shot.hit ? 'rgba(255,219,111,.8)' : 'rgba(175,167,140,.5)');
        ctx.strokeStyle = shot.hit ? '#ffe6a0' : '#b8b3a2'; ctx.lineWidth = 2;
        for (let i = 0; i < 8; i++) { const angle = i * Math.PI / 4 + .2, distance = 4 + t * 23;
          ctx.beginPath(); ctx.moveTo(p.x + Math.cos(angle) * distance * .4, p.y + Math.sin(angle) * distance * .4);
          ctx.lineTo(p.x + Math.cos(angle) * distance, p.y + Math.sin(angle) * distance + t * t * 12); ctx.stroke(); }
      }
      ctx.restore();
    }
    function draw(now, locationId) {
      shots = shots.filter(shot => phase(shot, now).name !== 'done');
      for (const shot of shots) {
        const current = phase(shot, now);
        if (shot.locationId !== locationId || current.name === 'pending') continue;
        const t = current.name === 'flight' ? current.progress : 1;
        const position = { ...shot.start, x: shot.start.x + (shot.end.x - shot.start.x) * t, y: shot.start.y + (shot.end.y - shot.start.y) * t };
        if (isVisible(position)) {
          if (current.name === 'flight') flight(shot, current.progress, now);
          else impact(shot, current.progress);
        }
        if (isVisible(shot.start)) muzzle(shot, now);
      }
    }
    return { enqueue, draw, clear: () => { shots = []; } };
  }
  return { profile, createShot, phase, createRenderer };
});
