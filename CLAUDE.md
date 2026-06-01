# MonteCarloRoadModelV2

A custom C# **Domain Model** plugin for the [Juno Cassandra](https://lonrix-limited.github.io/jcass_docs2/) infrastructure deterioration modelling framework. It implements a Monte Carlo road pavement deterioration model for NZTA state highway forecasting (see the `CopyDllToDestination` target in the `.csproj` — the build drops the DLL into an NZTA SH forecasting project's `domain_model` folder).

## How this plugs into Cassandra

Cassandra is a .NET/C# framework for Infrastructure Deterioration Modelling. A project folder contains a `domain_model/` subfolder where the compiled domain-model DLL plus its setup CSVs live. At runtime the Cassandra desktop app loads the DLL, instantiates the `DomainModelBase` subclass, and calls its lifecycle hooks per element (road segment) and per period.

The framework side lives in sibling repos referenced via `ProjectReference` in [MonteCarloRoadModelV2.csproj](MonteCarloRoadModelV2.csproj):
- `..\..\cassandra_main\JCass_Core` — core utilities, statistics, piecewise linear models, normal generators
- `..\..\cassandra_main\JCass_ModelCore` — `DomainModelBase`, `ModelBase`, `TreatmentInstance`, MonteCarlo helpers (`DistributionSimulator`), lookups

Target framework: **net9.0** (nullable + implicit usings enabled).

## Lifecycle entry points (DomainModelBase overrides)

All framework calls enter through [DomainObjects/MonteCarloRoadModelV2.cs](DomainObjects/MonteCarloRoadModelV2.cs):

| Override | When called | Delegates to |
|---|---|---|
| `SetupInstance()` | Once at model start, after `model` is wired up | Constructs `Initialiser`, `Incrementer`, `Resetter`, `RoutineMaintenanceModeller`, `SubModelDefinitions`, `Constants`; loads all sub-models via `SetupUtilities` from CSVs in `{workFolder}/domain_model/` |
| `Initialise(iElem, numInputs, textInputs, sinks…)` | Period 0, each element | [Initialiser](DomainObjects/Initialiser.cs) → builds a `RoadSegmentMC` from raw `inp_*` columns, then writes parameter values back via the numeric/text sink `Action`s |
| `Increment(iElem, iPeriod, …)` | Each period, elements without a selected treatment | [Incrementer](DomainObjects/Incrementer.cs) → advances age, traffic growth, and draws HSD deterioration (rut, IRI, texture) from sub-models |
| `Reset(treatment, iElem, iPeriod, …)` | Each period, elements that received a treatment | [Resetter](DomainObjects/Resetter.cs) → updates surfacing properties via lookups, re-draws HSD values from reset simulators |
| `GetTreatmentCandidates(...)` | Each element/period, MCDA triggering | [TreatmentsTrigger](DomainObjects/TreatmentsTrigger.cs) → gates on `CandidateSelectionOutcome == "ok"` + `periods_to_next_treatment`, dispatches on `NextSurface` to [TriggerAsphalts](DomainObjects/TriggerAsphalts.cs) / [TriggerChipseals](DomainObjects/TriggerChipseals.cs) / birthday-treatment branch. Fully active in V1. |
| `GetTriggeredMaintenance(...)` | After treatment selection | Returns `null!` by design — routine maintenance is modelled as a per-period probabilistic *cost* via `RoutineMaintenanceModeller` + PA/potfill probability+extent simulators, NOT as a framework-triggered `TreatmentInstance`. |
| `DoEndOfPeriodCalculations(iPeriod)` | End of each period, after all elements processed | No-op |

The sinks passed as `Action<string, double>` / `Action<string, string>` are how you write parameter values back into the framework matrices; see `RoadSegmentMC.SetParameterValues` for the full `par_*` contract.

## The RoadSegmentMC domain object

[DomainObjects/RoadSegmentMC.cs](DomainObjects/RoadSegmentMC.cs) is the central POCO — a single road segment with identification, quantity, surfacing/pavement, ONRC/rainfall, traffic, HSD (rut/IRI/texture) and maintenance properties. Key conventions:

- **Latent vs Observed** HSD values: `RutMeanLatent` drives deterioration; `RutMeanObserved = latent + normal residual` is reported. Same pattern for IRI and texture.
- **Episode lengths**: `RutAndIRIIncrementEpisodeLength` and `TextureIncrementEpisodeLength` count years since the last increment was drawn; when they exceed `Constants.MaximumEpisodeLength*`, a fresh increment is drawn from the distribution simulator (see `Incrementer.CheckRutAndIRIIncrementForEpisode`).
- **SurfaceClassForRules**: maps `concrete` / `unknown` → `other` before passing to cohort rules (model-building pipeline used those bucketings).
- **`par_*` write contract** (`SetParameterValues`): adt, hcv, pave_age/remlife/life_ach, surf_mat/class/thick/layers/func/exp_life/age/life_ach/remain_life, rut/iri/text (latent + observed + increment + episode length), maint_pa, maint_poth. Ranking parameters (`para_sla_rank`, `para_rut_rank`, `para_iri_rank`) are filled by the framework, not here.
- **`inp_*` read contract**: see [RoadSegmentFactoryMC.GetFromRawData](DomainObjects/RoadSegmentFactoryMC.cs) for the authoritative list of raw input columns the model setup Excel must provide.

## Sub-models and setup CSVs

[SubModelDefinitions](DomainObjects/SubModelDefinitions.cs) holds every stochastic component; [Utilities/SetupUtilities.cs](Utilities/SetupUtilities.cs) populates them at startup by reading CSVs from `{workFolder}/domain_model/`:

- **Increment simulators** (`DistributionSimulator`) for rut/IRI/texture rates — `cohorts_b_increments_*.csv`
- **Residual SD functions** (`PieceWiseLinearModel`) — `inc_resids_plm_setup_codes.csv`
- **Pothole-fill / PA maintenance probability models** (`LogisticModel`, AC + CS variants) read from R-exported coefficient CSVs with `term`/`estimate` columns
- **Maintenance extent simulators** (PA and potfill) — `cohorts_d_maint_model_data_*.csv`
- **Reset simulators** — separate rut/IRI simulators for resurf vs rehab; a single texture reset simulator across treatment types
- **Reduction-after-PA-maintenance simulators** for rut and IRI

All draws share the framework-seeded `NormalGenerator` / `model.Random`, so runs are reproducible from `model.RandomSeed`.

## Constants

[Constants](DomainObjects/Constants.cs) loads everything tunable from the framework's `Lookups` dictionary-of-dictionaries (populated from the project's lookup sets). Lookup-set keys actually used:
- `general` — `base_date`, `short_term_periods`
- `candidate_selection` — SLA/SDI/PDI thresholds and min-period gates
- `maint_pred` — maintenance cost calibration + PDI threshold
- `treatment_suitability_scores` — TSS rehab/holding/preserve rank curves and caps
- `mcda_treatment_triggering` — AC heavy-maintenance SLA cap and min-period gate
- `episode_length_max` — max episode lengths for rut/IRI and texture increment redraw

`Resetter` additionally consults `pavement_expected_life`, `treat_surf_materials`, `treat_surf_class`, `surf_thickness_new`, `surf_thickness_add`, `surf_life_exp`. `RoadSegmentFactoryMC` uses `road_class`. These must all exist in the project's lookup configuration.

## Deterioration logic at a glance

**Increment** (no treatment, [Incrementer](DomainObjects/Incrementer.cs)):
1. Traffic grows, pavement/surface age += 1
2. Check episode length; if expired, draw fresh `RutIncrement` / `IRIIncrement` / `TextureIncrement` from the cohort-based distribution simulators (keyed on rut/iri/surface age/adt/heavy%/surf_thick/rainfall/surf_class/surf_count)
3. Add increment to latent; add normal residual (SD from the piecewise-linear residual function) to get observed
4. If PA maintenance occurred in the preceding period, apply a reduction to rut/IRI instead of an increment (never below 1.5 mm rut / 0.5 IRI floor)
5. `MaintenanceModel.UpdateRoutineMaintenanceExtents(segment)` samples next-period maintenance extents from the probability + extent simulators

**Reset** (after treatment, [Resetter](DomainObjects/Resetter.cs)):
- Treatment type is `rehab` if the treatment name contains "rehab", else `resurf`.
- Rehab → `PavementAge = 0`, fresh single-layer surface; resurf → `SurfaceThickness += add`, `SurfaceNumberOfLayers += 1`, surface function transitions `1 → 2 → R`.
- Rut/IRI/texture reset values come from treatment-type-specific simulators; a new episode increment is drawn immediately.

**Initialise** ([Initialiser](DomainObjects/Initialiser.cs)): for each HSD metric, decides whether the raw value is still valid or whether the element was resurfaced/rehabilitated *after* the HSD survey — if so, simulates a reset value and a fresh increment. Uses `HSDSurveyDate` vs `SurfaceAge`/`PavementAge` relative to `Constants.BaseDate`.

## Build and deploy

```
dotnet build MonteCarloRoadModelV2.sln
```

An `AfterTargets="Build"` target in the csproj automatically copies the built DLL to:
```
C:\Users\fritz\Juno Services Dropbox\projects\nzta\2026\sh_forecast_model\monte_carlo\cassandra\domain_model\
```
That destination is the target Cassandra project's `domain_model` folder. If you're not Fritz or don't have that Dropbox path, either edit/remove the target or accept the build-time copy failure.

The project references sibling repos (`..\..\cassandra_main\JCass_Core` and `..\..\cassandra_main\JCass_ModelCore`) by relative path — they must exist on disk alongside this repo (typical layout: `cassandra/cassandra_main/...` and `cassandra/domain_models/MonteCarloRoadModelV2`).

## Conventions worth knowing

- **Debug breakpoints**: scattered `if (iElemIndex == N) { int kk = 9; }` blocks in `Initialise`/`Increment`/`Reset`/`Incrementer.Increment` are deliberate breakpoint anchors — leave them unless doing a cleanup pass.
- **Errors are wrapped and re-thrown** with element context (`$"Error ... on element index {iElemIndex}"`) so framework logs point at the offending segment. Preserve this when adding new code paths.
- **`model.LogMessage(...)`** is the framework-visible warning channel; used for anomalies like future-dated surfacing.
- **Surface-class lowercase invariant**: `SurfaceClass`, `UrbanRural`, `ONRC` setters force lowercase. Don't bypass.
- **GetSpecialPlaceholderValues(...)** is fetched at the top of Initialise/Increment/Reset/GetTreatmentCandidates and passed into formula-update calls. Most of those are currently commented out while formula/MCDA logic is being ported — keep the fetch in place.
- **V1 scope**: MCDA triggering (`GetTreatmentCandidates`) is fully wired — see [TreatmentsTrigger](DomainObjects/TreatmentsTrigger.cs) + [CandidateSelector](DomainObjects/CandidateSelector.cs) + [TriggerAsphalts](DomainObjects/TriggerAsphalts.cs) + [TriggerChipseals](DomainObjects/TriggerChipseals.cs) + [TreatmentSuitabilityScorer](DomainObjects/TreatmentSuitabilityScorer.cs). The only deliberate stub is `GetTriggeredMaintenance` (returns `null!`); routine maintenance is sampled by `RoutineMaintenanceModeller` inside `Increment` rather than emitted through the framework hook. The `segment.UpdateFormulaValues(...)` calls in `Initialise` / `Reset` / `GetTreatmentCandidates` are commented out because formula-update logic has been replaced by direct property computation in `RoadSegmentFactoryMC` / `RoadSegmentMC` — the trigger pipeline reads already-populated `CandidateSelectionOutcome`, `PavementDistressIndex`, `SurfaceDistressIndex`, `SurfaceAchievedLifePercent`, etc. from the segment.

## File map

- [DomainObjects/MonteCarloRoadModelV2.cs](DomainObjects/MonteCarloRoadModelV2.cs) — `DomainModelBase` subclass, lifecycle entry points
- [DomainObjects/RoadSegmentMC.cs](DomainObjects/RoadSegmentMC.cs) — segment POCO, `par_*` write contract
- [DomainObjects/RoadSegmentFactoryMC.cs](DomainObjects/RoadSegmentFactoryMC.cs) — `inp_*` read contract
- [DomainObjects/Initialiser.cs](DomainObjects/Initialiser.cs) — period-0 segment construction with post-survey treatment handling
- [DomainObjects/Incrementer.cs](DomainObjects/Incrementer.cs) — per-period deterioration, episode-length logic
- [DomainObjects/Resetter.cs](DomainObjects/Resetter.cs) — post-treatment state update
- [DomainObjects/RoutineMaintenanceModeller.cs](DomainObjects/RoutineMaintenanceModeller.cs) — samples PA + potfill probability/extent
- [DomainObjects/CandidateSelector.cs](DomainObjects/CandidateSelector.cs) — Stage-1 gating ("ok" / reason string)
- [DomainObjects/TreatmentsTrigger.cs](DomainObjects/TreatmentsTrigger.cs) — Stage-2 dispatcher by `NextSurface`
- [DomainObjects/TriggerAsphalts.cs](DomainObjects/TriggerAsphalts.cs) — AC/OGPA candidates: Preservation, Holding (composite), Heavy Maintenance, Rehabilitation
- [DomainObjects/TriggerChipseals.cs](DomainObjects/TriggerChipseals.cs) — Chipseal candidates: Second-coat (forced), Preservation, Preseal repair, Rehabilitation
- [DomainObjects/TreatmentSuitabilityScorer.cs](DomainObjects/TreatmentSuitabilityScorer.cs) — TSS arbitration via PWL curves on `RehabilitationNeedsIndexRank`; preservation uses `SurfacingNeedsIndexRank` directly
- [DomainObjects/SubModelDefinitions.cs](DomainObjects/SubModelDefinitions.cs) — container for all simulators/statistical sub-models
- [DomainObjects/Constants.cs](DomainObjects/Constants.cs) — tunables loaded from project lookups
- [Utilities/SetupUtilities.cs](Utilities/SetupUtilities.cs) — CSV-driven sub-model wiring

## Reference links

- Juno Cassandra docs: https://lonrix-limited.github.io/jcass_docs2/
- Project folder guide: https://lonrix-limited.github.io/jcass_docs2/guide/guide_project_folder.html
- JFunctions reference: https://lonrix-limited.github.io/jcass_docs2/jfuncs/jfuncs_a_overview.html
