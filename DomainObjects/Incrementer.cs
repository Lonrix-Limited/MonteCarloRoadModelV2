using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing.Charts;
using JCass_ModelCore.Models;

namespace MonteCarloRoadModelV1.DomainObjects;

public class Incrementer
{

    private ModelBase _frameworkModel;
    private MonteCarloRoadModelV1 _domainModel;

    public Incrementer(ModelBase frameworkModel, MonteCarloRoadModelV1 domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    public RoadSegmentMC Increment(RoadSegmentMC segment, int period)
    {

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

        double newValue = segment.TextureMeanLatent + segment.TextureIncrement;
        double residual = GetTextureResidual(_domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants, newValue);
        segment.TextureMeanLatent = newValue;
        segment.TextureMeanObserved = segment.TextureMeanLatent + residual;

        // Maintenance
        _domainModel.MaintenanceModel.UpdateRoutineMaintenanceExtents(segment);
        
        //PDI and SDI
        IncrementPDIandSDI(segment, _domainModel.Constants);

        // Ranking parameters will be calculated by the framework model

        return segment;

    }


    /// <summary>
    /// Placeholder for incrementing PDI and SDI. TODO: Update this with simulator etc.
    /// </summary>
    /// <param name="segment"></param>
    private void IncrementPDIandSDI(RoadSegmentMC segment,Constants constants)
    {
        // Very simple placeholder logic: if PDI or SDI is zero, it stays zero.
        
        // PDI: if above zero, increase by base rate of 1% plus 0.5% per year for each mm that rut is above threshold.
        double rutThreshold = constants.TSSExcessRutThresh;
        double excessRut = Math.Max(0, segment.RutMeanObserved - rutThreshold);
        if (segment.PavementDistressIndex > 0)
        {
            double pdiIncrement = 0.5 + (0.1 * excessRut);
            segment.PavementDistressIndex = segment.PavementDistressIndex + pdiIncrement;
        }

        // SDI: if above zero, increase by LN(ADT)/5; this gives about 0.5% increase at ADT = 10, 
        // 1% increase at ADT = 200, and 1.5% increase at ADT = 1600, 2% at ADT 20,000 etc.
        if (segment.SurfaceDistressIndex > 0)
        {
            double sdiIncrement = Math.Log(segment.AverageDailyTraffic+1) / 5.0;
            segment.SurfaceDistressIndex = segment.SurfaceDistressIndex + sdiIncrement;
        }

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
            segment.RutIncrement = GetRutIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants);
            segment.IRIIncrement = GetIRIIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants);
            
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
            segment.TextureIncrement = GetTextureIncrementForEpisode(segment, _domainModel.SubModels, _frameworkModel.Random, _domainModel.Constants);            
           
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
        double calibrationFactor = constants.CalFactRutIncrement.GetValue(sampledValue);
        if (sampledValue > constants.CalMaxRutIncrement) sampledValue = constants.CalMaxRutIncrement;
        return calibrationFactor * sampledValue;
    }

    public static double GetIRIIncrementForEpisode(RoadSegmentMC segment, SubModelDefinitions subModels, Random random, Constants constants)
    {        
        double sampledValue = subModels.IRIIncrementSimulator.GetSimulatedValue(GetSimulatorInputValues(segment), random);
        double calibrationFactor = constants.CalFactIriIncrement.GetValue(sampledValue);
        if (sampledValue > constants.CalMaxIriIncrement) sampledValue = constants.CalMaxIriIncrement;
        return calibrationFactor * sampledValue;
    }

    public static double GetTextureIncrementForEpisode(RoadSegmentMC segment, SubModelDefinitions subModels, Random random, Constants constants)
    {
        if (segment.SurfaceClass != "cs")
        {
            // For non-chipseal surfaces, estimate texture increment as uniform between 0.05 and -0.04 (based on box plot for AC and OGPA)
            return random.NextDouble() * (0.05 - (-0.04)) - 0.04;
        }

        double sampledValue = subModels.TextureIncrementSimulator.GetSimulatedValue(GetSimulatorInputValues(segment), random);
        double calibrationFactor = constants.CalFactTextureIncrement.GetValue(sampledValue);
        return calibrationFactor * sampledValue;
    }

    private static Dictionary<string, object> GetSimulatorInputValues(RoadSegmentMC segment)
    {
        //Note: cohort rules expect surface class 'concrete' to be 'conc'. So we need to convert it here before passing to the simulator
        Dictionary<string, object> inputParameters = new Dictionary<string, object>
        {
            { "rut_mean", segment.RutMeanLatent },
            { "iri_mean", segment.IRIMeanLatent },
            { "surf_age", segment.SurfaceAge },
            { "pre_potfill_mtc_extent", segment.MaintenancePotfill },
            { "pre_all_mtc_extent", segment.MaintenancePavement },
            { "adt", segment.AverageDailyTraffic },
            { "heavy_perc", segment.HeavyVehiclePercentage },
            { "surf_thick", segment.SurfaceThickness },
            { "rainfall", segment.RainfallMM },
            { "surf_class", segment.SurfaceClassForRules },
            { "surf_count", segment.SurfaceNumberOfLayers }
        };
        return inputParameters;
    }



}
