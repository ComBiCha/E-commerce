using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_commerce.Models
{
    public class WarrantyRequestModel
    {
        [Key]
        public int Id { get; set; } // Khóa chính
        public string WarrantyCode { get; set; } // Mã bảo hành
        public string UserId { get; set; } // ID người dùng yêu cầu

        public string Reason { get; set; } // Lý do yêu cầu bảo hành
        public int Status { get; set; } = 0; // Trạng thái (0: Pending, 1: Approved, 2: Denied)
        public DateTime CreatedDate { get; set; } = DateTime.Now; // Ngày tạo yêu cầu

        public DateTime UpdatedDate { get; set; } // Ngày cập nhật trạng thái

        public int WarrantyID { get; set; } // Khóa ngoại tham chiếu đến bảng Warranties

        // Điều hướng đến bảng liên kết
        [ForeignKey("WarrantyID")]
        public WarrantyModel Warranty { get; set; }

        [ForeignKey("UserId")]
        public AppUserModel User { get; set; }
    }
}
