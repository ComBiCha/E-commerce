using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce.Models
{
	public class OrderDetails
	{
		public int Id { get; set; }
		public string UserName { get; set; }
		public string OrderCode { get; set; }
		public long ProductId { get; set; }
		public decimal Price { get; set; }
		public int Quantity { get; set; }

		[ForeignKey("ProductId")]
		public ProductModel Product { get; set; }
	}
}
