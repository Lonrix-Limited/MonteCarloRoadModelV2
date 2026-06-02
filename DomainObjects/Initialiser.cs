using JCass_ModelCore.Models;


namespace MonteCarloRoadModelV2.DomainObjects;

/// <summary>
/// Class to handle initialisation, including helper functions and some domain logic.
/// </summary>
public class Initialiser
{
    private ModelBase _frameworkModel;
    private MonteCarloRoadModelV2 _domainModel;

    public Initialiser(ModelBase frameworkModel, MonteCarloRoadModelV2 domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public RoadSegmentMC InitialiseSegment(int iElemIndex)
    {


        if (iElemIndex == 12005)
        {
            _ = 0; // breakpoint anchor — set/remove IDE breakpoint here at runtime
        }


        // Create a new RoadSegmentMC object based purely on the raw data provided in the string array.
        RoadSegmentMC segment = RoadSegmentFactoryMC.GetFromRawData(_frameworkModel, iElemIndex);

        // Now do checks on the values and handle any anomalous data        
        segment.PavementAge = GetPavementAge(segment); 
        segment.SurfaceAge = GetSurfacingAge(segment); 
        
        segment.RutMeanLatent = GetInitialRuttingValue(segment);        
        segment.RutMeanObserved = segment.RutMeanLatent;   //Assume initial rut observation is equal to the latent value; Not really true, but for reporting
        segment.RutIncrement = GetRutIncrementEstimate(segment);
        
        segment.IRIMeanLatent = GetInitialIRIValue(segment);        
        segment.IRIMeanObserved = segment.IRIMeanLatent;
        segment.IRIIncrement = GetIRIIncrementEstimate(segment);

        segment.TextureMeanLatent = GetInitialTextureValue(segment);        
        segment.TextureMeanObserved = segment.TextureMeanLatent;
        segment.TextureIncrement = GetTextureIncrementEstimate(segment);

        segment.PavementDistressIndex = GetInitialPDIValue(segment);
        segment.SurfaceDistressIndex = GetInitialSDIValue(segment);

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

    #region Rut Initial Value and Increment Estimation

    /// <summary>
    /// Get the initial rutting value, taking into account the HSD survey age and the Surfacing and Pavement ages. There are
    /// three possibilities:
    /// <para>1. The HSD survey is older than the Pavement Age: In this case we presume the segment has been rehabilitated
    /// after the survey and return the reset value using the appropriate Reset Simulator for rehabilitation
    /// <para>2. The HSD survey is not older than the Pavement Age but older than Surface Age: In this case we presume the 
    /// segment has been resurfaced after the survey and simulate the resetted value using the appropriate Reset Simulator for resurfacing
    ///</para>
    /// <para>3. The HSD survey is not older than the Pavement Age or the Surface age - return the Rut Value from the input file    
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
            return Resetter.GetRutResetValue(segment, _domainModel.SubModels, "rehab", _domainModel.Constants, _frameworkModel.Random, 0);            
        }

        

        // If segment has been resurfaced, determine the rutting exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            return  Resetter.GetRutResetValue(segment, _domainModel.SubModels, "resurf", _domainModel.Constants, _frameworkModel.Random, 0);            
        }

        return segment.RutMeanLatent;
    }


    /// <summary>
    /// Get an estimate of the initial rut rate, in mm per year. If the segment has been treated (resurfaced or rehabilitated) since 
    /// the HSD survey, then generate a new increment for the episode using the appropriate Increment Simulator. If the segment has 
    /// not been treated since the HSD survey, then use the estimated rut increment from the Input File.
    /// </summary>    
    /// <returns>The estimated current rut rate, in mm/year</returns>
    private double GetRutIncrementEstimate(RoadSegmentMC segment)
    {

        double surveyAge = GetHSDSurveyAge(segment);

        // If segment has been rehabilitated, return the lookup value for the rutting reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        if (hasBeenRehabilitated)
        {
            return Incrementer.GetRutIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants);
        }

        double ruttingRaw = segment.RutMeanLatent;

        // If segment has been resurfaced, determine the rutting exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            return Incrementer.GetRutIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants);
        }
        
        return segment.RutIncrement; // Return the rut increment from the input file if the segment has not been treated since the HSD survey

    }

    #endregion

    #region IRI Initial Value and Increment Estimation

    /// <summary>
    /// Get the initial IRI value, taking into account the HSD survey age and the Surfacing and Pavement ages. There are
    /// three possibilities:
    /// <para>1. The HSD survey is older than the Pavement Age: In this case we presume the segment has been rehabilitated
    /// after the survey and return the reset value using the appropriate Reset Simulator for rehabilitation
    /// <para>2. The HSD survey is not older than the Pavement Age but older than Surface Age: In this case we presume the 
    /// segment has been resurfaced after the survey and simulate the resetted value using the appropriate Reset Simulator for resurfacing
    ///</para>
    /// <para>3. The HSD survey is not older than the Pavement Age or the Surface age - return the IRI Value from the input file    
    ///</para>
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    private double GetInitialIRIValue(RoadSegmentMC segment)
    {
        double surveyAge = GetHSDSurveyAge(segment);
        
        // If segment has been rehabilitated, return the lookup value for the IRI reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        if (hasBeenRehabilitated)
        {
            // Estimate the treatment name based on material type            
            string treatmentName = segment.SurfaceClass + "_rehab";          
            return Resetter.GetIRIResetValue(segment, _domainModel.SubModels, treatmentName, _domainModel.Constants, _frameworkModel.Random, 0);            
        }
        
        // If segment has been resurfaced, determine the IRI exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            // Estimate the treatment name based on material type
            string treatmentName = segment.SurfaceClass + "_resurf";            
            return Resetter.GetIRIResetValue(segment, _domainModel.SubModels, treatmentName, _domainModel.Constants, _frameworkModel.Random, 0);            
        }

        return segment.IRIMeanLatent;

    }


    /// <summary>
    /// Get an estimate of the initial IRI rate, in mm per year. If the segment has been treated (resurfaced or rehabilitated) since 
    /// the HSD survey, then generate a new increment for the episode using the appropriate Increment Simulator. If the segment has 
    /// not been treated since the HSD survey, then use the estimated IRI increment from the Input File.
    /// </summary>    
    /// <returns>The estimated current IRI rate, in mm/m/year</returns>
    private double GetIRIIncrementEstimate(RoadSegmentMC segment)
    {

        double surveyAge = GetHSDSurveyAge(segment);

        // If segment has been rehabilitated, return the lookup value for the IRI reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        if (hasBeenRehabilitated)
        {
            return Incrementer.GetIRIIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants);
        }

        double iriRaw = segment.IRIMeanLatent;

        // If segment has been resurfaced, determine the IRI exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            return Incrementer.GetIRIIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants);
        }

        return segment.IRIIncrement; // Return the IRI increment from the input file if the segment has not been treated since the HSD survey

    }

    #endregion

    #region Texture Depth Initial Value and Increment Estimation

    /// <summary>
    /// Get the initial Texture value, taking into account the HSD survey age and the Surfacing and Pavement ages. There are
    /// three possibilities:
    /// <para>1. The HSD survey is older than the Pavement Age: In this case we presume the segment has been rehabilitated
    /// after the survey and return the reset value using the appropriate Reset Simulator for rehabilitation
    /// <para>2. The HSD survey is not older than the Pavement Age but older than Surface Age: In this case we presume the 
    /// segment has been resurfaced after the survey and simulate the resetted value using the appropriate Reset Simulator for resurfacing
    ///</para>
    /// <para>3. The HSD survey is not older than the Pavement Age or the Surface age - return the Texture Value from the input file    
    ///</para>
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    private double GetInitialTextureValue(RoadSegmentMC segment)
    {

        double surveyAge = GetHSDSurveyAge(segment);

        // If segment has been resurfaced or rehabilitated, return the lookup value for the Texture reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        
        double textureRaw = segment.TextureMeanLatent;

        // If segment has been resurfaced, determine the Texture exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced || hasBeenRehabilitated)
        {
            double resetValue = Resetter.GetTextureDepthResetValue(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants, 0);            
            return resetValue;
        }

        // If segment has not been rehabilitated or resurfaced, use the raw Texture value
        return textureRaw;
    }


    /// <summary>
    /// Get an estimate of the initial Texture rate, in mm per year. If the segment has been treated (resurfaced or rehabilitated) since 
    /// the HSD survey, then generate a new increment for the episode using the appropriate Increment Simulator. If the segment has 
    /// not been treated since the HSD survey, then use the estimated Texture increment from the Input File.
    /// </summary>    
    /// <returns>The estimated current Texture rate, in mm/m/year</returns>
    private double GetTextureIncrementEstimate(RoadSegmentMC segment)
    {

        double surveyAge = GetHSDSurveyAge(segment);

        // If segment has been rehabilitated, return the lookup value for the Texture reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        if (hasBeenRehabilitated)
        {
            return Incrementer.GetTextureIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants);
        }

        double textureRaw = segment.TextureMeanLatent;
        // If segment has been resurfaced, determine the Texture exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            return Incrementer.GetTextureIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants);
        }

        return segment.TextureIncrement; // Return the Texture increment from the input file if the segment has not been treated since the HSD survey

    }

    #endregion

    #region PDI and SDI Initial Value and Increment Estimation

    /// <summary>
    /// Get the initial Pavement Distress Index (PDI) value, taking into account the HSD survey age and the Surfacing and Pavement ages. There are
    /// three possibilities:
    /// <para>1. The HSD survey is older than the Pavement Age: In this case we presume the segment has been rehabilitated
    /// after the survey and return zero since we assume rehab completely resets PDI
    /// <para>2. The HSD survey is not older than the Pavement Age but older than Surface Age: In this case we presume the 
    /// segment has been resurfaced: In this case set PDI to a low but non-zero value, since we assume resurfacing significantly reduces but does not 
    /// completely reset PDI. 
    ///</para>
    /// <para>3. The HSD survey is not older than the Pavement Age or the Surface age - return the PDI value from the input file    
    ///</para>
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    private double GetInitialPDIValue(RoadSegmentMC segment)
    {
        double surveyAge = GetHSDSurveyAge(segment);
        
        // If segment has been rehabilitated, return the lookup value for the rutting reset
        bool hasBeenRehabilitated = segment.PavementAge < surveyAge;
        if (hasBeenRehabilitated)
        {
            return 0; // Assume rehab completely resets PDI to zero
            
        }
        
        // If segment has been resurfaced, determine the rutting exceedance and the reset
        bool hasBeenResurfaced = segment.SurfaceAge < surveyAge;
        if (hasBeenResurfaced)
        {
            return Math.Max(0.1, segment.PavementDistressIndex);             
        }
        
        return segment.PavementDistressIndex; // Return the PDI value from the input file if the segment has not been treated since the HSD survey
    }

    /// <summary>
    /// Get the initial Surface Distress Index (SDI) value, taking into account the HSD survey age and the Surfacing age. There are 
    /// two possibilities:
    /// <para>1. The HSD survey is older than the Surface Age: In this case we presume the segment has been resurfaced ore rehabilitated
    /// after the survey and return zero since we assume resurfacing and rehabilitation completely resets SDI
    /// <para>2. The HSD survey is not older than the Surface Age - return the SDI value from the input file    
    ///</para>    
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    private double GetInitialSDIValue(RoadSegmentMC segment)
    {
        double surveyAge = GetHSDSurveyAge(segment);

        // If segment has been resurfaced or rehabilitated, return the lookup value for the SDI reset
        bool hasBeenTreated = segment.SurfaceAge < surveyAge;
        if (hasBeenTreated) return 0; // Assume resurfacing completely resets SDI to zero
                        
        return segment.SurfaceDistressIndex; // Return the SDI value from the input file if the segment has not been treated since the HSD survey
    }



    #endregion

}
