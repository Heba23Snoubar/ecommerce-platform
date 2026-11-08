using E.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E.Domain.Entites
{

    public class CartItem :BaseEntity
    { 

        public Guid CartId { get; private set; }
        public Cart Cart { get; private set; } = null!;

        public Guid ProductId { get; private set; }
         
        public int Quantity { get; private set; }

        private CartItem()
        {
        }

        public CartItem(
            Guid productId,
            int quantity)
        {
             
            if (productId == Guid.Empty)
                throw new ArgumentException(
                    "Invalid product ID.",
                    nameof(productId));

            if (quantity <= 0)
                throw new ArgumentException(
                    "Quantity must be greater than zero.",
                    nameof(quantity));
             
            ProductId = productId;
            Quantity = quantity;
        }

        internal void IncreaseQuantity(int amount)
        {
            if (amount <= 0)
                throw new ArgumentException(
                    "Amount must be greater than zero.",
                    nameof(amount));

            Quantity += amount;
        }

        internal void DecreaseQuantity(int amount)
        {
            if (amount <= 0)
                throw new ArgumentException(
                    "Amount must be greater than zero.",
                    nameof(amount));

            if (Quantity - amount <= 0)
                throw new InvalidOperationException(
                    "Quantity cannot become zero or negative.");

            Quantity -= amount;
        }



    }

}
