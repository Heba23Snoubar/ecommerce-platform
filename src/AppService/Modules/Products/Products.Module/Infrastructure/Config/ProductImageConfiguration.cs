using E.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Serialization;





namespace E.Infrastructure.Config
{
    public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
    {
        public void Configure(EntityTypeBuilder<ProductImage> builder)
        {

             
            builder.HasKey(pi => pi.Id);

            builder.Property(pi => pi.IsMain)
                   .IsRequired();

            builder.Property(pi =>pi.ImageUrl)
                   .IsRequired()
                   .HasMaxLength(500);

          
             
        }
    }

}
