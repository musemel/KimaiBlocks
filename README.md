# Kimai Blocks

<img src="Assets/KimaiBlocks.png" width="96" alt="Kimai Blocks icon">

Kimaiの実績を5分刻みのタイムブロックで管理する、Windows向け非公式クライアントです。C# / WPF / .NET 10で実装しています。現在はプロトタイプです。

## 主な機能

- 週カレンダーで実績を作成・ドラッグ移動・サイズ変更。未来の実績も入力可能
- プロジェクトの検索・表示選択、アクティビティツリー、お気に入り、フォルダ分類
- 週・日・プロジェクト・アクティビティ別の統計
- 休日と休み時間の暗色表示・入力禁止、土日の表示切り替え
- 起動時の自動接続、定期保存、終了時保存、未保存変更のキャッシュ
- グローバルアクティビティを有効にしたプロジェクトの追加

## 必要環境

- WindowsでWPFを実行できる環境
- 開発時: .NET 10 SDK
- 実行時: .NET 10 Desktop Runtime
- アクセス可能なKimaiサーバーと、そのユーザーのAPIトークン

Kimai 2.67.0で基本的な実績CRUDを確認しています。すべてのバージョンや独自設定との互換性を保証するものではありません。

## ビルドと起動

リポジトリのルートでPowerShellを開きます。

```powershell
./Build.ps1
./dist/KimaiBlocks.exe
```

初回ビルドにはNuGetからの依存パッケージ取得が必要です。実行ファイルだけでなく、`dist` フォルダ全体を配布してください。ビルド済みバイナリはこのソース一式には含まれません。

初回起動時の設定画面でKimai URLとAPIトークンを入力します。通常のログインパスワードは使いません。次回から保存した設定で自動接続します。

## 操作・設定

詳しくは [操作ガイド](docs/USAGE.md) を参照してください。

保存間隔は既定60秒です。保存失敗時は変更を保持し、通常の終了を中止します。HTTP接続は設定で明示的に許可した場合のみ使用できます。

接続設定とキャッシュは `%LOCALAPPDATA%/KimaiBlocks` に保存されます。APIトークンはWindows DPAPIで暗号化します。実績キャッシュは平文です。このディレクトリをリポジトリへ追加しないでください。

## 開発・テスト

```powershell
dotnet build -c Release
dotnet ./bin/Release/net10.0-windows/KimaiBlocks.dll --self-test
```

`--self-test` は模擬HTTP通信を使い、実サーバーを変更しません。対話可能なWindowsデスクトップでは `--sync-test` で保存周期・終了制御も検証できます（テスト用ウィンドウが開きます）。

| ファイル | 内容 |
| --- | --- |
| App.cs / Sidebar.cs / Statistics.cs | カレンダー・ツリー・統計UI |
| Connection.cs / PendingQueue.cs | 接続・定期保存・終了制御 |
| KimaiService.cs / CatalogCache.cs | APIクライアント・一覧キャッシュ |
| CalendarRules.cs / ProjectCreation.cs | 休日設定・プロジェクト追加 |
| *Tests.cs | 模擬通信・保存処理のテスト |
| Assets/ / AssetsIcon.py | 独自アイコンと生成スクリプト |

GitHub ActionsでWindows上のビルドと `--self-test` を実行します。実サーバーの認証情報や専用のセットアップスクリプトは含めていません。

## 現在の制限

- Undo、複数選択、ドラッグ中の自動スクロールは未実装
- 計測中、エクスポート済み、休憩付き、日またぎ、5分単位以外の実績は読み取り専用
- 統計はブロック時間の合計。正確な請求時間や休憩控除はKimaiで確認
- 保存前の競合確認は行いますが、サーバーとの完全な排他制御はありません

## ライセンス

[MIT License](LICENSE)。商用利用・改変・再配布が可能です。著作権表示とライセンス文を保持してください。無保証です。独自アイコンも同じライセンスです。

依存ライブラリについては [THIRD_PARTY.md](THIRD_PARTY.md) を参照してください。本プロジェクトはKimai公式プロジェクトではありません。
