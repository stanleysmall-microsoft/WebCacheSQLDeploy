using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ContosoTeamStats.Data;
using ContosoTeamStats.Models;
using System.Configuration;
using StackExchange.Redis;
using ContosoTeamStats;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.IdentityModel.Tokens;

namespace ContosoTeamStats.Controllers
{
    public class TeamsController : Controller
    {
        private readonly ContosoTeamStatsContext _context;
        private readonly Task<RedisConnection> _redisConnectionFactory;
        private RedisConnection _redisConnection;

        public TeamsController(ContosoTeamStatsContext context, Task<RedisConnection> redisConnectionFactory)
        {
            _context = context;
            _redisConnectionFactory = redisConnectionFactory;
        }

        // GET: Teams
        public async Task<IActionResult> Index(string actionType, string resultType)
        {
            _redisConnection = await _redisConnectionFactory;
            List<Team> teams = null;

            switch (actionType)
            {
                case "playGames": // Play a new season of games.
                    PlayGames();
                    break;

                case "clearCache": // Clear the results from the cache.
                    ClearCachedTeams();
                    break;

                case "rebuildDB": // Rebuild the database with sample data.
                    RebuildDB();
                    break;
            }

            // Measure the time it takes to retrieve the results.
            Stopwatch sw = Stopwatch.StartNew();

            switch (resultType)
            {
                case "teamsSortedSet": // Retrieve teams from sorted set.
                    teams = GetFromSortedSet();
                    break;

                case "teamsSortedSetTop5": // Retrieve the top 5 teams from the sorted set.
                    teams = GetFromSortedSetTop5();
                    break;

                case "teamsList": // Retrieve teams from the cached List<Team>.
                    teams = GetFromList();
                    break;

                case "fromDB": // Retrieve results from the database.
                default:
                    teams = GetFromDB();
                    break;
            }

            sw.Stop();
            double ms = sw.ElapsedTicks / (Stopwatch.Frequency / (1000.0));

            // Add the elapsed time of the operation to the ViewBag.msg.
            ViewBag.msg += " MS: " + ms.ToString();

            return View(teams);
        }

        void PlayGames()
        {
            ViewBag.msg += "Updating team statistics. ";
            // Play a "season" of games.
            var teams = from t in _context.Team
                        select t;

            Team.PlayGames(teams);

            _context.SaveChanges();

            // Clear any cached results
            ClearCachedTeams();
        }

        void RebuildDB()
        {
            ViewBag.msg += "Rebuilding DB. ";
            // Delete and re-initialize the database with sample data.
            _context.Team.ExecuteDeleteAsync();
            //db.Database.Delete();
            //db.Database.Initialize(true);

            // Clear any cached results
            ClearCachedTeams();
        }

        async void ClearCachedTeams()
        {
            await _redisConnection.BasicRetryAsync(async (db) => await db.KeyDeleteAsync("teamsList"));
            await _redisConnection.BasicRetryAsync(async (db) => await db.KeyDeleteAsync("teamsSortedSet"));
            ViewBag.msg += "Team data removed from cache. ";
        }

        List<Team> GetFromDB()
        {
            ViewBag.msg += "Results read from DB. ";
            var results = from t in _context.Team
                          orderby t.Wins descending
                          select t;

            return results.ToList();
        }

        List<Team> GetFromList()
        {
            List<Team> teams = null;


            //string serializedTeams = _redisConnection.BasicRetryAsync(async (db) => await db.StringGetAsync("teamsList"));

            string serializedTeams = _redisConnection.BasicRetryAsync(async (db) => await db.StringGetAsync("teamsList")).Result;

            if (!serializedTeams.IsNullOrEmpty())
            {
                teams = JsonConvert.DeserializeObject<List<Team>>(serializedTeams);

                ViewBag.msg += "List read from cache. ";
            }
            else
            {
                ViewBag.msg += "Teams list cache miss. ";
                // Get from database and store in cache
                teams = GetFromDB();

                ViewBag.msg += "Storing results to cache. ";

                (_redisConnection.BasicRetryAsync((db) => db.StringSetAsync("teamsList", JsonConvert.SerializeObject(teams)))).Wait();
            }
            return teams;
        }

        List<Team> GetFromSortedSet()
        {
            List<Team> teams = null;
            //IDatabase cache = Connection.GetDatabase();
            // If the key teamsSortedSet is not present, this method returns a 0 length collection.

            var teamsSortedSet = (_redisConnection.BasicRetryAsync(async (db) => await db.SortedSetRangeByRankWithScoresAsync("teamsSortedSet", order: Order.Descending))).Result;

            if (teamsSortedSet.Count() > 0)
            {
                ViewBag.msg += "Reading sorted set from cache. ";
                teams = new List<Team>();
                foreach (var t in teamsSortedSet)
                {
                    Team tt = JsonConvert.DeserializeObject<Team>(t.Element);
                    teams.Add(tt);
                }
            }
            else
            {
                ViewBag.msg += "Teams sorted set cache miss. ";

                // Read from DB
                teams = GetFromDB();

                ViewBag.msg += "Storing results to cache. ";
                foreach (var t in teams)
                {
                    Console.WriteLine("Adding to sorted set: {0} - {1}", t.Name, t.Wins);
                    _redisConnection.BasicRetryAsync(async (db) => await db.SortedSetAddAsync("teamsSortedSet", JsonConvert.SerializeObject(t), t.Wins)).Wait();
                }
            }
            return teams;
        }

        List<Team> GetFromSortedSetTop5()
        {
            List<Team> teams = null;

            // If the key teamsSortedSet is not present, this method returns a 0 length collection.
            var teamsSortedSet = _redisConnection.BasicRetryAsync(async (db) => await db.SortedSetRangeByRankWithScoresAsync("teamsSortedSet", stop: 4, order: Order.Descending)).Result;

            if (teamsSortedSet.Count() == 0)
            {
                // Load the entire sorted set into the cache.
                GetFromSortedSet();

                // Retrieve the top 5 teams.
                teamsSortedSet = _redisConnection.BasicRetryAsync(async (db) => await db.SortedSetRangeByRankWithScoresAsync("teamsSortedSet", stop: 4, order: Order.Descending)).Result;



            }

            ViewBag.msg += "Retrieving top 5 teams from cache. ";
            // Get the top 5 teams from the sorted set
            teams = new List<Team>();
            foreach (var team in teamsSortedSet)
            {
                teams.Add(JsonConvert.DeserializeObject<Team>(team.Element));
            }
            return teams;
        }

        // GET: Teams/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Team == null)
            {
                return NotFound();
            }

            var team = await _context.Team
                .FirstOrDefaultAsync(m => m.ID == id);
            if (team == null)
            {
                return NotFound();
            }

            return View(team);
        }

        // GET: Teams/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Teams/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,Wins,Losses,Ties")] Team team)
        {
            if (ModelState.IsValid)
            {
                _context.Add(team);
                await _context.SaveChangesAsync();
                ClearCachedTeams();
                return RedirectToAction(nameof(Index));
            }
            return View(team);
        }

        // GET: Teams/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Team == null)
            {
                return NotFound();
            }

            var team = await _context.Team.FindAsync(id);
            if (team == null)
            {
                return NotFound();
            }
            return View(team);
        }

        // POST: Teams/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Name,Wins,Losses,Ties")] Team team)
        {
            if (id != team.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(team);
                    await _context.SaveChangesAsync();
                    ClearCachedTeams();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TeamExists(team.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(team);
        }

        // GET: Teams/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Team == null)
            {
                return NotFound();
            }

            var team = await _context.Team
                .FirstOrDefaultAsync(m => m.ID == id);
            if (team == null)
            {
                return NotFound();
            }

            return View(team);
        }

        // POST: Teams/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Team == null)
            {
                return Problem("Entity set 'ContosoTeamStatsContext.Team'  is null.");
            }
            var team = await _context.Team.FindAsync(id);
            if (team != null)
            {
                _context.Team.Remove(team);
            }
            
            await _context.SaveChangesAsync();
            ClearCachedTeams();
            return RedirectToAction(nameof(Index));
        }

        private bool TeamExists(int id)
        {
          return _context.Team.Any(e => e.ID == id);
        }
    }
}
