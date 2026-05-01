using NSubstitute;
using PermissionControlSystem.meryem s.Dtos;
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

namespace PermissionControlSystem.meryem s
{
    public partial class meryem AppService_Unit_Test : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly Imeryem Repository _meryem RepoMock;
        private readonly IDistributedEventBus _eventBusMock;
        private readonly IDistributedCache<List<meryem CacheItem>, string> _meryem CacheMock;
        private readonly IRepository<OutboxMessage, Guid> _outboxRepoMock;
        private readonly meryem AppService _service;

        public meryem AppService_Unit_Test()
        {
            _meryem RepoMock = Substitute.For<Imeryem Repository>();
            _eventBusMock = Substitute.For<IDistributedEventBus>();
            _meryem CacheMock = Substitute.For<IDistributedCache<List<meryem CacheItem>, string>>();
            _outboxRepoMock = Substitute.For<IRepository<OutboxMessage, Guid>>();

            var currentUserMock = Substitute.For<ICurrentUser>();
            currentUserMock.Id.Returns(Guid.NewGuid());

            var serviceProviderMock = Substitute.For<IServiceProvider>();
            serviceProviderMock.GetService(typeof(ICurrentUser)).Returns(currentUserMock);
            serviceProviderMock.GetService(typeof(IObjectMapper)).Returns(GetRequiredService<IObjectMapper>());
            serviceProviderMock.GetService(typeof(IGuidGenerator)).Returns(GetRequiredService<IGuidGenerator>());
            serviceProviderMock.GetService(typeof(IAsyncQueryableExecuter)).Returns(GetRequiredService<IAsyncQueryableExecuter>());

            var lazyProvider = new AbpLazyServiceProvider(serviceProviderMock);
            var manager = new meryem Manager(_meryem RepoMock) { LazyServiceProvider = lazyProvider };

            _service = new meryem AppService(
                _meryem RepoMock,
                _eventBusMock,
                manager,
                _meryem CacheMock,
                _outboxRepoMock
            ) { LazyServiceProvider = lazyProvider };
        }

        [Fact]
        public async Task CreateAsync_Should_Create_And_Trigger_Outbox()
        {
            var input = new Createmeryem Dto();
            var result = await _service.CreateAsync(input);

            result.ShouldNotBeNull();
            await _meryem RepoMock.ReceivedWithAnyArgs(1).InsertAsync(default!);
            await _outboxRepoMock.ReceivedWithAnyArgs(1).InsertAsync(default!);
            await _meryem CacheMock.ReceivedWithAnyArgs(1).RemoveAsync(default!);
        }
    }
}