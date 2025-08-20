# config.nu
#
# Installed by:
# version = "0.105.1"
#
# This file is used to override default Nushell settings, define
# (or import) custom commands, or run any other startup tasks.
# See https://www.nushell.sh/book/configuration.html
#
# This file is loaded after env.nu and before login.nu
#
# You can open this file in your default editor using:
# config nu
#
# See `help config nu` for more options
#
# You can remove these comments if you want or leave
# them for future reference.

# Set the buffer editor to code
$env.config.buffer_editor = "code"

# Disable Welcome Banner
$env.config.show_banner = false

# starship configuration (Warning This will change in the future. Only Nushell v0.96 or higher is supported.)
mkdir ($nu.data-dir | path join "vendor/autoload")
starship init nu | save -f ($nu.data-dir | path join "vendor/autoload/starship.nu")

#Alias list
alias k = kubectl
