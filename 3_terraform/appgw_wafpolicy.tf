resource "azurerm_web_application_firewall_policy" "tfrg" {
  name                = "test-wafpolicy"
  resource_group_name = azurerm_resource_group.tfrg.name
  location            = var.location

  custom_rules {
    name      = "IPBlock"
    priority  = 100
    rule_type = "MatchRule"

    match_conditions {
      match_variables {
        variable_name = "RemoteAddr"
      }

      operator           = "IPMatch"
      negation_condition = false
      match_values       = ["10.10.10.10"]
    }
    action = "Block"
  }

  policy_settings {
    enabled                     = true
    mode                        = "Prevention"
    request_body_check          = true
    file_upload_limit_in_mb     = 100
    max_request_body_size_in_kb = 128
  }

  managed_rules {
    managed_rule_set {
      type    = "OWASP"
      version = "3.0"
    }
  }

}