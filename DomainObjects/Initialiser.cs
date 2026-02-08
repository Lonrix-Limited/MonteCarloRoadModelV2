using JCass_ModelCore.Models;


namespace MonteCarloRoadModelV1.DomainObjects;

/// <summary>
/// Class to handle initialisation, including helper functions and some domain logic.
/// </summary>
public class Initialiser
{
    private ModelBase _frameworkModel;
    private MonteCarloRoadModelV1 _domainModel;

    public Initialiser(ModelBase frameworkModel, MonteCarloRoadModelV1 domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public RoadSegmentMC InitialiseSegment(int iElemIndex)
    {

        if (iElemIndex == 8426)
        {
            int kk = 9;
        }

        // Create a new RoadSegmentMC object based purely on the raw data provided in the string array.
        RoadSegmentMC segment = RoadSegmentFactoryMC.GetFromRawData(_frameworkModel, iElemIndex);

        // Now do checks on the values and handle any anomalous data

        segment.AverageDailyTraffic = Math.Max(1, segment.AverageDailyTraffic); // Ensure ADT is at least 1
        segment.PavementAge = GetPavementAge(segment); 
        segment.SurfaceAge = GetSurfacingAge(segment); 
        
        //segment.RutParameterValue = GetInitialRuttingValue(segment);
        //segment.RutIncrement = GetRutIncrementEstimate(segment);

        //segment.Naasra85 = GetInitialNaasraValue(segment);
        //segment.NaasraIncrement = GetNaasraIncrementEstimate(segment);
               
        
        return segment;
    }
        
    private double GetPavementAge(RoadSegmentMC segment)
    {
        try
        {            
            double age = (_domainModel.Constants.BaseDate - segment.PavementDate).TotalDays / 365.25; // Use 365.25 to account for leap years
            
            // To duplicate jFunction setup, we must round age to 2 decimals
            age = Math.Round(age, 2);

            if (age < 0)
            {
                _frameworkModel.LogMessage($"Pavement date for segment {segment.FeebackCode} is in the future", false);
            }
            return age;
        }
        catch(Exception ex)
        {
            throw new Exception($"Error calculating pavement age for segment {segment.FeebackCode}: {ex.Message}");
        }
    }

    private double GetSurfacingAge(RoadSegmentMC segment)
    {
        try
        {            
            double age = (_domainModel.Constants.BaseDate - segment.SurfacingDate).TotalDays / 365.25; // Use 365.25 to account for leap years

            // To duplicate jFunction setup, we must round age to 2 decimals
            age = Math.Round(age, 2);
                                                                                          
            if (age < 0)
            {
                _frameworkModel.LogMessage($"Surfacing date for segment {segment.FeebackCode} is in the future", false);
            }
            return Math.Max(age, 0.1);  //Ensure age is not zero to avoid division by zero errors
        }
        catch (Exception ex)
        {
            throw new Exception($"Error calculating surfacing age for segment {segment.FeebackCode}: {ex.Message}");
        }
    }

    private double GetHSDSurveyAge(RoadSegmentMC segment)
    {        
        double age = (_domainModel.Constants.BaseDate - segment.HSDSurveyDate).TotalDays / 365.25; // Use 365.25 to account for leap years        
        if (age < 0)
        {
            _frameworkModel.LogMessage($"HSD Survey date for segment {segment.FeebackCode} is in the future", false);
        }
        return age;
    }
        
    /// <summary>
    /// Get the initial rutting value, taking into account the HSD survey age and the Surfacing and Pavement ages. There are
    /// three possibilities:
    /// <para>1. The HSD survey is older than the Pavement Age: In this case we presume the segment has been rehabilitated
    /// after the survey and return the value in lookup set 'rehab_resets_rut'</para>
    /// <para>2. The HSD survey is not older than the Pavement Age but older than Surface Age: In this case we presume the 
    /// segment has been resurfaced after the survey and calculate the resetted value based on how much the raw rutting value 
    /// (calculated as the maximum of the LWP and RWP 85th percentile rut values) exceeds the reset exceedance threshold, and 
    /// return the resetted value using a formula</para>
    ///</para>
    /// <para>3. The HSD survey is not older than the Pavement Age or the Surface age - return the maximum of the LWP and RWP 85th percentile ruts    
    ///</para>
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    private double GetInitialRuttingValue(RoadSegmentMC segment)
    {
        double surveyAge = GetHSDSurveyAge(segment);

        // If segment has been rehabilitated, return the lookup value for the rutting reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        if (hasBeenRehabilitated) {
            return _domainModel.GetLookupValueNumber("rehab_resets_rut", "all_cats");
        }

        double ruttingRaw = segment.RutMean;

        // If segment has been resurfaced, determine the rutting exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            double resetExceedenceThreshold = _domainModel.GetLookupValueNumber("reset_exceed_thresh_rut", "preserve");
            double resetImprovementFactor = _domainModel.GetLookupValueNumber("reset_perc_improv_facts_rut", "preserve");

            //double resetValue = CalculationUtilities.GetResetBasedOnExceedanceConcept(ruttingRaw, resetExceedenceThreshold, resetImprovementFactor);
            //return resetValue;
            return -1; // Placeholder until rut increment estimation logic is finalised
        }

        // If segment has not been rehabilitated or resurfaced, use the raw rutting value
        return ruttingRaw;

    }


    /// <summary>
    /// Get an estimate of the initial rut rate, in mm per year, based on the current rut value and the surface age. Here,
    /// we take into consideration that there is always some initial settlement. The effective rut rate is 
    /// based on the current rut minus the setting-in value (from lookup table) divided by the surface age. 
    /// A check is done to ensure the returned value is within a reasonable range (0.05 minimum to 1.5 maximum).
    /// </summary>    
    /// <returns>The estimated current rut rate, in mm/year</returns>
    private double GetRutIncrementEstimate(RoadSegmentMC segment)
    {

        // If a treatment has been applied, use the post-treatment rut increment
        // and not the estimate based on surface age
        //double surveyAge = GetRutSurveyAge(segment);
        //bool hasBeenTreated = segment.SurfaceAge < surveyAge;
        //if (hasBeenTreated)
        //{
        //    return segment.GetRutIncrementAfterTreatment();
        //}

        //// Get the estimated "settling-in" rut depth from the lookup table
        //double settingInRutDepth = _domainModel.GetLookupValueNumber("settling_in_values", "rut");

        //// Get the rut increase after settlement. Ensure the value is not negative
        //double rutAfterSettlement = Math.Max(0, segment.RutParameterValue - settingInRutDepth);

        //double surfAgeSafe = segment.SurfaceAge + 0.1; // Ensure surface age is not zero to avoid division by zero errors
        //double rutIncrementEstimate = rutAfterSettlement/ surfAgeSafe;

        //TODO: Make the min and max values configurable in lookups
        //return Math.Clamp(rutIncrementEstimate, 0.05, 1.5);
        return -1; // Placeholder until rut increment estimation logic is finalised

    }
   



}
