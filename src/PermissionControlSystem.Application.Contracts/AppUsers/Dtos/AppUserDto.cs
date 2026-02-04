using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.Identity;

namespace PermissionControlSystem.AppUsers.Dtos
{
    public class AppUserDto : IdentityUserDto
    {
        public Guid DepartmentId { get; set; }
        public string Position { get; set; }
        public string PhoneNumber2 { get; set; }
    }
}
