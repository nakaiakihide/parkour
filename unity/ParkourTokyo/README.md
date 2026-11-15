# ParkourTokyo（Unity版）

Web 試作（Three.js 単一 HTML）と**同じ街データ・同じ物理スペック**で動く Unity 側の土台プロジェクト。
データは Web 版から書き出した JSON を共有し、Web で調整した数値（現実×1.5倍盛り）をそのまま移植してある。

## セットアップ（Mac / Windows）

1. Unity Hub で **Unity 2022.3 LTS**（プロジェクトは 2022.3.50f1 で作成。それ以降の 2022.3 でも可）をインストール
2. Unity Hub →「開く」でこの `ParkourTokyo` フォルダを選択（初回はインポートに少し時間がかかる）
3. 新規シーンを作成し、空の GameObject を置いて **GameBootstrap** コンポーネントを追加
4. そのシーンを `Assets/Scenes/Main.unity` として保存し、File → Build Settings の Scenes in Build に追加
5. ▶ 再生で渋谷コースが読み込まれ、スポーンから走れる

## 操作

| 入力 | アクション |
|---|---|
| WASD / 左スティック（タッチ・マウス） | 移動（カメラ基準） |
| Space / ジャンプボタン | ジャンプ（長押しで高く、壁際では壁キック、壁沿いでは壁走り） |
| Shift / ダッシュボタン | ダッシュ（13m/s・0.85秒・CD1.4秒） |
| S / スライドボタン | スライディング |
| C / ロールボタン | ロール予約（落下直前に押すと受け身） |
| ドラッグ（右側） | 視点 |
| 1 / 2 / 3 | コース切替（渋谷 / 池袋 / 新宿） |
| R | ゴール後にリスタート |

縁に近づいて落下すると自動でつかむ → ぶら下がり（0.18秒）→ よじ登る。CP（黄色い柱）を5つ回るとゴール（最後は赤）。

## 構成

- `Assets/StreamingAssets/city/*.json` — コースデータ（Web 版から `rebuild/export_unity.py` で生成。**Web 側のデータを更新したら再実行して上書き**）
- `Assets/Scripts/`
  - `GameBootstrap.cs` — 起動・組み立て・コース切替
  - `CityData.cs` — JSON の受け口（JsonUtility 用にフラット配列化したスキーマ）
  - `CityBuilder.cs` — 建物（フットプリント×高さの柱）・道路リボン・地面の生成、屋根情報と**レッジ（掴める縁）線分の事前計算**
  - `PolyUtil.cs` — ポリゴン距離・内包判定・耳切り法の三角形分割
  - `PlayerController.cs` — 移動物理の移植（コヨーテ/先行入力/壁走り/縁つかみ/ダッシュ/スライド/受け身）
  - `CourseManager.cs` — スポーン選定と CP チェーン（Web 版 placeCheckpoints の canGo ルール）、タイマー、ベスト保存（PlayerPrefs）
  - `FollowCamera.cs` / `TouchControls.cs` — カメラとタッチ入力（IMGUI の土台実装）

## Web 版との連携

- 正は Web 版のリポジトリ（`nakaiakihide/parkour` の `index.html`）。この Unity プロジェクトは同じリポジトリの `unity/ParkourTokyo/` に置いてある
- 街データを更新する手順: Web 側で抽出 → `python3 rebuild/export_unity.py [対象HTML]` → 生成された JSON をコミット
- 物理スペックを変更するときは Web 版と Unity 版の両方を揃えること（数値は PlayerController 先頭に集約）

## 既知の制限・次の段階（Phase 2）

- **Android 実機では StreamingAssets を File で直接読めない**（jar 内）。`GameBootstrap.LoadCourse` を UnityWebRequest 読み込みに差し替えること
- 建物の見た目は単色の柱のみ。LOD2 サーフェス（JSON には同梱済み）で屋根形状を再現するのが次の作業
- 看板・駅名標・電車・高架は未実装（データは JSON にある: names / railPath / station）
- プレイヤーの見た目は仮カプセル。アニメーション付きキャラと IK 吸着は移行メモ（`../../unity-port-memo.md`）の順序で
- UI（ロビー等）は Web 版のデザインが正。Unity 側は uGUI で再現する前提で、現状は IMGUI の仮 HUD のみ
- 物理は Update ベース（端末差が出やすい）。固定タイムステップ化（Web 版は 120Hz）が Phase 2 の候補
- 将来の見た目の本命は Cesium 経由の Google フォトリアル 3D タイル（表示専用）＋ 判定は PLATEAU、という Web 側で決めた方針通り
