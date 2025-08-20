# Collection of Steam Launch Configurations.

Since the Launch options inside Steam are system specific. This list will keep them for me to setup a new installtion when needed.

## Sid Meier's Civilization VI

Since Civ VI has been updated for Windows but not for Linux and Mac, activate Proton(V7, V8, V9 all worked).

The Launcher does not start on Linux, but we can just run the binary.

```bash
eval $( echo "%command%" | sed "s/2KLauncher\/LauncherPatcher.exe'.*/Base\/Binaries\/Win64Steam\/CivilizationVI'/" )
```

## Valheim

Runs Native, Inorder to keep mods working.

This skript simpliefies the process of setting up Valheim on a new installtion because it is all contained inside the game Library

Note: I keep my games folder since it is its own drive anyway. 

```bash
~/games/SteamLibrary/steamapps/common/Valheim/start_Valheim.sh %command%
```
The start_Valheim.sh skript content 
```bash
cd ~/games/SteamLibrary/steamapps/common/Valheim/
~/games/SteamLibrary/steamapps/common/Valheim/start_game_bepinex.sh
```

## Worms W.M.D

Activate Proton (V8, V9 worked).
