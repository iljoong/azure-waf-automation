## Azure Function

> It is recommend to enable access restriction in _Netorking Settings_

Call azure function

```bash
curl --location --request POST 'https://sktsecfxapp.azurewebsites.net/api/BlockIPWaf?code=...==' \
--header 'Content-Type: application/json' \
--data-raw '{
    "subscriptionid": "...",
    "resourcegroup": "test-sktsec-rg",
    "resourcename": "test-wafpolicy",
    "clientid": "...",
    "blockips": [
        "10.10.10.10",
        "182.229.104.28"
    ]
}'
```