using E_commerce.Models;

namespace E_commerce.Areas.Admin.Repository
{
	public interface IOrderObserver
	{
		Task Notify(OrderModel order);
	}
}
