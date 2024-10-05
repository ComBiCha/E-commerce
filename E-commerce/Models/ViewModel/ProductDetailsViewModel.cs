using System.ComponentModel.DataAnnotations;

namespace E_commerce.Models.ViewModel
{
	public class ProductDetailsViewModel
	{
		public ProductModel ProductDetails { get; set; }

		[Required(ErrorMessage = "Comment is required")]
		public string Comment { get; set; }
		[Required(ErrorMessage = "Name is required")]
		public string Name { get; set; }
		[Required(ErrorMessage = "Email is required")]
		public string Email { get; set; }
	}
}
