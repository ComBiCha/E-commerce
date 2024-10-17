namespace E_commerce.Models.ViewModel
{
    public class CombinedViewModel
    {
        public IEnumerable<object> UsersWithRoles { get; set; }
        public List<AppUserModel> Users { get; set; }
    }

}
