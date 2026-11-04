using E.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
 



namespace E.Infrastructure.config
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasOne(p => p.Category)
                   .WithMany()
                   .HasForeignKey(p => p.CategoryId)
                   .OnDelete(DeleteBehavior.Restrict)
                   ;
             

            builder.Property(p => p.Price)
                .HasPrecision(18, 2);


             

            builder.Navigation(p => p.Images)
                   .UsePropertyAccessMode(PropertyAccessMode.Field)
                  ;

        }

    }

}
