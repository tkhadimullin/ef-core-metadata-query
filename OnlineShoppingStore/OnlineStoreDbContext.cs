using Microsoft.EntityFrameworkCore;

namespace OnlineShoppingStore
{
    public class OnlineStoreDbContext: DbContext
    {

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.ProductInfoId)
                    .HasName("PK_ProductInfoId");
            });
            
            base.OnModelCreating(modelBuilder);
        }
    }
}
