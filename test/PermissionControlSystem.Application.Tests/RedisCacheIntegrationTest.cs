using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed; // Cache servisi
using Shouldly;
using Volo.Abp.Caching;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace PermissionControlSystem;

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
        // 1. Hazırlık: Rastgele bir anahtar ve değer belirle
        var cacheKey = "TestAnahtari_" + Guid.NewGuid();
        var cacheValue = "Merhaba Redis! 🚀";

        // 2. Eylem: Redis'e veriyi yaz (Set)
        await _distributedCache.SetStringAsync(cacheKey, cacheValue);

        // 3. Eylem: Redis'ten veriyi geri oku (Get)
        var retrievedValue = await _distributedCache.GetStringAsync(cacheKey);

        // 4. Doğrulama: Yazdığımızla okuduğumuz aynı mı?
        retrievedValue.ShouldNotBeNull();
        retrievedValue.ShouldBe(cacheValue);
    }
}