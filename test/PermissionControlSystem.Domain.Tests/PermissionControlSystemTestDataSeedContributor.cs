using PermissionControlSystem.Departments2;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace PermissionControlSystem
{
    public class PermissionControlSystemTestDataSeedContributor : IDataSeedContributor, ITransientDependency
    {
        private readonly IRepository<Department, Guid> _departmentRepository;


        public PermissionControlSystemTestDataSeedContributor(IRepository<Department,Guid> departmentRepository)
        {
            _departmentRepository = departmentRepository;
        }


        public async Task SeedAsync(DataSeedContext context)
        {
            if (await _departmentRepository.GetCountAsync() <= 0)
            {
                await _departmentRepository.InsertAsync(
                    new Department(
                        Guid.NewGuid(),
                        "Human Resources",
                        "All HR related matters"
                        )
                    );

                await _departmentRepository.InsertAsync(
                    new Department(
                        Guid.NewGuid(),
                        "IT",
                        "Information Technology Department"
                        )

                    );


            }
        }


    }
}
