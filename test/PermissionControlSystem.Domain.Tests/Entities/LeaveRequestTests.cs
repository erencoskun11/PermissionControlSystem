using PermissionControlSystem.Entities;
using PermissionControlSystem.Enums;
using PermissionControlSystem.Leaves;
using Shouldly;
using System;
using Xunit;

namespace PermissionControlSystem.Entities
{
    public class LeaveRequestTests
    {
        [Fact]
        public void Should_Create_LeaveRequest_With_Pending_Status()
        {
            // Arrange
            var id = Guid.NewGuid();
            var employeeId = Guid.NewGuid();
            var startDate = DateTime.Today.AddDays(1);
            var endDate = DateTime.Today.AddDays(5);
            var reason = "Yıllık İzin Talebi";

            // Act
            var leaveRequest = new LeaveRequest(id, employeeId, LeaveType.Annual, startDate, endDate, reason);

            // Assert
            leaveRequest.Id.ShouldBe(id);
            leaveRequest.EmployeeId.ShouldBe(employeeId);
            leaveRequest.LeaveType.ShouldBe(LeaveType.Annual);
            leaveRequest.StartDate.ShouldBe(startDate);
            leaveRequest.EndDate.ShouldBe(endDate);
            leaveRequest.Reason.ShouldBe(reason);

            // DDD İş Kuralı: Yeni oluşturulan bir izin DIŞARIDAN müdahale edilemez şekilde Pending başlamalıdır!
            leaveRequest.Status.ShouldBe(LeaveRequestStatus.Pending);
            leaveRequest.ManagerResponse.ShouldBeNull(); // Henüz yönetici yanıtı olmamalı
        }
    }
}