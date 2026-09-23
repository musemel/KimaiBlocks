# Third-party dependencies

本プロジェクトのソースと独自アイコンにはMITライセンスを適用します。依存ライブラリには、それぞれのライセンスが適用されます。SDKのソースは同梱していません。

以下は開発時に解決されたNuGetパッケージのメタデータです。復元時の正確な一覧は `obj/project.assets.json` で確認できます。

| Package | Version | License | Authors |
| --- | --- | --- | --- |
| MarkZither.KimaiDotNet.ApiClient | 1.0.0 | MIT | Mark Burton |
| Microsoft.Extensions.DependencyInjection.Abstractions | 6.0.0 | MIT | Microsoft |
| Microsoft.IO.RecyclableMemoryStream | 3.0.1 | MIT | Microsoft |
| Microsoft.Kiota.Abstractions | 1.22.1 | MIT | Microsoft |
| Microsoft.Kiota.Bundle | 1.22.1 | MIT | Microsoft |
| Microsoft.Kiota.Http.HttpClientLibrary | 1.22.1 | MIT | Microsoft |
| Microsoft.Kiota.Serialization.Form | 1.22.1 | MIT | Microsoft |
| Microsoft.Kiota.Serialization.Json | 1.22.1 | MIT | Microsoft |
| Microsoft.Kiota.Serialization.Multipart | 1.22.1 | MIT | Microsoft |
| Microsoft.Kiota.Serialization.Text | 1.22.1 | MIT | Microsoft |
| Std.UriTemplate | 2.0.8 | Apache-2.0 | Std.UriTemplate |

- [KimaiDotNet.ApiClient](https://github.com/KimaiDotNet/KimaiDotNet.ApiClient)
- [Microsoft Kiota](https://github.com/microsoft/kiota)

バイナリを再配布するときは、使用した各パッケージの著作権表示・ライセンス文書も添付してください。NuGetキャッシュ内の `.nuspec` およびLICENSEファイルを参照してください。Kimaiサーバー自体は本リポジトリに含まれません。
