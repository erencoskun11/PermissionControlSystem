using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace PermissionControlSystem.Leaves.Dtos
{
    public class RejectLeaveDto
    {
        [Required(ErrorMessage = "Reddetme sebebi zorunludur.")]
        [MinLength(2, ErrorMessage = "Reddetme sebebi çok kısa olamaz.")]
        public string Reason { get; set; } = string.Empty;
    }
}
