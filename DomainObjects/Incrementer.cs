using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing.Charts;
using JCass_ModelCore.Models;

namespace MonteCarloRoadModelV2.DomainObjects;

public class Incrementer
{

    private ModelBase _frameworkModel;
    private MonteCarloRoadModelV2 _domainModel;

    /// <summary>
    /// Minimum rut increment to prevent unrealistic negative increments. We allow some negative increments to account for maintenance effects, but we set 
    /// a lower bound to avoid extreme values. TODO: this can possibly be set in lookups instead of being hardcoded here. 
    /// </summary>
    private double _minRutIncrement = -0.2;

    /// <summary>
    /// Minimum IRI increment to prevent unrealistic negative increments. We allow some negative increments to account for maintenance effects, but we set
    /// a lower bound to avoid extreme values. TODO: this can possibly be set in lookups instead of being hardcoded here.
    /// </summary>
    private double _minIRIIncrement = -0.1;

    /// <summary>
    /// Maximum texture increment to prevent unrealistic positive increments. We allow some positive increments to account for maintenance effects, but we set
    /// a upper bound to avoid extreme values. TODO: this can possibly be set in lookups instead of being hardcoded here.
    /// </summary>
    private double _maxTextureIncrement = 0.25; 


    public Incrementer(ModelBase frameworkModel, MonteCarloRoadModelV2 domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public RoadSegmentMC Increment(RoadSegmentMC segment, int period)
    {

        if (segment.ElementIndex == 17477 && period > 5)
        {
            _ = 9;
        }

        // Increment all properties related to model parameters
        // Keep the code same order as the model parameter list

        segment.AverageDailyTraffic = segment.AverageDailyTraffic * (1 + segment.TrafficGrowthPercent / 100);
        // No need to reset HCV count as it is automatically calculated based on the AverageDailyTraffic and HCVPercent

        segment.PavementAge = segment.PavementAge + 1;
        segment.PavementRemainingLife = segment.PavementRemainingLife - 1;

        // No need to update Pavement Life Achieved and HCV Risk because it is automatically calculated based on the HCV and Pavement Life Achieved

        // No change in these properties:
        // segment.SurfaceMaterial 
        // segment.SurfaceClass
        // segment.SurfaceThickness            
        // segment.SurfaceNumberOfLayers
        // segment.SurfaceFunction 
        // segment.SurfaceExpectedLife 

        segment.SurfaceAge = segment.SurfaceAge + 1;

        // Note: surface life achieved and surface remaining life are automatically calculated based on the surface age and expected life

        //--------------------------------------------------------------------------------------------------------------------------------------------
        // HSD Increments. For a normal increment situation, we retain the previously assigned Base Increment for the Episode, and then
        // add a residual for the current year
        //--------------------------------------------------------------------------------------------------------------------------------------------

        // Check if we need to draw a new increment for rut and IRI based on the episode length. This will update the RutIncrement and IRIIncrement properties of the segment as needed
        CheckRutAndIRIIncrementForEpisode(segment, _domainModel.Constants.MaximumEpisodeLengthRutAndIRI);

        // Rut Depth        
        IncrementRut(segment);

        // IRI 
        IncrementIRI(segment);

        // Texture Depth
        // Check if we need to draw a new increment for texture based on the episode length. This will update the TextureIncrement property of the segment as needed
        CheckTextureIncrementForEpisode(segment, _domainModel.Constants.MaximumEpisodeLengthTexture);

        IncrementTexture(segment);

        // Maintenance - only update after historic maintenance use period. This is to ensure that we are using the actual maintenance
        // history for the first few periods, and then we can start applying the maintenance model after that.
        if (period > _domainModel.Constants.HistoricalMaintenanceUsePeriods)
        {
            _domainModel.MaintenanceModel.UpdateRoutineMaintenanceExtents(segment);
        }


        // Get the next state for distress states using the transition probability models. 
        // Skip this for historic model since we have no distress data before 2020
        MigrateDistressStates(segment, _domainModel.Constants);

        // Ranking parameters will be calculated by the framework model

        return segment;

    }


    /// <summary>
    /// Use transition probability models to get the next state for Pavement, Surfacing and Flushing Distress
    /// </summary>
    /// <param name="segment"></param>
    private void MigrateDistressStates(RoadSegmentMC segment,Constants constants)
    {
        segment.PavementDistressState = _domainModel.SubModels.PavementDistressModelUntreated.GetNextState(segment.PavementDistressState, _frameworkModel.Random);
        segment.SurfacingDistressState = _domainModel.SubModels.SurfaceDistressModelUntreated.GetNextState(segment.SurfacingDistressState, _frameworkModel.Random);
        segment.FlushingDistressState = _domainModel.SubModels.FlushingDistressModelUntreated.GetNextState(segment.FlushingDistressState, _frameworkModel.Random);
    }

    /// <summary>
    /// Increments the rut depth for the given road segment, taking into account if there was PA maintenance in the preceding period. If there 
    /// was maintenance on the pavement, it applies a reduction to the rut depth based on the maintenance extent. Otherwise, it calculates a new latent 
    /// rut mean by adding the rut increment to the previous latent mean, and then adds a residual to get the observed rut mean. The residual is drawn from 
    /// a normal distribution with a standard deviation that depends on the new latent rut mean.
    /// </summary>
    /// <param name="segment"></param>
    private void IncrementRut(RoadSegmentMC segment)
    {
        if (segment.MaintenancePavement > _domainModel.Constants.RutReductionDueToPAMaintenanceThreshold)
        {            
            double reductionDueToMaintenance = _domainModel.MaintenanceModel.GetRutReductionDueToMaintenance(segment);
            segment.RutMeanLatent = Math.Max(1.5, segment.RutMeanLatent - reductionDueToMaintenance);
            segment.RutMeanObserved = Math.Max(1.5, segment.RutMeanObserved - reductionDueToMaintenance);
        }
        else
        {
            double newValue = segment.RutMeanLatent + segment.RutIncrement;
            double residual = GetRutResidual(_domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants, newValue);
            segment.RutMeanLatent = newValue;
            segment.RutMeanObserved = segment.RutMeanLatent + residual;  // Update the observed rut mean with the residual to reflect the variability in the increment
        }        
    }

    /// <summary>
    /// Increments the IRI for the given road segment, taking into account if there was PA maintenance in the preceding period. If there
    /// was maintenance on the pavement, it applies a reduction to the IRI based on the maintenance extent. Otherwise, it calculates a new latent
    /// IRI mean by adding the IRI increment to the previous latent mean, and then adds a residual to get the observed IRI mean. The residual is drawn from
    /// a normal distribution with a standard deviation that depends on the new latent IRI mean.
    /// </summary>
    /// <param name="segment"></param>
    private void IncrementIRI(RoadSegmentMC segment)
    {        
        if (segment.MaintenancePavement > _domainModel.Constants.IriReductionDueToPAMaintenanceThreshold)
        {
            double reductionDueToMaintenance = _domainModel.MaintenanceModel.GetIRIReductionDueToMaintenance(segment);
            segment.IRIMeanLatent = Math.Max(2.5, segment.IRIMeanLatent - reductionDueToMaintenance);
            segment.IRIMeanObserved = Math.Max(2.5, segment.IRIMeanObserved - reductionDueToMaintenance);
        }
        else
        {
            double newValue = segment.IRIMeanLatent + segment.IRIIncrement;
            double residual = GetIRIResidual(_domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants, newValue);
            segment.IRIMeanLatent = newValue;
            segment.IRIMeanObserved = segment.IRIMeanLatent + residual;
        }
    }


    private void IncrementTexture(RoadSegmentMC segment)
    {
        double newValue = segment.TextureMeanLatent + segment.TextureIncrement;
        double residual = GetTextureResidual(_domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants, newValue);
        segment.TextureMeanLatent = newValue;
        segment.TextureMeanObserved = segment.TextureMeanLatent + residual;                              
    }

    private void CheckRutAndIRIIncrementForEpisode(RoadSegmentMC segment, int maximumEpisodeLength)
    {
        if (segment.RutAndIRIIncrementEpisodeLength < maximumEpisodeLength)
        {
            // No need to draw a new increment for rut and IRI

            //Increment the episode length for rut and IRI increments
            segment.RutAndIRIIncrementEpisodeLength++;
        }
        else
        {           
            // Need to draw a new increment for rut and IRI, and reset the episode length. Apply calibration factors.
            segment.RutIncrement = Math.Max(_minRutIncrement, GetRutIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants));
            segment.IRIIncrement = Math.Max(_minIRIIncrement, GetIRIIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants));

            if (segment.IRIIncrement < -1.0)
            {
                _ = 9;
            }

            //Reset the episode length to 1
            segment.RutAndIRIIncrementEpisodeLength = 1;
        }        
    }

    private void CheckTextureIncrementForEpisode(RoadSegmentMC segment, int maximumEpisodeLength)
    {
        if (segment.TextureIncrementEpisodeLength < maximumEpisodeLength)
        {
            // No need to draw a new increment for rut and IRI

            //Increment the episode length for rut and IRI increments
            segment.TextureIncrementEpisodeLength++;
        }
        else
        {
            // Need to draw a new increment for texture, and reset the episode length
            segment.TextureIncrement = Math.Min(_maxTextureIncrement, GetTextureIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants));            
           
            //Reset the episode length to 1
            segment.TextureIncrementEpisodeLength = 1;
        }
    }

    public static double GetRutResidual(SubModelDefinitions subModels, Random random, Constants constants, double newValue)
    {
        double residualCalibrationFactor = constants.CalFactRutResiduals;
        double standardDeviation = subModels.RutIncrementResidualSDFunction.GetValue(newValue);
        double residual = residualCalibrationFactor * subModels.NormalGenerator.NextNormal(0, standardDeviation);
        return residual;
    }

    public static double GetIRIResidual(SubModelDefinitions subModels, Random random, Constants constants, double newValue)
    {
        double residualCalibrationFactor = constants.CalFactIriResiduals;
        double standardDeviation = subModels.IRIIncrementResidualSDFunction.GetValue(newValue);
        double residual = residualCalibrationFactor * subModels.NormalGenerator.NextNormal(0, standardDeviation);
        return residual;
    }

    public static double GetTextureResidual(SubModelDefinitions subModels, Random random, Constants constants, double newValue)
    {
        double residualCalibrationFactor = constants.CalFactTextureResiduals;
        double standardDeviation = subModels.TextureIncrementResidualSDFunction.GetValue(newValue);
        double residual = residualCalibrationFactor * subModels.NormalGenerator.NextNormal(0, standardDeviation);
        return residual;
    }


    public static double GetRutIncrementForEpisode(RoadSegmentMC segment, SubModelDefinitions subModels, Random random, Constants constants)
    {        
        double sampledValue = subModels.RutIncrementSimulator.GetSimulatedValue(GetSimulatorInputValues(segment), random);
        double adjustment = 0.0;
        if (constants.IncremAdjustmentFactorsRut.ContainsKey(segment.SurfaceClass))
        {
            adjustment = constants.IncremAdjustmentFactorsRut[segment.SurfaceClass];
        }               
        return sampledValue + adjustment;
    }

    public static double GetIRIIncrementForEpisode(RoadSegmentMC segment, SubModelDefinitions subModels, Random random, Constants constants)
    {        
        double sampledValue = subModels.IRIIncrementSimulator.GetSimulatedValue(GetSimulatorInputValues(segment), random);
        double adjustment = 0.0;
        if (constants.IncremAdjustmentFactorsIRI.ContainsKey(segment.SurfaceClass))
        {
            adjustment = constants.IncremAdjustmentFactorsIRI[segment.SurfaceClass];
        }
        return sampledValue + adjustment;
    }

    public static double GetTextureIncrementForEpisode(RoadSegmentMC segment, SubModelDefinitions subModels, Random random, Constants constants)
    {
        if (segment.SurfaceClass != "cs")
        {
            // For non-chipseal surfaces, estimate texture increment as uniform between 0.05 and -0.04 (based on box plot for AC and OGPA)
            return random.NextDouble() * (0.05 - (-0.04)) - 0.04;
        }

        double sampledValue = subModels.TextureIncrementSimulator.GetSimulatedValue(GetSimulatorInputValues(segment), random);
        double adjustment = 0.0;
        if (constants.IncremAdjustmentFactorsTexture.ContainsKey(segment.SurfaceClass))
        {
            adjustment = constants.IncremAdjustmentFactorsTexture[segment.SurfaceClass];
        }
        return sampledValue + adjustment;
    }

    private static Dictionary<string, object> GetSimulatorInputValues(RoadSegmentMC segment)
    {
        //Note: cohort rules expect surface class 'concrete' to be 'conc'. So we need to convert it here before passing to the simulator
        Dictionary<string, object> inputParameters = new Dictionary<string, object>
        {
            { "rut_mean", segment.RutMeanLatent },
            { "iri_mean", segment.IRIMeanLatent },
            { "text_mean", segment.TextureMeanLatent },
            { "surf_age", segment.SurfaceAge },
            { "pre_potfill_mtc_extent", segment.MaintenancePotfill },
            { "pre_all_mtc_extent", segment.MaintenancePavement },
            { "pre_surf_mtc_extent", segment.MaintenanceSurfacing },
            { "adt", segment.AverageDailyTraffic },
            { "heavy_perc", segment.HeavyVehiclePercentage },
            { "surf_thick", segment.SurfaceThickness },            
            { "surf_class", segment.SurfaceClassForRules },
            { "surf_count", segment.SurfaceNumberOfLayers }
        };
        return inputParameters;
    }



}
