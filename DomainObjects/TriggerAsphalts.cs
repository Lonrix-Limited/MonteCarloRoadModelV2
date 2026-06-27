using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using JCass_Core.JFunctions;
using JCass_ModelCore.Models;
using JCass_ModelCore.Treatments;

namespace MonteCarloRoadModelV2.DomainObjects;

public static class TriggerAsphalts
{

    public static List<TreatmentInstance> GetTriggeredAsphaltOrOgpaTreatments(RoadSegmentMC segment, int period,
        ModelBase frameworkModel, MonteCarloRoadModelV2 domainModel, Dictionary<string, Dictionary<string, object>> lookups,
        Dictionary<string, object> infoFromModel)
    {
        try
        {
            List<TreatmentInstance> triggeredTreatments = new List<TreatmentInstance>();
                       
            AddPreservationThinACIfValid(segment, domainModel, period, triggeredTreatments, lookups);
            AddHoldingThinACIfValid(segment, frameworkModel, domainModel, period, triggeredTreatments, lookups);
            AddAcHeavyMaintenanceIfValid(segment, period, domainModel, triggeredTreatments, infoFromModel, lookups);
            AddRehabilitationIfValid(segment, domainModel, period, triggeredTreatments, lookups);

            return triggeredTreatments;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error in {nameof(GetTriggeredAsphaltOrOgpaTreatments)} for segment {segment.FeebackCode} on period {period}. Details: {ex.Message}", ex);
        }
    }

    public static bool NextSurfacingIsAsphaltic(RoadSegmentMC segment)
    {
        try
        {
            // First check if pre-processing specified that the next surfacing MUST be Asphaltic. If so, then return true by default
            // This allows us to force the treatment to switch from whatever it is currently to Asphaltic, even if the current surface is not Asphaltic.
            // This is needed to handle cases where the current surface is ChipSeal, but the next surface needs to be Asphaltic
            if (segment.NextSurface == "ac" || segment.NextSurface == "ogpa") return true;

            // If we are not forcing the next surfacing to be Asphaltic, then we need to check if the current surface is Asphaltic. If it
            // is not, then we should not trigger an Asphaltic treatment. Note that 'SurfaceClassForTreatment' regards 'slurry' as 'ac'.
            if (segment.SurfaceClassForTreatment != "ac" && segment.SurfaceClassForTreatment != "ogpa") return false;
            
            // If we are here, the current surface is Asphaltic and NextSurface is not specified as something other than Asphaltic, so we can trigger an
            // Asphaltic treatment
            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error in {nameof(NextSurfacingIsAsphaltic)} for segment {segment.FeebackCode}. Details: {ex.Message}", ex);
        }
    }

    private static void AddRehabilitationIfValid(RoadSegmentMC segment, MonteCarloRoadModelV2 domainModel, int iPeriod, List<TreatmentInstance> treatments, 
        Dictionary<string, Dictionary<string, object>> lookups)
    {
        try
        {
            if (segment.CanRehabFlag == 0) return; // If the segment is not eligible for rehabilitation, do not add a treatment

            string treatmentName = segment.SurfaceClassForTreatment + "_rehab";
            
            double tssScore = TreatmentSuitabilityScorer.GetTSSForRehabilitation(segment, domainModel, iPeriod);
            if (tssScore <= 0) return; // If the TSS score is below the minimum allowed, do not add a treatment

            string reason = $"SLA={Math.Round(segment.SurfaceAchievedLifePercent, 1)}";
            string comment = $"PDS={segment.PavementDistressState}, TSS={Math.Round(tssScore, 2)}";

            double quantity = segment.AreaSquareMetre;
            string unitRateSetKey = segment.SurfaceClassForTreatment + "_rehab_rate";
            var unitRateSet = lookups[unitRateSetKey];
            if (!unitRateSet.ContainsKey(segment.ONRC)) throw new Exception($"Unit rate for ONRC category '{segment.ONRC}' not found in lookup set '{unitRateSetKey}'.");
            double unitRate = Convert.ToDouble(unitRateSet[segment.ONRC]);

            TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: quantity, unitRate: unitRate, false, reason, comment);
            treatment.TreatmentSuitabilityScore = tssScore;
            treatments.Add(treatment);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error in {nameof(AddRehabilitationIfValid)} for segment {segment.FeebackCode} on period {iPeriod}. Details: {ex.Message}", ex);
        }
    }

    private static void AddHoldingThinACIfValid(RoadSegmentMC segment, ModelBase frameworkModel, MonteCarloRoadModelV2 domainModel, int iPeriod, 
                                                List<TreatmentInstance> treatments, Dictionary<string, Dictionary<string, object>> lookups)
    {
        try
        {
            string treatmentName = segment.SurfaceClassForTreatment + "_holding";

            string resurfCode = segment.SurfaceClassForTreatment + "_resurf";
            string hmaintCode = segment.SurfaceClassForTreatment + "_hmaint";

            if (segment.NextSurface == "cs") return;

            // If the current pavement distress state is in the exclusion list for TSS for Holding Actions, then do not add a treatment.
            if (domainModel.Constants.TSSHoldingExclusionStates.Contains(segment.PavementDistressState)) return;

            // For Holding AC, do not eliminate if asphalt overlay is not allowed (in 'segment.AsphaltOkFlag') because of too high deflection etc.
            // This is because this treatment is assumed to include strengthening repairs to adress weak areas

            double tssScore = TreatmentSuitabilityScorer.GetTSSForHoldingAction(segment, domainModel, iPeriod);
            // If the TSS score is below the minimum allowed, do not add a treatment
            if (tssScore <= 0) return;
            
            string reason = $"SLA={Math.Round(segment.SurfaceAchievedLifePercent, 1)}";
            string comment = $"SDS={segment.SurfacingDistressState}; FDS={segment.FlushingDistressState}; TSS={Math.Round(tssScore, 2)}";

            double quantity = segment.AreaSquareMetre;

            double overlayQuantity = quantity;
            double repairQuantity = quantity * RoadSegmentMC.GetExtentEstimateFromStateScore(segment.PavementDistressState);

            var unitRateSet = lookups["unit_rates_general"];
            if (!unitRateSet.ContainsKey(resurfCode)) throw new Exception($"Unit rate for Treatment '{resurfCode}' not found in lookup set 'unit_rates_general'.");
            double acOverlayUnitRate = Convert.ToDouble(unitRateSet[resurfCode]);

            if (!unitRateSet.ContainsKey(hmaintCode)) throw new Exception($"Unit rate for Treatment '{hmaintCode}' not found in lookup set 'unit_rates_general'.");
            double acRepairUnitRate = Convert.ToDouble(unitRateSet[hmaintCode]);

            double overlayCost = overlayQuantity * acOverlayUnitRate;

            // Add a small fraction to the repair cost to ensure that the treatment is not considered as a pure overlay in the budgeting, but
            // rather as a combination of overlay and repair. Otherwise holding actions will sneak through even if the holding budget is zero
            double repairCost = repairQuantity * acRepairUnitRate + 1;

            double totalCost = overlayCost + repairCost;

            double dummyArea = totalCost; // Dummy area which is effectively the cost
            double dummyUnitRate = 1; // Dummy unit rate so that cost is equal to total cost

            TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, 
                                                                quantity: dummyArea, unitRate: dummyUnitRate, 
                                                                force: false, reason: reason, comment: comment);

            // Assign the relative fractions of the cost to the appropriate budget categories
            decimal repairFraction = Convert.ToDecimal(repairCost / totalCost);
            decimal overlayFraction = Convert.ToDecimal(overlayCost / totalCost);
            Dictionary<string, decimal> treatmentFractions = new Dictionary<string, decimal>
            {
                { $"Resurfacing-{segment.SurfaceClassForTreatment.ToUpper()}", overlayFraction },
                { "Pre-Repairs", repairFraction }
            };
            treatment.AssignBudgetCategoryFractions(treatmentFractions);


            treatment.TreatmentSuitabilityScore = tssScore;
            treatments.Add(treatment);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error in {nameof(AddHoldingThinACIfValid)} for segment {segment.FeebackCode} on period {iPeriod}. Details: {ex.Message}", ex);
        }
    }

    private static void AddPreservationThinACIfValid(RoadSegmentMC segment, MonteCarloRoadModelV2 domainModel, int iPeriod, List<TreatmentInstance> treatments,
                                                    Dictionary<string, Dictionary<string, object>> lookups)
    {
        try
        {
            string treatmentName = segment.SurfaceClassForTreatment + "_resurf";
            
            // If asphalt overlay is not allowed because of too high deflection etc, do not add a treatment
            if (segment.CanDoThinACOverlay == 0) return;

            double tssScore = TreatmentSuitabilityScorer.GetTSSForPreservationTreatment(segment, domainModel, iPeriod);

            // If the TSS score is below the minimum allowed, do not add a treatment
            if (tssScore <= 0) return;
            
            string reason = $"SLA={Math.Round(segment.SurfaceAchievedLifePercent, 1)}";
            string comment = $"SDS={segment.SurfacingDistressState}, TSS={Math.Round(tssScore, 2)}";

            double overlayQuantity = segment.AreaSquareMetre;

            var unitRateSet = lookups["unit_rates_general"];
            if (!unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for Treatment '{treatmentName}' not found in lookup set 'unit_rates_general'.");
            double unitRate = Convert.ToDouble(unitRateSet[treatmentName]);

            TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, overlayQuantity, unitRate:unitRate, false, reason, comment);

            treatment.TreatmentSuitabilityScore = tssScore;
            treatments.Add(treatment);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error in {nameof(AddPreservationThinACIfValid)} for segment {segment.FeebackCode} on period {iPeriod}. Details: {ex.Message}", ex);
        }
    }

    private static void AddAcHeavyMaintenanceIfValid(RoadSegmentMC segment, int iPeriod, MonteCarloRoadModelV2 domainModel,
                                                     List<TreatmentInstance> treatments, Dictionary<string, object> infoFromModel,
                                                     Dictionary<string, Dictionary<string, object>> lookups)
    {
        try
        {
            // surface class should be 'ac' or 'ogpa'
            string treatmentName = segment.SurfaceClassForTreatment + "_hmaint";

            double presealAreaFraction = RoadSegmentMC.GetExtentEstimateFromStateScore(segment.PavementDistressState);
            if (presealAreaFraction <= 0) return; // If there is no area in distress, do not add a treatment

            int periodsToLastNonRoutineTreatment = PeriodsToLastTreatmentNotRoutineMaintenance(infoFromModel, iPeriod);

            // Do not add AC Heavy Maintenance if the periods since last non-routine treatment is less than the minimum allowed
            if (periodsToLastNonRoutineTreatment < domainModel.Constants.MinPeriodsBetweenACHeavyMaint) return;

            // If an asphalt overlay is allowed, then only consider this treatment if the Surface Life Achieved is less than the maximum allowed for AC Heavy Maintenance
            // If an asphalt overlay is not allowed (e.g. due to deflection), then we can consider this treatment regardless of the SLA, otherwise the element will
            // have to wait until it can be rehabilitated
            if (segment.CanDoThinACOverlay == 1)
            {
                if (segment.SurfaceAchievedLifePercent > domainModel.Constants.MaxSlaForACHeavyMaint) return;
            }

            double tssScore = 0;
            if (segment.CanRehabFlag == 1)
            {
                // If this is a rehab route, then Preseal must compete with Rehab. Thus calculate the TSS for Preseal since
                // Rehab will be competing based on its TSS score.
                tssScore = TreatmentSuitabilityScorer.GetTSSForHoldingAction(segment, domainModel, iPeriod);
            }
            else
            {
                // If this is NOT a rehab route, then Preseal is considered as a Rehabilitation. Thus the TSS in this case
                // should be based on the TSS for Rehabilitation.
                tssScore = TreatmentSuitabilityScorer.GetTSSForRehabilitation(segment, domainModel, iPeriod);
            }

            if (tssScore <= 0) return; // If the TSS score is below the minimum allowed, do not add a treatment

            string reason = $"SLA={Math.Round(segment.SurfaceAchievedLifePercent, 1)}";
            string comment = $"PDS= {segment.PavementDistressState}; SDS = {segment.SurfacingDistressState}; TSS={Math.Round(tssScore, 2)}";

            double quantity = segment.AreaSquareMetre * presealAreaFraction;

            var unitRateSet = lookups["unit_rates_general"];
            if (!unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for Treatment '{treatmentName}' not found in lookup set 'unit_rates_general'.");
            double unitRate = Convert.ToDouble(unitRateSet[treatmentName]);

            TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity, unitRate: unitRate, false, reason, comment);
            treatment.TreatmentSuitabilityScore = tssScore;
            treatments.Add(treatment);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error in {nameof(AddAcHeavyMaintenanceIfValid)} for segment {segment.FeebackCode} on period {iPeriod}. Details: {ex.Message}", ex);
        }
    }

    private static int PeriodsToLastTreatmentNotRoutineMaintenance(Dictionary<string, object> infoFromModel, int iPeriod)
    {
        try
        {
            if (infoFromModel["previous_treatments"] is null) return 999; // Indicates that no treatments have been placed yet

            List<TreatmentInstance> previousTreatments = (List<TreatmentInstance>)infoFromModel["previous_treatments"];

            TreatmentInstance? lastNonRoutineMaintenanceTreatment = null;

            // Loop over all previous treatments to find the most recent non-routine maintenance treatment
            int minTreatmentPeriod = int.MaxValue;
            foreach (TreatmentInstance treatment in previousTreatments)
            {
                if (treatment.TreatmentName != "RMaint")
                {
                    int periodsToTreatment = iPeriod - treatment.TreatmentPeriod;
                    if (periodsToTreatment < minTreatmentPeriod)
                    {
                        minTreatmentPeriod = periodsToTreatment;
                        lastNonRoutineMaintenanceTreatment = treatment;
                    }
                }
            }
            if (lastNonRoutineMaintenanceTreatment is not null)
            {
                return iPeriod - lastNonRoutineMaintenanceTreatment.TreatmentPeriod;
            }
            else
            {
                return 999; // Indicates that no non-routine treatment has been placed yet
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error in {nameof(PeriodsToLastTreatmentNotRoutineMaintenance)} on period {iPeriod}. Details: {ex.Message}", ex);
        }
    }

}
