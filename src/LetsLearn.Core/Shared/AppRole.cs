using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Core.Shared
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Student = "Student";
        // Some existing accounts/frontends use "Learner" for the same learner role.
        public const string Learner = "Learner";
        public const string Teacher = "Teacher";

        public static readonly string[] All = new[] { Admin, Student , Teacher };

        public static bool IsStudent(string? role) =>
            string.Equals(role, Student, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, Learner, StringComparison.OrdinalIgnoreCase);
    }
}
