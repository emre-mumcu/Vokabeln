using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vokabeln.AppData.Entities;

public class WordConfig : IEntityTypeConfiguration<WordEntity>
{
    public void Configure(EntityTypeBuilder<WordEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Word).UseCollation("NOCASE");

        builder.HasIndex(p => new { p.Word, p.WordType }).IsUnique();

        builder.Property(x => x.WordType).HasConversion<int>();
        builder.Property(x => x.Auxiliary).HasConversion<int?>();

        // SQLite tarih türünü (Kind) saklamaz; okunan değerler UTC olarak işaretlenir
        builder.Property(x => x.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(x => x.UpdatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasIndex(x => x.UpdatedAt);

        // Çekimler: kelime silinince çekimleri de silinir. WordId üzerinde unique index otomatik oluşur.
        builder.HasOne(x => x.Praesens).WithOne(x => x.Word).HasForeignKey<PraesensEntity>(x => x.WordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Praeteritum).WithOne(x => x.Word).HasForeignKey<PraeteritumEntity>(x => x.WordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Perfekt).WithOne(x => x.Word).HasForeignKey<PerfektEntity>(x => x.WordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.FuturI).WithOne(x => x.Word).HasForeignKey<FuturIEntity>(x => x.WordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.KonjunktivII).WithOne(x => x.Word).HasForeignKey<KonjunktivIIEntity>(x => x.WordId).OnDelete(DeleteBehavior.Cascade);
    }
}
