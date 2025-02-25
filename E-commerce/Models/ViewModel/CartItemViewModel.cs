namespace E_commerce.Models.ViewModel
{
	public class CartItemViewModel
	{
		public List<CartItemModel> CartItems { get; set; }
		public decimal GrandTotal { get; set; }
		public decimal ShippingCost { get; set; }
        public string ShippingAddress { get; set; }
        public decimal DiscountAmount { get; set; } // Số tiền được giảm
        public decimal FinalTotal { get; set; } // Tổng sau khi giảm giá
    }
}
