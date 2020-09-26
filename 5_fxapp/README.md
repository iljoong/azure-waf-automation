# Azure Functions

This Azure Function is developed with C# script (csx) and targeted for dotnetcore 2.x runtime.

You may upgrade to 3.x runtime and use other languages.

> It is recommend to enable access restriction in _Netorking Settings_

## Setup environment variables

Function application needs following environment variables and add variables in __Application settings__. 

- subscriptionid: subscription id of wafpolicy
- resourcegroup: resourcegroup of wafpolicy
- resourcename: wafpolicy name
- clientid: client id of managed identity
- sqlserverfqdn: FQDN of sqlserver
- databasename: database name

## Deploy function app

You will deploy two function apps manually and you need to create two functions app before deploy them.

### __BlockIPWaf__ function app

- create as a _HTTP trigger_ function.
- copy/deploy the [BlockIPWaf.csx](./fx_v2/BlockIPWaf/BlockIPWaf.csx) file to the function.

### __TTLTimer__ function app

- create as a _Timer trigger_ function and set 1 minute timer(0 */1 * * * *)
- copy/deploy the [TTLTimer.csx](./fx_v2/TTLTimer/TTLTimer.csx) file to the function.

