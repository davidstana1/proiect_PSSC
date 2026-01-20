using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ProiectPSSC.Application.Orders;
using ProiectPSSC.Infrastructure.Persistence;
using Xunit;

namespace ProiectPSSC.IntegrationTests;

public class OrderWorkflowTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly PostgresWebApplicationFactory _factory;

    public OrderWorkflowTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PlaceOrder_ShouldCreateOrderAndInvoice()
    {
        // Arrange
        var request = new PlaceOrderRequest(
            CustomerEmail: "test@example.com",
            Lines: new List<PlaceOrderLine>
            {
                new("PROD-001", 2, 50.00m),
                new("PROD-002", 1, 25.00m)
            }
        );

        // Act
        var response = await _client.PostAsJsonAsync("/orders", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(content);
        Assert.Equal(125.00m, content.Total); // 2*50 + 1*25

        // Wait for outbox dispatcher to process events
        await Task.Delay(3000);

        // Verify invoice was created
        var invoiceResponse = await _client.GetAsync($"/orders/{content.OrderId}/invoice");
        Assert.Equal(HttpStatusCode.OK, invoiceResponse.StatusCode);

        var invoiceJson = await invoiceResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(invoiceJson);
        Assert.Equal(125.00m, doc.RootElement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task UpdateOrder_ShouldUpdateOrderAndInvoice()
    {
        // Arrange - place an order first
        var placeRequest = new PlaceOrderRequest(
            CustomerEmail: "update-test@example.com",
            Lines: new List<PlaceOrderLine>
            {
                new("PROD-A", 1, 100.00m)
            }
        );

        var placeResponse = await _client.PostAsJsonAsync("/orders", placeRequest);
        var placeResult = await placeResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(placeResult);

        // Wait for invoice creation
        await Task.Delay(3000);

        // Act - update the order
        var updateRequest = new UpdateOrderRequest(
            Lines: new List<UpdateOrderLine>
            {
                new("PROD-A", 2, 100.00m), // Increase quantity
                new("PROD-B", 1, 50.00m)   // Add new product
            }
        );

        var updateResponse = await _client.PutAsJsonAsync($"/orders/{placeResult.OrderId}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updateJson = await updateResponse.Content.ReadAsStringAsync();
        using var updateDoc = JsonDocument.Parse(updateJson);
        Assert.Equal(100.00m, updateDoc.RootElement.GetProperty("oldTotal").GetDecimal());
        Assert.Equal(250.00m, updateDoc.RootElement.GetProperty("newTotal").GetDecimal()); // 2*100 + 1*50

        // Wait for outbox dispatcher to process OrderUpdated event
        await Task.Delay(3000);

        // Verify invoice was updated
        var invoiceResponse = await _client.GetAsync($"/orders/{placeResult.OrderId}/invoice");
        Assert.Equal(HttpStatusCode.OK, invoiceResponse.StatusCode);

        var invoiceJson = await invoiceResponse.Content.ReadAsStringAsync();
        using var invoiceDoc = JsonDocument.Parse(invoiceJson);
        Assert.Equal(250.00m, invoiceDoc.RootElement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task ShipOrder_ShouldTransitionOrderToShipped()
    {
        // Arrange - place an order first
        var placeRequest = new PlaceOrderRequest(
            CustomerEmail: "ship-test@example.com",
            Lines: new List<PlaceOrderLine>
            {
                new("PROD-X", 1, 200.00m)
            }
        );

        var placeResponse = await _client.PostAsJsonAsync("/orders", placeRequest);
        var placeResult = await placeResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(placeResult);

        // Wait for invoice creation (status becomes Invoiced)
        await Task.Delay(3000);

        // Act - ship the order
        var shipRequest = new ShipOrderRequest("TRACK-123", "DHL");
        var shipResponse = await _client.PostAsJsonAsync($"/orders/{placeResult.OrderId}/ship", shipRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, shipResponse.StatusCode);

        var shipJson = await shipResponse.Content.ReadAsStringAsync();
        using var shipDoc = JsonDocument.Parse(shipJson);
        Assert.Equal("Shipped", shipDoc.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(Guid.Empty, shipDoc.RootElement.GetProperty("shipmentId").GetGuid());

        // Verify shipment can be retrieved
        var shipmentId = shipDoc.RootElement.GetProperty("shipmentId").GetGuid();
        var shipmentResponse = await _client.GetAsync($"/shipments/{shipmentId}");
        Assert.Equal(HttpStatusCode.OK, shipmentResponse.StatusCode);

        var shipmentJson = await shipmentResponse.Content.ReadAsStringAsync();
        using var shipmentDoc = JsonDocument.Parse(shipmentJson);
        Assert.Equal("TRACK-123", shipmentDoc.RootElement.GetProperty("trackingNumber").GetString());
        Assert.Equal("DHL", shipmentDoc.RootElement.GetProperty("carrier").GetString());
    }

    [Fact]
    public async Task ShipOrder_ShouldFailForNonInvoicedOrder()
    {
        // Arrange - place an order first (don't wait for invoice)
        var placeRequest = new PlaceOrderRequest(
            CustomerEmail: "ship-fail-test@example.com",
            Lines: new List<PlaceOrderLine>
            {
                new("PROD-Y", 1, 150.00m)
            }
        );

        var placeResponse = await _client.PostAsJsonAsync("/orders", placeRequest);
        var placeResult = await placeResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(placeResult);

        // Act - try to ship immediately (before invoicing)
        var shipRequest = new ShipOrderRequest("TRACK-456", "FedEx");
        var shipResponse = await _client.PostAsJsonAsync($"/orders/{placeResult.OrderId}/ship", shipRequest);

        // Assert - should fail because order is not invoiced yet
        Assert.Equal(HttpStatusCode.BadRequest, shipResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateOrder_ShouldFailForShippedOrder()
    {
        // Arrange - place, wait for invoice, then ship
        var placeRequest = new PlaceOrderRequest(
            CustomerEmail: "update-shipped-test@example.com",
            Lines: new List<PlaceOrderLine>
            {
                new("PROD-Z", 1, 300.00m)
            }
        );

        var placeResponse = await _client.PostAsJsonAsync("/orders", placeRequest);
        var placeResult = await placeResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(placeResult);

        // Wait for invoice creation
        await Task.Delay(3000);

        // Ship the order
        var shipRequest = new ShipOrderRequest("TRACK-789", "UPS");
        var shipResponse = await _client.PostAsJsonAsync($"/orders/{placeResult.OrderId}/ship", shipRequest);
        Assert.Equal(HttpStatusCode.OK, shipResponse.StatusCode);

        // Act - try to update the shipped order
        var updateRequest = new UpdateOrderRequest(
            Lines: new List<UpdateOrderLine>
            {
                new("PROD-Z", 5, 300.00m)
            }
        );

        var updateResponse = await _client.PutAsJsonAsync($"/orders/{placeResult.OrderId}", updateRequest);

        // Assert - should fail because order is shipped
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_ShouldFailForShippedOrder()
    {
        // Arrange - place, wait for invoice, then ship
        var placeRequest = new PlaceOrderRequest(
            CustomerEmail: "cancel-shipped-test@example.com",
            Lines: new List<PlaceOrderLine>
            {
                new("PROD-W", 1, 400.00m)
            }
        );

        var placeResponse = await _client.PostAsJsonAsync("/orders", placeRequest);
        var placeResult = await placeResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(placeResult);

        // Wait for invoice creation
        await Task.Delay(3000);

        // Ship the order
        var shipRequest = new ShipOrderRequest("TRACK-ABC", "DPD");
        var shipResponse = await _client.PostAsJsonAsync($"/orders/{placeResult.OrderId}/ship", shipRequest);
        Assert.Equal(HttpStatusCode.OK, shipResponse.StatusCode);

        // Act - try to cancel the shipped order
        var cancelResponse = await _client.PostAsJsonAsync($"/orders/{placeResult.OrderId}/cancel", new { reason = "Test cancel" });

        // Assert - should fail because order is shipped
        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task FullWorkflow_PlaceUpdateShipOrder()
    {
        // Step 1: Place an order
        var placeRequest = new PlaceOrderRequest(
            CustomerEmail: "full-workflow@example.com",
            Lines: new List<PlaceOrderLine>
            {
                new("ITEM-001", 1, 99.99m)
            }
        );

        var placeResponse = await _client.PostAsJsonAsync("/orders", placeRequest);
        Assert.Equal(HttpStatusCode.Created, placeResponse.StatusCode);

        var placeResult = await placeResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
        Assert.NotNull(placeResult);
        var orderId = placeResult.OrderId;

        // Step 2: Verify order was created
        var orderResponse = await _client.GetAsync($"/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);

        // Step 3: Wait for invoice generation
        await Task.Delay(3000);

        var invoiceResponse = await _client.GetAsync($"/orders/{orderId}/invoice");
        Assert.Equal(HttpStatusCode.OK, invoiceResponse.StatusCode);

        // Step 4: Update the order
        var updateRequest = new UpdateOrderRequest(
            Lines: new List<UpdateOrderLine>
            {
                new("ITEM-001", 2, 99.99m),
                new("ITEM-002", 1, 49.99m)
            }
        );

        var updateResponse = await _client.PutAsJsonAsync($"/orders/{orderId}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // Wait for invoice update
        await Task.Delay(3000);

        // Verify invoice was updated
        invoiceResponse = await _client.GetAsync($"/orders/{orderId}/invoice");
        var invoiceJson = await invoiceResponse.Content.ReadAsStringAsync();
        using var invoiceDoc = JsonDocument.Parse(invoiceJson);
        Assert.Equal(249.97m, invoiceDoc.RootElement.GetProperty("amount").GetDecimal()); // 2*99.99 + 49.99

        // Step 5: Ship the order
        var shipRequest = new ShipOrderRequest("FINAL-TRACK", "Express");
        var shipResponse = await _client.PostAsJsonAsync($"/orders/{orderId}/ship", shipRequest);
        Assert.Equal(HttpStatusCode.OK, shipResponse.StatusCode);

        // Step 6: Verify final order state
        orderResponse = await _client.GetAsync($"/orders/{orderId}");
        var orderJson = await orderResponse.Content.ReadAsStringAsync();
        using var orderDoc = JsonDocument.Parse(orderJson);
        Assert.Equal("Shipped", orderDoc.RootElement.GetProperty("status").GetString());

        // Step 7: Verify shipment details
        var shipmentResponse = await _client.GetAsync($"/orders/{orderId}/shipment");
        Assert.Equal(HttpStatusCode.OK, shipmentResponse.StatusCode);

        var shipmentJson = await shipmentResponse.Content.ReadAsStringAsync();
        using var shipmentDoc = JsonDocument.Parse(shipmentJson);
        Assert.Equal("FINAL-TRACK", shipmentDoc.RootElement.GetProperty("trackingNumber").GetString());
    }
}
