using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using PermissionControlSystem.Caching;
using PermissionControlSystem.Employees.Dtos;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Events;
using PermissionControlSystem.Events.Employees;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Services;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local; // 🔥 Artık LocalEventBus var
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;
using Xunit;

namespace PermissionControlSystem.Employees
{
    public class EmployeeAppService_Unit_Test : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IEmployeeRepository _employeeRepoMock;
        private readonly IElasticSearchService _elasticMock;
        private readonly EmployeeManager _employeeManager;
        private readonly EmployeeAppService _service;

        private readonly ICurrentUser _currentUserMock;
        private readonly IDistributedCache<List<EmployeeCacheItem>, string> _employeeListCacheMock;
        private readonly IDistributedCache<EmployeeCacheItem, string> _singleEmployeeCacheMock;

        // 🔥 YENİ KAHRAMANIMIZ
        private readonly ILocalEventBus _localEventBusMock;

        public EmployeeAppService_Unit_Test()
        {
            // 1. Gerekli Mock'ları oluşturuyoruz (Outbox ve DistributedEventBus SİLİNDİ!)
            _employeeRepoMock = Substitute.For<IEmployeeRepository>();
            _elasticMock = Substitute.For<IElasticSearchService>();
            _singleEmployeeCacheMock = Substitute.For<IDistributedCache<EmployeeCacheItem, string>>();
            _employeeListCacheMock = Substitute.For<IDistributedCache<List<EmployeeCacheItem>, string>>();
            _localEventBusMock = Substitute.For<ILocalEventBus>(); // ✅

            _currentUserMock = Substitute.For<ICurrentUser>();
            _currentUserMock.UserName.Returns("admin");
            _currentUserMock.Id.Returns(Guid.NewGuid());

            // 2. ABP Lazy Service Provider Kurulumu (Kusursuz!)
            var serviceProviderMock = Substitute.For<IServiceProvider>();

            serviceProviderMock.GetService(typeof(ICurrentUser)).Returns(_currentUserMock);
            serviceProviderMock.GetService(typeof(IObjectMapper)).Returns(GetRequiredService<IObjectMapper>());
            serviceProviderMock.GetService(typeof(IGuidGenerator)).Returns(GetRequiredService<IGuidGenerator>());
            serviceProviderMock.GetService(typeof(IAsyncQueryableExecuter)).Returns(GetRequiredService<IAsyncQueryableExecuter>());

            var lazyProvider = new AbpLazyServiceProvider(serviceProviderMock);

            _employeeManager = new EmployeeManager(_employeeRepoMock);
            _employeeManager.LazyServiceProvider = lazyProvider;

            // 🔥 3. Yeni, ince ve fit servisimize sadece 6 parametre geçiyoruz
            // 🔥 Derleyicinin (Compiler) tam olarak istediği sıralama:
            _service = new EmployeeAppService(
                _employeeRepoMock,          // 1. IEmployeeRepository
                _employeeManager,           // 2. EmployeeManager
                _localEventBusMock,         // 3. ILocalEventBus
                _elasticMock,               // 4. IElasticSearchService
                _employeeListCacheMock,     // 5. Liste Cache'i
                _singleEmployeeCacheMock    // 6. Tekil Cache
            );

            _service.LazyServiceProvider = lazyProvider;

            // İsim/Email çakışması yok simülasyonu
            _employeeRepoMock.FirstOrDefaultAsync(Arg.Any<Expression<Func<Employee, bool>>>())
                             .ReturnsForAnyArgs(Task.FromResult<Employee?>(null));
        }

        [Fact]
        public async Task CreateAsync_Should_Insert_Employee_And_Publish_Local_Event()
        {
            // 1. ARRANGE
            var input = new CreateEmployeeDto
            {
                FirstName = "Eren",
                LastName = "Coskun",
                Email = "eren@test.com",
                Position = "Developer"
            };

            // 2. ACT
            var result = await _service.CreateAsync(input);

            // 3. ASSERT
            result.ShouldNotBeNull();
            result.FirstName.ShouldBe("Eren");

            // ✅ Veritabanına kayıt atıldı mı?
            await _employeeRepoMock.Received(1).InsertAsync(Arg.Any<Employee>(), true);

            // ✅ EN KRİTİK NOKTA: Outbox veya RabbitMQ kontrolü yok! 
            // Sadece LocalEventBus "Olayı Fırlattı mı?" diye bakıyoruz!
            await _localEventBusMock.Received(1).PublishAsync(Arg.Is<EmployeeCreatedEvent>(e =>
                e.Email == "eren@test.com" && e.FullName == "Eren Coskun"
            ));
        }

        [Fact]
        public async Task DeleteAsync_Should_Delete_If_User_Is_Admin()
        {
            // 1. ARRANGE
            var empId = Guid.NewGuid();
            _currentUserMock.UserName.Returns("admin"); // Admin olarak giriş yapılmış

            // 2. ACT
            await _service.DeleteAsync(empId);

            // 3. ASSERT
            // Sadece base method'un çağrıldığını (Repository üzerinden silindiğini) teyit ediyoruz.
            // Outbox'a atma işi Handler'da olduğu için burada kontrol etmiyoruz.
            await _employeeRepoMock.Received(1).DeleteAsync(empId, Arg.Any<bool>());
        }

        [Fact]
        public async Task DeleteAsync_Should_Throw_Exception_If_User_Is_Not_Admin()
        {
            // 1. ARRANGE
            var empId = Guid.NewGuid();
            _currentUserMock.UserName.Returns("user"); // Normal kullanıcı giriş yapmış!

            // 2. ACT & ASSERT
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await _service.DeleteAsync(empId);
            });

            exception.Message.ShouldBe("Personel silme yetkiniz yok!");

            // Veritabanına kesinlikle dokunulmamış olmalı!
            await _employeeRepoMock.Received(0).DeleteAsync(Arg.Any<Guid>(), Arg.Any<bool>());
        }
    }
}