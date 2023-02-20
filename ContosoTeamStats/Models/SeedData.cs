using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ContosoTeamStats.Data;
using System;
using System.Linq;
using System.Collections.Generic;

namespace ContosoTeamStats.Models;
public class SeedData
{
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using (var context = new ContosoTeamStatsContext(
            serviceProvider.GetRequiredService<
                DbContextOptions<ContosoTeamStatsContext>>()))
        {
            if (context.Team.Any())
            {
                return;   // DB has been seeded
            }
            var teams = new List<Team>
            {
            new Team{Name="Adventure Works Cycles"},
            new Team{Name="Alpine Ski House"},
            new Team{Name="Blue Yonder Airlines"},
            new Team{Name="Coho Vineyard"},
            new Team{Name="Contoso, Ltd."},
            new Team{Name="Fabrikam, Inc."},
            new Team{Name="Lucerne Publishing"},
            new Team{Name="Northwind Traders"},
            new Team{Name="Consolidated Messenger"},
            new Team{Name="Fourth Coffee"},
            new Team{Name="Graphic Design Institute"},
            new Team{Name="Nod Publishers"}
            };

            Team.PlayGames(teams);

            teams.ForEach(t => context.Team.Add(t));

            context.SaveChanges();
        }
    }
}

