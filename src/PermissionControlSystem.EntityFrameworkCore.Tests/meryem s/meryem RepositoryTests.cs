using PermissionControlSystem.Entities;
using PermissionControlSystem.Interfaces;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

namespace PermissionControlSystem.EntityFrameworkCore.meryem s
{
    public partial class meryem RepositoryTests : PermissionControlSystemEntityFrameworkCoreTestBase
    {
        private readonly Imeryem Repository _meryem Repository;

        public meryem RepositoryTests()
        {
            _meryem Repository = GetRequiredService<Imeryem Repository>();
        }

        [Fact]
        public async Task GetListAsync_Should_Return_List()
        {
            await _meryem Repository.InsertAsync(new meryem (Guid.NewGuid()), autoSave: true);
            var result = await _meryem Repository.GetListAsync();
            result.Count.ShouldBeGreaterThan(0);
        }
    }
}