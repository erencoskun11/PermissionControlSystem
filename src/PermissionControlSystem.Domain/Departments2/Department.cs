using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.Domain.Entities.Auditing;

namespace PermissionControlSystem.Departments2
{
    public class Department : FullAuditedAggregateRoot<Guid>
    {
        public string Name { get; set; }
        public string Description { get; set; }


        protected Department()
        {
            

            
        }
        public Department(Guid id, string name, string description) : base(id)
        {
            Name = name;
            Description = description;
        }



    }
}
