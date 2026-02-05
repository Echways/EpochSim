# Determinism

Avoid non-deterministic sources inside systems and handlers.

## Common pitfalls

- `DateTime.Now` / `DateTime.UtcNow`
- `Random` instead of `ctx.Rng`
- `Guid.NewGuid()` in simulation logic
- relying on hash-collection iteration order
- hidden thread races affecting event order
- uncontrolled floating-point behavior

## Recommended practices

- use only `ctx.Rng` for domain randomness
- keep command/event handlers explicit and side-effect controlled
- persist `statefp.jsonl` for run-to-run comparison
- enable snapshots for fast deterministic replay

See also: `DeterminismContract.md`.
