using System.Collections.Generic;
using System.IO.Compression;
using System;
using Microsoft.EntityFrameworkCore;
using Plugin.Models;

namespace Plugin.Data
{
    public class PluginDbContext : DbContext
    {
        public PluginDbContext(DbContextOptions<PluginDbContext> options)
        : base(options)
        {
        }

        public DbSet<PluginFile> File { get; set; }
    }
}
