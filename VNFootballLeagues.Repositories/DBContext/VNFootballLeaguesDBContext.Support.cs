using Microsoft.EntityFrameworkCore;

namespace VNFootballLeagues.Repositories.Models;

public partial class VNFootballLeaguesDBContext
{
    public virtual DbSet<SupportTicket> SupportTickets { get; set; }
    public virtual DbSet<SupportMessage> SupportMessages { get; set; }
}
