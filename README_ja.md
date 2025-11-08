[English version is here](./README.md)

# OpenUtau_microtonal

** このプロジェクトは現在開発中のため、動作が不安定な場合があります。 **

これは、歌声合成ソフト OpenUTAU に微分音機能を追加するためのフォーク版です。現時点では、WORLDLINE-R レンダラー使用時のみ、平均律（EDO）に対応しています。また、VOICEVOX音源でも動作します。

OpenUTAU本体に関する詳しい情報は、[OpenUTAUのリポジトリ](https://github.com/stakira/OpenUtau)をご覧ください。
サポートについては、Discordで開発者 @takunnma に連絡するか、彼が参加しているこちらの[サーバー](https://discord.gg/k3Cp7kW6Jv)にて、本人に直接お問い合わせください。

## インストール

まず、[最新のベータ版](https://github.com/takunnma5286/OpenUtau_microtonal/releases/tag/beta)をダウンロードしてください、既存のOpenUTAUと並行してインストールすることができます。既存のインストールと同じフォルダやファイルを読み込むため、ボイスバンクやフォネマイザーなどを移動・コピーする必要はありません。

お使いのOSに合わせて、以下の画像を参考にファイルをダウンロードしてください↓

<img width="602" height="502" alt="image" src="https://github.com/user-attachments/assets/d72ac399-bb74-489f-ba4f-e493220ca9b8" />

Windows: ダウンロードしたzipファイルを解凍し、中にある `OpenUtau.exe` を実行してください。

OSX: ダウンロードしたdmgファイルを開き、表示される手順に従ってインストールしてください。

Linux: ターミナルを開き、`chmod +x Downloads/[file].AppImage` ファイルに実行権限を与えます。その後、ダウンロードしたAppImageファイルを実行してください。

## 使い方

1. WORLDLINE-Rを使用していることを確認してください。

<img width="301" height="106" alt="image" src="https://github.com/user-attachments/assets/0354b417-215a-4de7-95e5-12af4f26099a" />

2. このメニューを開きます：

<img width="295" height="97" alt="image" src="https://github.com/user-attachments/assets/cff18af9-f9e8-48b8-8c85-da44f12aa35b" />

3. そして、プロジェクト設定をクリックします。このようなメニューが表示されるはずです：

<img width="348" height="229" alt="image" src="https://github.com/user-attachments/assets/ac19d722-5ef5-430f-8c75-3ef5e1e76083" />

`Equal Temperament (Divisions per octave)`の項目で、使用したい平均律の分割数を設定します（通常、下の2つのオプションを変更する必要はありません）。

<img width="493" height="423" alt="image" src="https://github.com/user-attachments/assets/25fcf692-802f-4723-8366-27745850146a" />
