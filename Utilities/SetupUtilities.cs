using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using JCass_Core.JFunctions;
using JCass_Core.Statistics;
using JCass_Data.Objects;
using JCass_Data.Utils;
using JCass_ModelCore.MonteCarlo;
using MonteCarloRoadModelV2.DomainObjects;

namespace MonteCarloRoadModelV2.Utilities;

public static class SetupUtilities
{

    /// <summary>
    /// Helper function to setup the increment residual SD functions for the MonteCarloRoadModelV2 domain model. This reads in the setup codes from a CSV file and 
    /// creates the PieceWiseLinearModel instances for each of the three parameters (rut, IRI, texture). The setup codes in the
    /// CSV file should be in the format expected by the PieceWiseLinearModel constructor. The created models are then assigned to the domain model instance.
    /// </summary>
    /// <param name="domainModel">Monte Carlo DomainModel</param>
    /// <param name="workFolder">workfolder to find the path to setup CSV files</param>
    /// <exception cref="Exception"></exception>
    public static void SetupIncrementResidualModels(DomainObjects.MonteCarloRoadModelV2 domainModel, string workFolder)
    {
        string incrementResidualSDSetupFile = System.IO.Path.Combine(workFolder, @"domain_model/inc_resids_plm_setup_codes.csv");
        if (!System.IO.File.Exists(incrementResidualSDSetupFile))
        {
            throw new Exception($"Increment Residual SD setup file not found at: {incrementResidualSDSetupFile}");
        }
        jcDataSet allSetupData = CSVHelper.ReadDataFromCsvFile(incrementResidualSDSetupFile);
        allSetupData.SetupRowKeys("parameter");

        domainModel.SubModels.RutIncrementResidualSDFunction = GetPieceWiseLinearModel("rut_inc_resid", allSetupData);
        domainModel.SubModels.IRIIncrementResidualSDFunction = GetPieceWiseLinearModel("iri_inc_resid", allSetupData);
        domainModel.SubModels.TextureIncrementResidualSDFunction = GetPieceWiseLinearModel("text_inc_resid", allSetupData);
               

    }

    /// <summary>
    /// Setup helper function to setup the distribution simulators for the MonteCarloRoadModelV2 domain model. This reads in the setup data 
    /// from a CSV file and creates DistributionSimulator instances for each of the three parameters (rut, IRI, texture). The created 
    /// simulators are then assigned to the domain model instance.
    /// </summary>
    /// <param name="domainModel">Monte Carlo domain model</param>
    /// <param name="workFolder">workfolder to find the path to setup CSV files</param>
    /// <param name="random">Random number generator</param>
    /// <exception cref="Exception"></exception>
    public static void SetupDistributionSimulators(DomainObjects.MonteCarloRoadModelV2 domainModel, string workFolder, Random random)
    {
        //------------------------------------  Set up distribution simulators for increments------------------------------------

        string distributionSetupFile = System.IO.Path.Combine(workFolder, @"domain_model/cohorts_b_increments_6yrs_rut_mean_rate.csv");       
        domainModel.SubModels.RutIncrementSimulator = SetupUtilities.GetDistributionSimulator("rut_inc", distributionSetupFile, "Rut Increment");

        distributionSetupFile = System.IO.Path.Combine(workFolder, @"domain_model/cohorts_b_increments_6yrs_iri_mean_rate.csv");
        domainModel.SubModels.IRIIncrementSimulator = SetupUtilities.GetDistributionSimulator("iri_inc", distributionSetupFile, "IRI Increment");

        distributionSetupFile = System.IO.Path.Combine(workFolder, @"domain_model/cohorts_b_increments_3yrs_text_mean_rate.csv");
        domainModel.SubModels.TextureIncrementSimulator = SetupUtilities.GetDistributionSimulator("text_inc", distributionSetupFile, "Texture Increment");
        

        //------------------------------------  Set up distribution simulators for Maintenance Extent (PA and Potfill) ------------------------------------

        // First all PA excluding maintenance
        distributionSetupFile = System.IO.Path.Combine(workFolder, @"domain_model/cohorts_d_maint_model_data_post_potfill_mtc_extent.csv");               
        domainModel.SubModels.MaintenanceExtentPA = SetupUtilities.GetDistributionSimulator("post_all_mtc_extent", distributionSetupFile, "Maintenance Extent PA");
        
        // Then potfill extent
        distributionSetupFile = System.IO.Path.Combine(workFolder, @"domain_model/cohorts_d_maint_model_data_post_potfill_mtc_extent.csv");        
        domainModel.SubModels.MaintenanceExtentPotfill = SetupUtilities.GetDistributionSimulator("post_potfill_mtc_extent_ac_ogpa", distributionSetupFile, "Maintenance Extent Potfill");        
    }

    public static PieceWiseLinearModel GetPieceWiseLinearModel(string parameterName, jcDataSet allSetups)
    {
        Dictionary<string, object> row = allSetups.Row(parameterName);
        string pwlSetupString = row["plm_setup_code"]?.ToString()
            ?? throw new InvalidDataException($"Missing 'plm_setup_code' for parameter '{parameterName}'");
        PieceWiseLinearModel model = new PieceWiseLinearModel(pwlSetupString, false); //Do not extrapolate.
        return model;
    }

    public static DistributionSimulator GetDistributionSimulator(string parameterName, string setupFilePath, string paramLabelForError)
    {
        jcDataSet setupDataForParameter = CSVHelper.ReadDataFromCsvFile(setupFilePath);
        if (!System.IO.File.Exists(setupFilePath)) throw new Exception($"Cohort Distribution setup file for {paramLabelForError} not found at: {System.IO.Path.GetFileName(setupFilePath)}");
        jcDataSet setupData = CSVHelper.ReadDataFromCsvFile(setupFilePath);
        DistributionSimulator simulator = new DistributionSimulator(parameterName, setupDataForParameter);
        return simulator;
    }


    public static void SetupProbabilityModels(DomainObjects.MonteCarloRoadModelV2 domainModel, string workFolder)
    {
        //Set up Logistic prediction model for potfill probability for AC OGPA
        string coefsFile = Path.Combine(workFolder, @"domain_model/logistic_potfill_ac_ogpa.csv");
        if (!File.Exists(coefsFile)) throw new Exception($"Coefficient file for Logistic model not found at: {Path.GetFileName(coefsFile)}");        
        jcDataSet d1 = CSVHelper.ReadDataFromCsvFile(coefsFile);
        Dictionary<string, double> coefs1 = GetLogisticModelCoefficients(d1);
        domainModel.SubModels.PotfillProbabilityModelAC = new LogisticModel(coefs1);

        //Set up Logistic prediction model for potfill probability for CS slurry
        coefsFile = Path.Combine(workFolder, @"domain_model/logistic_potfill_cs_slurry.csv");
        if (!File.Exists(coefsFile)) throw new Exception($"Coefficient file for Logistic model not found at: {Path.GetFileName(coefsFile)}");
        jcDataSet d2 = CSVHelper.ReadDataFromCsvFile(coefsFile);
        Dictionary<string, double> coefs2 = GetLogisticModelCoefficients(d2);
        domainModel.SubModels.PotfillProbabilityModelCS = new LogisticModel(coefs2);

        //Set up Logistic prediction model for maintenance PA probability for CS slurry
        coefsFile = Path.Combine(workFolder, @"domain_model/logistic_pa_maint_cs_slurry.csv");
        if (!File.Exists(coefsFile)) throw new Exception($"Coefficient file for Logistic model not found at: {Path.GetFileName(coefsFile)}");
        jcDataSet d3 = CSVHelper.ReadDataFromCsvFile(coefsFile);
        Dictionary<string, double> coefs3 = GetLogisticModelCoefficients(d3);
        domainModel.SubModels.MaintPaProbabilityModelCS = new LogisticModel(coefs3);

        //Set up Logistic prediction model for maintenance PA probability for AC OGPA
        coefsFile = Path.Combine(workFolder, @"domain_model/logistic_pa_maint_ac_ogpa.csv");
        if (!File.Exists(coefsFile)) throw new Exception($"Coefficient file for Logistic model not found at: {Path.GetFileName(coefsFile)}");
        jcDataSet d4 = CSVHelper.ReadDataFromCsvFile(coefsFile);
        Dictionary<string, double> coefs4 = GetLogisticModelCoefficients(d4);
        domainModel.SubModels.MaintPaProbabilityModelAC = new LogisticModel(coefs4);


        //Set up Logistic prediction model for maintenance SU probability for CS slurry
        coefsFile = Path.Combine(workFolder, @"domain_model/logistic_su_maint_cs_slurry.csv");
        if (!File.Exists(coefsFile)) throw new Exception($"Coefficient file for Logistic model not found at: {Path.GetFileName(coefsFile)}");
        jcDataSet d5 = CSVHelper.ReadDataFromCsvFile(coefsFile);
        Dictionary<string, double> coefs5 = GetLogisticModelCoefficients(d5);
        domainModel.SubModels.MaintSuProbabilityModelCS = new LogisticModel(coefs5);

        //Set up Logistic prediction model for maintenance SU probability for AC OGPA
        coefsFile = Path.Combine(workFolder, @"domain_model/logistic_su_maint_ac_ogpa.csv");
        if (!File.Exists(coefsFile)) throw new Exception($"Coefficient file for Logistic model not found at: {Path.GetFileName(coefsFile)}");
        jcDataSet d6 = CSVHelper.ReadDataFromCsvFile(coefsFile);
        Dictionary<string, double> coefs6 = GetLogisticModelCoefficients(d6);
        domainModel.SubModels.MaintSuProbabilityModelAC = new LogisticModel(coefs6);


    }

    public static void SetupResetModels(DomainObjects.MonteCarloRoadModelV2 domainModel, string workFolder)
    {
        //------------------------------------  Set up distribution simulators for RESETS ------------------------------------

        // Texture Reset simulator
        string distributionSetupFile = Path.Combine(workFolder, @"domain_model/cohorts_c_treatment_resets_text_mean_post.csv");
        domainModel.SubModels.TextureResetSimulator = SetupUtilities.GetDistributionSimulator("text_mean_post", distributionSetupFile, "Texture Reset");

        // IRI Reset simulator - Resurfacing
        distributionSetupFile = Path.Combine(workFolder, @"domain_model/cohorts_c_resurf_resets_iri_mean_post.csv");
        domainModel.SubModels.IRIResetSimulatorResurf = SetupUtilities.GetDistributionSimulator("iri_mean_post", distributionSetupFile, "IRI Reset - Resurfacing");

        // IRI Reset simulator - Rehabilitation
        distributionSetupFile = Path.Combine(workFolder, @"domain_model/cohorts_c_rehab_resets_iri_mean_post.csv");
        domainModel.SubModels.IRIResetSimulatorRehab = SetupUtilities.GetDistributionSimulator("iri_mean_post", distributionSetupFile, "IRI Reset - Rehabilitation");

        // Rut Reset simulator - Resurfacing
        distributionSetupFile = Path.Combine(workFolder, @"domain_model/cohorts_c_resurf_resets_rut_mean_post.csv");
        domainModel.SubModels.RutResetSimulatorResurf = SetupUtilities.GetDistributionSimulator("rut_mean_post", distributionSetupFile, "Rut Reset - Resurfacing");

        // Rut Reset simulator - Rehabilitation
        distributionSetupFile = Path.Combine(workFolder, @"domain_model/cohorts_c_rehab_resets_rut_mean_post.csv");
        domainModel.SubModels.RutResetSimulatorRehab = SetupUtilities.GetDistributionSimulator("rut_mean_post", distributionSetupFile, "Rut Reset - Rehabilitation");

    }

    public static void SetupReductionDueToPaMaintenanceModels(DomainObjects.MonteCarloRoadModelV2 domainModel, string workFolder, Random random)
    {
       
        //-----------------  Set up distribution simulators for REDUCTION in Rut and IRI after PA Maintenance ------------------------------------

        string distributionSetupFile = Path.Combine(workFolder, @"domain_model/cohorts_maint_reduc_data_final_iri_reduction.csv");
        domainModel.SubModels.IRIReductionAfterPaMaintenanceSimulator = SetupUtilities.GetDistributionSimulator("iri_reduction", distributionSetupFile, "IRI Reduction after PA Maintenance");

        distributionSetupFile = Path.Combine(workFolder, @"domain_model/cohorts_maint_reduc_data_final_rut_reduction.csv");
        domainModel.SubModels.RutReductionAfterPaMaintenanceSimulator = SetupUtilities.GetDistributionSimulator("rut_reduction", distributionSetupFile, "Rut Reduction after PA Maintenance");

    }

    public static void SetupTreatmentSuitabilityScoreModels(DomainObjects.MonteCarloRoadModelV2 domainModel)
    {        
        double excessRutThreshold = domainModel.Constants.TSSExcessRutThresh;
                
        string tssModelSetup = $"{domainModel.Constants.TSSRehabRniRank},0|100,100";
        domainModel.SubModels.TSSForRehabilitation = new PieceWiseLinearModel(tssModelSetup, true);  //Previous model did not extrapolate - I think this is better for breaking ties

        tssModelSetup = $"{domainModel.Constants.TSSHoldingRniRankPt1},0|{domainModel.Constants.TSSHoldingRniRankPt2},100 | 100,{domainModel.Constants.TSSHoldingRniRankPt3}";
        domainModel.SubModels.TSSForHoldingAction = new PieceWiseLinearModel(tssModelSetup, true);

    }

    private static jcDataSet GetFilteredDataSet(string parameterName, jcDataSet allSetups)
    {
        jcDataSet setupData = new jcDataSet();
        setupData.Columns = allSetups.Columns;
        for (int i = 0; i < allSetups.Count; i++)
        {
            Dictionary<string, object> row = allSetups.Row(i);
            string setupParameterName = row["parameter_key"]?.ToString()
                ?? throw new InvalidDataException($"Missing 'parameter_key' on row {i}");
            if (setupParameterName == parameterName)
            {
                setupData.AddRow(row);
            }
        }
        return setupData;
    }

    private static Dictionary<string, double> GetLogisticModelCoefficients(jcDataSet coefsData)
    {        
        Dictionary<string, double> coefs = new Dictionary<string, double>();
        for (int i = 0; i < coefsData.Count; i++)
        {
            Dictionary<string, object> row = coefsData.Row(i);
            string variableName = row["term"]?.ToString()
                ?? throw new InvalidDataException($"Missing 'term' on row {i} of logistic coefficients");
            double coefValue = Convert.ToDouble(row["estimate"]);
            coefs[variableName] = coefValue;
        }
        return coefs;
    }

    /// <summary>
    /// Parses variable names and coefficient values from the provided jcDataSet to create a dictionary of coefficients for a linear regression model. 
    /// The jcDataSet is expected to have columns named "variable" and "coefficient", where "variable" contains the name of the predictor variable and "coefficient" contains the corresponding coefficient value. 
    /// The resulting dictionary maps variable names to their coefficient values, which can then be used to construct a linear regression model. Intercept should be "(Intercept)" in the variable column of the dataset.
    /// If there is a variable named 'resid_sd_plm' this is presumed to be NOT a variable in the regression but rather the setup code for a PieceWiseLinearModel that models the residual standard deviation of the 
    /// regression, and this setup code is returned as a separate string along with the coefficients dictionary.
    /// </summary>
    /// <param name="coefsData"></param>
    /// <returns></returns>
    private static (Dictionary<string, double> coefficients, string? residualSDPlmSetup) GetLinearRegressionModelCoefficients(jcDataSet coefsData)
    {
        string? residSDPlmSetup = null;
        Dictionary<string, double> coefs = new Dictionary<string, double>();
        for (int i = 0; i < coefsData.Count; i++)
        {
            Dictionary<string, object> row = coefsData.Row(i);
            string variableName = row["variable"]?.ToString()
                ?? throw new InvalidDataException($"Missing 'variable' on row {i} of linear regression coefficients");
            if (variableName == "resid_sd_plm")
            {
                // This is not a variable but rather the setup code for the residual SD PLM
                residSDPlmSetup = row["coefficient"]?.ToString();
            }
            else
            {
                double coefValue = Convert.ToDouble(row["coefficient"]);
                coefs[variableName] = coefValue;
            }
        }
        return (coefs, residSDPlmSetup);
    }
}
