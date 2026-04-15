using PermissionControlSystem.Departments.Dtos;
using PermissionControlSystem.Employees.Dtos;
using PermissionControlSystem.Leave.Dtos;
using PermissionControlSystem.Models;
using PermissionControlSystem.Statistics.Dtos;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PermissionControlSystem.Services
{
    public interface IElasticSearchService
    {
        // --- İzin İşlemleri ---
        Task IndexLeaveRequestAsync(LeaveIndexModel model, CancellationToken cancellationToken = default);
        Task<List<LeaveIndexModel>> SearchLeaveRequestAsync(string keyword, CancellationToken cancellationToken = default);
        Task UpdateLeaveRequestEmployeeNameAsync(Guid employeeId, string newEmployeeName, CancellationToken cancellationToken = default);
        Task UpdateLeaveRequestEmployeeDetailsAsync(Guid employeeId, string newName, string newDeptName, CancellationToken cancellationToken = default);
        Task UpdateLeaveRequestDepartmentNameByEmployeeIdAsync(Guid employeeId, string newDepartmentName, CancellationToken cancellationToken = default);
        Task UpdateLeaveRequestDepartmentNameByDepartmentIdAsync(Guid departmentId, string newDepartmentName, CancellationToken cancellationToken = default);
        Task DeleteLeaveRequestAsync(Guid id, CancellationToken cancellationToken = default);

        // --- Departman İşlemleri ---
        Task IndexDepartmentAsync(Guid id, string name, string description, CancellationToken cancellationToken = default);
        Task<List<DepartmentIndexModel>> SearchDepartmentAsync(string keyword, CancellationToken cancellationToken = default);
        Task DeleteDepartmentAsync(Guid id, CancellationToken cancellationToken = default);

        // --- Personel İşlemleri ---
        Task IndexEmployeeAsync(Guid id, Guid departmentId, string departmentName, string fullName, string position, string email, CancellationToken cancellationToken = default);
        Task DeleteEmployeeAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<EmployeeDto>> SearchEmployeeAsync(string keyword, CancellationToken cancellationToken = default);
        Task UpdateEmployeeDepartmentNameAsync(Guid departmentId, string newDepartmentName, CancellationToken cancellationToken = default);

        // --- İstatistik İşlemleri ---
        Task<StatisticsOverviewDto> GetOverviewFromElasticAsync(CancellationToken cancellationToken = default);
        Task<List<DepartmentLeaveStatDto>> GetDepartmentStatsFromElasticAsync(CancellationToken cancellationToken = default);
        Task<List<LeaveTypeStatDto>> GetLeaveTypeStatsFromElasticAsync(CancellationToken cancellationToken = default);
        Task<List<TopEmployeeStatDto>> GetTopEmployeesFromElasticAsync(CancellationToken cancellationToken = default);
        Task<List<RejectedEmployeeStatDto>> GetMostRejectedEmployeesFromElasticAsync(CancellationToken cancellationToken = default);
        Task<List<MonthlyLeaveStatDto>> GetMonthlyLeavesFromElasticAsync(CancellationToken cancellationToken = default);
        Task<List<OldestPendingLeaveStatDto>> GetOldestPendingLeavesFromElasticAsync(CancellationToken cancellationToken = default);

        // --- Toplu Senkronizasyon (Bulk Index) ---
        Task BulkIndexDepartmentsAsync(List<DepartmentIndexModel> departments, CancellationToken cancellationToken = default);
        Task BulkIndexEmployeesAsync(List<EmployeeIndexModel> employees, CancellationToken cancellationToken = default);
        Task BulkIndexLeaveRequestsAsync(List<LeaveIndexModel> leaveRequests, CancellationToken cancellationToken = default);
    }
}