# KuchiPaku - the YMM4 lip-sync support tool

**KuchiPaku** (クチパク) は[YMM4（ゆっくりムービーメーカー4）](https://manjubox.net/ymm4/)むけの「あいうえお口パク（リップシンク）」を生成するツールです。

The **KuchiPaku** (pronounce: `"coo-chee-park"`, meaning: lip-sync) is a lip-sync generator tool for the [YMM4 (Yukkuri Movie Maker 4)](https://manjubox.net/ymm4/).

## What's KuchiPaku?

YMM4がサポートしていない「あいうえお口パク」を作成するツールです。

**KuchiPaku**上で、キャラクターのそれぞれの口の形を指定することで、発音のタイミングに合わせて「表情アイテム」を自動でYMM4のタイムラインに並べます。

主な読み上げソフトのほか、歌声合成ソフトのあいうえお口パクにも利用できます。

[![alt設定](http://img.youtube.com/vi/0ibptgYs0VI/0.jpg)](https://www.youtube.com/watch?v=0ibptgYs0VI)

作った経緯はブログ記事でちょっと書いてます。
[YMMであいうえお口パクした～い！](https://note.com/inuinu_/n/n6e94d0a88edc)

## Supported YMM4 features

**KuchiPaku**はYMM4の以下の機能に対応、または対応予定です。
- 立ち絵形式
  - 「[動く立ち絵](https://manjubox.net/ymm4/faq/%E7%AB%8B%E3%81%A1%E7%B5%B5%E6%A9%9F%E8%83%BD/%E5%8B%95%E3%81%8F%E7%AB%8B%E3%81%A1%E7%B5%B5%E7%B4%A0%E6%9D%90%E3%81%AE%E4%BD%9C%E3%82%8A%E6%96%B9/)」
    - 動く立ち絵をあいうえお口パクできます
- ボイス形式
  - 「[カスタムボイス](https://manjubox.net/ymm4/faq/%E3%82%86%E3%81%A3%E3%81%8F%E3%82%8A%E3%83%9C%E3%82%A4%E3%82%B9/%E5%A4%96%E9%83%A8%E3%81%AE%E9%9F%B3%E5%A3%B0%E5%90%88%E6%88%90%E3%82%A8%E3%83%B3%E3%82%B8%E3%83%B3%E3%81%A7%E4%BD%9C%E6%88%90%E3%81%97%E3%81%9F%E9%9F%B3%E5%A3%B0%E3%83%95%E3%82%A1%E3%82%A4%E3%83%AB%E3%82%92%E4%BD%BF%E7%94%A8%E3%81%97%E3%81%9F%E3%81%84/)」
    - タイミング情報ファイル(`.lab`ファイル)を用意する必要があります
  - 以下のTTSソフトのAPI連携ボイス（**対応予定**）
    - [CeVIO Creative Studio](https://cevio.jp/product/ccs/) (SAPI5除く)
    - [CeVIO AI](https://cevio.jp/) (SAPI5除く)
    - [VOICEVOX](https://voicevox.hiroshiba.jp/)
    - [SHAREVOX](https://www.sharevox.app/)

## Install

- githubの[Releases](https://github.com/InuInu2022/KuchiPaku/releases)から最新のものをダウンロードしてください。
  - zipファイルを展開し、お好きな場所に設置してください
- **github以外からの配布は行っていません。**
  - github以外で配布されていた場合、ウイルスやトロイの木馬などの不正なプログラムの可能性があります。絶対にダウンロードしないでください。
- アップデートの際はそのまま上書きしてください
- アンインストールはフォルダ毎削除でOKです

## Requirements

動作環境
- Windows 10 64bit 18362以降
- [.NET 6 Runtime](https://dotnet.microsoft.com/ja-jp/download/dotnet/6.0/runtime)

## How to use

1. YMM4でプロジェクトを作ります
2. あいうえお口パクさせたいキャラの立ち絵とセリフアイテムを配置します
   - 立ち絵の種類は「動く立ち絵」、セリフの種類は「カスタムボイス」にしてください
3. プロジェクトを保存し、**YMM4を終了します**
4. KuchiPakuで保存したプロジェクト(`.ymmp`)を読み込みます
5. 「キャラ口パク設定」で設定するキャラを選びます
6. 右側であ行・い行などの口の形を指定します
7. すべてのキャラ設定が終わったらプロジェクトを保存します
8. YMM4で再度プロジェクトを開いて、続きを編集します

## Timing label file (`.lab`)

**KuchiPaku**は一部の音声合成ソフトがサポートするタイミング情報ファイル(`.lab`)を利用して口パクを生成します。`.lab`ファイルが用意できれば以下に記載がないソフトでも対応している可能性があります。

- 公式に出力サポートしているソフト (officialy supported tools)
  - 読み上げソフト (TTS tools)
    - [CeVIO Creative Studio](https://cevio.jp/product/ccs/)
    - [CeVIO AI](https://cevio.jp/)
    - [A.I.VOICE](https://aivoice.jp/)
    - [VOICEVOX](https://voicevox.hiroshiba.jp/)
  - 歌声合成ソフト (Vocal singing synth tools)
    - [CeVIO Creative Studio](https://cevio.jp/product/ccs/)
    - [CeVIO AI](https://cevio.jp/)
    - [NEUTRINO](https://studio-neutrino.com/)
    - [VoiSona](https://voisona.com/)
- タイミング情報ファイルを生成するツール(`.lab` file generator tool)
  - こちらのツールを利用することで`.lab`ファイルを生成できます
  - [Wav2Lab](https://www.nicovideo.jp/watch/sm34735545) by [@Auxilyrica](https://twitter.com/Auxilyrica)

## Projects

- `KuchiPaku.Core`: Independent common component project includes MVVM `Model` code.
- `KuchiPaku`: The WPF application project includes MVVM `View` and `ViewModel` code.

## Libraries

- [Epoxy](https://github.com/kekyo/Epoxy)
- [FluentWPF](https://github.com/sourcechord/FluentWPF)
- [ModernWpfUI](https://github.com/Kinnara/ModernWpf)
- [Enterwell Client WPF - Notifications](https://github.com/Enterwell/Wpf.Notifications)
- [Microsoft-WindowsAPICodePack-Shell](https://github.com/contre/Windows-API-Code-Pack-1.1)
- [NLog](https://nlog-project.org/)
- [Json.NET](https://github.com/JamesNK/Newtonsoft.Json)
- [MinVer](https://github.com/adamralph/minver)

## 🐶Developed by InuInu

- InuInu（いぬいぬ）
  - YouTube [YouTube](https://bit.ly/InuInuMusic)
  - Twitter [@InuInuGames](https://twitter.com/InuInuGames)
  - Blog [note.com](https://note.com/inuinu_)
  - niconico [niconico](https://nico.ms/user/98013232)
