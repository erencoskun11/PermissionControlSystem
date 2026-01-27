using Microsoft.EntityFrameworkCore;
using PermissionControlSystem.Employees.Dtos; // DTO'lar
using PermissionControlSystem.Departments2;   // Departman Entity
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

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
        // Repository Tiplerini netleştirdik
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

        // Listeleme (Department verisini dahil ederek)
        public override async Task<PagedResultDto<EmployeeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            // 1. Sorguyu Hazırla
            var queryable = await _repository.GetQueryableAsync();

            // 2. İlişkili Tabloyu Dahil Et (Join)
            var query = queryable.Include(x => x.Department);

            // 3. Toplam Sayıyı Bul
            var totalCount = await AsyncExecuter.CountAsync(query);

            // 4. Sayfalama ve Sıralama
            var items = await AsyncExecuter.ToListAsync(
                query
                .OrderByDescending(e => e.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
            );

            // 5. Entity -> DTO Çevrimi
            var dtos = ObjectMapper.Map<List<Employee>, List<EmployeeDto>>(items);

            return new PagedResultDto<EmployeeDto>(
                totalCount,
                dtos
            );
        }

        // Oluşturma
        public override async Task<EmployeeDto> CreateAsync(CreateEmployeeDto input)
        {
            // Constructor kullanımı daha sağlıklıdır
            var entity = new Employee(
                GuidGenerator.Create(),
                input.UserId,
                input.DepartmentId,
                input.FullName,
                input.Email,
                input.PhoneNumber
            );

            entity.Position = input.Position;

            await _repository.InsertAsync(entity, autoSave: true);

            return ObjectMapper.Map<Employee, EmployeeDto>(entity);
        }

        // Güncelleme
        public override async Task<EmployeeDto> UpdateAsync(Guid id, UpdateEmployeeDto input)
        {
            var entity = await _repository.GetAsync(id);

            entity.UpdateProfile(input.FullName, input.Position);
            entity.UpdateContact(input.Email, input.PhoneNumber);
            entity.UpdateDepartment(input.DepartmentId);

            await _repository.UpdateAsync(entity, autoSave: true);

            return ObjectMapper.Map<Employee, EmployeeDto>(entity);
        }
    }
}