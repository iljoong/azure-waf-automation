# Configure the Microsoft Azure Provider
provider "azurerm" {
  subscription_id = var.subscription_id
  client_id       = var.client_id
  client_secret   = var.client_secret
  tenant_id       = var.tenant_id

  features {}
}

resource "azurerm_resource_group" "tfrg" {
  name                = "test-${var.prefix}-rg"
  location            = var.location
}

resource "azurerm_virtual_network" "tfvnet" {
  name                = "${var.prefix}-vnet"
  resource_group_name = azurerm_resource_group.tfrg.name
  address_space       = ["10.1.0.0/16"]
  location            = var.location
}

resource "azurerm_subnet" "appgwsnet" {
  name                = "AppGWSubnet"
  virtual_network_name = azurerm_virtual_network.tfvnet.name
  resource_group_name  = azurerm_resource_group.tfrg.name
  address_prefixes     = ["10.1.1.0/24"]
}

resource "azurerm_subnet" "appsnet" {
  name                = "AppSubnet"
  virtual_network_name = azurerm_virtual_network.tfvnet.name
  resource_group_name  = azurerm_resource_group.tfrg.name
  address_prefixes     = ["10.1.2.0/24"]
}

resource "azurerm_network_security_group" "appnsg" {
  name                = "${var.prefix}-app-nsg"
  location            = var.location
  resource_group_name = azurerm_resource_group.tfrg.name

  security_rule {
    name                       = "SSH"
    priority                   = 1000
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "22"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }

  security_rule {
    name                       = "HTTP"
    priority                   = 1100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "80"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }
}

