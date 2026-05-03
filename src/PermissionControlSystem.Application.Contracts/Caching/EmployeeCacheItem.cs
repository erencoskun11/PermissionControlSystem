using System;

namespace PermissionControlSystem.Caching
{
    public class EmployeeCacheItem
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid DepartmentId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string ConcurrencyStamp { get; set; } = string.Empty;

        public EmployeeCacheItem()
        {
        }

        public EmployeeCacheItem(
            Guid id,
            Guid userId,
            Guid departmentId,
            string firstName,
            string lastName,
            string fullName,
            string position,
            string email,
            string phoneNumber,
            string departmentName,
            string concurrencyStamp)
        {
            Id = id;
            UserId = userId;
            DepartmentId = departmentId;
            FirstName = firstName ?? string.Empty;
            LastName = lastName ?? string.Empty;
            FullName = fullName ?? string.Empty;
            Position = position ?? string.Empty;
            Email = email ?? string.Empty;
            PhoneNumber = phoneNumber ?? string.Empty;
            DepartmentName = departmentName ?? string.Empty;
            ConcurrencyStamp = concurrencyStamp ?? string.Empty;
        }
    }
}
