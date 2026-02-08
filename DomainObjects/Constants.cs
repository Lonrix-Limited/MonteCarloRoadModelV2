
namespace MonteCarloRoadModelV1.DomainObjects;

/// <summary>
/// General model constants set up from model Lookup sets.
/// </summary>
public class Constants
{
    private DateTime _baseDate; 
    private int _shortTermPeriod;

    // Related to Candidate Selection
    private double _minSlaToTreatAc;
    private double _minSlaToTreatCs;    
    private double _min_periods_to_next_treat;
    private double _min_sdi_to_treat;
    private double _min_pdi_to_treat;


    private double _potholeBoostFactor;

    private double _maintenanceCostCalibrationFactor;
    private double _maintenanceCostPDIThreshold;

    // Related to TSS (Treatment Suitability Scores - MCDA)
    private double _rehabExcessRutThresh;
    private double _rehabExcessRutFact;
    private double _rehabPdiRank;
    private double _holdingPdiRankPt1;
    private double _holdingPdiRankPt2;
    private double _holdingPdiRankPt3;
    private double _holdingMaxRut;

    private double _preserveSdiRank;

    private double _preserveMaxPdiChipSeal;
    private double _preserveMaxPdiAC;
    private double _holdingMaxPdiAC;

    private double _preserveMaxRut;    
    private double _preserveMinSla;           

    // Related to MCDA Treatment Triggering
    private double _maxSlaForACHeavyMaint;
    private int _minPeriodsBetweenACHeavyMaint;


    /// <summary>
    /// Base date for the model run. Maps to lookup set "gernal" and setting key "base_date".
    /// </summary>
    public DateTime BaseDate { get { return _baseDate; } }

    #region Candidate Selection related constants

    /// <summary>
    /// Number of modelling periods considered short term for purposes of trigger adjustment. Used in Candidate Selection.
    /// </summary>
    public int CSShortTermPeriod
    {
        get { return _shortTermPeriod; }     
    }
    
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

    /// <summary>
    /// Minimum Surface Distress Index (SDI) to consider for treatment (EITHER condition applied with minimum PDI)
    /// </summary>
    public double CSMinSDIToTreat
    {
        get { return _min_sdi_to_treat; }       
    }

    /// <summary>
    /// Minimum Pavemenbt Distress Index (PDI) to consider for treatment.  (EITHER condition applied with minimum SDI)    
    /// </summary>
    public double CSMinPDIToTreat
    {
        get { return _min_pdi_to_treat; }
    }
    
    #endregion

    /// <summary>
    /// Boosting factor for pothole area to bring it to scale with other distresses
    /// </summary>
    public double PotholeBoostFactor
    {
        get { return _potholeBoostFactor; }
    }

    /// <summary>
    /// Calibration factor for maintenance cost
    /// TODO: Discussion with D&K
    /// </summary>
    public double MaintenanceCostCalibrationFactor
    {
        get { return _maintenanceCostCalibrationFactor; }     
    }

    /// <summary>
    /// Maintenance PDI threshold (force maintenance cost to zero if PDI is below this value)
    /// </summary>
    public double MaintenanceCostPDIThreshold
    {
        get { return _maintenanceCostPDIThreshold; }     
    }

    /// <summary>
    /// Rut threshold above which a penalty(for Holding Actions) or boost(for Rehabs) is applied(see below)
    /// </summary>
    public double TSSRehabExcessRutThresh
    {
        get { return _rehabExcessRutThresh; }
    }

    /// <summary>
    /// Multiply excessive rut with this value to get the boost for Rehab TSS based on excessive rut(if any)
    /// </summary>
    public double TSSRehabExcessRutFact
    {
        get { return _rehabExcessRutFact; }
    }

    /// <summary>
    /// PDI rank below which TSS score for Rehab becomes zero (i.e. no rehab if PDI is below this value)
    /// </summary>
    public double TSSRehabPdiRank
    {
        get { return _rehabPdiRank; }
    }

    /// <summary>
    /// PDI rank below which TSS score for Holding Action becomes zero (i.e. no holding action if PDI is below this value)
    /// </summary>
    public double TSSHoldingPdiRankPt1
    {
        get { return _holdingPdiRankPt1; }
    }


    /// <summary>
    /// PDI rank at which score for holding action is maximal(100)
    /// </summary>
    public double TSSHoldingPdiRankPt2
    {
        get { return _holdingPdiRankPt2; }
    }
    
    /// <summary>
    /// TSS for holding action based on PDI when PDI rank is 100
    /// </summary>
    public double TSSHoldingPdiRankPt3
    {
        get { return _holdingPdiRankPt3; }
    }

    /// <summary>
    /// Do not consider holding action if rut is above this value (unless it is not a rehab route in which case it is ignored)
    /// </summary>
    public double TSSHoldingMaxRut
    {
        get { return _holdingMaxRut; }
    }

    
    /// <summary>
    /// SDI Rank below which score for Preservation becomes zero (we want to apply preservation where there is some surface distress)
    /// </summary>
    public double TSSPreserveSdiRank
    {
        get { return _preserveSdiRank; }
    }
        
    /// <summary>
    /// Do not consider Preservation ChipSeal treatment if PDI is above this value 
    /// </summary>
    public double TSSPreserveMaxPdiChipseal
    {
        get { return _preserveMaxPdiChipSeal; }
    }

    /// <summary>
    /// Do not consider Preservation AC treatment if PDI is above this value 
    /// </summary>
    public double TSSPreserveMaxPdiAC
    {
        get { return _preserveMaxPdiAC; }
    }

    /// <summary>
    /// Do not consider Holding AC treatment if PDI is above this value 
    /// </summary>
    public double TSSHoldingMaxPdiAC
    {
        get { return _holdingMaxPdiAC; }
    }

    /// <summary>
    /// Do not consider preservation if rut is above this value
    /// </summary>
    public double TSSPreserveMaxRut
    {
        get { return _preserveMaxRut; }
    }

    /// <summary>
    /// Do not consider preservation if Surface Life Achieved % is below this value
    /// </summary>
    public double TSSPreserveMinSla
    {
        get { return _preserveMinSla; }
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

    public Constants(Dictionary<string, Dictionary<string, object>> lookupSets)
    {        
        _baseDate = JCass_Core.Utils.HelperMethods.ParseDateNoTime(lookupSets["general"]["base_date"]);
        _shortTermPeriod = Convert.ToInt32(lookupSets["general"]["short_term_periods"]);

        // Candidate Selection related constants
        _min_periods_to_next_treat = Convert.ToInt32(lookupSets["candidate_selection"]["min_periods_to_next_treat"]);
        _min_sdi_to_treat = Convert.ToDouble(lookupSets["candidate_selection"]["min_sdi_to_treat"]);
        _min_pdi_to_treat = Convert.ToDouble(lookupSets["candidate_selection"]["min_pdi_to_treat"]);
        _minSlaToTreatAc = Convert.ToDouble(lookupSets["candidate_selection"]["min_sla_to_treat_ac"]);
        _minSlaToTreatCs = Convert.ToDouble(lookupSets["candidate_selection"]["min_sla_to_treat_cs"]);
        
        _potholeBoostFactor = Convert.ToDouble(lookupSets["distress"]["poth_booster"]);

        _maintenanceCostCalibrationFactor = Convert.ToDouble(lookupSets["maint_pred"]["cal_maint_pred"]);
        _maintenanceCostPDIThreshold = Convert.ToDouble(lookupSets["maint_pred"]["maint_pdi_threshold"]);

        // Related to TSS
        _rehabExcessRutThresh = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["rehab_excess_rut_thresh"]);
        _rehabExcessRutFact = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["rehab_excess_rut_fact"]);
        _rehabPdiRank = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["rehab_pdi_rank"]);
        _holdingPdiRankPt1 = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_pdi_rank_pt1"]);
        _holdingPdiRankPt2 = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_pdi_rank_pt2"]);
        _holdingPdiRankPt3 = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_pdi_rank_pt3"]);
        _holdingMaxRut = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_max_rut"]);
        _preserveSdiRank = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["preserve_sdi_rank"]);
        
        _preserveMaxPdiChipSeal = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["preserve_max_pdi_chipseal"]);
        _preserveMaxPdiAC = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["preserve_max_pdi_ac"]);
        _holdingMaxPdiAC = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["holding_max_pdi_ac"]);

        _preserveMaxRut = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["preserve_max_rut"]);
        _preserveMinSla = Convert.ToDouble(lookupSets["treatment_suitability_scores"]["preserve_min_sla"]);

        // Related to MCDA Treatment Triggering
        _maxSlaForACHeavyMaint = Convert.ToDouble(lookupSets["mcda_treatment_triggering"]["ac_hmaint_maximum_sla"]);
        _minPeriodsBetweenACHeavyMaint = Convert.ToInt32(lookupSets["mcda_treatment_triggering"]["ac_hmaint_min_periods_between"]);
                
    }




}
