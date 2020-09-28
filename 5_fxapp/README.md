# Azure Functions

This Azure Function is developed with [C# script (csx)](https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference-csharp) and targeted for dotnetcore 2.x runtime.

You may upgrade to 3.x runtime and use other languages.

> It is recommend to enable access restriction in _Netorking Settings_

## Setup environment variables

Function application needs environment variables to get right access tokens and add following variables in __Application settings__. 

- __subscriptionid__: subscription id of wafpolicy
- __resourcegroup__: resourcegroup of wafpolicy
- __resourcename__: wafpolicy name
- __clientid__: client id of managed identity
- __sqlserverfqdn__: FQDN of sqlserver
- __databasename__: database name

## Deploy function app

You will deploy two function apps manually and you need to create two functions app before deploy them.

### __BlockIPWaf__ function app

- create as a _HTTP trigger_ function.
- copy/deploy the [BlockIPWaf.csx](./src/BlockIPWaf/BlockIPWaf.csx) file to the function.

### __TTLTimer__ function app

- create as a _Timer trigger_ function and set 1 minute timer(0 */1 * * * *)
- copy/deploy the [TTLTimer.csx](./src/TTLTimer/TTLTimer.csx) file to the function.

