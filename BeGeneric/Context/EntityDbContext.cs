using BeGeneric.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace BeGeneric.Context
{
    public class EntityDbContext : DbContext
    {
        public EntityDbContext(DbContextOptions<EntityDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }
}
