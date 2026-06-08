using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace MonteCarloRoadModelV2.DomainObjects;

public static class RehabilitationNeedsCalculator
{

    public static double GetRehabilitationNeedsIndex(RoadSegmentMC segment, Constants constants, int period)
    {
        return GetRehabilitationNeedsIndexWithoutPavementDistress(segment, constants, period);

        //if (segment.PavementDistressIndex >= 0) // If PDI is available, use it in the calculation
        //{
        //    return GetRehabilitationNeedsIndexWithPavementDistressIndexAvailable(segment, constants, period);
        //}
        //else // If PDI is not available, fall back to using rut depth and surface distress index only
        //{
        //    return GetRehabilitationNeedsIndexWithoutPavementDistress(segment, constants, period);
        //}
    }


    /// <summary>
    /// In this case, we do not have Pavement Distress Index available, so we need to rely on rut depth and maintenance history to estimate rehabilitation needs.
    /// </summary>
    /// <param name="constants">Domain constants used in the calculation</param>
    /// <param name="segment">The road segment for which rehabilitation needs are being calculated</param>
    /// <param name="period">The current period in the simulation</param>
    /// <returns>The rehabilitation needs index</returns>
    private static double GetRehabilitationNeedsIndexWithoutPavementDistress(RoadSegmentMC segment, Constants constants, int period)
    {
        if (segment.LengthInMetre < constants.MinimumLengthForRehab) return 0; // If segment is very short, then rehabilitation need is zero regardless of PDI, rut depth or SDI

        // Increase need for rehabilitation proportionally to how much the observed rut depth and IRI exceed the thresholds, using a non-linear function to reflect that needs increase more rapidly as rut depth and IRI get worse. If
        // they are below the threshold, then they do not contribute to rehabilitation needs.
        double rutFactor = segment.RutMeanObserved < constants.TSSExcessRutThresh ? 0 : Math.Pow(segment.RutMeanObserved - constants.TSSExcessRutThresh, 1.5);
        double iriFactor = segment.IRIMeanObserved < constants.TSSExcessIRIThresh ? 0 : Math.Pow(segment.IRIMeanObserved - constants.TSSExcessIRIThresh, 1.5);

        //double paMaintFactor = segment.MaintenancePavement * 10;
        //double suMaintFactor = rutFactor > 0 ? segment.MaintenanceSurfacing * 3 : 0; // Only consider surface maintenance if rut is above threshold
        //double pothMaintFactor = rutFactor > 0 ? segment.MaintenancePotfill * 3 : 0; // Only consider pothole maintenance if rut is above threshold

        //double needsIndex = rutFactor + iriFactor + paMaintFactor + suMaintFactor + pothMaintFactor;
        double needsIndex = rutFactor + iriFactor;
        return Math.Max(0, needsIndex);
    }

    /// <summary>
    /// Case where we DO have Pavement Distress Index available, so we can use it in combination with rut depth and surface distress index to estimate rehabilitation needs more accurately.
    /// </summary>
    /// <param name="constants">Domain constants used in the calculation</param>
    /// <param name="segment">The road segment for which rehabilitation needs are being calculated</param>
    /// <param name="period">The current period in the simulation</param>
    /// <returns>The rehabilitation needs index</returns>
    private static double GetRehabilitationNeedsIndexWithPavementDistressIndexAvailable(RoadSegmentMC segment, Constants constants, int period)
    {
        if (segment.LengthInMetre < constants.MinimumLengthForRehab) return 0; // If segment is very short, then rehabilitation need is zero regardless of PDI, rut depth or SDI

        if (segment.PavementDistressIndex < constants.MinimumPdiForRehab(segment.SurfaceClass) && segment.RutMeanObserved < 15) return 0; // If PDI is very low, then rehabilitation need is zero regardless of rut depth or SDI, except if Rut is above 15 mm
        
        double needsIndexBase = GetRehabilitationNeedsIndexWithoutPavementDistress(segment, constants, period); // Start with the needs index calculated without PDI

        // If PDI is above the threshold, then we need to increase the rehabilitation needs index to reflect the higher level of distress
        double kFactor = 0.4;
        double needsIndexFinal = (kFactor * needsIndexBase) + segment.PavementDistressIndex;

        return Math.Max(0, needsIndexFinal);
    }
        
}
