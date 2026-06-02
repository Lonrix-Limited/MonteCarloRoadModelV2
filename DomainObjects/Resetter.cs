using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing.Charts;
using JCass_Economics.Utilities;
using JCass_ModelCore.Models;
using JCass_ModelCore.Treatments;

namespace MonteCarloRoadModelV2.DomainObjects;

public class Resetter
{

    private ModelBase _frameworkModel;
    private MonteCarloRoadModelV2 _domainModel;

    public Resetter(ModelBase frameworkModel, MonteCarloRoadModelV2 domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public RoadSegmentMC ResetSegment(RoadSegmentMC segment, int period, TreatmentInstance treatment)
    {
        // if treatment is null, return segment without changes
        if (treatment == null) return segment;

        if (period > 2 && segment.ElementIndex == 10023)
        {
            _ = 0; // breakpoint anchor — set/remove IDE breakpoint here at runtime
        }


        bool isRehabTreatment = treatment.TreatmentName.ToLower().Contains("rehab");
        string treatmentTypeCode = isRehabTreatment ? "rehab" : "resurf";

        // Reset where needed, or Increment those that do not reset on treatment, such as traffic.
        // Keep the code same order as the model parameter list

        segment.AverageDailyTraffic = segment.AverageDailyTraffic * (1 + segment.TrafficGrowthPercent / 100);
        // No need to reset HCV count as it is automatically calculated based on the AverageDailyTraffic and HCVPercent

        segment.PavementAge = segment.PavementAge + 1;
        if (treatment.TreatmentName.ToLower().Contains("rehab")) segment.PavementAge = 0;  // Reset pavement age to 0 for rehab treatments. 

        segment.PavementRemainingLife = Convert.ToDouble(_frameworkModel.Lookups["pavement_expected_life"][segment.ONRC]);

        // No need to update Pavement Life Achieved and HCV Risk because it is automatically calculated based on the HCV and Pavement Life Achieved
        
        // Update surfacing age, class, material, thickness, function, expected life based on the treatment being applied. 
        UpdateSurfacingPropertiesForTreatment(segment, treatment, isRehabTreatment);

        UpdateFlagsForTreatment(segment, treatment);

        //--------------------------------------------------------------------------------------------------------------------------------------------
        // HSD Increments. When treatment is applied, always draw a new increment for the episode
        //--------------------------------------------------------------------------------------------------------------------------------------------

        // Rut Depth                
        double newValue = GetRutResetValue(segment, _domainModel.SubModels, treatment.TreatmentName, _domainModel.Constants, _frameworkModel.Random, period); //Reset value.
        segment.RutIncrement = Incrementer.GetRutIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants); //Get new increment for new eposode.        
        double residual = Incrementer.GetTextureResidual(_domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants, newValue);
        segment.RutMeanLatent = newValue;
        segment.RutMeanObserved = segment.RutMeanLatent + residual;  // Update the observed rut mean with the residual to reflect the variability in the increment
        segment.RutAndIRIIncrementEpisodeLength = 1;  // Reset episode length to 1 since we are drawing a new increment for the episode


        // IRI 
        newValue = GetIRIResetValue(segment, _domainModel.SubModels, treatment.TreatmentName, _domainModel.Constants, _frameworkModel.Random, period); //Reset value.
        segment.IRIIncrement = Incrementer.GetIRIIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants); //Get new increment for new eposode.        
        residual = Incrementer.GetIRIResidual(_domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants, newValue);
        segment.IRIMeanLatent = newValue;
        segment.IRIMeanObserved = segment.IRIMeanLatent + residual;
        // No need to reset episode separately for IRI as it is the same as Rut episode

        
        // Texture Depth               
        newValue = GetTextureDepthResetValue(segment, _domainModel.SubModels, treatment.TreatmentName, _domainModel.Constants, _frameworkModel.Random, period); //Reset value.
        segment.TextureIncrement = Incrementer.GetTextureIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants); //Get new increment for new eposode.        
        residual = Incrementer.GetTextureResidual(_domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants, newValue);
        segment.TextureMeanLatent = newValue;
        segment.TextureMeanObserved = segment.TextureMeanLatent + residual;
        segment.TextureIncrementEpisodeLength = 1;  // Reset episode length to 1 since we are drawing a new increment for the episode

        // Reset PDI and SDI based on treatment type. 
        ResetPavementDistressIndex(segment, _domainModel.SubModels, treatment.TreatmentName, _domainModel.Constants, _frameworkModel.Random, isRehabTreatment);
        ResetSurfacingDistressIndex(segment, _domainModel.SubModels, treatment.TreatmentName, _domainModel.Constants, _frameworkModel.Random);


        // Maintenance
        _domainModel.MaintenanceModel.UpdateRoutineMaintenanceExtents(segment);

        return segment;
    }

    #region Other Properties Reset

    private void UpdateFlagsForTreatment(RoadSegmentMC segment, TreatmentInstance treatment)
    {
        // Update any flags or categorical variables based on the treatment. For example, if the treatment is a rehab, we might want to reset the "MaintenancePavement" variable to 0 since the rehab would have addressed the maintenance issues on the pavement. 
        if (treatment.TreatmentName.ToLower().Contains("rehab"))
        {
            segment.CanDoThinACOverlay = 1; // After a rehab treatment, the segment can receive a thin AC overlay in the future
        }
    }

    #endregion

    #region Surfacing Properties Reset


    /// <summary>
    /// Updates surfacing properties of the given road segment based on the specified treatment. 
    /// </summary>
    /// <param name="segment">Segment on which to update surfacing properties</param>
    /// <param name="treatment">Treatment being applied</param>
    private void UpdateSurfacingPropertiesForTreatment(RoadSegmentMC segment, TreatmentInstance treatment, bool isRehabTreatment)
    {
        
        // No change in these properties:                       
        // segment.SurfaceExpectedLife 

        segment.SurfaceAge = 0;  
        segment.SurfaceMaterial = GetSurfaceMaterialAfterTreatment(treatment.TreatmentName);
        segment.SurfaceClass = segment.SurfaceMaterial; // After treatment,we simplify material name to be the same as surface class
        if (isRehabTreatment)
        {
            segment.SurfaceThickness = Convert.ToDouble(_frameworkModel.Lookups["surf_thickness_new"][segment.SurfaceMaterial]);
            segment.SurfaceNumberOfLayers = 1;

            // For rehab on chipseal, reset surfacing function to 1; for ac set to "R"
            segment.SurfaceFunction = segment.SurfaceClass == "cs" ? "1" : "R";            
        }
        else
        {
            double thicknessAdded = Convert.ToDouble(_frameworkModel.Lookups["surf_thickness_add"][segment.SurfaceMaterial]);
            segment.SurfaceThickness = segment.SurfaceThickness + thicknessAdded;
            segment.SurfaceNumberOfLayers += 1;

            bool isPresealRepairs = treatment.TreatmentName.ToLower().Contains("preseal");

            segment.SurfaceFunction = GetNextSurfaceFunction(segment.SurfaceFunction, isPresealRepairs);
        }

        List<string> specialCases = new List<string> { "blocks", "concrete", "xtreat" };
        if (specialCases.Contains(segment.SurfaceMaterial))
        {
            // For blocks, concrete and xtreat, we have a fixed expected life, unlike for cs, ogpa and ac, which also evaluates surface function and road category
            segment.SurfaceExpectedLife = Convert.ToDouble(_frameworkModel.Lookups["surf_life_exp"][segment.SurfaceMaterial]);
        }
        else
        {
            segment.SurfaceExpectedLife = Convert.ToDouble(_frameworkModel.Lookups["surf_life_exp"][segment.SurfaceExpectedLifeCode]);
        }
                    
        // Note: surface life achieved and surface remaining life are automatically calculated based on the surface age and expected life

    }

    private string GetNextSurfaceFunction(string currentFunction, bool isPresealRepairs)
    {
        if (isPresealRepairs) return "1a";
        switch (currentFunction)
        {
            case "1a":
                return "2";
            case "1":
                return "2";
            case "2": 
                return "R";
            default:
                return "R";
        }
    }

    private string GetSurfaceMaterialAfterTreatment(string treatmentName)
    {
        switch (treatmentName)
        {
            case "blocks" :
                return "blocks";
            case "concrete":
                return "concrete";
            case "xtreat":
                return "other";            
            default:
                //If we get here, we assume treatment name is of type 'cs_rehab'. Split and check if there is more than one value
                //after splitting by '_'. If not, throw an exception because we do not know how to determine the surfacing properties after treatment
                var parts = treatmentName.Split('_');
                if (parts.Length < 2) throw new InvalidOperationException($"Unexpected treatment name format: {treatmentName}. Expected format is 'surfacingtype_treatmenttype', e.g. 'cs_rehab'");
                string matCode = parts[0]; // The surfacing type code is the first part of the treatment name, e.g. 'cs' in 'cs_rehab'
                return matCode;
        }
    }

    #endregion

    #region Rut Reset

    /// <summary>
    /// Gets a simulated Rut Reset value for the given road segment based on the appropriate model for the surface class and treatment type.
    /// </summary>
    /// <param name="segment">Segment to simulate the Rut Reset value for</param>
    /// <param name="subModels">Sub-model definitions to use for the simulation</param>
    /// <param name="treatmentName">Treatment Name</param>
    /// <param name="constants">Model constants</param>
    /// <param name="random">Random number generator to use for the simulation</param>
    /// <returns>The simulated Rut Reset value</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static double GetRutResetValue(RoadSegmentMC segment, SubModelDefinitions subModels, string treatmentName, Constants constants, Random random, int period)
    {
        
        var inputParameters = GetInputParametersForSegment(segment);

        // Get the treatment specific calibration adjustment factor.
        double adjustment = 0;
        if (constants.ResetAdjustmentFactorsRut.ContainsKey(treatmentName))
        {
            adjustment = constants.ResetAdjustmentFactorsRut[treatmentName];
        }

        double resettedValue;

        if (treatmentName.Contains("rehab"))
        {
            if (period <= constants.MinimiseStochasticEffectsPeriod)
            {
                // Take the average of multiple draws to reduce stochasticity in the early periods after rehab, which can have a big impact on
                // model outputs and make it more difficult to identify the underlying drivers of model results.
                double meanReset = 0;
                int nDraws = 5;
                for (int i = 0; i < nDraws; i++)
                {
                    meanReset += subModels.RutResetSimulatorRehab.GetSimulatedValue(inputParameters, random);
                }
                resettedValue = meanReset / nDraws; 
            }
            else
            {
                resettedValue = subModels.RutResetSimulatorRehab.GetSimulatedValue(inputParameters, random);
            }                
        }
        else
        {
            if (period <= constants.MinimiseStochasticEffectsPeriod)
            {
                // Take the average of multiple draws to reduce stochasticity in the early periods after rehab, which can have a big impact on
                // model outputs and make it more difficult to identify the underlying drivers of model results.
                double meanReset = 0;
                int nDraws = 5;
                for (int i = 0; i < nDraws; i++)
                {
                    meanReset += subModels.RutResetSimulatorResurf.GetSimulatedValue(inputParameters, random);
                }
                resettedValue = meanReset / nDraws;
            }
            else
            {
                // Presume all other treatment types (holding, preseal repairs etc) reset as for resurfacing.
                resettedValue = subModels.RutResetSimulatorResurf.GetSimulatedValue(inputParameters, random);
            }
        }
                
        resettedValue = resettedValue + adjustment; // Apply the treatment specific adjustment to the reset value

        // Apply the overall calibration factor to the reset value after applying the treatment specific adjustment
        double calibrationFactor = constants.CalFactRutReset;
        return resettedValue * calibrationFactor;
    }

    #endregion

    #region IRI Reset

    /// <summary>
    /// Gets a simulated IRI Reset value for the given road segment based on the appropriate model for the surface class and treatment type.
    /// </summary>
    /// <param name="segment">Segment to simulate the IRI Reset value for</param>
    /// <param name="subModels">Sub-model definitions to use for the simulation</param>
    /// <param name="treatmentName">Treatment Name</param>
    /// <param name="constants">Model constants</param>
    /// <param name="random">Random number generator to use for the simulation</param>
    /// <returns>The simulated IRI Reset value</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static double GetIRIResetValue(RoadSegmentMC segment, SubModelDefinitions subModels, string treatmentName, Constants constants, Random random, int period)
    {
        var inputParameters = GetInputParametersForSegment(segment);
                
        // Get the treatment specific calibration adjustment factor.
        double adjustment = 0;
        if (constants.ResetAdjustmentFactorsIRI.ContainsKey(treatmentName))
        {
            adjustment = constants.ResetAdjustmentFactorsIRI[treatmentName];
        }

        double resettedValue;

        if (treatmentName.Contains("rehab"))
        {
            if (period <= constants.MinimiseStochasticEffectsPeriod)
            {
                // Take the average of multiple draws to reduce stochasticity in the early periods after rehab, which can have a big impact on
                // model outputs and make it more difficult to identify the underlying drivers of model results.
                double meanReset = 0;
                int nDraws = 5;
                for (int i = 0; i < nDraws; i++)
                {
                    meanReset += subModels.IRIResetSimulatorRehab.GetSimulatedValue(inputParameters, random);
                }
                resettedValue = meanReset / nDraws;
            }
            else
            {
                resettedValue = subModels.IRIResetSimulatorRehab.GetSimulatedValue(inputParameters, random);
            }
        }
        else
        {
            if (period <= constants.MinimiseStochasticEffectsPeriod)
            {
                // Take the average of multiple draws to reduce stochasticity in the early periods after rehab, which can have a big impact on
                // model outputs and make it more difficult to identify the underlying drivers of model results.
                double meanReset = 0;
                int nDraws = 5;
                for (int i = 0; i < nDraws; i++)
                {
                    meanReset += subModels.IRIResetSimulatorResurf.GetSimulatedValue(inputParameters, random);
                }
                resettedValue = meanReset / nDraws;
            }
            else
            {
                // Presume all other treatment types (holding, preseal repairs etc) reset as for resurfacing.
                resettedValue = subModels.IRIResetSimulatorResurf.GetSimulatedValue(inputParameters, random);
            }
        }

        resettedValue = resettedValue + adjustment; // Apply the treatment specific adjustment to the reset value

        // Apply the overall calibration factor to the reset value after applying the treatment specific adjustment
        double calibrationFactor = constants.CalFactIriReset;
        return resettedValue * calibrationFactor;

    }
        

    #endregion

    #region Texture Reset

    /// <summary>
    /// Gets a simulated Texture Depth Reset value for the given road segment based on the appropriate model for the surface class and treatment type.
    /// </summary>
    /// <param name="segment">Segment to simulate the Texture Depth Reset value for</param>
    /// <param name="subModels">Sub-model definitions to use for the simulation</param>    
    /// <param name="random">Random number generator to use for the simulation</param>
    /// <returns>The simulated Texture Depth  Reset value</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static double GetTextureDepthResetValue(RoadSegmentMC segment, SubModelDefinitions subModels, string treatmentName, Constants constants, Random random, int period)
    {
        var inputParameters = GetInputParametersForSegment(segment);
        double resettedValue = 0;

        // Get the treatment specific calibration adjustment factor.
        double adjustment = 0;
        if (constants.ResetAdjustmentFactorsTexture.ContainsKey(treatmentName))
        {
            adjustment = constants.ResetAdjustmentFactorsTexture[treatmentName];
        }


        if (period <= constants.MinimiseStochasticEffectsPeriod)
        {
            // Take the average of multiple draws to reduce stochasticity in the early periods after treatment, which can have a big impact on
            // model outputs and make it more difficult to identify the underlying drivers of model results.
            double meanReset = 0;
            int nDraws = 5;
            for (int i = 0; i < nDraws; i++)
            {
                meanReset += subModels.TextureResetSimulator.GetSimulatedValue(inputParameters, random);
            }
            resettedValue = meanReset / nDraws;
        }
        else
        {
            //Texture reset does not vary by treatment class, so we can use the same model for all segments regardless of treatment.                
            resettedValue = subModels.TextureResetSimulator.GetSimulatedValue(inputParameters, random); 
        }

        resettedValue = resettedValue + adjustment; // Apply the treatment specific adjustment to the reset value

        // Apply the overall calibration factor to the reset value after applying the treatment specific adjustment
        double calibrationFactor = constants.CalFactTextureReset;
        return resettedValue * calibrationFactor;
    }

    #endregion

    #region PDI and SDI reset

    /// <summary>
    /// Resets the Pavement Distress Index. For rehab treatments, we assume that the treatment will reset the PDI to . For 
    /// non-rehab treatments, we assume that the treatment will reduce the PDI but not reset it to 0, as some damage will remain after treatment. 
    /// TODO: improve logic here.
    /// </summary>
    /// <param name="segment"></param>
    /// <param name="subModels"></param>
    /// <param name="treatmentName"></param>
    /// <param name="constants"></param>
    /// <param name="random"></param>
    /// <param name="isRehab"></param>
    public static void ResetPavementDistressIndex(RoadSegmentMC segment, SubModelDefinitions subModels, string treatmentName, Constants constants, Random random, bool isRehab)
    {
        if (isRehab)
        {
            segment.PavementDistressIndex = 0;
        }
        else
        {
            // For non-rehab treatments, we assume that the treatment will reduce the PDI but not reset it to 0, as
            // some damage will remain after treatment. This means PDI will start progressing again right after the treatment, unless it 
            // was zero to begin with
            segment.PavementDistressIndex = Math.Min(segment.PavementDistressIndex, 0.1);
        }
    }

    /// <summary>
    /// Resets the Surface Distress Index. TODO: improve logic here.
    /// </summary>
    public static void ResetSurfacingDistressIndex(RoadSegmentMC segment, SubModelDefinitions subModels, string treatmentName, Constants constants, Random random)
    {
        segment.SurfaceDistressIndex = 0;        
    }


    #endregion

    #region Helper Methods

    private static Dictionary<string, object> GetInputParametersForSegment(RoadSegmentMC segment)
    {        
        return new Dictionary<string, object>
        {
            { "rut_mean_pre", segment.RutMeanLatent },
            { "iri_mean_pre", segment.IRIMeanLatent },
            { "surf_age", segment.SurfaceAge },            
            { "pre1_rea_mtc_extent", segment.MaintenancePavement },
            { "adt", segment.AverageDailyTraffic },
            { "heavy_perc", segment.HeavyVehiclePercentage },
            { "surf_thick", segment.SurfaceThickness },
            { "surf_class", segment.SurfaceClassForRules },
            { "surf_count", segment.SurfaceNumberOfLayers }
        };
    }

    #endregion
}
