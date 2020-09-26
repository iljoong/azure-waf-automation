resource "azurerm_sql_server" "sqldb" {
  name                         = "${var.prefix}sqlsvr"
  resource_group_name          = azurerm_resource_group.tfrg.name
  location                     = var.location
  version                      = "12.0"
  administrator_login          = var.admin_username
  administrator_login_password = var.admin_password
}

resource "azurerm_sql_database" "sqldb" {
  name                         = "${var.prefix}db"
  resource_group_name          = azurerm_resource_group.tfrg.name
  location                     = var.location
  server_name                  = azurerm_sql_server.sqldb.name

  edition                          = "Standard"
  requested_service_objective_name = "S0"
}

resource "azurerm_sql_active_directory_administrator" "sqldb" {
  server_name         = azurerm_sql_server.sqldb.name
  resource_group_name = azurerm_resource_group.tfrg.name
  login               = "sqladmin"
  tenant_id           = var.tenant_id
  object_id           = data.azurerm_user_assigned_identity.fxapp.principal_id
}