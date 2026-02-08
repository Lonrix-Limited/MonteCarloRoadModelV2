using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JCass_Data.Objects;
using JCass_ModelCore.MonteCarlo;

namespace MonteCarloRoadModelV1.Utilities;

public static class SetupUtilities
{

    public static DistributionSimulator GetDistributionSimulator(string parameterName, jcDataSet allSetups)
    {
        jcDataSet setupDataForParameter = GetFilteredDataSet(parameterName, allSetups);
        DistributionSimulator simulator = new DistributionSimulator(parameterName, setupDataForParameter);
        return simulator;
    }

    private static jcDataSet GetFilteredDataSet(string parameterName, jcDataSet allSetups)
    {
        jcDataSet setupData = new jcDataSet();
        setupData.Columns = allSetups.Columns;
        for (int i = 0; i < allSetups.Count; i++)
        {
            Dictionary<string, object> row = allSetups.Row(i);
            string setupParameterName = row["parameter_key"].ToString();
            if (setupParameterName == parameterName)
            {
                setupData.AddRow(row);
            }
        }
        return setupData;
    }


}
