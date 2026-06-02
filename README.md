
# MonteCarloRoadModelV2

A stochastic road pavement deterioration **Domain Model** for the
[Juno Cassandra](https://lonrix-limited.github.io/jcass_docs2/) framework.
Suited for road network deterioration modelling in general, but developed and
calibrated using data from New Zealand road networks.

**Open source.** Source lives at
[github.com/Lonrix-Limited/MonteCarloRoadModelV2](https://github.com/Lonrix-Limited/MonteCarloRoadModelV2).
Experienced modellers are welcome to clone, study and modify it for their own
use in Cassandra — the simulator wiring, lookup-driven thresholds and trigger
classes are all explicit entry points for customisation.

Every per-period change to condition — rut depth, IRI roughness, surface texture,
maintenance extents, post-treatment reset values — is drawn from an empirical
distribution fitted to historical HSD survey data, rather than computed from a
deterministic decay curve. The headline strength is **distribution fidelity**:
across thousands of segments, the full distributions of yearly increments and of
post-treatment reset values track the historical record closely, not just the means.

## How it runs

The model is built around Monte Carlo draws and was originally written to support
many-run Monte Carlo analyses in the Cassandra CLI / Console front-end. In the
**web version (Juno Cassandra Web)** the same DLL is used as a **single-run
forecast model with stochastic capabilities** — one stochastic forecast per run,
seeded reproducibly from `model.RandomSeed`. The full Monte Carlo many-run mode is
not exposed in the web UI; the stochastic machinery is otherwise identical.

Every random draw routes through the framework-seeded `NormalGenerator` and
`model.Random`, so a given seed reproduces the same run bit-for-bit.

## What it models

For each road segment, each period (typically 1 year):

- **Rut depth** — yearly increment drawn from a cohort-keyed distribution simulator;
  observed value = latent + normal residual whose SD is itself a function of current rut.
- **IRI roughness** — same pattern: latent increment + heteroskedastic normal residual.
- **Surface texture** — same pattern, with its own episode-length cycle.
- **Pavement (PA) routine maintenance** — probability from a logistic model (separate
  coefficients for AC and chipseal surfacings), then a maintenance-extent draw from a
  distribution simulator when triggered.
- **Pothole filling** — same pattern (logistic probability + extent draw, AC/CS split).
- **Post-treatment resets** — separate rut and IRI reset distributions for *resurfacing*
  vs *rehabilitation*; a single texture-reset distribution across treatment types;
  a fresh increment-episode begins immediately after a reset.
- **Reduction after PA maintenance** — rut and IRI receive a draw-based *reduction*
  (not a full reset) sized by the maintenance extent, floored at 1.5 mm rut /
  0.5 IRI to avoid unrealistic recoveries.

## Why it predicts distributions well

A handful of design choices, all visible in the code, drive the close match between
simulated and historical distributions:

1. **Empirical, cohort-keyed distribution simulators** rather than parametric decay
   curves. Rut, IRI, texture, maintenance-extent, reset and reduction draws all use
   `DistributionSimulator`, keyed on cohort variables (rut/IRI bucket, surface age,
   ADT, heavy %, surface thickness, rainfall, surface class, surface-count) so the
   tails are reproduced, not just the means.
2. **Heteroskedastic residuals via piecewise-linear SD functions.** The standard
   deviation of the residual at a given rut depth (or IRI value, or texture value)
   is itself read from a piecewise-linear function fit to the data — so high-rut
   segments get noisier residuals than low-rut segments, just like the survey record.
3. **Latent vs observed split.** Deterioration accumulates on a *latent* variable;
   the *observed* value reported to the framework is `latent + residual`. This stops
   noise from compounding period-to-period while letting the reported value still
   carry realistic survey-level scatter.
4. **Episode-length redraws.** Rut/IRI and texture increments are not redrawn every
   period — a sampled increment persists for an "episode" of N years, mimicking the
   serial correlation seen in real HSD time-series. When the episode expires (or a
   reset fires), a fresh increment is drawn.
5. **Separate reset simulators per treatment family.** A *rehabilitation* and a
   *resurfacing* leave a pavement in measurably different post-treatment states.
   The model carries that split through to two distinct reset distributions for
   rut and IRI.
6. **Survey-vs-treatment-date arbitration at initialisation.** If the segment was
   resurfaced or rehabilitated *after* the HSD survey date, the raw HSD value is
   discarded and a fresh reset + first increment are simulated. This stops stale
   pre-treatment values from polluting period-0 state.

## Stochastic components at a glance

All wired up in `SubModelDefinitions` and populated from CSVs by `SetupUtilities`:

| Component | Type | Purpose |
| --- | --- | --- |
| `RutIncrementSimulator` | `DistributionSimulator` | Yearly rut increment |
| `IRIIncrementSimulator` | `DistributionSimulator` | Yearly IRI increment |
| `TextureIncrementSimulator` | `DistributionSimulator` | Yearly texture increment |
| `RutIncrementResidualSDFunction` | `PieceWiseLinearModel` | Rut residual SD vs current rut |
| `IRIIncrementResidualSDFunction` | `PieceWiseLinearModel` | IRI residual SD vs current IRI |
| `TextureIncrementResidualSDFunction` | `PieceWiseLinearModel` | Texture residual SD vs current texture |
| `MaintPaProbabilityModelAC` / `…CS` | `LogisticModel` | P(PA maintenance next year), AC / chipseal |
| `PotfillProbabilityModelAC` / `…CS` | `LogisticModel` | P(pothole fill next year), AC / chipseal |
| `MaintenanceExtentPA` / `MaintenanceExtentPotfill` | `DistributionSimulator` | Extent given triggered |
| `RutResetSimulatorResurf` / `…Rehab` | `DistributionSimulator` | Post-treatment rut |
| `IRIResetSimulatorResurf` / `…Rehab` | `DistributionSimulator` | Post-treatment IRI |
| `TextureResetSimulator` | `DistributionSimulator` | Post-treatment texture (all treatments) |
| `RutReductionAfterPaMaintenanceSimulator` | `DistributionSimulator` | Rut reduction by PA extent |
| `IRIReductionAfterPaMaintenanceSimulator` | `DistributionSimulator` | IRI reduction by PA extent |
| `TSSForHoldingAction` / `TSSForRehabilitation` | `PieceWiseLinearModel` | Treatment suitability scoring curves |

Coefficient and distribution CSVs (`cohorts_b_increments_*.csv`,
`cohorts_d_maint_model_data_*.csv`, `inc_resids_plm_setup_codes.csv`, the R-exported
`term`/`estimate` files, etc.) ship inside the domain-model bundle and are read at
startup from the project's `domain_model/` folder.

## Framework lifecycle hooks

The `MonteCarloRoadModelV2` class subclasses `DomainModelBase` and implements:

- `SetupInstance()` — one-time wire-up of all simulators, residual functions,
  probability models, reset models, reduction models and treatment-suitability curves
  from the bundled CSVs.
- `Initialise(iElem, …)` — period-0 segment build from `inp_*` raw inputs; arbitrates
  HSD survey date against latest treatment date and re-simulates state when needed.
- `Increment(iElem, iPeriod, …)` — per-element, per-period deterioration draw + the
  next-period maintenance probability/extent sample.
- `Reset(treatment, iElem, iPeriod, …)` — post-treatment state update: surface age,
  thickness, layer count, surface function transitions (`1 → 2 → R`) and a fresh
  set of rut/IRI/texture resets and first-episode increments.
- `GetTreatmentCandidates(...)` — fully active. Runs the MCDA candidate-selection +
  triggering pipeline (see "Treatment triggering" below).
- `GetTriggeredMaintenance(...)` — returns `null` by design. Routine maintenance
  load is modelled probabilistically via `RoutineMaintenanceModeller` and the
  PA / potfill probability and extent simulators rather than as triggered
  `TreatmentInstance` objects.
- `DoEndOfPeriodCalculations(iPeriod)` — no-op in this version.

The central domain object is `RoadSegmentMC` (identification, quantity,
surfacing/pavement, ONRC/rainfall, traffic, HSD condition, maintenance properties).
Its `SetParameterValues(...)` method is the canonical write-contract for all `par_*`
outputs the framework expects; `RoadSegmentFactoryMC.GetFromRawData(...)` is the
authoritative reader for the `inp_*` input columns the setup workbook must supply.

## Treatment triggering

`GetTreatmentCandidates(...)` runs a two-stage MCDA pipeline per element per period:

**Stage 1 — Candidate selection** ([CandidateSelector.cs](DomainObjects/CandidateSelector.cs)).
Returns one of: `ok`, `committed near future`, `segment too short`,
`pdi and sdi below threshold`, `sla too low`, or `birthday type: too young`.
Thresholds come from the `candidate_selection` lookup set
(`CSMinPeriodsToNextTreat`, `CSMinLengthToTreatAny`, `CSMinSDIToTreat`,
`CSMinPDIToTreat`, separate `CSMinSlaToTreatCs` / `CSMinSlaToTreatAc`).
Only `"ok"` segments advance.

**Stage 2 — Treatment triggering** ([TreatmentsTrigger.cs](DomainObjects/TreatmentsTrigger.cs)).
Dispatches on the segment's `NextSurface` (which the input pre-processing can
force, allowing e.g. a current chipseal to be steered toward an asphalt next
surface):

- **Asphaltic next-surface** (`ac`, `ogpa`, `slurry`) →
  [TriggerAsphalts.cs](DomainObjects/TriggerAsphalts.cs) emits up to four
  candidates: **Preservation thin AC** (`{class}_resurf`), **Holding thin AC**
  (`{class}_holding` — a composite treatment with cost split via
  `AssignBudgetCategoryFractions` into `Resurfacing` and `Pre-Repairs`),
  **AC Heavy Maintenance** (`{class}_hmaint`, gated by
  `MinPeriodsBetweenACHeavyMaint` and `MaxSlaForACHeavyMaint`), and
  **Rehabilitation** (`{class}_rehab`, requires `CanRehabFlag == 1`).
- **Chipseal next-surface** (`cs`) →
  [TriggerChipseals.cs](DomainObjects/TriggerChipseals.cs) emits: **Second-coat**
  (forced, `cs_2nd_coat_r` or `cs_2nd_coat_h`, when `SecondCoatNeeded` and
  `SLA >= 100`); otherwise **Preservation chipseal** (`cs_resurf`),
  **Preseal repair** (`cs_preseal`, sized to the in-distress area fraction
  `PDI/100`), and **Rehabilitation** (`cs_rehab`).
- **Blocks / concrete / other** → birthday treatment (`blocks`, `concrete`,
  `xtreat`) when `SurfaceRemainingLife <= 1` and the period is past
  `EarliestTreatmentPeriod`. Forced, with a high TSS so it always survives
  optimisation.

**Treatment Suitability Score (TSS)**
([TreatmentSuitabilityScorer.cs](DomainObjects/TreatmentSuitabilityScorer.cs))
arbitrates between candidates inside the framework's optimisation stage:

- **Preservation** uses the segment's `SurfacingNeedsIndexRank` directly
  (rank-based, not curve-mapped).
- **Rehabilitation** uses the `TSSForRehabilitation` piecewise-linear curve
  on `RehabilitationNeedsIndexRank`.
- **Holding action** uses the `TSSForHoldingAction` piecewise-linear curve on
  `RehabilitationNeedsIndexRank`.

A non-trivial design touch: when a route is **not** rehab-eligible
(`CanRehabFlag == 0`), Preseal Repair and AC Heavy Maintenance fall back to the
rehabilitation TSS curve — they're effectively standing in for rehab on routes
that can't be rehabilitated, so they should compete on the rehab-side scale.
On rehab-eligible routes they use the holding-action curve and compete against
the actual rehab candidate, which carries the rehab TSS.

## Configuration and lookups

Tunables live in the project's lookup workbook, loaded into the framework's
`Lookups` dictionary-of-dictionaries and exposed via the `Constants` class. The
lookup sets the model reads from:

- `general` — base date, short-term-period count
- `candidate_selection` — SLA/SDI/PDI thresholds and minimum-period gates
- `maint_pred` — maintenance-cost calibration + PDI threshold
- `treatment_suitability_scores` — TSS rehab/holding/preserve rank curves and caps
- `mcda_treatment_triggering` — AC heavy-maintenance SLA cap and minimum-period gate
- `episode_length_max` — max episode lengths for rut/IRI and texture redraw
- `pavement_expected_life`, `treat_surf_materials`, `treat_surf_class`,
  `surf_thickness_new`, `surf_thickness_add`, `surf_life_exp` — used by the resetter
- `road_class` — used by `RoadSegmentFactoryMC`

If a lookup set or key is missing the model raises a clear error at setup time
rather than silently substituting a default.

## V1 scope and limitations

- **Routine maintenance is sampled, not framework-triggered.**
  `GetTriggeredMaintenance(...)` returns `null` by design. The PA and pothole-fill
  probability + extent simulators drive *condition* impact of routine maintenance
  inside `Increment` rather than emitting `TreatmentInstance` objects for the
  framework to optimise. This is intentional — routine maintenance load is treated
  as a stochastic *cost* rather than an optimisation choice.
- **Texture reset is treatment-agnostic.** A single texture-reset simulator is used
  for every treatment type, by design — historical texture-reset data did not
  separate cleanly by treatment family.
- **Reduction floors are hard-coded** (1.5 mm rut, 0.5 IRI). These prevent
  unrealistic recoveries after PA maintenance; they are not lookup-driven in V1.
- **No end-of-period network calculations.** `DoEndOfPeriodCalculations(...)` is a
  no-op. Network-level rankings / proportions that drive next-period decisions
  rely on framework-supplied ranks (`para_sla_rank`, `para_rut_rank`,
  `para_iri_rank`) rather than custom roll-ups.

## Build target

- .NET 9 (`net9.0`), `Nullable` + `ImplicitUsings` enabled, warnings-as-errors.
- References sibling Cassandra framework projects `JCass_Core` and `JCass_ModelCore`.
- The compiled DLL is consumed by Cassandra (desktop or web) by dropping it into the
  project's `domain_model/` folder alongside the setup CSVs and the lookup workbook.

## Reference

- Source repo — https://github.com/Lonrix-Limited/MonteCarloRoadModelV2
- Juno Cassandra docs — https://lonrix-limited.github.io/jcass_docs2/
- Project folder guide — https://lonrix-limited.github.io/jcass_docs2/guide/guide_project_folder.html
