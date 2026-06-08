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
        return GetSurfacingNeedsIndexWithoutDistressData(segment, constants, period);

        //if (segment.SurfaceDistressIndex >= 0) // If SDI is available, use it in the calculation
        //{
        //    return GetSurfacingNeedsIndexWithDistressDataAvailable(segment, constants, period);
        //}
        //else // If SDI is not available, fall back to using rut depth and surface distress index only
        //{
        //    return GetSurfacingNeedsIndexWithoutDistressData(segment, constants, period);
        //}
    }

    private static double GetSurfacingNeedsIndexWithoutDistressData(RoadSegmentMC segment, Constants constants, int period)
    {
        
        double suMaintFactor = segment.MaintenanceSurfacing * 10;
        double potholeFactor = segment.MaintenancePotfill * 5;

        double baseSNI = suMaintFactor + potholeFactor;

        if (segment.SurfaceClass == "cs")
        {            
            if (segment.SurfaceAchievedLifePercent < constants.MinSlaToResurfaceCs) return 0.0; // If surface has not achieved a minimum percentage of its expected life, then surface treatment need is zero
            if (segment.RutMeanObserved > constants.MaxRutForPreservationCS) return 0.0; // If rut is above a certain threshold, then surface treatment need is zero

            // Do not consider surface treatment if number of layers is above specified threshold (stability risk).
            if (segment.SurfaceNumberOfLayers > constants.MaxSealCountForChipSeal) return 0.0;      
            
            double lowTextureFactor = Math.Max(0, constants.TextureThresholdForChipSeal - segment.TextureMeanObserved) * constants.TexturePenaltyFactorForChipSeal;
            double rutPenalty = Math.Min(constants.TSSExcessRutThresh - segment.RutMeanObserved, 0); // Apply penalty (negative value) if rut depth exceeds the threshold, otherwise no penalty
            baseSNI += (lowTextureFactor + rutPenalty);
            return Math.Max(0, baseSNI);
        }
        else if (segment.SurfaceClass == "ac" || segment.SurfaceClass == "ogpa" || segment.SurfaceClass == "slurry")
        {                     
            if (segment.SurfaceAchievedLifePercent < constants.MinSlaToResurfaceACandOGPA) return 0.0; // If surface has not achieved a minimum percentage of its expected life, then surface treatment need is zero
            if (segment.RutMeanObserved > constants.MaxRutForPreservationAC) return 0.0; // If rut is above a certain threshold, then surface treatment need is zero            
            return Math.Max(0, baseSNI);
        }
        else
        {
            // For other surface classes, we do not have specific thresholds or adjustments, so we just return the base SNI if it is above zero, otherwise return zero.
            return Math.Max(0, baseSNI);
        }

    }

    private static double GetSurfacingNeedsIndexWithDistressDataAvailable(RoadSegmentMC segment, Constants constants, int period)
    {

        double baseSNI = GetSurfacingNeedsIndexWithoutDistressData(segment, constants, period); // Start with the SNI calculated without distress data
        baseSNI += segment.SurfaceDistressIndex;

        if (segment.SurfaceClass == "cs")
        {
            // Do not consider surface treatment if pavement distress is above specified threshold
            if (segment.PavementDistressIndex > constants.MaxPDIForChipsealResurfacing) return 0.0;
            if (segment.SurfaceDistressIndex < constants.MinSdiToResurfaceCs) return 0.0; // If SDI is very low, then surface treatment need is zero regardless of PDI or rut depth
            
            return Math.Max(0, baseSNI);

        }
        else if (segment.SurfaceClass == "ac" || segment.SurfaceClass == "ogpa" || segment.SurfaceClass == "slurry")
        {
            if (segment.PavementDistressIndex > constants.MaxPDIforACorOGPAResurfacing) return 0.0;
            if (segment.SurfaceDistressIndex < constants.MinSdiToResurfaceACandOGPA) return 0.0; // If SDI is very low, then surface treatment need is zero regardless of PDI or rut depth
            
            return Math.Max(0, baseSNI);
        }
        else
        {
            // For other surface classes, we do not have specific thresholds or adjustments, so we just return the base SNI if it is above zero, otherwise return zero.
            return Math.Max(0, baseSNI);
        }
    }

}