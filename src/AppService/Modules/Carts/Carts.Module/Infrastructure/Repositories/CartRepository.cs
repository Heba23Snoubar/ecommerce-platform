using Carts.Module.Infrastructure.Persistence;
using Catalog.Module.Application.Abstraction.Repositories;
using E.Domain.Entites;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carts.Module.Infrastructure.Repositories
{
     
    internal sealed class CartRepository : ICartRepository
    {
        private readonly CartDbContext _context;

        public CartRepository(CartDbContext context)
        {
            _context = context;
        }

        public async Task<Cart?> GetByUserIdAsync(
            Guid userId,
            CancellationToken ct = default)
        {
             return await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);
        }

        public async Task<Cart?> GetByIdAsync(
            Guid cartId,
            CancellationToken ct = default)
        {
            return await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cartId, ct);
        }

        public async Task AddAsync(
            Cart cart,
            CancellationToken ct = default)
        {
            await _context.Carts.AddAsync(cart, ct);
        }

        public void Remove(Cart cart)
        {
            _context.Carts.Remove(cart);
        }
    }
}
