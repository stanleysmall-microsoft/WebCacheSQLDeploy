using ContosoTeamStats.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ContosoTeamStats.Data;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;
using ContosoTeamStats;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ContosoTeamStatsContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ContosoTeamStatsContext") ?? throw new InvalidOperationException("Connection string 'COntosoTeamStatsContext' not found.")))
    .AddSingleton(async x => await RedisConnection.InitializeAsync(builder.Configuration["CacheConnection"].ToString()));

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    SeedData.Initialize(services);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Teams}/{action=Index}/{id?}");

app.Run();
