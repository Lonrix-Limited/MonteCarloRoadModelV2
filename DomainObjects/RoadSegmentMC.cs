using JCass_Functions.Engineering;
using JCass_ModelCore.Models;

namespace MonteCarloRoadModelV1.DomainObjects;


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
            return $"elem_index: {this.ElementIndex:D4} - {this.SegmentName}";
        }
    }

    /// <summary>
    /// Segment identifier. Maps to input column "file_seg_name".
    /// </summary>
    public string SegmentName { get; set; }

    /// <summary>
    /// Section ID. Maps to "file_section_id".
    /// </summary>
    public double SectionID { get; set; }

    /// <summary>
    /// Name of the section. Maps to "file_section_name".
    /// </summary>
    public string SectionName { get; set; }

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
    public string LaneCode { get; set; }

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

    private string _surfaceClass;

    /// <summary>
    /// Surface class ('cs', 'ac', 'blocks', 'concrete', 'other').
    /// </summary>
    public string SurfaceClass
    {
        get => _surfaceClass;
        set => _surfaceClass = value?.ToLower();
    }

    
    /// <summary>
    /// Flag indicating if the surface is a chip seal. This is calculated based on the SurfaceClass property.
    /// </summary>
    public int SurfaceIsChipSealFlag
    {
        get
        {
            // Return 1 if the surface class is 'cs' (chip seal), otherwise return 0.
            return this.SurfaceClass == "cs" ? 1 : 0;
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
    public string SurfaceMaterial { get; set; }

    /// <summary>
    /// Surfacing expected life (years) from RAMM.
    /// </summary>
    public double SurfaceExpectedLife { get; set; }

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
    /// Returns the percentage of the Surface Expected Life that has been achieved based on the Surface Age.
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
            return Math.Min(200, 100 * (this.SurfaceAge / this.SurfaceExpectedLife));
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

    #region ONRC and Carriageway Attributes

    private string _urbanRural;
    private string _onrc;    
    
    /// <summary>
    /// Urban/Rural flag.
    /// </summary>
    public string UrbanRural
    {
        get => _urbanRural;
        set => _urbanRural = value?.ToLower();
    }

    /// <summary>
    /// ONRC Category.
    /// </summary>
    public string ONRC
    {
        get => _onrc;
        set => _onrc = value?.ToLower();
    }
            
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
    
    #region High Speed Data (HSD) (Rut, Roughness, Texture etc.)

    /// <summary>
    /// HSD survey date (expecting ISO date in format 'yyyymmdd' in input data
    /// </summary>
    public DateTime HSDSurveyDate { get; set; }

    
    /// <summary>
    /// Mean IRI (mm/m) for the segment
    /// </summary>
    public double IRIMean { get; set; }

    /// <summary>
    /// Episode Increment for IRI in mm/m/year. 
    /// </summary>
    public double IRIIncrement { get; set; }

    /// <summary>
    /// Mean Rut Depth in mm.    
    /// </summary>
    public double RutMean { get; set; }

    /// <summary>
    /// Rut increment in mm/year for the episode    
    /// </summary>
    public double RutIncrement { get; set; }

    /// <summary>
    /// Texture depth mean for segment, in mm
    /// </summary>
    public double TextureMean { get; set; }

    /// <summary>
    /// Texture depth increment for the episode, in mm/year
    /// </summary>
    public double TextureIncrement { get; set; }
     
    #endregion
    

}


