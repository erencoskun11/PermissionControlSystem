using Microsoft.EntityFrameworkCore;
using PermissionControlSystem.Employees.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using PermissionControlSystem.Departments;

namespace PermissionControlSystem.Employees
{
    public class EmployeeAppService :
        CrudAppService<
            Employee,
            EmployeeDto,
            Guid,
            PagedAndSortedResultRequestDto,
            CreateEmployeeDto,
            UpdateEmployeeDto>,
        IEmployeeAppService
    {
        private readonly IRepository<Employee, Guid> _repository;
        private readonly IRepository<Department, Guid> _deptRepository;

        public EmployeeAppService(
            IRepository<Employee, Guid> repository,
            IRepository<Department, Guid> deptRepository)
            : base(repository)
        {
            _repository = repository;
            _deptRepository = deptRepository;
        }

        public override async Task<PagedResultDto<EmployeeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var queryable = await _repository.GetQueryableAsync();
            var query = queryable.Include(x => x.Department);

            var totalCount = await AsyncExecuter.CountAsync(query);

            var items = await AsyncExecuter.ToListAsync(
                query
                .OrderByDescending(e => e.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
            );

            var dtos = ObjectMapper.Map<List<Employee>, List<EmployeeDto>>(items);

            return new PagedResultDto<EmployeeDto>(
                totalCount,
                dtos
            );
        }

        public override async Task<EmployeeDto> CreateAsync(CreateEmployeeDto input)
        {
            var entity = new Employee(
                GuidGenerator.Create(),
                input.UserId,
                input.DepartmentId,
                input.FirstName,
                input.LastName,
                input.Email,
                input.PhoneNumber,
                input.Position
            );

            await _repository.InsertAsync(entity, autoSave: true);

            return ObjectMapper.Map<Employee, EmployeeDto>(entity);
        }

        public override async Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeDto input)
        {
            var entity = await _repository.GetAsync(id);

            entity.FirstName = input.FirstName;
            entity.LastName = input.LastName;
            entity.Position = input.Position;
            entity.Email = input.Email;
            entity.PhoneNumber = input.PhoneNumber;
            entity.DepartmentId = input.DepartmentId;

            await _repository.UpdateAsync(entity, autoSave: true);

            return ObjectMapper.Map<Employee, EmployeeDto>(entity);
        }
    }
}