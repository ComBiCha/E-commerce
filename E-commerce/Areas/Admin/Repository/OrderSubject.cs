using E_commerce.Models;

namespace E_commerce.Areas.Admin.Repository
{
	public class OrderSubject
	{
		private readonly List<IOrderObserver> _observers = new();

		public void Attach(IOrderObserver observer)
		{
			_observers.Add(observer);
		}

		public void Detach(IOrderObserver observer)
		{
			_observers.Remove(observer);
		}

		public async Task NotifyObservers(OrderModel order)
		{
			foreach (var observer in _observers)
			{
				await observer.Notify(order);
			}
		}
	}
}
