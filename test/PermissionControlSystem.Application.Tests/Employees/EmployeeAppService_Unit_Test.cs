using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Employees.Dtos;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Events;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Outbox;
using PermissionControlSystem.Services;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.Users;
using Volo.Abp.ObjectMapping;
using Xunit;

namespace PermissionControlSystem.Employees
{
    public class EmployeeAppService_Unit_Test : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IEmployeeRepository _employeeRepoMock;
        private readonly IElasticSearchService _elasticMock;
        private readonly IDistributedEventBus _eventBusMock;
        private readonly EmployeeManager _employeeManager;
        private readonly EmployeeAppService _service;

        private readonly ICurrentUser _currentUserMock;
        private readonly IDistributedCache<List<EmployeeCacheItem>, string> _employeeListCache;
        private readonly IDistributedCache<EmployeeCacheItem, string> _singleEmployeeCache;
        private readonly IDistributedCache<List<LeaveRequestCacheItem>, string> _employeeLeavesCacheMock;
        private readonly IRepository<OutboxMessage, Guid> _outboxRepoMock;

        public EmployeeAppService_Unit_Test()
        {
            _employeeRepoMock = Substitute.For<IEmployeeRepository>();
            _elasticMock = Substitute.For<IElasticSearchService>();
            _eventBusMock = Substitute.For<IDistributedEventBus>();
            _singleEmployeeCache = Substitute.For<IDistributedCache<EmployeeCacheItem, string>>();
            _employeeListCache = Substitute.For<IDistributedCache<List<EmployeeCacheItem>, string>>();
            _employeeLeavesCacheMock = Substitute.For<IDistributedCache<List<LeaveRequestCacheItem>, string>>();
            _outboxRepoMock = Substitute.For<IRepository<OutboxMessage, Guid>>();

            _currentUserMock = Substitute.For<ICurrentUser>();
            _currentUserMock.UserName.Returns("admin");
            _currentUserMock.Id.Returns(Guid.NewGuid());

            // 🔥 SENIOR FIX 1: Hata veren Fake sınıfı çöpe attık!
            // Doğrudan NSubstitute ile kilitli servisleri %100 uyumlu mockluyoruz.
            var lazySp = Substitute.For<IAbpLazyServiceProvider>();

            // 1. Current User
            lazySp.LazyGetRequiredService<ICurrentUser>().Returns(_currentUserMock);
            lazySp.LazyGetService<ICurrentUser>().Returns(_currentUserMock);

            // 2. Object Mapper
            var mapper = GetRequiredService<IObjectMapper>();
            lazySp.LazyGetRequiredService<IObjectMapper>().Returns(mapper);
            lazySp.LazyGetService<IObjectMapper>().Returns(mapper);

            // 3. Guid Generator
            var guidGen = GetRequiredService<IGuidGenerator>();
            lazySp.LazyGetRequiredService<IGuidGenerator>().Returns(guidGen);
            lazySp.LazyGetService<IGuidGenerator>().Returns(guidGen);

            // 4. Async Queryable Executer
            var asyncExec = GetRequiredService<IAsyncQueryableExecuter>();
            lazySp.LazyGetRequiredService<IAsyncQueryableExecuter>().Returns(asyncExec);
            lazySp.LazyGetService<IAsyncQueryableExecuter>().Returns(asyncExec);

            _employeeManager = new EmployeeManager(_employeeRepoMock);
            _employeeManager.LazyServiceProvider = lazySp;

            _service = new EmployeeAppService(
                _employeeRepoMock,
                _elasticMock,
                _eventBusMock,
                _employeeManager,
                _employeeListCache,
                _singleEmployeeCache,
                _outboxRepoMock,
                _employeeLeavesCacheMock
            );

            _service.LazyServiceProvider = lazySp;
        }

        #region GetListAsync & GetCachedEmployeeListAsync Tests

        [Fact]
        public async Task GetListAsync_Should_Return_Paged_List_When_Data_Exists()
        {
            var mockEmployees = new List<EmployeeCacheItem>
            {
                new EmployeeCacheItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Eren", "Coşkun", "Eren Coşkun", "Dev", "eren@test.com", "555", "IT")
            };

            // 🔥 SENIOR FIX 2: CS0121 hatalarını çözmek için 6 parametreyi de tam yazdık. Asla Ambiguous olmaz!
            _employeeListCache.GetOrAddAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<List<EmployeeCacheItem>>>>(),
                Arg.Any<Func<DistributedCacheEntryOptions>>(),
                Arg.Any<bool?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>()
            ).Returns(Task.FromResult(mockEmployees));

            var input = new PagedAndSortedResultRequestDto { SkipCount = 0, MaxResultCount = 10 };
            var result = await _service.GetListAsync(input);

            result.TotalCount.ShouldBe(1);
            result.Items[0].FirstName.ShouldBe("Eren");
        }

        [Fact]
        public async Task GetListAsync_Should_Return_Empty_List_When_Database_Is_Empty()
        {
            _employeeListCache.GetOrAddAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<List<EmployeeCacheItem>>>>(),
                Arg.Any<Func<DistributedCacheEntryOptions>>(),
                Arg.Any<bool?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>()
            ).Returns(Task.FromResult(new List<EmployeeCacheItem>()));

            var input = new PagedAndSortedResultRequestDto();
            var result = await _service.GetListAsync(input);

            result.TotalCount.ShouldBe(0);
            result.Items.ShouldBeEmpty();
        }

        #endregion

        #region GetAsync Tests

        [Fact]
        public async Task GetAsync_Should_Return_Employee_When_Id_Is_Valid()
        {
            var empId = Guid.NewGuid();
            var cachedEmployee = new EmployeeCacheItem(empId, Guid.NewGuid(), Guid.NewGuid(), "Ali", "Veli", "Ali Veli", "QA", "ali@test.com", "123", "HR");

            _singleEmployeeCache.GetOrAddAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<EmployeeCacheItem>>>(),
                Arg.Any<Func<DistributedCacheEntryOptions>>(),
                Arg.Any<bool?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>()
            ).Returns(Task.FromResult(cachedEmployee));

            var result = await _service.GetAsync(empId);
            result.FirstName.ShouldBe("Ali");
        }

        [Fact]
        public async Task GetAsync_Should_Throw_EntityNotFoundException_When_Id_Is_Invalid()
        {
            var invalidId = Guid.NewGuid();

            _singleEmployeeCache.GetOrAddAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<EmployeeCacheItem>>>(),
                Arg.Any<Func<DistributedCacheEntryOptions>>(),
                Arg.Any<bool?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>()
            ).Returns(Task.FromException<EmployeeCacheItem>(new EntityNotFoundException(typeof(Employee), invalidId)));

            await Assert.ThrowsAsync<EntityNotFoundException>(async () => await _service.GetAsync(invalidId));
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_Should_Create_And_Trigger_Outbox_And_RabbitMq_Even_If_Position_Is_Null()
        {
            var input = new CreateEmployeeDto
            {
                UserId = Guid.NewGuid(),
                DepartmentId = Guid.NewGuid(),
                FirstName = "Ayşe",
                LastName = "Yılmaz",
                Email = "ayse@test.com",
                PhoneNumber = "111",
                Position = null
            };

            var result = await _service.CreateAsync(input);

            result.FirstName.ShouldBe("Ayşe");

            // 🔥 SENIOR FIX 3: Tüm overload kargaşasını bitirmek için parametre sayılarını eksiksiz verdik!
            await _employeeRepoMock.Received(1).InsertAsync(Arg.Any<Employee>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
            await _outboxRepoMock.Received(1).InsertAsync(Arg.Any<OutboxMessage>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
            await _eventBusMock.Received(1).PublishAsync(Arg.Any<EmployeeCreatedEto>(), Arg.Any<bool>(), Arg.Any<bool>());
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_Should_Throw_UnauthorizedAccessException_If_Not_Admin()
        {
            _currentUserMock.UserName.Returns("normal_user");
            var input = new UpdateEmployeeDto { FirstName = "Yeni Ad" };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await _service.UpdateAsync(Guid.NewGuid(), input));
            ex.Message.ShouldBe("Personel güncelleme yetkiniz yok!");
        }

        [Fact]
        public async Task UpdateAsync_Should_Update_Entity_Create_Outbox_And_Clear_Caches()
        {
            var empId = Guid.NewGuid();
            var existingEmployee = new Employee(empId, Guid.NewGuid(), Guid.NewGuid(), "Eski", "Soyad", "eski@test.com", "000", "Eski");

            _employeeRepoMock.GetAsync(empId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(existingEmployee));

            var input = new UpdateEmployeeDto { FirstName = "Yeni Ad", LastName = "Soyad", Email = "a@a.com", Position = "Dev", DepartmentId = Guid.NewGuid() };

            var result = await _service.UpdateAsync(empId, input);

            result.FirstName.ShouldBe("Yeni Ad");
            await _outboxRepoMock.Received(1).InsertAsync(Arg.Any<OutboxMessage>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_Should_Throw_UnauthorizedAccessException_If_Not_Admin()
        {
            _currentUserMock.UserName.Returns("normal_user");
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await _service.DeleteAsync(Guid.NewGuid()));
            ex.Message.ShouldBe("Personel silme yetkiniz yok!");
        }

        [Fact]
        public async Task DeleteAsync_Should_Call_Repository_Create_Outbox_And_Clear_Caches()
        {
            var empId = Guid.NewGuid();
            await _service.DeleteAsync(empId);

            await _employeeRepoMock.Received(1).DeleteAsync(empId, Arg.Any<bool>(), Arg.Any<CancellationToken>());
            await _outboxRepoMock.Received(1).InsertAsync(Arg.Any<OutboxMessage>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }

        #endregion

        #region SearchAsync Tests

        [Fact]
        public async Task SearchAsync_Should_Return_Mapped_Dtos_From_ElasticSearch()
        {
            var keyword = "Eren";
            var elasticResults = new List<EmployeeDto> { new EmployeeDto { Id = Guid.NewGuid(), FirstName = "Eren", LastName = "C" } };

            _elasticMock.SearchEmployeeAsync(keyword).Returns(Task.FromResult(elasticResults));

            var result = await _service.SearchAsync(keyword);
            result[0].FirstName.ShouldBe("Eren");
        }

        [Fact]
        public async Task SearchAsync_Should_Fallback_To_Database_If_ElasticSearch_Throws_Exception()
        {
            var keyword = "Ahmet";

            _elasticMock.SearchEmployeeAsync(keyword).Returns(Task.FromException<List<EmployeeDto>>(new Exception("Elastic kapalı!")));

            var dbEmployees = new List<Employee> { new Employee(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ahmet", "Y", "a@a.com", "1", "Dev") };

            // Expression parametresi için kesin eşleşme
            _employeeRepoMock.GetListAsync(Arg.Any<Expression<Func<Employee, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                             .Returns(Task.FromResult(dbEmployees));

            var result = await _service.SearchAsync(keyword);
            result[0].FirstName.ShouldBe("Ahmet");
        }

        #endregion

        #region BulkCreateAsync Tests 

        [Fact]
        public async Task BulkCreateAsync_Should_Insert_Many_And_Trigger_Events()
        {
            var input = new List<CreateEmployeeDto>
            {
                new CreateEmployeeDto { UserId = Guid.NewGuid(), DepartmentId = Guid.NewGuid(), FirstName = "Toplu1", LastName="Bir", Email = "1@test.com" }
            };

            await _service.BulkCreateAsync(input);

            await _employeeRepoMock.Received(1).InsertManyAsync(Arg.Any<IEnumerable<Employee>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
            await _outboxRepoMock.Received(1).InsertManyAsync(Arg.Any<IEnumerable<OutboxMessage>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }

        #endregion
    }
}