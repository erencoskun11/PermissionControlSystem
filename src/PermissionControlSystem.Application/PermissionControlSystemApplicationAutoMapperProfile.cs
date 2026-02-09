using AutoMapper;
using PermissionControlSystem.AppUsers.Dtos;
using PermissionControlSystem.Departments;
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Employees;
using PermissionControlSystem.Employees.Dtos;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Leave;
using PermissionControlSystem.Leave.Dtos;
using PermissionControlSystem.Leaves;
using PermissionControlSystem.Users;
using System;

namespace PermissionControlSystem
{
    public class PermissionControlSystemApplicationAutoMapperProfile : Profile
    {
        public PermissionControlSystemApplicationAutoMapperProfile()
        {
            // --- Departmanlar ---
            CreateMap<Department, DepartmentDto>();
            CreateMap<Department, DepartmentListDto>();
            CreateMap<CreateDepartmentDto, Department>();
            CreateMap<UpdateDepartmentDto, Department>();

            // --- İzinler (Leaves) ---
            CreateMap<LeaveRequest, LeaveRequestDto>();
            CreateMap<LeaveRequest, LeaveRequestListDto>();

            CreateMap<CreateLeaveRequestDto, LeaveRequest>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => LeaveRequestStatus.Pending))
                .ForMember(dest => dest.ManagerResponse, opt => opt.Ignore());

            CreateMap<UpdateLeaveRequestDto, LeaveRequest>();

            // --- AppUsers ---
            CreateMap<AppUser, AppUserDto>();

            // --- Employees ---
            CreateMap<Employee, EmployeeDto>()
                .ForMember(dest => dest.DepartmentName,
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : null));

            CreateMap<CreateEmployeeDto, Employee>()
                .ConstructUsing(src => new Employee(
                    Guid.NewGuid(),      // 1. Id
                    src.UserId,          // 2. UserId
                    src.DepartmentId,    // 3. DepartmentId
                    src.FirstName,       // 4. FirstName
                    src.LastName,        // 5. LastName (Eksik olan buydu)
                    src.Email,           // 6. Email
                    src.PhoneNumber,       // 7. PhoneNumber
                    src.Position         // 7. Position
                ))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber));

            CreateMap<UpdateEmployeeDto, Employee>();
        }
    }
}