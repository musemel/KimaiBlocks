using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

public partial class Blocks {
 internal static ConnectionSettings DefaultPreferences(ConnectionSettings current,string json) {
  var initial=SettingsLayers.Load(json);
  foreach(string name in new[]{"Accounts","ActiveAccountId","Url","Username","ProtectedToken","Legacy","AllowHttp","UserId"}) {
   var property=typeof(ConnectionSettings).GetProperty(name);property.SetValue(initial,property.GetValue(current));
  }
  return JsonSerializer.Deserialize<ConnectionSettings>(JsonSerializer.Serialize(initial));
 }
 void ResetPreferencesFromJson(string json) {
  var previous=settings;var candidate=DefaultPreferences(previous,json);
  settings=candidate;try{StoreSettings(false);}catch{settings=previous;throw;}
  defaultsJson=json;zoomTimer.Stop();zoomScale=1;board.LayoutTransform=Transform.Identity;
  RestoreViewPreferences();if(saveTimer.IsEnabled)ConfigureTimer();rangeStart=null;
  Populate();Render();UpdateViewTools();status.Text="アカウント設定を保持して、その他の設定を初期値に戻しました。";
 }
 void ResetPreferencesDialog() {
  if(communicating||dragActive||!ApplyEditor())return;
  if(MessageBox.Show(this,"アカウント以外の設定を初期値に戻しますか？\n\n対象：表示・分類、倍率・パネル幅、休日・休み時間・時間外、保存間隔・キャッシュ、更新先、配色方式、集計設定。\nEXE隣の defaults.json（なければアプリ標準値）を使い、強制設定は引き続き優先します。\n\nアカウント・APIトークン・選択中の接続先、実績と未保存の編集、フォルダ・お気に入り・プロジェクト個別設定は保持します。","設定を初期値に戻す",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
  try {string path=Path.Combine(AppContext.BaseDirectory,"defaults.json");ResetPreferencesFromJson(File.Exists(path)?File.ReadAllText(path):null);}
  catch(Exception ex){MessageBox.Show(this,ReportSettingsError(ex),"設定を初期値に戻せません");}
 }
}
