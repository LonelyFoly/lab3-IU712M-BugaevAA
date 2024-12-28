
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace payment.DB
{
    using Microsoft.EntityFrameworkCore;

    public class ApplicationContext : DbContext
    {
        public DbSet<payment> payment { get; set; } = null!;
        private readonly string _connectionString;
        public ApplicationContext()
        {
            
            // Чтение строки подключения из переменной окружения
            string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                                //?? "Host=dpg-ctnh85lds78s73c4rho0-a.oregon-postgres.render.com;Port=5432;Database=postgre_afly;Username=program;Password=k6OUPizpZXrL6r0tRZ1ikZLg9bOxOkPK";
                                ?? "Host=postgres;Port=5432;Database=postgres;Username=program;Password=test";
            _connectionString = connectionString;
            Console.WriteLine($"Loaded connection string: {_connectionString}");
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString);
        }
    }
}

