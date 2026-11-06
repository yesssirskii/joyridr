using Joyridr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joyridr.Data.Configurations;

public class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_user_blocks_not_self", "blocker_id <> blocked_id"));
        b.HasKey(x => new { x.BlockerId, x.BlockedId });
        b.HasOne(x => x.Blocker).WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Blocked).WithMany().HasForeignKey(x => x.BlockedId).OnDelete(DeleteBehavior.Cascade);
        // "Has anyone blocked me?" looks up by the blocked side.
        b.HasIndex(x => x.BlockedId);
    }
}

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> b)
    {
        b.Property(r => r.ContentSnapshot).HasMaxLength(2000);
        b.Property(r => r.Details).HasMaxLength(1000);
        b.Property(r => r.Resolution).HasMaxLength(500);
        b.HasOne(r => r.Reporter).WithMany().HasForeignKey(r => r.ReporterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.ReportedUser).WithMany().HasForeignKey(r => r.ReportedUserId).OnDelete(DeleteBehavior.Cascade);
        // Removing the reported message or review keeps the report (with its snapshot).
        b.HasOne(r => r.Message).WithMany().HasForeignKey(r => r.MessageId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(r => r.Review).WithMany().HasForeignKey(r => r.ReviewId).OnDelete(DeleteBehavior.SetNull);
        // The admin queue: open reports, oldest first.
        b.HasIndex(r => new { r.Status, r.CreatedAt });
        b.HasIndex(r => r.ReportedUserId);
    }
}
