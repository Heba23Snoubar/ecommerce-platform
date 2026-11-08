

using E.Domain.Common;

namespace E.Domain.Entites
{

    public class Cart :BaseEntity
    {
        

        public Guid UserId { get; private set; }

        private List<CartItem> _items = new List<CartItem>();

        public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

        private Cart()
        {
        }

        public Cart(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException(
                    "Invalid user ID.",
                    nameof(userId));

            UserId = userId;
        }



        public void AddItem(Guid productId, int quantity)
        {


            if (productId == Guid.Empty)
                throw new ArgumentException("Ivalid Product Id .", nameof(productId));

            if (quantity <= 0)
                throw new ArgumentException(
                  "Quantity must be greater than zero.",
                  nameof(quantity));

           var existingItem=  Items.FirstOrDefault(item => item.ProductId == productId);

                if(existingItem is not  null)
            {  
            existingItem.IncreaseQuantity (quantity);
                return;
            }

             
            _items.Add(new CartItem(productId, quantity));
            
        }

        public void RemoveItem(Guid productId)
        {


            if (productId == Guid.Empty)
                throw new ArgumentException("Ivalid Product Id .", nameof(productId));

             
            var item = Items.FirstOrDefault(item => item.ProductId == productId);

            if (item is null)
                throw new InvalidOperationException(
                    "Product does not exist in the cart.");
        
            
            _items.Remove(item);
         }


        public void IncreaseItemQuantity(Guid productId, int quantity)
        {
            var item = GetItem(productId);

            item.IncreaseQuantity(quantity);
        }

        public void DecreaseItemQuantity(Guid productId, int quantity)
        {
            var item = GetItem(productId);

            item.DecreaseQuantity(quantity);
        }


        private CartItem GetItem (Guid productId)
        {
            if (productId == Guid.Empty)
                throw new ArgumentException(
                    "Invalid product ID.",
                    nameof(productId));

            var item = Items.FirstOrDefault(ite => ite.ProductId == productId);

            if(item is null)
                throw new InvalidOperationException(
                    "Product does not exist in the cart.");

            return item;
        }

        public void Clear()
        {
            _items.Clear();
            UpdatedAt=DateTime.UtcNow;
        }
    }

}
