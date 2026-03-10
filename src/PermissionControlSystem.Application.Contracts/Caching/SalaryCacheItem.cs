using System;

namespace PermissionControlSystem.Caching
{
    public class SalaryCacheItem
    {
        public Guid Id { get; set; }
        public string? Name { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;

        public SalaryCacheItem() { }

        public SalaryCacheItem(Guid id, string? name, string? description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }
}