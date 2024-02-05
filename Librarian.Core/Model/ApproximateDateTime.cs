using System.ComponentModel.DataAnnotations.Schema;

namespace Librarian.Model
{
    public enum DatePrecision
    {
        Exact,
        Second,
        Minute,
        Hour,
        Day,
        Week,
        Month,
        Quarter,
        HalfYear,
        Year,
        Decade
    }

    [ComplexType]
    public class ApproximateDateTime
    {
        public DateTimeOffset Date { get; set; }
        public DatePrecision Precision { get; set; }
    }
}
