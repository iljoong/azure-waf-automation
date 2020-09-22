## README

To run this script, you need to update variables in `variables.tf`

> Not that you need to provide pfx format certificate in order to enable HTTPS settings. If you don't have SSL certificate than you can remove Cert/HTTPS parts in `appgw.tf`. 

To run terraform,

```
terraform init

terraform validate

terraform apply
```