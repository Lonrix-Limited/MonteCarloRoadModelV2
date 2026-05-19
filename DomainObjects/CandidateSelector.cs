using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace MonteCarloRoadModelV1.DomainObjects;

public static class CandidateSelector
{

    public static string GetCandidateSelectionOutcome(RoadSegmentMC segment, Constants constants, int periodsToNextTreatment)
    {
        // The CSA flag will be evaluated in the NEXT period. However, periodsToNextTreatment would have been evaluated by the framework model in
        // the CURRENT period. Therefore, we need to subtract 1 from periodsToNextTreatment to get the correct value for the next period.
        periodsToNextTreatment = periodsToNextTreatment - 1;
        if (periodsToNextTreatment <= constants.CSMinPeriodsToNextTreat) { return "committed near future"; }
        if (segment.LengthInMetre < constants.CSMinLengthToTreatAny) { return "segment too short"; }

        if (TriggerChipseals.NextSurfacingIsChipsealTreatment(segment))
        {
            if (segment.SecondCoatNeeded) return "ok - 2nd coat needed";
            if (segment.SurfaceDistressIndex < constants.CSMinSDIToTreat &&
                segment.PavementDistressIndex < constants.CSMinPDIToTreat) return "pdi and sdi below threshold";
            if (segment.SurfaceAchievedLifePercent < constants.CSMinSlaToTreatCs) return "sla too low";


            return "ok";
        }
        else if (TriggerAsphalts.NextSurfacingIsAsphaltic(segment))
        {
            // Candidate Selection Filters:
            if (segment.SurfaceDistressIndex < constants.CSMinSDIToTreat &&
                segment.PavementDistressIndex < constants.CSMinPDIToTreat) return "pdi and sdi below threshold";

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
