
# Clone Copilot Studio Bot

This Azure Function automates the process of cloning Copilot Studio bots into a target Microsoft Dataverse environment using Power Apps CLI (PAC CLI) commands. It's packaged in a Docker container for easy deployment and scalability.

## Features

- Clone Copilot Studio bots with new display names and schema names.
- Parameter-driven execution allows for dynamic input via API calls.
- Docker containerization ensures compatibility and easy deployment across environments.

## Prerequisites

Before you begin, ensure you have the following:
- An Azure account with the necessary permissions to deploy Azure Functions.
- Docker installed on your machine.
- Access to a Microsoft Dataverse environment where the "CloneBotHolders" solution is installed.

## Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/yourusername/clone-copilot-studio-bot.git
   ```
2. **Navigate to the repository directory:**
   ```bash
   cd clone-copilot-studio-bot
   ```

3. **Build the Docker image:**
   ```bash
   docker build -t clonecopilot-function .
   ```

4. **Run the Docker container:**
   ```bash
   docker run -p 8080:80 -e AZURE_TENANT_ID=your-tenant-id -e AZURE_CLIENT_ID=your-client-id -e AZURE_CLIENT_SECRET=your-client-secret -e DYNAMICS_URL=https://yourcrm.crm.dynamics.com/ clonecopilot-function
   ```

## Usage

To clone a Copilot, send a POST request to the function's endpoint with the appropriate JSON body:

**Endpoint:**
```
https://clonecopilotstudiobot.azurewebsites.net/api/CopilotClone
```

**Sample JSON Body:**
```json
{
  "environmentId": "your-environment-id",
  "botId": "your-bot-id",
  "newCopilotDisplayName": "Cloned Copilot",
  "newCopilotSchemaName": "ibm_clonedcopilot",
  "newCopilotSolution": "CloneBotHolders"
}
```

Ensure that the "CloneBotHolders" solution exists in the target Dataverse environment before making the request.

## Contributing

Contributions are what make the open-source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

Distributed under the MIT License. See `LICENSE` for more information.

