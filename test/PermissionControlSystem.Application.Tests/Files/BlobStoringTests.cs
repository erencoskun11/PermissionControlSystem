using System.IO;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.BlobStoring;
using Xunit;

namespace PermissionControlSystem.Files
{
    public class BlobStoringTests : PermissionControlSystemApplicationTestBase<PermissionControlSystemApplicationTestModule>
    {
        [Fact]
        public async Task Should_Save_And_Get_File()
        {
            var fakeBlobContainer = Substitute.For<IBlobContainer>();

            var fileName = "testfile.txt";
            var fileContent = "Bu bir test dosyasıdır.";
            var bytes = Encoding.UTF8.GetBytes(fileContent);

            fakeBlobContainer.GetOrNullAsync(fileName).Returns(Task.FromResult<Stream>(new MemoryStream(bytes)));

            await fakeBlobContainer.SaveAsync(fileName, bytes, overrideExisting: true);

            var savedBytes = await fakeBlobContainer.GetAllBytesOrNullAsync(fileName);

            savedBytes.ShouldNotBeNull();
            Encoding.UTF8.GetString(savedBytes).ShouldBe(fileContent);
        }
    }
}