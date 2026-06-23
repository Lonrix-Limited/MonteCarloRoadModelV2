
# MonteCarloRoadModelV2

A stochastic road pavement deterioration **Domain Model** for the
[Juno Cassandra](https://lonrix-limited.github.io/jcass_docs2/) framework.
Suited for road network deterioration modelling in general, but developed and
calibrated using data from New Zealand road networks.

**Open source.** Source lives at
[github.com/Lonrix-Limited/MonteCarloRoadModelV2](https://github.com/Lonrix-Limited/MonteCarloRoadModelV2).
Experienced modellers are welcome to clone, study and modify it for their own
use in Cassandra — the simulator wiring, lookup-driven thresholds, calibration
factors and trigger classes are all explicit entry points for customisation.

Every per-period change to condition — rut depth, IRI roughness, surface
texture, distress state, maintenance extents, post-treatment reset values — is
drawn from an empirical distribution (or a Markov transition matrix) fitted to
historical HSD survey and maintenance data, rather than computed from a
deterministic decay curve. The headline strength is **distribution fidelity**:
across thousands of segments, the full distributions of yearly increments and
of post-treatment reset values track the historical record closely, not just
the means.

## How it runs

The model is built around Monte Carlo draws and was originally written to
support many-run Monte Carlo analyses in the Cassandra CLI / Console front-end.
In the **web version (Juno Cassandra Web)** the same DLL is used as a
**single-run forecast model with stochastic capabilities** — one stochastic
forecast per run, seeded reproducibly from `model.RandomSeed`. The full Monte
Carlo many-run mode is not exposed in the web UI; the stochastic machinery is
otherwise identical.

Every random draw routes through the framework-seeded `NormalGenerator` and
`model.Random`, so a given seed reproduces the same run bit-for-bit.

To soften noise in the first few periods (when every segment is fresh and a
single bad post-treatment draw can cascade into spurious re-treatment), a
configurable `minimise_stochastic_effects_period` switches the reset draws
(rut, IRI, texture) to a 5-draw mean. After that period the model reverts to
single draws.

## What it models

For each road segment, each period (typically 1 year):

- **Rut depth** — yearly increment drawn from a cohort-keyed distribution
  simulator; observed value = latent + normal residual whose SD is itself a
  function of current rut.
- **IRI roughness** — same pattern: latent increment + heteroskedastic normal
  residual.
- **Surface texture** — same pattern, with its own episode-length cycle. Only
  chipseal surfaces draw from the texture-increment simulator; AC/OGPA draw
  a uniform value in [-0.04, +0.05] mm/year.
- **Pavement distress, surfacing distress, flushing distress** — three
  categorical state codes of the form `E{0..3}-S{0..2}` (Extent × Severity)
  that evolve per period via Markov transition probability matrices. Separate
  TPMs for untreated vs treated (and, for pavement distress, separate
  rehabilitation vs resurfacing TPMs). **This is the headline refactor
  from V1**: V1 modelled pavement and surfacing condition through two scalar
  indices called PDI (Pavement Distress Index) and SDI (Surface Distress
  Index), each evolved by deterministic formulas. V2 replaces them with
  categorical distress states evolved by transition probability matrices
  fitted to successive survey rounds. Every downstream consumer — candidate
  selection, the Rehabilitation/Surfacing Needs Indices, the TSS curves,
  preseal-repair sizing — now reads the state codes (or extent/severity
  scores derived from them) instead of the old scalar indices.
- **Pavement (PA) routine maintenance** — probability from a logistic model
  (separate coefficients for AC and chipseal surfacings), then a maintenance
  extent draw from a distribution simulator when triggered.
- **Surfacing (SU) routine maintenance** — same pattern, separate logistic
  models for AC vs CS.
- **Pothole filling** — same pattern (logistic probability + extent draw,
  AC/CS split).
- **Post-treatment resets** — separate rut and IRI reset distributions for
  *resurfacing* vs *rehabilitation*; a single texture-reset distribution
  across treatment types; distress states transition through their own
  treated TPMs; a fresh increment-episode begins immediately after a reset.
- **Reduction after PA maintenance** — rut and IRI receive a draw-based
  *reduction* (not a full reset) sized by the maintenance extent, floored
  at 1.5 mm (rut) / 2.5 mm/m (IRI) to avoid unrealistic recoveries. The
  reduction only fires when the PA maintenance extent exceeds the
  lookup-tunable `cal_maintenance.rut_reduc` / `iri_reduc` thresholds.

## Why it predicts distributions well

A handful of design choices, all visible in the code, drive the close match
between simulated and historical distributions:

1. **Empirical, cohort-keyed distribution simulators** rather than parametric
   decay curves. Rut, IRI, texture, maintenance-extent, reset and reduction
   draws all use a cohort-based distribution simulator, keyed on cohort
   variables (rut/IRI bucket, surface age, ADT, heavy %, surface thickness,
   surface class, surface-count, and recent PA/SU/potfill extents) so the
   tails are reproduced, not just the means.
2. **Heteroskedastic residuals via piecewise-linear SD functions.** The
   standard deviation of the residual at a given rut depth (or IRI value, or
   texture value) is itself read from a piecewise-linear function fit to the
   data — so high-rut segments get noisier residuals than low-rut segments,
   just like the survey record.
3. **Latent vs observed split.** Deterioration accumulates on a *latent*
   variable; the *observed* value reported to the framework is
   `latent + residual`. This stops noise from compounding period-to-period
   while letting the reported value still carry realistic survey-level
   scatter.
4. **Markov transition matrices for categorical distress.** Pavement,
   surfacing and flushing distress evolve as discrete `E{0..3}-S{0..2}`
   states, with empirical transition probabilities estimated from successive
   HSD/Vis survey rounds. This is a deliberate move away from V1's scalar
   PDI/SDI indices — distress initiation and progression are inherently
   lumpy and staged (a segment moves from "no cracking" to "isolated
   cracking" to "extensive cracking" in discrete jumps), and a continuous
   index ends up smoothing those transitions in ways that misrepresent the
   risk of any given segment crossing a treatment threshold.
5. **Episode-length redraws.** Rut/IRI and texture increments are not redrawn
   every period — a sampled increment persists for an "episode" of N years
   (configurable via `episode_length_max`), mimicking the serial correlation
   seen in real HSD time-series. When the episode expires (or a reset fires),
   a fresh increment is drawn.
6. **Separate reset simulators per treatment family.** A *rehabilitation* and
   a *resurfacing* leave a pavement in measurably different post-treatment
   states. The model carries that split through to two distinct reset
   distributions for rut and IRI, and to two distinct pavement-distress
   transition matrices.
7. **Survey-vs-treatment-date arbitration at initialisation.** If the segment
   was resurfaced or rehabilitated *after* the HSD survey date, the raw HSD
   value is discarded and a fresh reset + first increment are simulated. This
   stops stale pre-treatment values from polluting period-0 state.

## Stochastic components at a glance

All wired up in `SubModelDefinitions` and populated from CSVs and one
workbook by `SetupUtilities`:

| Component | Type | Purpose |
| --- | --- | --- |
| `RutIncrementSimulator` | DistributionSimulator | Yearly rut increment |
| `IRIIncrementSimulator` | DistributionSimulator | Yearly IRI increment |
| `TextureIncrementSimulator` | DistributionSimulator | Yearly texture increment (CS only) |
| `RutIncrementResidualSDFunction` | PieceWiseLinearModel | Rut residual SD vs current rut |
| `IRIIncrementResidualSDFunction` | PieceWiseLinearModel | IRI residual SD vs current IRI |
| `TextureIncrementResidualSDFunction` | PieceWiseLinearModel | Texture residual SD vs current texture |
| `MaintPaProbabilityModelAC` / `…CS` | LogisticModel | P(PA maintenance next year), AC / chipseal |
| `MaintSuProbabilityModelAC` / `…CS` | LogisticModel | P(SU maintenance next year), AC / chipseal |
| `PotfillProbabilityModelAC` / `…CS` | LogisticModel | P(pothole fill next year), AC / chipseal |
| `MaintenanceExtentPA` / `MaintenanceExtentSU` / `MaintenanceExtentPotfill` | DistributionSimulator | Extent given triggered |
| `RutResetSimulatorResurf` / `…Rehab` | DistributionSimulator | Post-treatment rut |
| `IRIResetSimulatorResurf` / `…Rehab` | DistributionSimulator | Post-treatment IRI |
| `TextureResetSimulator` | DistributionSimulator | Post-treatment texture (all treatments) |
| `RutReductionAfterPaMaintenanceSimulator` | DistributionSimulator | Rut reduction by PA extent |
| `IRIReductionAfterPaMaintenanceSimulator` | DistributionSimulator | IRI reduction by PA extent |
| `PavementDistressModelUntreated` / `…Rehabilitation` / `…Resurfacing` | MarkovTransitionSimulator | Pavement distress state transitions |
| `SurfaceDistressModelUntreated` / `…Treated` | MarkovTransitionSimulator | Surfacing distress state transitions |
| `FlushingDistressModelUntreated` / `…Treated` | MarkovTransitionSimulator | Flushing state transitions |
| `TSSForHoldingAction` / `TSSForRehabilitation` | PieceWiseLinearModel | Treatment suitability scoring curves (built from `treatment_suitability_scores` lookups, not from a CSV) |

Coefficient and distribution CSVs (`cohorts_b_increments_*.csv`,
`cohorts_d_maint_model_data_*.csv`, `cohorts_c_*_resets_*.csv`,
`cohorts_maint_reduc_data_final_*.csv`, `inc_resids_plm_setup_codes.csv`, and
the R-exported `term`/`estimate` files `logistic_*.csv`) plus the
`distress_transition_models.xlsx` workbook ship inside the domain-model
bundle and are read at startup from the project's `domain_model/` folder.

## Framework lifecycle hooks

The `MonteCarloRoadModelV2` class subclasses `DomainModelBase` and implements:

- `SetupInstance()` — one-time wire-up of all simulators, residual functions,
  probability models, reset models, reduction models, distress-state
  transition models and treatment-suitability curves from the bundled CSVs
  and workbook.
- `Initialise(iElem, …)` — period-0 segment build from `inp_*` raw inputs;
  arbitrates HSD survey date against latest treatment date and re-simulates
  rut/IRI/texture (and forces all three distress states to `E0-S0`) when
  the segment was clearly treated after the survey.
- `Increment(iElem, iPeriod, …)` — per-element, per-period deterioration
  draws + Markov transitions for the three distress states, + the
  next-period maintenance probability/extent sample (only after the
  historical-maintenance window so the historical inputs are used unmodified
  for the first few periods).
- `Reset(treatment, iElem, iPeriod, …)` — post-treatment state update:
  surface age, thickness, layer count, surface function transitions
  (`1 → 2 → R`, plus a `1a` branch for preseal-repair history) and a fresh
  set of rut/IRI/texture resets and first-episode increments. Distress
  states transition through their treated TPMs. PA/SU/potfill current and
  historical extents are zeroed out so they don't drive next-period
  maintenance probability.
- `GetTreatmentCandidates(...)` — fully active. Runs the MCDA
  candidate-selection + triggering pipeline (see "Treatment triggering"
  below).
- `GetTriggeredMaintenance(...)` — returns `null` by design. Routine
  maintenance load is modelled probabilistically via
  `RoutineMaintenanceModeller` and the PA / SU / potfill probability and
  extent simulators rather than as triggered `TreatmentInstance` objects.
- `DoEndOfPeriodCalculations(iPeriod)` — no-op in this version.

The central domain object is `RoadSegmentMC` (identification, quantity,
surfacing/pavement, ONRC/road-class, traffic, HSD condition, distress
states, maintenance properties, treatment-trigger flags). Its
`SetParameterValues(...)` method is the canonical write-contract for all
`par_*` outputs the framework expects; `RoadSegmentFactoryMC.GetFromRawData`
is the authoritative reader for the `inp_*` input columns the setup workbook
must supply. Date columns are expected in 8-character ISO format
(`yyyymmdd`); the framework will throw a clear error at initialisation if a
column is malformed.

## Treatment triggering

`GetTreatmentCandidates(...)` runs a two-stage MCDA pipeline per element per
period:

**Stage 1 — Candidate selection** (`CandidateSelector.cs`). Returns one of:
`ok`, `ok - 2nd coat needed`, `treat flag is 0`, `committed near future`,
`segment too short`, `segment too long`, `sla too low`, or
`birthday type: too young`. Thresholds come from the `candidate_selection`
lookup set (`min_periods_to_next_treat`, `min_sla_to_treat_ac`,
`min_sla_to_treat_cs`) and from the per-surface-type minimum/maximum length
gates in the `rehab_needs_index` and `surfacing_needs_index` lookup sets.
Only outcomes starting with `"ok"` advance to Stage 2. The Stage-1 outcome
is also written to `par_trigg_info` so it shows up in framework logs and the
web UI.

**Stage 2 — Treatment triggering** (`TreatmentsTrigger.cs`). Dispatches on
the segment's `NextSurface` (which the input pre-processing can force,
allowing e.g. a current chipseal to be steered toward an asphalt next
surface):

- **Asphaltic next-surface** (`ac`, `ogpa`, `slurry`) → `TriggerAsphalts`
  emits up to four candidates: **Preservation thin AC** (`{class}_resurf`),
  **Holding thin AC** (`{class}_holding` — a composite treatment with cost
  split via `AssignBudgetCategoryFractions` into `Resurfacing` and
  `Pre-Repairs`), **AC Heavy Maintenance** (`{class}_hmaint`, gated by
  `MinPeriodsBetweenACHeavyMaint`, `MaxSlaForACHeavyMaint`, and requires the
  pavement distress state to imply a non-zero extent), and
  **Rehabilitation** (`{class}_rehab`, requires `CanRehabFlag == 1`).
- **Chipseal next-surface** (`cs`) → `TriggerChipseals` emits:
  **Second-coat** (forced, `cs_2nd_coat_r` or `cs_2nd_coat_h` depending on
  whether the previous surface function was `1` or `1a`, when
  `SecondCoatNeeded` and `SLA >= 100`); otherwise **Preservation chipseal**
  (`cs_resurf`), **Preseal repair** (`cs_preseal`, sized to the pavement
  distress extent estimate via `GetExtentEstimateFromStateScore`), and
  **Rehabilitation** (`cs_rehab`).
- **Blocks / concrete / other** → birthday treatment (`blocks`, `concrete`,
  `xtreat`) when `SurfaceRemainingLife <= 1` and the period is at or past
  `EarliestTreatmentPeriod`. Forced, with a high TSS so it always survives
  optimisation.

Distress extent for sizing follows a fixed mapping in
`RoadSegmentMC.GetExtentEstimateFromStateScore`: `E0 → 0%`, `E1 → 10%`,
`E2 → 30%`, `E3 → 50%`. Preseal repair quantity = `AreaSquareMetre × extent`,
and AC heavy maintenance is gated on `extent > 0`.

**Rehabilitation Needs Index and Surfacing Needs Index.** Per-period, the
domain model computes two scalar indices per segment
(`RehabilitationNeedsCalculator`, `SurfacingNeedsIndexCalculator`) and writes
them as `par_rni` and `par_sni`. The framework ranks them across the network
and feeds the ranks back as `par_rni_rank` / `par_sni_rank`, which the model
reads in the next period (via `RoadSegmentFactoryMC.GetFromModel`) to score
candidate treatments. The RNI combines pavement distress state × 10, current
rut depth, and a recent-maintenance bump while the model is still inside
the historical-maintenance use window. The SNI combines surfacing distress
state × 10, plus chipseal-specific texture and flushing penalties, plus a
skid-resistance contribution for the first few periods. Both indices return
0 outside their applicable length / SLA / rut gates — so a 0 rank doesn't
mean "low-need", it means "not eligible".

**Treatment Suitability Score (TSS)** (`TreatmentSuitabilityScorer.cs`)
arbitrates between candidates inside the framework's optimisation stage:

- **Preservation** uses the segment's `SurfacingNeedsIndexRank` directly
  (rank-based, not curve-mapped).
- **Rehabilitation** uses the `TSSForRehabilitation` piecewise-linear curve
  on `RehabilitationNeedsIndexRank`.
- **Holding action** uses the `TSSForHoldingAction` piecewise-linear curve
  on `RehabilitationNeedsIndexRank`. Holding is suppressed when the current
  pavement distress state is in the
  `treatment_suitability_scores.holding_exclusion_states` list
  (pipe-separated state codes).

A non-trivial design touch: when a route is **not** rehab-eligible
(`CanRehabFlag == 0`), Preseal Repair and AC Heavy Maintenance fall back to
the rehabilitation TSS curve — they're effectively standing in for rehab on
routes that can't be rehabilitated, so they should compete on the rehab-side
scale. On rehab-eligible routes they use the holding-action curve and
compete against the actual rehab candidate, which carries the rehab TSS.

## Configuration and lookups

Tunables live in the project's lookup workbook, loaded into the framework's
`Lookups` dictionary-of-dictionaries and exposed via the `Constants` class.
The lookup sets the model reads from:

**Core thresholds and gates**

- `general` — `base_date` (ISO `yyyymmdd`), `minimise_stochastic_effects_period`
- `candidate_selection` — `min_periods_to_next_treat`, `min_sla_to_treat_ac`,
  `min_sla_to_treat_cs`
- `rehab_needs_index` — `use_hist_maint_periods`, `min_length_rehab`,
  `max_length_rehab`, `excess_rut_threshold`, `unstable_rut_threshold`,
  `unstable_seal_count`
- `surfacing_needs_index` — `cs_max_seal_count`, `cs_texture_threshold`,
  `cs_texture_penalty_factor`, `flushing_penalty_factor`,
  `min_sla_to_resurface_cs`, `min_sla_to_resurface_ac`, `max_rut_cs`,
  `max_rut_ac`, `skid_resistance_max_period`, `min_length_chipseal`,
  `max_length_chipseal`, `min_length_ac_or_ogpa`, `max_length_ac_or_ogpa`
- `treatment_suitability_scores` — `rehab_min_rni_rank`,
  `holding_rni_rank_pt1/2/3`, `holding_exclusion_states`
- `mcda_treatment_triggering` — `ac_hmaint_maximum_sla`,
  `ac_hmaint_min_periods_between`
- `episode_length_max` — `rut_and_iri`, `texture`

**Calibration**

- `cal_residuals` — `rut`, `iri`, `texture` multipliers on the residual draw
  (set to 0 to disable observation noise entirely)
- `cal_maintenance` — `pa_proba`, `su_proba`, `potfill_proba` (probability
  multipliers), `pa_extent`, `su_extent`, `potfill_extent` (extent
  multipliers), `rut_reduc`, `iri_reduc` (PA-extent thresholds above which
  rut/IRI reduction fires), `rut_reduc_min`, `iri_reduc_min` (floors on the
  sampled reduction to stop maintenance from making condition worse beyond
  reason)
- `cal_increm_adj_iri` / `cal_increm_adj_rut` / `cal_increm_adj_text` —
  per-surface-class additive adjustments to drawn increments
- `cal_resets` — `rut`, `iri`, `texture` multipliers on reset values
- `cal_reset_adj_iri` / `cal_reset_adj_rut` / `cal_reset_adj_text` —
  per-treatment-name additive adjustments to reset values

**Used by Resetter and Factory**

- `pavement_expected_life` — keyed by ONRC; resets `PavementRemainingLife`
  after any treatment
- `surf_thickness_new` / `surf_thickness_add` — keyed by post-treatment
  surface material; used during rehab (new) vs resurfacing (add)
- `surf_life_exp` — keyed either by `SurfaceExpectedLifeCode`
  (`{function}_{material}_{roadclass}`) for standard surfaces, or by
  material name (`blocks` / `concrete` / `other` / `default_cs` /
  `default_ac` / `default_ogpa` / `default_slurry`)
- `road_class` — maps ONRC categories to `l` / `m` / `h` traffic class
- `unit_rates_general` — flat treatment-name → $/m² rates for non-rehab
  treatments
- Rehab unit-rate sets keyed `{class}_rehab_rate` (e.g. `cs_rehab_rate`) —
  rehab unit rates keyed by ONRC

If a lookup set or key is missing the model raises a clear error at setup
time rather than silently substituting a default.

## Scope and intentional limitations

- **Routine maintenance is sampled, not framework-triggered.**
  `GetTriggeredMaintenance(...)` returns `null` by design. The PA, SU and
  pothole-fill probability + extent simulators drive *condition* impact of
  routine maintenance inside `Increment` rather than emitting
  `TreatmentInstance` objects for the framework to optimise. This is
  intentional — routine maintenance load is treated as a stochastic *cost*
  rather than an optimisation choice.
- **Texture reset is treatment-agnostic.** A single texture-reset simulator
  is used for every treatment type, by design — historical texture-reset
  data did not separate cleanly by treatment family.
- **Texture increment for AC/OGPA is a uniform draw**, not a cohort-based
  simulator, because the modelling dataset did not contain enough non-CS
  texture-evolution data to fit a meaningful distribution.
- **Surfacing and flushing distress TPMs do not distinguish rehab vs
  resurf**, only treated vs untreated — both rehab and resurfacing reset the
  surfacing/flushing state through the same `…Treated` TPM. Pavement
  distress *does* have a separate rehab vs resurf TPM.
- **Reduction floors are hard-coded** (1.5 mm rut, 2.5 mm/m IRI) inside
  `Incrementer.IncrementRut` / `IncrementIRI`. These prevent unrealistic
  recoveries after PA maintenance and are not lookup-driven. The
  `cal_maintenance.rut_reduc_min` / `iri_reduc_min` calibration values
  separately cap the *sampled reduction value itself* before it's applied,
  but the post-reduction state floor is still the hard-coded 1.5 / 2.5.
- **No end-of-period network calculations.** `DoEndOfPeriodCalculations(...)`
  is a no-op. Network-level rankings (`para_sla_rank`, `para_rut_rank`,
  `para_iri_rank`, `par_rni_rank`, `par_sni_rank`) are filled by the
  framework rather than by this domain model.

## Build target

- .NET 9 (`net9.0`), `Nullable` + `ImplicitUsings` enabled,
  `TreatWarningsAsErrors=true`.
- References sibling Cassandra framework projects `JCass_Core` and
  `JCass_ModelCore`, which must exist on disk alongside this repo.
- The compiled DLL is consumed by Cassandra (desktop or web) by dropping it
  into the project's `domain_model/` folder alongside the setup CSVs, the
  `distress_transition_models.xlsx` workbook, and the lookup workbook.

## Reference

- Source repo — https://github.com/Lonrix-Limited/MonteCarloRoadModelV2
- Juno Cassandra docs — https://lonrix-limited.github.io/jcass_docs2/
- Project folder guide — https://lonrix-limited.github.io/jcass_docs2/guide/guide_project_folder.html
