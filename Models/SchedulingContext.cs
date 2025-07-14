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

    public virtual DbSet<AdmissionType> AdmissionTypes { get; set; }

    public virtual DbSet<AdmissionTypeCategory> AdmissionTypeCategories { get; set; }

    public virtual DbSet<AdmissionTypeDay> AdmissionTypeDays { get; set; }

    public virtual DbSet<Allocation> Allocations { get; set; }

    public virtual DbSet<Batch> Batchs { get; set; }

    public virtual DbSet<Block> Blocks { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<DaysOfWeek> DaysOfWeeks { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<DepartmentAdmin> DepartmentAdmins { get; set; }

    public virtual DbSet<Gender> Genders { get; set; }

    public virtual DbSet<Instructor> Instructors { get; set; }

    public virtual DbSet<Program> Programs { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<ScheduleSetting> ScheduleSettings { get; set; }

    public virtual DbSet<Section> Sections { get; set; }

    public virtual DbSet<Ssbatch> Ssbatchs { get; set; }

    public virtual DbSet<SstimeSlot> SstimeSlots { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<TimeSlot> TimeSlots { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("name=SchedulingConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdmissionType>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<AdmissionTypeCategory>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.AdmissionType).WithMany(p => p.AdmissionTypeCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdmissionTypeCategories_AdmissionTypes");

            entity.HasOne(d => d.Category).WithMany(p => p.AdmissionTypeCategories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdmissionTypeCategories_Categories");
        });

        modelBuilder.Entity<AdmissionTypeDay>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.AdmissionType).WithMany(p => p.AdmissionTypeDays)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdmissionTypeDays_AdmissionTypes");

            entity.HasOne(d => d.DayOfWeek).WithMany(p => p.AdmissionTypeDays)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdmissionTypeDays_DaysOfWeek");
        });

        modelBuilder.Entity<Allocation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Allocation");

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

        modelBuilder.Entity<Batch>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.AdmissionTypes).WithMany(p => p.Batches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Batchs_AdmissionTypes");

            entity.HasOne(d => d.Programs).WithMany(p => p.Batches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Batchs_Programs");
        });

        modelBuilder.Entity<Block>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
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

        modelBuilder.Entity<Department>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<DepartmentAdmin>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Department).WithMany(p => p.DepartmentAdmins)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentAdmins_Departments");

            entity.HasOne(d => d.User).WithMany(p => p.DepartmentAdmins)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DepartmentAdmins_Users");
        });

        modelBuilder.Entity<Gender>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Instructor>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_floors");

            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Program>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Rooms_1");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Block).WithMany(p => p.Rooms)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Rooms_Blocks");
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Schedule__3214EC07E140CA54");

            entity.HasOne(d => d.Allocation).WithMany(p => p.Schedules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Schedule__Alloca__2CF2ADDF");

            entity.HasOne(d => d.Room).WithMany(p => p.Schedules).HasConstraintName("FK_Schedules_Rooms");

            entity.HasOne(d => d.Ss).WithMany(p => p.Schedules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Schedules_ScheduleSettings");

            entity.HasOne(d => d.TimeSlot).WithMany(p => p.Schedules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Schedule__TimeSl__2DE6D218");
        });

        modelBuilder.Entity<ScheduleSetting>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Department).WithMany(p => p.ScheduleSettings)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ScheduleSettings_Departments");
        });

        modelBuilder.Entity<Section>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_rooms");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Batch).WithMany(p => p.Sections)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sections_Batchs");

            entity.HasOne(d => d.Department).WithMany(p => p.Sections)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sections_Departments");

            entity.HasOne(d => d.Room).WithMany(p => p.Sections).HasConstraintName("FK_Sections_Rooms");
        });

        modelBuilder.Entity<Ssbatch>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Ss).WithMany(p => p.Ssbatches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SSBatchs_ScheduleSettings");
        });

        modelBuilder.Entity<SstimeSlot>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.Category).WithMany(p => p.SstimeSlots)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SSTimeSlots_Categories");

            entity.HasOne(d => d.Ss).WithMany(p => p.SstimeSlots)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SSTimeSlots_ScheduleSettings");
        });

        modelBuilder.Entity<Student>(entity =>
        {
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

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Gender).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Gender");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Roles");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
