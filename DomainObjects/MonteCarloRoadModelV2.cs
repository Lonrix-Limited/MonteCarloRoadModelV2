
using System.Runtime.CompilerServices;
using JCass_Core.Statistics;
using JCass_Data.Objects;
using JCass_Data.Utils;
using JCass_ModelCore.DomainModels;
using JCass_ModelCore.MonteCarlo;
using JCass_ModelCore.Treatments;
using MonteCarloRoadModelV2.Utilities;

namespace MonteCarloRoadModelV2.DomainObjects;

public class MonteCarloRoadModelV2 : DomainModelBase
{

    #region Variables and Submodels

    public Constants Constants { get; set; } = null!;

    private Initialiser _initialiser = null!;

    private Incrementer _incrementer = null!;

    private Resetter _resetter = null!;

    public SubModelDefinitions SubModels { get; set; } = null!;

    public RoutineMaintenanceModeller MaintenanceModel = null!;
    
    #endregion

    public MonteCarloRoadModelV2()
    {
        //Nothing to do here. Note that property 'model' mapping to the ModelBase class (i.e. the Framework Model)
        //will be automatically set up right after this default constructor is called.        
    }

    /// <summary>
    /// Stub that allows custom domain models to set up custom elements such as Machine Learning models,
    /// lookups, special objects etc
    /// </summary>
    public override void SetupInstance()
    {
        try
        {
            _initialiser = new Initialiser(this.model, this);            
            _incrementer = new Incrementer(this.model, this);
            _resetter = new Resetter(this.model, this);
            MaintenanceModel = new RoutineMaintenanceModeller(this.model, this);

            this.SubModels = new SubModelDefinitions(model.RandomSeed);

            this.Constants = new Constants(this.model.Lookups);
            
            string workFolder = model.Configuration.WorkFolder;

            // Set up the distribution simulators for the increments for Rut, IRI and Texture
            SetupUtilities.SetupDistributionSimulators(this, workFolder, this.model.Random);

            // Set up the Piecewise Linear Models to calculate the Standard Deviation of the residuals for the increments
            // for Rut, IRI and Texture
            SetupUtilities.SetupIncrementResidualModels(this, workFolder);

            // Set up the probability models for pothole filling
            SetupUtilities.SetupProbabilityModels(this, workFolder);

            // Set up the models for the reset values after treatments
            SetupUtilities.SetupResetModels(this, workFolder);

            // Set up the models for the reduction in condition after PA maintenance
            SetupUtilities.SetupReductionDueToPaMaintenanceModels(this, workFolder, this.model.Random);

            SetupUtilities.SetupTreatmentSuitabilityScoreModels(this);

        }
        catch (Exception ex)
        {
            // Tell the user where the error occurred
            throw new Exception($"Error setting up custom Road Network Model: {ex.Message}");            
        }        
    }

 
    /// <summary>
    /// Evaluates the Initial Values for all parameters for the element at the start of the analysis. This method is called from the Framework Model 
    /// for all elements at the start of the model run. Use the raw/input data values with domain logic to assign an initial value to all
    /// modelling parameters. 
    /// </summary>
    /// <param name="iElemIndex">Zero-based index of the element</param>        
    /// <param name="numInputs">Raw numeric input values for the element. Keys are input names, values are input values</param>
    /// <param name="textInputs">Raw text input values for the element. Keys are input names, values are input values</param>
    /// <param name="numModParamValues">Return value: Sink holding values for numeric parameters (to be updated by Domain Model). Keys are parameter names, values are assigned values</param>
    /// <param name="textModParamValues">Return value: Sink holding values for text parameters (to be updated by Domain Model). Keys are parameter names, values are assigned values</param>  
    public override void Initialise(int iElemIndex, Dictionary<string, double> numInputs, Dictionary<string, string> textInputs,
            Action<string, double> numModParamValues, Action<string, string> textModParamValues)
    {
        try
        {
            Dictionary<string, object> infoFromModel = model.GetSpecialPlaceholderValues(iElemIndex, 0);
            RoadSegmentMC segment = _initialiser.InitialiseSegment(iElemIndex);

            //// Update the formula values such as PDI, SDI, Objective Value Parameters, Maintenance Cost and CSA Status/Outcome
            //// before getting the parameter values
            //segment.UpdateFormulaValues(this.model, this, 0,infoFromModel);

            //// By updating the sinks, the model will automatically update the values in the Framework Model matrices
            segment.SetParameterValues(numModParamValues, textModParamValues, this.Constants, infoFromModel, 0);
                        
        }
        catch (Exception ex)
        {
            throw new Exception($"Error initialising on element index {iElemIndex}. Details: {ex.Message}");
        }
    }

    /// <summary>
    /// Evaluates the Reset/Updated values for all parameters for the element at the start of the analysis. This method is called from the Framework Model 
    /// for all elements at the start of the model run. Use the raw/input data values with domain logic to assign an initial value to all
    /// modelling parameters. 
    /// </summary>
    /// <param name="iElemIndex">Zero-based index of the element</param>
    /// <param name="iPeriod">Modelling period (values like 1,2,...n)</param>
    /// <param name="numInputs">Raw numeric input values for the element. Keys are input names, values are input values</param>
    /// <param name="textInputs">Raw text input values for the element. Keys are input names, values are input values</param>
    /// <param name="currentNumModParamValues">Values for Numeric Model Parameters as they were in the previous epoch. Keys are parameter names</param>
    /// <param name="currentTextModParamValues">Values for Text Model Parameters as they were at the previous epoch. Keys are parameter names</param>
    /// <param name="numModParamValues">Return value: Sink holding values for numeric parameters (to be updated by Domain Model). Keys are parameter names, values are assigned values</param>
    /// <param name="textModParamValues">Return value: Sink holding values for text parameters (to be updated by Domain Model). Keys are parameter names, values are assigned values</param>        
    public override void Reset(TreatmentInstance treatment, int iElemIndex, int iPeriod,
        Dictionary<string, double> numInputs, Dictionary<string, string> textInputs,
        Dictionary<string, double> currentNumModParamValues, Dictionary<string, string> currentTextModParamValues,
        Action<string, double> numModParamValues, Action<string, string> textModParamValues)
    {
        try
        {

            Dictionary<string, object> infoFromModel = model.GetSpecialPlaceholderValues(iElemIndex, iPeriod, treatment);

            RoadSegmentMC segment = RoadSegmentFactoryMC.GetFromModel(this.model, numInputs, textInputs, currentNumModParamValues, currentTextModParamValues, iElemIndex, iPeriod);
                       

            // Apply Resets
            RoadSegmentMC resettedSegment = _resetter.ResetSegment(segment, iPeriod, treatment);
            //resettedSegment.UpdateFormulaValues(this.model, this, iPeriod, infoFromModel);
            
            resettedSegment.SetParameterValues(numModParamValues, textModParamValues, this.Constants, infoFromModel, iPeriod);
            
        }
        catch (Exception ex)
        {
            throw new Exception($"Error Resetting element index {iElemIndex}. Details: {ex.Message}");
        }
    }

    /// <summary>
    /// Evaluates the Increment for all parameters for the element in the current period. This method is called from the Framework Model 
    /// for elements that do not have a treatment selected after optimisation in the current period. 
    /// </summary>
    /// <param name="iElemIndex">Zero-based index of the element</param>
    /// <param name="iPeriod">Modelling period (values like 1,2,...n)</param>
    /// <param name="numInputs">Raw numeric input values for the element. Keys are input names, values are input values</param>
    /// <param name="textInputs">Raw text input values for the element. Keys are input names, values are input values</param>
    /// <param name="currentNumModParamValues">Values for Numeric Model Parameters as they were in the previous epoch. Keys are parameter names</param>
    /// <param name="currentTextModParamValues">Values for Text Model Parameters as they were at the previous epoch. Keys are parameter names</param>
    /// <param name="numModParamValues">Return value: Sink holding values for numeric parameters (to be updated by Domain Model). Keys are parameter names, values are assigned values</param>
    /// <param name="textModParamValues">Return value: Sink holding values for text parameters (to be updated by Domain Model). Keys are parameter names, values are assigned values</param>        
    public override void Increment(int iElemIndex, int iPeriod, Dictionary<string, double> numInputs, Dictionary<string, string> textInputs,
        Dictionary<string, double> currentNumModParamValues, Dictionary<string, string> currentTextModParamValues,
        Action<string, double> numModParamValues, Action<string, string> textModParamValues)
    {
        try
        {            
            Dictionary<string, object> infoFromModel = model.GetSpecialPlaceholderValues(iElemIndex, iPeriod, null);

            RoadSegmentMC segment = RoadSegmentFactoryMC.GetFromModel(this.model, numInputs, textInputs, currentNumModParamValues, currentTextModParamValues, iElemIndex, iPeriod);
            
            // Apply increments here
            RoadSegmentMC incrementedSegment = _incrementer.Increment(segment, iPeriod);
         
            incrementedSegment.SetParameterValues(numModParamValues, textModParamValues, this.Constants, infoFromModel, iPeriod);
            
        }
        catch (Exception ex)
        {
            throw new Exception($"Error Incrementing element index {iElemIndex}. Details: {ex.Message}");
        }
    }

    
    /// <summary>
    /// Execute treatment selection/trigger logic to select all treatment instances for an element in the current period. The
    /// framework model will call this method for each element in and for each period. This method is only used in MCDA type models
    /// that evaluate individual treatments instead of Strategies. Use the raw input values for
    /// the element as well as the previous values of the parameters for the element with your domain logic to determine which
    /// treatment(s) can be considered for this element in the optimisation stage. If no treatments are applicable, return an empty list.
    /// </summary>
    /// <param name="iElemIndex">Zero-based index of the element</param>
    /// <param name="iPeriod">Modelling period (values like 1,2,...n)</param>
    /// <param name="numInputs">Raw numeric input values for the element. Keys are input names, values are input values</param>
    /// <param name="textInputs">Raw text input values for the element. Keys are input names, values are input values</param>
    /// <param name="numModParamValues">Values for Numeric Model Parameters as they were in the previous epoch. Keys are parameter names</param>
    /// <param name="textModParamValues">Values for Text Model Parameters as they were at the previous epoch. Keys are parameter names</param>
    /// <returns>A list of all treatment instances to consider for this element in the optimisation stage</returns>
    public override List<TreatmentInstance> GetTreatmentCandidates(int iElemIndex, int iPeriod,
        Dictionary<string, double> numInputs, Dictionary<string, string> textInputs,
        Dictionary<string, double> numModParamValues, Dictionary<string, string> textModParamValues)
    {
        try
        {
            Dictionary<string, object> infoFromModel = model.GetSpecialPlaceholderValues(iElemIndex, iPeriod, null);

            RoadSegmentMC segment = RoadSegmentFactoryMC.GetFromModel(this.model, numInputs, textInputs, numModParamValues, textModParamValues, iElemIndex, iPeriod);

            TreatmentsTrigger mcdaTriggerFunction = new TreatmentsTrigger(this.model, this);
            List<TreatmentInstance> candidates = mcdaTriggerFunction.GetTriggeredTreatments(segment, iPeriod, infoFromModel);

            return candidates;            
        }
        catch (Exception ex)
        {
            throw new Exception($"Error checking Treatment Candidate Selection on element index {iElemIndex}. Details: {ex.Message}");
        }
    }

    /// <summary>
    /// Uses domain logic to determine if there is routine maintenance triggered for the current element and period. This method is 
    /// called from the Framework Model after treatment selection to determine if there is any triggered maintenance that should be applied to the element.
    /// If there is no triggered maintenance, return null.
    /// </summary>
    /// <param name="ielem">Zero-based index of the element</param>
    /// <param name="iPeriod">Modelling period (values like 1,2,...n)</param>
    /// <param name="numInputs">Raw numeric input values for the element. Keys are input names, values are input values</param>
    /// <param name="textInputs">Raw text input values for the element. Keys are input names, values are input values</param>
    /// <param name="numModParamValues">Values for Numeric Model Parameters as they were in the previous epoch. Keys are parameter names</param>
    /// <param name="textModParamValues">Values for Text Model Parameters as they were at the previous epoch. Keys are parameter names</param>
    /// <returns>A Treatment Instance object representing Routine Maintenance</returns>
    public override TreatmentInstance GetTriggeredMaintenance(int iElemIndex, int iPeriod,
        Dictionary<string, double> numInputs, Dictionary<string, string> textInputs,
        Dictionary<string, double> numModParamValues, Dictionary<string, string> textModParamValues)
    {
        try
        {
            //Dictionary<string, object> infoFromModel = model.GetSpecialPlaceholderValues(iElemIndex, iPeriod, null);
            //RoadSegmentMC segment = RoadSegmentMCFactory.GetFromModel(this.model, numInputs, textInputs, numModParamValues, textModParamValues, iElemIndex, iPeriod);
            //segment.UpdateFormulaValues(this.model, this, iPeriod, infoFromModel);  //Immediately update the formula values for the segment

            //return RoutineMaintenance.GetRoutineMaintenance(segment, iPeriod);
            // Base signature is non-nullable, but framework null-checks; suppress with null-forgiving.
            return null!;

        }
        catch (Exception ex)
        {
            throw new Exception($"Error triggering Routine Maintenance on element index {iElemIndex}. Details: {ex.Message}");
        }
    }

    /// <summary>
    /// Stub for the Domain Model that can be used to perform any end of period calculations after the treatment selection and parameter updates have 
    /// been performed for all elements in the current period. This can be used to calculate any additional parameters that are needed for the next period 
    /// or for reporting purposes, using the updated parameter values after treatment application. This method is called from the Framework Model at the end of 
    /// each period, after all elements have been processed for the current period. You can use this to do things such as calculating network level rankings, statistics,
    /// proportions over/under etc. that can be used to drive decisions in the next period. Implementers should store calculated values in the Domain Model object. Take
    /// care on how you index or store results. Inless you index by period, your values will be replaced/recycled at the end of each period.
    /// </summary>    
    /// <param name="iPeriod">Modelling period (values like 1,2,...n)</param>
    public override void DoEndOfPeriodCalculations(int iPeriod)
    {
        //Nothing to do here in the default model, but you can use this to do things such as calculating network level rankings, statistics,
    }

}
