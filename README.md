# Lunar Outpost — Junior Astronaut Mission Trainer (MVP)

NASA Space Apps Challenge 2026「Build a Junior Astronaut Mission Trainer」向けの中間発表用 MVP。
Unity 6 (6000.6.9f1, URP) で作り、WebGL (wasm) でブラウザに公開する。

> **AI の使用について**: このリポジトリのコードとデータの形は Claude Code (Anthropic) を使って生成した。
> 数値のうち出典が `TBD` のものは仮の値で、人間の確認が必要。

## 遊び方

| 操作 | キー |
| --- | --- |
| 移動 / ジャンプ | WASD・矢印 / Space |
| カメラ | 右ドラッグ、ホイールで距離 |
| 使う（乗り物に入る・出る、氷を集める） | E |
| 建設（1〜8 で選んでクリック/Enter） | B |
| 打ち上げの注文 | L |
| 居住モジュールにレゴリスを盛る | R |
| 速さ 1x / 4x、一時停止 | Tab / P |
| English / 日本語 | T |
| ヘルプ（データの出典つき） | H |

発展度（自給・安定・安全・規模の幾何平均）が 60 に届くとクリア。生命維持（酸素・水・食料）のどれかが尽きるとゲームオーバー。

## MVP に入っているもの

- **チュートリアル**（3ミッション：電気 → 放射線 → 氷と酸素）。目的地の光の柱と矢印、やってみた後の解説カード、まちがえると再挑戦できるクイズ。HUD は学んだ順に表示。チュートリアル中は時間が1/4
- 月の南極の自動生成の地形（低い太陽で長い影が動く、永久影のクレーターに氷）
- 日照の判定（太陽の方向へのレイキャスト）で太陽電池が影に入ると止まる、蓄電池
- 生命維持：氷 → 水 → 電気分解で酸素、栽培室で食料。ボンベの酸素切れで着陸船に戻される
- 放射線：表面の線量（Chang'E-4 LND の実測値）、過去の実際の DONKI イベントの警報で退避／作業継続の選択、居住モジュールのレゴリス・遮蔽の壁
- 通信：地球が沈む時期は通信が途切れ、警報と打ち上げの注文が届かない。中継衛星で回復。電波の遅れを表示
- 打ち上げ：コスト ＝ max（重さ, 容積重量）、1回の上限つき
- 発展度の4要素と、クリア・ゲームオーバー
- 英語／日本語の切り替え（文言は `Assets/Resources/i18n/*.json` だけから読む）

## MVP に入っていないもの（仕様書のステップ）

研究と改善、機器の劣化と点検、隊員の割り当て、セーブ、実績、スマホの仮想パッド、PGDA の実地形、DONKI の期間の再生、ランキング。

## 構成

```
Assets/Scripts/Sim/    純粋な C#（UnityEngine を参照しない asmdef: noEngineReferences）
Assets/Scripts/Game/   Unity 側：地形と太陽 (MoonWorld)、宇宙飛行士とカメラ (PlayerRig)、施設の見た目、ループと HUD (GameController)
Assets/Resources/data/ values.json（すべての数値と出典）、donki-events.json
Assets/Resources/i18n/ en.json, ja.json
```

シーンは空でよく、`GameController` が起動時にすべてを組み立てる。

## ビルド

Unity CLI から（エディタ起動中）:

```sh
unity command build run --output-path Builds/WebGL --confirm true
cd Builds/WebGL && python3 -m http.server 8000   # http://localhost:8000
```

WebGL の圧縮は無効（Cloudflare Pages 側で圧縮される）。

## 素材とライセンス

| 素材 | 出典 | ライセンス |
| --- | --- | --- |
| Noto Sans JP (`Assets/Resources/fonts/NotoSansJP.ttf`) | https://github.com/google/fonts/tree/main/ofl/notosansjp | SIL Open Font License 1.1 |

3D モデルはすべて Unity のプリミティブ。NASA のロゴやデザイン要素は使っていない。
