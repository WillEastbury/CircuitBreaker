using Microsoft.AspNetCore.Mvc;
internal class Program
{
    public static int ErrorChance = 0 ; // from zero to 100
    public static int TransientChance = 0 ; // from zero to 100
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.MapGet("/FailureSim/{ErrorChance}/{TransientChance}", async (int errorChance, int transientChance, HttpResponse response) =>
        {
            if(errorChance + transientChance > 100)
            {
                response.StatusCode = 500;
                await response.WriteAsync($"You can only specify a total of 100% chance for error and transient in total, you specified {errorChance + TransientChance}");
                await response.CompleteAsync();
            }
            else
            {
                ErrorChance = errorChance;
                TransientChance = transientChance;
                response.StatusCode = 200;
                await response.WriteAsync($"All OK, ErrorChance is {errorChance}%, TransientChance is {TransientChance}%, so total chance of error is {errorChance + TransientChance}%.");
                await response.CompleteAsync();
            }
        });

        app.MapGet("/test", async (HttpResponse response) =>
        {
            // Generate a random success, transient failure, or failure response based on the failure probability of ErrorChance and TransientChance
            var random = new Random();
            var randomNumber = random.Next(0, 100);
            if (randomNumber < ErrorChance)
            {
                response.StatusCode = 500;
                Console.WriteLine("Return error - test");
                await response.WriteAsync("<!DOCTYPE html><html><head><title>Non Transient Error</title><style>body {background-color: red;}</style></head><body><h2>Uh Oh, Error!</h2></body></html>");
                await response.CompleteAsync();
            }
            else if (randomNumber < ErrorChance + TransientChance)
            {
                response.StatusCode = 429;
                Console.WriteLine("Return transient error - test");
                await response.WriteAsync("<!DOCTYPE html><html><head><title>Transient Error</title><style>body {background-color: yellow;}</style></head><body><h2>Transient Error, Please Retry</h2></body></html>");
                await response.CompleteAsync();
            }
            else
            {
                response.StatusCode = 200;
                Console.WriteLine("Return success - test");
                await response.WriteAsync("<!DOCTYPE html><html><head><title>All OK</title><style>body {background-color: green;}</style></head><body><h2>All is OK </h2></body></html>");
                await response.CompleteAsync();
            }
           // return response;
        });

         app.MapPost("/transaction/{id}", async ([FromHeader(Name = "X-Extra-HeaderInfo")] string ExtraHeaderInfo, HttpResponse response) =>
        {
            // Generate a random success, transient failure, or failure response based on the failure probability of ErrorChance and TransientChance
            var random = new Random();
            var randomNumber = random.Next(0, 100);
            if (randomNumber < ErrorChance)
            {
                response.StatusCode = 500;
                Console.WriteLine($"Return error - Transaction {ExtraHeaderInfo}");
                await response.WriteAsync("<!DOCTYPE html><html><head><title>Non Transient Error</title><style>body {background-color: red;}</style></head><body><h2>Uh Oh, Error!</h2></body></html>");
                await response.CompleteAsync();
            }
            else if (randomNumber < ErrorChance + TransientChance)
            {
                response.StatusCode = 429;
                Console.WriteLine($"Return transient error - Transaction {ExtraHeaderInfo}");
                await response.WriteAsync("<!DOCTYPE html><html><head><title>Transient Error</title><style>body {background-color: yellow;}</style></head><body><h2>Transient Error, Please Retry</h2></body></html>");
                await response.CompleteAsync();
            }
            else
            {
                response.StatusCode = 200;
                Console.WriteLine($"Return success - Transaction {ExtraHeaderInfo}");
                await response.WriteAsync("<!DOCTYPE html><html><head><title>All OK</title><style>body {background-color: green;}</style></head><body><h2>All is OK </h2></body></html>");
                await response.CompleteAsync();
            }
           // return response;
        });

        app.Run();
    }
}