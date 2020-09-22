resource "azurerm_app_service_plan" "fxapp" {
  name                      = "${var.prefix}-svcplan"
  location                  = azurerm_resource_group.tfrg.location
  resource_group_name       = azurerm_resource_group.tfrg.name
  kind                      = "FunctionApp"

  sku {
    tier = "Dynamic"
    size = "Y1"
  }
}

resource "azurerm_storage_account" "fxapp" {

  name                      = "${var.prefix}000stor"
  location                  = azurerm_resource_group.tfrg.location
  resource_group_name       = azurerm_resource_group.tfrg.name
  account_tier              = "Standard"
  account_replication_type  = "GRS"

}

data "azurerm_user_assigned_identity" "fxapp" {
  name                      = var.identityname
  resource_group_name       = var.identityrgname
}

resource "azurerm_function_app" "fxapp" {
  name                       = "${var.prefix}fxapp"
  location                   = azurerm_resource_group.tfrg.location
  resource_group_name        = azurerm_resource_group.tfrg.name
  app_service_plan_id        = azurerm_app_service_plan.fxapp.id
  storage_account_name       = azurerm_storage_account.fxapp.name
  storage_account_access_key = azurerm_storage_account.fxapp.primary_access_key

  version = "~2"

  identity {
      type                   = "UserAssigned"
      identity_ids           = [data.azurerm_user_assigned_identity.fxapp.id]
  }

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME                 = "dotnet"
    WEBSITE_CONTENTAZUREFILECONNECTIONSTRING = azurerm_storage_account.fxapp.primary_connection_string
    WEBSITE_CONTENTSHARE                     = "fxapp" #azurerm_storage_account.tfrg.name

    # change default key management, https://github.com/Azure/azure-functions-host/wiki/Changes-to-Key-Management-in-Functions-V2
    AzureWebJobsSecretStorageType = "Files"
  }
}

output "fxapp_site_credentials" {
  value = azurerm_function_app.fxapp.site_credential
}