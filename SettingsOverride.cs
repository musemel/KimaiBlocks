using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

public sealed class ManagedSettings {
 public string UpdateFolder,ActivityGroupingPattern;
 public static ManagedSettings Read(string json) {
  var data=JsonNode.Parse(json) as JsonObject??throw new ArgumentException("管理設定はJSONオブジェクトで指定してください。");var result=new ManagedSettings();
  foreach(var item in data){if(item.Key!="UpdateFolder"&&item.Key!="ActivityGroupingPattern")throw new ArgumentException("管理対象外の設定: "+item.Key);if(item.Value is not JsonValue value||!value.TryGetValue<string>(out var text))throw new ArgumentException(item.Key+"は文字列で指定してください。");if(item.Key=="UpdateFolder")result.UpdateFolder=text;else {_ = new ActivityGrouping(text);result.ActivityGroupingPattern=text;}}
  return result;
 }
 public static void Tests(){var value=Read("{\"UpdateFolder\":\"\",\"ActivityGroupingPattern\":\"(.*)/(.*)\"}");if(value.UpdateFolder!=""||value.ActivityGroupingPattern==null||Read("{}").UpdateFolder!=null)throw new Exception("Managed setting presence");bool failed=false;try{Read("{\"SaveSeconds\":20}");}catch(ArgumentException){failed=true;}if(!failed)throw new Exception("Unsupported policy accepted");}
}
public partial class Blocks {
 ManagedSettings managedSettings=new ManagedSettings();
 string EffectiveUpdateFolder=>managedSettings.UpdateFolder??settings.UpdateFolder;
 string EffectiveActivityPattern=>managedSettings.ActivityGroupingPattern??settings.ActivityGroupingPattern;
 void ApplyStartupOverrides() {
  managedSettings=new ManagedSettings();string path=Path.Combine(AppContext.BaseDirectory,"settings.policy.json");if(!File.Exists(path))return;
  try {managedSettings=ManagedSettings.Read(File.ReadAllText(path));}
  catch(Exception ex){MessagePolicyError(path,ex);}
 }
 void MessagePolicyError(string path,Exception ex)=>System.Windows.MessageBox.Show(this,"管理設定を読み込めません。通常の設定で起動します。\n"+path+"\n"+ex.Message,"管理設定のエラー");
}

