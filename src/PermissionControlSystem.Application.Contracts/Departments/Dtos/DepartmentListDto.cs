using System;
using Volo.Abp.Application.Dtos;

namespace PermissionControlSystem.Departments.Dtos
{
    public class DepartmentListDto : EntityDto<Guid>
    {
        public string Name { get; set; }
        // İleride buraya 'PersonelSayisi' gibi özet bilgiler eklenebilir
    }
}