# FleetCheck — Quarterly Random Selection (Part 4)

Deterministic, auditable selection of a share of a company's active vehicles
for surprise safety inspections.

## Why this design
- **Reproducible & provable:** the selection is a pure function of
  `(companyId, quarter, vehicleSet, percentage)`. The seed is derived via
  SHA-256 from those inputs, so an auditor can regenerate the exact seed and
  re-run the pick — no hidden `Random` state, no reliance on a stored seed.
- **Idempotent:** running twice for the same company and quarter yields the
  identical selection.
- **Equal chance:** each vehicle is ranked by `hash(seed + vehicleId)` and the
  lowest-ranked share is picked — an unbiased, uniform shuffle.
- **History-independent:** a vehicle picked last quarter is still fully eligible.
- **Scales:** O(n log n); tested at 20 and 200,000 vehicles.

## Run
```bash
dotnet test