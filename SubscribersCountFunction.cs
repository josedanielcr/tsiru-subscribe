using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace function;

public class SubscribersCountFunction(ILoggerFactory loggerFactory)
{
    
    [Function("count-subscriptions-function")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "count-subscriptions")] HttpRequestData req)
    {
        var logger = loggerFactory.CreateLogger<SubscribersCountFunction>();
        logger.LogInformation("Contando todas las filas en la tabla SubscriptionList...");

        try
        {
            var connectionString = Environment.GetEnvironmentVariable("AzureTableAccountStorageName")!;
            var tableClient = new TableClient(connectionString, "SubscriptionList");
            await tableClient.CreateIfNotExistsAsync();

            var entities = tableClient.Query<TableEntity>(filter: $"PartitionKey eq 'subscription'");
            var count = entities.Count();

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Total = count
            });
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocurrió un error al contar las filas en la tabla SubscriptionList.");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                Mensaje = "¡Ups! Algo salió mal mientras contábamos las suscripciones.",
                DetalleDelError = ex.Message
            });
            return errorResponse;
        }
    }
}