using System.ComponentModel.DataAnnotations;

namespace E_commerce.Models
{
	public class CouponModel
	{
		public int Id { get; set; }
		public string Code { get; set; } // Mã giảm giá
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Discount Amount must be a positive value.")]
        public decimal DiscountAmount { get; set; } // Số tiền giảm cố định (hoặc % nếu cần)
        [Required]
        public bool IsPercentage { get; set; } // Giảm theo % hay số tiền cố định
        [Range(0, int.MaxValue, ErrorMessage = "Min Order Value must be a non-negative number.")]
        public int? MinOrderValue { get; set; } // Giá trị đơn hàng tối thiểu để áp dụng
        [Range(0, int.MaxValue, ErrorMessage = "Max Usage must be a non-negative number.")]
        public int? MaxUsage { get; set; } // Số lần sử dụng tối đa
		public int UsedCount { get; set; } // Số lần đã sử dụng
        [Required]
        public DateTime ExpiryDate { get; set; } // Ngày hết hạn
	}
}
