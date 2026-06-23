using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace MonteCarloRoadModelV2.DomainObjects;

public static class CandidateSelector
{

    public static string GetCandidateSelectionOutcome(RoadSegmentMC segment, Constants constants, int periodsToNextTreatment)
    {

        if (segment.CanTreatFlag == 0) return "treat flag is 0";

        // The CSA flag will be evaluated in the NEXT period. However, periodsToNextTreatment would have been evaluated by the framework model in
        // the CURRENT period. Therefore, we need to subtract 1 from periodsToNextTreatment to get the correct value for the next period.
        periodsToNextTreatment = periodsToNextTreatment - 1;
        if (periodsToNextTreatment <= constants.CSMinPeriodsToNextTreat) { return "committed near future"; }
        
        //If the length of the segment is less than any of the allowed minimum lengths for CS, AC/OGPA and Rehab, then it is too short to 
        //be treated
        if (segment.LengthInMetre < constants.MinimumLengthForRehab && 
            segment.LengthInMetre < constants.MinimumLengthForChipseal && 
            segment.LengthInMetre < constants.MinimumLengthForACorOGPA) { return "segment too short"; }

        // If the lengt of the segment is more than the allowed maximum length for CS, AC/OGPA or Rehab then it is too long to be treated
        // with any type of treatment
        if (segment.LengthInMetre > constants.MaximumLengthForRehab && 
            segment.LengthInMetre > constants.MaximumLengthForChipseal && 
            segment.LengthInMetre > constants.MaximumLengthForACorOGPA) { return "segment too long"; }


        if (TriggerChipseals.NextSurfacingIsChipsealTreatment(segment))
        {
            if (segment.SecondCoatNeeded) return "ok - 2nd coat needed";            
            if (segment.SurfaceAchievedLifePercent < constants.CSMinSlaToTreatCs) return "sla too low";
            return "ok";
        }
        else if (TriggerAsphalts.NextSurfacingIsAsphaltic(segment))
        {
            // Candidate Selection Filters:            
            if (segment.SurfaceAchievedLifePercent < constants.CSMinSlaToTreatAc) return "sla too low";
            return "ok";
        }
        else
        {
            // If we get here, surfacing is 'block', 'concrete' or 'other'. Thus there is only one check and that is whether the
            // surface life is now at or over expected life. Thus - birthday treatment
            if (segment.SurfaceRemainingLife > 1) return "birthday type: too young";
            return "ok";
        }
    }


}
