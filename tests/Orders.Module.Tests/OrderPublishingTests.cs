using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Contracts;
using Orders.Module.Data;
using Orders.Module.Messaging;
using Orders.Module.Ports;
using Orders.Module.Services;
using Orders.Module.Stubs;

namespace Orders.Module.Tests;

public class OrderPublishingTests
{
    private static readonly Guid LaptopId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.NewGuid();

    // ---- بدائل بسيطة للاعتماديات ----
    private sealed class RecordingPublisher : IOrderEventPublisher
    {
        public List<OrderCreatedMessage> Messages { get; } = new();

        public Task PublishOrderCreatedAsync(OrderCreatedMessage message, CancellationToken ct)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IOrderEventPublisher
    {
        public Task PublishOrderCreatedAsync(OrderCreatedMessage message, CancellationToken ct)
            => throw new InvalidOperationException("RabbitMQ is down");
    }

    private sealed class StubContact(CustomerContact? contact) : ICustomerContact
    {
        public CustomerContact? GetCurrent() => contact;
    }

    private sealed class EmptyCart : ICartSource
    {
        public Task<IReadOnlyList<CartLine>> GetItemsAsync(Guid userId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CartLine>>(Array.Empty<CartLine>());

        public Task ClearAsync(Guid userId, CancellationToken ct) => Task.CompletedTask;
    }

    private static (OrdersService Service, OrdersDbContext Db) Create(
        IOrderEventPublisher publisher, CustomerContact? contact)
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);

        var service = new OrdersService(
            db,
            new FakeProductCatalog(),
            new FakeStockReservation(NullLogger<FakeStockReservation>.Instance,
                new ConfigurationBuilder().Build()),
            new EmptyCart(),
            new StubContact(contact),
            publisher,
            NullLogger<OrdersService>.Instance);

        return (service, db);
    }

    private static CreateOrderRequest OneLaptop(int quantity = 2) =>
        new(new List<OrderItemRequest> { new(LaptopId, quantity) });

    // ---- الاختبارات ----
    [Fact]
    public async Task Create_PublishesOneMessage_WithCorrectFields()
    {
        var publisher = new RecordingPublisher();
        var (service, _) = Create(publisher, new CustomerContact("Khadija", "khadija@example.com"));

        var order = await service.CreateAsync(UserId, OneLaptop(2), CancellationToken.None);

        var message = Assert.Single(publisher.Messages);
        Assert.Equal(order.Id, message.OrderId);
        Assert.Equal(UserId, message.UserId);
        Assert.Equal("Khadija", message.CustomerName);
        Assert.Equal("khadija@example.com", message.CustomerEmail);
        Assert.Equal(2400m, message.Total);
        Assert.NotEqual(Guid.Empty, message.MessageId);

        var item = Assert.Single(message.Items);
        Assert.Equal("Laptop", item.ProductName);
        Assert.Equal(1200m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public async Task Create_WhenPublishFails_OrderIsStillSavedAndReturned()
    {
        var (service, db) = Create(new ThrowingPublisher(),
            new CustomerContact("Khadija", "khadija@example.com"));

        var order = await service.CreateAsync(UserId, OneLaptop(), CancellationToken.None);

        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Equal(order.Id, (await db.Orders.SingleAsync()).Id);
    }

    [Fact]
    public async Task Create_WithoutCustomerEmail_DoesNotPublish_ButSavesOrder()
    {
        var publisher = new RecordingPublisher();
        var (service, db) = Create(publisher, contact: null);

        await service.CreateAsync(UserId, OneLaptop(), CancellationToken.None);

        Assert.Empty(publisher.Messages);
        Assert.Equal(1, await db.Orders.CountAsync());
    }
}