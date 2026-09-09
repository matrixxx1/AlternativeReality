// Legacy entry point retained after direct event triggers were replaced by Server Vote.
// Detailed Retro mechanics are covered by RealityWorldRetroBattleTests.
console.log('Retro battles now use Server Vote; running the current vote-driven smoke test.');
await import('./expansion-smoke.mjs');
