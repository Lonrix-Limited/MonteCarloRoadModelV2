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
        // First check for exclusion criteria. If any of these are met, the RNI is zero and we can exit the function immediately.
        if (segment.CanRehabFlag == 0) return 0;
        if (segment.LengthInMetre < constants.MinimumLengthForRehab) return 0;
        if (segment.LengthInMetre > constants.MaximumLengthForRehab) return 0;
        if (segment.RutMeanObserved < constants.RutThresholdForRehabs) return 0;

        if (segment.SurfaceClass == "cs")
        {
            //Return maximal RNI if seal layers is unstable
            if (segment.SurfaceNumberOfLayers >= constants.UnstableSealCount &&
                segment.RutMeanObserved > constants.UnstableRutThreshold) { return 100; }                        
        }

        // Get the pavement distress state score. Should range from zero to 6. Multiply by 10 to get
        // a value between 0 and 60.
        double pavementDistressStateScore = RoadSegmentMC.GetStateScore(segment.PavementDistressState) * 10;

        // Add rut depth. Since rut is normally between 0 and 20, this will add between 0 and 20 to the RNI.
        double rniScore = pavementDistressStateScore + segment.RutMeanObserved;

        //If we are within the window in which we count historical maintenance, then increase the score if there 
        // has been maintenance in the past 3 years. The idea is that if there has been maintenance in the past 3 years and we
        // are still in distress, then this is a stronger signal of rehabilitation need than if there has not been maintenance in
        // the past 3 years.
        if (period <= constants.HistoricalMaintenanceUsePeriods && pavementDistressStateScore > 0)
        {

            // Bump up the RNI score if there is historical maintenance over past 3 years and still distress
            // The historical maintenance is a score from 0 to 1. Multiply by 20 to get a value between 0 and 20, added to the RNI.
            // Also add a smaller bump for historical pothole maintenance, which is a score from 0 to 1. Multiply by 10 to get a
            // value between 0 and 10, which will be added to the RNI.
            // Also add a smaller bump for historical surfacing maintenance, which is a score from 0 to 1. Multiply by 5 to get a
            // value between 0 and 5, which will be added to the RNI.
            double maintenanceHistoryScore = segment.MaintenanceFreqHistoricalPA * 20;
            double potholeHistoryScore = segment.MaintenanceFreqHistoricalPoth * 10;
            double surfacingHistoryScore = segment.MaintenanceSurfacing * 5;

            rniScore = rniScore + maintenanceHistoryScore + potholeHistoryScore + surfacingHistoryScore;

        }        
        return rniScore;

    }


    

    
        
}
