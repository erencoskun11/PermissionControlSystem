using System;
using System.ComponentModel.DataAnnotations;

namespace PermissionControlSystem.Leave.Dtos
{
    public class CreateLeaveRequestDto
    {
        // 👇 BUNU EKLE (Artık ön yüzden personel ID'si isteyeceğiz)
        [Required]
        public Guid EmployeeId { get; set; }

        [Required]
        public Guid LeaveTypeId { get; set; } 
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public string Reason { get; set; }
    }
}