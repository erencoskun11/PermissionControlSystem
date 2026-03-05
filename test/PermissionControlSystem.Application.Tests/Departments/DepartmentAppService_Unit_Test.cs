using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Events;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Notifications;
using PermissionControlSystem.Outbox;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Users;
using Xunit;

namespace PermissionControlSystem.Departments
{
    public class DepartmentAppService_Unit_Test : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IDepartmentRepository _departmentRepoMock;
        private readonly INotificationService _notificationMock;
        private readonly IElasticSearchService _elasticMock;
        private readonly IDistributedEventBus _eventBusMock;
        private readonly IRepository<Employee, Guid> _employeeRepoMock;
        private readonly IDistributedCache<List<DepartmentCacheItem>, string> _departmentCacheMock;
        private readonly IRepository<OutboxMessage, Guid> _outboxRepoMock;
        private readonly ICurrentUser _currentUserMock;
        private readonly DepartmentAppService _service;

        public DepartmentAppService_Unit_Test()
        {
            _departmentRepoMock = Substitute.For<IDepartmentRepository>();
            _notificationMock = Substitute.For<INotificationService>();
            _elasticMock = Substitute.For<IElasticSearchService>();
            _eventBusMock = Substitute.For<IDistributedEventBus>();
            _employeeRepoMock = Substitute.For<IRepository<Employee, Guid>>();
            _departmentCacheMock = Substitute.For<IDistributedCache<List<DepartmentCacheItem>, string>>();
            _outboxRepoMock = Substitute.For<IRepository<OutboxMessage, Guid>>();

            _currentUserMock = Substitute.For<ICurrentUser>();
            _currentUserMock.UserName.Returns("admin"); // Yetki hatalarına takılmamak için

            var departmentManager = new DepartmentManager(_departmentRepoMock, _employeeRepoMock);
            departmentManager.LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>();

            _service = new DepartmentAppService(
                _departmentRepoMock,
                _notificationMock,
                _elasticMock,
                _eventBusMock,
                departmentManager,
                _departmentCacheMock,
                _outboxRepoMock
            );

            var lazyServiceProviderMock = Substitute.For<IAbpLazyServiceProvider>();
            lazyServiceProviderMock.LazyGetRequiredService<ICurrentUser>().Returns(_currentUserMock);
            _service.LazyServiceProvider = lazyServiceProviderMock;
        }

        #region GetListAsync Tests

        [Fact]
        public async Task GetListAsync_Should_Return_Mapped_Departments_From_Cache()
        {
            // ARRANGE: Artık veritabanından değil, Cache'ten geliyor!
            var mockCacheList = new List<DepartmentCacheItem>
            {
                new DepartmentCacheItem(Guid.NewGuid(), "IT", "Bilgi İşlem")
            };

            _departmentCacheMock.GetOrAddAsync(Arg.Any<string>(), Arg.Any<Func<Task<List<DepartmentCacheItem>>>>(), Arg.Any<Func<DistributedCacheEntryOptions>>(), null, false, default)
                                .Returns(mockCacheList);

            // ACT
            var result = await _service.GetListAsync(new PagedAndSortedResultRequestDto());

            // ASSERT
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

            _departmentRepoMock
                .FirstOrDefaultAsync(Arg.Any<Expression<Func<Department, bool>>>())
                .ReturnsForAnyArgs(Task.FromResult(existingDept));

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
                               .Returns((Department)null);

            var result = await _service.CreateAsync(input);

            result.ShouldNotBeNull();
            result.Name.ShouldBe("HR");

            // 1. Veritabanına kaydedildi mi?
            await _departmentRepoMock.Received(1).InsertAsync(Arg.Is<Department>(d => d.Name == "HR"), true);

            // 2. Bildirim atıldı mı?
            await _notificationMock.Received(1).AddNotificationAsync(Arg.Is<string>(s => s.Contains("HR")));

            // 🔥 3. SENIOR FIX: Elastic yerine Outbox'a atıldı mı?
            await _outboxRepoMock.Received(1).InsertAsync(Arg.Is<OutboxMessage>(o => o.Type == "DepartmentCreated"));

            // 4. RabbitMQ'ya mesaj atıldı mı?
            await _eventBusMock.Received(1).PublishAsync(Arg.Is<DepartmentCreatedEto>(e => e.DepartmentName == "HR"));

            // 🔥 5. SENIOR FIX: Cache temizlendi mi?
            await _departmentCacheMock.Received(1).RemoveAsync("AllActiveDepartments", null, false, default);
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_Should_Update_Entity_Create_Outbox_And_Clear_Cache()
        {
            var deptId = Guid.NewGuid();
            var existingDept = new Department(deptId, "Eski Ad", "Eski Desc");

            _departmentRepoMock.GetAsync(deptId).Returns(existingDept);
            _departmentRepoMock.FirstOrDefaultAsync(Arg.Any<Expression<Func<Department, bool>>>())
                               .Returns((Department)null);

            var input = new UpdateDepartmentDto { Name = "Yeni Ad", Description = "Yeni Desc" };

            var result = await _service.UpdateAsync(deptId, input);

            result.Name.ShouldBe("Yeni Ad");

            await _departmentRepoMock.Received(1).UpdateAsync(Arg.Is<Department>(d => d.Name == "Yeni Ad"), true);

            // 🔥 SENIOR FIX: Elastic yerine Outbox kontrolü ve Cache temizliği
            await _outboxRepoMock.Received(1).InsertAsync(Arg.Is<OutboxMessage>(o => o.Type == "DepartmentUpdated"));
            await _departmentCacheMock.Received(1).RemoveAsync("AllActiveDepartments", null, false, default);
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_Should_Throw_BusinessException_If_Department_Has_Employees()
        {
            var deptId = Guid.NewGuid();

            _employeeRepoMock.CountAsync(Arg.Any<Expression<Func<Employee, bool>>>())
                             .Returns(5);

            var exception = await Assert.ThrowsAsync<BusinessException>(async () => await _service.DeleteAsync(deptId));
            exception.Code.ShouldBe("Dept:002");

            await _departmentRepoMock.DidNotReceive().DeleteAsync(deptId);
            await _outboxRepoMock.DidNotReceive().InsertAsync(Arg.Any<OutboxMessage>());
        }

        [Fact]
        public async Task DeleteAsync_Should_Delete_And_Trigger_Outbox_If_Department_Is_Empty()
        {
            var deptId = Guid.NewGuid();

            _employeeRepoMock.CountAsync(Arg.Any<Expression<Func<Employee, bool>>>())
                             .Returns(0);

            await _service.DeleteAsync(deptId);

            await _departmentRepoMock.Received(1).DeleteAsync(deptId);
            await _eventBusMock.Received(1).PublishAsync(Arg.Is<DepartmentDeletedEto>(e => e.DepartmentId == deptId));

            // 🔥 SENIOR FIX: Elastic silme yerine Outbox'a Deleted mesajı bırakıldı mı? Cache temizlendi mi?
            await _outboxRepoMock.Received(1).InsertAsync(Arg.Is<OutboxMessage>(o => o.Type == "DepartmentDeleted"));
            await _departmentCacheMock.Received(1).RemoveAsync("AllActiveDepartments", null, false, default);
        }

        #endregion

        #region BulkCreateAsync Tests (YENİ EKLENDİ)

        [Fact]
        public async Task BulkCreateAsync_Should_Insert_Many_Departments_And_OutboxMessages()
        {
            var input = new List<CreateDepartmentDto>
            {
                new CreateDepartmentDto { Name = "Satış", Description = "Satış Departmanı" },
                new CreateDepartmentDto { Name = "Pazarlama", Description = "Pazarlama Departmanı" }
            };

            await _service.BulkCreateAsync(input);

            // Veritabanına toplu kayıt atıldı mı?
            await _departmentRepoMock.Received(1).InsertManyAsync(Arg.Is<IEnumerable<Department>>(x => x.Count() == 2), false); // autoSave: false bekliyoruz!

            // Outbox'a toplu kayıt atıldı mı?
            await _outboxRepoMock.Received(1).InsertManyAsync(Arg.Is<IEnumerable<OutboxMessage>>(x => x.Count() == 2 && x.First().Type == "DepartmentCreated"), true);

            // Cache temizlendi mi?
            await _departmentCacheMock.Received(1).RemoveAsync("AllActiveDepartments", null, false, default);
        }

        #endregion
    }
}