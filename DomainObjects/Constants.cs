
using System.Runtime.CompilerServices;
using DocumentFormat.OpenXml.Wordprocessing;
using JCass_Core.Statistics;
using JCass_Economics.Utilities;

namespace MonteCarloRoadModelV2.DomainObjects;

/// <summary>
/// General model constants set up from model Lookup sets.
/// </summary>
public class Constants
{

    #region Backing Variables

    // General
    private DateTime _baseDate;
    private int _minimiseStochasticEffectsPeriod;
    
    // Related to Candidate Selection
    private double _minSlaToTreatAc;
    private double _minSlaToTreatCs;    
    private double _min_periods_to_next_treat;    
    

    // Related to Treatment Suitability Score (TSS). Used in MCDA model
    private double _rehabMinRniRank;
    private double _holdingRniRankPt1;
    private double _holdingRniRankPt2;
    private double _holdingRniRankPt3;    
    private List<string> _holdingActionExclusionStates;

    // Related to Rehabilitation Needs Index
    private int _histMaintUsePeriods;
    private double _rutThresholdForRehabs;
    private double _unstableSealCount;
    private double _unstableRutThreshold;
    private double _minLengthRehab;
    private double _maxLengthRehab;

    // Related to Surfacing Needs Index but also used for TSS
    private double _csMaxSealCount;
    private double _csTextureThreshold;
    private double _csTextureFactor;
    private double _flushingFactor;
    private double _minSlaToResurfaceCs;
    private double _minSlaToResurfaceAc;    
    private double _maxRutForPreservationCS;
    private double _maxRutForPreservationAC;
    private double _skidResistanceMaxPeriod;
    private double _minLengthChipseal;
    private double _maxLengthChipseal;
    private double _minLengthACorOGPA;
    private double _maxLengthACorOGPA;

    // Related to MCDA Treatment Triggering
    private double _maxSlaForACHeavyMaint;
    private int _minPeriodsBetweenACHeavyMaint;


    // Episode Lengths for deterioration rates
    private int _episodeLengthRutAndIRI;
    private int _episodeLengthTexture;

    //public int DebugLogCounter = 0;

    #endregion

    #region General Constants

    /// <summary>
    /// Base date for the model run. Maps to lookup set "gernal" and setting key "base_date".
    /// </summary>
    public DateTime BaseDate { get { return _baseDate; } }

    // Number of periods at the start of the model run to apply a more deterministic deterioration to reduce stochastic effects at
    // the start of the model run when all roads are in perfect condition. Maps to lookup set "general" and setting key "minimise_stochastic_effects_period".
    // Currently, this affects only resets. If the reset is applied when the period is less than or equal to this value, then the reset is taken
    // as the mean of 5 draws instead of a single draw (which could assign a really bad reset causing another treatment to be triggered within the 
    // short term FWP period
    public int MinimiseStochasticEffectsPeriod
    {
        get { return _minimiseStochasticEffectsPeriod; }
    }

    #endregion

    #region Candidate Selection related constants


    /// <summary>
    /// Minimum Surface Life Achieved to consider for AC - gatekeeper that can be used to throttle treatments
    /// </summary>
    public double CSMinSlaToTreatAc
    {
        get { return _minSlaToTreatAc; }
    }

    /// <summary>
    /// Minimum periods to next treatment (i.e. do not consider treatment if periods to a committed future treatment is less than this)
    /// </summary>
    public double CSMinPeriodsToNextTreat
    {
        get { return _min_periods_to_next_treat; }
    }

    /// <summary>
    /// Minimum Surface Life Achieved to consider for Chipseals - gatekeeper that can be used to throttle treatments
    /// </summary>
    public double CSMinSlaToTreatCs
    {
        get { return _minSlaToTreatCs; }
    }

    #endregion

    #region Rehabilitation Needs Index related constants

    /// <summary>
    /// Minimim length, in metres, below which rehabilitation treatments are not considered
    /// </summary>
    public double MinimumLengthForRehab
    {
        get { return _minLengthRehab; }
    }

    /// <summary>
    /// Maximum length, in metres, above which rehabilitation treatments are not considered
    /// </summary>
    public double MaximumLengthForRehab
    {
        get { return _maxLengthRehab; }
    }

    /// <summary>
    /// Number of periods over which historical maintenance information in Input Set should be used in the
    /// calculation of the Rehabilitation Needs Index. 
    /// </summary>
    public int HistoricalMaintenanceUsePeriods
    {                
        get { return _histMaintUsePeriods; }
    }

    /// <summary>
    /// Rut threshold above which Rehabilitation is considered. 
    /// </summary>
    public double RutThresholdForRehabs
    {
        get { return _rutThresholdForRehabs; }
    }

    /// <summary>
    /// Rut threshold above which Surfacing is regarded as unstable IF the surface is a ChipSeal and the
    /// number of seal layers is at or above the UnstableSealCount threshold. 
    /// </summary>
    public double UnstableRutThreshold
    {
        get { return _unstableRutThreshold; }
    }

    /// <summary>
    /// Number of seal layers at or above which a ChipSeal surface is regarded as unstable if the Rut is at or above the UnstableRutThreshold.
    /// </summary>
    public double UnstableSealCount
    {
        get { return _unstableSealCount; }
    }

    #endregion

    #region Surfacing Needs Index related constants


    /// <summary>
    /// Minimum length, in metres, at which a Preservation Chipseal can be considered. Below this, SNI is zero
    /// </summary>
    public double MinimumLengthForChipseal
    {
        get { return _minLengthChipseal; }
    }

    /// <summary>
    /// Maximum length, in metres, at which a Preservation Chipseal can be considered. Above this, SNI is zero. 
    /// Set this to a high value to effectively have no maximum.
    /// </summary>
    public double MaximumLengthForChipseal
    {
        get { return _maxLengthChipseal; }
    }

    /// <summary>
    /// Minimum length, in metres, at which a Preservation AC or OGPA can be considered. Below this, SNI is zero.
    /// </summary>
    public double MinimumLengthForACorOGPA
    {
        get { return _minLengthACorOGPA; }
    }

    /// <summary>
    /// Maximum length, in metres, at which a Preservation AC or OGPA can be considered. Above this, SNI is zero.
    /// </summary>
    public double MaximumLengthForACorOGPA
    {
        get { return _maxLengthACorOGPA; }
    }



    /// <summary>
    /// Maximum number of previous seals for a ChipSeal treatment to be considered (stability concern if too many seals already). 
    /// Set this to a high value to effectively have no maximum.
    /// </summary>
    public double MaxSealCountForChipSeal
    {
        get { return _csMaxSealCount; }
    }

    /// <summary>
    /// Texture threshold below which Surface Needs index for a Chipseal is increased since it points to flushing risk
    /// </summary>
    public double TextureThresholdForChipSeal
    {
        get { return _csTextureThreshold; }
    }

    /// <summary>
    /// Multiplier for Surface Needs Index for a Chipseal when Texture is below the threshold, to account for increased risk of flushing. 
    /// Only has an influence if texture if below the TextureThresholdForChipSeal threshold. Set this to zero to have no increase in Surface Needs Index due to low texture. 
    /// </summary>
    public double TexturePenaltyFactorForChipSeal
    {
        get { return _csTextureFactor; }
    }

    /// <summary>
    /// Multiplier to get penalty for flushing. This will be multiplied by the state score (0 to 6). Only applied to Chipseals. 
    /// Set this to zero to have no penalty for flushing.
    /// </summary>
    public double FlushingPenaltyFactor
    {
        get { return _flushingFactor; }
    }

    /// <summary>
    /// Maximum number of periods for which Skid Resistance Residual Value needs to be taken into account. Since we are not modelling
    /// skid resistance, we use it only for a certain number of years at the start for short term FWP development.
    /// </summary>
    public double SkidResistanceMaxPeriod
    {
        get { return _skidResistanceMaxPeriod; }
    }

    /// <summary>
    /// Minimum Surface Life Achieved to consider for resurfacing treatments on Chipseals. Below this value the 
    /// Surfacing Needs Index is zero for Chipseals.
    /// </summary>
    public double MinSlaToResurfaceCs
    {
        get { return _minSlaToResurfaceCs; }
    }


    /// <summary>
    /// Minimum Surface Life Achieved to consider for resurfacing treatments on AC and OGPA. Below this value the
    /// Surfacing Needs Index is zero for AC and OGPA. 
    /// </summary>
    public double MinSlaToResurfaceACandOGPA
    {
        get { return _minSlaToResurfaceAc; }
    }

    
    /// <summary>
    /// Maximum rut depth for Preservation AC treatment to be considered
    /// </summary>
    public double MaxRutForPreservationAC
    {
        get { return _maxRutForPreservationAC; }
    }

    /// <summary>
    /// Maximum rut depth for Preservation ChipSeal treatment to be considered
    /// </summary>
    public double MaxRutForPreservationCS
    {
        get { return _maxRutForPreservationCS; }
    }

    #endregion

    #region Treatment Selection MCDA

           
    /// <summary>
    /// RNI rank below which TSS score for Rehab becomes zero (i.e. no rehab if RNI is below this value)
    /// </summary>
    public double TSSRehabRniRank
    {
        get { return _rehabMinRniRank; }
    }

    /// <summary>
    /// RNI rank below which TSS score for Holding Action becomes zero (i.e. no holding action if RNI is below this value)
    /// </summary>
    public double TSSHoldingRniRankPt1  
    {
        get { return _holdingRniRankPt1; }
    }


    /// <summary>
    /// RNI rank at which score for holding action is maximal(100)
    /// </summary>
    public double TSSHoldingRniRankPt2
    {
        get { return _holdingRniRankPt2; }
    }
    
    /// <summary>
    /// TSS for holding action based on RNI when RNI rank is 100
    /// </summary>
    public double TSSHoldingRniRankPt3
    {
        get { return _holdingRniRankPt3; }
    }
        
    /// <summary>
    /// Do not consider Holding AC treatment if PDI is above this value 
    /// </summary>
    public List<string> TSSHoldingExclusionStates
    {
        get { return _holdingActionExclusionStates; }
    }

    /// <summary>
    /// Maximum Surface Life Achieved % to consider AC Heavy Maintenance (i.e. do not consider AC Heavy Maintenance if SLA is above this value)
    /// </summary>
    public double MaxSlaForACHeavyMaint
    {
        get { return _maxSlaForACHeavyMaint; }
    }

    /// <summary>
    /// Minimum number of periods between AC Heavy Maintenance treatment and any other previous treatment (excluding Routine Maintenance)
    /// </summary>
    public int MinPeriodsBetweenACHeavyMaint
    {
        get { return _minPeriodsBetweenACHeavyMaint; }
    }

    /// <summary>
    /// Maximum episode length for assigned deterioration rate for Rut and IRI. If last draw of random rate is longer than this ago, then draw new
    /// </summary>
    public int MaximumEpisodeLengthRutAndIRI
    {
        get { return _episodeLengthRutAndIRI; }
    }

    /// <summary>
    /// Maximum episode length for assigned deterioration rate for Texture Depth. If last draw of random rate is longer than this ago, then draw new
    /// </summary>
    public int MaximumEpisodeLengthTexture
    {
        get { return _episodeLengthTexture; }
    }


    #endregion

    #region Calibration Factors

    // --- Residuals ---

    private double _calFactRutResiduals;
    private double _calFactIriResiduals;
    private double _calFactTextureResiduals;

    // --- Maintenance ---

    private double _calFactPaProba;
    private double _calFactSuProba;
    private double _calFactPotfillProba;
    private double _calFactPaExtent;
    private double _calFactSuExtent;
    private double _calFactPotfillExtent;
    private double _calFactRutReduc;
    private double _calFactIriReduc;
    private double _calMinRutReducPaMaint;
    private double _calMinIRIReducPaMaint;

    // --- Increments ---

    private Dictionary<string, double> _incremAdjustmentFactorsIRI;
    private Dictionary<string, double> _incremAdjustmentFactorsRut;
    private Dictionary<string, double> _incremAdjustmentFactorsTexture;

    // --- Resets ---

    private double _calFactRutReset;
    private double _calFactIriReset;
    private double _calFactTextureReset;

    private Dictionary<string, double> _resetAdjustmentFactorsIRI;
    private Dictionary<string, double> _resetAdjustmentFactorsRut;
    private Dictionary<string, double> _resetAdjustmentFactorsTexture;


    // ---- Residual calibration properties ----

    /// <summary>
    /// Calibration factor for Rut residuals. Residual is multiplied by this factor. Set to zero to remove residual effects completely.
    /// Lookup set: cal_residuals, key: rut.
    /// </summary>
    public double CalFactRutResiduals
    {
        get { return _calFactRutResiduals; }
    }

    /// <summary>
    /// Calibration factor for IRI residuals. Residual is multiplied by this factor. Set to zero to remove residual effects completely.
    /// Lookup set: cal_residuals, key: iri.
    /// </summary>
    public double CalFactIriResiduals
    {
        get { return _calFactIriResiduals; }
    }

    /// <summary>
    /// Calibration factor for Texture residuals. Residual is multiplied by this factor. Set to zero to remove residual effects completely.
    /// Lookup set: cal_residuals, key: texture.
    /// </summary>
    public double CalFactTextureResiduals
    {
        get { return _calFactTextureResiduals; }
    }

    // ---- Maintenance calibration properties ----

    /// <summary>
    /// Calibration factor for probability of PA maintenance (excluding Pothole filling). Decreasing this decreases probability of PA maintenance.
    /// Lookup set: cal_maintenance, key: pa_proba.
    /// </summary>
    public double CalFactPaMaintenanceProbability
    {
        get { return _calFactPaProba; }
    }

    /// <summary>
    /// Calibration factor for probability of SU maintenance (excluding Pothole filling). Decreasing this decreases probability of SU maintenance.
    /// Lookup set: cal_maintenance, key: su_proba.
    /// </summary>
    public double CalFactSuMaintenanceProbability
    {
        get { return _calFactSuProba; }
    }

    /// <summary>
    /// Calibration factor for probability of Pothole Filling maintenance. Decreasing this decreases probability of Pothole Filling.
    /// Lookup set: cal_maintenance, key: potfill_proba.
    /// </summary>
    public double CalFactPotfillProbability
    {
        get { return _calFactPotfillProba; }
    }

    /// <summary>
    /// Calibration factor for extent of PA Maintenance. Sampled extent is multiplied by this factor to reduce or increase.
    /// Lookup set: cal_maintenance, key: pa_extent.
    /// </summary>
    public double CalFactPaMaintenanceExtent
    {
        get { return _calFactPaExtent; }
    }

    /// <summary>
    /// Calibration factor for extent of PA Maintenance. Sampled extent is multiplied by this factor to reduce or increase.
    /// Lookup set: cal_maintenance, key: su_extent.
    /// </summary>
    public double CalFactSuMaintenanceExtent
    {
        get { return _calFactSuExtent; }
    }

    /// <summary>
    /// Calibration factor for extent of Pothole filling. Sampled extent is multiplied by this factor to reduce or increase.
    /// Lookup set: cal_maintenance, key: potfill_extent.
    /// </summary>
    public double CalFactPotfillMaintenanceExtent
    {
        get { return _calFactPotfillExtent; }
    }

    /// <summary>
    /// Extent of PA maintenance at which Rut-reduction to to maintenance kicks in. Range should be 0 to 1. Value of greater than one 
    /// effectively removes rut reduction due to PA Maint.
    /// Lookup set: cal_maintenance, key: rut_reduc.
    /// </summary>
    public double RutReductionDueToPAMaintenanceThreshold
    {
        get { return _calFactRutReduc; }
    }

    /// <summary>
    /// Extent of PA maintenance at which IRI-reduction to to maintenance kicks in. Range should be 0 to 1. Value of greater than one 
    /// Lookup set: cal_maintenance, key: iri_reduc.
    /// </summary>
    public double IriReductionDueToPAMaintenanceThreshold
    {
        get { return _calFactIriReduc; }
    }

    /// <summary>
    /// Minimum Rut reduction due to PA Maintenance allowed (in mm). Set this to a low negative to prevent PA maint from making Rut much worse. For
    /// example, since negative reduction means rut gets worse, setting this to -2 means that if sampled reduction is -3mm, it will be 
    /// calibrated to -2mm (i.e. only 2mm worse instead of 3mm worse). Set this to zero or a positive value to prevent PA maintenance 
    /// from making Rut worse at all.
    /// </summary>
    public double CalFactMinRutReductionDueToPAMaintenance
    {
        get { return _calMinRutReducPaMaint; }
    }


    /// <summary>
    /// Minimum IRI reduction due to PA Maintenance allowed (in mm/m). Set this to a low negative to prevent PA maint from making IRI much worse. For
    /// example, since negative reduction means IRI gets worse, setting this to -0.5 mm/m means that if sampled reduction is -1.50 mm/m, it will be
    /// calibrated to -0.5 mm/m (i.e. only 0.5 mm/m worse instead of 1.5 mm/m worse). Set this to zero or a positive value to prevent PA maintenance
    /// from making IRI worse at all.
    /// </summary>
    public double CalFactMinIriReductionDueToPAMaintenance
    {
        get { return _calMinIRIReducPaMaint; }
    }

    // ---- Increment calibration properties ----


    /// <summary>
    /// Increment Adjustment factors for IRI deterioration increments based on Surface Class. This value is added to the deterioration increment 
    /// </summary>
    public Dictionary<string, double> IncremAdjustmentFactorsIRI
    {
        get { return _incremAdjustmentFactorsIRI; }
    }

    /// <summary>
    /// increment Adjustment factors for Rut deterioration increments based on Surface Class. This value is added to the deterioration increment 
    /// </summary>
    public Dictionary<string, double> IncremAdjustmentFactorsRut
    {
        get { return _incremAdjustmentFactorsRut; }
    }

    /// <summary>
    /// Increment Adjustment factors for Texture deterioration increments based on Surface Class. This value is added to the deterioration increment
    /// </summary>
    public Dictionary<string, double> IncremAdjustmentFactorsTexture
    {
        get { return _incremAdjustmentFactorsTexture; }
    }

    // ---- Reset calibration properties ----

    /// <summary>
    /// Calibration factor for Rut reset. Sampled reset value is multiplied by this factor to increase or decrease reset value.
    /// Lookup set: cal_resets, key: rut.
    /// </summary>
    public double CalFactRutReset
    {
        get { return _calFactRutReset; }
    }

    /// <summary>
    /// Calibration factor for IRI reset. Sampled reset value is multiplied by this factor to increase or decrease reset value.
    /// Lookup set: cal_resets, key: iri.
    /// </summary>
    public double CalFactIriReset
    {
        get { return _calFactIriReset; }
    }

    /// <summary>
    /// Calibration factor for Texture reset. Sampled reset value is multiplied by this factor to increase or decrease reset value.
    /// Lookup set: cal_resets, key: texture.
    /// </summary>
    public double CalFactTextureReset
    {
        get { return _calFactTextureReset; }
    }

    /// <summary>
    /// Adjustment factors for IRI resets based on Treatment Type. This value is added to the reset value after multiplying by the CalFactIriReset factor.
    /// Lookup set: cal_reset_adj_iri, key: with sub-keys for each treatment type.
    /// </summary>
    public Dictionary<string, double> ResetAdjustmentFactorsIRI
    {
        get { return _resetAdjustmentFactorsIRI; }
    }

    /// <summary>
    /// Adjustment factors for Rut resets based on Treatment Type. This value is added to the reset value after multiplying by the CalFactRutReset factor.
    /// Lookup set: cal_reset_adj_rut, key: with sub-keys for each treatment type.
    /// </summary>
    public Dictionary<string, double> ResetAdjustmentFactorsRut
    {
        get { return _resetAdjustmentFactorsRut; }
    }
    
    /// <summary>
    /// Adjustment factors for Texture resets based on Treatment Type. This value is added to the reset value after multiplying by the CalFactTextureReset factor.
    /// Lookup set: cal_reset_adj_text, key: with sub-keys for each treatment type.
    /// </summary>
    public Dictionary<string, double> ResetAdjustmentFactorsTexture
    {
        get { return _resetAdjustmentFactorsTexture; }
    }

    #endregion


    public Constants(Dictionary<string, Dictionary<string, object>> lookupSets)
    {

        // General constants
        string baseDateStr = (string)lookupSets["general"]["base_date"];
        _baseDate = JCass_Core.Utils.HelperMethods.ParseISODateNoTime(baseDateStr);
        _minimiseStochasticEffectsPeriod = Convert.ToInt32(lookupSets["general"]["minimise_stochastic_effects_period"]);

        // Candidate Selection related constants
        _min_periods_to_next_treat = Convert.ToInt32(lookupSets["candidate_selection"]["min_periods_to_next_treat"]);        
        _minSlaToTreatAc = Convert.ToDouble(lookupSets["candidate_selection"]["min_sla_to_treat_ac"]);
        _minSlaToTreatCs = Convert.ToDouble(lookupSets["candidate_selection"]["min_sla_to_treat_cs"]);
        
        // Rehabilitation Needs Index related constants
        _histMaintUsePeriods = Convert.ToInt32(lookupSets["rehab_needs_index"]["use_hist_maint_periods"]);
        _minLengthRehab = Convert.ToDouble(lookupSets["rehab_needs_index"]["min_length_rehab"]);        
        _maxLengthRehab = Convert.ToDouble(lookupSets["rehab_needs_index"]["max_length_rehab"]);
        _rutThresholdForRehabs = Convert.ToDouble(lookupSets["rehab_needs_index"]["excess_rut_threshold"]);
        _unstableRutThreshold = Convert.ToDouble(lookupSets["rehab_needs_index"]["unstable_rut_threshold"]);
        _unstableSealCount = Convert.ToDouble(lookupSets["rehab_needs_index"]["unstable_seal_count"]);

        // Surfacing Needs Index related constants (some of these also influence TSS)
        _csMaxSealCount = Convert.ToInt32(lookupSets["surfacing_needs_index"]["cs_max_seal_count"]);
        _csTextureThreshold = Convert.ToDouble(lookupSets["surfacing_needs_index"]["cs_texture_threshold"]);
        _csTextureFactor = Convert.ToDouble(lookupSets["surfacing_needs_index"]["cs_texture_penalty_factor"]);
        _flushingFactor = Convert.ToDouble(lookupSets["surfacing_needs_index"]["flushing_penalty_factor"]);
        _minSlaToResurfaceCs = Convert.ToDouble(lookupSets["surfacing_needs_index"]["min_sla_to_resurface_cs"]);
        _minSlaToResurfaceAc = Convert.ToDouble(lookupSets["surfacing_needs_index"]["min_sla_to_resurface_ac"]);        
        _maxRutForPreservationAC = Convert.ToDouble(lookupSets["surfacing_needs_index"]["max_rut_ac"]);
        _maxRutForPreservationCS = Convert.ToDouble(lookupSets["surfacing_needs_index"]["max_rut_cs"]);
        _skidResistanceMaxPeriod = Convert.ToDouble(lookupSets["surfacing_needs_index"]["skid_resistance_max_period"]);
        _minLengthChipseal = Convert.ToDouble(lookupSets["surfacing_needs_index"]["min_length_chipseal"]);
        _maxLengthChipseal = Convert.ToDouble(lookupSets["surfacing_needs_index"]["max_length_chipseal"]);
        _minLengthACorOGPA = Convert.ToDouble(lookupSets["surfacing_needs_index"]["min_length_ac_or_ogpa"]);
        _maxLengthACorOGPA = Convert.ToDouble(lookupSets["surfacing_needs_index"]["max_length_ac_or_ogpa"]);

        // Related to TSS
        _rehabMinRniRank = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["rehab_min_rni_rank"]);
        _holdingRniRankPt1 = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_rni_rank_pt1"]);
        _holdingRniRankPt2 = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_rni_rank_pt2"]);
        _holdingRniRankPt3 = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_rni_rank_pt3"]);
        _holdingActionExclusionStates = ((string)lookupSets["treatment_suitability_scores"]["holding_exclusion_states"]).Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
                               
        // Related to MCDA Treatment Triggering
        _maxSlaForACHeavyMaint = Convert.ToDouble(lookupSets["mcda_treatment_triggering"]["ac_hmaint_maximum_sla"]);
        _minPeriodsBetweenACHeavyMaint = Convert.ToInt32(lookupSets["mcda_treatment_triggering"]["ac_hmaint_min_periods_between"]);

        _episodeLengthRutAndIRI = Convert.ToInt32(lookupSets["episode_length_max"]["rut_and_iri"]);
        _episodeLengthTexture = Convert.ToInt32(lookupSets["episode_length_max"]["texture"]);

        // Calibration factors - Residuals
        _calFactRutResiduals = Convert.ToDouble(lookupSets["cal_residuals"]["rut"]);
        _calFactIriResiduals = Convert.ToDouble(lookupSets["cal_residuals"]["iri"]);
        _calFactTextureResiduals = Convert.ToDouble(lookupSets["cal_residuals"]["texture"]);

        // Calibration factors - Maintenance
        _calFactPaProba = Convert.ToDouble(lookupSets["cal_maintenance"]["pa_proba"]);
        _calFactSuProba = Convert.ToDouble(lookupSets["cal_maintenance"]["su_proba"]);
        _calFactPotfillProba = Convert.ToDouble(lookupSets["cal_maintenance"]["potfill_proba"]);
        _calFactPaExtent = Convert.ToDouble(lookupSets["cal_maintenance"]["pa_extent"]);
        _calFactSuExtent = Convert.ToDouble(lookupSets["cal_maintenance"]["su_extent"]);
        _calFactPotfillExtent = Convert.ToDouble(lookupSets["cal_maintenance"]["potfill_extent"]);
        _calFactRutReduc = Convert.ToDouble(lookupSets["cal_maintenance"]["rut_reduc"]);
        _calFactIriReduc = Convert.ToDouble(lookupSets["cal_maintenance"]["iri_reduc"]);
        _calMinRutReducPaMaint = Convert.ToDouble(lookupSets["cal_maintenance"]["rut_reduc_min"]);
        _calMinIRIReducPaMaint = Convert.ToDouble(lookupSets["cal_maintenance"]["iri_reduc_min"]);

        // Calibration factors - Increments
        _incremAdjustmentFactorsIRI = new Dictionary<string, double>();
        foreach (var key in lookupSets["cal_increm_adj_iri"].Keys)
        {
            _incremAdjustmentFactorsIRI[key] = Convert.ToDouble(lookupSets["cal_increm_adj_iri"][key]);
        }

        _incremAdjustmentFactorsRut = new Dictionary<string, double>();
        foreach (var key in lookupSets["cal_increm_adj_rut"].Keys)
        {
            _incremAdjustmentFactorsRut[key] = Convert.ToDouble(lookupSets["cal_increm_adj_rut"][key]);
        }

        _incremAdjustmentFactorsTexture = new Dictionary<string, double>();
        foreach (var key in lookupSets["cal_increm_adj_text"].Keys)
        {
            _incremAdjustmentFactorsTexture[key] = Convert.ToDouble(lookupSets["cal_increm_adj_text"][key]);
        }

        // Calibration factors - Resets
        _calFactRutReset = Convert.ToDouble(lookupSets["cal_resets"]["rut"]);
        _calFactIriReset = Convert.ToDouble(lookupSets["cal_resets"]["iri"]);
        _calFactTextureReset = Convert.ToDouble(lookupSets["cal_resets"]["texture"]);

        _resetAdjustmentFactorsIRI = new Dictionary<string, double>();
        foreach (var key in lookupSets["cal_reset_adj_iri"].Keys)
        {
            _resetAdjustmentFactorsIRI[key] = Convert.ToDouble(lookupSets["cal_reset_adj_iri"][key]);
        }

        _resetAdjustmentFactorsRut = new Dictionary<string, double>();
        foreach (var key in lookupSets["cal_reset_adj_rut"].Keys)
        {
            _resetAdjustmentFactorsRut[key] = Convert.ToDouble(lookupSets["cal_reset_adj_rut"][key]);
        }

        _resetAdjustmentFactorsTexture = new Dictionary<string, double>();
        foreach (var key in lookupSets["cal_reset_adj_text"].Keys)
        {
            _resetAdjustmentFactorsTexture[key] = Convert.ToDouble(lookupSets["cal_reset_adj_text"][key]);
        }

    }




}
