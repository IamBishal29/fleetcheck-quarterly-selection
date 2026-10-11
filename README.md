# FleetCheck — Quarterly Random Selection (Part 4)

Deterministic, auditable selection of a share of a company's active vehicles for
quarterly surprise safety inspections.

This is the single piece built as working code for the FleetCheck assignment
(Part 4, Option A).

---

## What it does

Given a company, a quarter, its active vehicles and a percentage (e.g. 25%), it
picks a fair, random share of those vehicles for inspection — in a way that an
auditor can reproduce and verify later.

---

## Why this design

The hard part of this requirement isn't picking randomly — it's being able to
**prove to a regulator that the pick was fair**, and getting the **same result**
every time it runs for the same inputs.

A normal random number generator can't do that: it produces a different result
on every run. So instead of real randomness, the selection uses **repeatable
"randomness" based on hashing**:

- **Reproducible and provable.** The selection is a pure function of
  `(companyId, quarter, vehicleSet, percentage)`. The seed is derived with
  SHA-256 from those inputs, so an auditor can regenerate the exact same seed
  from scratch and re-run the pick. There is no reliance on trusting that we
  stored the right seed.
- **Idempotent.** Running it twice for the same company and quarter produces the
  identical selection — never a second, different one.
- **Equal chance.** Each vehicle is given a score of `hash(seed + vehicleId)`.
  Sorting by that score and taking the top slice is an unbiased, uniform shuffle,
  so every vehicle has an equal chance of being picked.
- **History-independent.** A vehicle picked last quarter is still fully eligible —
  the algorithm never looks at past results.
- **Scales.** The work is "score every vehicle, sort, take the top few", which is
  fast at both 20 and 200,000 vehicles.

---

## How the algorithm works (step by step)

1. **Clean the input** — remove blanks and duplicates, then sort the vehicle IDs
   so the order they arrive in never affects the result.
2. **Build a seed** — hash the company, quarter and vehicle list together into one
   value. Because it comes from the inputs, it can be rebuilt by anyone with the
   same inputs.
3. **Decide how many to pick** — round the percentage to the nearest whole vehicle
   (always at least one if a non-zero percentage is requested).
4. **Score each vehicle** — `hash(seed + vehicleId)` gives each vehicle an
   unpredictable but repeatable score.
5. **Pick** — sort by score and take the required number.

---

## Project structure
fleetcheck-quarterly-selection/
├── README.md
├── src/
│   └── FleetCheck.Selection/
│       └── VehicleSelection.cs        # the core logic
└── tests/
└── FleetCheck.Tests/
└── QuarterlySelectorTests.cs  # the tests


---

## Running the tests

From the root folder:

Run in cmd:
dotnet test


## Time and space complexity

Let **n** = the number of active vehicles for the company.

**Time: O(n log n)**
- Cleaning the input (removing blanks, de-duplicating) is a single pass: O(n).
- Building the seed hashes the joined list once: O(n).
- Scoring each vehicle is one hash per vehicle: O(n).
- The dominant cost is **sorting** the vehicles by their score: **O(n log n)**.

So overall the algorithm is **O(n log n)**, driven by the sort. At 200,000
vehicles this is comfortably fast (a few hundred thousand simple operations).

## One edge case I found that the brief does not mention
## A non-zero percentage that rounds down to zero (e.g. 10% of 3 vehicles = 0.3)
**The brief doesn't say what should happen. My choice: always pick at least one, because a surprise-inspection regime that picks nobody defeats the purpose. (Already handled and tested.)**

