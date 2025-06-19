using E_commerce.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace E_commerce.Repository
{
	public class DataContext : IdentityDbContext<AppUserModel>
	{
		public DataContext(DbContextOptions<DataContext> options) : base(options)
		{

		}
		public DbSet<BrandModel> Brands { get; set; }
		public DbSet<ProductModel> Products { get; set; }
		public DbSet<RatingModel> Ratings { get; set; }
		public DbSet<CategoryModel> Categories { get; set; }
		public DbSet<OrderModel> Orders { get; set; }
		public DbSet<OrderDetails> OrderDetails { get; set; }
		public DbSet<SliderModel> Sliders { get; set; }
		public DbSet<ContactModel> Contacts { get; set; }
		public DbSet<WishlistModel> Wishlists { get; set; }
		public DbSet<CompareModel> Compares { get; set; }
        public DbSet<ProductQuantityModel> ProductQuantities { get; set; }
		public DbSet<ShippingModel> Shippings { get; set; }
        public DbSet<WarrantyModel> Warranties { get; set; }
        public DbSet<WarrantyRequestModel> WarrantyRequests { get; set; }
        public DbSet<ProductVariationModel> Variations { get; set; }
        public DbSet<MaterialModel> Materials { get; set; }
        public DbSet<ColorModel> Colors { get; set; }
		public DbSet<CouponModel> Coupons { get; set; }
        public DbSet<CouponUsageModel> CouponUsages { get; set; }

        public DbSet<E_commerce.Models.UserModel> UserModel { get; set; }

        public DbSet<Messages> Messages { get; set; }
    
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        
            // Cấu hình relationship cho Messages
            modelBuilder.Entity<Messages>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            
            modelBuilder.Entity<Messages>()
                .HasOne(m => m.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
