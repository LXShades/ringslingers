# DISCLAIMER
This is a fangame of [SRB2](http://srb2.org) and contains assets by Sonic Team JR, SEGA and Sonic Team. If you haven't checked out the [original long-running hit Sonic fangame](http://srb2.org), go check it out now!

# Ringslingers
Blast other Sonic characters with rings in this fast-paced multiplayer fangame inspired by Sonic Robo Blast 2's Ringslingers game modes!

This fangame is made in Unity. The current required Unity version is **2023.1.11f1**.

**This open-source game is non-profit.** Not all content is original, see Credits.

Everything is WIP! This readme was written at 11pm on a Sunday after a long day of fixing movement code and drinking extremely tasty imported American Dr Pepper (the sugar tax ruined the UK version, I swear).

# Compiling and opening
**Make sure to follow these steps before opening the project in Unity.**

* Step 1. Clone the repo. Ensure LFS is enabled. Clone recursively (if not, run a Submodule Update after pulling). This repo currently uses multiple submodules.  
* Step 2. Delete the Mirror folder in UnityMultiplayerEssentials (it conflicts with Mirror in the root folder).  
* Step 3. Comment out the lines in IgnoranceToolbox that reference ENet.Native. For some reason this just doesn't work (third-party issue?)  
* Step 4. Open in Unity.  

This is all work-in-progress, and I understand it's a pain to deal with a couple of these steps. I'm still learning best practices and welcome any feedback to improve this in the future.

# Playing the game in engine
* Step 1. Open a map (for example, Core/Scenes/TEST_Gameplay.unity).  
* Step 1.5. (Make sure the map is in the Build Settings, and that Boot is the top map. This is usually fine as-is.)  
* Step 2. Ensure the Playtest menu has Autohost in Playmode checked. This makes you start as a host and enters your character into the game when you play.  
* Step 3. Click the Play button.  

Sometimes, Unity crashes with a MemoryStream corruption error on the first run. I have no clue why it happens. The solution is to open and run again.

# Testing the game with multiple players
Use the Playtest menu to run multi-player playtests. **The player count can be inaccurate sometimes because there is always a host character** and the playtest menu doesn't realize this yet (this will probably be fixed in the future).

# Licenses
Code in this repo, **outside of the ThirdParty folder, packages and submodules**, is free to use for any purpose without credit under Creative Commons 0. Code within the [Mirror](https://github.com/vis2k/mirror) and [Ignorance](https://github.com/SoftwareGuy/Ignorance) submodules, and possibly others during development, have their own licenses. See relevant credits. Some assets are subject to copyright - see below.

# Credits
This fangame contains ripped assets, music and levels, not only from Sonic itself, but another Sonic fangame: [Sonic Robo Blast 2](http://srb2.org) developed by Sonic Team Junior. All level designs, textures, sound effects, music and characters belong to their respective owners. Sonic and related properties are copyrighted properties of SEGA Corporation.

This fangame and its creator(s) has no direct relationship or affiliation with these parties.

### Contributors
LXShades - The core project, a whole lot of code, map ripping and importing, character models and animations.
Spazzo - UI assistance

### Libraries and third-party sources:  
[Mirror](https://github.com/vis2k/mirror)  
[Ignorance](https://github.com/SoftwareGuy/Ignorance)  
[MutationNetwork](https://github.com/kipgparker/MutationNetwork)  
[RealtimeCSG](https://github.com/LogicalError/realtime-CSG-for-unity)  