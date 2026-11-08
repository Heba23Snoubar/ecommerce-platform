using E.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace E.Infrastructure.Config
{
    public class CartConfiguration : IEntityTypeConfiguration<Cart>
    {
        public void Configure(EntityTypeBuilder<Cart> builder)
        {
            builder.ToTable("carts");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.UserId)
                .IsRequired();


            builder.HasIndex(c => c.UserId)
                .IsUnique();


            builder.Navigation(c => c.Items)
                   .UsePropertyAccessMode(PropertyAccessMode.Field)
                 ;
        }
         
    }

}
