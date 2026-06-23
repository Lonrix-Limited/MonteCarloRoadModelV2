
using DocumentFormat.OpenXml.Office.CoverPageProps;
using JCass_ModelCore.Models;

namespace MonteCarloRoadModelV2.DomainObjects;


/// <summary>
/// Object representing a road segment with various properties and attributes.
/// </summary>
public class RoadSegmentMC
{

    private double _surfaceAge;
    private double _surfaceAgeBeforeReset;
    private string _surfaceFunction = "unknown"; // Default value for previous surface function
    private string _previousSurfaceFunction = "unknown"; // Default value for previous surface function

    #region Identification

    /// <summary>
    /// Zero-based index of the element in the model. This is set by the Framework Model and is used to identify the element in the model.
    /// </summary>
    public int ElementIndex { get; set; }

    /// <summary>
    /// Short code for identifying the segment in debug/feeback messages
    /// </summary>
    public string FeebackCode
    {
        get
        {
            return $"elem_index: {this.ElementIndex:D4} - {this.SegmentCode}";
        }
    }

    /// <summary>
    /// Segment identifier. Maps to input column "file_seg_name".
    /// </summary>
    public string SegmentCode { get; set; } = null!;

    /// <summary>
    /// Section ID. Maps to "file_section_id".
    /// </summary>
    public double SectionID { get; set; }

    /// <summary>
    /// Name of the section. Maps to "file_section_name".
    /// </summary>
    public string SectionName { get; set; } = null!;

    /// <summary>
    /// Start metre of the segment. Maps to "file_loc_from".
    /// </summary>
    public double LocFrom { get; set; }

    /// <summary>
    /// End metre of the segment. Maps to "file_loc_to".
    /// </summary>
    public double LocTo { get; set; }

    /// <summary>
    /// Lane code. Maps to "file_lane_name".
    /// </summary>
    public string LaneCode { get; set; } = null!;

    #endregion

    #region Quantity 

    /// <summary>
    /// Length of the segment in metres.
    /// </summary>
    public double LengthInMetre { get; set; }

    /// <summary>
    /// Square metre area.
    /// </summary>
    public double AreaSquareMetre { get; set; }

    /// <summary>
    /// Width in metres. By default, this is calculated on initialisation from Area and Length
    /// </summary>
    public double WidthInMetre { get; set; }

    #endregion
    
    #region Surface and Pavement Properties

    private string _surfaceClass = null!;

    /// <summary>
    /// Surface class ('cs', 'ac', 'blocks', 'concrete', 'other').
    /// </summary>
    public string SurfaceClass
    {
        get => _surfaceClass;
        set => _surfaceClass = value?.ToLower() ?? string.Empty;
    }

    /// <summary>
    /// Surface class used to identify treatment type based on 'ac', 'ogpa' or 'cs'. Currently , this maps:
    /// 'slurry' to 'ac' for treatment purposes
    /// 'unknown' to 'cs' for treatment purposes 
    /// </summary>
    public string SurfaceClassForTreatment
    {
        get
        {
            if (this.SurfaceClass == "slurry") return "ac";
            if (this.SurfaceClass == "unknown") return "cs";
            return this.SurfaceClass;
        }
    }

    public string SurfaceClassForRules
    {
        get
        {
            // Map surface classes to the ones used in the rules. This is needed because some of the 
            // surface classes in the input data are not used in the rules, and need to be mapped to the
            // ones assigned in the Model Development  (Casper) pipeline

            // 'concrete' should be 'other' to match the rules (even though there is a 'conc' in some rules,
            // user 'other' because some model building sets did not contain a 'conc' class. Thus, to be consistent, we map 'concrete' to 'other' for all rules)
            if (this.SurfaceClass == "concrete") return "other";

            // 'unknown' should be 'other' to match the rules. If some rules contain 'unknown' as a class, we should update the rules to use 'other' instead
            if (this.SurfaceClass == "unknown") return "other";
                        
            return this.SurfaceClass;
        }
    }


    /// <summary>
    /// Flag indicating if the surface is either chip seal or asphalt concrete. This is calculated based on the SurfaceClass property.
    /// </summary>
    public int SurfaceIsChipSealOrACFlag
    {
        get
        {
            // Return 1 if the surface class is 'cs' (chip seal) or 'ac' (asphalt concrete), otherwise return 0.
            return this.SurfaceClass == "cs" || this.SurfaceClass == "ac" ? 1 : 0;
        }
    }

    /// <summary>
    /// Surfacing date. Expected to be ISO date in format 'dd/mm/yyyy' in input data, and converted to DateTime during initialisation.
    /// </summary>
    public DateTime SurfacingDate { get; set; }

    /// <summary>
    /// Surfacing date in fractional years, calculated from the SurfacingDateString during Initialisation.
    /// </summary>
    public double SurfaceAge 
    { get {
            return _surfaceAge;
        }
      set {
            _surfaceAgeBeforeReset = this.SurfaceAge;
            _surfaceAge = value;
        } 
    }
    
    /// <summary>
    /// Intermediate variable holding the Surface Age before a reset was applied.
    /// </summary>
    public double SurfaceAgeBeforeReset { get { return _surfaceAgeBeforeReset; } }

    /// <summary>
    /// Surface function.
    /// </summary>
    public string SurfaceFunction
    {
        get { return _surfaceFunction;  }
        set
        {
            _previousSurfaceFunction = _surfaceFunction;
            _surfaceFunction = value;
        }
    }

    /// <summary>
    /// Preceding Surface function - use this to check what the situation was before the last reset.
    /// </summary>
    public string SurfaceFunctionPrevious
    {
        get { return _previousSurfaceFunction; }        
    }

    /// <summary>
    /// Surfacing material.
    /// </summary>
    public string SurfaceMaterial { get; set; } = null!;

    /// <summary>
    /// Surfacing expected life (years) from RAMM.
    /// </summary>
    public double SurfaceExpectedLife { get; set; }

    /// <summary>
    /// Read-only code combining Surface Function, Surface Material and Road Class, used to look up the Surface Expected Life from Lookup Tables. 
    /// </summary>
    public string SurfaceExpectedLifeCode
    {
        get
        {
            return $"{this.SurfaceFunction.ToLower()}_{this.SurfaceMaterial.ToLower()}_{this.RoadClass}";
        }
    }

    /// <summary>
    /// Returns the Surface Expective life minus the Surface Age, which gives the remaining life of the surface in years.
    /// </summary>
    public double SurfaceRemainingLife
    {
        get
        {            
            return this.SurfaceExpectedLife - this.SurfaceAge;
        }
    }

        
    /// <summary>
    /// Returns the percentage of the Surface Expected Life that has been achieved based on the Surface Age, as it will be in the NEXT
    /// period. We add one year to the surfacing age when calculating this, so that, for example for a first coat that has a 1 year life,
    /// the value will be 100% at the START of the next period when triggers are evaluated. If we do not do this, the 100% value will be
    /// registered only at the END of the next period (one year too late). Sketch it out!
    /// </summary>
    public double SurfaceAchievedLifePercent
    {
        get
        {
            if (this.SurfaceExpectedLife <= 0.0)
            {
                throw new Exception($"Surface expected life is zero or negative for segment {this.FeebackCode}. Surface Age: {this.SurfaceAge}, Expected Life: {this.SurfaceExpectedLife}.");
            }
            // As per JFunctions, limit the value to 200 to prevent very high values from distorting MCDA
            // TODO: Re-think this
            return Math.Min(200, 100 * ((this.SurfaceAge+1) / this.SurfaceExpectedLife));
        }
    }

    /// <summary>
    /// Surfacing number of layers
    /// </summary>
    public double SurfaceNumberOfLayers { get; set; }

    /// <summary>
    /// Surfacing thickness in millimetres.
    /// </summary>
    public double SurfaceThickness { get; set; }


    /// <summary>
    /// Pavement construction date. Expected to be ISO date in format 'dd/mm/yyyy' in input data, and converted to DateTime during initialisation.
    /// </summary>
    public DateTime PavementDate { get; set; }

    /// <summary>
    /// Pavement Age in fractional years, calculated from the PavementDateString during Initialisation.
    /// </summary>
    public double PavementAge { get; set; }

    /// <summary>
    /// Age-based pavement remaining life.
    /// </summary>
    public double PavementRemainingLife { get; set; }

    /// <summary>
    /// Returns the percentage of the Expected Pavement Life based on the Pavement Age and Remaining Life.
    /// </summary>
    public double PavementAchievedLife
    {
        get
        {
            double expectedLife = this.PavementAge + this.PavementRemainingLife;
            if (expectedLife <= 0.0)
            {
                throw new Exception($"Pavement expected life is zero or negative for segment {this.FeebackCode}. Pavement Age: {this.PavementAge}, Remaining Life: {this.PavementRemainingLife}.");
            }
            return this.PavementAge / expectedLife * 100.0;
        }
    }
    

    #endregion

    #region ONRC/Carriageway and Rainfall Attributes

    private string _urbanRural = null!;
    private string _onrc = null!;

    /// <summary>
    /// Urban/Rural flag.
    /// </summary>
    public string UrbanRural
    {
        get => _urbanRural;
        set => _urbanRural = value?.ToLower() ?? string.Empty;
    }

    /// <summary>
    /// ONRC Category.
    /// </summary>
    public string ONRC
    {
        get => _onrc;
        set => _onrc = value?.ToLower() ?? string.Empty;
    }
           
    

    /// <summary>
    /// Road class based on ONRC, with values 'l', 'm' and 'h' for low, medium and high traffic respectively. This classification
    /// collapses the ONRC categories into three classes based on traffic volumes. It is used to refine certain aspects such 
    /// as Expected Surface Life, allowing differentiation but only in 3 classes.
    /// </summary>
    public string RoadClass { get; set; } = null!;

    #endregion

    #region Traffic and Growth

    /// <summary>
    /// Average daily traffic.
    /// </summary>
    public double AverageDailyTraffic { get; set; }

    /// <summary>
    /// Heavy vehicle percentage.
    /// </summary>
    public double HeavyVehiclePercentage { get; set; }
    
    /// <summary>
    /// Traffic growth percentage.
    /// </summary>
    public double TrafficGrowthPercent { get; set; }

    /// <summary>
    /// Heavy vehicles per day, calculated as a percentage of Average Daily Traffic using HeavyVehiclePercentage.
    /// </summary>
    public double HeavyVehiclesPerDay
    {
        get
        {
            return this.AverageDailyTraffic * (this.HeavyVehiclePercentage / 100.0);
        }
    }

    #endregion
    
    #region High Speed Data (HSD) (Rut, Roughness, Texture, Skid Resistance etc.)

    /// <summary>
    /// HSD survey date (expecting ISO date in format 'yyyymmdd' in input data
    /// </summary>
    public DateTime HSDSurveyDate { get; set; }

    
    /// <summary>
    /// Mean IRI (mm/m) for the segment — latent underlying condition state used for deterioration
    /// </summary>
    public double IRIMeanLatent { get; set; }

    /// <summary>
    /// Mean IRI (mm/m) for the segment — observed condition state including random fluctuation
    /// </summary>
    public double IRIMeanObserved { get; set; }

    /// <summary>
    /// Episode Increment for IRI in mm/m/year. 
    /// </summary>
    public double IRIIncrement { get; set; }

    /// <summary>
    /// Mean rut depth (mm) — latent underlying condition state used for deterioration   
    /// </summary>
    public double RutMeanLatent { get; set; }

    /// <summary>
    /// Mean rut depth (mm) — observed condition state including random fluctuation   
    /// </summary>
    public double RutMeanObserved { get; set; }

    /// <summary>
    /// Rut increment in mm/year for the episode    
    /// </summary>
    public double RutIncrement { get; set; }

    /// <summary>
    /// Episode length for Rut and IRI increments, in years. This value is reset, and Rut and IRI increments are re-drawn 
    /// when current episode length exceeds the maximum allowed in Constants.EpisodeLengthRutAndIRI
    /// </summary>
    public int RutAndIRIIncrementEpisodeLength { get; set; }

    /// <summary>
    /// Texture depth mean for segment, in mm — latent underlying condition state used for deterioration
    /// </summary>
    public double TextureMeanLatent { get; set; }

    /// <summary>
    /// Texture depth mean for segment, in mm — observed condition state including random fluctuation
    /// </summary>
    public double TextureMeanObserved { get; set; }

    /// <summary>
    /// Texture depth increment for the episode, in mm/year
    /// </summary>
    public double TextureIncrement { get; set; }

    /// <summary>
    /// Episode length for texture increment, in years. This value is reset, and Increment is re-drawn when current episode length exceeds the
    /// maximum allowed in Constants.EpisodeLengthTexture
    /// </summary>
    public int TextureIncrementEpisodeLength { get; set; }

    /// <summary>
    /// Percentage of the segment length with Skid Resistance below the Threshold Level, as measured in the High Speed Data survey. 
    /// This is not a model parameter but only an input column. It is used to help make decisions over short term model development.
    /// </summary>
    public double SkidResistanceBelowTLPercent { get; set; }


    #endregion

    #region Maintenance


    /// <summary>
    /// Extent of Pavement-related Routine Maintenance (excluding Pothole Filling), as fraction of total length (value 0 to 1), triggered in the period. 
    /// </summary>
    public double MaintenancePavement { get; set; }

    /// <summary>
    /// Extent of Surfacing-related Routine Maintenance (excluding Pothole Filling), as fraction of total length (value 0 to 1), triggered in the period. 
    /// </summary>
    public double MaintenanceSurfacing { get; set; }

    /// <summary>
    /// Extent of Pothole Filling Routine Maintenance, as fraction of total length (value 0 to 1), triggered in the period. 
    /// </summary>
    public double MaintenancePotfill { get; set; }

    /// <summary>
    /// Historical maintenance extent - calculated in Casper as the extent of the segment that has seen 2 or more
    /// PA maintenance actions in the last 3 years. 
    /// </summary>
    public double MaintenanceFreqHistoricalPA { get; set; }

    /// <summary>
    /// Historical maintenance extent - calculated in Casper as the extent of the segment that has seen 2 or more
    /// SU maintenance actions in the last 3 years. 
    /// </summary>
    public double MaintenanceFreqHistoricalSU { get; set; }

    /// <summary>
    /// Historical maintenance extent - calculated in Casper as the extent of the segment that has seen 2 or more
    /// Pothole maintenance actions in the last 3 years. 
    /// </summary>
    public double MaintenanceFreqHistoricalPoth { get; set; }

    public double GetHistoricalPAMaintenanceBoostFactor()
    {
        // Zero if MaintenanceFreqHistoricalPA is <= 0.1, otherwise linear increase from 1 at 0.1 to 1.5 at 0.5, and capped at 1.5 for values above 0.5
        if (this.MaintenanceFreqHistoricalPA <= 0.1) return 1.0;
        if (this.MaintenanceFreqHistoricalPA >= 0.5) return 1.5;
        return 1.0 + (this.MaintenanceFreqHistoricalPA - 0.1) / (0.5 - 0.1) * 0.5;
    }

    public double GetHistoricalPotfillMaintenanceBoostFactor()
    {
        // Zero if MaintenanceFreqHistoricalPoth is <= 0.1, otherwise linear increase from 1 at 0.1 to 1.5 at 0.5, and capped at 1.5 for values above 0.5
        if (this.MaintenanceFreqHistoricalPoth <= 0.1) return 1.0;
        if (this.MaintenanceFreqHistoricalPoth >= 0.5) return 1.5;
        return 1.0 + (this.MaintenanceFreqHistoricalPoth - 0.1) / (0.5 - 0.1) * 0.5;
    }

    #endregion

    #region Distress States

    /// <summary>
    /// State of Pavement Distress, expressed in Extent (prefix 'E', scale 0,1,2,3) and Severity (prefix 'S', scale 0,1,2).
    /// Lowest state is "E0-S0" indicating no or isolated distress of very low severity, and highest state is "E3-S3" indicating 
    /// extensive distress of high severity. 
    /// </summary>
    public string PavementDistressState { get; set; } = null!;

    /// <summary>
    /// State of Surfacing Distress (excluding Flushing), expressed in Extent (prefix 'E', scale 0,1,2,3) and Severity (prefix 'S', scale 0,1,2).
    /// Lowest state is "E0-S0" indicating no or isolated distress of very low severity, and highest state is "E3-S3" indicating
    /// extensive distress of high severity. 
    /// </summary>

    public string SurfacingDistressState { get; set; } = null!;


    /// <summary>
    /// State of Flushing Distress, expressed in Extent (prefix 'E', scale 0,1,2,3) and Severity (prefix 'S', scale 0,1,2).
    /// Lowest state is "E0-S0" indicating no or isolated distress of very low severity, and highest state is "E3-S3" indicating
    /// extensive distress of high severity. 
    /// </summary>
    public string FlushingDistressState { get; set; } = null!;


    #endregion

    #region Treatment Trigger Related

    /// <summary>
    /// Number of treatments triggered on the segment during the model run - committed or triggered
    /// </summary>
    public double NumberOfTreatments { get; set; } = 0;

    /// <summary>
    /// Candidate Selection Outcome calculated at the end of the epoch based on the segment properties and condition, and 
    /// used in the next period to determine if the segment is a candidate for treatment. If value is 'ok' it means the
    /// segment can be considered for treatment. Otherwise, the flag should contain information explaining why the segment
    /// is NOT a viableThis is set based on the logic in the CandidateSelector class, and can be used to provide feedback on why a segment was or was not selected as a candidate for treatment. It is also used in the TreatmentsTrigger class to determine which treatments to trigger for the segment based on the Candidate Selection Outcome. Default value is "Not evaluated" before candidate selection is evaluated, and it should be set to a specific outcome after evaluation.
    /// </summary>
    public string CandidateSelectionOutcome { get; set; } = "Not evaluated"; // Default value before candidate selection is evaluated

    public double GetRehabilitationNeedsIndex(Constants constants, int period)
    {
        return RehabilitationNeedsCalculator.GetRehabilitationNeedsIndex(this, constants, period);
    }

    public double GetSurfaceTreatmentNeedsIndex(Constants constants, int period)
    {
        return SurfacingNeedsIndexCalculator.GetSurfacingNeedsIndex(this, constants, period);
    }


    /// <summary>
    /// Percentage rank of the Rehabilitation Needs Index value for the segment compared to all other segments in the model. 
    /// </summary>
    public double RehabilitationNeedsIndexRank { get; set; } = 0;


    /// <summary>
    /// Percentage rank of the Surface Treatment Needs Index value for the segment compared to all other segments in the model. 
    /// </summary>
    public double SurfacingNeedsIndexRank { get; set; } = 0;


    /// <summary>
    /// Flag indicating if the segment should be considered at all as a candiate for treatment. This is not read from input data
    /// but set before calling the Treatments Trigger, based on condition, age etc.
    /// </summary>
    public int IsCandidateForTreatment { get; set; } = 1; // Default to 1 (is candidate) if not specified in input data

    /// <summary>
    /// Flag set from input data indicating if a segment can be treated or not. This can be set based on client-specific
    /// rules or constraints, and is used to filter out segments that should not be considered for treatment in the model. 
    /// This flag is set during initialisation based on input data column 'inp_can_treat_flag' and it not modified during the model run.
    /// </summary>
    public int CanTreatFlag { get; set; } = 1; // Default to 1 (can treat) if not specified in input data

    /// <summary>
    /// Flag to indicate if a thin asphalt overlay can be considered on this pavement. This flag is read from input data column 'inp_thin_ac_ok_flag' and can 
    /// be used to filter out segments that should not be considered for asphalt overlay treatments in the model, based on client-specific rules or constraints
    /// such as too high deflection curvature etc.
    /// </summary>
    public int CanDoThinACOverlay { get; set; } = 1; // Default to 1 (asphalt is ok) if not specified in input data

    /// <summary>
    /// Flag to indicate if the segment can be considered for Rehabilitation. This is set based on input data column 'inp_can_rehab_flag' and can be used 
    /// to filter out segments that should not be considered for rehabilitation treatments in the model, based on client-specific rules or constraints. 
    /// This allows to prevent rehabilitation treatments from being triggered for certain segments if this is not realistic or desired. 
    /// Needs to be set explicitly during pre-processing.
    /// </summary>
    public int CanRehabFlag { get; set; } = 1; // Default to 1 (can rehabilitate) if not specified in input data

    /// <summary>
    /// Code for the next surface to be applied. This is set based on input data column 'inp_next_surf' and can be used to 
    /// switch surface type e.g. from Chipseal to Asphalt Concrete based on client-specific rules or constraints. Needs to
    /// be set explicitly during pre-processing. 
    /// </summary>
    public string NextSurface { get; set; } = "cs"; // Default to 'cs' (chip seal) if not specified in input data
      

    /// <summary>
    /// Earliest treatment period. This is set based on input data column 'inp_earliest_treat_period' and can be used to specify the 
    /// earliest period in which a treatment can be triggered for the segment, based on client-specific rules or constraints. 
    /// This allows to prevent treatments from being triggered in the first periods of the model run if this is not realistic or desired. 
    /// Needs to be set explicitly during pre-processing.
    /// </summary>
    public int EarliestTreatmentPeriod { get; set; } = 0; // Default to 0 (treatment can be triggered from first period) if not specified in input data

    
    /// <summary>
    /// Checks if a second coat is needed now based on the surface function. If surface function is "1" or "1a" then
    /// a second coat is needed, otherwise it is not. 
    /// </summary>
    public bool SecondCoatNeeded
    {
        get
        {
            return this.SurfaceFunction == "1" || this.SurfaceFunction == "1a";
        }
    }


    #endregion

    #region Helper Methods

    /// <summary>
    /// Parses the state key into an Extent and Severity score, and returns a combined score calculated as Extent * Severity. 
    /// The state key is expected to be in the format "E{extent}-S{severity}", where {extent} is an integer from 0 to 3 
    /// and {severity} is an integer from 0 to 2. For example, "E2-S1" would have an Extent score of 2 and a Severity score of 1, resulting 
    /// in a combined score of 2 * 1 = 2. 
    /// </summary>
    /// <param name="stateKey">State key is expected to be in the format "E{extent}-S{severity}", where {extent} is an integer from 0 to 3 
    /// and {severity} is an integer from 0 to 2.</param>
    /// <returns>Combined score calculated as Extent * Severity.</returns>
    public static double GetStateScore(string stateKey)
    {
        string[] values = stateKey.Split('-');
        if (values.Length != 2) throw new Exception($"Invalid state key format: {stateKey}. Expected format is 'E{{extent}}-S{{severity}}'.");
        
        string extentPart = values[0];
        string severityPart = values[1];
        double extentScore = 0;
        double severityScore = 0;

        if (extentPart.StartsWith("E") && double.TryParse(extentPart.Substring(1), out double extent))
        {
            extentScore = extent;
        }
        else
        {
            throw new Exception($"Invalid extent format: {extentPart}. Expected format is 'E{{extent}}'.");
        }

        if (severityPart.StartsWith("S") && double.TryParse(severityPart.Substring(1), out double severity))
        {
            severityScore = severity;
        }
        else
        {
            throw new Exception($"Invalid severity format: {severityPart}. Expected format is 'S{{severity}}'.");
        }

        return extentScore * severityScore;
    }

    /// <summary>
    /// Parses the state key into an Extent estimate ranging from 0 to 1. This is used to estimate physical extent of distress on the segment 
    /// based on the state key. This is a best estimate of the extent of repair work to be done based on the extent score. Thus maximum
    /// extent (E3) does not necessarily correspond to 100% of the segment needing repair, but rather a high extent of repair work needed. The mapping is as follows:
    /// 'E0' -> 0% extent (return 0.0)
    /// 'E1' -> 10% extent (return 0.1)
    /// 'E2' -> 30% extent (return 0.3)
    /// 'E3' -> 50% extent (return 0.5)
    /// </summary>
    /// <param name="stateKey">State key is expected to be in the format "E{extent}-S{severity}", where {extent} is an integer from 0 to 3 
    /// and {severity} is an integer from 0 to 2.</param>
    /// <returns>Extent estimate ranging from 0 to 1.</returns>
    public static double GetExtentEstimateFromStateScore(string stateKey)
    {
        string[] values = stateKey.Split('-');
        if (values.Length != 2) throw new Exception($"Invalid state key format: {stateKey}. Expected format is 'E{{extent}}-S{{severity}}'.");

        string extentPart = values[0];
        switch (extentPart)
        {
            case "E0":
                return 0.0;
            case "E1":
                return 0.1;
            case "E2":
                return 0.3;
            case "E3":  
                return 0.5;
            default:
                throw new Exception($"Invalid extent format: {extentPart}. Expected format is 'E{{extent}}'.");
        }
    }


    /// <summary>
    /// Updates the sinks mapping back to parameter values in the model. 
    /// </summary>
    /// <param name="numModParamValues">Return value: Sink holding values for numeric parameters (to be updated by Domain Model). Keys are parameter names, values are assigned values</param>
    /// <param name="textModParamValues">Return value: Sink holding values for text parameters (to be updated by Domain Model). Keys are parameter names, values are assigned values</param>     
    public void SetParameterValues(Action<string, double> numModParamValues, Action<string, string> textModParamValues, Constants constants,
                                   Dictionary<string, object> infoFromModel, int period)
    {
        numModParamValues("par_adt", this.AverageDailyTraffic);
        numModParamValues("par_hcv", this.HeavyVehiclesPerDay);

        numModParamValues("par_pave_age", this.PavementAge);
        numModParamValues("par_pave_remlife", this.PavementRemainingLife);
        numModParamValues("par_pave_life_ach", this.PavementAchievedLife);
        
        textModParamValues("par_surf_mat", this.SurfaceMaterial);
        textModParamValues("par_surf_class", this.SurfaceClass);        
        numModParamValues("par_surf_thick", this.SurfaceThickness);
        numModParamValues("par_surf_layers", this.SurfaceNumberOfLayers);
        textModParamValues("par_surf_func", this.SurfaceFunction);
        numModParamValues("par_surf_exp_life", this.SurfaceExpectedLife);
        numModParamValues("par_surf_age", this.SurfaceAge);
        numModParamValues("par_surf_life_ach", this.SurfaceAchievedLifePercent);
        numModParamValues("par_surf_remain_life", this.SurfaceRemainingLife);
        
        numModParamValues("par_rut_increm", this.RutIncrement);
        numModParamValues("par_rut", this.RutMeanLatent);
        numModParamValues("par_rut_obs", this.RutMeanObserved);
        numModParamValues("par_rut_iri_epi_len", this.RutAndIRIIncrementEpisodeLength);

        numModParamValues("par_iri_increm", this.IRIIncrement);
        numModParamValues("par_iri", this.IRIMeanLatent);
        numModParamValues("par_iri_obs", this.IRIMeanObserved);

        numModParamValues("par_text_increm", this.TextureIncrement);
        numModParamValues("par_text", this.TextureMeanLatent);
        numModParamValues("par_text_obs", this.TextureMeanObserved);
        numModParamValues("par_text_epi_len", this.TextureIncrementEpisodeLength);

        textModParamValues("par_pds", this.PavementDistressState);
        textModParamValues("par_sds", this.SurfacingDistressState);
        textModParamValues("par_fds", this.FlushingDistressState);

        numModParamValues("par_rni", this.GetRehabilitationNeedsIndex(constants, period));
        numModParamValues("par_sni", this.GetSurfaceTreatmentNeedsIndex(constants, period));

        numModParamValues("par_maint_pa", this.MaintenancePavement);
        numModParamValues("par_maint_su", this.MaintenanceSurfacing);
        numModParamValues("par_maint_poth", this.MaintenancePotfill);

        int periodsToNextTreatment = Convert.ToInt32(infoFromModel["periods_to_next_treatment"]);
        textModParamValues("par_trigg_info", CandidateSelector.GetCandidateSelectionOutcome(this, constants, periodsToNextTreatment));

        numModParamValues("par_treat_count", this.NumberOfTreatments);

        // The following are Network Parameters - to be set automatically by the framework model:        
        //para_sla_rank
        //para_rut_rank
        //para_iri_rank

    }

    
    #endregion

}


