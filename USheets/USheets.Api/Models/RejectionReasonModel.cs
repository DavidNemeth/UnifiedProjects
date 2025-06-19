using System.ComponentModel.DataAnnotations;

namespace USheets.Api.Models
{
    public class RejectionReasonModel
    {
        [Required]
        public string? Reason { get; set; }
    }
}
