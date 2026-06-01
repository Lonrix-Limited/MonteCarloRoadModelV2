

namespace MonteCarloRoadModelV2.DomainObjects;

public static class TreatmentSuitabilityScorer
{

    public static double GetTSSForPreservationTreatment(RoadSegmentMC segment, MonteCarloRoadModelV2 domainModel, int iPeriod)
    {        
        return segment.SurfacingNeedsIndexRank;
    }

    public static double GetTSSForRehabilitation(RoadSegmentMC segment, MonteCarloRoadModelV2 domainModel, int iPeriod)
    {            
        double tssScore = domainModel.SubModels.TSSForRehabilitation.GetValue(segment.RehabilitationNeedsIndexRank);   //Use RANK, not the RNI itself!!        
        return tssScore;
    }

    public static double GetTSSForHoldingAction(RoadSegmentMC segment, MonteCarloRoadModelV2 domainModel, int iPeriod)
    {                
        // If we get here, a holding action is valid. Calculate the relative suitability score based on the Rehabilitation Needs Index Rank.
        double tssScore = domainModel.SubModels.TSSForHoldingAction.GetValue(segment.RehabilitationNeedsIndexRank);
        return tssScore;
    }

}
