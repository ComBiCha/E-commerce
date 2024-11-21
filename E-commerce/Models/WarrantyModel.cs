using Stripe.Climate;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce.Models
{
    public class WarrantyModel
    {
        public int Id { get; set; }
        public string WarrantyCode { get; set; }
        public long ProductId { get; set; }
        public string OrderCode { get; set; }
        public DateTime ExpirationDate { get; set; }
        public DateTime CreatedDate { get; set; }

        [ForeignKey("ProductId")]
        public ProductModel Product { get; set; }
    }
}
