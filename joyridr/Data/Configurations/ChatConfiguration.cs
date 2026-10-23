using Joyridr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joyridr.Data.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_conversations_participants", "rider_id <> driver_id"));
        b.HasOne(c => c.Rider).WithMany().HasForeignKey(c => c.RiderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.Driver).WithMany().HasForeignKey(c => c.DriverId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(c => new { c.RiderId, c.DriverId }).IsUnique();
        b.HasIndex(c => new { c.DriverId, c.LastMessageAt });
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.Property(m => m.Body).HasMaxLength(2000);
        b.HasOne(m => m.Conversation).WithMany(c => c.Messages).HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(m => new { m.ConversationId, m.CreatedAt });
    }
}
