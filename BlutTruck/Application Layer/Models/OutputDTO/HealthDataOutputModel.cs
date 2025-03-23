namespace BlutTruck.Application_Layer.Models
{
    public class HealthDataOutputModel
    {
        public required string UserId { get; set; }
        public int? Steps { get; set; }
        public double? ActiveCalories { get; set; }

        public List<int?> HeartRates { get; set; } = new List<int?>();
        public List<HeartRateDataPoint> HeartRateData { get; set; } = new List<HeartRateDataPoint>();

        public double? AvgHeartRate
        {
            get
            {
                var validRates = HeartRates?.Where(h => h.HasValue).Select(h => h.Value);
                return validRates?.Any() == true ? validRates.Average() : null;
            }
        }
        public double? MinHeartRate
        {
            get
            {
                var validRates = HeartRates?.Where(h => h.HasValue).Select(h => h.Value);
                return validRates?.Any() == true ? validRates.Min() : null;
            }
        }
        public double? MaxHeartRate
        {
            get
            {
                var validRates = HeartRates?.Where(h => h.HasValue).Select(h => h.Value);
                return validRates?.Any() == true ? validRates.Max() : null;
            }
        }

        public double? RestingHeartRate { get; set; }
        public double? Weight { get; set; }
        public double? Height { get; set; }

        public List<BloodPressureDataPoint> BloodPressureData { get; set; } = new List<BloodPressureDataPoint>();
        public List<OxygenSaturationDataPoint> OxygenSaturationData { get; set; } = new List<OxygenSaturationDataPoint>();
        public List<BloodGlucoseDataPoint> BloodGlucoseData { get; set; } = new List<BloodGlucoseDataPoint>();

        public double? BodyTemperature { get; set; }
        public List<TemperatureDataPoint> TemperatureData { get; set; } = new List<TemperatureDataPoint>();

        public List<RespiratoryRateDataPoint> RespiratoryRateData { get; set; } = new List<RespiratoryRateDataPoint>();
        public List<SleepSessionDataPoint> SleepData { get; set; } = new List<SleepSessionDataPoint>();
    }
}
