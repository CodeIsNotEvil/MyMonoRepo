## The Nushell Configuiration.

The Documentation can be foud at [nushell.sh](https://www.nushell.sh/book/configuration.html)

### Where to find ther config file
Default location Linux: `~/.config/nushell/config.nu`

Default location Windows: `C:\Users\<USERNAME>\AppData\Roaming\nushell\config.nu`

The path to the config is defined with: `$nu.default-config-dir`

### How to Edit

Typeing `config nu` inside nushell opens the config in the `$env.config.buffer_editor` defined editor


## Installing Starship for Nushell

Install vis the install script
```bash
curl -sS https://starship.rs/install.sh | sh
 ```

Follow the instructions for nushell, which sould be prompted inside the terminal or got to the [website](https://starship.rs/#nushell)

Install a Template like _Gruvbox rainbow_
```bash
starship preset gruvbox-rainbow -o ~/.config/starship.toml
```

Install nerd fonts like _AdwaitaMonoFont_
```bash
sudo cp ~/Repositories/MyMonoRepo/Configurations/Terminals/Nushell/AdwaitaMonoFont/* /usr/share/fonts/
```

Setup the Terminal to use this font.

In Pop!_OS it is done through the preferences of the Profile. These options are availabe in the Terminal Window throgh the Hamburger menu.