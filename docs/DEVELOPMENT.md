# 開発ガイド

[READMEへ戻る](../README.md)

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

## バージョン

KimaiBlocks.csproj の Version は日本時間の 年.月.日.時分 です。DLL・EXEのファイルバージョンでアップデートを比較します。同じ分に再配布する場合も単調に増える値にしてください。
