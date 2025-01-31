namespace BlutTruck.Application_Layer.Models
{
    public class HealthDataInputModel
    {
        public required string UserId { get; set; }
        public int? Steps { get; set; }
        public double? ActiveCalories { get; set; }

        public List<int?> HeartRates { get; set; } = new List<int?>();

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
        public double? BloodPressureSystolic { get; set; }
        public double? BloodPressureDiastolic { get; set; }
        public double? OxygenSaturation { get; set; }
        public double? BloodGlucose { get; set; }
        public double? BodyTemperature { get; set; }
        public double? RespiratoryRate { get; set; }
    }
}
