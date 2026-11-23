using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vokabeln.AppData.Entities;

public class GameAnswerConfig : IEntityTypeConfiguration<GameAnswerEntity>
{
    public void Configure(EntityTypeBuilder<GameAnswerEntity> builder)
    {
        builder.HasKey(x => x.Id);

        // Bir oyunda her kelime bir kez cevaplanır (sayfa yenilense de puan iki kez sayılmaz)
        builder.HasIndex(x => new { x.GameId, x.WordId }).IsUnique();

        // Kelime silinirse ona ait cevaplar da silinir
        builder.HasOne(x => x.Word)
            .WithMany()
            .HasForeignKey(x => x.WordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
