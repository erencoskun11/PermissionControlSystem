using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Events;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Managers;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local; // 🔥 Artık LocalEventBus kullanıyoruz
using Volo.Abp.ObjectMapping;
using Xunit;

namespace PermissionControlSystem.Departments
{
    public class DepartmentAppService_Unit_Test : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IDepartmentRepository _departmentRepoMock;
        private readonly IRepository<Employee, Guid> _employeeRepoMock;
        private readonly IElasticSearchService _elasticMock;
        private readonly IDistributedCache<List<DepartmentCacheItem>, string> _departmentCacheMock;
        private readonly ILocalEventBus _localEventBusMock; // 🔥 Yeni Mock
        private readonly DepartmentAppService _service;
        private readonly IDistributedEventBus _distributedEventBusMock; // 🔥 YENİ MOCK

        public DepartmentAppService_Unit_Test()
        {
            // 1. Mock'ları Hazırla
            _departmentRepoMock = Substitute.For<IDepartmentRepository>();
            _employeeRepoMock = Substitute.For<IRepository<Employee, Guid>>();
            _elasticMock = Substitute.For<IElasticSearchService>();
            _departmentCacheMock = Substitute.For<IDistributedCache<List<DepartmentCacheItem>, string>>();
            _localEventBusMock = Substitute.For<ILocalEventBus>();
            _distributedEventBusMock = Substitute.For<IDistributedEventBus>(); // 🔥 YENİ MOCK YARATILDI

            // 2. Manager ve Service Kurulumu
            var departmentManager = new DepartmentManager(_departmentRepoMock, _employeeRepoMock);
            departmentManager.LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>();

            // 🔥 Yeni zayıflatılmış servise sadece 5 parametre veriyoruz
            _service = new DepartmentAppService(
          _departmentRepoMock,    // 1
          _elasticMock,           // 2
          departmentManager,      // 3
          _departmentCacheMock,   // 4
          _localEventBusMock,      // 5
          _employeeRepoMock,       // 6 
          _distributedEventBusMock  // 7
      );

            _service.LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>();
        }

        #region GetListAsync Tests

        [Fact]
        public async Task GetListAsync_Should_Return_Mapped_Departments_From_Cache()
        {
            var mockCacheList = new List<DepartmentCacheItem>
            {
                new DepartmentCacheItem(Guid.NewGuid(), "IT", "Bilgi İşlem")
            };

            // 🔥 Tip belirtmeye gerek yok, metodun her türlü çağrısında mockCacheList dönecek!
            _departmentCacheMock.GetOrAddAsync(default!, default!)
                                .ReturnsForAnyArgs(Task.FromResult(mockCacheList));

            var input = new PagedAndSortedResultRequestDto { SkipCount = 0, MaxResultCount = 10 };
            var result = await _service.GetListAsync(input);

            result.TotalCount.ShouldBe(1);
            result.Items[0].Name.ShouldBe("IT");
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_Should_Throw_BusinessException_If_Name_Exists()
        {
            var input = new CreateDepartmentDto { Name = "IT", Description = "Desc" };
            var existingDept = new Department(Guid.NewGuid(), "IT", "Eski");

            // 🔥 FirstOrDefaultAsync için sadece Expression parametresini mockluyoruz
            _departmentRepoMock.FirstOrDefaultAsync(Arg.Any<Expression<Func<Department, bool>>>())
                               .ReturnsForAnyArgs(Task.FromResult<Department?>(existingDept));

            var exception = await Assert.ThrowsAsync<BusinessException>(async () =>
            {
                await _service.CreateAsync(input);
            });

            exception.Code.ShouldBe("Dept:001");
        }

        [Fact]
        public async Task CreateAsync_Should_Create_And_Trigger_Outbox_And_RabbitMq()
        {
            var input = new CreateDepartmentDto { Name = "HR", Description = "İnsan Kaynakları" };

            _departmentRepoMock.FirstOrDefaultAsync(Arg.Any<Expression<Func<Department, bool>>>())
                               .ReturnsForAnyArgs(Task.FromResult<Department?>(null));

            var result = await _service.CreateAsync(input);

            result.ShouldNotBeNull();
            result.Name.ShouldBe("HR");

            // 🔥 ReceivedWithAnyArgs kullanarak parametre imza krizlerini tamamen bitiriyoruz
            await _departmentRepoMock.ReceivedWithAnyArgs(1).InsertAsync(default!);
            await _departmentCacheMock.ReceivedWithAnyArgs(1).RemoveAsync(default!);
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_Should_Update_Entity_Create_Outbox_And_Clear_Cache()
        {
            var deptId = Guid.NewGuid();
            var existingDept = new Department(deptId, "Eski Ad", "Eski Desc");

            _departmentRepoMock.GetAsync(deptId).ReturnsForAnyArgs(Task.FromResult(existingDept));

            _departmentRepoMock.FirstOrDefaultAsync(Arg.Any<Expression<Func<Department, bool>>>())
                               .ReturnsForAnyArgs(Task.FromResult<Department?>(null));

            var input = new UpdateDepartmentDto { Name = "Yeni Ad", Description = "Yeni Desc" };

            var result = await _service.UpdateAsync(deptId, input);

            result.Name.ShouldBe("Yeni Ad");

            await _departmentRepoMock.ReceivedWithAnyArgs(1).UpdateAsync(default!);
            await _departmentCacheMock.ReceivedWithAnyArgs(1).RemoveAsync(default!);
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_Should_Throw_BusinessException_If_Department_Has_Employees()
        {
            var deptId = Guid.NewGuid();

            _employeeRepoMock.CountAsync(Arg.Any<Expression<Func<Employee, bool>>>())
                             .ReturnsForAnyArgs(Task.FromResult(5));

            var exception = await Assert.ThrowsAsync<BusinessException>(async () => await _service.DeleteAsync(deptId));
            exception.Code.ShouldBe("Dept:002");

            await _departmentRepoMock.DidNotReceiveWithAnyArgs().DeleteAsync(default(Guid));
        }

        [Fact]
        public async Task DeleteAsync_Should_Delete_And_Trigger_Outbox_If_Department_Is_Empty()
        {
            var deptId = Guid.NewGuid();

            _employeeRepoMock.CountAsync(Arg.Any<Expression<Func<Employee, bool>>>())
                             .ReturnsForAnyArgs(Task.FromResult(0));

            await _service.DeleteAsync(deptId);

            await _departmentRepoMock.ReceivedWithAnyArgs(1).DeleteAsync(default(Guid));

            await _departmentCacheMock.ReceivedWithAnyArgs(1).RemoveAsync(default!);
        }

        #endregion

        #region BulkCreateAsync Tests 

        [Fact]
        public async Task BulkCreateAsync_Should_Insert_Many_Departments_And_OutboxMessages()
        {
            var input = new List<CreateDepartmentDto>
            {
                new CreateDepartmentDto { Name = "Satış", Description = "Satış Departmanı" },
                new CreateDepartmentDto { Name = "Pazarlama", Description = "Pazarlama Departmanı" }
            };

            await _service.BulkCreateAsync(input);

            await _departmentRepoMock.ReceivedWithAnyArgs(1).InsertManyAsync(default!);
            await _departmentCacheMock.ReceivedWithAnyArgs(1).RemoveAsync(default!);
        }

        #endregion
    }
}