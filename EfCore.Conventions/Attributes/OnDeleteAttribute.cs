using System;
using Microsoft.EntityFrameworkCore;

namespace EfCore.Conventions.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class OnDeleteAttribute : Attribute
    {
        public DeleteBehavior Behavior { get; set; }

        public OnDeleteAttribute(DeleteBehavior behavior)
        {
            this.Behavior = behavior;
        }
    }
}