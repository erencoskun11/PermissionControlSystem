using AutoMapper;
using PermissionControlSystem.AppUsers.Dtos;
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Departments2;
using PermissionControlSystem.Employees;
using PermissionControlSystem.Employees.Dtos;
using PermissionControlSystem.Leaves;
using PermissionControlSystem.Leaves.Dtos;
using PermissionControlSystem.Leaves2;
using PermissionControlSystem.Users;
using System;

namespace PermissionControlSystem
{
    public class PermissionControlSystemApplicationAutoMapperProfile : Profile
    {
        public PermissionControlSystemApplicationAutoMapperProfile()
        {
            /* --- Departmanlar --- */
            CreateMap<Department, DepartmentDto>();
            CreateMap<Department, DepartmentListDto>();
            CreateMap<CreateDepartmentDto, Department>();
            CreateMap<UpdateDepartmentDto, Department>();

            /* --- İzinler (Leaves) --- */
            // 1. Entity -> DTO
            CreateMap<LeaveRequest, LeaveRequestDto>();
            CreateMap<LeaveRequest, LeaveRequestListDto>();

            // 2. Create -> Entity
            CreateMap<CreateLeaveRequestDto, LeaveRequest>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => LeaveRequestStatus.Pending))
                .ForMember(dest => dest.ManagerResponse, opt => opt.Ignore());

            // 3. Update -> Entity
            CreateMap<UpdateLeaveRequestDto, LeaveRequest>();

            /* --- AppUsers (Eğer kullanıyorsan) --- */
            // Not: AppUserDto ve diğerleri tanımlıysa burası kalabilir.
            // Eğer yoksa hata verir, proje içinde kontrol et.
            // CreateMap<AppUser, AppUserDto>();
            // CreateMap<CreateAppUserDto, AppUser>();
            // CreateMap<UpdateAppUserDto, AppUser>();

            /* --- Employees (ÇALIŞANLAR) --- */

            // 1. Entity -> DTO (Listeleme)
            // Department null gelirse hata vermemesi için kontrol ekli
            CreateMap<Employee, EmployeeDto>()
                .ForMember(dest => dest.DepartmentName,
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : null));

            // Not: EmployeeListDto diye bir dosya oluşturmadıysak 
            // aşağıdaki bloğu silebilirsin veya EmployeeDto kullanabilirsin.
            // CreateMap<Employee, EmployeeListDto>() ...

            // 2. Create -> Entity (Kayıt)
            // 👇 KRİTİK DÜZELTME BURADA 👇
            CreateMap<CreateEmployeeDto, Employee>()
                .ConstructUsing(src => new Employee(
                    Guid.NewGuid(),      // ID
                    src.UserId,          // User ID
                    src.DepartmentId,    // Dept ID
                    src.FullName,        // Ad Soyad
                    src.Email,           // Email
                    src.PhoneNumber      // Tel
                ))
                // Position, Constructor'da olmadığı için onu ayrıca setliyoruz:
                .ForMember(dest => dest.Position, opt => opt.MapFrom(src => src.Position));

            // 3. Update -> Entity (Güncelleme)
            CreateMap<UpdateEmployeeDto, Employee>();
        }
    }
}