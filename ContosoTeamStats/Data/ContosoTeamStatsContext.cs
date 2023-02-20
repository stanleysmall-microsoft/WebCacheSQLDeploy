using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ContosoTeamStats.Models;

namespace ContosoTeamStats.Data
{
    public class ContosoTeamStatsContext : DbContext
    {
        public ContosoTeamStatsContext (DbContextOptions<ContosoTeamStatsContext> options)
            : base(options)
        {
        }

        public DbSet<ContosoTeamStats.Models.Team> Team { get; set; } = default!;
    }
}
