using E_commerce.Repository.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce.Models
{
	public class ContactModel
	{
		[Key]

		[Required(ErrorMessage = "Website Name is required")]
		public string Name { get; set; }
		[Required(ErrorMessage = "Contact info is required")]
		public string Description { get; set; }
		[Required(ErrorMessage = "Map is required")]
		public string Map { get; set; }
		[Required(ErrorMessage = "Phone number is required")]
		public string Phone { get; set; }
		[Required(ErrorMessage = "Email is required")]
		public string Email { get; set; }
		public string LogoImg { get; set; }

		[NotMapped]
		[FileExtension]
		public IFormFile? ImageUpload { get; set; }
	}
}
