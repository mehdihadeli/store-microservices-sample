using BuildingBlocks.Core.Messaging;

namespace FoodDelivery.Services.Shared.Catalogs.Suppliers.Events.v1.Integration;

public record SupplierCreatedV1(long Id, string Name) : IntegrationEvent;
