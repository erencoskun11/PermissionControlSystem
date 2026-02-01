using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.BlobStoring;

namespace PermissionControlSystem.Controllers
{
    [Route("api/file")]
    public class FileController : AbpController
    {
        private readonly IBlobContainer _blobContainer;

        public FileController(IBlobContainer blobContainer)
        {
            _blobContainer = blobContainer;
        }

        [HttpPost]
        [Route("upload")]
        public async Task<string> UploadAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new UserFriendlyException("Dosya seçilmedi!");
            }

            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                // Dosyayı Blob Storing sistemine kaydet (Veritabanına yazar)
                await _blobContainer.SaveAsync(file.FileName, fileBytes, overrideExisting: true);
            }

            return $"Dosya başarıyla yüklendi: {file.FileName}";
        }

        [HttpGet]
        [Route("download/{fileName}")]
        public async Task<IActionResult> DownloadAsync(string fileName)
        {
            var fileBytes = await _blobContainer.GetAllBytesOrNullAsync(fileName);

            if (fileBytes == null)
            {
                return NotFound("Dosya bulunamadı.");
            }

            return File(fileBytes, "application/octet-stream", fileName);
        }
    }
}