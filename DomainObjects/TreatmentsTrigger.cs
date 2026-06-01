
using JCass_Core.JFunctions;
using JCass_ModelCore.Models;
using JCass_ModelCore.Treatments;

namespace MonteCarloRoadModelV2.DomainObjects;

/// <summary>
/// Class for checking treatments triggering 
/// </summary>
public class TreatmentsTrigger
{
    private ModelBase _frameworkModel;
    private MonteCarloRoadModelV2 _domainModel;

    public TreatmentsTrigger(ModelBase frameworkModel, MonteCarloRoadModelV2 domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public List<TreatmentInstance> GetTriggeredTreatments(RoadSegmentMC segment, int period, Dictionary<string, object> infoFromModel)
    {        
        List<TreatmentInstance> triggeredTreatments = new List<TreatmentInstance>();

        if (period > 2 && segment.ElementIndex == 10023)
        {
            _ = 0; // breakpoint anchor — set/remove IDE breakpoint here at runtime
        }

        // Check if the segment passes the Candidate Selection checks. If not, return an empty list.
        if (segment.CandidateSelectionOutcome.StartsWith("ok") == false) return triggeredTreatments;

        // Although we check if Periods to Next Treatment (i.e. committed) in the Candidate Selection, we need to do it 
        // again here, because the Candidate Selection result was last evaluated at the last epoch, while the periods to
        // next treatment have now changed since the period has changed
        int periodsToNextTreatment = Convert.ToInt32(infoFromModel["periods_to_next_treatment"]);
        if (periodsToNextTreatment <= _domainModel.Constants.CSMinPeriodsToNextTreat) { return triggeredTreatments; }
                
        //---------------------------------------------------------------------------------------------------------------------------------
        //      If we get here, we know that no second coats or birthday treatments are added.
        //      Now find candidate treatments to add to the optimisation stage
        //---------------------------------------------------------------------------------------------------------------------------------

        if (TriggerChipseals.NextSurfacingIsChipsealTreatment(segment))
        {
            return TriggerChipseals.GetTriggeredChipsealTreatments(segment, period, _domainModel, _frameworkModel.Lookups);
        }
        else if (TriggerAsphalts.NextSurfacingIsAsphaltic(segment))
        {
            // If we get here, the surfacing type should be 'ac' or 'ogpa'. Double check and throw an exception if not
            if (segment.NextSurface != "ac" && segment.NextSurface != "ogpa" && segment.NextSurface != "slurry")
            {
                throw new Exception($"Unexpected surfacing type for segment {segment.ElementIndex}. Expected 'ac' or 'ogpa', but got '{segment.NextSurface}'");
            }

            // Safe to get an AC or OGPA rehabilitation treatment
            return TriggerAsphalts.GetTriggeredAsphaltOrOgpaTreatments(segment, period, _frameworkModel, _domainModel, _frameworkModel.Lookups, infoFromModel);
        }
        else
        {
            return GetBirthdayTreatmentBlocksOrConcreteIfValid(segment, period, _frameworkModel.Lookups);
        }

    }
        
    private List<TreatmentInstance> GetBirthdayTreatmentBlocksOrConcreteIfValid(RoadSegmentMC segment, int iPeriod, Dictionary<string, Dictionary<string, object>> lookups)
    {
        List<TreatmentInstance> treatments = new List<TreatmentInstance>();
        
        string treatmentName = "";

        switch (segment.NextSurface)
        {
            case "blocks":
                treatmentName = "blocks";
                break;
            case "concrete":
                treatmentName = "concrete";
                break;
            case "other":
                treatmentName = "xtreat";
                break;
            default:
                //If we get here, it is ChipSeal or Asphalt, which are not valid for this treatment
                throw new Exception($"Unexpected NextSurface type for birthday treatment for segment {segment.ElementIndex}. Expected 'blocks', 'concrete', or 'other', but got '{segment.NextSurface}'");
        }

        var unitRateSet = lookups["unit_rates_general"];
        if (!unitRateSet.ContainsKey(treatmentName)) throw new Exception($"Unit rate for Treatment '{treatmentName}' not found in lookup set 'unit_rates_general'.");
        double unitRate = Convert.ToDouble(unitRateSet[treatmentName]);

        if (segment.SurfaceRemainingLife > 1) return treatments; // If the surface remaining life is greater than 1, do not add a treatment
        if (iPeriod < segment.EarliestTreatmentPeriod) return treatments; // If the period is less than the earliest treatment period, do not add a treatment

        //If we get here, a birthday treatment is valid
        double quantity = segment.AreaSquareMetre;
        bool forceTreatment = true;
        TreatmentInstance treatment = new TreatmentInstance(segment.ElementIndex, treatmentName, iPeriod, quantity: quantity, unitRate: unitRate, 
                                                            force: forceTreatment, reason: "Birthday treatment", comment: "");
        treatment.TreatmentSuitabilityScore = 102; // Set a high suitability score for second coat treatments
        
        treatments.Add(treatment);
        return treatments;
    }
   

}
