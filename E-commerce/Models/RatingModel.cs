using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce.Models
{
	public class RatingModel
	{
		[Key]
		public int Id { get; set; }
		public long ProductId { get; set; }
		[Required(ErrorMessage = "Comment is required")]
		public string Comment { get; set; }
		[Required(ErrorMessage = "Name is required")]
		public string Name { get; set; }
		[Required(ErrorMessage = "Email is required")]
		public string Email { get; set; }

		public string Star { get; set; }
		[ForeignKey("ProductId")]
		public ProductModel Product { get; set; }
	}
}
