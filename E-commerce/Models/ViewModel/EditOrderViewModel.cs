namespace E_commerce.Models.ViewModel
{
    public class EditOrderViewModel
    {
        public OrderModel Order { get; set; }
        public List<OrderDetails> OrderDetails { get; set; }
    }
}
