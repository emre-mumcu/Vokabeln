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
        builder.Property(x => x.Type).HasConversion<int>(); // Mevcut kayıtlar 0 = Meaning (anlam oyunu) olur
        builder.Property(x => x.Tense).HasConversion<int?>();

        builder.HasIndex(x => x.FinishedAt);
        builder.HasIndex(x => x.Type);

        builder.HasMany(x => x.Answers)
            .WithOne(x => x.Game)
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
