using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UPortal.Dtos
{
    public class CompanyTaxDto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(5, 4)")]
        public decimal Rate { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }
    }
}
