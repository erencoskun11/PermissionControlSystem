using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Shouldly;
using Xunit;

namespace PermissionControlSystem
{
    [Collection(PermissionControlSystemTestConsts.CollectionDefinitionName)]
    public class RedisCacheIntegrationTest : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IDistributedCache _distributedCache;

        public RedisCacheIntegrationTest()
        {
            _distributedCache = GetRequiredService<IDistributedCache>();
        }

        [Fact]
        public async Task Should_Write_And_Read_From_Redis()
        {
            var cacheKey = "TestAnahtari_" + Guid.NewGuid();
            var cacheValue = "Merhaba Redis! 🚀";

            await _distributedCache.SetStringAsync(cacheKey, cacheValue);
            var retrievedValue = await _distributedCache.GetStringAsync(cacheKey);

            retrievedValue.ShouldNotBeNull();
            retrievedValue.ShouldBe(cacheValue);
        }
    }
}