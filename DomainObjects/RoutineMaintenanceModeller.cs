using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JCass_ModelCore.Models;

namespace MonteCarloRoadModelV2.DomainObjects;

public class RoutineMaintenanceModeller
{

    private ModelBase _frameworkModel;
    private MonteCarloRoadModelV2 _domainModel;

    public RoutineMaintenanceModeller(ModelBase frameworkModel, MonteCarloRoadModelV2 domainModel)
    {
        _frameworkModel = frameworkModel ?? throw new ArgumentNullException(nameof(frameworkModel), "Domain model cannot be null");
        _domainModel = domainModel ?? throw new ArgumentNullException(nameof(domainModel), "Domain model cannot be null");
    }

    /// <summary>
    /// Updates the maintenance extent for PA (excluding potfill) and potfill maintenance based on the 
    /// predicted probabilities and extents from the domain model.
    /// </summary>
    /// <param name="segment"></param>
    public void UpdateRoutineMaintenanceExtents(RoadSegmentMC segment)
    {
        double calibrationFactor = _domainModel.Constants.CalFactPaMaintenanceProbability;

        // Deal first with PA maintenance (excluding potfill) 
        double probabilityOfMaintenance = calibrationFactor * GetMaintenanceProbabilityPA(segment);
        double randomValue = _frameworkModel.Random.NextDouble();
        if (randomValue < probabilityOfMaintenance)
        {
            segment.MaintenancePavement = _domainModel.Constants.CalFactPaMaintenanceExtent * GetMaintenanceExtentPA(segment);
        }
        else
        {
            segment.MaintenancePavement = 0;
        }

        // Now deal with potfill maintenance
        probabilityOfMaintenance = _domainModel.Constants.CalFactPotfillProbability * GetMaintenanceProbabilityPotFill(segment);
        randomValue = _frameworkModel.Random.NextDouble();
        if (randomValue < probabilityOfMaintenance)
        {
            segment.MaintenancePotfill = _domainModel.Constants.CalFactPotfillMaintenanceExtent * GetMaintenanceExtentPotfill(segment);
        }
        else
        {
            segment.MaintenancePotfill = 0;
        }
    }


    /// <summary>
    /// Gets the simulated reduction in Rut Depth (not reset value, but reduction in the current value) after PA maintenance (excluding potfill) based on the extent of maintenance.
    /// </summary>
    /// <param name="maintenanceExtent"></param>
    /// <returns></returns>
    public double GetRutReductionDueToMaintenance(RoadSegmentMC segment)
    {        
        Dictionary<string, object> inputParameters = new Dictionary<string, object>
        {
            { "maint_extent", segment.MaintenancePavement },
            { "rut_mean_pre", segment.RutMeanLatent },
            { "iri_mean_pre", segment.IRIMeanLatent },
            { "surf_age", segment.SurfaceAge },
            { "surf_class", segment.SurfaceClassForRules },
            { "surf_count", segment.SurfaceNumberOfLayers }
        };
        
        double reducRaw = _domainModel.SubModels.RutReductionAfterPaMaintenanceSimulator.GetSimulatedValue(inputParameters, _frameworkModel.Random);     
        double minReduction = _domainModel.Constants.CalFactMinRutReductionDueToPAMaintenance;
        if (reducRaw < minReduction) reducRaw = minReduction;  // Set a minimum reduction to avoid cases where the model might predict a very small reduction that is not realistic based on engineering judgement.        
        return reducRaw;
    }

    /// <summary>
    /// Gets the simulated reduction in IRI (not reset value, but reduction in the current value) after PA maintenance
    /// </summary>
    /// <param name="maintenanceExtent"></param>
    /// <returns></returns>
    public double GetIRIReductionDueToMaintenance(RoadSegmentMC segment)
    {        
        Dictionary<string, object> inputParameters = new Dictionary<string, object>
        {
            { "maint_extent", segment.MaintenancePavement },
            { "rut_mean_pre", segment.RutMeanLatent },
            { "iri_mean_pre", segment.IRIMeanLatent },
            { "surf_age", segment.SurfaceAge },                        
            { "surf_count", segment.SurfaceNumberOfLayers },
            { "surf_class", segment.SurfaceClassForRules }
        };
        
        double reducRaw = _domainModel.SubModels.IRIReductionAfterPaMaintenanceSimulator.GetSimulatedValue(inputParameters, _frameworkModel.Random);
        double minReduction = _domainModel.Constants.CalFactMinIriReductionDueToPAMaintenance;
        if (reducRaw < minReduction) reducRaw = minReduction;  // Set a minimum reduction to avoid cases where the model might predict a very small reduction that is not realistic based on engineering judgement.
        return reducRaw;
    }

    /// <summary>
    /// Gets the probability of PA maintenance (excluding potfill) for the given road segment based on the appropriate model for the surface class.
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private double GetMaintenanceProbabilityPA(RoadSegmentMC segment)
    {
        Dictionary<string, double> inputParameters = new Dictionary<string, double>
        {
            { "rut_mean_pre", segment.RutMeanLatent },
            { "iri_mean_pre", segment.IRIMeanLatent },
            { "surf_age", segment.SurfaceAge },
            { "pre_potfill_mtc_extent", segment.MaintenancePotfill },
            { "pre_all_mtc_extent", segment.MaintenancePavement },
            { "log(adt)", Math.Log(segment.AverageDailyTraffic) },
            { "heavy_perc", segment.HeavyVehiclePercentage }            
        };

        if (segment.SurfaceClass == "cs" || segment.SurfaceClass == "slurry")
        {
            return _domainModel.SubModels.MaintPaProbabilityModelCS.PredictProbability(inputParameters);
        }
        else if (segment.SurfaceClass == "ac" || segment.SurfaceClass == "ogpa")
        {
            return _domainModel.SubModels.MaintPaProbabilityModelAC.PredictProbability(inputParameters);
        }
        if (segment.SurfaceClass == "concrete")
        {
            return 0.0;   //Not enough data. TODO: explore potfill model for concrete
        }
        if (segment.SurfaceClass == "unknown")
        {
            //For unknown, return value for CS and Slurry as a best-guess
            return _domainModel.SubModels.MaintPaProbabilityModelCS.PredictProbability(inputParameters);
        }
        else
        {
            throw new InvalidOperationException($"Unknown surface class: {segment.SurfaceClass}");
        }
    }


    /// <summary>
    /// Gets the probability of potfill maintenance for the given road segment based on the appropriate model for the surface class.
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private double GetMaintenanceProbabilityPotFill(RoadSegmentMC segment)
    {
        Dictionary<string, double> inputParameters = new Dictionary<string, double>
        {
            { "rut_mean_pre", segment.RutMeanLatent },
            { "iri_mean_pre", segment.IRIMeanLatent },
            { "surf_age", segment.SurfaceAge },
            { "pre_potfill_mtc_extent", segment.MaintenancePotfill },
            { "pre_all_mtc_extent", segment.MaintenancePotfill },
            { "log(adt)", Math.Log(segment.AverageDailyTraffic) },
            { "heavy_perc", segment.HeavyVehiclePercentage },
        };

        if (segment.SurfaceClass == "cs" || segment.SurfaceClass == "slurry")
        {
            return _domainModel.SubModels.PotfillProbabilityModelCS.PredictProbability(inputParameters);
        }
        else if (segment.SurfaceClass == "ac" || segment.SurfaceClass == "ogpa")
        {
            return _domainModel.SubModels.PotfillProbabilityModelAC.PredictProbability(inputParameters);
        }
        if (segment.SurfaceClass == "concrete")
        {
            return 0.0;   //Not enough data. TODO: explore potfill model for concrete
        }
        if (segment.SurfaceClass == "unknown")
        {
            //For unknown, return value for CS and Slurry as a best-guess
            return _domainModel.SubModels.MaintPaProbabilityModelCS.PredictProbability(inputParameters);
        }
        else
        {
            throw new InvalidOperationException($"Unknown surface class: {segment.SurfaceClass}");
        }
    }

    /// <summary>
    /// Gets a simulated maintenance extent for PA maintenance (excluding potfill) for the given road segment based on the appropriate model for the surface class.
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private double GetMaintenanceExtentPA(RoadSegmentMC segment)
    {        
        Dictionary<string, object> inputParameters = new Dictionary<string, object>
        {
            { "rut_mean_pre", segment.RutMeanLatent },
            { "iri_mean_pre", segment.IRIMeanLatent },
            { "surf_age", segment.SurfaceAge },
            { "pre_potfill_mtc_extent", segment.MaintenancePotfill },
            { "pre_all_mtc_extent", segment.MaintenancePavement },
            { "adt", segment.AverageDailyTraffic },
            { "heavy_perc", segment.HeavyVehiclePercentage },
            { "surf_thick", segment.SurfaceThickness },
            { "surf_class", segment.SurfaceClassForRules }
        };

        return _domainModel.SubModels.MaintenanceExtentPA.GetSimulatedValue(inputParameters, _frameworkModel.Random);

    }


    /// <summary>
    /// Gets a simulated maintenance extent for potfill maintenance for the given road segment based on the appropriate model for the surface class.
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private double GetMaintenanceExtentPotfill(RoadSegmentMC segment)
    {
        Dictionary<string, object> inputParameters = new Dictionary<string, object>
        {
            { "rut_mean_pre", segment.RutMeanLatent },
            { "iri_mean_pre", segment.IRIMeanLatent },
            { "surf_age", segment.SurfaceAge },
            { "pre_potfill_mtc_extent", segment.MaintenancePotfill },
            { "pre_all_mtc_extent", segment.MaintenancePavement },
            { "adt", segment.AverageDailyTraffic },
            { "heavy_perc", segment.HeavyVehiclePercentage },
            { "surf_thick", segment.SurfaceThickness },
            { "surf_class", segment.SurfaceClassForRules }
        };

        return _domainModel.SubModels.MaintenanceExtentPotfill.GetSimulatedValue(inputParameters, _frameworkModel.Random);        
    }

}
