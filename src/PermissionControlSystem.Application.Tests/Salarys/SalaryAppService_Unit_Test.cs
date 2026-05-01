using NSubstitute;
using PermissionControlSystem.Salarys.Dtos;
using PermissionControlSystem.Entities;
using PermissionControlSystem.Interfaces;
using PermissionControlSystem.Managers;
using PermissionControlSystem.Outbox;
using Shouldly;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Xunit;
using Microsoft.Extensions.Caching.Distributed;
using PermissionControlSystem.Caching;
using System.Collections.Generic;
using Volo.Abp.Users;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Guids;
using Volo.Abp.Linq;

namespace PermissionControlSystem.Salarys
{
    public class SalaryAppService_Unit_Test : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly ISalaryRepository _salaryRepoMock;
        private readonly IDistributedEventBus _eventBusMock;
        private readonly IDistributedCache<List<SalaryCacheItem>, string> _salaryCacheMock;
        private readonly IRepository<OutboxMessage, Guid> _outboxRepoMock;
        private readonly SalaryAppService _service;

        public SalaryAppService_Unit_Test()
        {
            _salaryRepoMock = Substitute.For<ISalaryRepository>();
            _eventBusMock = Substitute.For<IDistributedEventBus>();
            _salaryCacheMock = Substitute.For<IDistributedCache<List<SalaryCacheItem>, string>>();
            _outboxRepoMock = Substitute.For<IRepository<OutboxMessage, Guid>>();

            var currentUserMock = Substitute.For<ICurrentUser>();
            currentUserMock.Id.Returns(Guid.NewGuid());

            var serviceProviderMock = Substitute.For<IServiceProvider>();
            serviceProviderMock.GetService(typeof(ICurrentUser)).Returns(currentUserMock);
            serviceProviderMock.GetService(typeof(IObjectMapper)).Returns(GetRequiredService<IObjectMapper>());
            serviceProviderMock.GetService(typeof(IGuidGenerator)).Returns(GetRequiredService<IGuidGenerator>());
            serviceProviderMock.GetService(typeof(IAsyncQueryableExecuter)).Returns(GetRequiredService<IAsyncQueryableExecuter>());

            var lazyProvider = new AbpLazyServiceProvider(serviceProviderMock);
            var manager = new SalaryManager(_salaryRepoMock) { LazyServiceProvider = lazyProvider };

            _service = new SalaryAppService(
                _salaryRepoMock,
                _eventBusMock,
                manager,
                _salaryCacheMock,
                _outboxRepoMock
            ) { LazyServiceProvider = lazyProvider };
        }

        [Fact]
        public async Task CreateAsync_Should_Create_And_Trigger_Outbox()
        {
            var input = new CreateSalaryDto();
            var result = await _service.CreateAsync(input);

            result.ShouldNotBeNull();
            await _salaryRepoMock.ReceivedWithAnyArgs(1).InsertAsync(default!);
            await _outboxRepoMock.ReceivedWithAnyArgs(1).InsertAsync(default!);
            await _salaryCacheMock.ReceivedWithAnyArgs(1).RemoveAsync(default!);
        }
    }
}