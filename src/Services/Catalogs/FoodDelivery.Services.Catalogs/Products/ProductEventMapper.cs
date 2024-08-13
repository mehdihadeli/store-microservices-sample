using BuildingBlocks.Abstractions.Events;
using BuildingBlocks.Abstractions.Events.Internal;
using BuildingBlocks.Abstractions.Messaging;
using BuildingBlocks.Core.Extensions;
using FoodDelivery.Services.Catalogs.Products.Features.CreatingProduct.V1.Events.Domain;
using FoodDelivery.Services.Catalogs.Products.Features.CreatingProduct.V1.Events.Notification;
using FoodDelivery.Services.Catalogs.Products.Features.DebitingProductStock.V1.Events.Domain;
using FoodDelivery.Services.Catalogs.Products.Features.ReplenishingProductStock.V1.Events.Domain;
using FoodDelivery.Services.Catalogs.Products.Features.UpdatingProduct.V1;
using FoodDelivery.Services.Catalogs.Shared.Data;
using FoodDelivery.Services.Shared.Catalogs.Products.Events.V1.Integration;
using Microsoft.EntityFrameworkCore;

namespace FoodDelivery.Services.Catalogs.Products;

public class ProductEventMapper(CatalogDbContext catalogDbContext) : IEventMapper
{
    public IIntegrationEvent? MapToIntegrationEvent(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            // Materialize domain event to integration event
            case ProductCreated productCreated:
            {
                var product = catalogDbContext
                    .Products.Include(x => x.Brand)
                    .Include(x => x.Category)
                    .Include(x => x.Supplier)
                    .FirstOrDefault(x => x.Id == productCreated.Id);

                product.NotBeNull();
                product.Category.NotBeNull();

                return ProductCreatedV1.Of(
                    productCreated.Id,
                    productCreated.Name,
                    productCreated.CategoryId,
                    product.Category.Name,
                    productCreated.AvailableStock
                );
            }

            case ProductUpdated productUpdated:
            {
                var product = catalogDbContext
                    .Products.Include(x => x.Brand)
                    .Include(x => x.Category)
                    .Include(x => x.Supplier)
                    .FirstOrDefault(x => x.Id == productUpdated.Id);

                product.NotBeNull();
                product.Category.NotBeNull();

                return ProductUpdatedV1.Of(
                    productUpdated.Id,
                    productUpdated.Name,
                    productUpdated.CategoryId,
                    product.Category.Name,
                    productUpdated.AvailableStock
                );
            }

            case ProductStockDebited productStockDebited:
                return ProductStockDebitedV1.Of(
                    productStockDebited.ProductId,
                    productStockDebited.AvailableStock,
                    productStockDebited.DebitQuantity
                );
            case ProductStockReplenished productStockReplenished:
                return ProductStockReplenishedV1.Of(
                    productStockReplenished.ProductId,
                    productStockReplenished.AvailableStock,
                    productStockReplenished.ReplenishedQuantity
                );
            default:
                return null;
        }
    }

    public IDomainNotificationEvent? MapToDomainNotificationEvent(IDomainEvent domainEvent)
    {
        return domainEvent switch
        {
            ProductCreated productCreated => new ProductCreatedNotification(productCreated),
            _ => null
        };
    }

    public IReadOnlyList<IIntegrationEvent?> MapToIntegrationEvents(IReadOnlyList<IDomainEvent> domainEvents)
    {
        return domainEvents.Select(MapToIntegrationEvent).ToList().AsReadOnly();
    }

    public IReadOnlyList<IDomainNotificationEvent?> MapToDomainNotificationEvents(
        IReadOnlyList<IDomainEvent> domainEvents
    )
    {
        return domainEvents.Select(MapToDomainNotificationEvent).ToList().AsReadOnly();
    }
}
