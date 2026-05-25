using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Volo.Abp.Domain.Entities;

namespace PermissionControlSystem.Leaves.Dtos
{
    public class RejectLeaveDto : IHasConcurrencyStamp
    {
        [Required(ErrorMessage = "Reddetme sebebi zorunludur.")]
        [MinLength(2, ErrorMessage = "Reddetme sebebi çok kısa olamaz.")]
        public string Reason { get; set; } = string.Empty;

        // 🔥 MÜHÜR EKRANDAN SUNUCUYA GELİYOR
        public string ConcurrencyStamp { get; set; } = string.Empty;

    }
}
