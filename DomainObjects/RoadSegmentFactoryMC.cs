
using JCass_ModelCore.Models;

namespace MonteCarloRoadModelV1.DomainObjects;

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
        segment.SegmentName = model.GetInputDataText(segment.ElementIndex, "inp_seg_name");
        segment.SectionID = model.GetInputDataNumber(segment.ElementIndex, "inp_section_id");
        segment.SectionName = model.GetInputDataText(segment.ElementIndex, "inp_section_name");
        segment.LocFrom = model.GetInputDataNumber(segment.ElementIndex, "inp_loc_from");
        segment.LocTo = model.GetInputDataNumber(segment.ElementIndex, "inp_loc_to");
        segment.LaneCode = model.GetInputDataText(segment.ElementIndex, "inp_lane_name");

        // Core measures
        segment.LengthInMetre = model.GetInputDataNumber(segment.ElementIndex, "inp_length");
        segment.AreaSquareMetre = model.GetInputDataNumber(segment.ElementIndex, "inp_area_m2");
        segment.WidthInMetre = segment.AreaSquareMetre / segment.LengthInMetre;

        // Classification
        segment.UrbanRural = model.GetInputDataText(segment.ElementIndex, "inp_urban_rural").ToLower();
        segment.ONRC = model.GetInputDataText(segment.ElementIndex, "inp_onrc").ToLower();
        
        
        // Traffic        
        segment.AverageDailyTraffic = model.GetInputDataNumber(segment.ElementIndex, "inp_adt");
        segment.HeavyVehiclePercentage = model.GetInputDataNumber(segment.ElementIndex, "inp_heavy_perc");        
        segment.TrafficGrowthPercent = model.GetInputDataNumber(segment.ElementIndex, "inp_traff_growth_perc");

        // Surfacing
        segment.SurfaceClass = model.GetInputDataText(segment.ElementIndex, "inp_surf_class").ToLower();
        segment.SurfacingDate = GetDateFromISO(model.GetInputDataText(segment.ElementIndex, "inp_surf_date"), "Surfacing Date");
        segment.SurfaceFunction = model.GetInputDataText(segment.ElementIndex, "inp_surf_function");        
        segment.SurfaceMaterial = model.GetInputDataText(segment.ElementIndex, "inp_surf_material");
        segment.SurfaceExpectedLife = model.GetInputDataNumber(segment.ElementIndex, "inp_surf_life_expected");
        segment.SurfaceNumberOfLayers = model.GetInputDataNumber(segment.ElementIndex, "inp_surf_layer_no");
        segment.SurfaceThickness = model.GetInputDataNumber(segment.ElementIndex, "inp_surf_thick");

        // Pavement        
        segment.PavementDate = GetDateFromISO(model.GetInputDataText(segment.ElementIndex, "inp_pave_date"), "Pavement Date");
        segment.PavementRemainingLife = model.GetInputDataNumber(segment.ElementIndex, "inp_pave_remlife");
        
        // Roughness and rutting
        segment.HSDSurveyDate = GetDateFromISO(model.GetInputDataText(segment.ElementIndex, "inp_rough_survey_date"),"HSD Survey Date");
        segment.IRIMean = model.GetInputDataNumber(segment.ElementIndex, "inp_iri_mean");
        segment.RutMean = model.GetInputDataNumber(segment.ElementIndex, "inp_iri_mean");
        segment.TextureMean = model.GetInputDataNumber(segment.ElementIndex, "inp_text_mean");


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
        RoadSegmentMC segment = new RoadSegmentMC();

        //First set all properties that are still dependend on the raw input data and that do not change over
        // the modelling periods

        segment.ElementIndex = elementIndex; // Set the element index for this segment

        // Identification
        segment.SegmentName = textInputValues["inp_seg_name"];
        segment.SectionID = Convert.ToInt32(numInputValues["inp_section_id"]);
        segment.SectionName = textInputValues["inp_section_name"];
        segment.LocFrom = Convert.ToInt32(numInputValues["inp_loc_from"]);
        segment.LocTo = Convert.ToInt32(numInputValues["inp_loc_to"]);
        segment.LaneCode = textInputValues["inp_lane_name"];

        // Core measures
        segment.LengthInMetre = numInputValues["inp_length"];
        segment.AreaSquareMetre = numInputValues["inp_area_m2"];
        segment.WidthInMetre = segment.AreaSquareMetre / segment.LengthInMetre;
        
        // Classification
        segment.UrbanRural = textInputValues["inp_urban_rural"].ToLower();
        segment.ONRC = textInputValues["inp_onrc"].ToLower();
                       
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
        segment.RutIncrement = numParamValues["par_rut_increm"];  // Updated rut
        segment.RutMean = numParamValues["par_rut"];

        segment.IRIIncrement = numParamValues["par_iri_increm"];  // Updated IRI increment
        segment.IRIMean = numParamValues["par_iri"];  // Updated IRI value
        
        // Ensure that the method to re-calculate index values are called on return

        return segment;
    }


    /// <summary>
    /// Parses an ISO date from input file in format 'yyyyMMdd' and returns a DateTime object. If the input is not a valid date, returns null.
    /// </summary>
    /// <param name="isodate"></param>
    private static DateTime GetDateFromISO(string isoDate, string errorLabel)
    {        
        if (DateTime.TryParseExact(isoDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
        {
            return parsedDate;
        }
        else
        {
            throw new Exception($"Date for '{errorLabel}' is not in right format. Expecting 'yyyyMMdd'");
        }

    }

}

