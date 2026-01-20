using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Xunit;

namespace PermissionControlSystem.Files
{
    public class BlobStoringTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        private readonly IBlobContainer _blobContainer;
    
    public BlobStoringTests()
        {
            _blobContainer = GetRequiredService<IBlobContainer>();
        }
        [Fact]
        public async Task Should_Save_And_Get_File()
        {
            //Arrange
            var fileName = "testfile.txt";
            var fileContent = "Bu bir test dosyasıdır.";
            var bytes = Encoding.UTF8.GetBytes(fileContent);

            //Act
            await _blobContainer.SaveAsync(fileName, bytes, overrideExisting: true);

            //Act
            var savedBytes = await _blobContainer.GetAllBytesOrNullAsync(fileName);

            //Assert
            savedBytes.ShouldNotBeNull();
            Encoding.UTF8.GetString(savedBytes).ShouldBe(fileContent);
        }
    }
}

