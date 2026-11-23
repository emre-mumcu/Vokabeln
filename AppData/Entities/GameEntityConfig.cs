using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vokabeln.AppData.Entities;

public class GameConfig : IEntityTypeConfiguration<GameEntity>
{
    public void Configure(EntityTypeBuilder<GameEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WordType).HasConversion<int?>();
        builder.Property(x => x.Article).HasConversion<int>();

        builder.HasIndex(x => x.FinishedAt);

        builder.HasMany(x => x.Answers)
            .WithOne(x => x.Game)
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
