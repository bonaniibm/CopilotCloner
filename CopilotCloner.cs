using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace CopilotCloner
{
    public class CopilotCloneFunction(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<CopilotCloneFunction>();
        private readonly IConfiguration _configuration = configuration;

        [Function("CopilotClone")]
        public async Task<HttpResponseData> RunAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request to clone a copilot.");

            _logger.LogInformation($"Current PATH: {Environment.GetEnvironmentVariable("PATH")}");

            string pacPath = "/usr/local/bin";
            string existingPath = Environment.GetEnvironmentVariable("PATH");
            if (!existingPath.Contains(pacPath))
            {
                string newPath = $"{existingPath}:{pacPath}";
                Environment.SetEnvironmentVariable("PATH", newPath);
                _logger.LogInformation($"Updated PATH: {newPath}");
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string environmentId = data?.environmentId;
            string botId = data?.botId;
            string newCopilotDisplayName = data?.newCopilotDisplayName;
            string newCopilotSchemaName = data?.newCopilotSchemaName;
            string newCopilotSolution = data?.newCopilotSolution;

            if (string.IsNullOrEmpty(environmentId) || string.IsNullOrEmpty(botId) ||
                string.IsNullOrEmpty(newCopilotDisplayName) || string.IsNullOrEmpty(newCopilotSchemaName) ||
                string.IsNullOrEmpty(newCopilotSolution))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Please provide all required parameters.");
                return badRequestResponse;
            }

            try
            {
                if (!IsPacInstalled())
                {
                    var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Power Platform CLI (pac) is not installed or not in the system PATH.");
                    return badRequestResponse;
                }

                await SetupPacAuthentication();

                string templateFileName = $"{newCopilotSchemaName}_template.yaml";
                var extractResult = await ExecutePacCommand(
                    $"copilot extract-template --environment \"{environmentId}\" --bot \"{botId}\" --templateFileName \"{templateFileName}\""
                );

                if (!extractResult.Success)
                {
                    var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync($"Failed to extract template: {extractResult.Error}");
                    return badRequestResponse;
                }

                var createResult = await ExecutePacCommand(
                    $"copilot create --environment \"{environmentId}\" --displayName \"{newCopilotDisplayName}\" --schemaName \"{newCopilotSchemaName}\" --solution \"{newCopilotSolution}\" --templateFileName \"{templateFileName}\""
                );

                File.Delete(templateFileName);

                if (!createResult.Success)
                {
                    var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync($"Failed to create new copilot: {createResult.Error} {createResult.Output}");
                    return badRequestResponse;
                }

                string copilotId = ExtractCopilotId(createResult.Output);
                string copilotUrl = ExtractCopilotUrl(createResult.Output);

                var okResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await okResponse.WriteStringAsync(
                    $"Copilot created successfully.\n" +
                    $"New copilot: {newCopilotDisplayName}\n" +
                    $"Copilot ID: {copilotId}\n" +
                    $"Copilot URL: {copilotUrl}\n\n" +
                    "To publish this copilot to channels of your choice, please follow these steps:\n" +
                    "1. Go to the Power Virtual Agents portal: https://web.powerva.microsoft.com\n" +
                    "2. Select your environment and open the newly created copilot\n" +
                    "3. Navigate to the 'Publish' section\n" +
                    "4. Choose the desired channels and follow the instructions to publish your copilot\n\n" +
                    "For more information on publishing, visit: https://learn.microsoft.com/en-us/power-virtual-agents/publication-fundamentals-publish-channels"
                );
                return okResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cloning copilot: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred while cloning the copilot.");
                return errorResponse;
            }
        }

        private bool IsPacInstalled()
        {
            try
            {
                _logger.LogInformation($"Current PATH: {Environment.GetEnvironmentVariable("PATH")}");
                _logger.LogInformation($"Current Directory: {Environment.CurrentDirectory}");

                using var process = new Process();
                process.StartInfo.FileName = "pac";
                process.StartInfo.Arguments = ""; // No arguments
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                _logger.LogInformation("Running 'pac' without arguments to check installation.");

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                _logger.LogInformation($"PAC output: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogError($"PAC error: {error}");
                }
                bool isPacInstalled = output.Contains("Microsoft PowerPlatform CLI") && process.ExitCode == 0;

                _logger.LogInformation($"Is PAC installed: {isPacInstalled}");

                return isPacInstalled;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking PAC installation: {ex.Message}");
                _logger.LogError($"Exception details: {ex}");
                return false;
            }
        }


        private async Task<(bool Success, string Output, string Error)> ExecutePacCommand(string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = "pac";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                _logger.LogInformation($"Executing command: pac {arguments}");

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                _logger.LogInformation($"PAC command output: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogError($"PAC command error: {error}");
                }

                return (process.ExitCode == 0, output, error);
            }
        }

        private static string ExtractCopilotId(string output)
        {
            var match = System.Text.RegularExpressions.Regex.Match(output, @"id ([0-9a-fA-F-]+)");
            return match.Success ? match.Groups[1].Value : "ID not found";
        }

        private static string ExtractCopilotUrl(string output)
        {
            var match = System.Text.RegularExpressions.Regex.Match(output, @"(https://web\.powerva\.microsoft\.com/environments/[^\s]+)");
            return match.Success ? match.Groups[1].Value : "URL not found";
        }


        private async Task SetupPacAuthentication()
        {
            var tenantId = _configuration["AZURE_TENANT_ID"];
            var clientId = _configuration["AZURE_CLIENT_ID"];
            var clientSecret = _configuration["AZURE_CLIENT_SECRET"];
            var environment = _configuration["DYNAMICS_URL"];

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) ||
                string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(environment))
            {
                throw new Exception("Missing authentication configuration. Please check your environment variables.");
            }

            var authCommand = $"auth create --environment \"{environment}\" --tenant \"{tenantId}\" --applicationId \"{clientId}\" --clientSecret \"{clientSecret}\"";
            var (Success, Output, Error) = await ExecutePacCommand(authCommand);

            if (!Success)
            {
                _logger.LogError($"PAC authentication failed. Output: {Output}. Error: {Error}");
                throw new Exception($"Failed to authenticate PAC CLI: {Error}");
            }
        }

    }
}
