using PermissionControlSystem.Departments2;
using PermissionControlSystem.Leaves;
using PermissionControlSystem.Leaves2;
using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace PermissionControlSystem.Data
{
    public class PermissionControlSystemDataSeedContributor : IDataSeedContributor, ITransientDependency
    {
        private readonly IRepository<Department, Guid> _departmentRepository;
        private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;
        private readonly IGuidGenerator _guidGenerator;

        public PermissionControlSystemDataSeedContributor(
            IRepository<Department, Guid> departmentRepository,
            IRepository<LeaveType, Guid> leaveTypeRepository,
            IGuidGenerator guidGenerator)
        {
            _departmentRepository = departmentRepository;
            _leaveTypeRepository = leaveTypeRepository;
            _guidGenerator = guidGenerator;
        }

        public async Task SeedAsync(DataSeedContext context)
        {
            // 1. Departmanları Ekle (Eğer hiç yoksa)
            if (await _departmentRepository.GetCountAsync() <= 0)
            {
                await _departmentRepository.InsertAsync(
                    new Department(
                        _guidGenerator.Create(),
                        "Yazılım Geliştirme",
                        "Yazılım ve teknoloji ekibi"
                    )
                );

                await _departmentRepository.InsertAsync(
                    new Department(
                        _guidGenerator.Create(),
                        "İnsan Kaynakları",
                        "Personel yönetimi ve işe alım"
                    )
                );
            }

            // 2. İzin Türlerini Ekle (Eğer hiç yoksa)
            if (await _leaveTypeRepository.GetCountAsync() <= 0)
            {
                await _leaveTypeRepository.InsertAsync(
                    new LeaveType(
                        _guidGenerator.Create(),
                        "Yıllık İzin",
                        14 // Varsayılan 14 gün
                    )
                );

                await _leaveTypeRepository.InsertAsync(
                    new LeaveType(
                        _guidGenerator.Create(),
                        "Mazeret İzni",
                        3 // Varsayılan 3 gün
                    )
                );

                await _leaveTypeRepository.InsertAsync(
                    new LeaveType(
                        _guidGenerator.Create(),
                        "Sağlık Raporu",
                        0 // Limit yok anlamında
                    )
                );
            }
        }
    }
}