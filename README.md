# chess-rogue-proto - Chess Auto-Battler (Unity製オートバトラー)

チェスの駒とシャンチー（中国将棋）由来の特性を融合させた、Unity製のオートバトラー系ローグライトです。
盤面配置・合成進化・装備ハクスラといった要素を、すべて**ScriptableObjectによるデータ駆動設計**で構築し、
Inspector上の数値調整だけでバランス変更が完結する拡張性の高いアーキテクチャを目指して実装しました。

---

## 📷 ゲームプレイ画面 / Screenshots

<img width="690" height="388" alt="gameplay_01" src="https://github.com/user-attachments/assets/824b9aaa-9ab5-42e8-9671-728d28bf74b9" />

<img width="691" height="387" alt="gameplay_02" src="https://github.com/user-attachments/assets/4c8d067a-3f6a-49db-a538-8686dbec0127" />

---
<!--
## ♟️ 駒デザイン ギャラリー / Piece Models

各駒は西洋チェスとシャンチー（中国将棋）双方の元ネタを踏まえてデザインしており、
見た目だけでなく行動ロジックにもその由来を反映しています（例: ナイトの「蹩馬腿」、
ビショップの守備的な近接ペナルティ等）。

| ポーン | ナイト | ルーク |
|:---:|:---:|:---:|
| ![ポーン](Images/models/pawn.png) | ![ナイト](Images/models/knight.png) | ![ルーク](Images/models/rook.png) |

| ビショップ | クイーン | キング |
|:---:|:---:|:---:|
| ![ビショップ](Images/models/bishop.png) | ![クイーン](Images/models/queen.png) | ![キング](Images/models/king.png) |

| パラディン | 精鋭騎兵（融合進化） |
|:---:|:---:|
| ![パラディン](Images/models/paladin.png) | ![精鋭騎兵](Images/models/elite_cavalier.png) |

*※各画像を該当駒の3Dモデルのレンダリング画像/スクリーンショットに差し替えてください*

--- -->

## 💡 技術的工夫・設計のこだわり

* **ScriptableObjectによる完全データ駆動設計**
  駒の基礎ステータス・行動ロジック用パラメータ・進化ルール・敵AIパターン・融合レシピなど、
  ゲームバランスに関わる数値をすべて`ScriptableObject`（`UnitStatusDataSO` / `EvolutionRuleDataSO` /
  `AIBehaviorDataSO` / `FusionRecipeDataSO`等）として外部化。コードの再コンパイルなしに
  Inspector上でバランス調整・新規コンテンツ追加が可能な構成にしています。

* **育成履歴に応じた分岐進化システム**
  ★1→★2進化時に選んだ強化内容（攻撃/HP/速度/吸血）の履歴を保持し、★2→★3への合成時に
  3体の履歴傾向を判定して派生駒（固有のフレーバー名+追加ステータス）へ自動的に進化する
  システムを実装。さらに、異なる駒種同士を組み合わせる「融合進化」（例: ポーン×ナイト→
  精鋭騎兵）にも対応し、単純な数値強化に留まらない育成の分岐を持たせています。

* **複数パターンを持つ戦闘AI**
  駒のターゲット選定ロジックを「バランス型」「弱者優先型」「本命特攻型」等の複数パターンで
  ScriptableObject管理し、敵ウェーブ単位・プレイヤー駒単位のどちらでも個別に設定できる設計に
  しています。プレイヤーは自分の駒それぞれに戦術を割り当てることで、単なる配置ゲーム以上の
  戦略性を持たせています。

* **プレイヤー主導の合成/融合選択UI**
  合成・融合の対象駒を自動選択するのではなく、盤面上のクリックと一覧リストの両方から
  プレイヤー自身が使用する駒を選べる手動選択モードを実装。選択状態は双方向に同期し、
  確定するまで実行されない設計にすることで、誤操作を防ぎつつ戦略的な選択を可能にしています。

* **パフォーマンスを意識した駒管理**
  当初`FindObjectsOfType`によるシーン全体走査に依存していたターゲット探索処理を、
  スポーン/撃破に連動して自己登録・解除される`PieceRegistry`（駒レジストリ）へ置き換え、
  駒数増加時のスケーラビリティを確保しています。

---

## 🛠️ 使用技術・ツール

* **エンジン**: Unity 6.5
* **言語**: C#
* **UI**: UGUI + TextMeshPro
* **入力**: 新Input System（`UnityEngine.InputSystem`）
* **設計パターン**: ScriptableObjectによるデータ駆動設計 / シングルトンパターン（各種UIポップアップ）

---

## 📁 ファイル構成

```text
├── Assets/
│   ├── Scripts/         # ゲームロジック一式（駒AI、UI制御、データ管理等）
│   ├── ScriptableObjects/ # UnitStatusData, EvolutionRuleData, AIBehaviorData 等の設定アセット
│   ├── Prefabs/         # UIプレハブ（合成ボタン、装備スロット等）
│   └── Models/          # 駒の3Dモデル
└── Images/
    ├── gameplay_*.png   # ゲームプレイスクリーンショット
    └── models/          # 駒モデルのレンダリング画像
```

---

## 🎮 遊び方（任意で追記）

*※起動方法、操作方法、目標（ウェーブをクリアして生き残る、等）を簡単に記載*
