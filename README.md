# KuchiPaku - a YMM4 lip-sync support tool

**KuchiPaku** (クチパク) は[YMM4（ゆっくりムービーメーカー4）](https://manjubox.net/ymm4/)むけの「あいうえお口パク（リップシンク）」を生成するツールです。

**KuchiPaku** (pronounce: "coo-chee-park", meaning: lip-sync) is a generator tool for the [YMM4 (Yukkuri Movie Maker 4)](https://manjubox.net/ymm4/).

## What's KuchiPaku?

YMM4がサポートしていない「あいうえお口パク」を作成するツールです。

KuchiPaku上で、キャラクターのそれぞれの口の形を指定することで、発音のタイミングに合わせて「表情アイテム」を自動でYMM4のタイムラインに並べます。

主な読み上げソフトのほか、歌声合成ソフトのあいうえお口パクにも利用できます。

## Supported YMM4 features

- 立ち絵形式
  - 「[動く立ち絵](https://manjubox.net/ymm4/faq/%E7%AB%8B%E3%81%A1%E7%B5%B5%E6%A9%9F%E8%83%BD/%E5%8B%95%E3%81%8F%E7%AB%8B%E3%81%A1%E7%B5%B5%E7%B4%A0%E6%9D%90%E3%81%AE%E4%BD%9C%E3%82%8A%E6%96%B9/)」
    - 動く立ち絵をあいうえお口パクできます
- ボイス形式
  - 「カスタムボイス」
    - タイミング情報ファイル(`.lab`ファイル)を用意する必要があります
  - 以下のTTSソフトのAPI連携ボイス
    - [CeVIO Creative Studio](https://cevio.jp/product/ccs/) (SAPI5除く)
    - [CeVIO AI](https://cevio.jp/) (SAPI5除く)
    - VOICEVOX

## Timing label file (`.lab`)

KuchiPakuは一部の音声合成ソフトがサポートするタイミング情報ファイル(`.lab`)を利用して口パクを生成します。

- 公式に出力サポートしているソフト (official export support tools)
  - 読み上げソフト (TTS)
    - [CeVIO Creative Studio](https://cevio.jp/product/ccs/)
    - [CeVIO AI](https://cevio.jp/)
    - A.I.VOICE
    - VOICEVOX
  - 歌声合成ソフト (Vocal singing synth)
    - [CeVIO Creative Studio](https://cevio.jp/product/ccs/)
    - [CeVIO AI](https://cevio.jp/)
    - NEUTRINO
- タイミング情報ファイルを生成するツール
  - [Wav2Lab](https://www.nicovideo.jp/watch/sm34735545) by @Auxilyrica

## Projects

- `KuchiPaku.Core`: Independent common component project includes MVVM `Model` code.
- `KuchiPaku`: The WPF application project includes MVVM `View` and `ViewModel` code.

## Libraries

- [Epoxy GitHub repository](https://github.com/kekyo/Epoxy)
