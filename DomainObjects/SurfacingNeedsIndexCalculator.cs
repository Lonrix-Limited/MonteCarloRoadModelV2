using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonteCarloRoadModelV2.DomainObjects;

public static class SurfacingNeedsIndexCalculator
{

    public static double GetSurfacingNeedsIndex(RoadSegmentMC segment, Constants constants, int period)
    {

        // If there is valid condition state information in the input data and model, then use it to get the base SNI score
        // from the surfacing distress state. Should range from zero to 6. Multiply by 10 to get a value between 0 and 60.
        double baseSNI = RoadSegmentMC.GetStateScore(segment.SurfacingDistressState, constants.HasConditionStateData) * 10;

        if (segment.ElementIndex == 29238)
        {
            _ = 9;
        }

        if (segment.SurfaceClass == "cs")
        {
            // Check if the segment length is within the range for chipseal application. If not, then surface treatment need is zero.
            if (segment.LengthInMetre < constants.MinimumLengthForChipseal) return 0.0;
            if (segment.LengthInMetre > constants.MaximumLengthForChipseal) return 0.0;

            // If surface has not achieved a minimum percentage of its expected life, then surface treatment need is zero
            if (segment.SurfaceAchievedLifePercent < constants.MinSlaToResurfaceCs) return 0.0;

            // If rut is above a certain threshold, then surface treatment need is zero
            if (segment.RutMeanObserved > constants.MaxRutForPreservationCS) return 0.0; 

            // Do not consider surface treatment if number of layers is above specified threshold (stability risk).
            if (segment.SurfaceNumberOfLayers > constants.MaxSealCountForChipSeal) return 0.0;

            double lowTextureFactor = Math.Max(0, constants.TextureThresholdForChipSeal - segment.TextureMeanObserved) * constants.TexturePenaltyFactorForChipSeal;

            // Flushing state score ranges from 0 to 6, multiply by 10 to get a value between 0 and 60.
            double flushingStateScore = RoadSegmentMC.GetStateScore(segment.FlushingDistressState, constants.HasConditionStateData) * constants.FlushingPenaltyFactor;

            baseSNI += lowTextureFactor + flushingStateScore + segment.RutMeanObserved * 5;                            

        }
        else if (segment.SurfaceClass == "ac" || segment.SurfaceClass == "ogpa" || segment.SurfaceClass == "slurry")
        {
            // Check if the segment length is within the range for AC/OGPA application. If not, then surface treatment need is zero.
            if (segment.LengthInMetre < constants.MinimumLengthForACorOGPA) return 0.0;
            if (segment.LengthInMetre > constants.MaximumLengthForACorOGPA) return 0.0;

            // If surface has not achieved a minimum percentage of its expected life, then surface treatment need is zero
            if (segment.SurfaceAchievedLifePercent < constants.MinSlaToResurfaceACandOGPA) return 0.0;

            // If rut is above a certain threshold, then surface treatment need is zero                        
            if (segment.RutMeanObserved > constants.MaxRutForPreservationAC) return 0.0;

            baseSNI += segment.RutMeanObserved * 5;
        }

        // If within the window to consider Skid Resistance, then add to the SNI if Skid Resistance is below the threshold. 
        if (period <= constants.SkidResistanceMaxPeriod)
        {
            double skidBelowTLPercent = segment.SkidResistanceBelowTLPercent;  //Value from 0 to 100
            baseSNI += skidBelowTLPercent;
        }

        return baseSNI;
    }



}