using AutoMapper;
using PermissionControlSystem.Departments;
using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Departments2;
using PermissionControlSystem.Leaves;
using PermissionControlSystem.Leaves.Dtos;
using PermissionControlSystem.Leaves2;

namespace PermissionControlSystem;

public class PermissionControlSystemApplicationAutoMapperProfile : Profile
{
    public PermissionControlSystemApplicationAutoMapperProfile()
    {
        /* 1. Departman Eşleşmeleri */
        CreateMap<Department, DepartmentDto>();
        CreateMap<Department, DepartmentListDto>();
        CreateMap<CreateDepartmentDto, Department>();
        CreateMap<UpdateDepartmentDto, Department>();

        /* 2. İzin Talebi (LeaveRequest) Eşleşmeleri */
        CreateMap<LeaveRequest, LeaveRequestDto>();
        CreateMap<LeaveRequest, LeaveRequestListDto>();
        CreateMap<CreateLeaveRequestDto, LeaveRequest>();
        CreateMap<UpdateLeaveRequestDto, LeaveRequest>();
    }
}