using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Interfaces;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace PermissionControlSystem.Departments
{
    public class DepartmentAppService :
        CrudAppService<
            Department,
            DepartmentDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateDepartmentDto,
            UpdateDepartmentDto>,
        IDepartmentAppService
    {
        private readonly IDepartmentRepository _departmentRepository;

        public DepartmentAppService(IDepartmentRepository departmentRepository)
            : base(departmentRepository)
        {
            _departmentRepository = departmentRepository;
        }

        public override async Task<PagedResultDto<DepartmentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var departments = await _departmentRepository.GetListAsync(
                null,
                false
            );

            var totalCount = departments.Count;

            return new PagedResultDto<DepartmentDto>(
                totalCount,
                ObjectMapper.Map<List<Department>, List<DepartmentDto>>(departments)
            );
        }

        public override async Task<DepartmentDto> CreateAsync(CreateDepartmentDto input)
        {
            var existingDept = await _departmentRepository.FindByNameAsync(input.Name);
            if (existingDept != null)
            {
                throw new UserFriendlyException($"'{input.Name}' isminde bir departman zaten mevcut!");
            }

            var department = new Department(
                GuidGenerator.Create(),
                input.Name,
                input.Description ?? "" 
            );

            await _departmentRepository.InsertAsync(department, autoSave: true);

            return ObjectMapper.Map<Department, DepartmentDto>(department);
        }

        public override async Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentDto input)
        {
            var department = await _departmentRepository.GetAsync(id);

            if (department.Name != input.Name)
            {
                var existingDept = await _departmentRepository.FindByNameAsync(input.Name);
                if (existingDept != null && existingDept.Id != id)
                {
                    throw new UserFriendlyException($"'{input.Name}' ismi başka bir departman tarafından kullanılıyor.");
                }
            }

            department.Name = input.Name;
            department.Description = input.Description; 

            await _departmentRepository.UpdateAsync(department, autoSave: true);

            return ObjectMapper.Map<Department, DepartmentDto>(department);
        }
    }
}