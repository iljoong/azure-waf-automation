# FX Local Development

## Prep

Develop for dotnetcore 3 runtime

- [VSCode latest](https://code.visualstudio.com/Download)
  - It is recommended to install _Azure Functions extension_ for VSCode
- [.netcore 3.1 sdk](https://dotnet.microsoft.com/download/dotnet-core/3.1)
- [Function CLI(v3)](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=windows%2Ccsharp%2Cbash)

> Use azure storage or install [Storage Emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator) (+SQLExpress)

## Local development

- conditional build using preprocess directive

https://stackoverflow.com/questions/10714668/how-do-you-pass-conditional-compilation-symbols-defineconstants-to-msbuild

add define constant, such as `LOCALDEV`, in `.proj` file

```
  <PropertyGroup>
    ...
    <DefineConstants>LOCALDEV</DefineConstants>
  </PropertyGroup>
```
- `local.settings.json

add following environment variables in this file. 

```
    "SQL_CONNECTION": "sql_connection_string_with_username_password",
    "AZ_TENANTID": "tenantid of aad",
    "AZ_CLIENTID": "clientid of service principal",
    "AZ_SECRET": "secret of service principal",

    "subscriptionid": "subscriptionid",
    "resourcegroup": "rgname",
    "resourcename": "wafpolicyname",
    "clientid": "clientid of managed identity"
```

## Auth Token from IMDS

- For Azure service management api, get token using service principal.
- For SQL token, use connection string with admin/passwd.

## Enhancement

### Durable Function

https://docs.microsoft.com/en-us/azure/azure-functions/functions-overview

### Singleton

> See [Singleton Function](https://docs.microsoft.com/en-us/azure/app-service/webjobs-sdk-how-to#singleton-attribute) for more information.

use `[Singleton(Mode = SingletonMode.Function)]` to avoid any race condition issue with updating WAF policy.
when adding and removing call happens concurrently, it may corrupt the state. 

```
state: ["1.1.1.1", "2.2.2.2"]

//no singleton
remove(1.1.1.1)------->(get state)------->(remove ip)=>state (2.2.2.2)
         add(3.3.3.3)----------->(get state)----------->(add ip_ (1.1.1., 2.2.2.2, 3.3.3.3)
//singleton
remove(1.1.1.1)-->get state-->(remove ip)=>state (2.2.2.2)-->add(3.3.3.3)-->get state-->(add ip)=>state(1.1.1.1, 3.3.3.3)
```

### Multi-tenant 

Support multi-tenant WAF policy
