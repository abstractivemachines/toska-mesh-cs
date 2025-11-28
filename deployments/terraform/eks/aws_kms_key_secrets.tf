resource "aws_kms_key" "secrets" {
  description             = "${local.name} shared secret encryption"
  deletion_window_in_days = 10
  enable_key_rotation     = true
  tags                    = local.tags
}
