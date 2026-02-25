[English version is here](./README.md)

# OpenUtau_microtonal

** このプロジェクトは現在開発中のため、動作が不安定な場合があります。 **

これは、歌声合成ソフト OpenUTAU に微分音機能を追加するためのフォーク版です。現時点では、WORLDLINE-R レンダラー、~~classic レンダラー、VOICEVOX~~使用時のみ、平均律及び .tun ファイルの読み込みに対応しています。

> [!WARNING]
> ### ブラウザ版に関する注意点
> **このブランチは、WebAssembly を使用してブラウザ上で動作するように変更されています。** そのため、デスクトップ版と比較して以下の制限事項があることにご注意ください：
> - **仮想ファイルシステム**: ブラウザのサンドボックス内で動作するため、ローカルファイルに直接アクセスすることができません。プロジェクト、音源、オーディオファイルを読み込むには、ブラウザのファイル選択ダイアログを使用して読み込む必要があります。
> - **パフォーマンス**: WebAssembly で実行されているため、オーディオレンダリングはメインスレッドで同期的に実行されます。そのため、再生やエクスポートなどの重い処理中に UI が一時的にフリーズする可能性があります。
これは設定の再生→Playback Buffer Sizeを増やすことで緩和できます。

OpenUTAU本体に関する詳しい情報は、[OpenUTAUのリポジトリ](https://github.com/stakira/OpenUtau)をご覧ください。
サポートについては、Discordで開発者 @takunnma に連絡するか、彼が参加しているこちらの[サーバー](https://discord.gg/k3Cp7kW6Jv)にて、本人に直接お問い合わせください。

## 使い方

1. WORLDLINE-Rを使用していることを確認してください。

<img width="301" height="106" alt="image" src="https://github.com/user-attachments/assets/0354b417-215a-4de7-95e5-12af4f26099a" />

2. このメニューを開きます：

<img width="295" height="97" alt="image" src="https://github.com/user-attachments/assets/cff18af9-f9e8-48b8-8c85-da44f12aa35b" />

3. そして、プロジェクト設定をクリックします。このようなメニューが表示されるはずです：

<img width="348" height="229" alt="image" src="https://github.com/user-attachments/assets/ac19d722-5ef5-430f-8c75-3ef5e1e76083" />

`Equal Temperament (Divisions per octave)`の項目で、使用したい平均律の分割数を設定します（通常、下の2つのオプションを変更する必要はありません）。

<img width="493" height="423" alt="image" src="https://github.com/user-attachments/assets/25fcf692-802f-4723-8366-27745850146a" />
