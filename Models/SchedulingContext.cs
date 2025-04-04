using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Scheduling.Models;

public partial class SchedulingContext : DbContext
{
    public SchedulingContext()
    {
    }

    public SchedulingContext(DbContextOptions<SchedulingContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Allocation> Allocations { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<DaysOfWeek> DaysOfWeeks { get; set; }

    public virtual DbSet<Instructor> Instructors { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<Section> Sections { get; set; }

    public virtual DbSet<Table1> Table1s { get; set; }

    public virtual DbSet<TimeSlot> TimeSlots { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("name=SchedulingConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Allocation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_users");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Course).WithMany(p => p.Allocations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Allocations_Courses");

            entity.HasOne(d => d.Instructor).WithMany(p => p.Allocations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Allocations_Instructors");

            entity.HasOne(d => d.Section).WithMany(p => p.Allocations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Allocations_Sections");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<DaysOfWeek>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_tenats");

            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Instructor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_floors");

            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Allocation).WithMany(p => p.Schedules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Schedules_Allocations");

            entity.HasOne(d => d.TimeSlot).WithMany(p => p.Schedules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Schedules_TimeSlots");
        });

        modelBuilder.Entity<Section>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_rooms");

            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<TimeSlot>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Category).WithMany(p => p.TimeSlots)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TimeSlots_Categories");

            entity.HasOne(d => d.DaysOfWeek).WithMany(p => p.TimeSlots)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TimeSlots_DaysOfWeek");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
