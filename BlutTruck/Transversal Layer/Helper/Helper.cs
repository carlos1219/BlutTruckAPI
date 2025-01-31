namespace BlutTruck.Transversal_Layer.Helper
{
    using BlutTruck.Transversal_Layer.IHelper;
    using BlutTruck.Application_Layer.Models;
    using System;
        using System.Collections.Generic;

        public class Helper : IHelper
        {
            public string GetCurrentDateKey()
            {
                return DateTime.Now.ToString("yyyy-MM-dd");
            }

            public Dictionary<string, object> FormatHealthData(HealthDataInputModel data)
            {
                var healthDataDict = new Dictionary<string, object> { { "UserId", data.UserId } };

                if (data.Steps.HasValue) healthDataDict["steps"] = data.Steps.Value;
                if (data.ActiveCalories.HasValue) healthDataDict["activeCalories"] = data.ActiveCalories.Value;
                if (data.AvgHeartRate.HasValue) healthDataDict["avgHeartRate"] = data.AvgHeartRate.Value;
                if (data.MinHeartRate.HasValue) healthDataDict["minHeartRate"] = data.MinHeartRate.Value;
                if (data.MaxHeartRate.HasValue) healthDataDict["maxHeartRate"] = data.MaxHeartRate.Value;
                if (data.RestingHeartRate.HasValue) healthDataDict["restingHeartRate"] = data.RestingHeartRate.Value;
                if (data.Weight.HasValue) healthDataDict["weight"] = data.Weight.Value;
                if (data.Height.HasValue) healthDataDict["height"] = data.Height.Value;
                if (data.BloodPressureSystolic.HasValue) healthDataDict["bloodPressureSystolic"] = data.BloodPressureSystolic.Value;
                if (data.BloodPressureDiastolic.HasValue) healthDataDict["bloodPressureDiastolic"] = data.BloodPressureDiastolic.Value;
                if (data.OxygenSaturation.HasValue) healthDataDict["oxygenSaturation"] = data.OxygenSaturation.Value;
                if (data.BloodGlucose.HasValue) healthDataDict["bloodGlucose"] = data.BloodGlucose.Value;
                if (data.BodyTemperature.HasValue) healthDataDict["bodyTemperature"] = data.BodyTemperature.Value;
                if (data.RespiratoryRate.HasValue) healthDataDict["respiratoryRate"] = data.RespiratoryRate.Value;

                return healthDataDict;
            }
        }
    }
