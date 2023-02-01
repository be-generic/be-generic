using BeGeneric.Models;
using BeGeneric.Services.BeGeneric;
using Microsoft.EntityFrameworkCore;

namespace BeGeneric.Context
{
    public class ControllerDbContext : DbContext
    {
        internal const string GENERIC_SCHEMA = "gba";

        public ControllerDbContext(DbContextOptions<ControllerDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Property>()
                .HasOne(p => p.Entity)
                .WithMany(b => b.Properties);

            modelBuilder.Entity<Property>()
                .HasOne(p => p.ReferencingEntity)
                .WithMany(b => b.ReferencingProperties);

            modelBuilder.Entity<ColumnMetadata>()
                .HasKey(cm => new { cm.TableName, cm.ColumnName });

            modelBuilder.Entity<EntityRole>()
                .HasOne(p => p.Entity)
                .WithMany(b => b.EntityRoles);

            modelBuilder.Entity<EntityRole>()
                .HasKey(cm => new { cm.EntitiesEntityId, cm.RolesId });

            modelBuilder.Entity<EntityRelation>()
                .HasOne(p => p.Entity1)
                .WithMany(b => b.EntityRelations1);

            modelBuilder.Entity<EntityRelation>()
                .HasOne(p => p.Entity2)
                .WithMany(b => b.EntityRelations2);

            modelBuilder.Entity<Endpoint>()
                .HasOne(p => p.StartingEntity)
                .WithMany(b => b.Endpoints);

            modelBuilder.Entity<EndpointProperty>()
                .HasOne(p => p.Endpoint)
                .WithMany(b => b.EndpointProperties);

            modelBuilder.Entity<Entity>().ToTable("Entities", GENERIC_SCHEMA);
            modelBuilder.Entity<EntityRole>().ToTable("EntityRole", GENERIC_SCHEMA);
            modelBuilder.Entity<EntityRelation>().ToTable("EntityRelation", GENERIC_SCHEMA);
            modelBuilder.Entity<Property>().ToTable("Properties", GENERIC_SCHEMA);
            modelBuilder.Entity<ColumnMetadata>().ToTable("ColumnMetadata", GENERIC_SCHEMA);
            modelBuilder.Entity<Endpoint>().ToTable("Endpoint", GENERIC_SCHEMA);
            modelBuilder.Entity<EndpointProperty>().ToTable("EndpointProperty", GENERIC_SCHEMA);

            modelBuilder.Entity<Account>().ToTable("Accounts", GenericDataService.SCHEMA);
            modelBuilder.Entity<Role>().ToTable("Roles", GenericDataService.SCHEMA);
            modelBuilder.Entity<ResetPassword>().ToTable("ResetPassword", GenericDataService.SCHEMA);
        }

        public DbSet<Property> Properties { get; set; }

        public DbSet<Entity> Entities { get; set; }

        public DbSet<Account> Accounts { get; set; }

        public DbSet<Role> Roles { get; set; }

        public DbSet<ColumnMetadata> ColumnMetadatas { get; set; }

        public DbSet<EntityRelation> EntityRelations { get; set; }

        public DbSet<Endpoint> Endpoints { get; set; }

        public DbSet<ResetPassword> ResetPasswords { get; set; }
    }
}
