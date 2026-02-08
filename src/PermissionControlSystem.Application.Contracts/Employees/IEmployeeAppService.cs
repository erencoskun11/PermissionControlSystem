using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using PermissionControlSystem.Employees.Dtos; // 👈 EKSİK OLAN BU!

namespace PermissionControlSystem.Employees
{
    public interface IEmployeeAppService :
        ICrudAppService<
            EmployeeDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateEmployeeDto,
            UpdateEmployeeDto>
    {

    }
}