using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Vokabeln.AppData.Entities;

public class WordConfig : IEntityTypeConfiguration<WordEntity>
{
    public void Configure(EntityTypeBuilder<WordEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(p => new { p.Word, p.WordType }).IsUnique();

        builder.Property(x => x.WordType).HasConversion<int>();
    }
}
