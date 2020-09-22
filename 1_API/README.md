# REST API for WAF Policy

## Access Token 

1. Prepare AAD SP

create Azure AD service principal using CLI.

> This SP assigned as `Contributor` role. You can assign to other role, such as `Network Contributor`.

```bash
az ad sp create-for-rbac --role="Contributor" --scopes="/subscriptions/{SUBSCRIPTION_ID}"
```

You will use the information () from the CLI output. Note that, `appId` is `client_id` and `password` is `client_secret`.

```
{
  "appId": "0000000000-0000-0000-0000-0000000000",
  "displayName": "azure-cli-2018-11-15-07-19-22",
  "name": "http://azure-cli-2018-11-15-07-19-22",
  "password": "xxxxxxxxxx-xxxx-xxxx-xxxxxxxxxxx",
  "tenant": "11111111-1111-1111-11111-11111111111"
}
```

2. Get access token

```bash
curl --location --request POST 'https://login.microsoftonline.com/{tenant_id}/oauth2/token' \
--header 'Content-Type: application/x-www-form-urlencoded' \
--data-urlencode 'grant_type=client_credentials' \
--data-urlencode 'resource=https://management.azure.com/' \
--data-urlencode 'client_id={client_id}' \
--data-urlencode 'client_secret={client_secret}'
```

## WAF policy API

1. Get WAF policy

```bash
curl --location --request GET 'https://management.azure.com/subscriptions/{subscription_id}/resourceGroups/{resourcegroup_name}/providers/Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies/{resource_name}?api-version=2020-06-01' \
--header 'Authorization: Bearer {access_token}' \
--data-raw ''
```

2. Update WAF policy

> Note that no documentation regarding limitation of custom rule array. Personal test showed that about _650 custom rule array_ supported.

```bash
curl --location --request PUT 'https://management.azure.com/subscriptions/{subscription_id}/resourceGroups/{resourcegroup_name}/providers/Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies/{resource_name}?api-version=2020-06-01' \
--header 'Authorization: Bearer {access_token}' \
--header 'Content-Type: application/json' \
--data-raw '{
  "location": "koreacentral",
  "properties": {
    "customRules": [
      {
        "name": "IPBlock",
        "priority": 100,
        "ruleType": "MatchRule",
        "action": "Block",
        "matchConditions": [
          {
            "matchVariables": [
              {
                "variableName": "RemoteAddr"
              }
            ],
            "operator": "IPMatch",
            "negationConditon": false,
            "matchValues": [
              "10.10.10.10"
            ],
            "transforms": []
          }
        ],
        "skippedManagedRuleSets": []
      }
    ],
    "policySettings": {
      "requestBodyCheck": true,
      "maxRequestBodySizeInKb": 128,
      "fileUploadLimitInMb": 100,
      "state": "Enabled",
      "mode": "Prevention"
    },
    "managedRules": {
      "managedRuleSets": [
        {
          "ruleSetType": "OWASP",
          "ruleSetVersion": "3.0",
          "ruleGroupOverrides": []
        }
      ],
      "exclusions": []
    }
  }
}'
```