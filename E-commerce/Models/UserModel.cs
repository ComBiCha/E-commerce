using System.ComponentModel.DataAnnotations;

namespace E_commerce.Models
{
    public class UserModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Please Type Your Username")]
        public string UserName { get; set; }
        [Required(ErrorMessage = "Please Type Your Email"),EmailAddress]
        public string Email { get; set; }
        [DataType(DataType.Password),Required(ErrorMessage = "Please Type Your Password")]
        public string Password { get; set; }
    }
}