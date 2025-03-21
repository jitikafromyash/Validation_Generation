using Microsoft.EntityFrameworkCore;

namespace WebApp_MVC_.Models
{
    public class AppDbContext : DbContext
    {
        // Constructor to pass the options to the base DbContext class
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Represents the Users table in the database
        public DbSet<User> Users { get; set; }
    }
}
