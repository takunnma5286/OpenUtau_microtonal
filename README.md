[日本語版はこちら](./README_ja.md)

# OpenUtau_microtonal

** This project is under active development! So expect instabilities. **

This is a fork of OpenUTAU with the goal of adding support for microtonality. At the moment, it supports equal temperaments and loading .tun files only when using the WORLDLINE-R renderer, ~~classic renderer, or VOICEVOX~~.

> [!WARNING]
> ### Browser Version Caveats
> **This branch has been modified to run in the browser using WebAssembly.** Because of this environment, please be aware of the following limitations compared to the desktop version:
> - **Virtual File System**: The application runs in a browser sandbox and cannot directly access your local files. You must use the browser's file picker dialogs to load projects, voicebanks, and audio files.
> - **Performance**: Because it runs in WebAssembly, audio rendering runs synchronously on the main thread. This may cause the UI to freeze temporarily during heavy operations like playback or export.
> This can be mitigated by increasing the "Playback Buffer Size" option under Preferences -> Playback.

For more information about OpenUTAU, visit [the repository for OpenUTAU](https://github.com/stakira/OpenUtau).
For support, you can contact @takunnma on Discord. You can find him on [this server](https://discord.gg/k3Cp7kW6Jv).

## Usage

Make sure you are using WORLDLINE-R.

<img width="301" height="106" alt="image" src="https://github.com/user-attachments/assets/0354b417-215a-4de7-95e5-12af4f26099a" />

Open this menu:

<img width="295" height="97" alt="image" src="https://github.com/user-attachments/assets/cff18af9-f9e8-48b8-8c85-da44f12aa35b" />

and click on Project Settings. You should see a menu like this:

<img width="348" height="229" alt="image" src="https://github.com/user-attachments/assets/ac19d722-5ef5-430f-8c75-3ef5e1e76083" />

Set the desired number of divisions in the 'Equal Temperament (Divisions per octave)' field. (You typically do not need to change the two options below.)

<img width="493" height="423" alt="image" src="https://github.com/user-attachments/assets/25fcf692-802f-4723-8366-27745850146a" />

