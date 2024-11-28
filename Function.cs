using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace function;

public class Function
{
    private readonly ILoggerFactory _loggerFactory;

    public Function(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }
    
    [Function("subscribe-function")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "subscribe")] HttpRequestData req)
    {
        var logger = _loggerFactory.CreateLogger<Function>();
        logger.LogInformation("Iniciando el procesamiento de la solicitud...");
        
        // Parsear el cuerpo de la solicitud para obtener el correo
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        dynamic data = JsonConvert.DeserializeObject(requestBody)!;
        string email = data?.email!;
        
        if (string.IsNullOrEmpty(email))
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("¡Ups! Parece que no proporcionaste un correo electrónico. Por favor, inténtalo de nuevo.");
            return badResponse;
        }
        
        // Conectar a Azure Table Storage
        string connectionString = Environment.GetEnvironmentVariable("AzureTableAccountStorageName")!;
        TableClient tableClient = new TableClient(connectionString, "SubscriptionList");
        
        // Asegurarse de que la tabla exista
        await tableClient.CreateIfNotExistsAsync();
        
        // Verificar si el correo ya está suscrito
        var query = tableClient.Query<TableEntity>(filter: $"PartitionKey eq 'subscription' and Email eq '{email}'");
        if (query.Any())
        {
            var conflictResponse = req.CreateResponse(System.Net.HttpStatusCode.Conflict);
            await conflictResponse.WriteAsJsonAsync(new
            {
                Message = "El correo electrónico que ingresaste ya está registrado en Tsiru. ¡Gracias por ser parte de nuestra comunidad!"
            });
            return conflictResponse;
        }
        
        // Crear una nueva entidad
        var emailEntity = new TableEntity("subscription", Guid.NewGuid().ToString())
        {
            { "Email", email },
            { "Timestamp", DateTime.UtcNow }
        };
        
        // Insertar la entidad en la tabla
        await tableClient.AddEntityAsync(emailEntity);

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            Message = "¡Tu correo electrónico se ha suscrito exitosamente a Tsiru! Estamos emocionados de que formes parte de nuestra comunidad."
        });
        return response;
    }
}