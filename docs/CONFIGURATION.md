# 設定・配布ガイド

[READMEへ戻る](../README.md) · [操作](USAGE.md) · [集計](REPORTS.md)

## 必要なKimai権限

管理者である必要はありません。ロールはサーバーでカスタマイズできるため、権限名で確認してください。

| 用途 | 権限・条件 |
| --- | --- |
| API接続 | `api_access` と有効なAPIトークン |
| 自分のトークン発行 | `api-token_own_profile`（Kimaiのプロフィール画面） |
| 自分の実績の参照・作成・編集・削除 | `view_own_timesheet`, `create_own_timesheet`, `edit_own_timesheet`, `delete_own_timesheet` |
| プロジェクト追加 | `create_project`、追加先の顧客にアクセスできること |
| 一覧や登録先の利用 | Kimaiのチーム割当・プロジェクト／顧客／アクティビティの公開範囲で対象にアクセスできること。サーバーで一覧・管理機能が制限されている場合は `view_project`, `view_activity`, `view_customer` も確認 |

請求対象は操作画面を設けずAPIにも送信しません。既存の値は維持し、新規作成はKimaiの既定値に従います。通常の実績保存で請求対象・単価・エクスポート・他人の実績の編集権限は不要です。未来入力・締め期間・プロジェクト期間などのサーバー側制約は権限とは別に適用されます。
参照: [公式の権限一覧](https://www.kimai.org/documentation/permissions.html)、[実績の権限](https://www.kimai.org/documentation/timesheet.html)、[チーム](https://www.kimai.org/documentation/teams.html)。

## 設定の移行と配布

アプリを終了し、`%LOCALAPPDATA%\KimaiBlocks` **全体**を移行先の同じ場所へコピーしてください。`connection.json`、`token.key`、各アカウントのキャッシュが対象です。移行先の既存フォルダは必要なら別途バックアップしてください。

鍵はPCやWindowsユーザーに依存しません。**このフォルダと鍵を入手した人はトークンを復号できます**。移行データは認証情報として扱ってください。旧DPAPI形式は元のWindowsユーザーで新版を一度起動すると移行されます。旧形式を先に別PCへコピーした場合はAPIキーの再入力が必要です。APIキーはアカウント編集画面では表示され、一覧ではマスクされます。

EXEの隣の `defaults.json` は、**初回起動・保存されていない項目・アカウント追加時**の初期値です。既に保存した項目は保持し、不足する項目だけを補います。新しいアカウントのURLなどは現在選択中のアカウントからではなく、このファイルから取得します。`AllowHttp` も指定できます。APIキーやユーザー別情報は配布用ファイルに入れないでください。強制設定がある項目は `settings.override.json` が最優先です。

```json
{
  "Url": "https://kimai.example.com",
  "SaveSeconds": 60,
  "CatalogMinutes": 60,
  "UpdateFolder": "C:\\CompanyApps\\KimaiBlocks",
  "Calendar": {
    "DaysOff": [0, 6],
    "Holidays": "",
    "Breaks": "12:00-13:00",
    "OffHours": "00:00-09:00\n18:00-24:00",
    "Shade": true,
    "BlockInput": false
  }
}
```

「メニュー → 更新・配布設定 → 自動保存・キャッシュ・更新先」のアップデート確認フォルダにはローカルフォルダまたはUNCパスを指定します。配布側はビルドした **dist一式**をその場所へ配置します。起動時とメニューから `KimaiBlocks.dll` と `KimaiBlocks.exe` のファイルバージョンを確認し、読み取れた新しい方を現在のバージョンと比較します。DLLがないランタイム同梱の単一EXE配布にも対応し、新版があれば通知します。実行・インストール・ファイル置換は自動では行いません。確認はバックグラウンドで行い、8秒で待機を打ち切ります。起動時に確認できなかった場合は通知せず、メニューの手動確認でエラーを確認できます。空欄で無効です。

バージョンは `年.月.日.時分`（日本時間）です。例: `2026.9.27.549` は2026年9月27日05:49更新。変更時に `KimaiBlocks.csproj` の `Version` を更新してください。同じ分に複数版を配布する場合は次の分の値など、より大きな値にします。

### 常設の管理設定

EXEの隣に **settings.override.json** を配置すると、指定された項目が保存済み設定・配布用初期値より優先されます。該当する設定画面や切替ボタンはグレーアウトします。指定していない項目は変更できます。入れ子の `Calendar` も指定した項目だけを上書きします。

```json
{
  "Url": "https://kimai.example.com",
  "ActivityGroupingPattern": "(.*)/(.*)"
}
```

[設定例](settings.override.example.json)を編集して使用してください。URLは全アカウントと新規アカウントに適用します。`UpdateFolder`、`SaveSeconds`、`Calendar.BlockInput`、`ExcludedReportUserIds` など、`ConnectionSettings` に定義された他の設定も指定できます。空文字・false・空配列も強制値です。設定名の大文字小文字は区別しません。

優先順位は **強制設定 → 保存済み設定 → defaults.json → アプリ標準値** です。新規アカウントでは保存済みアカウントの値を引き継がず、強制設定 → defaults.json → 標準値の順で初期化します。ファイルは削除せず、起動時と設定保存・接続時に強制値を適用します。適用後の値は個人設定にも保存されるため、キーを削除して再起動すると編集可能になりますが、以前の値へ自動では戻りません。

JSONの形式、未知の設定名、不正な値はエラーダイアログで通知し、接続を開始しません。旧 `settings.policy.json` も互換用に読み込みますが、同じ項目は `settings.override.json` が優先します。更新時はEXEなどと一緒に配置してください。`settings.override.pending.json` は使いません。

### アカウント以外の設定を初期値に戻す

「メニュー → 更新・配布設定 → アカウント以外の設定を初期値に戻す」で確認後に初期化できます。EXE隣の defaults.json を読み直し、なければアプリ標準値を使います。settings.override.json の強制値は引き続き優先します。表示・分類、倍率・パネル幅、カレンダーの入力制限、保存間隔・キャッシュ、更新先、配色方式、集計設定を戻します。アカウント一覧・選択中の接続先・APIトークン、実績・未保存編集、フォルダ・お気に入り・プロジェクト個別設定は保持します。

## 更新時のフォルダ確認

アップデート画面から配布フォルダと実行中のEXEフォルダを開けます。保存して終了後に配布ファイル一式をコピーしてください。実行中の自動置換は行いません。

### 入力間隔

`InputIntervalMinutes` は `5`（既定）・`10`・`15`・`20`・`25`・`30` を指定できます。`defaults.json` による初期値と `settings.override.json` による固定に対応します。
