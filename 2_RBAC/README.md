# RBAC for WAF Automation

## User assigned identity

Create a user assigned identity for Function.

```bash
az identity create -g <resource group> -n <identity name>
```

> Copy value of `principalId` from the output. Note that `princialId` is also called `object id`.

See [azure docs](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-to-manage-ua-identity-cli).

## Assign Role (WafOps)

> You may need AAD global admin role to avoid this error `"Role definition limit exceeded. No more role definitions can be created."`.

Create a new role definition for WAF Ops role and assign to the user assigned identity you just created.

```bash
az role definition create --role-definition role_wafops.json
```

Assign role to the managed identity you just created. Use the value of managed identity's `object id` or ``principalId` as an assignee input.

```bash
az role assignment create --assignee {object id} --role 'WafOps' --scope '/subscriptions/{subscription id}'
```
