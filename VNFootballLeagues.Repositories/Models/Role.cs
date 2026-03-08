using System;
using System.Collections.Generic;

namespace VNFootballLeagues.Repositories.Models;

public partial class Role
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}