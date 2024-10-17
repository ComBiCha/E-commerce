using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce.Models
{
	public class CompareModel
	{
		[Key]
		public int Id { get; set; }
		public long ProductId { get; set; }
		public string UserId { get; set; }
		[ForeignKey("ProductId")]
		public ProductModel Product { get; set; }
	}
}
