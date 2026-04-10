using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VNFootballLeagues.Repositories.Models
{
    public partial class UserFavoritePlayer
    {
        public Guid FavoriteId { get; set; }
        public Guid UserId { get; set; }
        public int PlayerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public virtual User User { get; set; }
        public virtual Player Player { get; set; }
    }
}
