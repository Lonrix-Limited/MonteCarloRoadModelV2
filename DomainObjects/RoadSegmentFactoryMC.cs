
using DocumentFormat.OpenXml.EMMA;
using JCass_Core.Utils;
using JCass_ModelCore.Models;

namespace MonteCarloRoadModelV2.DomainObjects;

public static class RoadSegmentFactoryMC
{

    /// <summary>
    /// Creates a RoadSegment object from raw data provided in a string array. We assume columns are in the order defined in the model's raw data schema.
    /// </summary>
    /// <param name="model">Model object from which to refer the Raw Data schema</param>
    /// <param name="rawRow">Row of raw data values for each column in the schema</param>
    /// <returns></returns>
    public static RoadSegmentMC GetFromRawData(ModelBase model, int elementIndex)
    {
        RoadSegmentMC segment = new RoadSegmentMC();

        segment.ElementIndex = elementIndex; // Set the element index for this segment
        

        // Identification
        segment.SegmentCode = model.GetInputDataText(segment.ElementIndex, "inp_seg_code");
        segment.SectionID = model.GetInputDataNumber(segment.ElementIndex, "inp_section_id");
        segment.SectionName = model.GetInputDataText(segment.ElementIndex, "inp_section_name");
        segment.LocFrom = model.GetInputDataNumber(segment.ElementIndex, "inp_loc_from");
        segment.LocTo = model.GetInputDataNumber(segment.ElementIndex, "inp_loc_to");
        segment.LaneCode = model.GetInputDataText(segment.ElementIndex, "inp_lane");

        // Core measures
        segment.LengthInMetre = model.GetInputDataNumber(segment.ElementIndex, "inp_length");
        segment.AreaSquareMetre = model.GetInputDataNumber(segment.ElementIndex, "inp_area_m2");
        segment.WidthInMetre = segment.AreaSquareMetre / segment.LengthInMetre;

        // Classification/Carriageway/Rainfall
        segment.UrbanRural = model.GetInputDataText(segment.ElementIndex, "inp_urban_rural").ToLower();
        segment.ONRC = model.GetInputDataText(segment.ElementIndex, "inp_onrc").ToLower();
        segment.RainfallMM = model.GetInputDataNumber(segment.ElementIndex, "inp_rainfall");
        segment.RoadClass = model.Lookups["road_class"][segment.ONRC].ToString()
            ?? throw new InvalidOperationException($"Lookup 'road_class' for ONRC '{segment.ONRC}' returned null on element {elementIndex}");

        // Traffic        
        segment.AverageDailyTraffic = Math.Max(1, model.GetInputDataNumber(segment.ElementIndex, "inp_adt")); //Ensure ADT is at least 1
        segment.HeavyVehiclePercentage = model.GetInputDataNumber(segment.ElementIndex, "inp_heavy_perc");        
        segment.TrafficGrowthPercent = model.GetInputDataNumber(segment.ElementIndex, "inp_traff_growth_perc");

        // Surfacing
        segment.SurfaceClass = model.GetInputDataText(segment.ElementIndex, "inp_surf_class").ToLower();
        segment.SurfacingDate = GetDateFromISO(model.GetInputDataText(segment.ElementIndex, "inp_surf_date"), "Surfacing Date");
        segment.SurfaceFunction = model.GetInputDataText(segment.ElementIndex, "inp_surf_function");        
        segment.SurfaceMaterial = model.GetInputDataText(segment.ElementIndex, "inp_surf_material");
        segment.SurfaceExpectedLife = GetExpectedSurfaceLifeSafe(model, segment);
        segment.SurfaceNumberOfLayers = model.GetInputDataNumber(segment.ElementIndex, "inp_surf_layers");
        segment.SurfaceThickness = model.GetInputDataNumber(segment.ElementIndex, "inp_surf_thick");

        // Pavement        
        segment.PavementDate = GetDateFromISO(model.GetInputDataText(segment.ElementIndex, "inp_pave_date"), "Pavement Date");       

        // High Speed Data - Rutting, Roughness (IRI) and Texture Depth
        segment.HSDSurveyDate = GetDateFromISO(model.GetInputDataText(segment.ElementIndex, "inp_hsd_survey_date"),"HSD Survey Date");
        segment.RutMeanLatent = model.GetInputDataNumber(segment.ElementIndex, "inp_rut_mean");
        segment.RutIncrement = Math.Max(0.01,model.GetInputDataNumber(segment.ElementIndex, "inp_rut_rate"));
        segment.RutAndIRIIncrementEpisodeLength = 1; // Initially 1

        segment.IRIMeanLatent = model.GetInputDataNumber(segment.ElementIndex, "inp_iri_mean");        
        segment.IRIIncrement = Math.Max(0.001, model.GetInputDataNumber(segment.ElementIndex, "inp_iri_rate"));

        segment.TextureMeanLatent = model.GetInputDataNumber(segment.ElementIndex, "inp_text_mean");
        segment.TextureIncrement = Math.Min(-0.01, model.GetInputDataNumber(segment.ElementIndex, "inp_text_rate"));
        segment.TextureIncrementEpisodeLength = 1; // Initially 1

        // Pavement and Surface Distress States
        segment.PavementDistressState = GetDistressStateKey(model, "inp_pde", "inp_pds", segment.ElementIndex);
        segment.SurfacingDistressState = GetDistressStateKey(model, "inp_sde", "inp_sds", segment.ElementIndex);
        segment.FlushingDistressState = GetDistressStateKey(model, "inp_fde", "inp_fds", segment.ElementIndex);

        // Routine Maintenance 
        segment.MaintenancePavement = model.GetInputDataNumber(segment.ElementIndex, "inp_maint_pa_ext");
        segment.MaintenanceSurfacing = model.GetInputDataNumber(segment.ElementIndex, "inp_maint_su_ext");
        segment.MaintenancePotfill = model.GetInputDataNumber(segment.ElementIndex, "inp_maint_poth_ext");
        segment.MaintenanceFreqHistoricalPA = model.GetInputDataNumber(segment.ElementIndex, "inp_maint_pa_rept_ext");
        segment.MaintenanceFreqHistoricalPoth = model.GetInputDataNumber(segment.ElementIndex, "inp_maint_poth_rept_ext");
        segment.MaintenanceFreqHistoricalSU = model.GetInputDataNumber(segment.ElementIndex, "inp_maint_su_rept_ext");

        // Treatment Triggering Flags
        segment.CanTreatFlag = Convert.ToInt32(model.GetInputDataNumber(segment.ElementIndex, "inp_can_treat_flag"));        
        segment.CanDoThinACOverlay = Convert.ToInt32(model.GetInputDataNumber(segment.ElementIndex, "inp_thin_ac_ok_flag"));
        segment.CanRehabFlag = Convert.ToInt32(model.GetInputDataNumber(segment.ElementIndex, "inp_can_rehab_flag"));

        segment.EarliestTreatmentPeriod = Convert.ToInt32(model.GetInputDataNumber(segment.ElementIndex, "inp_earliest_treat_period"));
        segment.NextSurface = model.GetInputDataText(segment.ElementIndex, "inp_next_surf").ToLower();

        return segment;
    }

    /// <summary>
    /// Gets a segment object from a model's input and parameter values dictionary. Use this method AFTER intialisation, when the model
    /// has already calculated initial values for model parameters and holds these values (initial or iterated/resetted). The inputAndParameterValues
    /// dictionary holds keys mapping to both the raw input columns and to the model parameters, with the Values mapping to the corresponding values.
    /// </summary>
    /// <param name="frameworkModel">Model object from which to refer the Raw Data schema</param>
    /// <param name="numParamValues">Dictionary provided by model containing last/current values for numeric model parameters./param>
    /// <param name="textParamValues">Dictionary provided by model containing last/current values for numeric model parameters./param>
    /// <returns></returns>
    public static RoadSegmentMC GetFromModel(ModelBase frameworkModel, Dictionary<string, double> numInputValues, Dictionary<string, string> textInputValues, 
        Dictionary<string, double> numParamValues, Dictionary<string, string> textParamValues, int elementIndex, int iPeriod)
    {

        // First set all properties that are still dependend on the raw input data and that do not change over
        // the modelling periods. Some properties set on raw data such as rut and iri will no longer be relevant, but
        // we will update those properties based on the model parameters later in this method, so we can still use the raw data
        // as a starting point for those properties.
        RoadSegmentMC segment = GetFromRawData(frameworkModel, elementIndex);
                               
        // Now set the properties that depend on model parameters: Work in order of model parameter definition set
        // in the setup file so that we can more easily spot missing parameters.

        segment.AverageDailyTraffic = numParamValues["par_adt"];
        // HCV is automatically updated based on ADT and HeavyVehiclePercentage
        segment.PavementAge = numParamValues["par_pave_age"];
        segment.PavementRemainingLife = numParamValues["par_pave_remlife"];
        // Note: segment.PavementAchievedLife will be automatically calculated by the model based on the PavementAge and PavementExpectedLife
        

        segment.SurfaceMaterial = textParamValues["par_surf_mat"];
        segment.SurfaceClass = textParamValues["par_surf_class"].ToLower();
        // Automatically updated:
        // segment.SurfaceIsChipSealFlag 
        // segment.SurfaceIsChipSealOrACFlag 
        // segment.SurfaceRoadType
        segment.SurfaceThickness = numParamValues["par_surf_thick"];
        segment.SurfaceNumberOfLayers = numParamValues["par_surf_layers"];
        segment.SurfaceFunction = textParamValues["par_surf_func"];
        segment.SurfaceExpectedLife = numParamValues["par_surf_exp_life"];
        segment.SurfaceAge = numParamValues["par_surf_age"];         
        // Automatically updated:
        // segment.SurfaceAchievedLifePercent
        // segment.SurfaceRemainingLife 

        
        //Rutting and IRI
        segment.RutIncrement = numParamValues["par_rut_increm"];  // Updated Rut Increment for the episode
        segment.RutMeanLatent = numParamValues["par_rut"];              // Updated Rut value
        segment.RutMeanObserved = numParamValues["par_rut_obs"];              // Updated Rut value
        segment.RutAndIRIIncrementEpisodeLength = Convert.ToInt32(numParamValues["par_rut_iri_epi_len"]);

        segment.IRIIncrement = numParamValues["par_iri_increm"];  // Updated IRI increment for the episode
        segment.IRIMeanLatent = numParamValues["par_iri"];  // Updated IRI latent value
        segment.IRIMeanObserved = numParamValues["par_iri_obs"];  // Updated IRI observed value

        segment.TextureIncrement = numParamValues["par_text_increm"];  // Updated Texture increment for the episode
        segment.TextureMeanLatent = numParamValues["par_text"];  // Updated Texture latent value
        segment.TextureMeanObserved = numParamValues["par_text_obs"];  // Updated Texture observed value
        segment.TextureIncrementEpisodeLength = Convert.ToInt32(numParamValues["par_text_epi_len"]);

        // Distress indices
        segment.PavementDistressIndex = numParamValues["par_pdi"];
        segment.SurfaceDistressIndex = numParamValues["par_sdi"];
        segment.PavementDistressIndexRank = numParamValues["par_pdi_rank"];
        segment.SurfaceDistressIndexRank = numParamValues["par_sdi_rank"];

        segment.RehabilitationNeedsIndexRank = numParamValues["par_rni_rank"];
        segment.SurfacingNeedsIndexRank = numParamValues["par_sni_rank"];

        // Routine Maintenance
        segment.MaintenancePavement = numParamValues["par_maint_pa"]; // Updated maintenance extent for pavement
        segment.MaintenanceSurfacing = numParamValues["par_maint_su"]; // Updated maintenance extent for surfacing
        segment.MaintenancePotfill = numParamValues["par_maint_poth"]; // Updated maintenance extent for pothole filling

        // Candidate Selection from last period
        segment.CandidateSelectionOutcome = textParamValues["par_trigg_info"];

        segment.NumberOfTreatments = numParamValues["par_treat_count"]; 


        // Ensure that the method to re-calculate index values is called after return

        return segment;
    }


    /// <summary>
    /// Parses an ISO date from input file in format 'yyyyMMdd' and returns a DateTime object.
    /// </summary>
    /// <param name="isoDate">ISO date string in format 'yyyyMMdd'</param>
    /// <param name="errorLabel">Label for the field being parsed, used in error messages</param>
    /// <returns>Parsed DateTime object</returns>
    /// <exception cref="FormatException">Thrown when date format is invalid</exception>
    private static DateTime GetDateFromISO(string isoDate, string errorLabel)
    {
        try
        {
            return HelperMethods.ParseISODateNoTime(isoDate);
        }
        catch (Exception ex)
        {
            throw new FormatException($"Date for '{errorLabel}' is not in right format. Expecting 'yyyyMMdd'. Details: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the expected surface life for a segment, first checking if a specific value is provided in the input data for the segment, 
    /// and if not, looking up a default value based on the surface class. This method ensures that a valid expected surface life value is 
    /// always returned, either from the input data or from the lookups, and throws an exception if neither is available or if the surface 
    /// class is unrecognized. 
    /// </summary>
    /// <param name="model">Framework Model</param>
    /// <param name="segment">Road segment for which to get the expected surface life</param>
    /// <returns>Expected surface life value</returns>
    /// <exception cref="Exception">Thrown when the surface class is unrecognized or no valid expected surface life value is available  </exception>
    private static double GetExpectedSurfaceLifeSafe(ModelBase model, RoadSegmentMC segment)
    {
        double rawValue = model.GetInputDataNumber(segment.ElementIndex, "inp_surf_life_expected");
        if (rawValue > 0) return rawValue;

        switch (segment.SurfaceClass)
        {
            case "cs":
                return Convert.ToDouble(model.Lookups["surf_life_exp"]["default_cs"]);
            case "slurry":
                return Convert.ToDouble(model.Lookups["surf_life_exp"]["default_slurry"]);
            case "ac":
                return Convert.ToDouble(model.Lookups["surf_life_exp"]["default_ac"]);
            case "ogpa":
                return Convert.ToDouble(model.Lookups["surf_life_exp"]["default_ogpa"]);
            default:
                throw new Exception($"Unexpected surface class '{segment.SurfaceClass}' for elementIndex {segment.ElementIndex}. Cannot determine default expected surface life.");
        }
    }


    private static string GetDistressStateKey(ModelBase model, string extentColumn, string severityColumn, int elementIndex)
    {
        double rawExtent = model.GetInputDataNumber(elementIndex, extentColumn);
        double rawSeverity = model.GetInputDataNumber(elementIndex, severityColumn);

        double extent = Math.Floor(rawExtent);
        double severity = Math.Floor(rawSeverity);

        if (extent > 3 || severity > 3)
        {
            throw new Exception($"Invalid distress extent or severity value for elementIndex {elementIndex}. Extent: {rawExtent}, Severity: {rawSeverity}. Values should be between 0 and 3.");
        }

        // Collapse severity 2 and 3 into 2 only
        severity = severity > 1 ? 2 : severity;

        return $"E{extent}-S{severity}";

    }

}

