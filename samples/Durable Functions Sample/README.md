# Create your first C# Durable Functions on Azure Container Apps

Durable Functions is an extension of Azure Functions that lets you write stateful functions in a serverless environment. The extension manages state, checkpoints, and restarts for you.

This quickstart will show you how to create a Durable Function that runs on Azure Container Apps. 

> Note: Only the [MSSQL storage provider](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-storage-providers#mssql) is currently supported for running Durable Functions in Azure Container Apps. . 

## **Create the local C# isolated Functions project**

Before starting, check that you have the right versions specified in this[prequisites table](https://github.com/Azure/azure-functions-on-container-apps#prerequisites). 

1\. Run the func init command, as follows, to create a functions project in a folder named *LocalFunctionProj* with the specified runtime:
Below sample built for .NET 7

```sh
func init LocalFunctionProj --worker-runtime dotnet-isolated --docker --target-framework net7.0
```

The \--docker option generates a Dockerfile for the project, which defines a suitable custom container for use with Azure Functions and the selected runtime.

2\. Navigate into the project folder

```sh
cd LocalFunctionProj
```
This folder contains the Dockerfile and other files for the project, including configurations files named [local.settings.json](https://learn.microsoft.com/azure/azure-functions/functions-develop-local#local-settings-file) and [host.json](https://learn.microsoft.com/azure/azure-functions/functions-host-json).
By default, the *local.settings.json* file is excluded from source control in the *.gitignore* file. This exclusion is because the file can contain secrets that are downloaded from Azure.

3\. Open the Dockerfile to include following (Usually found in Line 13)

> FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated7.0

> This version of the base image supports Azure Functions deployment to an Azure Container Apps service check to include below as well in the Dockerfile (found in Line 1)
> FROM mcr.microsoft.com/dotnet/sdk:7.0 AS installer-env 

Sample Dockerfile for .NET 7
```sh
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0 AS installer-env

# Build requires 3.1 SDK
#COPY --from=mcr.microsoft.com/dotnet/core/sdk:3.1 /usr/share/dotnet /usr/share/dotnet

COPY . /src/dotnet-function-app
RUN cd /src/dotnet-function-app && \
  mkdir -p /home/site/wwwroot && \
  dotnet publish *.csproj --output /home/site/wwwroot

  #WORKDIR /src/dotnet-function-app
#RUN dotnet publish *.csproj --output /home/site/wwwroot

# To enable ssh & remote debugging on app service change the base image to the one below
# FROM mcr.microsoft.com/azure-functions/dotnet-isolated:3.0-dotnet-isolated5.0-appservice
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated7.0
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=installer-env ["/home/site/wwwroot", "/home/site/wwwroot"]
```
> Note : If you are using .NET 7 then remember to update the .csproj file <TargetFramework> to point to net7.0 as shown below
  ```sh
    <TargetFramework>net7.0</TargetFramework>
   ```

4\. Create a C# file called *DurableFunctionsOrchestrationCSharp.cs* and add your Durable Functions orchestration to it: 

```c#
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public static class DurableFunctionsOrchestrationCSharp
    {
        [Function(nameof(DurableFunctionsOrchestrationCSharp))]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(DurableFunctionsOrchestrationCSharp));
            logger.LogInformation("Saying hello.");
            var outputs = new List<string>();

            // Replace name and input with values relevant for your Durable Functions Activity
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [Function(nameof(SayHello))]
        public static string SayHello([ActivityTrigger] string name, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("SayHello");
            logger.LogInformation("Saying hello to {name}.", name);
            return $"Hello {name}!";
        }

        [Function("DurableFunctionsOrchestrationCSharp_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("DurableFunctionsOrchestrationCSharp_HttpStart");

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(DurableFunctionsOrchestrationCSharp));

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}

```

5\. Install the required extensions using standard NuGet package installation methods, such as `dotnet add package`
- [Microsoft.Azure.Functions.Worker.Extensions.DurableTask]()
- [Microsoft.Azure.Functions.Worker.Extensions.DurableTask.SqlServer]()
- [Microsoft.Azure.Functions.Worker.Extensions.Http]()

6\. Make sure the following package references are in the *.csproj* file: 
```sh
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.DurableTask" Version="1.0.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.DurableTask.SqlServer" Version="1.1.1" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http" Version="3.0.13" />
```

## Setting up database 

As the MSSQL backend is designed for portability, you have several options to set up your backing database. For example, you can set up an on-premises SQL Server instance, use a fully managed Azure SQL DB, or use any other SQL Server-compatible hosting option.

You can also do local, offline development with SQL Server Express on your local Windows machine or use SQL Server Docker image running in a Docker container. To set up a local Docker-based SQL Server, follow [these instructions](https://learn.microsoft.com/azure/azure-functions/durable/quickstart-mssql#set-up-your-local-docker-based-sql-server). 

To run your app on Azure, you'll need a publicly accessible SQL Server instance. If you want to create an Azure SQL Database, follow [these instructions](https://learn.microsoft.com/azure/azure-functions/durable/quickstart-mssql#create-an-azure-sql-database). 

Whether you are running your app locally or on Azure, be sure to update your *local.settings.json*:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true", 
    "SQLDB_Connection": "<<Your connection string >>",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```
and *host.json*:

```json
{
  "version": "2.0",
  "extensions": {
    "durableTask": {
      "storageProvider": {
        "type": "mssql",
        "connectionStringName": "SQLDB_Connection",
        "createDatabaseIfNotExists": true 
        }
    }
  },
  "logging": {
    "logLevel": {
      "DurableTask.SqlServer": "Warning",
      "DurableTask.Core": "Warning"
    }
  }
}  
```

## Build the container image 

The Dockerfile in the project root describes the minimum required environment to run the function app in a container. The complete list of supported base images for Azure Functions is documented above as **Host images** in the pre-requisites section or can be found in the [Azure Functions Base by Microsoft \| Docker
Hub](https://hub.docker.com/_/microsoft-azure-functions-base)


1\. In the root project folder, run the [docker build](https://docs.docker.com/engine/reference/commandline/build/) command, and provide a name, azurefunctionsimage, and tag, v1.0.0.
```sh
docker build --platform linux --tag <DOCKER_ID>/azurefunctionsimage:v1.0.0 .
```

In this example, replace \<DOCKER_ID\> with your Docker Hub account ID. When the command completes, you can run the new container locally.

2\. To test the build, run the image in a local container using the [docker run](https://docs.docker.com/engine/reference/commandline/run/) command, with the adding the ports argument, -p 8080:80.
```sh
docker run -p 8080:80 -it <docker_id>/azurefunctionsimage:v1.0.0
```
Again, replace <DOCKER_ID with your Docker ID and adding the ports argument, -p 8080:80

3\. After the image is running in a local container, browse to http://localhost:8080/api/HttpExample?name=Functions, which should display the same "hello" message as before. Because the HTTP triggered function uses anonymous authorization, you can still call the function even though it\'s running in the container. Function access key settings are enforced when running locally in a container. If you have problems calling the function, make sure that [access to the function](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger#authorization-keys) is set to anonymous.

4\. After you've verified the function app in the container, stop docker with **Ctrl**+**C**.

Docker Hub is a container registry that hosts images and provides image and container services. To share your image, which includes deploying to Azure, you must push it to a registry.
**Docker login**

5\.  If you haven\'t already signed in to Docker, do so with the [docker login](https://docs.docker.com/engine/reference/commandline/login/) command, replacing  <docker_id> with your Docker ID. This command prompts you for your username and password. A "Login Succeeded" message confirms that you\'re signed in.

6\. After you\'ve signed in, push the image to Docker Hub by using the [docker push](https://docs.docker.com/engine/reference/commandline/push/) command, again replacing <docker_id> with your Docker ID.
```sh
docker push <docker_id>/azurefunctionsimage:v1.0.0
```
 7\. Depending on your network speed, pushing the image the first time might take a few minutes (pushing subsequent changes is much faster). While you\'re waiting, you can proceed to the next section and create Azure resources in another terminal.
 

## Run your Durable Functions on Azure 

Before you can deploy your container to your Azure Container apps you need to create two more resources

a\. A [Storage account](https://learn.microsoft.com/azure/storage/common/storage-account-create). 

b\. Create the Container Apps environment with a Log Analytics workspace

1\. Login to your Azure subscription

```sh 
az login
  
az account set -subscription | -s <subscription_name>

az upgrade

az extension add --name containerapp --upgrade

az provider register --namespace Microsoft.Web

az provider register --namespace Microsoft.App

az provider register --namespace Microsoft.OperationalInsights
```
---

2\. Create azure container app environment

Create an environment with an auto-generated Log Analytics workspace.

```sh
  az group create --name MyResourceGroup --location northeurope
  az containerapp env create -n MyContainerappEnvironment -g MyResourceGroup --location northeurope
  ```
3\.  Create Storage account

Use the [az storage account create](https://learn.microsoft.com/en-us/cli/azure/storage/account#az-storage-account-create) command to create a general-purpose storage account in your resource group and region:

```sh
az storage account create --name <STORAGE_NAME> --location northeurope --resource-group MyResourceGroup --sku Standard_LRS
  ```
Replace <STORAGE_NAME> with a name that is appropriate to you and unique in Azure Storage. Names must contain three to 24 characters numbers and lowercase letters only. Standard_LRS specifies a general-purpose account, which is [supported by Functions](https://learn.microsoft.com/azure/azure-functions/storage considerations#storage-account-requirements). The --location value is a standard Azure region.

4\. Create the function app
 
 Run the [az functionapp create](https://learn.microsoft.com/en-us/cli/azure/functionapp#az-functionapp-create) command to create a new function app in the new managed environment backed by azure container apps.

```sh
az functionapp create --resource-group MyResourceGroup --name <functionapp_name> \
--environment MyContainerappEnvironment \
--storage-account <Storage_name> \
--functions-version 4 \
--runtime dotnet-isolated \
--image <DOCKER_ID>/<image_name>:<version> 
```

In this example, replace **MyContainerappEnvironment** with the Azure container apps environment name. Also, replace <STORAGE_NAME> with the name of the account you used in the previous step, <APP_NAME> with a globally unique name appropriate to you, and <DOCKER_ID> or <login-server> with your Docker Hub ID.

5\. Set required app settings

Get you Azure Storage and Azure SQL connection strings from Azure Portal. Then go to your function app on portal, look for *Configuration* (under *Settings* on the left menu), and update the value of `AzureWebJobsStorage` with Azure Storage's connection string. If you don't see a setting for `SQLDB_Connection`, create one and set its value to Azure SQL's connection string. 

6\. Start your Durable Functions orchestration

Because the sample code above uses an HTTP trigger to start the Durable Functions orchestration, you can make an HTTP request to its URL in the browser to start the orchestration. To do that, find the *URL* of your app on Azure Portal (look on the top right of the "Essentials" section in the "Overview" tab), it should look something like the following:

```
https://<< your app name >>.northeurope.azurecontainerapps.io
```

Add the following to the end of the URL:

```
/api/{your client function name}
```

In the sample code above, the name is `DurableFunctionsOrchestrationCSharp_HttpStart`. 

You should see a json that looks something like the following when you make a request to the URL: 

```json
{
    "id":"3fa7eb4568c9416cb71c4aa7100887e6",
    "purgeHistoryDeleteUri":"http://durable-on-aca2.calmwave-40e10600.northeurope.azurecontainerapps.io/runtime/webhooks/durabletask/instances/3fa7eb4568c9416cb71c4aa7100887e6?code=LiHbNJwA5aabwZmST-liTxADINRDFaeh6oE-pcI1KinNAzFuho2KUQ==",
    "sendEventPostUri":"http://durable-on-aca2.calmwave-40e10600.northeurope.azurecontainerapps.io/runtime/webhooks/durabletask/instances/3fa7eb4568c9416cb71c4aa7100887e6/raiseEvent/{eventName}?code=LiHbNJwA5aabwZmST-liTxADINRDFaeh6oE-pcI1KinNAzFuho2KUQ==",
    "statusQueryGetUri":"http://durable-on-aca2.calmwave-40e10600.northeurope.azurecontainerapps.io/runtime/webhooks/durabletask/instances/3fa7eb4568c9416cb71c4aa7100887e6?code=LiHbNJwA5aabwZmST-liTxADINRDFaeh6oE-pcI1KinNAzFuho2KUQ==",
    "terminatePostUri":"http://durable-on-aca2.calmwave-40e10600.northeurope.azurecontainerapps.io/runtime/webhooks/durabletask/instances/3fa7eb4568c9416cb71c4aa7100887e6/terminate?reason={{text}}}\u0026code=LiHbNJwA5aabwZmST-liTxADINRDFaeh6oE-pcI1KinNAzFuho2KUQ=="
}
```

You can check the status of the orchestration by going to the URL provided by *statusQueryGetUri*, which should return:

```json
{
    "name":"DurableFunctionsOrchestrationCSharp",
    "instanceId":"3fa7eb4568c9416cb71c4aa7100887e6",
    "runtimeStatus":"Completed",
    "input":null,
    "customStatus":null,
    "output":["Hello Tokyo!","Hello Seattle!","Hello London!"],
    "createdTime":"2023-05-17T19:30:10Z",
    "lastUpdatedTime":"2023-05-17T19:30:13Z"
}
```
