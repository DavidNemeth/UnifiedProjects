using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UPortal.Data.Models
{
    public class CompanyTax
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(5, 4)")] // Allows for rates like 0.1300 (13%)
        public decimal Rate { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }
    }
}
